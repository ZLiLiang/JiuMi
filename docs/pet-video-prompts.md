# 我的JiuMi 视频生成提示词

记录桌面猫动画所用的 Image Edit / VideoGen 提示词与后处理规范。**Phase 2（#211）已冻结 v2 极简日系绘本橘猫角色风格母版、7 张基准母姿（含 4 核心母姿 + 3 向坐姿看鼠标母姿）与标准化提示词模板**；Phase 3 全量新动画生产一律以「v2 标准化模板」章节为唯一入口，下方保留 v1 历史记录备查。

---

## v2 冻结母版与标准化提示词模板（#211，Phase 2）

### 1. 冻结参考资产（唯一参考源）

| 资产 | 路径 | 规格与对齐 | 用途 |
| --- | --- | --- | --- |
| 角色风格母版 | `docs/images/pet/v2-masters/style-sheet.png` | 圆形脸部特写 + 3/4 站姿标尺 + 实测 Hex 色板 + 描边线宽样本 + 4 核心母姿 + 3 坐姿母姿排布 | 角色身份、配色、线宽唯一依据 |
| 六姿原图与拎起原图 | `docs/images/pet/v2-masters/raw/master-6poses-source-raw.png`、`raw/master-drag-source-raw.png` | `1024×1024` 极简绘本纯文生图六姿原图（`#F2EEE3` 底）+ 派生拎起单图（`#F1EEDF` 底） | 7 张冻结母姿的唯一无损源画布 |
| 母姿 A：站姿 | `docs/images/pet/v2-masters/master-standing.png` | `400×400` 8-bit RGBA · `anchor: { x: 200, y: 360 }` · `scale=1.000`（`284×272`） | 静止四足站立（前后双腿各自并拢）；`stretch` 末帧、`walk` 首帧、`jump` 起止基准 |
| 母姿 B：趴姿 | `docs/images/pet/v2-masters/master-lying.png` | `400×400` 8-bit RGBA · `anchor: { x: 200, y: 360 }` · `scale=1.000`（`293×215`） | `lying-idle` / `blink` / `react` 底图；`stretch` 首帧、`wake` 末帧、`sleep-in` 首帧、`sit-to-lie` 末帧 |
| 母姿 C：熟睡 | `docs/images/pet/v2-masters/master-sleeping.png` | `400×400` 8-bit RGBA · `anchor: { x: 200, y: 360 }` · `scale=1.000`（`280×194`） | `sleep` 底图；`sleep-in` 末帧、`wake` 首帧 |
| 独立态：拎起 | `docs/images/pet/v2-masters/master-drag.png` | `400×400` 8-bit RGBA · `anchor: { x: 200, y: 360 }` · `scale=0.400`（`318×264`） | `drag` 循环底图 |
| 坐姿左看 | `docs/images/pet/v2-masters/master-sit-left.png` | `400×400` 8-bit RGBA · `anchor: { x: 200, y: 360 }` · 下半身对齐 `sit-center`（`282×309`） | `interactions.sit-watch-left`（鼠标在头左侧 `< -60 DIP`）；`sit-to-lie` 首帧 |
| 坐姿中看 | `docs/images/pet/v2-masters/master-sit-center.png` | `400×400` 8-bit RGBA · `anchor: { x: 200, y: 360 }` · `scale=1.000`（`261×309`） | `interactions.sit-watch-center`（`\|cursorX - catHeadX\| <= 60 DIP`） |
| 坐姿右看 | `docs/images/pet/v2-masters/master-sit-right.png` | `400×400` 8-bit RGBA · `anchor: { x: 200, y: 360 }` · 下半身对齐 `sit-center`（`256×311`） | `interactions.sit-watch-right`（鼠标在头右侧 `> +60 DIP`） |
| 单态切片原图 | `docs/images/pet/v2-masters/raw/master-{lying,standing,sleeping,drag,sit-left,sit-center,sit-right}-raw.png` | 去背归一化至 `#F4EFE9` 纯底的 `600×600` 单姿原图（与 `400×400` 严格 `1.5×` 对应，`anchor: 300,540`） | L1/L2/L3 模型输入原图（导出统一乘 `2/3 ≈ 0.667`） |
| 审片图 | `docs/images/pet/v2-masters/onion-skin-masters.png`、`onion-skin-sit-watch.png`、`preview-light-dark-200dip.png` | `tools/verify-pet-pack.ps1` 渲染输出 | 4 核心母姿洋葱皮 + 3 坐姿下身锁死洋葱皮 + 7 姿浅/深色 200 DIP 边缘校验 |

**冻结色板与物理标尺（极简日系绘本风）**：
- 轮廓深棕 `#542E12`（圆角连接，同步用于脸颊胡须；`400×400` 导出 **`~5.0 px`** → `200 DIP` 运行时 `~2.5 px`）；主毛蜂蜜暖橘 `#E89C4A`；额头三道对称竖纹 `#CD7834`（**仅额头 3 道对称圆头竖条，不画体侧与四肢虎斑、不单独描边**）；奶油白 `#F9EDD8`（口鼻/圆润胸口/腹缘/四只短圆白爪/短白尖尾）；耳廓粉 `#EFB29D`；粉鼻 `#D39175`；琥珀虹膜 `#DE9C48`（清晰深棕瞳孔 + 小白高光）；参考画布底色 `#F4EFE9`（仅生成用，运行时透明）。
- **物理标尺（`400×400` 实测自 `master-standing.png`）**：头宽 **`~136 px`**、躯干长（胸前至臀后不含尾）**`~185 px`**（柔和紧凑的 **2.2–2.4 头身**绘本比例）、含耳总高 **`272 px`**（`y: 89..360`）。六格原图全部采用 **`scale = 1.000`** 零插值模糊导出，`drag` 单图按头宽对齐采用 **`scale = 0.400`** 导出；7 张母姿全部 **`maxY = 360`**（`foot_dy = 0 px`，底边留 `40 px` 垂尾余量），质心 `x = 198.20–201.54`。`sit-left` / `sit-right` 严格按 `y = 195..360` 下半身轮廓最大化重合度对齐至 `sit-center`（`lowerIoU = 0.9869 / 0.9959`），保证转头看鼠标时坐姿躯干与尾巴稳如磐石。
- **花纹与尾巴一致性标准（v2 冻结）**：去掉体侧条纹、四肢条纹与尾巴环纹，只保留**额头正中 3 道对称圆头竖纹**、**每侧脸颊 2 根短巧深棕小胡须**、以及**短巧紧凑的纯暖橘（`#E89C4A`）白尖（`#F9EDD8`）尾**，兼顾 `200 DIP` / `160 DIP` 清晰度、AI 跨帧零闪烁与「坐立头部跟随鼠标 → 看累趴下睡觉」交互扩展。

### 2. 极简绘本 6 姿母版 + `drag` 派生生成模板

```text
# 2x3 六姿母版纯文生图（站 / 趴 / 睡 + 坐姿左/中/右转头）
A clean 2-row character concept sheet (6 sprites: 3 on top row, 3 on bottom row) of the exact same simplified cozy Japanese picture-book orange cat on a uniform solid warm off-white background (#F4EFE9). No grid lines, no borders, no text, no labels, no ground shadows, no props.

Style: Warm indie 2D hand-drawn picture-book illustration, smooth bold uniform dark-brown outline (#542E12) with rounded joins, pure flat cel fills, zero fur strands, zero gradients, zero 3D shading. Compact softly rounded 2.2-2.4 head-lengths proportions (approachable and expressive, not cheap giant-head chibi, not rigid geometric logo).

Minimal markings (100% identical across all 6 sprites): Solid warm honey-orange body (#E89C4A) with NO body stripes, NO leg stripes, and NO tail rings.
FOREHEAD MARKING LOCK (100% IDENTICAL IN ALL 6 SPRITES): Centered directly between the two ears starting from the top crown of the head, draw EXACTLY three neat, clean, symmetrical vertical pill-shaped stripes in darker orange (#CD7834) pointing straight down toward the eyes — one slightly longer center stripe flanked by two shorter identical side stripes, evenly spaced and uniform in thickness (NEVER random dots, NEVER M-shaped, NEVER slanted or wavy).
Cream-white (#F9EDD8) muzzle, chest bib, belly trim, four neat short white paws, and a small cream-white tip on a short compact solid-orange tail. Soft pink inner ears (#EFB29D), tiny pink nose (#D39175), 2 short neat delicate dark-brown whiskers on each cheek (#542E12), and expressive half-lidded amber eyes with clear dark pupils.

Top row (3 poses facing left in 3/4 view): 1. Stationary standing on all four white paws; 2. Lying comfortably on belly with paws tucked forward; 3. Curled peacefully asleep with closed curved eyes, no Zzz.
Bottom row (3 sitting mouse-watching poses — EXACT SAME stationary sitting body & curled tail in all 3, ONLY the head and eyes turn): 4. Sitting upright looking left; 5. Exact same sitting body looking forward/slightly up; 6. Exact same sitting body looking right.

# 从站姿单图派生拎起（drag）母姿（1:1 图生图）
Using the provided 2D simplified Japanese picture-book orange cat as the EXACT character and style reference, draw a single centered 1:1 sprite of this exact cat floating gently in mid-air as if lifted softly from the upper back by an invisible force, facing left in 3/4 view on the exact same uniform warm off-white background (#F2EEE3). Keep the exact same 2.2-2.4 head-lengths rounded picture-book proportions, head size, bold uniform dark-brown outline (#542E12), pure flat cel colors, solid honey-orange body with NO body stripes and NO leg stripes, EXACTLY three symmetrical vertical pill-shaped darker-orange forehead stripes between the ears, 2 short neat dark-brown whiskers per cheek, pale pink inner ears, tiny pink nose, cream-white muzzle and chest bib, four short rounded white paws, and short solid-orange tail with a small rounded cream-white tip. The back is gently arched, all four legs and the white-tipped tail dangle loosely downward, with a calm slightly aloof half-lidded expression. Clean isolated single cat sprite only — no human hand, no collar, no grid, no ground shadow, no text.
```

### 3. L1 局部编辑模板（`lying-idle` / `blink` / `sleep` / `react` / `drag` / `interactions.sit-watch-*`）

- **适用状态**：`lying-idle`、`blink`、`sleep`、`react`、`drag`，以及坐姿 9 向 `interactions.sit-watch-{center,up,up-right,right,down-right,down,down-left,left,up-left}`（#215 / #226 同步校验坐姿下身零偏移）。
- **输入**：对应冻结母姿的切片原图（`docs/images/pet/v2-masters/raw/master-{lying,standing,sleeping,drag,sit-left,sit-center,sit-right}-raw.png`，`600×600` 纯 `#F4EFE9` 底）。
- **后处理硬门**：仅抠出局部变化区域（眼/耳/尾/呼吸胸腹），用 **Alpha Mask 回贴到冻结的 `400×400` 母姿底图**，确保未编辑区域的躯干与外轮廓达到 `diff = 0`（通过 `tools/verify-pet-pack.ps1` 的 L1 `MinL1ExactMatchPct >= 80%` 门）。

```text
Edit ONLY the specified local region of the provided 2D simplified Japanese picture-book illustration of the orange cat.

STRICT LOCKS (DO NOT CHANGE):
- Keep the exact same character identity, 2.2-2.4 head-lengths rounded picture-book proportions, body silhouette, torso position, head size, camera framing, bold uniform dark-brown outline (#542E12, ~5 px @ 400x400), pure flat cel palette (#E89C4A honey-orange solid fur, ONLY 3 symmetrical vertical pill-shaped forehead stripes #CD7834 with NO body/leg stripes, compact solid-orange tail with NO dark rings and a small rounded cream-white tip #F9EDD8, #F9EDD8 white muzzle/chest bib/belly/short white paws, #EFB29D inner ears, #D39175 pink nose, #DE9C48 amber iris, 2 short neat whiskers per cheek), and plain solid uniform warm off-white background (#F4EFE9).
- Do NOT shift, scale, rotate, or redraw the unedited body, paws, or outer contour.
- Do NOT add body stripes, fur strands, gradients, 3D shading, ground shadows, props, text, or watermarks.

LOCAL EDIT ONLY:
<REPLACE_WITH_STATE_EDIT:
  - blink-half: lower the upper eyelids halfway over the amber irises while keeping head, ears, whiskers, and body 100% identical.
  - blink-closed: close both eyes smoothly into gentle dark-brown curved lines while keeping head, ears, whiskers, and body 100% identical.
  - lying-idle-01: gently sway ONLY the short cream-tipped tail tip slightly upward/outward by ~12 px and add a subtle 4 px chest rise from breathing; keep head, face, front paws, and ground line 100% locked.
  - sleep-01: add a subtle 5 px slow-breathing rise on the back contour only; keep folded ears, closed eyes, paws, and curled tail 100% locked.
  - react-01: perk both ears slightly more upright and widen the almond amber eyes slightly in mild surprise; keep torso and paws 100% locked.
  - drag-01: sway ONLY the dangling hind paws and hanging cream-tipped tail slightly to the right by ~10 px; keep head, shoulders, and torso 100% locked.
>
```

### 4. L2 首尾帧过渡 I2V 模板（`stretch` / `sleep-in` / `wake` / `jump` / `interactions.sit-to-lie`）

- **适用状态**：`stretch`（`master-lying` → `master-standing`）、`sleep-in`（`master-lying` → `master-sleeping`）、`wake`（`master-sleeping` → `master-lying`）、`jump`（`master-standing` → 垂直轻跳 → `master-standing`），以及 `interactions.sit-to-lie`（`master-sit-left` → `master-lying`，4–6 帧约 0.5 s，服务 #221「坐立看累后缓缓趴下睡觉」）。
- **生成参数**：Image-to-Video（First + Last Frame Lock），1:1（`768×768` 或 `1024×1024`），`24 fps`，`2–3 s`（`sit-to-lie` 可 `1.5–2 s`），非循环。
- **抽帧与后处理硬门**：核心过渡均匀抽取 **6–8 帧**（运行时 `10–12 fps`，即 `83–100 ms/帧`）；`sit-to-lie` 抽取 **4–6 帧**；整段使用**单一缩放系数**与**统一抠像去边（defringe 至 `#542E12`）**对齐至 `anchor: { x: 200, y: 360 }`；过 `tools/verify-pet-pack.ps1` 连续性硬门（相邻帧质心突跳 `<= 6 px`、包围盒缩放变化 `<= 5%`）。

```text
Animate a smooth, natural 2D hand-drawn frame-by-frame transition between the locked start frame and the locked end frame of this exact simplified Japanese picture-book orange cat.

STRICT STYLE & CAMERA LOCKS:
- Preserve the exact same character design, 2.2-2.4 head-lengths rounded picture-book proportions, almond half-lidded amber eyes, pink nose, 2 short neat whiskers per cheek, cream-white muzzle/chest bib/belly/short white paws, 3 symmetrical vertical pill-shaped darker-orange forehead stripes (#CD7834) ONLY (NO body stripes, NO leg stripes), compact solid warm-orange tail (#E89C4A, NO dark rings) with a small rounded cream-white tip (#F9EDD8), bold uniform dark-brown outline (#542E12), and pure flat cel illustration style.
- Do NOT restyle, do not add body stripes, 3D depth, fur texture, gradients, soft glow, or photorealism.
- Locked static camera: zero zoom, zero pan, zero tilt, zero camera shake.
- Plain solid uniform warm off-white background (#F4EFE9) throughout every frame.
- No ground shadow, no floor line, no props, no motion lines, no text, no watermark, no extra cats.

MOTION (IN PLACE, NO HORIZONTAL DRIFT):
<REPLACE_WITH_TRANSITION:
  - stretch (lying -> standing): The cat smoothly pushes up from the lying pose into a brief natural feline back-arch/front-paw stretch, then settles cleanly into the exact locked neutral four-paw standing end frame in place.
  - sleep-in (lying -> sleeping): The cat smoothly lowers its head onto its front paws, gently folds its ears slightly, closes its eyes, and tucks snugly into the exact locked curled sleeping end frame in place.
  - wake (sleeping -> lying): The cat smoothly lifts its head from its paws, perks its ears back up, opens its almond half-lidded amber eyes, and settles into the exact locked lying end frame in place.
  - sit-to-lie (sit-left -> lying): The cat smoothly lowers its chest and front paws forward onto the ground from the sitting pose, curling the short white-tipped tail beside its hip into the exact locked lying end frame in place.
  - jump (standing -> standing): The cat performs a small vertical hop in place — brief crouch anticipation, push-off with all four white paws tucked cleanly off the ground, short airborne apex, soft landing back onto the exact locked four-paw standing end frame. No forward translation.
>
```

### 5. L3 固定机位走路 I2V 模板（`walk`）

- **适用状态**：`walk`（全身连续循环；只需面向左侧的单向循环，向右由运行时水平镜像）。
- **输入**：首帧锁定 `docs/images/pet/v2-masters/raw/master-standing-raw.png`（静止四足站姿，确保起手比例、额头三纹与短白尖尾 100% 对齐），步态节奏参考 `docs/images/pet/references/walk-reference.gif`。
- **生成参数**：Reference / Image-to-Video，固定机位，`3–5 s`，`24 fps`，纯 `#F4EFE9` 底。
- **抽帧与后处理硬门**：选取 1 个完整左右交替步态周期，抽取 **16–20 帧**（运行时 `18–24 fps`，即 `42–56 ms/帧`，按页面移速 `~103 DIP/s` 匹配步幅不滑步）；整段采用**单一裁切与单一缩放系数**，统一去边对齐至 `anchor: { x: 200, y: 360 }`；通过 `tools/verify-pet-pack.ps1` 连续性硬门（相邻帧质心突跳 `<= 6 px`、包围盒缩放变化 `<= 5%`）。

```text
Animate the provided 2D simplified Japanese picture-book illustration of the orange cat (starting from the locked neutral standing pose) into a smooth, seamless in-place walk cycle facing left.

STRICT STYLE & CAMERA LOCKS:
- Keep the exact same character identity, 2.2-2.4 head-lengths rounded picture-book proportions, face, almond half-lidded amber eyes, pink nose, 2 short neat whiskers per cheek, cream-white muzzle/chest bib/belly/short white paws, 3 symmetrical vertical pill-shaped darker-orange forehead stripes (#CD7834) ONLY (NO body stripes, NO leg stripes), compact solid warm-orange tail (#E89C4A, NO dark rings) with a small rounded cream-white tip (#F9EDD8), bold uniform dark-brown outline (#542E12), and pure flat cel illustration style.
- Do NOT restyle, do not add body/leg stripes, do not make it 3D, do not add fur strands, gradients, shading flicker, or photorealism.
- Locked static camera: zero zoom, zero pan, zero tilt; plain solid uniform warm off-white background (#F4EFE9).
- Full body visible in left-facing 3/4 side view, all four white paws clearly visible and never cropped.

WALK CYCLE MOTION (TREADMILL IN PLACE):
- The cat walks smoothly in place to the left as if on an invisible treadmill, matching a natural four-beat feline walk gait: front legs alternate cleanly (left forward, then right forward), hind legs alternate naturally with slight shoulder/hip sway, and the short cream-tipped tail sways gently and relaxed behind the body without touching the ground line.
- Keep the cat at the exact same scale, horizontal center, and vertical ground plane across every frame; zero forward translation, zero vertical drift, zero sliding paws.
- Smooth seamless loop. No ground shadow, no floor line, no props, no motion lines, no text, no watermark, no extra cats.
```

---

## v1 历史记录（#121 / #161 / #163 归档备查）

### 走路循环（v1）

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
- 已知差异：抽帧线宽比现有帧略细，眼部更圆；200 DIP 下可接受，2× 可见。尾巴已按多数派修正（#163）。

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

**白尖位置修正（2026-11-15 晚）**：第一版把白涂在了「离质心最远、且在画面右半」的像素上——那个位置其实是**臀部/大腿外侧**，不是尾尖。这两个姿势的尾巴是**从臀部绕下、沿地面朝前扫**，尾尖落在**底部中偏左**、贴近前爪处：

| 动作 | 尾尖坐标（挖源帧内） | 错误位置（第一版） |
| --- | --- | --- |
| `sit` | (217, 271) | (403, 267) 右缘臀部 |
| `loaf` | (237, 184) | (423, 151) 右缘臀部 |

**涂法（第二版，现行）**：白区 = **以尾尖为圆心、r=30 px 的圆** ∩ 尾巴内部，同时受测地距离约束（`g < 2.2r`，沿描边内侧传播）防止绕到别处。

两处细节必须照做，否则会退回错误版本：

1. **传播时把暗描边排除掉**（`luma < 110` 的像素不参与连通传播）。不排除的话白会跨过尾巴的上缘描边漏进腹侧，出现「一块矩形填白」的观感——这正是第二版被否掉的原因。
2. **用圆而不是测地等距线**。尾巴是等宽带状，测地等距线的边界是**垂直于尾巴的直线**，看起来就像拿矩形框填的；而参考画法（`walk/00` 的尾帽）是一条**向身体方向鼓出的弧**，半径 30 px 的圆截在尾巴上正好得到这个形状。

包围盒尺寸不变：`sit` 仍 277×208、`loaf` 仍 366×176。

已知局限：这两个姿势的尾尖**紧贴腹部白/前爪白**，白尖会和腹侧白连成一片，200 DIP 下对比不如 `walk/00` 那种外伸尾巴明显。要更清楚只能改姿势（把尾巴改到体侧外伸），那会改动 `poses/sit.png` 的既有造型，本票不做。

### P1 的两处教训

1. **「两个动作拼一条」只适用于同族且都靠表情/头部完成的动作。** `groom`+`petting` 成功，`knead`+`scratch` 只做成了后半个——踩奶的幅度被模型压到 3 px。判断标准不是"同一姿势族"，而是"动作幅度是否在同一数量级"。
2. **抠像前先遮掉角标。** 这条 stretch 片子的「AI生成」角标离后腿只有 4 px，某些帧会并进最大连通域。导出前把左上 130×60、右下 68×58 两块清零再测背景距离，能彻底避免角标残留在尾巴或后腿上。

## 尾巴标准（2026-11-15 定）

逐张数过已发布素材：**54 帧里 51 帧的尾巴是「粗短 + 末端白尖」**，只有 `stretch/03`、`jump/airborne`、`jump/landing` 三帧是「细长鞭状、无白尖」——那三帧全部来自 6.58 s 动作源。所以：

- **多数派（粗短 + 白尖）是标准**，细长无白尖的是抽帧管线的缺陷。
- 方向稿 `d117` 四张（趴/走/伸展/睡）**没有一张是竖尾**，所以竖尾造型从来没有被定义过。`_refs/stand-tail-neutral.png` 就是补出来的竖尾基准：以 `stretch/03` 为底，把尾巴换成多数派的粗短白尖形状。
- `poses/stand.png` **不能当站姿参考**——它就是那条细长无白尖的尾巴，用它生成会把缺陷放大（P0 首跑就是这么错的）。
- `stretch/03` 和 `jump/*` 的同类缺陷已在 #163 修复（见下一节），三帧现在与多数派一致。

### 尾巴修复（#163，2026-11-15）

首版用像素拼接只换尾巴，接根留下旧尾残边和断口，两次视觉退回。最终采用 **ImageGen 整帧改图**：以原帧为底、`_refs/stand-tail-neutral.png` 只提供尾巴造型；生成后抠像、按「遮掉尾巴」的剪影 ECC 对齐回原帧的 400 画布（脚底 `y=399`），**整帧直接作为运行时资产**，不再做尾巴拼接。

| 帧 | 生成参考 | 对齐 | 交付 |
| --- | --- | --- | --- |
| `stretch/03` | 原帧 + 参考尾（长度以上一轮「加长」那版为准） | 剪影 ECC cc 0.999 | 竖尾、白尖高于头；`stretch/02` → `03` 无突变 |
| `jump/landing` | 原帧 + 参考尾 | 剪影 ECC cc 0.998 | 竖尾白尖；闭眼姿势保持 |
| `jump/airborne` | 原帧 + 参考尾；尾向约 45° 上右 | 剪影 ECC cc 0.999 | 白尖高于背线；尾根落在原臀部 |

- 三帧为 400×400 RGBA、底边 `y=399`、身体水平位置与原帧一致；米色底按边界 flood fill 抠像，残边 defringe，浅/深色 200 DIP 下无白边/绿边。
- **已知偏离**：整帧重绘意味着身体是「同姿势、同比例、同位置」的重绘版，不是原帧的像素级复制；owner 于 2026-11-15 确认以整帧生成图为准，不再使用拼接。
- 未用视频重新生成；不改代码、帧数/命名/状态。


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




