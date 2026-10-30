# 推箱子 Sokoban

在经典推箱子机制上加入「吸住拖拽 + 机关门 + 坑洞填平」，含关卡编辑器。
- 引擎：Unity 2022.3.51f1c1（LTS）
- 渲染管线：Built-in（内置）

核心规则：不再是「把所有箱子推到目标点」，而是**操控主角走到终点 `.` 即过关**。木箱、机关、坑洞是路上的障碍与工具。

---

## 一、、游戏流程和主要操作介绍

### 1. 游戏流程

#### 主页

主页是关卡卡片列表：内置关卡（暖色）与自定义关卡（青蓝色）用卡片底色区分；自定义卡片带「修改 / 删除」按钮（删除前弹窗确认）；列表末尾是「新增关卡」卡片。内置关卡按顺序解锁（第 `i` 关需第 `i-1` 关已通关），锁定关卡置灰不可点。

#### 对局

进入关卡后 3D 浮岛渲染棋盘，HUD 显示关卡名与「撤销 / 重做 / 重开 / 主页」按钮。

#### 通关

主角到达终点即通关：记录通关 → 胜利音效 → 黑场过渡 → 自动切入下一关（无下一关则回主页）→ 淡入恢复。

#### 关卡编辑器

从主页末尾「新增关卡」进入新建，或用自定义卡片上的「修改」进入编辑：

- 底部图块工具 9 种：墙 / 空 / 地面 / 终点 / 箱子 / 主角 / 触发器 / 机关门 / 提示。
- 左键 / 拖拽绘制，右键清除，中键点击（不拖拽）选中格（黑色边框），中键拖拽平移网格。
- 在触发器上按下拖到机关门（或反向）建立一对一机关关联。
- 「撤销 / 重做」；「▶ 测试」把当前关卡载入 3D 场景试玩（`Esc` 返回编辑）。
- 「保存」把内容裁成包围盒后存为自定义关卡。
- 中键点击提示块可编辑其说明文字。

### 2. 主要操作（键位）

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

### 3. 核心机制

#### 移动 / 推箱

`WASD` / 方向键移动；向前一格是木箱则推动它。被墙、坑洞、未开启的机关门、另一个箱子阻挡时移动无效（不计步、不响）。

#### 吸住 / 拖拽（`J`）

沿当前朝向吸住直线上最近的木箱（墙 / 未开启的门挡住视线，坑洞不挡）。吸住后木箱跟着主角移动；再按一次 `J` 释放。吸住 / 释放各计 1 步、可撤销。

#### 转向（`E`）

顺时针旋转朝向 90°。只转向、不移动、不计步。

#### 坑洞填平

把木箱推进坑洞（空格）时，木箱下沉消失，坑洞被填平变成可通行的岩面路面（音效为 fill）。

#### 机关（触发器 → 机关门）

木箱压在触发器 `!` 上时，其关联的机关门 `%` 打开（一对一关联）；箱子移开后门重新关闭。未关联的机关门视为永久关闭。

#### 撤销 / 重做 / 重开

快照栈实现（主角位置 + 箱子位置 + 坑洞/填平 + 计数 + 吸住状态）。`Z` 撤销、`Y` 重做、`R` 原地重开。

---

## 三、代码介绍

### 1. 程序集划分

| 程序集 | 目录 | 说明 |
|---|---|---|
| `SokoBan.Core` | `Scripts/Core/`（`SokoBan.Core.asmdef`） | 纯 C# 引擎与关卡数据，不依赖 UnityEngine，可单测、可复用 |
| `Assembly-CSharp` | `Scripts/App、Rendering、UI、Audio、Persistence` | 运行时游戏代码 |
| `SokoBan.Tests` | `Tests/Editor/`（`SokoBan.Tests.asmdef`） | EditMode 单元测试，引用 SokoBan.Core |

### 2. 模块与关键文件

**核心引擎（SokoBan.Core，纯 C#）**

- [Core/Game.cs](Scripts/Core/Game.cs) — 引擎：移动/推箱/吸住拖拽/转向、机关与坑洞判定、`Undo/Redo/Restart`、`IsWon`。ASCII 解析委托给 LevelParser。
- [Core/LevelData.cs](Scripts/Core/LevelData.cs) — 数据模型：`Dir`/`Direction`/`Box`/`LevelData`，及 `MoveOutcome`/`MoveResult`/`GrabOutcome`。
- [Core/LevelParser.cs](Scripts/Core/LevelParser.cs) — ASCII 关卡解析（`# . $ @ * + ! % - 空格 i` → `LevelData`），供 Game 与编辑器复用。
- [Core/Levels.cs](Scripts/Core/Levels.cs) — 内置关卡注册表，从 `Resources/SokoBan/levels.json` 加载（3 关，Id = c0..c2）。`ParMoves` 为历史遗留字段，当前未参与结算。

**渲染（3D，程序化生成）**

- [Rendering/Board3DRenderer.cs](Scripts/Rendering/Board3DRenderer.cs) — 场景构建（雾/光照/水面/浮岛/墙/地板/箱子/角色/火炬）、移动动画补间、与引擎状态同步、机关/吸住/目标高亮。
- [Rendering/BoardCameraRig.cs](Scripts/Rendering/BoardCameraRig.cs) — 相机取景（按棋盘包围盒自适应）。
- [Rendering/TweenRunner.cs](Scripts/Rendering/TweenRunner.cs) — 补间动画列表（内部工具）。
- [Rendering/Primitives.cs](Scripts/Rendering/Primitives.cs) — 材质 / 程序化网格（圆柱/圆锥/圆台）/ emissive 工具。
- [Rendering/Easing.cs](Scripts/Rendering/Easing.cs) — 缓动函数 + seeded 伪随机。
- [Rendering/FireEffect.cs](Scripts/Rendering/FireEffect.cs) — 程序化火炬火焰（粒子 + 点光 + 地面辉光）。

**应用主循环**

- [App/GameController.cs](Scripts/App/GameController.cs) — 输入路由、屏幕状态机（主页/对局/编辑器）、关卡载入、移动/吸住/转向/撤销/重做/重开、切关、通关（黑场→自动切关）。

**UI（预制体 + 代码绑定）**

- [UI/UIManager.cs](Scripts/UI/UIManager.cs) — UI 门面：构建画布、装配 主页/HUD/Toast/黑场，屏幕切换转发。
- [UI/HomeScreen.cs](Scripts/UI/HomeScreen.cs) — 主页：关卡卡片列表（内置 + 自定义）、颜色区分、修改/删除/新增入口、像素缩略图。
- [UI/HudController.cs](Scripts/UI/HudController.cs) — 对局 HUD：关卡名 + 撤销/重做/重开/主页按钮 + 提示弹窗。
- [UI/Toast.cs](Scripts/UI/Toast.cs) — 轻提示。
- [UI/ScreenFader.cs](Scripts/UI/ScreenFader.cs) — 黑场淡入淡出。
- [UI/LevelEditorUI.cs](Scripts/UI/LevelEditorUI.cs) — 编辑器门面：画布、图块工具、绘制/擦除/连线、测试试玩、保存。
- [UI/EditorDocument.cs](Scripts/UI/EditorDocument.cs) — 编辑器数据模型：网格 + 机关关联 + 撤销/重做 + 校验/裁剪/序列化。
- [UI/EditorGridView.cs](Scripts/UI/EditorGridView.cs) — 编辑器网格视图：实例化/上色格子、绘制机关连线、选中高亮。
- [UI/EditorCell.cs](Scripts/UI/EditorCell.cs) — 编辑器单个格子（点击/悬停/绘制回调）。
- [UI/EditorTestMode.cs](Scripts/UI/EditorTestMode.cs) — 编辑器「测试试玩」：在临时 Game 上跑输入、同步 HUD。
- [UI/LayoutConst.cs](Scripts/UI/LayoutConst.cs) — 编辑器网格布局常量与 2D 调色板。
- [UI/UiUtil.cs](Scripts/UI/UiUtil.cs) — UI 共用工具：节点查找/实例化/字体递归赋值 + RectTransform 铺满扩展。

**音效 / 存档**

- [Audio/SynthAudio.cs](Scripts/Audio/SynthAudio.cs) — 运行时合成音效。
- [Persistence/SaveData.cs](Scripts/Persistence/SaveData.cs) — 存档 DTO（`CustomLevel`/`ProgressEntry`/`Settings`/`SaveFile`）。
- [Persistence/SaveManager.cs](Scripts/Persistence/SaveManager.cs) — JSON 读写 + 关卡/进度/设置接口。

**测试**

- [Tests/Editor/GameTests.cs](Tests/Editor/GameTests.cs) — 引擎单元测试（移动/推箱/吸住/转向/坑洞/撤销重做/重开/胜负）。

### 3. 存档（JSON）

`SaveManager` 将数据写入 `Application.persistentDataPath/sokoban_save.json`：

- `levels`：自定义关卡（`id` / `name` / `rows` / `links` / `tips`）
- `progress`：每关通关记录（`id` / `completed` / `at` 时间戳）
- `settings`：`sound`

读写异常自动降级为内存默认。

---

## 四、资产介绍

### 1. UI 预制体（`Resources/UI/`）

UI 静态结构存放为 prefab，运行时 `Resources.Load` 实例化 + 按名字 `Find` 绑定：

| 预制体 | 说明 |
|---|---|
| `Home.prefab` | 主页 |
| `LevelCard.prefab` | 主页关卡卡片模板 |
| `Hud.prefab` | 对局 HUD |
| `Toast.prefab` | 轻提示 |
| `EditorScreen.prefab` | 编辑器界面 |
| `EditorCell.prefab` | 编辑器网格单元 |
| `LevelListItem.prefab` | 编辑器关卡列表项（历史遗留，编辑器已不再使用） |

### 2. 运行时资源（`Resources/SokoBan/`）

| 资源 | 说明 |
|---|---|
| `GamePalette.asset` | 全局调色板 + 渲染参数（3D 与 2D 共用一份颜色） |
| `levels.json` | 内置经典关卡数据（3 关，`id`/`name`/`rows`/`links`/`tips`） |
| `Materials/` | 角色材质（`Player_Cream` / `Player_Dark` / `Player_Orange` / `Player_White`） |
| `Meshes/` | 角色部件网格（帽子 / 躯干 / 围巾 / 帽檐 / 脚） |
| `Player.prefab` | 角色预制体 |
| `TipBar.prefab` | 提示块 3D 提示牌 |

### 3. 场景

- `Scenes/Main.unity` — 唯一主场景。根物体 `Sokoban` 挂 `GameController`，其余对象 / UI / 棋盘均运行时程序化生成。

### 4. 纹理（`Resources/Textures/`）

`bg.png`、`icon.png`、`lock.png`、`check.png`、`circle.png`、`track.png`、`pointer.png`、`EditLevelBtn.png`、`SettingBtn.png` 等为早期静态 UI 遗留纹理；当前 UI 已改为运行时构建，脚本中未直接引用这些纹理。
