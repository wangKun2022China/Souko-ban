# 推箱子 Sokoban · 3D（Unity 复刻）

对网页版推箱子游戏 `E:\TuiXiangZi` 的复刻：Townscaper 风格彩色积木浮岛 + 纪念碑谷式角色。玩法在经典推箱子的基础上加入「吸住拖拽 + 转向 + 机关门 + 坑洞填平」等机制，含完整关卡编辑器、运行时合成音效、JSON 存档。

- 引擎：Unity 2022.3.51f1c1（LTS）
- 渲染管线：Built-in（内置）
- 依赖：**零第三方**（动画补间、音效、UI 程序化/预制体、存档 JSON 均运行时生成）

---

## 一、核心规则

目标不再是「把所有箱子推到目标点」，而是**操控主角走到终点 `.` 即过关**。木箱、机关、坑洞是路上的障碍与工具。

### 1. 图块编码（ASCII）

| 图块 | 字符 | 说明 |
|---|---|---|
| 墙 | `#` | 不可通行 |
| 空（坑洞） | ` ` | 不可通行，也不铺地板 |
| 地板 | `-` | 可通行 |
| 终点 | `.` | 主角到达即过关 |
| 木箱 | `$` | 可推动 |
| 主角 | `@` | 玩家出生点 |
| 触发器（压板） | `!` | 被箱子压住时开启关联的机关门 |
| 机关门 | `%` | 未开启时不可通行 |
| 箱子在终点 | `*` | — |
| 主角在终点 | `+` | — |

### 2. 移动 / 推箱

`WASD` / 方向键移动；向前一格是木箱则推动它。被墙、坑洞、未开启的机关门、另一个箱子阻挡时移动无效（不计步、不响、不转向不生效的计数）。

### 3. 吸住 / 拖拽（`J`）

沿当前朝向吸住直线上最近的木箱（墙 / 未开启的门挡住视线，坑洞不挡）。吸住后木箱不瞬移，而是**跟着主角移动**；再按一次 `J` 释放。吸住/释放各计 1 步、可撤销。吸住中不能推别的箱子，只能拖拽被吸住的箱子。

### 4. 转向（`E`）

顺时针旋转朝向 90°。只转向、不移动、不改变地图、不计步。

### 5. 坑洞填平

把木箱推进坑洞（空格）时，木箱下沉消失，坑洞被填平变成**可通行的特殊路面**（音效为 fill）。

### 6. 机关（触发器 → 机关门）

木箱压在触发器 `!` 上时，其关联的机关门 `%` 打开（一对一关联）；箱子移开后门重新关闭。未关联的机关门视为永久关闭。

### 7. 撤销 / 重做 / 重开

快照栈实现（保存主角位置 + 箱子位置 + 坑洞/填平 + 计数 + 吸住状态）。`Z` 撤销、`Y` 重做、`R` 原地重开。

### 8. 死锁提示

非终点木箱位于两墙相邻的角落时判定为「简单死角」，开启 `deadlockHint` 后此类箱子红色高亮（只做安全判定，不误报）。

---

## 二、操作键位

| 键位 | 作用 |
|---|---|
| `W` / `↑` | 上移 |
| `S` / `↓` | 下移 |
| `A` / `←` | 左移 |
| `D` / `→` | 右移 |
| `J` | 吸住 / 释放木箱 |
| `E` | 顺时针转向 90° |
| `Z` | 撤销 |
| `Y` | 重做 |
| `R` | 重开本关 |
| `N` / `P` | 下一关 / 上一关 |
| `Esc` | 返回主页 |

计时器在**第一次有效移动**时才开始计时；被阻挡的移动不计数、不响、不返回。

---

## 三、游戏流程

### 1. 主页

运行 `Assets/Scenes/Main.unity`（场景根物体 `Sokoban`，挂 `GameController`，其余组件运行时动态添加）。启动后进入主页：关卡路径卡片（8 个内置关卡 + 自定义关卡）+「关卡编辑器」入口。每张卡片显示关卡像素缩略图；经典关卡按顺序解锁（`第 i 关` 需 `第 i-1 关` 已通关），锁定关卡置灰不可点。

### 2. 对局

进入关卡后 3D 浮岛渲染棋盘，HUD 显示关卡名与 撤销 / 重做 / 重开 / 主页 按钮。

### 3. 通关

主角到达终点即通关：记录通关 → 播放胜利音效 → **黑场过渡 → 自动切入下一关**（无下一关则回主页）→ 淡入恢复。

### 4. 关卡编辑器

主页「关卡编辑器」进入：

- 左侧调色板 8 种图块：墙 / 空 / 地面 / 终点 / 箱子 / 主角 / 触发器 / 机关门。
- 左键/拖拽绘制，右键擦除；在触发器上按下拖到机关门（或反向）建立一对一机关关联。
- 撤销 / 重做；「▶ 测试」把当前关卡载入 3D 场景试玩（`Esc` 返回编辑）。
- 「保存」把内容裁成包围盒后存为自定义关卡。
- 中键拖拽平移网格。
- **内置关卡不可编辑**：编辑器左侧列表只显示自定义关卡与「+ 新增关卡」。

---

## 四、存档（JSON）

`SaveManager` 将数据写入 `Application.persistentDataPath/sokoban_save.json`：

- `levels`：自定义关卡（`id` / `name` / `rows` / `links`）
- `progress`：每关通关记录（`id` / `completed` / `at` 时间戳）
- `settings`：`sound` / `deadlockHint`

读写异常自动降级为内存默认。

---

## 五、音效

`SynthAudio` 运行时用 `AudioClip.Create` 合成（正弦/三角/方波/锯齿 + 指数包络 + 频率滑音），对应 click / move / push / grab / release / fill / undo / error / win，零音频资产。

---

## 六、代码结构

### 1. 程序集划分

| 程序集 | 目录 | 说明 |
|---|---|---|
| `SokoBan.Core` | `Scripts/Core/`（`SokoBan.Core.asmdef`） | 纯 C# 引擎与关卡数据，不依赖 UnityEngine，可单测、可复用 |
| `Assembly-CSharp` | `Scripts/App、Rendering、UI、Audio、Persistence` | 运行时游戏代码 |
| `Assembly-CSharp-Editor` | `Scripts/Rendering/Editor/` | 仅编辑器内编译的场景生成工具 |
| `SokoBan.Tests` | `Tests/Editor/`（`SokoBan.Tests.asmdef`） | EditMode 单元测试，引用 SokoBan.Core |

### 2. 模块与关键文件

**核心引擎（SokoBan.Core，纯 C#）**

- [Core/Game.cs](Assets/Scripts/Core/Game.cs) — 引擎：移动/推箱/吸住拖拽/转向、机关与坑洞判定、`Undo/Redo/Restart`、`DeadlockedBoxes`、`IsWon`。ASCII 解析委托给 LevelParser。
- [Core/LevelData.cs](Assets/Scripts/Core/LevelData.cs) — 数据模型：`Dir`/`Direction`/`Box`/`LevelData`，及 `MoveOutcome`/`MoveResult`/`GrabOutcome`。
- [Core/LevelParser.cs](Assets/Scripts/Core/LevelParser.cs) — ASCII 关卡解析（`# . $ @ * + ! % - 空格` → `LevelData`），供 Game 与编辑器复用。
- [Core/Levels.cs](Assets/Scripts/Core/Levels.cs) — 8 个内置关卡（`Id = c0..c7`）。`ParMoves` 为历史遗留字段，当前未参与结算。

**渲染（3D，程序化生成）**

- [Rendering/Board3DRenderer.cs](Assets/Scripts/Rendering/Board3DRenderer.cs) — 场景构建（雾/光照/水面/浮岛/墙/地板/箱子/角色/火炬）、移动动画补间、与引擎状态同步、死锁/机关高亮。
- [Rendering/BoardCameraRig.cs](Assets/Scripts/Rendering/BoardCameraRig.cs) — 相机取景（按棋盘包围盒自适应）。
- [Rendering/TweenRunner.cs](Assets/Scripts/Rendering/TweenRunner.cs) — 补间动画列表（内部工具）。
- [Rendering/Primitives.cs](Assets/Scripts/Rendering/Primitives.cs) — 材质 / 程序化网格（圆柱/圆锥/圆台）/ emissive 工具。
- [Rendering/Easing.cs](Assets/Scripts/Rendering/Easing.cs) — 缓动函数 + seeded 伪随机。
- [Rendering/FireEffect.cs](Assets/Scripts/Rendering/FireEffect.cs) — 程序化火炬火焰（粒子 + 点光 + 地面辉光），浮岛前缘火炬用。
- [Rendering/Editor/SokobanSceneBuilder.cs](Assets/Scripts/Rendering/Editor/SokobanSceneBuilder.cs) — 菜单 `Sokoban/Build Preview Scene` 生成主场景。

**应用主循环**

- [App/GameController.cs](Assets/Scripts/App/GameController.cs) — 输入路由、屏幕状态机（主页/对局/编辑器）、计时、关卡载入、移动/吸住/转向/撤销/重做/重开、切关、通关（黑场→自动切关）。

**UI（预制体 + 代码绑定）**

- [UI/UIManager.cs](Assets/Scripts/UI/UIManager.cs) — UI 门面：构建画布、装配 主页/HUD/Toast/黑场，屏幕切换转发。
- [UI/HomeScreen.cs](Assets/Scripts/UI/HomeScreen.cs) — 主页：关卡卡片列表（经典 + 自定义）、像素缩略图、编辑器入口。
- [UI/HudController.cs](Assets/Scripts/UI/HudController.cs) — 对局 HUD：关卡名 + 撤销/重做/重开/主页按钮。
- [UI/Toast.cs](Assets/Scripts/UI/Toast.cs) — 轻提示。
- [UI/ScreenFader.cs](Assets/Scripts/UI/ScreenFader.cs) — 黑场淡入淡出。
- [UI/LevelEditorUI.cs](Assets/Scripts/UI/LevelEditorUI.cs) — 编辑器门面：画布、调色板、绘制/擦除/连线、测试试玩、保存。
- [UI/EditorDocument.cs](Assets/Scripts/UI/EditorDocument.cs) — 编辑器数据模型：网格 + 机关关联 + 撤销/重做 + 校验/裁剪/序列化。
- [UI/EditorGridView.cs](Assets/Scripts/UI/EditorGridView.cs) — 编辑器网格视图：实例化/上色格子、绘制机关连线。
- [UI/EditorCell.cs](Assets/Scripts/UI/EditorCell.cs) — 编辑器单个格子（点击/悬停/绘制回调）。
- [UI/EditorLevelList.cs](Assets/Scripts/UI/EditorLevelList.cs) — 编辑器左侧关卡列表（自定义 + 新增）。
- [UI/EditorTestMode.cs](Assets/Scripts/UI/EditorTestMode.cs) — 编辑器「测试试玩」：在临时 Game 上跑输入、同步 HUD。
- [UI/LayoutConst.cs](Assets/Scripts/UI/LayoutConst.cs) — 编辑器网格布局常量与调色板。
- [UI/UiUtil.cs](Assets/Scripts/UI/UiUtil.cs) — UI 共用工具：节点查找/实例化/字体递归赋值 + RectTransform 铺满扩展。

**音效 / 存档**

- [Audio/SynthAudio.cs](Assets/Scripts/Audio/SynthAudio.cs) — 运行时合成音效。
- [Persistence/SaveData.cs](Assets/Scripts/Persistence/SaveData.cs) — 存档 DTO（`CustomLevel`/`ProgressEntry`/`Settings`/`SaveFile`）。
- [Persistence/SaveManager.cs](Assets/Scripts/Persistence/SaveManager.cs) — JSON 读写 + 关卡/进度/设置接口。

**测试**

- [Tests/Editor/GameTests.cs](Assets/Tests/Editor/GameTests.cs) — 引擎单元测试（移动/推箱/吸住/转向/坑洞/撤销重做/重开/胜负）。

### 3. 运行时组装

场景只含根物体 `Sokoban`（`GameController`）。`GameController.Awake()` 动态 `AddComponent` 其余组件：

```
Sokoban (GameController)
├─ Board3DRenderer   —— 3D 场景 + 动画
├─ UIManager         —— 主 UI（Canvas sortingOrder 10）
├─ SynthAudio        —— 音效
└─ LevelEditorUI     —— 编辑器（独立 Canvas sortingOrder 20）
```

### 4. UI 预制体工作流

UI 静态结构存放在 `Assets/Resources/UI/*.prefab`；运行时 `Resources.Load` 实例化 + 按名字 `Find` 绑定：

```
Assets/Resources/UI/
├─ Home.prefab         主页
├─ LevelCard.prefab    主页关卡卡片模板
├─ Hud.prefab          对局 HUD
├─ Toast.prefab        轻提示
├─ EditorScreen.prefab 编辑器界面
├─ EditorCell.prefab   编辑器网格单元
└─ LevelListItem.prefab 编辑器关卡列表项
```

> 注意：prefab 内 `Text` 默认字体为 Arial（编辑器内中文显示豆腐块），运行时由代码统一替换为微软雅黑，不影响运行效果。

### 5. 关键坐标与配色约定

- 3D 棋盘：`CellToWorld(r,c)` 中 `列→x`（取反）、`行→z`，原点居中；相机 `θ=30°` 俯视、`φ=90°` 从 +z 正视。
- 场景背景 / 雾色 `#d8e6e2`。
- 主页纪念碑谷暖色主题：浅色渐变 `#fbeede → #f6dcc9 → #dbe8e4`，深色 `#2b2620 → #332a20 → #1f2b2a`；ink `#45392f` / `#f0e7da`；蜜桃 `#e8a87c→#d98a5f`、青绿 `#6bb5a8→#4f948a`。

---

## 构建与运行

1. 用 Unity 2022.3.51f1c1 打开项目 `SokoBanGame`。
2. 打开场景 `Assets/Scenes/Main.unity`（若缺失，菜单 `Sokoban/Build Preview Scene` 生成）。
3. 点 Play 运行。
