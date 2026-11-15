# ADR-0013 微信视频下载：MITM 抓取链路与按需代理

- 状态：已接受（2026-11-15 修订：PAC 按域名分流、证书会话间保留、发现入口 no-go）
- 日期：2026-11-15
- 来源：地图 [#68](https://github.com/ZLiLiang/JiuMi/issues/68)；可行性 spike [#94](https://github.com/ZLiLiang/JiuMi/issues/94)，2026-11-15 在 Weixin 4.2 实机验证

## 背景

JiuMi 要支持把微信里正在看的视频号视频下载到本地。旧 `JiumiTool`（微信 3.9）的做法是：Titanium 起本地 HTTPS 代理，只解密 `channels.weixin.qq.com`，向页面注入脚本包装 `WeixinJSBridge.invoke`，在 `H5ExtTransfer:ok` 回调里取 `object.object_desc.media[0]`。旧实现只有**在聊天里打开分享的视频**才会触发，视频号信息流里打开的视频抓不到。

现在本机微信已经升级为 Weixin 4.2（`D:\Tencent\Weixin`，网络栈含 `mmcronet`），旧 3.9 路径不复存在。spike 需要回答：4.2 是否还走系统代理、桥事件是否还触发、视频号里打开的视频能否拿到媒体信息、下载与 XOR 是否还能用。

约束与边界：

- 当前用户没有管理员权限：证书只能装 `CurrentUser`，代理只能改 WinINet 的 HTTPS 项。
- 用户要求下载的是**当前打开的视频**，不做微信界面内的下载按钮；JiuMi 自动列出、用户点下载。
- 微信版本**不锁定**，按用户通常使用的最新版验证，不写死小版本号。
- 隐私：除两个目标域名外不解密任何 HTTPS；抓包日志含 token，不进仓库。
- 仓库体积敏感；`PublishTrimmed` 兼容性归 [#66](https://github.com/ZLiLiang/JiuMi/issues/66)。

## 决策

1. **抓取形态**：短时按需的本地 PAC + Titanium.Web.Proxy。PAC 只把 `channels.weixin.qq.com` 指向本地代理，其余主机回退用户原代理（无原代理则 `DIRECT`）；`finder.video.qq.com` 抓包期直连，不经过 MITM。代理本身仍只允许解密这两个域名，实际流量只会命中页面域名。
2. **注入与回传**：在 `channels.weixin.qq.com` 的 200 HTML 的 `</body>` 前注入脚本，包装 `WeixinJSBridge.invoke`。`H5ExtTransfer:ok` 时把 `media[0]` 回传到独立的 `127.0.0.1` 随机端口 + 随机路径 listener，不复用代理端口。
3. **只认打开的视频页**：只处理 `/web/pages/feed` 的捕获。信息流首页 `/web/pages/home` 的预览视频也会逐条触发同一桥事件，但不建列表、不展示。
4. **去重**：同一次打开会连发多条事件，`oid+nid` 相同而 `token` / `decode_key` 每次新发。feed 页按 `oid+nid` 去重；不能依赖 URL 的 `m` 参数（4.2 有一部分媒体 URL 没有它）。
5. **媒体地址与请求头**：实际下载地址是 `media.url + media.url_token`。在微信外可直接 GET，不需要微信 cookie；带 `User-Agent`、`Referer: https://channels.weixin.qq.com/`、`Origin: https://channels.weixin.qq.com` 即可。封面 `thumb_url` 使用同一组请求头。
6. **大小与进度**：`media[0].file_size` 不是实际文件大小（实测字段 60.4MB / 68.7MB / 26.6MB，服务端分别为 7.93MB / 8.99MB / 2.61MB）。下载进度与 Range 一律以响应的 `Content-Length` / `Content-Range` 为准。
7. **下载与解密**：按 262144 字节 Range 分段下载。响应头 `X-encflag: 1`、`X-enclen: 131072`；**只有前 131072 字节**用 ISAAC64（seed = `decode_key`）+ `GetDecryptionArray(131072)` 生成的数组逐字节 XOR，第 131072 字节之后原样写入。`decode_key` 与 URL 绑定，换 URL 必须换 key。
8. **代理按需启停**：不常驻。用户打开视频时启动抓包，捕获到媒体信息后即可停止并还原。启动前快照 `ProxyEnable` / `ProxyServer` / `ProxyOverride` / `AutoConfigURL` / `AutoDetect`；抓包期间把 `AutoConfigURL` 指向本机随机路径的 PAC 并关闭自动检测，结束时逐项还原并刷新 WinINet。根证书只装 `CurrentUser\Root`（签名私钥在 `CurrentUser\My`），5 年有效，跨会话复用；设置页通过卸载 API 显式删除，不每次抓包重装或删除，避免反复弹信任/移除确认。
9. **隐私边界**：抓包日志只落本地 `artifacts/`（运行时目录），含 token，不提交、不外发；仓库与研究分支只保留不含 token 的结论与原型。
10. **协议失效兜底**：若桥事件不再触发，依次尝试页面 `fetch` / `XHR` 观察、MITM 侧媒体 JSON；若视频号改为原生面板、拿不到媒体信息，则该路径 no-go，不硬做。

## 为什么

**为什么 MITM，而不是直接调接口。** 媒体地址带 `encfilekey`、`token`、`basedata`、`sign` 等一串随会话生成的参数，还有一个与 URL 绑定的 `decode_key`，没有稳定的公开接口可以复现。从微信自己的页面里拿它实际请求的媒体信息，是唯一不需要逆向签名算法的路径，也和旧实现一致。

**为什么只解密两个域名。** `channels.weixin.qq.com` 承载页面与桥，`finder.video.qq.com` 承载媒体与封面。其余域名（登录、消息、支付等）与功能无关，解密它们只会扩大证书与隐私风险。

**为什么按需启停。** 实机观察：抓包期间视频号信息流曾刷不出来，停抓并还原代理后恢复；原因未定位，可能是代理链路拖慢或微信对代理有反应。常驻代理的风险与收益不成比例，短时抓包已经足够拿到当前视频。PAC 只把页面域名交给本地代理，其他应用与微信其他流量继续走原代理，进一步缩小影响面。

**为什么按域名 PAC，而不是全局固定代理。** 固定 HTTPS 代理会让所有使用系统代理的应用都经过 JiuMi，即使只解密两个域名，代理故障也会波及其他应用。PAC 只把 `channels.weixin.qq.com` 指向本地，其余主机回退用户原代理；2026-11-15 实测 Weixin 4.2 采纳该系统 PAC，聊天分享入口可正常抓取，信息流未见“只推广告/无法滑动”。

**为什么只 XOR 前 131072 字节。** 服务端的 `X-enclen: 131072` 与实测边界一致：第 131072 字节起与服务器原文逐字节相同。全文件 XOR 会破坏后半段。spike 用完整下载 + OpenCV 解码验证了这一点（576×1024，时长与 `video_play_len` 一致），用户也实际播放确认。

**为什么不信 `file_size`。** 该字段更像是原始素材或另一档码率的大小，与服务端实际返回的文件不一致。用它算进度会在 10% 左右就“下载完成”。

## 备选与取舍

| 备选 | 为什么没选 |
| --- | --- |
| 常驻本地代理，不用时也开着 | 信息流可能被拖住；长期 MITM 与隐私边界不匹配 |
| 全局固定 HTTPS 代理 | 其他应用也会被强制经过本地代理；PAC 可按域名把影响限制在页面 |
| 只支持聊天里打开的分享视频 | 不满足“视频号里打开的视频也要能下载” |
| 直接复现媒体接口与签名 | 参数随会话生成、`decode_key` 与 URL 绑定，签名算法脆弱且易失效 |
| 原生 Hook / 内存注入 | 复杂度、杀软误报与版本不锁定下的维护成本都高 |
| 用 `media[0].file_size` 算进度 | 与实际文件大小不符 |
| 对全文件 XOR | 与 `X-enclen` 和实测边界矛盾，会把后半段视频解坏 |
| 用系统 `MessageBox` 或常驻提示解释证书 | 证书信任是一次性系统弹窗，应用内只需在失败时给可理解的错误 |
| 视频号改原生面板时硬做 | 拿不到媒体信息时没有可靠 fallback，应明确 no-go |

## 后果

**正面**

- 无管理员权限也能抓：只动 `CurrentUser` 证书与 WinINet 的 PAC 项，其他应用继续走原代理。
- 聊天分享与视频号打开的视频走同一页面与同一桥事件，一套逻辑覆盖两条入口。
- 媒体地址在微信外可下载，下载器不依赖微信进程或登录态。
- 体积可控：只解密两个域名，日志不落仓库。

**负面（明确接受的代价）**

- 需要用户信任一次临时根证书；证书保留在 `CurrentUser` 直到用户显式卸载，属于明确接受的长期信任面。
- 抓包窗口内视频号信息流可能不可用，必须按需启停；PAC 按域名分流降低但不消除这个风险。
- 发现首页 `/home` 的全屏视频窗口在 4.2 不请求 `feed` 页面、也未触发 `H5ExtTransfer:ok`，发现入口暂不支持（[#171](https://github.com/ZLiLiang/JiuMi/issues/171)）；聊天分享入口可用。
- `decode_key`、token 均随会话失效；未来版本若更换加密或桥事件，需重新 spike。
- `file_size` 不可信，进度必须等响应头；首段请求失败时无法提前展示总大小。
- 原型的清理阶段可能卡住（进程、监听端口、证书残留），生产实现必须有兜底与残留提示。

**中性**

- 微信版本不锁定，本 ADR 只对验证日的 4.2.0.52 负责；升级后按同一清单复验。
- 视频清晰度跟随微信实际返回的档位，不额外请求更高码率。

## 不在本 ADR 内

- UI：导航项、列表交互、开关、证书入口、残留提示与 i18n。
- 输出文件名清洗、命名规则与去重后的展示。
- 下载调度、断点续传、并发与失败重试。
- `PublishTrimmed` 兼容性（归 [#66](https://github.com/ZLiLiang/JiuMi/issues/66)）。
- 发现首页入口（归 [#171](https://github.com/ZLiLiang/JiuMi/issues/171)）。
- token 有效期、200% 缩放、多屏/混合 DPI 的补验。

## 参考

- 地图：[#68](https://github.com/ZLiLiang/JiuMi/issues/68)
- 可行性 spike：[#94](https://github.com/ZLiLiang/JiuMi/issues/94)（[2026-11-15 结论评论](https://github.com/ZLiLiang/JiuMi/issues/94#issuecomment-6895427287)）
- 发现入口复验：[#171](https://github.com/ZLiLiang/JiuMi/issues/171)
- 原型归档：`research/94-wechat-mitm-spike` 分支（不合并、不 PR）
- 旧实现：`ZLiLiang/JiumiTool` 的 `JiumiTool2` 分支
