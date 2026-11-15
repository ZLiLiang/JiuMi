# 我的JiuMi 视频生成提示词

记录桌面猫动画所用的 VideoGen 提示词与后处理，方便后续制作跳跃、趴下、起身等动作。提示词按「参考图 + 动作 + 固定机位 + 风格约束」组织；换动作时只替换动作段，其余约束保持不变。

## 走路循环

- 参考图：`docs/images/pet/direction-117/walk.png`（已确认走路单帧）
- 生成：VideoGen，wan30，`reference_to_video`，5 s，1280×720
- 步态参考：owner 提供的 GIF（157 帧，480×480；一个循环 13 帧，约 0.85 s）；已提交在 `docs/images/pet/references/walk-reference.gif`，仅作步态依据。
- 源视频：`docs/images/pet/final-121/walk-source.mp4`（随 PR 提交）
- 后处理：抽帧 → 选一个完整周期（本次 33 帧）→ 去背景/连通阴影 → 固定缩放/锚点导出 400×400 → 按参考 GIF 重定时 26 ms/帧（约 38.5 fps）。
- 本次固定参数：缩放 `240/386`，锚点 `cx=670`、`bottom=541`；换动作时重新测包围盒后重取。

### 提示词（原文）

```text
Animate the provided 2D hand-drawn illustration of the orange tabby cat as a smooth walk cycle.

Keep the exact same character design, face, eye shape, palette, bold dark-brown outline, white chest and white paws, orange tabby stripes and flat warm illustration style. Do not restyle, do not make it 3D, do not add fur strands, gradients or photorealism.

Locked camera, no zoom, no pan, no background change; plain warm off-white background (#F5F2EC). Full body visible, side/3-4 view, all four legs visible.

The cat walks in place to the left on a treadmill: the four legs alternate naturally; front paws alternate clearly (left forward, then right forward), hind legs alternate, tail relaxed. Keep the cat at the same size and vertical position; no forward translation, no position drift, no extra cats.

Smooth, seamless 5-second loop. No text, no watermark, no shadow on the ground, no props, no motion lines, no style change.
```

## 后续动作模板

保留上面的角色/风格/机位/背景约束，只改动作段：

- **跳跃**：`animate a small vertical hop: crouch, push off, a short airborne phase with all four paws off the ground, land back in the same spot; no forward translation; 2 s; non-looping`。
- **趴下**：`animate the cat lowering from a standing/alert pose into a curled lying pose, eyes closing, tail curling around the body; stay in place; 3 s; non-looping`。
- **起身/伸懒腰**：`animate the cat rising from lying into a stretch and then a walk-ready stance; stay in place; 3 s; non-looping`。

每次仍需：固定机位、同一张已确认参考图、纯色背景、无阴影/文字/水印、全身与四腿可见；生成后抽帧、选段、去背景/阴影、固定缩放/锚点导出。

## 动作源视频（2026-11-12）

已生成并归档一段 6.58 s 的连续动作源，作为后续坐姿、蹲伏、腾空、落地等姿态的来源；**不整段进入运行时**。

- 绿幕：`docs/images/pet/action-source/jiumi-cat-6s-actions-green.mp4`
- 中性底预览：`docs/images/pet/action-source/jiumi-cat-6s-actions-neutral.mp4`
- 规格：768×768，24 fps，6.58 s。
- 内容：趴 → 卷成睡球 → 醒 → 坐 → 走 → 蹲 → 小跳 → 落地站立。
- 用法：按关键动作抽 1–3 帧，按本文的导出流程去背/去阴影、固定缩放与锚点，导出 400×400 RGBA；状态帧接进 `MyJiuMiAnimationCatalog`，点击小跳的叠加层用独立的 `jump/` 资产。
- 带“AI生成”水印的原片不进 git；当前页面微动效（呼吸/轻摆/眨眼）不依赖这段视频。

### 抽帧姿势（2026-11-12）

从上面的绿幕视频抽出 4 个姿势，已抠像、统一缩放并贴地对齐到 400×400 RGBA：

| 姿势 | 文件 | 时间点 | 用途 |
| --- | --- | --- | --- |
| 坐姿 | `docs/images/pet/action-source/poses/sit.png` | 3.5 s | 后续休息/起身过渡候选 |
| 腾空 | `docs/images/pet/action-source/poses/airborne.png` | 5.3 s | 点击小跳的腾空帧 |
| 落地 | `docs/images/pet/action-source/poses/landing.png` | 5.6 s | 点击小跳的落地帧 |
| 站姿 | `docs/images/pet/action-source/poses/stand.png` | 6.4 s | 站姿参考；现有 `stretch/03` 已覆盖 |

- 预览：`docs/images/pet/action-source/poses/preview.png`。
- 腾空/落地已复制到运行时 `src/JiuMi/Assets/MyJiuMi/jump/`，由 `MyJiuMiSession` 的小跳叠加层按 14%/70%/84% 相位换帧；不新增状态。
- 处理：绿幕抠像；以站姿高度对齐现有 `stretch/03` 的 291 px；坐/落地/站贴地，腾空由小跳的位移层抬升。
- 已知差异：抽帧线宽比现有帧略细，眼部更圆；200 DIP 下可接受，2× 可见。

## 交互动作抽帧（#161，2026-11-15 起）

按 #161 的 10 条片子计划逐条交付。单条源视频 **≤5 s**（成本上限），运行时**不播放源视频**，只按下方清单重定时。

- 生成：VideoGen `image-to-video`，首帧＝该片段的起始姿势参考，末帧＝结束姿势参考，1:1，24 fps。
- 参考图：趴姿用 `lying-idle.png`，站姿用 `walk.png`，身份检查用 `face-closeup.png`。
- 后处理：按背景色距离抠像（不用 HSV 色带，容易漏边）→ 统一缩放 → 每帧底边 `y=399`、中心 `x=200` 导出 400×400 RGBA。
- 尺寸基准（实测自运行时帧）：趴 176 px、坐 208 px、站 240 px、伸展 291 px、睡球 148 px。运行时把 400×400 图按 `Stretch="Uniform"` 画进 200 DIP 方格，**不做尺寸归一**，所以导出尺寸就是唯一杠杆。
- **尾巴长度会变的帧不能按总高度归一**：总高度含尾巴，尾巴一变长缩放比就变小、整只猫跟着缩水。改用刚性部位——取剪影最低 18% 那一带的横向宽度（四爪张开宽度）作基准。`tail-flick/01` 和 `annoyed` 的尾巴修正都踩过这个坑。
- 站姿参考只能用 `walk.png`（仅限 `settle` 的近景过渡）或 `_refs/stand-tail-neutral.png`；**`poses/stand.png` 不能用作站姿参考**，它的尾巴是细长无白尖的抽帧缺陷。

### settle（P0-1）

- 源：`docs/images/pet/action-source/interaction/p0-1-settle/settle-source.mp4`（768×768，24 fps，124 帧，5.17 s）
- 首帧参考 `walk.png`（站）、末帧参考 `lying-idle.png`（趴）；抠的是 `#F1EBE4` 平底（当次模型没按提示出绿幕，抠像仍干净）
- 四帧：`00` 站 0.08 s · `01` 坐 2.50 s · `02` 折 3.54 s · `03` 趴 4.58 s；统一缩放 0.55，底边 y=399、中心 x=200
- 预览：`docs/images/pet/action-source/interaction/preview-{light,dark}.png`

**为什么采用「有坐姿」的那一版**：坐姿阶段整只猫的轮廓高度从 413 涨到 487 px（+18%），一开始被判断成「猫走近镜头了」。用头宽复核后否掉了这个判断——全程头宽恒定（193 / 192 / 193 / 171 px），说明**角色缩放没变**，+18% 只是「直立坐、前腿撑直」这个姿势本身更高（对照 `stretch/03` 直立站姿 291 px，坐姿 268 px 属同一量级）。两版与相邻帧的咬合也做了并排对比：这一版的首帧更接近 `walk/00`、末帧更接近 `lying-idle/00`，所以采用它，第一版（无坐姿、连续下降）退为备选。

**已知偏离**：① 坐姿相对头宽比 `poses/sit.png` 高约 15%，是更直立的坐姿；② 中途视角由侧面转到正面，120 ms 内有一次「转身坐下」；③ 首帧 228 px（`walk` 240，−5%）、末帧 186 px（`lying-idle` 176，+6%），是单一缩放下的残余误差，接入票里补 2–3 帧缩放过渡即可。

**对照**:两版对照图在 `artifacts/161/compare/`（不进 git）。

| 动作 | 源时间点 | 帧 | 帧数 | 每帧 | 类型 | 触发场景 |
| --- | --- | --- | --- | --- | --- | --- |
| `settle` | 0.083 s | `settle/00.png` 357×228 站 | 4 | 120 ms | 一次性 | `Walk` 结束、召唤到达 |
| `settle` | 2.500 s | `settle/01.png` 232×268 坐 | | 120 ms | 一次性 | 同上 |
| `settle` | 3.542 s | `settle/02.png` 273×240 折 | | 120 ms | 一次性 | 同上 |
| `settle` | 4.583 s | `settle/03.png` 360×186 趴 | | 120 ms | 一次性 | 同上；末帧接 `LyingIdle` |

### yawn / happy（P0-2，同一条片子）

- 源：`.../p0-2-yawn-happy/yawn-happy-source.mp4`（768×768，24 fps，5.17 s）；首末帧都用 `lying-idle.png` 锁定趴姿中性
- 缩放 0.5207（中性帧 338 px → 176 px），趴姿族
- 哈欠在 0.42–1.88 s，张开度最大约 1.04–1.33 s

| 动作 | 源时间点 | 帧 | 帧数 | 每帧 | 类型 | 触发场景 |
| --- | --- | --- | --- | --- | --- | --- |
| `yawn` | 0.625 s | `yawn/00.png` 342×177 张口 | 4 | 300 ms | 一次性 | 睡前 / 4–5 min 无交互时的随机 |
| `yawn` | 1.042 s | `yawn/01.png` 343×169 最大 | | 300 ms | 一次性 | 同上 |
| `yawn` | 1.333 s | `yawn/02.png` 343×170 头后仰 | | 300 ms | 一次性 | 同上 |
| `yawn` | 1.708 s | `yawn/03.png` 343×172 收回 | | 300 ms | 一次性 | 同上 |
| `happy` | 2.917 s | `happy/00.png` 342×177 眯眼 | 2 | 600 ms | 循环 | 工具完成、长按抚摸 |
| `happy` | 3.750 s | `happy/01.png` 342×176 慢呼吸 | | 600 ms | 循环 | 同上 |

### annoyed / startle（P0-4，同一条片子，2026-11-15 重抽）

- 源：`.../p0-4-annoyed-startle/annoyed-startle-source.mp4`（768×768，24 fps，5.17 s）
- 首末帧用**修正后的竖尾基准** `_refs/stand-tail-neutral.png`（见下方「尾巴标准」），缩放 0.4478（中性帧 536 px → 240 px）
- 耳朵后压在 1.25–2.29 s；瞪眼/后缩在 3.12–3.54 s

| 动作 | 源时间点 | 帧 | 帧数 | 每帧 | 类型 | 触发场景 |
| --- | --- | --- | --- | --- | --- | --- |
| `annoyed` | 1.250 s | `annoyed/00.png` 276×226 耳朵后压 | 3 | 250 ms | 一次性 | 连续点击（3 s 内 ≥3 次） |
| `annoyed` | 1.667 s | `annoyed/01.png` 275×239 保持 | | 250 ms | 一次性 | 同上 |
| `annoyed` | 2.292 s | `annoyed/02.png` 264×236 收回 | | 250 ms | 一次性 | 同上 |
| `startle` | 3.125 s | `startle/00.png` 256×230 瞪眼 | 2 | 150 ms | 一次性 | 拖起（开始拖动） |
| `startle` | 3.542 s | `startle/01.png` 256×230 后缩 | | 150 ms | 一次性 | 同上 |

### ear-flick / tail-flick / greet / look-up（静态图源）

这四个动作幅度太小，视频模型做不出来（`ear-flick` 两次试跑的抽动都读不出，`greet` 的慢眨眼也没出现），改用**改图**：以已确认帧为底，只改耳朵 / 尾尖 / 眼皮 / 头颈。改图后用**固定的族内缩放**（趴族 176/450），不按单帧高度归一，这样抬头帧自然比趴姿高几像素（181 vs 176）而不是被压回同一高度。

| 动作 | 源（中性底统一放 `interaction/_refs/`） | 帧 | 帧数 | 每帧 | 类型 | 触发场景 |
| --- | --- | --- | --- | --- | --- | --- |
| `ear-flick` | `_refs/lying-neutral.png` + `p0-3-ear-tail/ear-flick-01-ear-back.png` | `ear-flick/00-01.png` 341×176 | 2 | 150 ms | 一次性 | 指针靠近、随机被打扰 |
| `tail-flick` | `_refs/lying-neutral.png` + `p0-3-ear-tail/tail-flick-01-tail-up.png` | `tail-flick/00.png` 341×176 · `tail-flick/01.png` 370×186 | 2 | 150 ms | 一次性 | 同上 |
| `greet` | `_refs/stand-tail-neutral.png` + `p0-5-greet/greet-01-blink-shut.png` | `greet/00-01.png` 263×238 | 2 | 300 ms | 一次性 | 页面进入 / 切回我的JiuMi |
| `look-up` | `_refs/lying-neutral.png` + `p0-6-look-up/look-up-head-raised.png` | `look-up/00.png` 341×181 | 1 | 保持 | 一次性 | 房间内指针移动 / 靠近 |

## 交互动作抽帧 · P1（#161，2026-11-15）

P1 十个动作按「能不能用视频做」分成三类，只有 3 条片子真正付了费：

- **视频**：`groom`/`petting`、`scratch`、`stretch-long`（连续腿脚或大范围姿势变化）
- **改图**：`knead`、`swat`、`sleepy`、`head-tilt`、`sleep-twitch`（离散姿态或微动）
- **挖源**：`sit`、`loaf`（6.58 s 动作源抽帧 + 像素补白尖，零成本）

### groom / petting（P1-1，同一条片子）

源 `.../p1-1-groom-petting/groom-petting-source.mp4`（768×768，24 fps，124 帧）。首末帧都是 `_refs/lying-neutral.png`，缩放 0.5207（中性帧 338 px → 176 px），趴族。

| 动作 | 源时间点 | 帧 | 帧数 | 每帧 | 类型 | 触发场景 |
| --- | --- | --- | --- | --- | --- | --- |
| `groom` | 0.625 s | `groom/00.png` 341×166 舔爪 | 3 | 300 ms | 一次性 | 长按抚摸结束后 |
| `groom` | 1.000 s | `groom/01.png` 346×158 爪抹脸 | | 300 ms | 一次性 | 同上 |
| `groom` | 1.250 s | `groom/02.png` 347×161 抹到耳侧 | | 300 ms | 一次性 | 同上 |
| `petting` | 2.875 s | `petting/00.png` 341×178 眯眼 | 3 | 400 ms | 一次性 | 长按抚摸中（响应） |
| `petting` | 3.500 s | `petting/01.png` 341×186 仰头顶手 | | 400 ms | 一次性 | 同上 |
| `petting` | 4.000 s | `petting/02.png` 341×187 保持 | | 400 ms | 一次性 | 同上 |

### knead（P1-2，改图）

视频那条里踩奶**确实发生了**，但幅度只有 12 px（768 尺度），折到 200 DIP 是 3 px，运行时读不出来。所以不用它的帧，改用两张改图放大对比：爪压平 vs 抬爪。

| 动作 | 源 | 帧 | 帧数 | 每帧 | 类型 | 触发场景 |
| --- | --- | --- | --- | --- | --- | --- |
| `knead` | `p1-2-knead-scratch/knead-00-press.png` | `knead/00.png` 344×183 压平 | 2 | 200 ms | 循环 | 长按抚摸中（更深的满足） |
| `knead` | `p1-2-knead-scratch/knead-01-lift.png` | `knead/01.png` 337×176 抬爪 | | 200 ms | 循环 | 同上 |

缩放 0.3911，取自 `_refs/lying-neutral.png` 的原始高度 450 px，**不按各帧自身高度归一**——抬爪帧的包围盒本来就更矮。

### scratch（P1-2，同一条片子）

源 `.../p1-2-knead-scratch/scratch-source.mp4`。模型给的是一条「前腿抬到胸颈、保持不动」的单次动作，不是两下刮擦；200 DIP 下能读成挠痒，按 2 帧交付。

| 动作 | 源时间点 | 帧 | 帧数 | 每帧 | 类型 | 触发场景 |
| --- | --- | --- | --- | --- | --- | --- |
| `scratch` | 2.750 s | `scratch/00.png` 342×173 抬腿 | 2 | 250 ms | 一次性 | 休息中随机被打扰 |
| `scratch` | 3.417 s | `scratch/01.png` 342×173 抵颈 | | 250 ms | 一次性 | 同上 |

### swat（P1-3，改图）

| 动作 | 源 | 帧 | 帧数 | 每帧 | 类型 | 触发场景 |
| --- | --- | --- | --- | --- | --- | --- |
| `swat` | `p1-3-swat-stretch-long/swat-00-raised.png` | `swat/00.png` 264×240 抬爪 | 2 | 120 ms | 一次性 | 连续点击 / 指针快速划过 |
| `swat` | `p1-3-swat-stretch-long/swat-01-strike.png` | `swat/01.png` 264×240 挥出 | | 120 ms | 一次性 | 同上 |

缩放 0.3376，取自 `_refs/stand-tail-neutral.png`（781×711 → 240 站族）。

### stretch-long（P1-3，同一条片子）

源 `.../p1-3-swat-stretch-long/stretch-long-source.mp4`。这是本批唯一一条模型同时做到「正确绿幕 + 保住白尖尾 + 做对动作」的片子。

| 动作 | 源时间点 | 帧 | 帧数 | 每帧 | 类型 | 触发场景 |
| --- | --- | --- | --- | --- | --- | --- |
| `stretch-long` | 1.250 s | `stretch-long/00.png` 286×254 前肢前伸 | 4 | 260 ms | 一次性 | 长时间静置后醒来 |
| `stretch-long` | 1.667 s | `stretch-long/01.png` 287×262 胸口下沉 | | 260 ms | 一次性 | 同上 |
| `stretch-long` | 2.500 s | `stretch-long/02.png` 290×257 下巴贴地 | | 260 ms | 一次性 | 同上 |
| `stretch-long` | 3.333 s | `stretch-long/03.png` 264×253 起身 | | 260 ms | 一次性 | 同上 |

缩放 0.4528（站族 240）。**源片前爪贴到了画面底边**，因为落地线本身就是底边，裁切与地面线重合，导出后看不出。

### sleepy / head-tilt（P1-4，改图）

| 动作 | 源 | 帧 | 帧数 | 每帧 | 类型 | 触发场景 |
| --- | --- | --- | --- | --- | --- | --- |
| `sleepy` | `p1-4-sleepy-head-tilt/sleepy-00-droop.png` | `sleepy/00.png` 341×141 眼皮沉重 | 2 | 500 ms | 一次性 | 睡前（进入 `Sleep` 前） |
| `sleepy` | `p1-4-sleepy-head-tilt/sleepy-01-closed.png` | `sleepy/01.png` 341×144 闭眼下巴搭爪 | | 500 ms | 一次性 | 同上 |
| `head-tilt` | `p1-4-sleepy-head-tilt/head-tilt.png` | `head-tilt/00.png` 341×182 歪头 | 1 | 保持 | 一次性 | 指针靠近 / 工具结果到达 |

`head-tilt` 实测歪约 12–15°，规格写的是 20°，属已知偏离。

### sleep-twitch（P1-5，改图 + 复用 `sleep/00`）

| 动作 | 源 | 帧 | 帧数 | 每帧 | 类型 | 触发场景 |
| --- | --- | --- | --- | --- | --- | --- |
| `sleep-twitch` | `sleep/00.png`（复用） | `sleep-twitch/00.png` 334×147 睡球 | 2 | 400 ms | 循环 | `Sleep` 状态中随机 |
| `sleep-twitch` | `p1-5-sleep-twitch/sleep-twitch-01.png` | `sleep-twitch/01.png` 334×147 耳尖+爪尖轻抽 | | 400 ms | 循环 | 同上 |

### sit / loaf（P1-6，挖源，零成本）

从 6.58 s 动作源抽帧，抠像后用像素补丁把末端约 30 px 涂成近白（源本身丢白尖），描边保留。

| 动作 | 源时间点 | 帧 | 帧数 | 每帧 | 类型 | 触发场景 |
| --- | --- | --- | --- | --- | --- | --- |
| `sit` | 3.500 s | `sit/00.png` 277×208 | 1 | 保持 | 循环 | 待机 / 短暂停顿 |
| `loaf` | 3.000 s | `loaf/00.png` 366×176 | 1 | 保持 | 循环 | 长时间待机 |

`sit` 与仓库里的 `poses/sit.png` 是同一帧（277×208 vs 278×208），完全对得上。源图含补白尖前后两份，在 `p1-sit-loaf/`。

### P1 的两处教训

1. **「两个动作拼一条」只适用于同族且都靠表情/头部完成的动作。** `groom`+`petting` 成功，`knead`+`scratch` 只做成了后半个——踩奶的幅度被模型压到 3 px。判断标准不是"同一姿势族"，而是"动作幅度是否在同一数量级"。
2. **抠像前先遮掉角标。** 这条 stretch 片子的「AI生成」角标离后腿只有 4 px，某些帧会并进最大连通域。导出前把左上 130×60、右下 68×58 两块清零再测背景距离，能彻底避免角标残留在尾巴或后腿上。

## 尾巴标准（2026-11-15 定）

逐张数过已发布素材：**54 帧里 51 帧的尾巴是「粗短 + 末端白尖」**，只有 `stretch/03`、`jump/airborne`、`jump/landing` 三帧是「细长鞭状、无白尖」——那三帧全部来自 6.58 s 动作源。所以：

- **多数派（粗短 + 白尖）是标准**，细长无白尖的是抽帧管线的缺陷。
- 方向稿 `d117` 四张（趴/走/伸展/睡）**没有一张是竖尾**，所以竖尾造型从来没有被定义过。`_refs/stand-tail-neutral.png` 就是补出来的竖尾基准：以 `stretch/03` 为底，把尾巴换成多数派的粗短白尖形状。
- `poses/stand.png` **不能当站姿参考**——它就是那条细长无白尖的尾巴，用它生成会把缺陷放大（P0 首跑就是这么错的）。
- `stretch/03` 和 `jump/*` 的同类缺陷在线上，属已发布资产，单独修复，不在本票处理。


## 已有素材复用清单（2026-11-15）

先挖后生成：以下判定基于实际抽帧与尺寸比对，不是凭印象。

| 已有素材 | 本票是否复用 | 说明 |
| --- | --- | --- |
| `poses/sit.png` | ✅ 复用 | 3.50 s 抽帧，278×208；P1 的 `sit` 静态帧直接用 |
| 6.58 s 源的 3.00 s 趴姿 / 1.75–2.75 s 睡球 | ✅ 复用 | P1 的 `loaf`、`sleep-twitch` 基底，缩到趴族 176 |
| 6.58 s 源的 站→坐→趴 序列 | ❌ 不复用 | 源站姿是正侧面（576×297，比 1.94），现有 `walk` 是 3/4（354×240，1.47），无缩放比可同时对齐且不超 400 画布；挖出的趴姿还小约 20%。`settle` 只能生成 |
| 6.58 s 源的 `crouch` | ❌ 不复用 | 属 P2，不在本票 |
| `react/` | ❌ 不复用 | 只竖耳弹起、不抬头，`look-up` 用改图另做 |
| `blink/` | ❌ 不复用 | 3 帧趴姿眨眼，`greet` 是站姿慢眨眼，姿势族不同 |
| `jump/airborne` `landing` | ❌ 不复用 | 本票没有跳跃动作 |

## 两条踩过的坑（2026-11-15）

1. **不能用 `walk.png` 当站姿动作的首末帧。** 它本身就是"走路中"的姿态，模型会顺着它继续迈腿——`annoyed`/`greet` 的首跑两条片子猫一直在走，帧完全不可用。改用**`_refs/stand-tail-neutral.png`**（真正的静止站姿 + 多数派尾巴）后两条一次通过。`walk.png` 只适合做 `settle` 的起始帧。注意早期版本这里曾写过 `poses/stand.png`，那张尾巴是细长无白尖的，已废弃。
2. **幅度只有"眼睛/耳朵/尾巴"的小动作不要用视频。** 模型对这类微动几乎无响应（`ear-flick` 两次都没做出抽动，`greet` 的慢眨眼也没出现）。这类 2 帧动作直接改图：一次 5–10 credits，且姿势/尺寸完全一致。




