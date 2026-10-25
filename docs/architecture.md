# 架构

JiuMi 应用外壳的设计基线。本文件记录跨模块的结构性规则；单点决策的完整论证在 `docs/adr/`，每条规则都能追溯到一份决议或 ADR。

## 启动序列

启动是一个显式状态机。splash 的退场由**两道门取晚者**决定——「动画与最短展示已结束」与「初始化已就绪」，就绪另设 10 秒兜底。它不是某个定时器到点就交接。

### 状态

| 状态 | 进入条件 | 退出条件 |
| --- | --- | --- |
| `Bootstrapping` | 进程启动 | DI 容器建好（此阶段不做 IO） |
| `SplashShown` | 容器就绪；按资源可用性选定降级档位并显示 splash | `Opened` 之后的下一个渲染帧启动计时与 GIF |
| `Animating` | GIF 播放中（`IterationCount` 保持 `Infinite`） | GIF 时长到点：就绪则交接，未就绪转入 `Waiting` |
| `Waiting` | 动画已停、初始化未就绪；停画（`Source = null`）并显示不确定进度环 | 就绪，或 10 秒兜底到点 |
| `Handoff` | 已过 `max(1000ms, GIF 时长)` **且**（就绪 **或** 已过 10000ms） | 200ms 交叉淡入淡出完成 |
| `Running` | splash 关闭、主窗口不透明度 1 并激活 | —— |
| `Faulted` | 初始化抛未捕获异常、拿不到可用状态 | 终态：不进主窗口，呈现见错误呈现章节 |

`Handoff` 的判定是一个表达式，不是三个定时器各自触发：`elapsed >= max(1000ms, gifDuration) && (ready || elapsed >= 10000ms)`。

### 初始化内容

splash 显示之后、`Handoff` 之前完成的初始化，按依赖顺序：

1. 配置探针 → 设置加载。探针必须先于首次读取（规则见配置章节）。
2. 本地化：读 `languages.json`，按已存语言的解析链加载文案（规则见本地化章节）。
3. 主题应用。
4. 主窗口构造并赋给 `MainWindow`——必须早于 `Handoff`。

全部完成后置「就绪」。单实例抢占与启动期的激活请求见单实例与窗口生命周期章节。

### 计时

- **起点**：splash 的 `Opened` 事件之后的**下一个动画帧**（`TopLevel.RequestAnimationFrame`），不是窗口创建时刻——否则首帧渲染会吞掉动画开头。
- **GIF 时长**：按帧延时累加。解码器规则为 `delayMs = delayCentiseconds * 10`，`delayMs <= 10` 时按 100ms 计。到点即 `Source = null` 停止绘制。
- **最短展示**：1000ms。GIF 短于 1 秒时补到 1 秒；长于 1 秒时以 GIF 时长为准，不截断。
- **就绪兜底**：10000ms。到点强制交接并记 Warning（`Startup.Timeout`）。
- 三者始终取晚者，不相加。
- 超过 10 秒进入主窗口后，**迟到的初始化静默应用**（主题与语言走既有热切换路径），并记一条 Error 说明「初始化超时后完成」。

### 单次播放由应用计时

`Avalonia.Labs.Gif 12.0.2` 的 `GifImage` 只当播放器用，`IterationCount` 保持 `Infinite`。它的迭代语义不可依赖：

- 没有 `AnimationCompleted`、没有 Play/Stop/Pause、没有播放状态属性，唯一控制是 `IterationCount`。
- 判断为 `_iterationCount > IterationCount.Value`，且只在帧索引变化时更新，因此设 `N` 实际播 `N+1` 遍；单帧 GIF 因帧索引不变而永不停止。
- 播完是**停止绘制**，不是定格末帧。
- GIF 内嵌的 NETSCAPE 循环次数被解析但从不使用。

结论：单次播放与结束时机全由应用按帧延时计时。

### 资源降级阶梯

| 档位 | 条件 | 表现 |
| --- | --- | --- |
| ① 正常 | `splash.gif` 可加载、可构造 `GifStreamSource` | GIF 播一遍 → 就绪慢则停画 + 进度环 |
| ② 静帧 | GIF 缺失，或构造/头部解析抛异常（只能按 `Exception` 捕获，异常类型是 internal） | `splash.png` poster + 进度环**立即**显示；1000ms 底与就绪取晚者 |
| ③ 纯背景 | poster 也缺失 | 不透明背景 + 进度环，计时同 ② |
| ④ 无窗口 | splash 窗口本身建不出来 | 不显示窗口，静默等就绪（10s 兜底）后进主窗口，记 Error |

- 降档只记 Debug / Error，不打扰用户。
- **渲染期 LZW 解码错误不可控**：它发生在合成线程，被 Avalonia 捕获并仅写日志，应用抓不到；表现为 GIF 区停在上次绘制画面，计时照常推进。这是已知不可控项，不构成状态分支。

### splash 窗口

- `SystemDecorations=None`、`CanResize=false`、`ShowInTaskbar=false`、居中、不可拖动。
- **不透明背景**：规避 GIF alpha 被 `AlphaFormat.Opaque` 渲染成黑。
- 尺寸与进度环视觉形态归 UI 规格。

### 交接

- 主窗口在 `Handoff` 前已构造完成。
- 进入 `Handoff` 时主窗口以 `Opacity = 0` 显示在 splash 之上，两窗同时做 200ms 淡变；结束时关闭 splash、主窗口 `Opacity = 1` 并激活。
- 不用 `Topmost`：0 不透明度的主窗口就在上层，splash 自然透出。
- `ShutdownMode = OnMainWindowClose` 且 `MainWindow` 已赋值，因此关闭 splash 不会结束进程。

### 不允许跳过

点击与 Esc 均不响应。展示时长下限 1 秒且只播一遍，跳过能省的时间有限；更关键的是跳过无法越过就绪门槛，真正可能拖住用户的是初始化。

### 启动期失败

启动期**一律不弹模态框**：所有告警进队列，主窗口出现后以 InfoBar 呈现，同类合并。呈现形态见错误呈现章节。

| 失败 | 行为 |
| --- | --- |
| 设置文件损坏 | 改名 `.bak`，用默认值继续启动 |
| 配置落点回退到 AppData | 每会话一次的非阻塞提示 |
| AppData 也不可写 | 仅内存运行 + 常驻提示 |
| GIF 加载或构造失败 | 静默降档，只记日志 |
| 主题资源缺失 | 回退 Fluent 默认主题，只记 Debug |
| 语言文件缺失或解析失败 | 跳过该语言（见本地化章节） |
| 初始化超过 10 秒 | 「启动未完全就绪」告警 |

## 单实例与窗口生命周期

同一个 exe 副本在同一用户会话里只跑一个进程：第二次启动不建窗口，只把已有窗口叫到前台。托盘只承接「最小化」，**关闭仍然是退出**。决议见 [本期是否实现单实例与托盘行为？](https://github.com/ZLiLiang/JiuMi/issues/7)。

### 单实例

| 项 | 规则 |
| --- | --- |
| 机制 | 命名互斥量判重 + 命名管道递激活请求 |
| 名称派生 | `SHA-256(exe 完整路径)` 取前 16 字节十六进制，加前缀 `JiuMi.`；互斥量与管道同名 |
| 作用域 | 用 `Local\` 命名空间（默认），即「当前用户会话 + 当前 exe 路径」；不使用 `Global\` |
| 第二实例 | 向管道写一条 `SHOW` 后关闭连接，随即以退出码 `0` 退出；不建窗口、不弹提示、不显示 splash |
| 首实例 | `Main`（Desktop 组合根）先创建互斥量与管道监听，**再**启动 Avalonia；监听在后台线程 |
| 端口 | `JiuMi.Application` 定义 `ISingleInstanceCoordinator`（`TryAcquire()` 与 `ActivationRequested`），命名与管道细节留在 Infrastructure；UI 只订阅事件 |
| 连不上管道 | 互斥量在、管道无响应时视为前一进程正在退出：记 Debug，仍以 `0` 退出 |

- **为什么按路径而非应用名**：便携程序可能被复制到不同目录做不同用途，全局锁会误伤；不同副本的 `config\` 本来就不同，不存在写冲突。
- 崩溃残留不用清理：互斥量是内核对象，进程结束即释放；管道随监听线程消失，下次启动重新获取。

### 启动期请求排队

- 「互斥量已建、管道已监听、UI 尚未就绪」这段窗口里到达的请求不能丢。
- 请求只记**一个待办激活标记**，不按次数累积；splash 或主窗口可用后消费一次，按下表执行。不为此新加状态（状态机见启动序列章节）。

### 激活规则

按当前顶层状态选目标窗口，一次请求只激活一个：

| 当前状态 | 目标 |
| --- | --- |
| splash 可见（`SplashShown` / `Animating` / `Waiting`） | 激活并置前 splash |
| `Running`，主窗口隐藏到托盘 | 恢复显示、还原窗口状态、激活 |
| `Running`，主窗口可见但被遮挡 | 激活并置前 |
| 主窗口不可用、只剩错误窗口 | 聚焦错误窗口 |
| `Faulted` 且无任何窗口 | 不做任何事（进程即将退出） |

- 不新建第二个窗口，也不重跑启动序列。

### 窗口生命周期

| 事件 | 行为 |
| --- | --- |
| splash 关闭 | 不结束进程（`ShutdownMode = OnMainWindowClose`，`MainWindow` 已赋值） |
| 主窗口最小化 | 隐藏窗口并从任务栏移除，常驻托盘；每会话第一次投递一条系统通知 |
| 主窗口从托盘恢复 | 显示 → 还原到最小化前的尺寸与窗口状态 → 激活 |
| 主窗口关闭 | 退出进程（码 `0`）；托盘菜单「退出」走同一条路径 |
| 进程退出 | 销毁托盘图标，关闭管道监听并释放互斥量 |

- **关闭按钮不隐藏到托盘**，`ShutdownMode` 不变（与启动序列章节一致）。
- 最小化到托盘只针对主窗口；splash 不可最小化（见启动序列章节），因此启动期没有托盘入口。

### 托盘

| 项 | 规则 |
| --- | --- |
| 创建时机 | 主窗口第一次显示后（进入 `Running`）创建；进程退出时销毁 |
| 图标 | 暂用占位资源，与任务栏图标同一份；正式品牌资源未定 |
| 左键单击 | 显示并激活主窗口（同激活规则） |
| 右键菜单 | 显示主窗口 / 设置 / 退出，文案键 `Shell.Tray.Show` / `Shell.Tray.Settings` / `Shell.Tray.Exit` |
| 「设置」项 | 恢复并激活窗口后，走导航栏「设置」同一条导航命令 |
| 平台 | 只在 Windows 实现；其他平台为 no-op，其最小化行为随跨平台分发另行决定 |

- 首次最小化提示的文案取 `Shell.Tray.MinimizedHint`，经错误呈现章节的 `IUserNotification` 端口投递，不用托盘气泡 API。
- 四个 `Shell.Tray.*` 键已登记在本地化章节的已登记键表，顶层白名单不变。
- 托盘与单实例的实现放 Infrastructure；`#if WINDOWS` 只出现在 Infrastructure 与 Desktop 组合根。

### 隐藏期间的错误投递

- 隐藏到托盘 = 没有可见窗口 → 非阻断 Error 走系统通知（触发规则见错误呈现与诊断章节）；点击通知恢复主窗口并呈现对应 InfoBar。
- 错误窗口本身就是可见窗口：它在显示时不触发系统通知。
- 托盘图标不承担状态提示：不闪烁、不加角标。

### 与并发写的关系

- 单实例锁只覆盖「同一用户会话 + 同一 exe 路径」。不同用户会话、或共享盘上两台机器仍可能同时写同一个 `config\settings.json`：接受最后写入者胜，不加跨进程文件锁（见配置与外置文件章节）。

## 错误呈现与诊断

错误只按**是否阻断**分两级。用户可见面是**全局 InfoBar、容器内占位、独立错误窗口**三类；系统通知只是「没有可见窗口」时的投递兜底。用户能拿到的「详情」不是另造的数据结构，就是一条错误记录。

决议见 [各类错误发生时用户应该看到什么？](https://github.com/ZLiLiang/JiuMi/issues/6)，论证见 [ADR-0003](./adr/0003-error-presentation.md)，视觉细节见 [`docs/ui-spec.md`](./ui-spec.md)。

### 分级

| 级别 | 判定 | 用户契约 |
| --- | --- | --- |
| 非阻断 | 主窗口可用，操作未完成但界面能继续用 | 告知，可选重试；不阻断任何操作 |
| 阻断 | 主窗口不存在或不可用，没有可继续的操作 | 只剩「复制详情」与「退出」 |

- 级别只由**主窗口是否可用**决定，不由异常类型、来源模块或调用方自报的严重程度决定。
- 不存在第三级：可恢复与功能失败在用户侧是同一个契约，差别只落在文案与有没有「重试」。

### 载体

| 载体 | 职责 | 约束 |
| --- | --- | --- |
| 全局 InfoBar | 默认载体：启动期入队的告警、会话中途的失败 | 内容区顶部、导航栏右侧，推挤内容不覆盖；跨页常驻 |
| 容器内占位 | 局部资源加载失败（GIF / hero / 图标） | 只降级该容器，不弹提示 |
| 独立错误窗口 | 阻断级失败；以及主窗口可用时的 UI 线程未捕获异常 | 不依赖主窗口、本地化资源与主题资源 |
| 系统通知（端口） | 无可见窗口时投递非阻断 Error | 其余平台为 no-op |

- **不做 `ContentDialog`**：本期唯一的阻断场景发生在启动期，主窗口不存在、没有 owner；其余场景已定不弹模态（见启动序列章节与 ADR-0002），留着就是死代码。
- **不做状态栏**：外壳没有常驻状态区，为报错单独造一条不划算。

### 场景映射

| 场景 | 级别 | `code` | 载体 | 动作 |
| --- | --- | --- | --- | --- |
| 会话中途设置保存失败 | Error | `Settings.SaveFailed` | InfoBar | 重试 + 复制详情 |
| 设置文件损坏（改名 `.bak`，用默认值继续） | Warning | `Settings.Corrupted` | 队列 → InfoBar | 复制详情（正文说明已备份） |
| 清单内的语言文件缺失或解析失败 | Debug | — | 仅日志 | —（见本地化章节） |
| 用户主动切换语言失败 | Warning | `Localization.SwitchFailed` | InfoBar | 说明已回退，选择回弹 |
| 主题跟随系统时应用失败 | Debug | — | 仅日志 | —（回退 Fluent 默认主题） |
| 用户主动切换主题失败 | Warning | `Theme.SwitchFailed` | InfoBar | 选择回弹 |
| splash / hero 的 GIF 加载或构造失败 | Debug | — | 容器内占位 | —（见启动序列章节的降级阶梯） |
| 配置落点回退到 AppData | Warning（每会话一次） | `Config.FallbackToAppData` | InfoBar | 打开文件夹 |
| 配置两处都不可写 → 仅内存 | Error（常驻、不可关闭） | `Config.MemoryOnly` | InfoBar + 设置页「关于」 | 复制详情 |
| 导出诊断包失败 | Error | `Diagnostics.ExportFailed` | InfoBar | 重试 + 复制详情 |
| 初始化超过 10 秒 | Warning | `Startup.Timeout` | 队列 → InfoBar | 复制详情 |
| 初始化抛未捕获异常（`Faulted`） | 阻断 | `Startup.Faulted` | 错误窗口 | 复制详情 + 退出 |
| 主窗口构造或显示失败 | 阻断 | `Shell.MainWindowFailed` | 错误窗口 | 复制详情 + 退出 |
| UI 线程未捕获异常（主窗口可用） | 非阻断 | `Unexpected.UiThreadException` | 错误窗口 | 继续运行 / 退出 / 复制详情 |

横切规则：

- 同一 `code` 在一次会话内合并为一条并显示次数；Error 排在 Warning 之前。
- 未登记的 `code` 默认仅日志，不弹任何载体。
- 「重试」= 用当前内存状态重跑一次该操作；再失败只把次数 +1，不新增提示。
- 映射表是「每个 `Error.*` 文案键都能被某个 `code` 映射到」这条单测的依据（见本地化章节单测第 5 项）。

### 错误记录

| 字段 | 说明 |
| --- | --- |
| `timestamp` | ISO 8601 带偏移 |
| `level` | Warning / Error；Fatal 另写崩溃文件（见日志与诊断章节） |
| `code` | 稳定事件名，如 `Settings.SaveFailed`，对应文案键 `Error.Settings.SaveFailed` |
| `operation` | 如 `Settings.Save` |
| `exceptionType` / `exceptionMessage` / `stackTrace` | 仅在捕获到异常时存在 |
| `appVersion` / `osDescription` | 环境 |
| `configPath` | 当前生效的配置落点（见配置章节） |
| `count` | 会话内合并次数 |

- **来源**：内存环形缓冲（最近 50 条），由产生这条提示的同一处写入；呈现层不另造字段。50 条覆盖「看到提示 → 去点复制」的窗口，同时保证内存有界。
- **复制格式**：本地化文案一行，随后 `key: value` 一行一字段，堆栈保持原样。
- **崩溃文件只承接致命**：`<日志目录>\crash-yyyyMMdd-HHmmss.txt`，含时间、版本、阶段或操作名、异常类型、消息、堆栈与当前配置落点；最多保留 5 份；两处都不可写则静默放弃，详情仍可复制。
- Warning 及以上同时写进文件日志（见日志与诊断章节）；内存缓冲仍是详情与合并计数的唯一来源。

### 报告通道与线程

- `JiuMi.Application` 定义 `IErrorReporter.Report(ErrorRecord)`；`JiuMi.Infrastructure` 提供实现，负责写入环形缓冲并发出进程内事件。
- UI 在组合根（Desktop）订阅，经 `Dispatcher.UIThread.Post` 编组入队。
- 任何层、任何线程都可以上报；UI 层不引用 Infrastructure。
- **是否显示由 UI 按 `code` 查映射表决定**，不由调用方声明：一套记录，一张表。

### 全局异常钩子

| 钩子 | 时机 | 行为 |
| --- | --- | --- |
| `Dispatcher.UIThread.UnhandledException` | UI 线程 | 主窗口可用 → 错误窗口（继续运行 / 退出 / 复制详情）；启动期 → `Faulted` 错误窗口；标记 `Handled`，不叠加 |
| `AppDomain.UnhandledException` | 非 UI 线程或终局 | UI 不可靠：只同步尽力写崩溃文件，不弹界面 |
| `TaskScheduler.UnobservedTaskException` | 被放弃的任务 | `SetObserved()` + 记 Warning，不弹界面 |

退出码：正常 `0`，致命 `1`。

### 系统通知

- 抽象 `IUserNotification` 在 `JiuMi.Application`；Windows 实现（`AppNotification` toast，有托盘时可用 `NotifyIcon` 气泡）在 `JiuMi.Infrastructure`，其他平台 no-op。
- **触发**：仅当应用没有可见窗口（关闭或最小化到托盘）时投递非阻断 Error；窗口可见或最小化到任务栏只走 in-app。
- **内容**：标题为应用名，正文只用本地化短句、不带技术细节；点击激活主窗口并呈现对应 InfoBar。
- 托盘图标本身归单实例与窗口生命周期章节；toast 不依赖托盘。本期没有后台任务，实际触发点为零，但端口与实现先建好。
- 平台专属 API 允许使用，但必须隔离在 Infrastructure（经 Application 抽象暴露）；`#if WINDOWS` 只允许出现在 Infrastructure 与 Desktop 组合根，UI / Application 只认抽象。

## 日志与诊断

日志是**旁路**：它记录发生了什么，不参与任何功能判定，写不进去也不能影响功能路径。日志文件、崩溃文件、详情缓冲与诊断包服务不同读者，但共用一套脱敏规则。

决议见 [日志与崩溃报告设施应做到什么程度？](https://github.com/ZLiLiang/JiuMi/issues/11)，论证见 [ADR-0004](./adr/0004-logging.md)。

### 依赖与边界

| 包 | 版本 | 用途 |
| --- | --- | --- |
| `Serilog` | 4.4.0 | 日志核心 |
| `Serilog.Sinks.File` | 7.0.0 | 文件 sink（`net8.0` 目标，`net10.0` 兼容） |
| `Serilog.Formatting.Compact` | 3.0.0 | `CompactJsonFormatter` |
| `Serilog.Sinks.Debug` | 3.0.0 | 仅 DEBUG 构建，输出到调试器 |

- **不引入** `Serilog.Extensions.Logging`（MEL 桥接）、`Serilog.Sinks.Async`、`Serilog.Settings.Configuration`、`Serilog.Settings.AppSettings`。
- `JiuMi.Application` 直接依赖 `Serilog.ILogger`；`LoggerConfiguration` 只在 Desktop 组合根与 Infrastructure 组装，UI 不引用 sink 类型，也不读日志文件。
- 平台边界同错误呈现章节：`#if WINDOWS` 只出现在 Infrastructure 与 Desktop 组合根。

### 位置与生命周期

| 项 | 规则 |
| --- | --- |
| 目录 | `<exe 目录>\logs\`；exe 侧不可写时为 `%AppData%\JiuMi\logs\` |
| 与配置的关系 | 跟随配置启动探针的结论，与 `config\` **同级**；绝不进 `data\` |
| 创建时机 | 首次写日志之前建目录 |
| 多进程 | `shared: false`；#7 的单实例保证同一 exe、同一用户会话只有一个进程，第二实例在打开日志前退出 |
| 收尾 | 正常退出与致命退出都调用 `Log.CloseAndFlush()` |

### 级别与过滤

- 五级：`Debug` / `Information` / `Warning` / `Error` / `Fatal`；**不产生 `Verbose`**。
- 发布构建默认 `Information`；`Debug` 需显式开启。
- 环境变量 `JIUMI_LOG_LEVEL` 可覆盖，取值为五个级别名；非法值回退 `Information`，并记一条自诊断 Warning。
- **设置页不提供级别开关**，级别也不写进 `settings.json`——避免污染半稳定契约（见配置与外置文件章节）。

### 格式与事件字段

- 格式：Compact JSON Lines（`CompactJsonFormatter`），一行一个事件，UTF-8 无 BOM。
- 文件名：`jiuMi-yyyyMMdd.jsonl`；单文件达 5 MB 时追加 `_001`、`_002` 序号继续写。
- 时间：文件里的 `@t` 为 UTC；「复制详情」保持错误记录里的带偏移本地时间。
- enricher 为每条事件附加 `appVersion`、`osDescription`、`configPath`。
- `Warning` 及以上由 `IErrorReporter` 附带 `code` / `operation` / `count`，与错误记录的字段一一对应：日志事件与错误记录是同一组字段的两种形态，不是两套数据结构。
- 消息模板固定**英文**；UI 文案另走本地化资源，日志不依赖 i18n 键。
- 「复制详情」仍是 `key: value` 文本；与文件日志**共用脱敏规则，不共用序列化格式**。

### 覆盖范围

固定最小集：

- 启动状态机七态，各一条 `Information`
- 配置探针结果与生效落点
- 设置保存结果
- 语言切换、主题切换
- 主窗口显示 / 关闭、过渡到托盘
- 所有异常路径（钩子分工见错误呈现章节）

页面内部细节（导航切换、控件状态）走 `Debug`。**禁止散落的 `Debug.WriteLine`**：全部走 Serilog，DEBUG 构建由 `Serilog.Sinks.Debug` 汇入同一条事件流。

### 写入与失败降级

- 同步、unbuffered；**不加 Async sink**——桌面量级用不上异步队列的吞吐，还会多一个组件与一处崩溃刷盘风险。
- `Fatal` 由 sink 立即刷盘。
- 文件写入失败：`SelfLog.Enable` 输出到调试器；**不弹 InfoBar**，只在设置页「关于」区显示「日志不可用」状态（`Settings.About.LoggingUnavailable`）。
- 日志失败绝不影响功能路径。

### 崩溃文件

沿用错误呈现章节的独立契约，不并入统一 sink：

- 路径 `<日志目录>\crash-yyyyMMdd-HHmmss.txt`，纯文本 `key: value` + 堆栈，最多保留 5 份。
- 由 `AppDomain.UnhandledException` 钩子同步尽力写；两侧都不可写则静默放弃，详情仍可复制。
- 同一 `Fatal` 事件同时写进常规 JSONL 日志。

### 脱敏

- 唯一规则：把用户目录前缀替换为 `%USERPROFILE%`（覆盖 `C:\Users\<name>` 的大小写变体）。
- 其余路径、异常消息、堆栈原样保留——诊断需要它们。
- 不记录设置值、凭据、文件内容。
- 文件日志、「复制详情」、诊断包共用这一套规则。

### 保留与清理

- `rollingInterval: Day`、`fileSizeLimitBytes: 5 MB`、`rollOnFileSizeLimit: true`。
- `retainedFileCountLimit: 30`，约 150 MB 硬上限。
- 清理由 sink 在滚动时执行；不自建清理器，也不在启动时扫描目录。
- 崩溃文件按 5 份单独保留。

### 详情缓冲

- 保留错误呈现章节的 50 条内存环形缓冲，作为 Warning / Error 合并与「复制详情」的唯一来源。
- `Warning` 及以上同时落文件；`Debug` / `Information` 只进文件。
- UI **不读**日志文件。

### 诊断导出

| 项 | 规则 |
| --- | --- |
| 入口 | 设置页「关于」区，「打开文件夹」旁 |
| 交互 | 保存对话框；默认文件名 `JiuMi-diagnostics-yyyyMMdd-HHmmss.zip`，默认目录为当前生效的 `logs\` |
| 内容 | 保留期内全部 `.jsonl` + 现有 `crash-*.txt` + `settings.json` 快照 + `diagnostics.txt`（应用版本、OS、生效路径、语言、主题、日志级别覆盖、生成时间） |
| 实现 | BCL `System.IO.Compression.ZipFile`，零额外依赖 |
| 失败 | 非阻断 Error（`Diagnostics.ExportFailed`）+ 复制详情，不阻断 |

### 不外发

崩溃报告、日志与诊断包一律**不外发**；诊断包只能由用户手动导出并自行发送。将来若要外发，涉及同意与隐私，需单独立项。

## 配置与外置文件

设置是一份**单文件**的状态，位置在启动时判定一次，此后本次会话的读取与写入都指向同一个文件。

### 位置

| 顺序 | 位置 | 条件 |
| --- | --- | --- |
| 1 | `<exe 目录>\config\settings.json` | 探针通过 |
| 2 | `%AppData%\JiuMi\settings.json` | exe 侧不可写，且 AppData 可写 |
| 3 | 仅内存 | 两处都不可写；常驻可见提示 |

- **不迁移、不合并**：回落到 AppData 时不读取 exe 侧旧文件，不复制作种子。当前位置由本次探针决定，下次启动重新判定。
- **读写同源**：不存在「读 exe、写 AppData」的混合状态。
- 非 Windows 的回落路径不在本期范围。

### 启动探针

- 对 `<exe>\config\` 做写探针：建临时文件再删除。
- 若 `settings.json` 已存在，再单独探它能否以写方式打开。
- 目录与文件都通过，才判定 exe 侧可用。
- 探针必须在**首次读取配置之前**完成；它在启动序列中的落点见启动序列章节。
- 探针查不出会话中途的写失败（磁盘满、文件被占用、U 盘拔出）。此时保持当前位置不变，给一次非阻塞提示，下次启动重新探针。

### 目录职责

| 目录 | 内容 | 本期 |
| --- | --- | --- |
| `<exe>\config\` | 用户可改的应用状态 | 创建 |
| `<exe>\data\` | 工具产物，按工具分子目录 | 预留，不创建 |
| `<exe>\logs\` | 日志、崩溃文件与诊断包的来源 | 创建 |

三者语义分开：`config` 是应用状态，`data` 是工具产物，`logs` 是排障输出。exe 侧不可写时，`logs` 与 `config` 一起回落到 `%AppData%\JiuMi\`（见日志与诊断章节）。

### 文件契约（半稳定）

允许用户直接手改，但只承诺「读得进来的旧值」：

- 缺键 → 用默认值补齐，不报错。
- 未知键 → 读时忽略，**重写时丢弃**。降级不保真：新版本写过的文件回到旧版本会丢失旧版本不认识的键。
- JSON 损坏 → 原文件改名为 `settings.json.bak`（单份，每次覆盖），用默认值继续启动，并给出可见提示。
- 始终写入 `"schemaVersion": 1`，作为将来迁移的锚点。
- 不做严格校验，也不因配置错误拒绝启动。

### 窗口与导航状态

本期只持久化**一比特** UI 状态：导航栏是展开还是收起。窗口几何不进配置。

| 键 | 类型 | 缺省 | 含义 |
| --- | --- | --- | --- |
| `navExpanded` | boolean | `true` | 导航栏展开（`true`）还是收起（`false`） |

- 窗口尺寸、位置、最大化与还原尺寸**不记忆**：每次启动按 1200 × 760 打开，位置由系统决定（见 UI 规格）。理由见 [ADR-0005](./adr/0005-window-state-memory.md)。
- `navExpanded` **只在主窗口关闭时写一次**；用户点展开/收起按钮不触发写盘，拖动、缩放、频繁切换都不会引起整文件重写。
- 关闭时写入失败**静默**：记一条不带 `code` 的 Warning 日志（操作 `Settings.SaveOnExit`），不进详情缓冲、不弹 InfoBar。此时窗口正在关，用户无从处理；「保存失败必须可见」的契约针对用户主动修改的设置。
- 该键属新增键，`schemaVersion` 保持 `1`：缺键补默认即可，不是迁移。

### 写入

- **整文件写**：不做增量或局部写。
- 同目录临时文件（`settings.json.tmp`）→ `Flush(true)` 落盘 → 同卷原子重命名替换。临时文件必须在同一目录，跨卷重命名不是原子操作。
- 不加跨进程文件锁，接受**最后写入者胜**；原子替换保证任何时刻磁盘上都是完整文件。
- 崩溃残留的 `settings.json.tmp` 由下次启动清理。

### 用户可见性

- 设置页「关于」区常显**当前生效路径**，并提供「打开文件夹」。
- 回落到 AppData 时，每会话最多一次非阻塞提示。
- 提示的控件形态属于 UI 规格。

## 本地化

界面文案是**外置数据**，不是代码里的常量：加一门语言只加一个文件与一条清单记录，不改代码。

### 文件与结构

- 位置：`Assets/i18n/{zh-CN,en-US}.json`，随程序发布。
- **扁平键**：一个键就是一条完整文案，运行时只加载为一个 `Dictionary<string, string>`，不做嵌套对象与拍平。
- 文件里只有「键 → 文案」，不写分组、注释或元数据。

### 键命名

**语义分层、PascalCase、点分隔**：`<顶层命名空间>.<语义路径>.<名称>`。

顶层命名空间是白名单，只有六个：

| 顶层 | 用途 |
| --- | --- |
| `Shell.*` | 窗口、导航、托盘 |
| `Home.*` | 主页 |
| `Settings.*` | 设置页 |
| `Tool.*` | 工具页 |
| `Common.*` | 跨页复用且**含义相同**的文案 |
| `Error.*` | 错误文案，按稳定事件码命名 |

规则：**含义不同就是不同的键，哪怕中文写法一样。**「设置」作为导航项是 `Shell.Nav.Settings`，作为页面标题是 `Settings.Title`；「主页」同理拆成 `Shell.Nav.Home` 与 `Home.Title`。

已登记的键（新增键先在此登记，再进 JSON）：

| 键 | 含义 |
| --- | --- |
| `Shell.Nav.Home` / `Shell.Nav.Settings` | 导航项 |
| `Shell.Tray.Show` / `Shell.Tray.Settings` / `Shell.Tray.Exit` | 托盘右键菜单 |
| `Shell.Tray.MinimizedHint` | 首次最小化的系统通知 |
| `Home.Title` / `Settings.Title` | 页面标题 |
| `Shell.Nav.Expand` / `Shell.Nav.Collapse` | 导航栏展开/收起按钮的可访问名，按当前状态取键 |
| `Home.Subtitle` / `Home.Hero.Title` / `Home.Hero.Body` / `Home.Tools.Title` | 主页副标题、hero 文案与工具分区标题 |
| `Tool.*.Name` / `Tool.*.Description` | 四张工具卡与导航项共用的名称与描述；`*` 取 `WeChatDownload` / `BatchRename` / `IconConvert` / `WorldClock` |
| `Settings.Subtitle` | 设置页副标题 |
| `Settings.Language.Section` / `Settings.Language.Label` / `Settings.Language.Hint` | 「语言和区域」分区标题与行 |
| `Settings.Appearance.Section` / `Settings.Appearance.Label` / `Settings.Appearance.Hint` | 「外观」分区标题与行 |
| `Settings.Appearance.FollowSystem` / `Settings.Appearance.Light` / `Settings.Appearance.Dark` | 主题三选项 |
| `Settings.About.Section` / `Settings.About.Version` / `Settings.About.Repository` | 「关于」分区标题、版本行与仓库链接；`Version` 含 `{0}` |
| `Settings.About.ExportDiagnostics` / `Settings.About.LoggingUnavailable` | 「关于」区的「导出诊断包」按钮与「日志不可用」状态 |
| `Common.ComingSoon` | 工具卡与导航项上的「即将推出」 |
| `Common.Retry` / `Common.OpenFolder` / `Common.CopyDetails` / `Common.Copied` / `Common.Close` | 错误呈现的操作、复制反馈与关闭按钮可访问名 |
| `Common.ErrorCountSuffix` | 合并提示的次数后缀，`{0}` 为次数 |
| `Error.<事件码>` | 与 `ErrorRecord.code` 一一对应，如 `Error.Settings.SaveFailed`；事件码清单见错误呈现与诊断章节的场景映射 |

### 占位符

- 索引占位符 `{0}`、`{1}`…，服务提供 `Format(key, args)`，内部走 `string.Format`，允许译者调整参数顺序。
- **复数规则本期不做**：外壳没有带计数的文案，现在引入 `.one` / `.other` 约定或 ICU MessageFormat 依赖是为猜测买单。将来某个工具真需要计数时，按后缀补键并单开票。

### 语言清单

`Assets/i18n/languages.json`：

```json
{
  "source": "zh-CN",
  "languages": [
    { "code": "zh-CN", "displayName": "中文（简体）" },
    { "code": "en-US", "displayName": "English" }
  ]
}
```

- `source`：回退语言，也是解析链路的终点；不逐条写 `isSource`。
- `code`：具体语言代码，与同名的文案文件对应。
- `displayName`：**母语名**，不随界面语言变化——看不懂当前界面语言的人仍要能找回自己的语言。

### 首次启动与切换

- 无已存设置时按 **精确匹配 → 语言子标签 → 源语言** 落定：`zh-Hans-CN` → `zh-CN`，`en-GB` → `en-US`，`ja-JP` → `zh-CN`；不弹窗询问。
- 解析结果**立即作为具体代码持久化**；语言选择器是具体语言的平铺列表，**没有「跟随系统」**（只有主题保留跟随系统）。
- 已存代码不在清单里（手改、拼错）：视为不支持，走同一条解析链路并记 Debug 日志，不弹窗、不报错。
- 匹配大小写不敏感：`zh-cn` 命中 `zh-CN`。
- 切换即时生效：`LocalizationService` 暴露索引器与 `PropertyChanged`，文案全部走绑定，无需重启。
- **作用域文化跟随界面语言**，不跟随操作系统：英文界面显示 `10/18/2026`、`1,234.5`。
- 清单里有、但文件缺失或解析失败的语言：记 Debug 日志并**跳过该语言**，不影响启动。

### 缺键回退

运行时回退顺序：**目标语言 → `zh-CN` → 键名**。

- 命中退路记一条 Debug 日志；**绝不抛异常**。
- 直接回显键名，让遗漏在界面上暴露出来。

### 单测

构建期失败，覆盖：

1. 各语言 JSON 的**键集合完全相同**。
2. 每个键的**占位符索引集合相同**（防英文版把 `{0}` 写丢）。
3. 键名匹配 `^[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*)+$`，且顶层在白名单内。
4. `languages.json` 列出的每个 `code` 都有对应文件且可解析。
5. 每个 `Error.*` 键都能被某个 `ErrorRecord.code` 映射到（与错误章节的映射表一致）。

## 参考

- [ADR-0001 设置持久化：JSON、位置判定与原子写](./adr/0001-settings-persistence.md)
- [ADR-0002 splash 启动序列：应用计时、两道门与降级阶梯](./adr/0002-splash-startup-sequence.md)
- [ADR-0003 错误呈现：两级模型、四类载体、详情即日志记录](./adr/0003-error-presentation.md)
- [ADR-0004 日志：Serilog 写 JSON Lines、崩溃文件独立、不外发](./adr/0004-logging.md)
- [ADR-0005 窗口状态：只记导航栏展开，不记窗口几何](./adr/0005-window-state-memory.md)
- [决议：日志与崩溃报告设施应做到什么程度？](https://github.com/ZLiLiang/JiuMi/issues/11)
- [决议：启动序列与 splash 退出应遵循什么状态机？](https://github.com/ZLiLiang/JiuMi/issues/4)
- [决议：本地化 JSON 的结构、键命名与缺键回退](https://github.com/ZLiLiang/JiuMi/issues/3)
- [决议：各类错误发生时用户应该看到什么？](https://github.com/ZLiLiang/JiuMi/issues/6)
- [决议：单实例与托盘（`Shell.Tray.*` 键名）](https://github.com/ZLiLiang/JiuMi/issues/7)
