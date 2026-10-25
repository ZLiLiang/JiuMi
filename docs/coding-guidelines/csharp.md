# C# 编码规范

- 状态：已接受
- 适用范围：仓库内所有 C# 代码（含测试）；生成代码除外
- 依据：Microsoft Learn、.NET Runtime / Roslyn 官方风格指南、Framework Design Guidelines、Avalonia 官方文档
- 检索日期：2026-10-18
- 上位文档：[architecture-map.md](../agents/architecture-map.md)（按章节选读 [architecture.md](../architecture.md)）、[ADR](../adr/)、[GLOSSARY.md](../../GLOSSARY.md)

本文件是 Code Review 的参照标准。**必须/不得** 是硬规则；**应当/不应** 偏离需在 PR 说明理由。冲突顺序：ADR / 架构 / 术语表 > 本规范 > 根目录 `.editorconfig` > 官方默认值。生成代码不适用本规范，但不得手改。

产品契约不在本规范重复定义：错误分级、日志字段、配置回退、本地化键、平台边界以 [architecture.md](../architecture.md) 与 ADR 为准（按 [architecture-map.md](../agents/architecture-map.md) 选读）；本规范只规定代码如何实现这些契约。

## 1. 审查级别

| 级别 | 触发条件 | 处理 |
| --- | --- | --- |
| **Blocker** | 违反必须/不得；正确性、安全、数据丢失、分层破坏、未处理异常、异步死锁、资源泄漏 | 阻止合并 |
| **Major** | 违反应当/不应且有实际风险；API 设计、错误处理、可测试性、文化/格式 | 合并前修复或说明 |
| **Minor** | 可读性、重复、命名、结构；不改变行为 | 建议修复 |
| **Nit** | 纯格式/偏好，formatter 可修复 | 不阻塞 |

审查意见格式：`[Blocker] 文件:行 — 规则/CA-ID：问题与具体修复`。不要对未改动代码提纯风格意见。

## 2. 工程基线

- 新项目使用 `net10.0`；现有项目以 `.csproj` 为准；变更 TFM/语言版本需 ADR 或 PR 说明。
- 使用 SDK 默认稳定 C#；不得 `LangVersion=preview`。
- 所有项目 `<Nullable>enable</Nullable>`；CI 应让新增 warning 失败。
- 分析器基线由根目录 `Directory.Build.props` 设置：`AnalysisLevel=latest-recommended`、`EnforceCodeStyleInBuild=true`、`TreatWarningsAsErrors=true`。
- 格式与命名由根目录 `.editorconfig` 配置；`pwsh ./verify.ps1` 是唯一门禁命令（见 [build-and-verification.md](../agents/build-and-verification.md)）。
- 新增/升级 NuGet 包说明用途与替代方案；不得重复现有依赖（JSON `System.Text.Json`、日志 Serilog、MVVM `CommunityToolkit.Mvvm`）；不得预览版包。

## 3. 格式与命名

- UTF-8（C# 文件带 BOM，与 .NET 模板/Visual Studio 默认一致）、CRLF、4 空格、文件末换行、无行尾空白；Allman；控制流必须带大括号。
- 每行一条语句；最多一个连续空行；文件作用域命名空间；`using` 在 namespace 外，`System.*` 优先并排序。
- 一个文件一个顶层类型，文件名与类型名一致；`partial` 仅用于 XAML/生成器。
- 不用 `this.`；显式访问修饰符；`var` 仅在右侧类型明显时使用；避免 `#region`。
- `#if WINDOWS` 只允许在 Infrastructure 与 Desktop 组合根；UI/Application/Domain 禁止平台 API。
- 生成代码（`*.g.cs`、T4 `Keys.cs`）不得手改。

| 元素 | 规则 |
| --- | --- |
| 类型 / 方法 / 属性 / 事件 / 常量 | PascalCase；方法用动词，属性用名词/形容词，事件用动词或过去式 |
| 接口 | `I` + 形容词/名词短语 |
| 异步方法 | `Async` 后缀；同步方法不得加 |
| 私有字段 | `_camelCase`（本项目） |
| 参数 / 局部变量 | camelCase；类型参数 PascalCase，可用 `T` |
| 布尔成员 | 肯定语义：`CanSave`、`IsEnabled` |
| 集合 | 复数：`LanguageCodes` |
| 异常 / 特性 / EventArgs | `Exception` / `Attribute` / `EventArgs` 后缀 |
| 测试方法 | `方法_场景_预期` |

- 不得使用匈牙利命名法、无上下文缩写；常见缩写写作 `Json`、`Http`、`Uri`、`Id`、`Ui`。
- 名称表达职责；避免在 C# 类型/成员命名中使用 `Manager`、`Helper`、`Util`、`Common` 等兜底名称（本地化键的 `Common.*` 是既有契约，不在此列）。
- 命名空间与文件夹一致；UI 文案使用本地化资源键，不硬编码。

## 4. 正确性规则

### 可空性

- 可能为 `null` 的引用必须用 `?`；契约不允许 `null` 时不得标注可空。
- 外部输入在边界校验：`ArgumentNullException.ThrowIfNull`、`ArgumentException.ThrowIfNullOrWhiteSpace`。
- 用 `is null` / `is not null`；`!` 只用于刚校验过且编译器无法推断的地方，并写明原因。
- 不得 `#nullable disable`；不得全局抑制可空警告；对外返回空集合而不是 `null`。

### 异步

- `async` / `await` 到底；禁止 `.Result`、`.Wait()`、`GetAwaiter().GetResult()`。
- `async void` 仅事件处理器/框架 `void` 重写；内部必须捕获异常。
- 返回 `Task` / `Task<T>`；`ValueTask` 仅在有测量依据的热路径；`Async` 后缀。
- `CancellationToken` 放最后并向下传；不得静默忽略。
- 不用 `Task.Run` 包装 I/O；不用 `Task.Delay` 作为同步原语（动画/启动计时除外）；不 fire-and-forget；`IAsyncDisposable` 用 `await using`。
- Application/Infrastructure 用 `ConfigureAwait(false)`；UI 代码在 `await` 后要操作 UI 时必须回到 UI 线程。

### 异常

- 异常用于异常情况；可预期失败用 `TryXxx` / 结果类型；不得用异常做控制流。
- 抛具体异常；`throw;` 不 `throw ex;`；转换异常保留 inner exception。
- 不得静默吞 `Exception`；只在进程/UI 边界捕获并记录/转换；取消正常传播。
- 边界按 [ADR-0003](../adr/0003-error-presentation.md) 转 `ErrorRecord`：稳定 `code`、`operation`、`Error.<code>` 映射；技术细节进日志/详情，不进主提示。

### 资源与并发

- `IDisposable` / `IAsyncDisposable` 必须有明确所有者；`using` / `await using`；创建者释放。
- 注入的服务由 DI 容器释放，接收方不得 `Dispose`；终结器只用于非托管资源，优先 `SafeHandle`。
- 事件订阅必须对称取消；静态缓存必须有界；退出路径显式 flush/dispose。
- UI 只在 UI 线程访问；后台更新用 `Dispatcher.UIThread`；不得 lock `this` / `typeof(T)` / 字符串；不得 `Thread.Sleep`。

## 5. API、数据与项目边界

- 最小可见性；非继承点 `sealed`；无 public/protected 字段；用 `init` / `required`。
- 公开集合返回 `IReadOnlyList<T>` / `IReadOnlyDictionary<TKey, TValue>`；参数用 `IEnumerable<T>` / `IReadOnlyCollection<T>`；不得返回 `null` 集合。
- 不可变数据用 `record`；`struct` 仅用于小、不可变、不常装箱的值；不用可变 `struct`。
- 当前时间用 `TimeProvider`；字符串比较/格式化显式 `StringComparison` / `IFormatProvider`；标识符/配置/协议用 Ordinal/Invariant，用户文本用当前界面语言对应的 `CultureInfo`（不直接跟随操作系统，见 architecture 本地化章节）。
- 使用 `System.Text.Json` 并复用 options；配置容忍未知/缺键；不得 `BinaryFormatter`。
- 构造函数注入；禁止服务定位器/静态可变状态/直接 `new` 依赖；生命周期明确，短生命周期不得捕获进长生命周期。

| 项目 | 允许依赖 | 禁止 |
| --- | --- | --- |
| `JiuMi.Domain` | BCL | 其他 JiuMi 项目、Avalonia、Serilog、平台 API |
| `JiuMi.Application` | Domain、BCL、`Serilog.ILogger` | Avalonia、Infrastructure、Desktop、平台 API、sink 类型 |
| `JiuMi.Infrastructure` | Application、Domain、BCL、平台 API | Desktop、UI 控件、业务规则 |
| `JiuMi`（UI） | Application、Avalonia | Infrastructure、业务规则、平台 API 散落、直接读日志文件 |
| `JiuMi.Desktop` | UI、Application、Infrastructure、Avalonia | 业务规则；平台 API 仅在组合根 |

- 组合根只在 Desktop/ADR 指定位置；Domain/Application/Infrastructure 不 `BuildServiceProvider`。
- UI code-behind 只做视图生命周期、事件转发与 UI 编组；业务逻辑在 ViewModel/Application。
- 日志按 [ADR-0004](../adr/0004-logging.md)：Serilog、结构化模板、英文模板；不记录设置值/凭据/文件内容；`%USERPROFILE%` 脱敏；不用 `Debug.WriteLine`；不外发。
- 安全：外部输入不可信；防路径穿越；不写密钥；不关 TLS 校验；进程参数用列表；安全随机用 `RandomNumberGenerator`；不抑制安全规则。

## 6. 测试

- 测行为不测实现；Arrange–Act–Assert；测试独立、可并行。
- 时间用 `TimeProvider`，文件用临时目录，外部服务用 fake；不得依赖真实时钟/网络/用户目录。
- 不用 `Thread.Sleep`；异步测试返回 `Task`；断言具体异常/错误码。
- 覆盖正常、错误、取消、边界、空/缺省；保留架构文档要求的契约测试（本地化键、占位符、`Error.*` 映射）。
- 不设覆盖率硬指标；回归缺陷必须有测试。

## 7. Code Review 清单

### Blocker

- [ ] 可编译、无新增 warning；没有全局 `NoWarn` / 宽范围 `#pragma`。
- [ ] 可空性正确；没有用 `!` 掩盖未校验输入。
- [ ] 没有 `.Result` / `.Wait()` / 非事件 `async void`；取消正常传播。
- [ ] 异常未被吞；资源/订阅有释放；UI 不被阻塞。
- [ ] 分层边界未破；`#if WINDOWS` 位置正确；无平台 API 散落。
- [ ] 无敏感数据进日志/详情/UI；新错误码有 `Error.*` 映射。
- [ ] 外部输入校验；路径防穿越；无 unsafe deserialization / 命令注入。
- [ ] 变更行为有测试；缺陷修复有复现测试。

### Major

- [ ] 命名/文件组织符合第 3 节；最小可见性；非继承点 `sealed`；无公共字段。
- [ ] 异步命名/返回/取消正确；`ConfigureAwait` 符合层规则。
- [ ] 集合 API 只读抽象；无 `null` 集合/多次枚举。
- [ ] 构造函数注入；无服务定位器/直接 `new` 依赖。
- [ ] 日志模板化；无每层 `log + rethrow`。
- [ ] 公共/跨项目 API 有 XML 文档。
- [ ] 方法职责单一；重复逻辑已提取；UI code-behind 无业务逻辑。

### Nit

- [ ] 格式/using 排序交给 formatter；无注释掉的代码/过时 TODO；测试命名一致。

## 8. 例外与维护

- 现有代码可能与规范不一致；本次改动涉及的代码按规范修正，不要求顺带重写无关代码。
- 偏离“应当/不应”需在 PR 或注释说明原因、范围、替代方案。
- 抑制必须窄：单行 `#pragma` + 规则 ID + 理由；不得项目级关闭整类规则。
- 规则不适合本项目时，改规范与 `.editorconfig`，不在代码中长期绕过。
- 随 .NET 主版本复核；修改规则时同步更新本文件与配置。

## 9. 来源

- **语言/风格**：[.NET Coding Conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions)、[Framework Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/)、[Names of Type Members](https://learn.microsoft.com/dotnet/standard/design-guidelines/names-of-type-members)、[.NET Runtime C# Coding Style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md)
- **分析器/配置**：[Code-style rules](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/)、[Code analysis](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview)、[官方 .editorconfig 示例](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/code-style-rule-options)
- **正确性**：[Nullable](https://learn.microsoft.com/dotnet/csharp/fundamentals/null-safety/nullable-reference-types)、[TAP](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap)、[Exceptions](https://learn.microsoft.com/dotnet/standard/exceptions/best-practices-for-exceptions)、[Dispose](https://learn.microsoft.com/dotnet/standard/garbage-collection/implementing-dispose)、[DI](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection/guidelines)
- **框架**：[Avalonia threading](https://docs.avaloniaui.net/docs/app-development/threading)、[Avalonia compiled bindings](https://docs.avaloniaui.net/docs/data-binding/compiled-bindings)
- **项目**：[architecture.md](../architecture.md)、[ADR-0001](../adr/0001-settings-persistence.md)、[ADR-0003](../adr/0003-error-presentation.md)、[ADR-0004](../adr/0004-logging.md)、[GLOSSARY.md](../../GLOSSARY.md)
