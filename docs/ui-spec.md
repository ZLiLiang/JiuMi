# UI 规格

JiuMi 应用外壳的视觉与交互规格。跨模块的结构规则见 [`docs/architecture.md`](./architecture.md)，单点决策的论证见 [`docs/adr/`](./adr/)。

> 本文件逐步成稿：目前只有「错误呈现」一节，壳层视觉（主页、导航、hero、主题与 splash）随后补入。

## 错误呈现

分级、载体选择与场景映射是架构规则，见 [`docs/architecture.md`](./architecture.md) 的「错误呈现与诊断」章节。本节只定位置、尺寸、间距、排版与交互。

- 颜色一律引用 FluentAvalonia 的主题资源，不写死色值：浅色、深色与高对比度随主题自动成立。
- 尺寸是 100% 文本缩放下的默认值；缩放到 200% 时以「不裁切、可滚动」为准。

### 全局 InfoBar

#### 位置

- 挂在主窗口内容区顶部、导航栏右侧，横跨内容区整宽。
- 推挤内容而不是覆盖：占据内容 `Grid` 的第一行，`Margin="16,12,16,0"`。
- 导航栏展开或收起时宽度跟随内容区，不移动到导航栏下方。
- 跨页常驻：切换页面不清空、不重建。

#### 尺寸与排版

| 项 | 值 |
| --- | --- |
| 外边距 | 左 / 右 16，上 12，下 0 |
| 内边距 | 16, 12 |
| 圆角 | 4 |
| 最小高度 | 48 |
| 图标 | 16 × 16，与文字间距 12，与首行文字垂直居中对齐 |
| 文案 | 14px / Regular / `TextFillColorPrimaryBrush`，行高 20，可换行 |
| 关闭按钮 | 32 × 32，图标 12，悬停底 `SubtleFillColorSecondaryBrush` |

- **只有一个文案位**，取 `Error.<code>`；不在 InfoBar 里拼标题与正文两段。
- 同一 `code` 合并后，文案尾部追加 `Common.ErrorCountSuffix`（`（已发生 {0} 次）`），计数变化只更新这一段文本。

#### 配色

| 级别 | 图标 / 强调 | 背景 | 边框 |
| --- | --- | --- | --- |
| Error | `SystemFillColorCriticalBrush` | `SystemFillColorCriticalBackgroundBrush` | `ControlStrokeColorDefaultBrush` |
| Warning | `SystemFillColorCautionBrush` | `SystemFillColorCautionBackgroundBrush` | 同上 |

- Error 用圆形叉图标，Warning 用三角形感叹图标：级别不靠颜色区分。
- 常驻类（`Config.MemoryOnly`）配色同 Error，但不渲染关闭按钮。

#### 生命周期

- 同时只有一条可见；其余按（级别、时间）排队，Error 在 Warning 之前；当前条关闭或自动收起后显示下一条。
- Error：手动关闭。
- Warning：8 秒自动收起；指针悬停或键盘焦点落在其内时暂停计时，移出后重新计满 8 秒。
- 常驻类：不可关闭、不参与自动收起，直到条件解除。
- 内容切换不做过渡动画。

#### 多动作布局

- InfoBar 只有一个 `ActionButton`，放主操作（重试 / 打开文件夹）：`Button` 默认样式，高 32，右侧对齐，与文字间距 12。
- 「复制详情」不做成按钮：正文末尾一个 `HyperlinkButton`（12px）。复制成功后自身文本在 2 秒内显示 `Common.Copied`，随后复原。
- 内容区宽度小于 640 时，动作按钮换到下一行、左对齐，正文先占满整行。

#### 启动期入队

主窗口出现前产生的非阻断记录只入队；主窗口首次激活后按排队顺序开始显示（见 [`docs/architecture.md`](./architecture.md) 启动序列章节）。

### 容器内占位

局部资源加载失败时就地降级，不弹提示。

| 项 | 值 |
| --- | --- |
| 填充 | 透明，不铺底色 |
| 边框 | 1px 虚线 `ControlStrokeColorDefaultBrush` |
| 圆角 | 4，与所在卡片一致 |
| 图标 | 24 × 24 居中，`TextFillColorSecondaryBrush` |
| 文案 | 可选，12px / Regular / `TextFillColorSecondaryBrush`，与图标间距 8 |

- 占位不改变容器尺寸，不引起布局跳动。
- splash 的降级形态由启动序列决定（四档阶梯），不走本占位；本占位只用于主窗口内的图片容器。

### 错误窗口

#### 窗口属性

| 项 | 值 |
| --- | --- |
| 尺寸 | 420 × 240，固定 |
| 可缩放 | 否 |
| 位置 | 屏幕居中 |
| 任务栏 | 显示（它可能是进程唯一的窗口） |
| 系统装饰 | 默认标题栏，可拖动，带关闭 X |
| 标题 | 应用名，内嵌常量 |
| 主题 | Fluent 默认主题，不读用户主题设置 |
| 实例 | 每进程最多一个；已在显示时再发生致命错误，写日志后直接退出，不叠加 |

#### 布局与排版

- 内边距 24。
- 图标 32 × 32（Error 圆形叉），与标题同一行，间距 12。
- 标题：20px / SemiBold / `TextFillColorPrimaryBrush`，行高 28，最多两行，超出省略。
- 正文：14px / Regular / `TextFillColorSecondaryBrush`，行高 20，最多四行，超出省略；左边缘对齐标题左边缘（缩进 44）。
- 字体使用 Fluent 默认主题字体（`ContentControlThemeFontFamily`）。
- 垂直结构：内容区 `*` + 按钮组 `Auto`，按钮组贴底；图标、标题、正文、详情依次排列，标题与正文间距 8，正文与详情间距 12，详情与按钮组间距 16。

#### 文案

- 内嵌 zh-CN 常量，不走 i18n：错误窗口可能出现在本地化初始化完成之前，也可能出现在语言文件损坏时。窗口文案不随语言切换变化。

#### 按钮组

| 变体 | 按钮（从左到右） | 默认 / 回车 | 关闭 X | Esc |
| --- | --- | --- | --- | --- |
| 阻断：启动期失败、主窗口不可用 | 复制详情（次要）、退出（主） | 退出 | 退出 | 退出 |
| 可继续：主窗口可用时的 UI 线程未捕获异常 | 复制详情（次要）、退出（次要）、继续运行（主） | 继续运行 | 继续运行 | 继续运行 |

- 按钮右对齐，高 32，间距 8；主按钮使用 `AccentButtonStyle`。

#### 详情

- 按钮组上方一个 `Expander`，标题「查看详情」；折叠时占 32 高，窗口尺寸不变。
- 展开后占用正文与按钮组之间的剩余高度（约 96），内容为只读 `TextBox`：`IsReadOnly=True`、`AcceptsReturn=True`、不换行、纵向滚动、可选中复制；字体 `Cascadia Mono, Consolas, monospace`，12px。
- 内容是错误记录的复制格式（字段一行一个），不加额外说明文字。
- 展开与折叠都不改变窗口尺寸。

#### 复制

- 「复制详情」写入剪贴板的内容与 InfoBar 的 `HyperlinkButton` 完全一致（见 [`docs/architecture.md`](./architecture.md) 错误记录章节的复制格式）。
- 复制成功不弹提示：按钮文本在 2 秒内显示「已复制」，随后复原（内嵌常量，不走 i18n）。

### 可访问性

- InfoBar 根元素设 `AutomationProperties.LiveSetting="Polite"`；消息文本的 `AutomationProperties.Name` 为完整文案。
- 级别不靠颜色：Error 与 Warning 使用不同图标形状。
- Warning 的自动收起在指针悬停或焦点进入时暂停；关闭按钮可获得焦点、可用键盘触发。
- 错误窗口打开即聚焦默认按钮；`Tab` 在按钮与详情之间循环；Esc 与关闭 X 的语义一致。
- 详情框获得焦点时不吞 Esc：Esc 仍执行窗口的默认动作。
- 文本缩放：InfoBar 高度自适应、动作按钮按宽度换行；错误窗口尺寸不变，正文与详情区滚动而不是撑破窗口。
- 高对比度：全部颜色取自主题资源，不做额外覆盖。
