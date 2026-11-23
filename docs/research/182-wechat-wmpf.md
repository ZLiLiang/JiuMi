# #182 研究：Weixin 4.2.0.53 视频号 LiteApp 页面的抓取入口

- 日期：2026-11-22
- 环境：Windows 10 19045 x64；Weixin 4.2.0.53（`Weixin.exe` FileVersion 4.2.0.53）；内置 WMPF `RadiumWMPF` 2.5.8.26134；安装路径 `D:\Tencent\Weixin\4.2.0.53`；数据目录 `%APPDATA%\Tencent\xwechat`；当前用户无管理员权限；单屏 1920×1080 / 100%
- 关联：[#68](https://github.com/ZLiLiang/JiuMi/issues/68) / [#94](https://github.com/ZLiLiang/JiuMi/issues/94) / [#171](https://github.com/ZLiLiang/JiuMi/issues/171) / [#182](https://github.com/ZLiLiang/JiuMi/issues/182) / [ADR-0013](../adr/0013-wechat-video-download.md)
- 原始产物（不进仓库）：`artifacts/182-wechat-wmpf/`（LiteApp 包解包、worker 脚本；不含 token、decode_key、完整媒体 URL）

## 结论摘要

**页面注入 no-go；预下载缓存 + seed 条件可行（见下方追加实测）。** 4.2.0.53 本机上的视频号首页/详情不再由 `channels.weixin.qq.com` 的远程 HTML 承载，而是 WMPF 里的 **LiteApp**（本地签名包 + 原生桥）。因此：

- 没有可用的 DevTools/CDP 入口（无需注入的前提下）；
- 没有用户可达的开关把 LiteApp 强制切回远程页面；
- feed 元数据不落普通网络/日志；但 LiteApp 预下载时会把 `seed` 写入 IndexedDB，完整预下载缓存可直接解密；
- 媒体 URL 可从 xweb 缓存恢复；`decode_key` 不在 URL/请求头里，但预下载记录里有 `seed`；
- 视频号 PC 端没有原生「保存视频」入口；「复制链接」只给 H5 分享页地址。

唯一公开可见的注入面见 [参考](#参考) 中的 PR：MITM `res.wx.qq.com` 的 SDK、运行时改写 LiteApp 模块、并搬走微信的 LiteApp SDK 缓存。它需要新增解密域名并操作微信数据文件，违反本研究边界，且该 PR 未合并、未验证，不作为 go。

## 2026-11-22 追加实测：seed 可从预下载缓存获得（条件可行）

上面的 no-go 针对“页面注入 + 桥取 key”。继续按旧版文章的“从解密过程反推 key”做静态分析后，发现 LiteApp 自己把 key 落盘了：

- LiteApp 解密代码确认 `seed = objectDesc.media[0].decodeKey`；`store-common.js` 在预下载视频时把 `seed`、`videoKey`、`preDownloadedContentLen`、`videoContentLen` 写入 `video_cache_info`。
- 本机 `radium/web/profiles/multitab_*/IndexedDB/weixin_xworker_0.indexeddb.leveldb/000003.log` 中可直接读到明文 JSON 记录（字段 `feedId`、`videoKey`、`preDownloadedContentLen`、`videoContentLen`、`seed`）。
- 预下载文件 `xworker/liteapp/local/assets/tmp/.../finder/finder_videoCache_*` 是加密视频前缀；用该记录里的 `seed` 生成 Isaac64 流，XOR 前 131072 字节即可。
- 端到端验证：
  - 完整缓存（`preDownloadedContentLen == videoContentLen`，370,351 字节）解密后 `ffprobe` 为 HEVC 720×1280 / AAC / 7.52 秒，`ffmpeg -f null` 无解码错误。
  - 部分缓存（571,092 字节，记录总长 1,027,964）解密后 MP4 头正常，但在 `0x8be76` 处截断，说明长视频仍需补齐缓存之后的字节。
- 因此原文件路径改为 **条件可行**：完整预下载缓存可直接解密为原文件；长视频需要同时拿到完整媒体 URL（xweb 缓存命中或媒体域名代理捕获），再按内容长度/spec 与 seed 记录配对。尚未完成长视频 URL + seed 的端到端验证。
- 限制：依赖 LiteApp 预下载缓存的文件布局与 IndexedDB 明文 `seed`；版本升级必须复验。未解密额外域名、未注入、未改微信文件。

## 问题

2026-11-22 在 4.2.0.53 复验时，代理/TLS/注入链路自检正常，但在只代理 `channels.weixin.qq.com` 的观察面内，微信 webview 只发出 `POST /web/report-perf`，没有 `/web/pages/home|feed` 的 HTML/JS 请求；清空 xweb 缓存、降级到 4.2.0.52 都无效。需要判断 4.2.0.53+ 是否存在安全、可维护、无需管理员的抓取入口。

## 方法

- **A. 本机静态检查（主要一手证据）**：安装目录与 `RadiumWMPF.bin`；WMPF 插件目录（`%APPDATA%\Tencent\xwechat\xplugin\plugins\RadiumWMPF\26134`）；xweb profile（`Preferences`、`Local State`、`History`、Cache）；xweb MMKV 配置；微信日志；LiteApp 缓存包与 finder worker。定向字符串搜索，限制输出量，未导出整文件。
- **B. 运行时只读检查**：`Get-CimInstance Win32_Process` 看 `Weixin.exe` / `WeChatAppEx.exe` 命令行；`Get-NetTCPConnection -State Listen` 看监听端口。**未**启动/停止/重启微信，**未**连接任何调试端口，**未**改注册表或系统代理。
- **C. 公开资料**：GitHub 现有视频号下载器与其 issue/PR、社区调试文章、官方 WMPF/开放文档。与本地证据分开标注。
- **D. 原生保存路径**：LiteApp 页面菜单、`Weixin.dll`/`XPlayer.bin` 字符串、磁盘缓存。
- **E. WMPF worker/IPC**：只做静态与日志观察，不 hook、不注入、不附加进程。

## 关键发现

### 1. 页面形态变了：视频号首页是 LiteApp，不是远程 H5

- xweb 的 `multitab` profile `History`（SQLite，只读查询）显示：
  - 2026-11-17（4.2.0.52 时期）：`https://channels.weixin.qq.com/web/pages/feed`；
  - 2026-11-22（4.2.0.53）：`https://liteapp.weixin.qq.com/a/finder/wxalite…`（标题「视频号」，UTC 06:53–07:32 多次）。
- LiteApp 包已落盘：`%APPDATA%\Tencent\xwechat\radium\users\<user>\xworker\liteapp\resources\wxalite…\226\main`，是 8.38 MB 的 zip，内含 `pkg/app.json`、`pkg/chunks-common.js`、`signatures.json`、`liteapp.pem` 等；`app.json` 为 Vue3、`pages/lite/home`、`authUrl: https://liteapp.weixin.qq.com/a/finder/…`。包是签名的，改包不是可维护路径。
- `RadiumWMPF.bin`（zip，429 MB）与解包后的 `runtime/flue.dll`、`wmpf_resources.pak` 中出现：`xweb://star-page/web/pages/home`、`/web/pages/feed|live|profile`、`FinderHome/FinderFeed/FinderLive/FinderProfile`、`url_classify.cc GetFinderTypeWithUrl`，以及 `liteapp.weixin.qq.com/a/finder/…`。`wmpf_resources.pak` 中未发现 HTML 文档，`/web/pages/home` 只出现在一个 URL 模板字符串里；`xweb://star-page` 则是 flue 的本地 WebUI 外壳（CSP 里可见 `xweb://star-page`）。
- 4.2.0.52 时期的 `/web/pages/*` 不能从「客户端升级」解释：本机 `History` 在 11-17 仍是 channels 页面，11-22 才全部变成 LiteApp；与 #182 中「降级 .52 仍无页面请求」一致。
- **对 #182 现象的直接修正**：4.2.0.53 不是「webview 不发页面请求」，而是页面请求换了域——首次打开会从 `liteapp.weixin.qq.com` 拉 LiteApp 包（本机包时间与一次打开时间吻合），之后复用本地缓存。JiuMi 的 PAC 只代理 `channels.weixin.qq.com`，因此这部分流量本来就不在观察面内。即使把 `liteapp.weixin.qq.com` 加入解密面，也只能看到一次性的包下载，数据仍走原生桥（见下）。

### 2. 数据仍走 `H5ExtTransfer`，但由 LiteApp 原生桥发起

解包后的 `pkg/chunks-common.js` / `store-common.js` 中，finder API 统一走：

```
lite.jsapi.invoke("H5ExtTransfer", {
  url, cgi_cmdid, scope, req_json, is_security_check: false
})
```

API 表包含 `FinderInit`（cmdid 5831）、`FinderPcFlow`（11597）、`FinderGetRecommend`（6638）、`FinderGetFeedH5Url`（22299）等，全部是 `/cgi-bin/micromsg-bin/pc_finder*`。也就是说：**桥事件名没变，变的是调用方与承载页面**——脚本不再通过 `WeixinJSBridge` 在远程页面里调用，而是 LiteApp 在 WMPF 内调用原生桥。这正是 4.2.0.53 只看到 `POST /web/report-perf?pf=lite`、看不到 cgi 请求的原因。

### 3. 页面模式由服务端实验控制

LiteApp 在 `FinderInit` 响应里取 `exptInfo`（源码里有 `[实验]useSvrExperiments` 日志），映射为 `finderFlow = pcDirectFlow`、`detailJumpFlow = pcDetailFlow`、`canCopyH5Url = pcCopyH5Url` 等；部分条件下 `pcDetailFlow` 会被强制置 0。`goToDetail` 只有在 `finderFlow && detailJumpFlow`（且非青少年模式、推荐未关闭）时才跳到远程 `https://channels.weixin.qq.com/web/pages/feed?...`，否则走本地 LiteApp 路由。

这解释了 #182 的现象：协议没有被删除，`/web/pages/feed` 的代码路径仍在，只是当前账号/会话被服务端实验切到 LiteApp。客户端版本不是决定因素，所以降级 4.2.0.52 不恢复。

### 4. 静态面没有可用的调试/保存入口

- 运行中的 `WeChatAppEx.exe` 命令行只有 `--mojo-platform-channel-handle`、`--wmpf_root_dir`、`--type=utility/gpu-process` 等，没有 `--remote-debugging-port`、`--inspect`；三个 WMPF 进程没有任何监听 TCP 端口。
- `flue.dll` 的 Chromium 开关表里有 `remote-debugging-port` / `remote-debugging-pipe` 字符串，但没有 `DevToolsActivePort`、没有 `chrome://inspect`；`WeChatAppEx.exe`、`xweb_elf.dll`、`wmpf_host_export_x64.dll`、`Weixin.dll` 均不含 `remote-debugging-port`。
- xweb `Preferences` 只有 `proxy.mode=system` 等常规项；`Local State` 只有 `os_crypt`、`profile_network_context_service`、`subresource_filter`；MMKV 配置为不透明二进制，无可读开关键。
- LiteApp 页面菜单只有 `copy_feed_link` 与 `popout_button`，没有保存/下载项；`保存视频` 在 `Weixin.dll`、`XPlayer.bin`、`flue.dll`、`RadiumWMPF.bin` 中均为 0 次（UTF-8 与 UTF-16LE 都查过）。

## 逐条回答

### 1. 是否暴露可用的 DevTools/远程调试端口或 xweb 调试开关？

**否（本机实测 + 公开资料，可信度高）。**

- 本机：无监听端口、无调试参数、无 `DevToolsActivePort`、无 xweb 调试配置键；`remote-debugging-port` 仅作为 Chromium 开关名存在于 `flue.dll` 的字符串表，未被启动参数使用。
- 公开：社区文章明确说明微信 3.9+/4.x 中 `Weixin.exe` 不再向 `WeChatAppEx.exe` 透传 Chromium 调试参数，旧的 `--remote-debugging-port=9222` 与 `debugxweb.qq.com` 入口已失效；目前可用的 `WMPFDebugger` 依赖 Frida 注入 `WeChatAppEx.exe`（需管理员），属于本研究的禁止项（进程注入/调试附加）。
- 限制：没有用调试参数启动 `WeChatAppEx.exe` 做对照——那会以调试方式启动微信组件，越界且会扰动正在运行的微信。

### 2. 能否通过微信支持的配置、实验室开关或 preload 参数强制走远程 HTML？

**否（本机实测，可信度高；具体实验门控为静态分析，可信度中）。**

- 页面由 LiteApp 包承载；切换远程页面的开关是服务端 `exptInfo.pcDetailFlow`/`pcDirectFlow`，随 `FinderInit` 下发并写入本地缓存，用户界面没有对应项。
- `preload` 参数确实存在：LiteApp 的 `requestPrerender`/`onPageHide` 会拼 `https://channels.weixin.qq.com/web/pages/...?is_preload=1`，`flue.dll` 里还有 `is_preload=0&…&xlab_enable_finder_home=1&preload_id=…` 的模板。但它们由客户端/服务端内部使用，不是用户可设置的开关；`xweb_config_storage` 也不可读。
- 限制：没有尝试篡改 LiteApp 本地实验缓存或 xweb 配置来强制 `pcDetailFlow`——那是修改微信数据文件，属于边界外，且 `FinderInit` 会重新覆盖。

### 3. feed 元数据能否在 worker/IPC、日志或可观察网络响应中拿到？

**普通网络/日志不落；但 LiteApp 预下载记录会把 `seed` 写到 IndexedDB（2026-11-22 追加实测，修正了“磁盘拿不到”）。**

- 元数据结构与 #171 相同：`objectDesc.media[0]` 含 `url`、`urlToken`、`decodeKey`、`videoPlayLen`、`thumbUrl`；由 `H5ExtTransfer` 的 `jsapi_resp` 返回给 LiteApp。
- WMPF 侧保留的 finder xworker（`xworker/finder/28/worker`）只包含旧 `channels.weixin.qq.com/web/pages/*` 的 URL 常量；4.2.0.53 的 LiteApp 页面逻辑在 LiteApp 包内，不经过这个 worker。
- 4.2.0.53 的可观察网络里没有 `/cgi-bin/micromsg-bin/pc_finder*`，只有页面自己的 `POST /web/report-perf`（LiteApp 代码里是 `${U}/web/report-perf?pf=lite`，JiuMi 侧看到的正是这条）。
- 微信日志是 xlog 二进制（非 zlib、非明文），全量搜索 `channels.weixin`、`report-perf`、`finder`、`devtools` 均为 0 命中；xweb Cache/Local Storage 没有 feed 数据。
- **修正**：`radium/web/profiles/multitab_*/IndexedDB/weixin_xworker_0.indexeddb.leveldb` 的 `video_cache_info` 记录含明文 `seed`；`xworker/liteapp/local/assets/tmp/.../finder/finder_videoCache_*` 是预下载视频前缀，可用该 `seed` 解密（见追加实测）。
- 公开：同类工具 [wx_channels_download](https://github.com/ltaoo/wx_channels_download) 的 issue 也确认「原生 LiteApp 视频页」无法建立页面注入连接。

### 4. 媒体请求是否可观察；`decode_key` 有网络/服务端来源，还是只在原生内存/IPC？

**媒体请求可在主机级观察；`decode_key` 有服务端来源，但只经过原生桥，不在可观察网络里（本机实测 + 既有 ADR 实测，可信度高）。**

- LiteApp 的 `videoUrl` getter 是 `media.url + media.urlToken`，并把 `wxapp.tc.qq.com` 重写为 `finder.video.qq.com`；播放器在页面内用 WASM worker（源码日志 `[decryptWorker-pool]`）按 `decodeKey` 解密首段。
- `decodeKey` 只来自 `objectDesc.media[0]`，即 `H5ExtTransfer` 响应；它不出现在媒体 URL、`urlToken` 或请求头里，无法从 URL 推导。
- 因此：能看到「请求了 `finder.video.qq.com`」（主机/连接级），URL 里没有 `decode_key`；ADR-0013 已实测媒体响应 `X-encflag: 1`、`X-enclen: 131072`，只解首 128 KB。
- 限制：本次没有重新抓包（避免扩大解密面）；媒体侧结论沿用 ADR-0013 的实测与本次代码证据。
- 2026-11-22 追加实测：播放后，xweb `Cache/Cache_Data/data_1|data_2` 中可检索到完整的 `20302/stodownload` URL（参数含 `encfilekey`、`token`、`sign`、`exportkey`、`pass_ticket` 等，不含 `decode_key`）。用该 URL 做 `Range: 0-0` 与 `0-131071` 请求均返回 `206 video/mp4`，总长 236,107 字节，`X-encflag: 1`、`X-enclen: 131072`；测试时 URL 仍有效。
- **修正**：URL 本身不含 key，但 LiteApp 的预下载记录在 IndexedDB 里有 `seed`；完整预下载缓存已验证可解密，长视频需要把完整 URL 与 seed 记录按长度/spec 配对（未完成端到端验证）。

### 5. 是否有可驱动的原生保存/分享/复制链接入口？

**没有可替代注入的原生保存入口（本机实测，可信度中高）。**

- LiteApp 页面菜单只有 `copy_feed_link`：点击后调用 `finderGetFeedH5Url({objectId})`（cmdid 22299，仍走 `H5ExtTransfer`），把返回的 `feedH5Url` 写入剪贴板；这是 H5 分享页地址，不是媒体地址，也不含 `decode_key`。是否可用还受 `pcCopyH5Url` 实验位控制。
- 没有 `保存`/`保存视频` 文案；`XPlayer.bin`、`Weixin.dll` 中 `保存视频` 均 0 命中；`Weixin.dll` 里的 `复制链接`/`另存为` 无法归因到视频号播放器菜单。
- 磁盘侧没有「保存视频」按钮，但预下载缓存留下了 `finder_videoCache_*` 视频前缀和 IndexedDB 里的 `seed`；完整预下载缓存可直接解密为 MP4（见追加实测），部分缓存需补齐剩余字节。
- 公开：同类工具是在页面里**自建**下载按钮（README），侧面说明微信 PC 端没有原生保存入口。

## 最终结论

**页面注入：no-go；预下载缓存 + seed：条件 go（见上方 2026-11-22 追加实测）。**

理由按重要性排序：

1. 视频号页面已变为 WMPF LiteApp（本地签名包），没有可注入的远程 HTML/JS；MITM `channels.weixin.qq.com` 只剩 telemetry。
2. feed 元数据不落普通网络；但 LiteApp 预下载时会把 `seed` 写入 IndexedDB，完整预下载缓存可直接解密（条件 go）；长视频还需完整 URL 配对。
3. DevTools 入口需要 Frida/内存注入（越界）；配置/实验开关不可达，篡改 LiteApp 缓存也不是「支持的配置」。
4. 原生保存/复制链接不能替代：复制链接只给 H5 分享页，且没有 `decode_key`。

**明确不做的路径**（不是「暂缓」，是违反边界）：

- MITM `res.wx.qq.com` / `liteapp.weixin.qq.com` 并在 LiteApp 运行时改写模块（公开 PR 的做法）：新增解密域名、搬移微信 LiteApp SDK 缓存、依赖内部模块形状，未合并未验证。
- Frida/WMPFDebugger、给 `WeChatAppEx.exe` 加调试参数、hook 原生桥。

**保留的观察点**：远程 `/web/pages/feed` 代码路径仍在，由服务端 `pcDetailFlow`/`pcDirectFlow` 控制。如果服务端把流量切回远程页面，现有注入方案可能恢复；复验以 `History` 里的页面 URL 为准（见下）。这不是可依赖的产品路径。

**产品建议**（交主代理决策，本研究不实现）：

- 在 4.2.0.53+ 上把视频号下载标为「暂不可用」，并给出可理解的说明；#182 的背景里聊天分享入口同样未能捕获，若确认它也走 LiteApp，则一并受影响。
- 不要为了恢复功能而放宽解密域名或引入注入型调试；等微信把页面切回或出现官方入口。
- 若产品接受「在浏览器里看官方视频号网页版」，可另开一张探索票验证 `channels.weixin.qq.com/web/pages/home` 在普通浏览器（扫码登录）下是否可用；它仍属现有允许域名，但会改变「下载微信里正在看的视频」这一产品前提，且登录/可用性未验证。

## 替代路径初步评估（2026-11-22）

自动抓原文件在本版本 no-go；下面是改变产品前提的兜底，尚未实现。

### 屏幕录制（已实机验证，需用桌面复制）

- 窗口：`WeChatAppEx.exe` / `Chrome_WidgetWin_0`，子窗口为 `Chrome_RenderWidgetHostHWND` 与 `Intermediate D3D Window`。
- **Windows Graphics Capture 窗口捕获不可用**：能录到标题栏、评论、按钮等 UI，但视频区域在 1s / 4s 帧均为全黑（904×882 H.264，5 秒仅 23KB）；对子窗口 HWND 捕获直接失败。视频面不在顶层窗口的可捕获合成里（或受保护）。
- **桌面复制（DXGI Desktop Duplication）可用**：同一屏幕区域以 920×890 / 30fps 捕获到真实视频，1s / 4s 帧不同，5 秒 4.06MB；系统级防录屏没有挡住视频。
- 代价：DDA 录的是屏幕可见像素，窗口必须恢复且不被遮挡/最小化；测试时视频窗口左侧被 Qoder 盖住，DDA 就录进了 Qoder，正式录制需要把视频窗口置前或至少完全露出；分辨率等于窗口显示大小并需重编码；UI/弹幕会计入；本机 DirectShow 没有回环设备，音频需另走 WASAPI 进程/系统回环；拿不到时长与播放状态，需要用户手动开始/停止。
- 结论：屏幕录制技术可行，但实现应选 **DDA + 窗口区域裁切**，而不是 WGC 窗口捕获；录制期间必须保持视频窗口可见。

### 官方网页版 / H5 分享页（兜底）

`channels.weixin.qq.com/web/pages/*` 的远程页面代码路径仍在，但普通浏览器是否可用未证实。

2026-11-22 用公开短链 `https://weixin.qq.com/sph/…` 复验：

- 短链 301 到 `channels.weixin.qq.com/finder-preview/pages/sph?id=…`；Edge 渲染后只显示二维码与「可扫码前往微信观看此内容」，不是可播放的网页。
- 页面会匿名调用 `POST /finder-preview/api/feed/get_feed_info`（`shortUri` + 空 `generalToken`），只返回作者、描述、封面、互动数和 `dynamicExportId`，**没有视频地址**；前端包里的 `h264VideoInfo.videoUrl` 需要登录态/微信环境才会出现。
- 因此普通浏览器分享页不能替代注入；要拿到原文件仍需要微信登录态、原生桥或注入/逆向，未作为当前方案。

## 版本复验清单（10 分钟内跑完）

1. **版本**：`(Get-Item 'D:\Tencent\Weixin\Weixin.exe').VersionInfo.FileVersion`；`D:\Tencent\Weixin\<ver>\plugin_info.ini` 的 `RadiumWMPF`。
2. **进程参数**：`Get-CimInstance Win32_Process -Filter "Name='WeChatAppEx.exe'"`，确认无 `--remote-debugging-port`、`--inspect`。
3. **监听端口**：`Get-NetTCPConnection -State Listen` 过滤上述 PID，确认没有回环调试端口。
4. **页面形态（最关键）**：打开一次「发现 → 视频号」，只读查询
   `%APPDATA%\Tencent\xwechat\radium\web\profiles\multitab_*\History`（SQLite `urls` 表，按 `last_visit_time` 倒序）：
   - 最新是 `channels.weixin.qq.com/web/pages/*` → 远程页面回来了，重跑 #171/#177 的注入链路；
   - 最新是 `liteapp.weixin.qq.com/a/finder/*` → 仍是 LiteApp，维持 no-go。
5. **LiteApp 版本**：`xworker\liteapp\resources\wxalite*\*\main` 是否存在；解包 `pkg/app.json` 看 `app_version`、`pages/lite/*`。
6. **桥与缓存**：在解包出的 `pkg/chunks-common.js` 搜 `jsapi.invoke("H5ExtTransfer"` 与 `FinderPcFlow`；同时检查 `IndexedDB/weixin_xworker_0.indexeddb.leveldb` 是否仍有 `seed` 和 `finder_videoCache_*`。有 → 预下载缓存路线可继续；没有 → 记录新形态。
7. **网络对照**：只代理 `channels.weixin.qq.com` 时，若仍只有 `/web/report-perf?pf=lite`、无 HTML/JS，则确认页面不在该域。
8. **决策**：第 4 步回到远程页面 → 重新评估注入；否则先检查预下载缓存 + `seed` 是否仍可用（#182 追加实测）；两者都不可用才维持 no-go。

## 未验证与限制

- 没有在 4.2.0.52 上重新打开视频号做 A/B 对照（降级会被自动更新；#182 已有一次严格对照记录）。
- 没有连接或探测任何调试端口，没有尝试用调试参数启动 `WeChatAppEx.exe`。
- `pcDetailFlow` 的具体服务端取值与灰度规则未知；结论是「用户不可设置」，不是「永远关闭」。
- 媒体侧未重新抓包；`decode_key`/加密边界沿用 ADR-0013 的实测。
- 公开 PR 的可行性未复现；它需要新增域名与缓存操作，即使可行也不在本研究边界内。

## 参考

官方：

- [WMPF 小程序硬件框架（wmpf_demo_external）](https://github.com/wmpf/wmpf_demo_external)
- [小程序打开视频号视频（微信开放文档）](https://developers.weixin.qq.com/miniprogram/dev/framework/open-ability/channels-activity.html)
- [视频号直播（微信开放文档）](https://developers.weixin.qq.com/miniprogram/dev/framework/open-ability/channels-live.html)

公开资料（社区，仅作线索）：

- [ltaoo/wx_channels_download](https://github.com/ltaoo/wx_channels_download)（管理员 + 证书 + 代理 + 页面注入；README 自建下载按钮）
- [#606 macOS 4.2.0.54 原生 LiteApp 视频页未建立页面连接](https://github.com/ltaoo/wx_channels_download/issues/606)
- [#607 Windows 4.2.0.53 没有下载按钮](https://github.com/ltaoo/wx_channels_download/issues/607)
- [#611 fix: cannot insert download button in LiteApp version](https://github.com/ltaoo/wx_channels_download/pull/611)（未合并：MITM `res.wx.qq.com` SDK + 运行时改写 LiteApp 模块 + 搬移 LiteApp SDK 缓存）
- [#612 微信 4.2.0.53 后续无下载按钮](https://github.com/ltaoo/wx_channels_download/issues/612)
- [xweb debugging 笔记](https://github.com/5ec1cff/my-notes/blob/master/xweb-debugging.md)（移动端 `debugxweb` 入口，PC 4.x 不适用）
- [PC 微信小程序 DevTools 映射（Frida + CDP，需管理员）](https://blog.csdn.net/ymdaa555/article/details/164151290)

本仓库：

- [ADR-0013](../adr/0013-wechat-video-download.md)
- [研究记录 #171](171-wechat-discover.md)
