# #94 微信视频 MITM 抓包原型（研究分支）

状态：**未完成，暂停续做**。本目录是 #94 的一次性抓包原型与操作说明，只存在于
`research/94-wechat-mitm-spike` 分支，不合并、不开 PR。

## 目标

- 验证微信仍可通过本地 MITM + 注入 `channels.weixin.qq.com` 拿到视频媒体信息。
- 已接受「打开后捕获」：用户在微信里打开视频后，JiuMi 自动列出当前视频并允许下载。
- 除聊天里分享的视频，还要验证**视频号里打开的视频**能否走同一链路；如果桥事件
  不触发，再试页面 `fetch`/`XHR` 或 MITM 媒体 JSON。

## 当前进度

已验证：

- Titanium.Web.Proxy 7.0.27 在 net10 上可用，只解密
  `channels.weixin.qq.com` 与 `finder.video.qq.com`。
- 通过代理访问 `https://channels.weixin.qq.com/` 返回 200，HTML 被注入
  `InjectionScript.js`（冒烟日志记录 `INJECT ... script=6469B`）。
- CurrentUser 根证书 `JiumiSpike` 的信任弹窗可通过「是(Y)」完成；系统 HTTPS
  代理只指向本地，HTTP 仍保留 Clash `127.0.0.1:7897`。
- 停止后系统代理已精确还原，证书已从 CurrentUser `Root`/`My` 删除。

未验证：

- 微信打开聊天分享视频时的 `H5ExtTransfer:ok` 与 `media[0]` 字段。
- 视频号信息流的流量形态、媒体 JSON 与可下载性。
- 下载、XOR 范围（首 131072 字节还是全文件）、可播放性。
- 200% 缩放、Win11、多屏/混合 DPI。

## 环境

- Windows 10 19045 x64、单屏 1920×1080、100% 缩放。
- .NET 10；Titanium.Web.Proxy 7.0.27。
- 微信版本**不锁定**（用户通常保持最新版）；本原型不依赖具体小版本号。
- 旧实现参考：`ZLiLiang/JiumiTool` 的 `JiumiTool2` 分支
  （`Services/HttpsProxyService.cs`、`Script/InjectionScript.js`、
  `Services/DownloadService.cs`、`Commons/Rng.cs`）。

## 安全边界

- 只解密 `channels.weixin.qq.com` 与 `finder.video.qq.com`；其他 HTTPS 只转发，
  不解密、不记录正文。
- 代理与注入回传都只监听 `127.0.0.1` 的随机端口，回传路径带随机 token。
- 根证书只装 `CurrentUser\Root`；退出时删除。系统代理在启动时快照，退出时按
  快照精确还原（包括 `ProxyEnable`、`ProxyServer`、`ProxyOverride`、
  `AutoConfigURL`、`AutoDetect`）。
- 注入脚本会把页面里的媒体 JSON 与 URL 回传到本地日志；日志仅在
  `WeChatSpike/logs/`（已忽略），不要把带 token 的日志提交或贴到票里。

## 运行

```powershell
cd research/94-wechat-mitm/WeChatSpike
dotnet run -c Release -- --minutes=25
```

启动流程：

1. 生成 `JiumiSpike` 根证书；第二次运行时 Windows 会弹「安全警告」，点「是(Y)」。
2. 随机端口起代理与本地回传 listener，并把系统 HTTPS 代理切到本地。
3. 在微信里打开聊天分享的视频并播放 5–10 秒。
4. 打开「视频号」，点开一个视频并播放 5–10 秒。
5. 创建 `logs/STOP`（或 Ctrl+C）停止，自动还原代理并删除证书。

手动兜底（进程崩溃或被强杀时）：

```powershell
powershell -ExecutionPolicy Bypass -File research/94-wechat-mitm/tools/restore-proxy-cert.ps1
```

## 捕获事件

`logs/captures.ndjson` 每行一个 JSON，`kind` 取值：

- `page`：页面地址与标题（区分聊天视频页与视频号页）。
- `video`：`H5ExtTransfer:ok` 的 `media[0]` 字段（url、decodekey、size、description 等）。
- `invoke`：桥调用命令名（去重），用于发现视频号用的 JSAPI。
- `bridge-other`：其他桥回调里出现媒体字段的载荷（截断）。
- `network`：页面 `fetch`/`XHR` 响应中出现媒体字段的 JSON（截断）。
- `resource`：`performance` 里匹配 finder/video/media 的资源 URL。
- `mitm-json`：代理侧直接看到的媒体 JSON 响应（注入未覆盖时的兜底）。

`logs/traffic.log` 记录两个目标域名的请求/响应元数据、CONNECT 主机名与注入动作。

## 续做清单

1. 让微信完成登录，确认代理能看到 `channels.weixin.qq.com` 的请求；如果完全没有
   目标流量，先判断是 XWeb 不走系统代理、QUIC，还是证书未被信任。
2. 聊天分享视频：记录 `H5ExtTransfer:ok` 是否出现、`media[0]` 字段形状、URL/token
   拼接、关闭后再打开是否重复触发。
3. 视频号：对比 `page`/`invoke`/`network`/`resource`/`mitm-json`，确认能否拿到当前
   视频的 `url`、`decode_key`、`url_token`、`file_size`、封面。
4. 用旧 `DownloadService`/`Rng` 跑一次完整下载，判定 XOR 实际范围与可播放性。
5. 回填 #94，给出 go/no-go；go 时再写 ADR-0013 与子票 2–5 的规格。
