# ADR-0009 图片格式转换：PSD 解码选型与批量转换管线

- 状态：已接受
- 日期：2026-10-28
- 来源：README 路线图「ps格式图标转换」；实现票与体积票见文末「实施」

## 背景

用户需要把设计交付的 PSD 批量转成 PNG，也需要 PNG/JPG/WebP 之间互转。选题时有三个硬约束：

1. **不自己手搓解码与合成算法**。PSD 是分层容器格式，通道压缩、色彩模式与合成规则都不是本项目的领域。
2. **安装包体积**。发布产物是自包含 win-x64 `zip`，当时的实测基线是 zip 71.7 MB、磁盘 209.1 MB（其中 100.2 MB 是 `publish.ps1` 未排除的 `.pdb`）。
3. **Windows 没有可用的系统解码路径**。

### 事实核查

- **WIC 没有 PSD 编解码器**。Microsoft 的编解码器清单不含 PSD；在**已安装 Photoshop** 的机器上实测 `BitmapDecoder.Create` 打开 `.psd` 仍抛 `NotSupportedException`；`.psd` / `.psb` 也没有注册 thumbnail handler。系统缩略图与 WIC 都不可用。
- **[photocraft](https://github.com/storytold/photocraft) 不能以库的方式复用**。它是纯 Rust 的 Photoshop 复刻编辑器（v0.16.0，自述 early alpha），许可证 MIT OR Apache-2.0，PSD/PSB 读写完整，但官方 Windows 便携包中 `photocraft.exe` 为 114.75 MB、`photocraft-cli.exe` 为 80.58 MB；仓库没有 cdylib / C ABI，插件体系是 WASM 沙箱。唯一可行的调用方式是**子进程**（CLI / MCP / JSON-lines）。
- **候选解码库实测**（11 个真实 PSD/PSB 样本，含 PSB、16/32-bit、CMYK）：

| 方案 | 新增磁盘（win-x64） | 成功解析 | 结论 |
| --- | --- | --- | --- |
| Magick.NET 15.0.0 Q16 | 原生 24.2 MB + 托管 1.1 MB | 11/11 | 采用 |
| PsdSharp 1.2.0 | 0.14 MB | 8/11 | 3 个正常文件抛 `EndOfStreamException`；16/32-bit 样本解析出 0 图层 |
| Aspose.PSD 26.10 | 7 MB | 未实测 | 商业授权，Developer 起价 $599 |

- **SkiaSharp 3.119.4 只能编码 PNG/JPEG/WebP**（BMP/ICO/AVIF/TIFF/GIF 的 `Encode` 返回 `null`）。它已经作为 Avalonia 的渲染依赖存在于包内。

## 决策

1. **保真度定档：只读内嵌合成图**。转换读取 PSD/PSB 中 Photoshop 保存时写入的扁平合成位图；不自建图层合成，不做「挑一层导出」。文件未保存合成数据时明确失败，界面上不承诺「与 Photoshop 逐像素一致」。
2. **解码用 Magick.NET Q16 15.0.0**（`Magick.NET-Q16-AnyCPU`）。Q16 保留 16 bit 精度；16 bit 源输出 16 bit PNG，JPEG/WebP 受格式限制仍为 8 bit。
3. **编码也走 Magick.NET**。转换链路只有一个成像引擎，避免 Skia 与 ImageMagick 在色差、缩放核与压缩参数上给出两套结果。SkiaSharp 继续仅供 Avalonia 渲染使用。
4. **格式矩阵**：输入 `.psd` `.psb` `.png` `.jpg/.jpeg` `.webp` `.bmp`；输出 PNG / JPEG / WebP。ICO、TIFF、AVIF 不进 v1，但因为选用了 Magick.NET，将来加这些输出不需要换库。
5. **色彩与元数据**：所有输出统一转换到 sRGB 并嵌入 sRGB profile；EXIF/GPS/相机信息默认剥离，进阶参数可关掉剥离以保留。
6. **参数分两档**。基础：目标格式、质量 1–100（默认 90，仅 JPEG/WebP 有损生效）、最大边等比缩放、JPG 底色（默认白、可选色、跨会话记住）、输出目录。进阶（折叠区、默认收起、不记忆展开状态）：WebP 有损/无损、PNG 压缩级别、保留源元数据。允许同格式转换（如 JPG→JPG），界面提示会重新压缩。实现期界面调整（#59）：质量与 JPG 底色归入进阶折叠区，基础区只留目标格式、最大边与输出；参数集合、默认值与生效规则不变。
7. **交互形态：批量队列页**。侧边导航工具页 + 页内拖拽；列表显示缩略图（≤ 256 px、异步解码、结果缓存给转换阶段复用）、源尺寸与状态。
8. **输出位置与命名**：默认写回源文件同目录，可切换到用户选定目录并记住上次选择；命名为 `原名.<新后缀>`，重名追加序号（`a-1.png`），**绝不覆盖已有文件或源文件**。
9. **失败语义**：单文件失败不中断整批。失败行标红并给出可读原因，结束时汇总一条 InfoBar；不弹全局错误窗，符合 [ADR-0003](./0003-error-presentation.md)。
10. **分层与接缝**：`JiuMi.Application` 持有纯函数（转换请求模型、输出路径规划、格式表与参数校验）；`JiuMi.Infrastructure` 实现 `IImageProbe` / `IImageConverter`（Magick.NET 实现）；`JiuMi` 只做绑定、拖拽与进度。
11. **photocraft 不进 v1**：不打包、不实现下载器。仅在此记录路径——若将来需要图层级精准渲染，再以**可选模块**（首次使用时下载 `photocraft-cli`，带版本与哈希校验）接入，届时另立 ADR 覆盖下载、校验与卸载。
12. **体积预算**：发布 zip ≤ 100 MB，本功能新增磁盘 ≤ 25 MB。本次实测 Magick Q16 使 zip 从 46.4 MB（去 pdb 后）升至约 57 MB、磁盘从 108.8 MB 升至约 133 MB。

## 为什么

- **保真度定在合成图，是唯一能同时满足「不手搓」与「体积可控」的档位**。图层合成需要混合模式、调整层、蒙版、组不透明度全套实现；能接近的只有 80 MB 级别的编辑器，而做得到的库（PsdSharp）明确声明不做合成。
- **Magick.NET 的 25 MB 换来的是「打得开」**。转换工具的全部价值在于对真实文件的鲁棒性，11/11 与 8/11 的差别直接表现为「每四个 PSD 就有一个报错」。PsdSharp 的 0.14 MB 省不下有意义的空间，却把失败率变成产品缺陷。
- **Q16 而不是 Q8**。两者体积相近（原生库 24.2 MB vs 24.2 MB），Q16 保住 16 bit 源的信息，避免「印刷稿转出来偏色/断层」这类难以解释的投诉。
- **编码复用同一引擎**。双引擎意味着同一次批处理里，PNG 走 Skia、JPEG 走 ImageMagick 时缩放与色彩处理不一致；单一引擎让「同一批参数得到同一批结果」可验证。
- **sRGB + 剥离元数据是「素材转换」的正确默认**。CMYK 印刷稿不转色域会输出诡异颜色；把相机型号、GPS 坐标带进交付物是隐私风险。需要原样保留的人可以在进阶里关掉剥离。
- **批量队列 + 缩略图是风险控制**。批量转换最容易出的错是「转了一批错文件」；缩略图是唯一的低成本防线，而解码结果复用让它的成本接近零。
- **子进程调用 photocraft 在 v1 不成立**。+80.58 MB 的未压缩二进制、进程启动开销、以及「一个编辑器」带来的攻击面与更新负担，都与体积预算和工具定位冲突。

## 备选与取舍

| 备选 | 为什么没选 |
| --- | --- |
| 打包 `photocraft-cli.exe` 作为转换后端 | 未压缩 80.58 MB，直接把 zip 推到 100 MB 上限附近；且需要进程间协议、超时与崩溃隔离 |
| 按需下载 photocraft 模块 | 方向成立，但下载器（版本、哈希、代理、断点、卸载）是独立课题，不阻塞 v1 |
| PsdSharp（纯托管，0.14 MB） | 11 个真实样本中 3 个抛 `EndOfStreamException`；16/32-bit 样本解析出 0 图层 |
| Aspose.PSD | 能力最全，但商业授权（$599 起）与开源分发模型冲突 |
| 走 WIC / 系统缩略图 | Windows 无 PSD 编解码器，装了 Photoshop 也无法解码；shell 缩略图同样缺 handler |
| 自己解析 PSD 并合成图层 | 违反「不手搓算法」；工作量与维护成本远超本项目范围 |
| 用 `psd-tools`（Python）等外部进程 | 需要分发 Python 运行时与 numpy/Pillow，体积与启动开销都更差 |
| v1 就做 ICO / TIFF / AVIF 输出 | 「图标」在本需求里是图片素材的口语说法；ICO 需要多尺寸预设与重采样策略，属独立分支 |
| 只做 PSD→PNG，不做 raster 互转 | 「PNG 转 JPG」是同类高频需求，且复用同一条管线，边际成本极低 |

## 后果

- 发布包新增约 24 MB 磁盘占用（zip 约 +11 MB）。转换功能因此必须与体积预算一起看：pdb 移除与运行时裁剪另票处理。
- 包内出现第二个原生成像引擎（Magick.NET 与 SkiaSharp 并存）。原生库加载失败时，只有转换功能不可用，工具页显式降级，不影响启动与其它工具。
- 需要补充第三方许可声明：Magick.NET（Apache-2.0）与随包分发的 ImageMagick 7（ImageMagick License）。发布流程需要包含这份声明。
- 输出不再携带 EXIF/GPS；需要元数据的用户必须在进阶里显式开启保留。
- PSD 保真度以 Photoshop 的合成图为上限：含大量调整层或未保存合成数据的文件会失败或被拒绝，这是既定取舍。
- 集成测试需要真实 PSD 样本；样本由 Magick.NET 在测试里自行写出，避免引入第三方作品的版权问题。

## 不在本 ADR 内

- 图层合成、选层导出、ICO/TIFF/AVIF 输出：明确不做。
- photocraft 可选模块的下载器、版本与哈希校验、卸载：仅记录方向。
- 资源管理器右键「用九米转换」：需要注册表与 shell 扩展，另议。
- 体积瘦身的具体手段（`.pdb` 排除、`PublishTrimmed`、无用 RID 原生资产）：另票处理。
- 像素级界面规格：见 [`docs/ui-spec.md`](../ui-spec.md) 的工具页章节。

## 实施

- 功能票：[#59](https://github.com/ZLiLiang/JiuMi/issues/59)
- 体积票：[#60](https://github.com/ZLiLiang/JiuMi/issues/60)，阻塞于 #59：先拿到真实的体积基线再动手
