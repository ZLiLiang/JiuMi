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
- **就绪兜底**：10000ms。到点强制交接并记 Error。
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

两者语义分开：`config` 是应用状态，`data` 是工具产物。

### 文件契约（半稳定）

允许用户直接手改，但只承诺「读得进来的旧值」：

- 缺键 → 用默认值补齐，不报错。
- 未知键 → 读时忽略，**重写时丢弃**。降级不保真：新版本写过的文件回到旧版本会丢失旧版本不认识的键。
- JSON 损坏 → 原文件改名为 `settings.json.bak`（单份，每次覆盖），用默认值继续启动，并给出可见提示。
- 始终写入 `"schemaVersion": 1`，作为将来迁移的锚点。
- 不做严格校验，也不因配置错误拒绝启动。

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
| `Common.ComingSoon` | 工具卡上的「即将推出」 |
| `Error.<事件码>` | 与 `ErrorRecord.code` 一一对应，如 `Error.Settings.SaveFailed` |

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
- [决议：启动序列与 splash 退出应遵循什么状态机？](https://github.com/ZLiLiang/JiuMi/issues/4)
- [决议：本地化 JSON 的结构、键命名与缺键回退](https://github.com/ZLiLiang/JiuMi/issues/3)
- [决议：单实例与托盘（`Shell.Tray.*` 键名）](https://github.com/ZLiLiang/JiuMi/issues/7)
