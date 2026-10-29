# JiuMi

JiuMi 是一个 Windows 桌面小工具集：把日常零散的小需求收进一个窗口，不用开浏览器，也不用装一堆扩展。

## 主页

![主页](docs/images/readme/home.png)

所有工具都从主页进入：左侧是导航，右侧是工具卡；卡片上标「即将推出」的工具还没有上线。微信视频下载、批量重命名还在计划中。

## 世界工作时间

![世界工作时间](docs/images/readme/world-clock.png)

看哪些国家或城市正在上班：按各地当地时间列出当前状态，一眼看清现在能联系谁、哪些时区的工作时间有重叠。

## 图片格式转换

![图片格式转换](docs/images/readme/image-convert.png)

把 PSD、PNG、JPG、WebP、BMP 批量转成 PNG、JPEG 或 WebP：可以设最大边、质量和输出目录，PSD 直接取内嵌合成图，不用开 Photoshop。

## 开发

- 运行：`dotnet run --project src/JiuMi.Desktop`
- 结构：`src/` 为产品项目，`test/` 为测试项目
- 门禁：`pwsh ./verify.ps1`（Release 构建 + 测试 + 格式检查）
- 发布：`pwsh ./publish.ps1`（自包含 win-x64，产物在 `artifacts/JiuMi-<版本>-win-x64.zip`）
- 版本：语义版本来自 `Directory.Build.props` 的 `<Version>`；informational version 带 `+<commit>`，「关于」只显示语义版本
- CI：GitHub Actions 在每次推送与 pull request 上跑门禁并归档 Windows 产物
