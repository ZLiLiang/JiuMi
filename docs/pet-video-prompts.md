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

### 抽帧姿势（2026-11-12）

从上面的绿幕视频抽出 4 个候选姿势，已抠像、统一缩放并贴地对齐到 400×400 RGBA：

| 姿势 | 文件 | 时间点 | 用途 |
| --- | --- | --- | --- |
| 坐姿 | `docs/images/pet/action-source/poses/sit.png` | 3.5 s | 新的休息/起身过渡姿势 |
| 腾空 | `docs/images/pet/action-source/poses/airborne.png` | 5.3 s | 点击小跳的腾空帧 |
| 落地 | `docs/images/pet/action-source/poses/landing.png` | 5.6 s | 跳跃落地/蹲伏 |
| 站姿 | `docs/images/pet/action-source/poses/stand.png` | 6.4 s | 站姿参考；现有 `stretch/03` 已覆盖 |

- 预览：`docs/images/pet/action-source/poses/preview.png`。
- 处理：绿幕抠像；以站姿高度对齐现有 `stretch/03` 的 291 px；坐/落地/站贴地，腾空在播放跳跃时额外上移约 40 DIP。
- 已知差异：抽帧线宽比现有帧略细，眼部更圆；200 DIP 下可接受，2× 可见。接入 `src/JiuMi/Assets/MyJiuMi/` 前需要做一轮描边/眼部确认。
- 状态：候选素材，尚未接入运行时；先归档保留。


