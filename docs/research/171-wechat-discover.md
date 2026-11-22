# #171 发现入口视频捕获复验（Weixin 4.2）

- 日期：2026-11-17
- 环境：Windows 10 19045 x64；Weixin 4.2.0.52（版本不锁定，仅记录验证日快照）；单屏 1920×1080 / 100%
- 关联：[#68](https://github.com/ZLiLiang/JiuMi/issues/68) / [#94](https://github.com/ZLiLiang/JiuMi/issues/94) / [#171](https://github.com/ZLiLiang/JiuMi/issues/171) / [ADR-0013](../adr/0013-wechat-video-download.md)

## 问题

2026-11-15 复验发现：从「发现 → 视频号」进入后，页面始终请求 `/web/pages/home`，没有 `oid/nid`；首页滑动与全屏播放都不触发 `H5ExtTransfer:ok`。需要确认发现入口是否还能捕获当前视频，还是应该明确 no-go。

## 观察

- 首页信息流和全屏播放都留在 `/web/pages/home`，不会跳到 `/web/pages/feed`；全屏是页面内的 DOM Fullscreen（`document.fullscreenElement` 是一个 `DIV`，视频在其内部），不是 `/feed` 详情页。
- 首页的预览视频会随着滚动自动播放；它们走页面自己的 `xweb` worker 拿数据，不触发 `H5ExtTransfer`。
- 页面通过 `window.xweb.worker.port` 收到 feed 数据：每个 feed 项含 `id`、`objectDesc.description`、`objectDesc.media[0]`（`url`、`urlToken`、`decodeKey`、`videoPlayLen`、`thumbUrl`、`fileSize`），与 `/feed` 的媒体结构同源。
- 页面右上角「独立窗口显示」会调用 `onDiscoveryDetachButtonClicked`，随后由原生窗口接管（worker 事件里可见 `viewScene: DiscoverDetach`）；页面内「进入全屏」则触发 `fullscreenchange`。
- 两种入口都不触发 `H5ExtTransfer:ok`；但页面在两种动作发生时都能读到“当前正在播放的 `<video>`”。

## 探针

一次性诊断探针（本地 `artifacts/171-wechat-discover/`，不进仓库）：

1. 本地 PAC 只把 `channels.weixin.qq.com` 指向 Titanium；媒体域名直连；注入脚本额外绑定 `window.xweb.worker.port` 的 `message`。
2. 在内存里维护 feed 目录；侧窗口、独立小窗口或全屏里的当前视频连续播放满 5 秒时，用当前 `<video>.duration` 与 feed 的 `videoPlayLen` 做 ±1.5 秒匹配，必要时用描述消歧。
3. 只回传匹配到的这一条；滚动预览只缓存、不回传。

## 证据

- 页面内「进入全屏」两次：当前播放时长 187.0 秒与 1320 秒；各自唯一匹配到一条 feed（`videoPlayLen` 187 / 1320，均带 `decodeKey`），无歧义。
- 「独立窗口显示」两次：当前播放时长 345.5 秒与 1784 秒；各自唯一匹配到一条 feed（`videoPlayLen` 345 / 1784）。
- 对 345 秒的匹配项做端到端验证：
  - `Range` 请求返回 `206`、`Content-Type: video/mp4`、`X-encflag: 1`、`X-enclen: 131072`，总长 31,100,298 字节。
  - 完整下载后用 `Isaac64KeyStream`（seed = `decodeKey`）只 XOR 前 131072 字节，`ffprobe` 读到时长 **345.467 秒**，与页面播放的 345.5 秒一致，确认为同一视频。
- 对照：聊天分享的 `/web/pages/feed` 仍按原路径触发 `H5ExtTransfer:ok`；同一次打开多条事件按 `oid+nid` 去重为 1 条。
- 生产脚本最终规则（连续播放满 5 秒）：侧窗口看两段各约 7 秒、快速滑过一两个；点「独立窗口显示」后在小窗口看两段各约 7 秒。共回传 4 条 `source=discover`、`trigger=dwell`（`videoLength` 657 / 13 / 10 / 16），快速滑过的预览没有回传。
- 全屏也走同一规则：早期实测进入全屏 + 两次滑动可分别回传不同 `videoId`（352 / 252 / 220 秒），最终不再为全屏单独抢建。
- 未新增解密域名：`xweb` worker 数据本来就在页面内存里；只把匹配到的一条回传到 `127.0.0.1` 回环 listener。

## 结论

发现入口在 4.2 可抓，但依赖页面的内部通道：

- 触发：侧窗口、独立小窗口或全屏里的当前视频连续播放满 5 秒；快速滑过的预览不加入；
- 目录：`window.xweb.worker` 的 feed 消息；
- 当前视频：按播放时长唯一匹配，匹配不到或有多条无法消歧时不回传；同一视频只回传一次；
- 不采集预览列表，不新增解密域名。

## 限制与复验

- `xweb` worker 的消息形状、页面 DOM、播放器行为都是微信内部行为；升级后需按本清单复验。
- 侧窗口按“连续播放满 5 秒”加入：快速滑过的预览不会加入；进入全屏后仍按同一规则，不额外抢建列表。
- 独立小窗口在 4.2 实测可滚动，播放的视频按同一 5 秒规则加入；若未来版本改为纯原生渲染，窗口内无法按页面规则捕获，需要复验。
- 同 feed 目录里时长相同且描述无法区分时，脚本选择不回传（宁缺毋滥）。
- 抓包日志含 token，只留在本地 `artifacts/`，不提交、不外发。

## 2026-11-22 复验：Weixin 4.2.0.53+（LiteApp，页面注入 no-go）

- 现象：聊天分享与发现入口都捕获不到；只代理 `channels.weixin.qq.com` 时，隧道内只有 `POST /web/report-perf?pf=lite`，没有 `/web/pages/home|feed` 的 HTML/JS。
- 页面形态：xweb `multitab` 的 `History` 显示 2026-11-17 仍是 `channels.weixin.qq.com/web/pages/*`，2026-11-22 变为 `liteapp.weixin.qq.com/a/finder/wxalite…`；本地缓存的 LiteApp 包（`…\xworker\liteapp\resources\wxalite…\226\main`，约 8 MB）含 `pages/lite/home` 与原生桥调用。
- 数据通道：LiteApp 通过 `lite.jsapi.invoke("H5ExtTransfer", …)` 调 `pc_finder*`；`objectDesc.media[0]` 与 `decode_key` 只在该桥响应/页面内存，未出现在可观察网络、日志或磁盘。
- 开关：页面走 LiteApp 还是远程 `/web/pages/feed` 由服务端实验 `pcDirectFlow` / `pcDetailFlow` 控制，用户侧无开关；这解释了严格 .52 对照仍无页面请求。
- 调试与保存：`WeChatAppEx.exe` 无 `--remote-debugging-port` / `--inspect`，WMPF 进程无监听端口；没有原生「保存视频」入口，「复制链接」只返回 H5 分享页。
- 结论：本版本页面注入 no-go；详见 [研究记录 #182](182-wechat-wmpf.md)。复验以 xweb `History` 的页面 URL 为准：回到 `channels.weixin.qq.com/web/pages/*` 才重新评估注入。
