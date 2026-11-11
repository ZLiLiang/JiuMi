# 我的JiuMi 视频生成提示词

记录桌面猫动画所用的 VideoGen 提示词与后处理，方便后续制作跳跃、趴下、起身等动作。提示词按「参考图 + 动作 + 固定机位 + 风格约束」组织；换动作时只替换动作段，其余约束保持不变。

## 走路循环

- 参考图：`docs/images/pet/direction-117/walk.png`（已确认走路单帧）
- 生成：VideoGen，wan30，`reference_to_video`，5 s，1280×720
- 步态参考：owner 提供的 GIF（13 帧，约 0.85 s/循环）；仅作参考，不进 git。
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
