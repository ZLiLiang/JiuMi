# JiuMi 交付体积压缩研究（win-x64）

> 研究日期 2026-10-29，版本 1.0.0，SDK 10.0.402，分支 `feat/60-package-slimming`。只做研究与实测，未改动业务代码、构建脚本，也未提交 git。
> 口径：下文 MB 一律指 MiB（1 MiB = 1,048,576 B），与 issue #60 的 135.6 MB / 59.7 MB 一致；“软件占用”指 publish 目录逻辑字节和，不含 NTFS 簇开销。

## 1. 结论摘要

| 方案 | 软件目录 | zip（7z Deflate，除非注明） | 代价与风险 | 建议 |
| --- | ---: | ---: | --- | --- |
| 现状 | 135.57 | 59.67（Compress-Archive） | — | 基线 |
| zip 改 `SmallestSize` | 135.57 | 57.57 | 无，仅改 `publish.ps1` | 先做 |
| zip 改 7z `-mx=9 -mm=Deflate` | 135.57 | 55.48 | CI 预装 7-Zip；本机需 7-Zip | 先做 |
| 原生资产清理 | 127.30 | 52.22 | 删 13 个非 win-x64/调试文件，已冒烟启动 | 建议 |
| 运行时特性开关 | 135.57（裁剪后 71.10） | 不变 | 收益 0，行为有损 | 不建议 |
| Magick.NET 换 Q8 | 135.54 | ≈55.4（推断） | 仅省 31 KB，损失 16 位精度 | 不建议 |
| single-file（未裁剪） | 128.94（另解压 83.2 到 %TEMP%） | 51.91 | 首次解压、双份磁盘、杀软扫描 | 不建议 |
| single-file + 压缩 | 59.87（另解压 83.2 到 %TEMP%） | 53.84 | 启动解压开销，运行时总占用反而更大 | 不建议 |
| ReadyToRun | 158.49 | 未测（预计不降） | 体积 +22.9 | 不建议 |
| **裁剪 partial + 原生清理** | **71.10** | **28.70** | 需改 8 处反射/JSON 或窄抑制，回归面大 | **主方案** |
| 裁剪 + single-file | 34.79 | 28.98 | 裁剪改造 + 解压问题，zip 无额外收益 | 不建议 |
| 裁剪 + solid 7z | 71.10 | 20.51 | 资源管理器不能解压 | 可选副产物 |
| Native AOT | 未验证 | 未验证 | 改造量最大，Magick.NET 无 AOT 承诺 | 本轮不做 |
| UPX | 不变 | 不变 | 杀软误报、破坏签名/单文件 | 禁止 |

**一句话**：只靠清理和换压缩参数到不了 100 MB；唯一能同时把软件压到 100 MB 以下、把 zip 砍半的是 `PublishTrimmed`（partial），最低风险的前置项是原生资产清理与 zip 参数。

## 2. 现状基线与口径（本机实测 2026-10-29）

| 产物 | 文件数 | 字节 | MiB |
| --- | ---: | ---: | ---: |
| `artifacts/publish/win-x64` | 243 | 142,158,815 | 135.57 |
| `artifacts/JiuMi-1.0.0-win-x64.zip`（Compress-Archive Optimal） | 243 | 62,565,347 | 59.67 |

最大文件（MiB）：`Magick.Native-Q16-x64.dll` 23.64、`System.Private.CoreLib.dll` 15.29、`libSkiaSharp.dll` 11.09、`System.Private.Xml.dll` 7.43、`av_libglesv2.dll` 5.14、`coreclr.dll` 4.40、`FluentAvalonia.dll` 3.50、`System.Linq.Expressions.dll` 3.47。
对照项：`publish.ps1` 已排除全部 `*.pdb`（基线含 pdb 时 249 文件 / 235.8 MiB，其中 Skia/HarfBuzz 符号 100.3 MiB），本研究的全部实验均继承该排除。

## 3. 情况一：软件（整目录磁盘占用）

### 3.1 原生资产清理（本机实测）

用 MSBuild target 在 `ComputeFilesToPublish` 后移除 13 个文件，合计 8,679,184 B / 8.28 MiB：

| 组 | 文件 | MiB | 说明 |
| --- | --- | ---: | --- |
| Linux 后端 | `Avalonia.FreeDesktop`、`.AtSpi`、`Avalonia.X11`、`Tmds.DBus.Protocol` | 1.39 | win-x64 不可达 |
| macOS 后端 | `Avalonia.Native`、`Avalonia.Metal` | 0.35 | win-x64 不可达 |
| 调试器 | `Microsoft.DiaSymReader.Native.amd64`、`mscordaccore`×2、`mscordbi`、`createdump` | 6.10 | 仅崩溃转储/调试 |
| 运行时消息资源 | `clretwrc`、`mscorrc` | 0.43 | 仅原生错误文本 |

结果：**127.30 MiB / 230 文件**（−8.28 MiB，−6.1%）；zip 52.22 MiB（同 7z Deflate 基线 55.48，−3.26 MiB）。
冒烟：清理版 exe 启动后存活 10 s，正常。风险：依赖 Avalonia 平台探测不加载缺失程序集；每次升级 Avalonia 后需重跑冒烟。结论：**推翻** `build-and-verification.md` 的“不值得动”——数字上值得，但单独做仍高于 100 MB，只能作为裁剪的乘数项。

### 3.2 运行时特性开关（本机实测）

| 组合 | 软件目录 | 相对基线 |
| --- | ---: | ---: |
| `InvariantGlobalization=true`（未裁剪） | 135.57 | +107 B（仅 runtimeconfig） |
| `DebuggerSupport/EventSourceSupport/HttpActivityPropagationSupport/NullabilityInfoContextSupport=false` + `UseSystemResourceKeys=true`（未裁剪） | 135.57 | +309 B（仅 runtimeconfig） |
| 同一组开关 + 裁剪 partial | 71.10 | +253 B（仅 runtimeconfig） |
| `InvariantGlobalization=true` + 裁剪 partial | 77.55 | −0.09 MiB |

原因：Windows 自包含包不携带 ICU 数据（用系统 ICU/NLS），发布目录没有任何 `*.resources.dll`，框架程序集也不因这些开关重新编译；裁剪时 SDK 已默认关闭 MetadataUpdater 等。收益全部为 0，zip 同样不变。
行为风险：`UseSystemResourceKeys` 会把 `System.*` 异常消息换成资源键名，日志与用户可见错误质量下降；`InvariantGlobalization` 失去区域性日期/货币格式与排序，世界时钟/本地化是核心功能，风险不可接受。**以上全部不采用**。

### 3.3 裁剪（PublishTrimmed / TrimMode=partial）（本机实测）

语义与前提（Microsoft Learn）：裁剪只支持自包含发布，靠静态分析删除未引用代码，反射是主要失败源；`partial` 只裁标记为 trimmable 的程序集。实测印证：裁剪后 `JiuMi.dll`、`JiuMi.Infrastructure.dll`、`JiuMi.Application.dll` 与基线**逐字节相同**，被裁的是框架（`System.Private.CoreLib` 15.29→3.08 MiB、`Avalonia.Base` 2.25→1.69 MiB、`System.Private.Xml` 整包移除）。

| 变体 | 文件数 | 软件目录 | zip（7z Deflate） |
| --- | ---: | ---: | ---: |
| partial，`SuppressTrimAnalysisWarnings` + `JsonSerializerIsReflectionEnabledByDefault=true` | 120 | 77.64 | 31.36 |
| 上述 + 原生资产清理 | 112 | 71.10 | 28.70 |
| 上述 + single-file + 压缩 | 11（含 2 个运行期生成文件） | 34.79 | 28.98 |

启动复核：`trim-partial`（partial + JSON 反射开关）连续运行 12 s 未退出；**不再复现**“启动即崩”。但既有记录（`build-and-verification.md`）已证明：不加 `JsonSerializerIsReflectionEnabledByDefault` 时，本地化加载抛 `InvalidOperationException: Reflection-based serialization has been disabled`，因此该开关是当前能启动的必要条件，而不是可选优化。

需要改造的代码面（行号为本分支现状）：

| 位置 | 问题 | 改法 | 量 |
| --- | --- | --- | --- |
| `src/JiuMi.Infrastructure/Localization/LocalizationFileSource.cs:143` | 反射反序列化 `Dictionary<string,string>` | 源生成 `JsonSerializerContext` | 小 |
| `src/JiuMi.Infrastructure/Settings/JsonSettingsStore.cs:72` | 反射序列化设置文档 | 同上 | 小 |
| `src/JiuMi.Infrastructure/WorldClock/HolidayCache.cs:126` | 反射序列化节假日缓存 | 同上 | 小 |
| `src/JiuMi.Infrastructure/WorldClock/WorldClockCatalogCache.cs:227,296` | 反射序列化目录清单 | 同上 | 小 |
| `src/JiuMi/Localization/LocalizeExtension.cs:40` | `new Binding` 反射绑定 | 改用编译绑定，或对该方法窄抑制（partial 不裁应用程序集） | 小 |
| `src/JiuMi/Styles/ShellStyles.axaml:55` | `{ReflectionBinding Label}` | `x:DataType` + `{CompiledBinding}` | 小 |
| `src/JiuMi/ViewLocator.cs:32,38` | `Assembly.GetType` + `Activator.CreateInstance` | 显式 View 映射表或 `DynamicallyAccessedMembers` 标注 | 小 |
| 第三方告警 | `Avalonia.Controls.DataGrid`、`Avalonia.DesignerSupport` 自带裁警告 | 保留程序集/针对性抑制并回归 | 小 |

工作量评估：**中**（代码改造 1 天量级，回归 1–2 天）。两条路线：

- **3b 正规路线（推荐）**：JSON 全部源生成，绑定改编译绑定，ViewLocator 改显式映射，删掉 `JsonSerializerIsReflectionEnabledByDefault`。告警清零，符合仓库“告警即错误”的门槛。
- **3a 应急路线**：保留 partial + JSON 反射开关，对 8 处 IL2026/IL2072 逐条窄抑制并写明理由。1 天内可出包，但把真实的裁剪风险藏起来，后续 SDK/Avalonia 升级可能静默失效；只应作为临时方案。

未验证：`TrimMode=full`（会裁应用程序集，反射绑定/ViewLocator 必坏，未测）；裁剪后除启动外的功能回归未做。

### 3.4 Magick.NET 变体（本机实测 + 官方文档）

- Q8 实测：临时仓库副本改 `Magick.NET-Q8-AnyCPU 15.0.0`，编译/发布成功并冒烟启动；`Magick.Native-Q8-x64.dll` 24,753,328 B，Q16 为 24,785,072 B，**只小 31,744 B（0.03 MiB）**；整目录 135.54 MiB。
- 官方文档：Q8 是每通道 8 位，Q16 为 16 位；Q8 “requires twice as much resources” 指的是运行内存，不是文件体积；需要高精度读写时应选 Q16。Q8 是官方推荐默认值，但本项目无体积收益，换它只会丢精度。
- AnyCPU vs RID 包：`Magick.NET-Q16-AnyCPU` nupkg 88,423,276 B，`Magick.NET-Q16-x64` 43,882,218 B（NuGet 实测 Content-Length）。RID 包只减少还原下载/缓存，**发布目录不变**（win-x64 只复制同一份原生库）。
- 裁剪/AOT：包内 `Magick.NET-Q16-AnyCPU.dll` 不含 `IsTrimmable`/`IsAotCompatible` 元数据；裁剪实测可通过 ILLink（P/Invoke 静态可见），但上游没有 AOT 承诺。结论：**不换 Q8、不换 RID 包**（对交付体积无意义）。

### 3.5 single-file（本机实测）

`PublishSingleFile=true` + `IncludeNativeLibrariesForSelfExtract=true`：8 文件 / 128.94 MiB（exe 128.43 MiB），只比目录少 6.6 MiB。加 `EnableCompressionInSingleFile=true`：8 文件 / 59.87 MiB。
坑（实测 + 官方文档）：压缩版启动时把原生库解压到 `%TEMP%\.net\JiuMi.Desktop\<随机名>`，本次解压 **83.2 MiB**；连续启动两次留下两个目录。即“软件占用”实际是 59.87 + 83.2 ≈ 143 MiB，还多一次首次启动解压和杀软扫描；`Assembly.Location` 等 API 语义也改变。zip 侧：未压缩 single-file 51.91 MiB（略优于目录 55.48），压缩版 53.84 MiB（更差）。结论：**不用于软件交付**；仅“zip 只发一个 exe”场景可考虑未压缩 single-file（仍需接受解压）。

### 3.6 ReadyToRun（本机实测）

`PublishReadyToRun=true`：243 文件 / 158.49 MiB（+22.92 MiB，+16.9%）。官方文档明确 R2R 程序集体积为 2–3 倍，只换启动速度。与目标相反，**不采用**。

### 3.7 Native AOT（未实测，官方文档/推断）

Avalonia 12 官方支持 Native AOT，但要求 `x:CompileBindings`、避免动态 XAML、注册视图模型、trimmer root；本项目 `LocalizeExtension`、`ShellStyles.axaml`、`ViewLocator` 正好是反射路径。Magick.NET 无 AOT 兼容标记，P/Invoke 原生库与 ILC 的组合未验证。AOT 还隐含 single-file 的全部限制。改造量**大**，且裁剪已能把目录压到 71 MiB，**本轮不做**。

## 4. 情况二：zip（下载体积）

全部以同一份基线 publish 目录为输入（本机实测 2026-10-29；7z 25.01）：

| 方式 | 体积 MiB | 相对现状 | 资源管理器/.NET 解压 | CI 可得性 |
| --- | ---: | ---: | --- | --- |
| Compress-Archive Optimal（现状） | 59.67 | — | 是 | 内置 |
| .NET `ZipFile` Optimal | 59.67 | 0.00 | 是 | 内置 |
| .NET `ZipFile` SmallestSize | 57.57 | −2.10 | 是 | 内置 |
| 7z `-tzip -mx=9 -mm=Deflate` | 55.48 | −4.19 | 是（.NET 实测解压 OK） | runner 预装 7-Zip 26.04 |
| 7z `-tzip -mm=LZMA` | 45.27 | −14.40 | **否**：.NET 抛“不支持的压缩方法” | 预装 |
| 7z 格式（solid） | 39.82 | −19.85 | 需 7-Zip/WinRAR | 预装 |
| 原生清理 + Deflate | 52.22 | −7.45 | 是 | 同上 |
| 原生清理 + SmallestSize | 54.19 | −5.48 | 是 | 内置 |
| 裁剪 + 原生清理 + Deflate | 28.70 | −30.97 | 是 | 同上 |
| 裁剪 + 原生清理 + solid 7z | 20.51 | −39.16 | 仅 7-Zip | 同上 |

要点：

- `Compress-Archive` 在 pwsh 7 下就是 .NET 10 的 Optimal，换 API 不换级别没有收益；`SmallestSize` 才省 2.1 MiB。
- 7z 的 Deflate 实现比 .NET 小 2.1 MiB，且 zip 根目录可直接放 `JiuMi.Desktop.exe`（实测产物根层即 exe/Assets，无外层目录）。命令需在 publish 目录内执行：`7z a -tzip -mx=9 -mm=Deflate <zip> *`。
- zip-LZMA 使用 ZIP method 14，超出 Windows 资源管理器与 .NET `ZipArchive` 的支持范围；实测 .NET 解压直接失败，不能作为默认下载包。
- solid 7z 体积最好，但 Windows 原生不能打开，且整包解压、无法按需取文件；只适合作为“高级用户可选”的第二个产物。
- CI：GitHub `windows-2025` 镜像预装 7-Zip 26.04，`publish.ps1` 可直接调用 `C:\Program Files\7-Zip\7z.exe`；本地开发机未装时回退到 `SmallestSize` 即可，不需要新依赖。
- UPX：ESET 明确把 UPX 打包的 exe/dll 作为病毒检测对象，Defender 也存在打包器误报处理流程；压缩后的 exe 还会破坏 .NET bundle、代码签名与崩溃转储。zip 本身已压缩，UPX 对下载体积几乎无收益，**明确不建议**。

## 5. 同时改善两者的交叉项

- **裁剪**：软件 −64.47 MiB（−47.6%），zip −30.97 MiB（−51.9%），是唯一量级性方案。
- **原生资产清理**：软件 −8.28 MiB，zip −3.26 MiB（Deflate 同法），与裁剪叠加后仍有约 6.5 MiB 目录收益，且不触碰业务代码。
- zip 压缩参数与软件占用无关；single-file 只是把目录换成 %TEMP% 解压，不减少总占用；Q8/特性开关/R2R 均不改善任一指标。

## 6. 不建议的路线与理由

- **UPX / 自解压 exe / MSIX**：UPX 误报且破坏 bundle；自解压与 MSIX 偏离“解压即双击 exe”的交付形态，MSIX 还需签名与安装流程，issue #60 也已排除。
- **换 UI 框架 / 换渲染后端**：收益不确定，重写成本远超体积目标。
- **Native AOT**：改造量最大，Magick.NET 无 AOT 兼容承诺，且裁剪已达标。
- **Q8、特性开关、ReadyToRun**：实测 0 收益或有损。
- **手工删 `System.Data.Common`/`Microsoft.VisualBasic*` 等运行时程序集**（文档估 5.8 MiB）：脆弱、不到 100 MB，裁剪会以受控方式移除。
- **`TrimMode=full` 或全局 `NoWarn`**：绕过仓库“告警即错误”的门槛，风险不可控。

## 7. 建议的落地顺序

| 步骤 | 改动 | 软件 | zip | 风险/门槛 |
| --- | --- | ---: | ---: | --- |
| 1 | `publish.ps1`：压缩改 `SmallestSize`（无 7z 时）或 7z Deflate | 135.57 | 57.57 / 55.48 | 无；`verify.ps1` 不受影响 |
| 2 | `JiuMi.Desktop.csproj`：新增 win-x64 原生清理 target（放在现有 `RemovePdbFromPublish` 旁） | 127.30 | 52.22 / 54.19 | 低；升级 Avalonia 后冒烟 |
| 3 | 按 3.3 改造 JSON/绑定/ViewLocator，再在 csproj 开 `PublishTrimmed` + `TrimMode=partial` | 71.10 | 28.70 | 中；裁剪分析器让 `verify.ps1` 的构建更严（告警即错误），但脚本本身不改 |
| 4 | 可选：额外产出 solid 7z 供高级用户 | 71.10 | 20.51 | 低；不作为默认下载 |

最终可达：**软件 71.10 MiB（74.6 MB），zip 28.70 MiB（30.1 MB）**，相对现状分别 −47.6% / −51.9%，满足 ≤100 MB 且余量约 29 MiB。
顺序原则：1、2 只动 `publish.ps1` 和 `JiuMi.Desktop.csproj`，可在一个小 PR 内先落地；3 必须单独成票，带完整回归清单（启动、世界时钟、图片转换、设置读写、本地化切换、DataGrid 页面）。若必须当天达标，可用 3a 应急路线，但应同时开票补 3b。

## 8. 来源清单（查阅日期 2026-10-29）

1. Trim options（feature switches 语义与禁用项）— https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options
2. Trim self-contained applications（仅自包含、反射风险）— https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained
3. Single-file deployment（原生库分离、`IncludeNativeLibrariesForSelfExtract`、`%TEMP%/.net`、压缩启动成本）— https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
4. ReadyToRun（程序集 2–3 倍）— https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run
5. Native AOT（限制：无动态加载/运行时代码生成、隐含 single-file）— https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/
6. Globalization config（Invariant 模式失去区域性数据）— https://learn.microsoft.com/en-us/dotnet/core/runtime-config/globalization
7. `CompressionLevel`（Optimal/Fastest/SmallestSize 定义）— https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.compressionlevel
8. runtime feature switches 清单 — https://github.com/dotnet/runtime/blob/main/docs/workflow/trimming/feature-switches.md
9. Avalonia Native AOT（支持状态、`x:CompileBindings`、trimmer root、已知限制）— https://docs.avaloniaui.net/docs/deployment/native-aot ；站点无独立 trimming 指南（sitemap 核对）
10. Magick.NET README（AnyCPU vs 平台专用包）— https://github.com/dlemstra/Magick.NET
11. Magick.NET Q8/Q16 说明（精度与内存翻倍）— https://github.com/dlemstra/Magick.NET/blob/main/docs/Readme.md
12. NuGet 包体积（Q16-AnyCPU 88,423,276 B / Q16-x64 43,882,218 B）— https://www.nuget.org/packages/Magick.NET-Q16-AnyCPU 、https://www.nuget.org/packages/Magick.NET-Q16-x64
13. GitHub Actions Windows 2025 镜像预装 7-Zip 26.04 — https://github.com/actions/runner-images/blob/main/images/windows/Windows2025-Readme.md
14. ESET KB117（UPX 打包文件被检测）— https://support.eset.com/cn/kb117-eset-windows
15. Defender 误报处理流程 — https://learn.microsoft.com/en-us/defender-endpoint/defender-endpoint-false-positives-negatives
16. ZIP APPNOTE（method 14 = LZMA）— https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT
