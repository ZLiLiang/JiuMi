# ADR-0018 微信视频下载：纯只读内存双阶段采集（替代 MITM 链路）

- 状态：提议（替代 [ADR-0013](./0013-wechat-video-download.md) 的 MITM / 证书 / PAC / 页面注入采集链路；下载、Range 分段与 ISAAC64 头部解密契约沿用）
- 日期：2026-11-22
- 来源：Weixin 4.2.0.53+ LiteApp 迁移导致旧链路失效（[#182](https://github.com/ZLiLiang/JiuMi/issues/182)）；2026-11-22 实机原型验证（原型保留于本地 `artifacts/wechat-liteapp-proto/`，不入库）

## 背景

[ADR-0013](./0013-wechat-video-download.md) 采用「本地 PAC + Titanium MITM 代理 + `CurrentUser` 自签名根证书 + `InjectionScript.js` 页面注入」获取视频号媒体信息。在 Weixin 4.2.0.53+ 中，视频号首页与详情页均已迁移至 WMPF **LiteApp**（本地签名包），不再请求 `channels.weixin.qq.com` 远程 HTML，导致整个 MITM 注入链路彻底失效。

2026-11-22 在本机 Weixin 4.2.0.53 上进行了三轮实机原型验证，确认了三个关键事实：

1. **完整元数据与密钥明文驻留进程内存**：`Weixin.exe` 与 `WeChatAppEx.exe` 的可读私有内存中直接存在视频号 feed 的 JSON 元数据，包含 `url`、`url_token`、`decode_key`（或 `decodeKey`）、`thumb_url`、作者 `nickname`、标题 `description`、时长、文件大小及多清晰度 `spec` 列表。
2. **仅扫元数据会引入大量预加载噪音，而 `NetworkService` 只包含真正拉流的视频**：刷不到 10 条视频时，渲染进程内存中会一次性预加载出 60～97 条 feed JSON；但 `WeChatAppEx.exe` 的 `--utility-sub-type=network.mojom.NetworkService` 子进程只处理真正发起视频流 HTTP 请求（`/20302/stodownload?encfilekey=...`）的视频，单次扫描仅约 22 ms。实测刷 3 条视频时，`NetworkService` 精准触发 3 条（0 条预加载噪音）。
3. **元数据 JSON 存在 GC 窗口期**：feed 列表刚下发时完整 JSON 驻留在内存中，但几十秒后用户才滑到预加载批次中的某条视频时，原始 JSON 字符串可能已被 V8 GC 回收；因此需要用后台静默缓存提前保底，再由 `NetworkService` 触发输出。

约束与边界：

- 仍不要求管理员权限：同一登录会话下的 `Weixin.exe` / `WeChatAppEx.exe` 允许以最小只读权限 `PROCESS_VM_READ | PROCESS_QUERY_INFORMATION` 读取。
- 坚持零侵入、低封号风险：不做 DLL 注入、API Hook、远程线程、内存写入或 LiteApp 本地包篡改。
- 坚持产品边界：**只列出用户真正在看的视频**，不把微信预加载的几十条列表全倒进界面。
- 媒体文件仍需通过 CDN 直连重新下载（播放器采用流式分片缓冲，内存中不存在完整 MP4 文件）。

## 决策

### 1. 彻底移除 MITM 代理、证书与页面注入链路

新方案完全不依赖网络中间人拦截，移除以下组件与依赖：

- 移除 NuGet 依赖 `Titanium.Web.Proxy`。
- 移除代理、PAC、证书、回环接收器、残留恢复与页面注入实现：`TitaniumWeChatProxy`、`WeChatPacScript`、`WeChatCaptureListener`、`WeChatCaptureStateStore`、`WeChatRootCertificateFactory`、`WindowsWeChatCertificateStore`、`WinInetSystemProxy`、`InjectionScript.js`、`WeChatHtmlInjector`、`WeChatInjectionScript`、`WeChatCaptureResponsePolicy`。
- 移除启动期代理残留恢复流程（`RestoreResidualAsync`）、首次开启监听的证书确认弹窗、设置页证书卸载入口及关联错误码（`WeChatVideo.CertificateUninstallFailed`、`WeChatVideo.ProxyRestoreFailed`）。

### 2. 双阶段纯只读内存采集架构

监听开启后，后台并行运行两个只读轮询循环，全程只调用 `OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION)`、`VirtualQueryEx` 与 `ReadProcessMemory`，且跳过 `MEM_IMAGE`、保护页与不可读页：

1. **阶段一：静默元数据索引（不进 UI）**
   - 每 2～3 秒扫描 `Weixin.exe` 与 `WeChatAppEx.exe` 的可读私有内存，定位 `decode_key` / `decodeKey` 所在的 JSON `media` 对象。
   - 遇到 JSON 字符串内嵌的双重转义文本（如 `[{\"url\":\"https://...`）时，先执行单趟一层反转义（保持内层 `\"` 与 `\n` 合法），再从锚点向左退出当前字符串、按 `{ ... }` 括号平衡切出单个 `media` 对象，交由 `System.Text.Json` 严格解析，彻底杜绝跨对象错配。
   - 从该 `media` 对象读取 `url`、`url_token`、`decode_key`、`thumb_url`、时长与 `spec`，并从紧邻的上级上下文提取 `nickname` 与 `description`。
   - 结果按 `encfilekey` 前缀写入有界内存缓存（LRU 上限 500 条），仅作内部索引，**不直接推送到 UI**。
2. **阶段二：`NetworkService` 播放触发（驱动 UI 列表）**
   - 定位 `WeChatAppEx.exe` 中命令行含 `network.mojom.NetworkService` 的子进程（进程重启时自动重定位）。
   - 启动监听时先拍一次 `NetworkService` 中已有 `/20302/stodownload?encfilekey=` 的基线快照并忽略，避免启动前已看过的视频涌入。
   - 每 500 ms 扫描一次 `NetworkService` 内存（单次约 22 ms）；仅当出现新的 `encfilekey` 时，从阶段一的静默索引取出完整元数据推送到 `WeChatVideoSession`。
   - 若静默索引尚未命中（竞态窗口），立即针对该 `encfilekey` 执行一次定向补扫，必要时 600 ms 后重试一次；仅当解析到有效 `decode_key`（或确认服务端无加密）时入列。

### 3. 去重键与 CDN 域名放宽

- **去重键**：4.2 实测同一视频在不同入口刷新时会下发新的 `decode_key` 与 `token`。去重键优先取 URL 中的 `encfilekey`（或保留 `oid + nid` 与去 `token` 的 URL 规范化回退），不得以 `decode_key` 作为去重键。
- **CDN 域名**：4.2 实测媒体链接已使用 `finder.video.qq.com`、`findera4.video.qq.com`、`findernd.video.qq.com` 等多子域。媒体与封面主机校验放宽为 `*.video.qq.com`（HTTPS），不再写死单一域名 `finder.video.qq.com`。

### 4. 沿用下载、解密与会话契约

以下经实践验证稳定的机制保持不变：

- **下载方式**：使用 `url + url_token` 通过 `HttpClient` 直接从 CDN 拉取（无需微信 Cookie），按 262144 字节 Range 分段下载，每块最多重试 2 次；以服务端 `Content-Length` / `Content-Range` 为进度与总大小唯一真值，不采信 `file_size`。
- **解密方式**：完全复用 `Isaac64KeyStream`；当首块响应带 `X-encflag: 1` 且 `X-enclen > 0`（上限 16 MB）时，仅对文件开头 `min(X-enclen, 文件长)` 字节逐字节 XOR，其余字节原样写入。
- **状态与落盘**：沿用 `WeChatVideoSession` 仅会话内存在、串行 FIFO 下载队列、403/404/410 判 `Expired`、`.mp4.part` 写完原子改名、文件名清洗与 `weChatVideo.downloadDirectory` 设置项。

### 5. 分层与接缝设计

| 层 | 职责与核心类型 | 约束 |
| --- | --- | --- |
| `JiuMi.Application` | 保留 `IWeChatVideoCapture`（去除证书与残留方法）、`WeChatVideoSession`、`Isaac64KeyStream`、下载协调与队列；新增纯逻辑：`WeChatMediaJsonExtractor`（单趟反转义 + `{}` 边界解析）、`WeChatMetadataCache`（有界 `encfilekey` 索引）、`WeChatPlaybackCorrelator`（基线过滤 + 播放触发关联） | 纯逻辑，无 Win32、无注册表、无 Avalonia 依赖，全量可单测 |
| `JiuMi.Infrastructure` | 新增 `IWeChatProcessMemoryReader`（封装只读内存枚举与读取，供测试替换）、`WindowsWeChatProcessLocator`（识别 `Weixin`、`WeChatAppEx` 及 `NetworkService` PID）、`MemoryWeChatVideoCapture`（调度双阶段循环）；保留 `WeChatVideoDownloader`、`WeChatVideoThumbnailFetcher`；非 Windows 继续注册 `NullWeChatVideoCapture` | 唯一持有 Win32 P/Invoke 与网络实现 |
| `JiuMi.Desktop` | 组合根注册新采集实现；移除启动期 `RestoreResidualAsync` | 不承载领域规则 |
| `JiuMi`（UI） | `WeChatVideoViewModel` / `WeChatVideoView` 保留列表、开关与下载交互；移除证书弹窗与设置页证书卸载段 | 开启监听即刻生效，零系统弹窗 |

### 6. 交互边界与隐私安全

- **保持现有交互逻辑不变**：不新增背景常驻扫描、不新增交互状态。监听仍由用户在「微信视频下载」页手动点击开关启动（`SetListeningAsync(true)` → `IWeChatVideoCapture.StartAsync()`），关闭开关即停止（`StopAsync()`）。
- **启动前检与既有错误呈现**：点击启动时检查微信进程存在且具备只读访问权限（`PROCESS_VM_READ | PROCESS_QUERY_INFORMATION`）；若微信未运行或 `OpenProcess` 因权限不足失败（例如微信以管理员身份运行而 JiuMi 为普通用户），`StartAsync()` 直接抛出明确异常，复用现有的 `WeChatVideo.CaptureStartFailed` InfoBar 与「重试」机制，开关保持 `Stopped`，不引入新的弹窗或状态位。
- **最小只读权限与不落盘**：仅请求 `PROCESS_VM_READ | PROCESS_QUERY_INFORMATION`（`0x0410`），严禁写入目标进程内存、附加调试器或注入模块；内存提取出的 `url_token`、`decode_key` 与原始内存片段只驻留会话内存，严禁写入日志、崩溃文件或诊断包。

## 为什么

**为什么选择「纯只读内存双阶段」，而不是「被动代理 + 内存 MP4 头拼接」。** 实机验证表明，静态/空闲状态下内存里几乎抓不到完整的 128 KB 解密后 MP4 头（播放器分片缓冲生命周期极短），而 feed 元数据 JSON 与 `decode_key` 直接存在于内存中，直接提取即可复用现有 `Isaac64KeyStream` 与 CDN 下载器；彻底摆脱代理与根证书后，用户体验、隐私边界、代码体积与维护成本全部显著优于代理方案。

**为什么必须拆成「静默索引 + `NetworkService` 触发」，而不是单阶段全扫内存。** 实测单阶段全扫在用户只看几条视频时会抓出 97 条预加载列表项，严重违背「只列正在看的视频」的产品定位；反过来，仅在 `NetworkService` 触发时临时反查，又会因为 V8 GC 丢失提前预加载视频的原始 JSON。双阶段恰好互补，同时达成零预加载噪音与高元数据命中率。

**为什么不直接读取磁盘缓存 / IndexedDB（#182 补充研究路径）。** LiteApp 本地 IndexedDB / LevelDB 格式随 WMPF 版本变动且存在文件锁竞争，长视频只缓存前缀仍需另寻完整 URL；只读内存双阶段对短、长视频走同一条链路，且实测已覆盖 400 KB 到 720 MB 的各类视频。

**为什么不在内存中拼接完整视频，而是重新下载。** 微信播放器按需 Range 拉流与淘汰分片，内存里只保留播放点附近缓冲，无法保证完整文件；从内存拿到 `url + url_token` 与 `decode_key` 后直连 CDN 下载，完整、无损且不受用户是否看完影响。

## 备选与取舍

| 备选 | 为什么没选 |
| --- | --- |
| 继续维护 MITM 代理 + JS 注入（ADR-0013） | Weixin 4.2.0.53+ LiteApp 不再走远程 HTML，页面注入彻底失效 |
| 被动代理抓 `stodownload` + 内存扫描解密后 `ftyp` 头拼接 | 仍需安装根证书与改写系统 PAC；实测内存中完整 128 KB 明文 MP4 头滞留极短，稳定性不如直接取 `decode_key` |
| 单阶段轮询扫描 `Weixin`/`WeChatAppEx` 内存并直接进 UI | 一次刷列表涌入数十上百条未看预加载视频，产品体验不可接受 |
| 仅在 `NetworkService` 触发时临时反查内存（无静默索引） | 实测对预加载批次中较晚滑到的视频，原始 JSON 已被 GC，导致丢 `decode_key` |
| 读 LiteApp 本地 IndexedDB / LevelDB 缓存 | 文件锁与内部 Schema 耦合高，长视频仍需额外配对 URL |
| DLL 注入 / Frida / Hook 微信原生函数 | 触发微信反篡改与封号风险高、杀软误报严重，违背低风险边界 |

## 后果

**正面**

- **零侵入与零配置**：无需安装或卸载自签名根证书、无需修改 WinINet 系统代理/PAC、无崩溃后系统代理残留风险。
- **彻底解耦 LiteApp 前端形态**：不依赖 HTML、DOM、`WeixinJSBridge` 或 `window.xweb.worker`，兼容 4.2.0.53+ LiteApp 全部入口（推荐流、聊天卡片、作者主页、独立窗口）。
- **代码与体积大幅精简**：移除 `Titanium.Web.Proxy` 及一整套代理/证书/注入/残留恢复基础设施。
- **精准低噪与低开销**：`NetworkService` 轮询单次 ~22 ms，刷 N 条视频只出 ~N 条（至多含 1 条微信预缓冲的下一视频），彻底消除 97 条预加载刷屏。

**负面（明确接受的代价）**

- **仅限 Windows**：依赖 Win32 `ReadProcessMemory`；非 Windows 平台仍走 `NullWeChatVideoCapture`。
- **进程权限对称要求**：若用户以管理员身份运行微信而以普通权限运行 JiuMi，点击开始监听时 `OpenProcess` 会拒绝访问并通过既有 `WeChatVideo.CaptureStartFailed` InfoBar 提示失败（用户可修正后点「重试」）。
- **杀软误报风险**：跨进程 `ReadProcessMemory` 在极少数安全软件下可能被关注（虽无 `PROCESS_VM_WRITE` 与注入行为，风险远低于 Hook，仍需在发布前关注 Defender 表现）。
- **依赖微信进程内存形态**：若未来微信把 `decode_key` 改为二进制 protobuf 专有结构、不再以 JSON 文本驻留，或合并 `NetworkService` 进程，需要重新验证并调整解析器。
- **旧版根证书存量**：此前使用过旧版 JiuMi 的用户 `CurrentUser\Root` 中可能仍留有 `CN=JiuMi` 证书；移除设置页证书段前，需决定是否在发布说明中给出手动清理指引，或保留一个轻量的「清理旧版证书」按钮过渡一个版本。

## 不在本 ADR 内

- 具体实现拆分、工单排期与代码重构步骤。
- 多清晰度 `spec` 选择 UI（首期沿用默认直链，后续可按需扩展）。
- 4 条 `HTTP 400` 边缘样本（约 4%）的 URL 参数细节排查（在实现 `WeChatMediaJsonExtractor` 单测时一并覆验）。
- `PublishTrimmed` 兼容性（归 [#66](https://github.com/ZLiLiang/JiuMi/issues/66)；移除 `Titanium.Web.Proxy` 后该阻塞因素将自然消失）。

## 参考

- 被替代的旧决策：[ADR-0013 微信视频下载：MITM 抓取链路与按需代理](./0013-wechat-video-download.md)
- LiteApp 失效研究：[#182](https://github.com/ZLiLiang/JiuMi/issues/182)（[研究记录](../research/182-wechat-wmpf.md)）
- 实机验证原型（本地保留，不入库）：`artifacts/wechat-liteapp-proto/`
