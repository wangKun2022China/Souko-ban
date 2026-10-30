using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SokoBan.Core;

namespace SokoBan.Rendering
{
    // 绘制场景
    public class Board3DRenderer : MonoBehaviour
    {
        public bool deadlockHint = true;

        [SerializeField] private GamePalette _palette;
        [SerializeField] private GameObject _playerPrefab;
        private GameObject _tipBarPrefab;

        private const string PalettePath = "SokoBan/GamePalette";
        private const string PlayerPrefabPath = "SokoBan/Player";
        private const string TipBarPrefabPath = "SokoBan/TipBar";

        private GamePalette Palette
        {
            get
            {
                if (_palette == null) _palette = Resources.Load<GamePalette>(PalettePath);
                return _palette;
            }
        }

        private GameObject PlayerPrefab
        {
            get
            {
                if (_playerPrefab == null) _playerPrefab = Resources.Load<GameObject>(PlayerPrefabPath);
                return _playerPrefab;
            }
        }

        private GameObject TipBarPrefab
        {
            get
            {
                if (_tipBarPrefab == null) _tipBarPrefab = Resources.Load<GameObject>(TipBarPrefabPath);
                return _tipBarPrefab;
            }
        }

        private Game _game;
        private Camera _cam;
        private Light _sun;
        private Transform _staticGroup;
        private Transform _dynamicGroup;

        private BoardCameraRig _camRig;
        private readonly TweenRunner _tweens = new TweenRunner();
        private readonly Dictionary<int, Transform> _boxMeshes = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Material> _boxMats = new Dictionary<int, Material>();
        private readonly Dictionary<int, Vector2> _boxRendered = new Dictionary<int, Vector2>();
        private readonly Dictionary<(int r, int c), GameObject> _gateMeshes = new Dictionary<(int r, int c), GameObject>();
        private readonly Dictionary<(int r, int c), GameObject> _voidTiles = new Dictionary<(int r, int c), GameObject>();
        private Material _rockMat;

        private Transform _playerMesh;
        private Transform _playerBody;
        private Vector2 _playerRendered;
        private float _playerYawTarget;   // 度
        private bool _playerMoving;

        private void Awake()
        {
            BuildEnvironment();
            _camRig = new BoardCameraRig(_cam);
        }

        private void Update()
        {
            _tweens.Tick(Time.time);
            AnimateCharacter(Time.time);
        }

        /* ---------- 环境：相机 / 光照 / 雾 / 水面 ---------- */
        private void BuildEnvironment()
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = Palette.background;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = Palette.fogStart;
            RenderSettings.fogEndDistance = Palette.fogEnd;

            // 半球光（Trilight 环境光近似）+ 低强度环境
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.white;
            RenderSettings.ambientEquatorColor = Color.Lerp(Color.white, Palette.warmGround, 0.5f);
            RenderSettings.ambientGroundColor = Palette.warmGround;
            RenderSettings.ambientIntensity = Palette.ambientIntensity;

            // 太阳（主方向光）
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            _sun = sunGo.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.color = Palette.sunColor;
            _sun.intensity = Palette.sunIntensity;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowCustomResolution = 2048;
            _sun.shadowBias = 0.05f;
            _sun.shadowNormalBias = 0.02f;
            _sun.shadowNearPlane = 1f;

            // 相机（优先复用 MainCamera，否则新建）
            _cam = Camera.main;
            if (_cam == null)
            {
                var camGo = new GameObject("Board Camera");
                camGo.tag = "MainCamera";
                camGo.transform.SetParent(transform, false);
                _cam = camGo.AddComponent<Camera>();
            }
            _cam.fieldOfView = Palette.cameraFov;
            _cam.nearClipPlane = Palette.cameraNear;
            _cam.farClipPlane = Palette.cameraFar;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Palette.background;

            // 水面
            var water = MakePrimitive(PrimitiveType.Plane, "Water", transform);
            water.transform.localScale = new Vector3(Palette.waterSize, 1f, Palette.waterSize); // 10 → 130
            water.transform.localPosition = new Vector3(0, Palette.waterY, 0);
            var wmr = water.GetComponent<MeshRenderer>();
            wmr.sharedMaterial = Primitives.Standard(Palette.waterColor, 0.25f, 0.1f);
            wmr.receiveShadows = true;
        }

        /* ---------- 关卡构建 ---------- */
        public void SetGame(Game game, bool noAnim = false)
        {
            _game = game;
            _tweens.Clear();
            EnsureGroups();
            ClearChildren(_staticGroup);
            ClearChildren(_dynamicGroup);
            _boxMeshes.Clear();
            _boxMats.Clear();
            _boxRendered.Clear();
            _gateMeshes.Clear();
            _voidTiles.Clear();

            BuildStatic();
            BuildDynamic(noAnim);

            float diag = Mathf.Sqrt(game.Cols * game.Cols + game.Rows * game.Rows);
            _camRig.Refit(ContentBounds());
            SetSunBounds(diag);
        }

        private Vector3 CellToWorld(int r, int c)
        {
            // x 取反以纠正 Unity（左手系）LookAt 相对 Three.js（右手系）的左右镜像：
            // 使「列号增大 = 屏幕向右」，与网页版一致。
            return new Vector3((_game.Cols - 1) / 2f - c, 0f, r - (_game.Rows - 1) / 2f);
        }

        private void BuildStatic()
        {
            var g = _game;

            var island = MakePrimitive(PrimitiveType.Cube, "Island", _staticGroup);
            island.transform.localScale = new Vector3(g.Cols + Palette.islandPad, Palette.islandDepth, g.Rows + Palette.islandPad);
            island.transform.localPosition = new Vector3(0, Palette.islandY, 0);
            var imr = island.GetComponent<MeshRenderer>();
            imr.sharedMaterial = Primitives.Standard(Palette.islandColor, 0.9f);
            imr.receiveShadows = true;

            var floorA = Primitives.Standard(Palette.floorA, 0.9f);
            var floorB = Primitives.Standard(Palette.floorB, 0.9f);
            var goalMat = Primitives.Standard(Palette.goalColor, 0.5f);
            var goalMesh = Primitives.Cylinder(0.34f, 0.34f, 0.02f, 24);
            var triggerMat = Primitives.Standard(Palette.triggerColor, 0.5f);
            var triggerMesh = Primitives.Cylinder(0.3f, 0.3f, 0.02f, 24);
            var gateMat = Primitives.Standard(Palette.gateColor, 0.7f);
            _rockMat = Primitives.Standard(Palette.rockColor, 0.85f);

            for (int r = 0; r < g.Rows; r++)
            {
                for (int c = 0; c < g.Cols; c++)
                {
                    var w = CellToWorld(r, c);
                    bool isGate = g.IsGate(r, c);
                    bool isWall = !isGate && g.IsSolidWall(r, c);

                    if (isWall)
                    {
                        int ci = r * g.Cols + c;
                        var color = Palette.wallColors[(int)(Easing.Seeded(ci) * Palette.wallColors.Length)];
                        float h = Palette.wallMinHeight + Easing.Seeded(ci + 9) * Palette.wallHeightVariation;

                        var wall = MakePrimitive(PrimitiveType.Cube, "Wall", _staticGroup);
                        wall.transform.localScale = new Vector3(0.9f, h, 0.9f);
                        wall.transform.localPosition = new Vector3(w.x, h / 2f, w.z);
                        wall.transform.localRotation = Quaternion.Euler(0f, (Easing.Seeded(ci + 5) - 0.5f) * 0.1f * Mathf.Rad2Deg, 0f);
                        var wmr = wall.GetComponent<MeshRenderer>();
                        wmr.sharedMaterial = Primitives.Standard(color, 0.75f);
                        wmr.shadowCastingMode = ShadowCastingMode.On;
                        wmr.receiveShadows = true;
                        continue;
                    }

                    // 地板（地面 / 目标 / 触发器 / 被触发块下方均铺地板；空位也预铺但隐藏，待吸力填充后显示）
                    var tile = MakePrimitive(PrimitiveType.Cube, "Tile", _staticGroup);
                    tile.transform.localScale = new Vector3(0.96f, Palette.floorTop, 0.96f);
                    tile.transform.localPosition = new Vector3(w.x, Palette.floorTop * 0.5f, w.z);
                    var tmr = tile.GetComponent<MeshRenderer>();
                    tmr.sharedMaterial = ((r + c) % 2 != 0) ? floorB : floorA;
                    tmr.receiveShadows = true;

                    if (g.IsVoid(r, c))
                    {
                        tile.SetActive(false); // 空：坑洞，地板暂时隐藏（露出浮岛）
                        _voidTiles[(r, c)] = tile;
                        continue;
                    }

                    // 提示块（i）：可自由通行，立一块白色提示牌
                    if (g.IsTip(r, c) && TipBarPrefab != null)
                    {
                        var tip = Instantiate(TipBarPrefab, _staticGroup);
                        tip.name = "TipBar_" + r + "_" + c;
                        tip.transform.localPosition = new Vector3(w.x - 0.3f, 0.6f, w.z - 0.3f);
                    }

                    if (isGate)
                    {
                        var gate = MakePrimitive(PrimitiveType.Cube, "Gate", _staticGroup);
                        gate.transform.localScale = new Vector3(0.86f, 0.5f, 0.86f);
                        gate.transform.localPosition = new Vector3(w.x, 0.25f, w.z);
                        var gmr = gate.GetComponent<MeshRenderer>();
                        gmr.sharedMaterial = gateMat;
                        gmr.shadowCastingMode = ShadowCastingMode.On;
                        gmr.receiveShadows = true;
                        _gateMeshes[(r, c)] = gate;
                    }
                    else
                    {
                        if (g.IsGoal(r, c))
                        {
                            var goal = MakeMesh("Goal", goalMesh, goalMat, _staticGroup);
                            goal.transform.localPosition = new Vector3(w.x, Palette.floorTop + 0.01f, w.z);
                            goal.GetComponent<MeshRenderer>().receiveShadows = true;
                        }
                        if (g.IsTrigger(r, c))
                        {
                            var trigger = MakeMesh("Trigger", triggerMesh, triggerMat, _staticGroup);
                            trigger.transform.localPosition = new Vector3(w.x, Palette.floorTop + 0.012f, w.z);
                            trigger.GetComponent<MeshRenderer>().receiveShadows = true;
                        }
                    }
                }
            }

            UpdateGates();
        }

        private void BuildDynamic(bool noAnim)
        {
            var g = _game;
            int i = 0;

            foreach (var b in g.Boxes)
            {
                var box = BuildBox();
                var w = CellToWorld(b.R, b.C);
                box.transform.localPosition = new Vector3(w.x, Palette.floorTop + Palette.boxHeight / 2f, w.z);
                _boxMeshes[b.Id] = box.transform;
                _boxMats[b.Id] = box.GetComponent<MeshRenderer>().sharedMaterial;
                _boxRendered[b.Id] = new Vector2(w.x, w.z);
                if (!noAnim) PopIn(box.transform, i * 0.05f);
                i++;
            }

            _playerMesh = BuildPlayer();
            var pw = CellToWorld(g.Player.r, g.Player.c);
            _playerMesh.localPosition = new Vector3(pw.x, Palette.floorTop, pw.z);
            _playerMesh.localRotation = Quaternion.identity;
            _playerRendered = new Vector2(pw.x, pw.z);
            _playerYawTarget = 0f;
            if (!noAnim) PopIn(_playerMesh, i * 0.05f);

            UpdateBoxStates();
        }

        private GameObject BuildBox()
        {
            var box = MakePrimitive(PrimitiveType.Cube, "Box", _dynamicGroup);
            box.transform.localScale = new Vector3(0.72f, Palette.boxHeight, 0.72f);
            var mr = box.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Primitives.Standard(Palette.boxColor, 0.6f, 0.05f);
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            return box;
        }

        // 确保指定木箱有网格：若已被下沉消耗而销毁，则重建（撤销/重做/重开时恢复显示）。
        private void EnsureBoxMesh(Box b)
        {
            if (_boxMeshes.ContainsKey(b.Id)) return;
            var box = BuildBox();
            var w = CellToWorld(b.R, b.C);
            box.transform.localPosition = new Vector3(w.x, Palette.floorTop + Palette.boxHeight / 2f, w.z);
            _boxMeshes[b.Id] = box.transform;
            _boxMats[b.Id] = box.GetComponent<MeshRenderer>().sharedMaterial;
            _boxRendered[b.Id] = new Vector2(w.x, w.z);
        }

        // 实例化玩家预制体（结构/网格/材质来自 Assets/Resources/SokoBan/Player.prefab）。
        private Transform BuildPlayer()
        {
            var prefab = PlayerPrefab;
            if (prefab == null)
            {
                Debug.LogError("[Board3DRenderer] 找不到玩家预制体 " + PlayerPrefabPath + "，请运行菜单 SokoBan/生成资源。");
                var holder = new GameObject("Player").transform;
                holder.SetParent(_dynamicGroup, false);
                _playerBody = new GameObject("Body").transform;
                _playerBody.SetParent(holder, false);
                return holder;
            }

            var go = Instantiate(prefab, _dynamicGroup);
            go.name = "Player";
            _playerBody = go.transform.Find("Body");
            return go.transform;
        }

        /* ---------- 同步（移动动画） ---------- */
        // 朝向 → 角色偏航角（度）。与 CellToWorld 的镜像约定一致：Down=0°、Right=-90°、Up=180°、Left=90°。
        private static float YawFor(Dir d)
        {
            switch (d)
            {
                case Dir.Up: return 180f;
                case Dir.Down: return 0f;
                case Dir.Left: return 90f;
                case Dir.Right: return -90f;
                default: return 0f;
            }
        }

        public void Sync()
        {
            var g = _game;
            if (g == null) return;

            _playerYawTarget = YawFor(g.Facing); // 朝向（即使被阻挡也更新）
            RefreshVoids();

            var pw = CellToWorld(g.Player.r, g.Player.c);
            if (_playerRendered.x != pw.x || _playerRendered.y != pw.z)
                MovePlayer(pw);

            foreach (var b in g.Boxes)
            {
                EnsureBoxMesh(b); // 撤销/重做恢复被下沉消耗的木箱时，重建其网格
                var w = CellToWorld(b.R, b.C);
                if (!_boxRendered.TryGetValue(b.Id, out var rend) || rend.x != w.x || rend.y != w.z)
                    MoveBox(b.Id, w);
            }

            RemoveConsumedBoxes();

            UpdateBoxStates();
            UpdateGates();
        }

        private void MovePlayer(Vector3 w)
        {
            _playerMoving = true;
            float startX = _playerMesh.localPosition.x;
            float startZ = _playerMesh.localPosition.z;

            _tweens.AddTween(0.18f, e =>
            {
                var p = _playerMesh.localPosition;
                p.x = startX + (w.x - startX) * e;
                p.z = startZ + (w.z - startZ) * e;
                p.y = Palette.floorTop + Mathf.Sin(e * Mathf.PI) * 0.13f;
                _playerMesh.localPosition = p;
            }, () =>
            {
                var p = _playerMesh.localPosition;
                p.y = Palette.floorTop;
                _playerMesh.localPosition = p;
                _playerMoving = false;
            });

            _playerRendered = new Vector2(w.x, w.z);
        }

        private void MoveBox(int id, Vector3 w)
        {
            if (!_boxMeshes.TryGetValue(id, out var mesh)) return;
            float startX = mesh.localPosition.x;
            float startZ = mesh.localPosition.z;
            var baseScale = new Vector3(0.72f, Palette.boxHeight, 0.72f);

            _tweens.AddTween(0.2f, e =>
            {
                var p = mesh.localPosition;
                p.x = startX + (w.x - startX) * e;
                p.z = startZ + (w.z - startZ) * e;
                mesh.localPosition = p;
                mesh.localScale = baseScale * (1f + Mathf.Sin(e * Mathf.PI) * 0.05f);
            }, () => mesh.localScale = baseScale);

            _boxRendered[id] = new Vector2(w.x, w.z);
        }

        // 清理已下沉填坑（从木箱列表消失）的箱子网格。
        private void RemoveConsumedBoxes()
        {
            var alive = new HashSet<int>();
            foreach (var b in _game.Boxes) alive.Add(b.Id);

            var gone = new List<int>();
            foreach (var id in _boxMeshes.Keys) if (!alive.Contains(id)) gone.Add(id);

            foreach (var id in gone)
            {
                var mesh = _boxMeshes[id];
                if (mesh != null)
                    DestroySafe(mesh.gameObject);
                _boxMeshes.Remove(id);
                _boxMats.Remove(id);
                _boxRendered.Remove(id);
            }
        }

        private void UpdateBoxStates()
        {
            var g = _game;
            if (g == null) return;
            var dead = deadlockHint ? g.DeadlockedBoxes() : null;

            for (int i = 0; i < g.Boxes.Count; i++)
            {
                var b = g.Boxes[i];
                if (!_boxMats.TryGetValue(b.Id, out var mat)) continue;
                if (b.Id == g.HeldBoxId) Primitives.SetEmissive(mat, Palette.heldColor, 0.55f);
                else if (g.IsGoal(b.R, b.C)) Primitives.SetEmissive(mat, Palette.boxOnGoal, 0.5f);
                else if (dead != null && dead.Contains(i)) Primitives.SetEmissive(mat, Palette.boxDead, 0.55f);
                else Primitives.SetEmissive(mat, Color.black, 0f);
            }
        }

        // 按机关状态显隐每个被触发块挡板（一对一：对应触发器有箱子即收起，转为可行走地板）。
        private void UpdateGates()
        {
            if (_game == null) return;
            foreach (var kv in _gateMeshes)
                if (kv.Value != null) kv.Value.SetActive(!_game.IsGateOpen(kv.Key.r, kv.Key.c));
        }

        // 同步坑洞地板：木箱下沉填平时显示为特殊岩面路面，撤销恢复为空时隐藏。
        private void RefreshVoids()
        {
            if (_game == null) return;
            foreach (var kv in _voidTiles)
            {
                if (kv.Value == null) continue;
                bool filled = !_game.IsVoid(kv.Key.r, kv.Key.c);
                if (filled)
                {
                    if (!kv.Value.activeSelf) kv.Value.SetActive(true);
                    var mr = kv.Value.GetComponent<MeshRenderer>();
                    if (mr != null && mr.sharedMaterial != _rockMat) mr.sharedMaterial = _rockMat;
                }
                else if (kv.Value.activeSelf)
                {
                    kv.Value.SetActive(false);
                }
            }
        }

        /* ---------- 补间 / 角色动画 ---------- */
        private void PopIn(Transform t, float delay)
        {
            var baseScale = t.localScale;
            t.localScale = baseScale * 0.01f;
            _tweens.AddTween(0.35f, e => t.localScale = baseScale * Easing.EaseOutBack(e), null, delay);
        }

        private void AnimateCharacter(float sec)
        {
            if (_playerMesh == null) return;

            float cur = _playerMesh.localEulerAngles.y;
            _playerMesh.localEulerAngles = new Vector3(0f, cur + Mathf.DeltaAngle(cur, _playerYawTarget) * 0.3f, 0f);

            if (_playerBody != null)
            {
                float amp = _playerMoving ? 0.04f : 0.015f;
                float freq = _playerMoving ? 16f : 2f;
                var p = _playerBody.localPosition;
                p.y = Mathf.Sin(sec * freq) * amp;
                _playerBody.localPosition = p;

                float lean = _playerMoving ? 0.07f * Mathf.Rad2Deg : 0f;
                var rot = _playerBody.localEulerAngles;
                rot.x += (lean - rot.x) * 0.3f;
                _playerBody.localEulerAngles = rot;
            }
        }

        /* ---------- 相机自适应 ---------- */
        private Bounds ContentBounds()
        {
            float pad = 1.35f;
            float hx = (_game.Cols - 1) / 2f + pad;
            float hz = (_game.Rows - 1) / 2f + pad;
            var b = new Bounds();
            b.SetMinMax(new Vector3(-hx, -0.5f, -hz), new Vector3(hx, 0.8f, hz));
            return b;
        }

        private void SetSunBounds(float diag)
        {
            _sun.transform.position = new Vector3(diag * 0.7f, diag * 1.1f, diag * 0.6f);
            _sun.transform.rotation = Quaternion.LookRotation(-_sun.transform.position.normalized);
        }

        /* ---------- 工具 ---------- */
        private void EnsureGroups()
        {
            if (_staticGroup == null)
            {
                _staticGroup = new GameObject("Static").transform;
                _staticGroup.SetParent(transform, false);
            }
            if (_dynamicGroup == null)
            {
                _dynamicGroup = new GameObject("Dynamic").transform;
                _dynamicGroup.SetParent(transform, false);
            }
        }

        private static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                DestroySafe(t.GetChild(i).gameObject);
        }

        private static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        private static GameObject MakePrimitive(PrimitiveType type, string name, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) DestroySafe(col);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject MakeMesh(string name, Mesh mesh, Material mat, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }
    }
}
