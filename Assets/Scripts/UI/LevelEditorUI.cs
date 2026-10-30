using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SokoBan.App;
using SokoBan.Persistence;

namespace SokoBan.UI
{
    // 关卡编辑器门面：构建画布、装配各子组件（数据模型 / 网格视图 / 测试试玩 / 关卡列表），
    /// 并把输入事件与按钮转发给对应组件。
    public class LevelEditorUI : MonoBehaviour
    {
        private const int Rows = 20, Cols = 20; // 大画布（视为“无限”空网格）

        private GameController _controller;
        private Font _font;

        private GameObject _editorRoot;
        private Image _editorBg;
        private GameObject _editPanel;
        private RectTransform _gridContent;
        private Text _statusText;
        private Button _saveBtn;
        private Button _undoBtn, _redoBtn;
        private GameObject _confirmModal;
        private Action _confirmCb;

        // 子组件
        private EditorDocument _doc;
        private EditorGridView _view;
        private EditorTestMode _test;

        // 编辑态
        private string _tool = "wall";
        private readonly Dictionary<string, Button> _toolButtons = new Dictionary<string, Button>();
        private readonly Color _toolNormalColor = Color.white;
        private readonly Color _toolSelectedColor = new Color(0.32f, 0.64f, 0.56f, 1f); // 选中的工具按钮高亮色
        private string _editingId;
        private string _editingName;
        private bool _dirty;
        private bool _painting;
        private bool _panning;
        private Vector2 _lastPanMouse;
        private bool _middleDown;              // 中键按下
        private bool _middleMoved;             // 中键按下后是否发生拖拽（区分点击选中与平移）
        private int _middleCellR = -1, _middleCellC = -1; // 中键按下时的格子
        private Vector2 _middleDownPos;

        // 连线拖拽手势：在触发器上按下，拖到被触发块上松开
        private bool _linking;
        private int _linkStartR, _linkStartC, _linkEndR, _linkEndC;

        // 选中模式：中键点击选中格 + 提示块说明输入框
        private int _selR = -1, _selC = -1;
        private GameObject _tipEditor;
        private InputField _tipInput;
        private int _tipEditR = -1, _tipEditC = -1; // 输入框当前编辑的提示块坐标

        private Transform _canvasRoot;

        public bool IsTesting() => _test != null && _test.IsTesting;

        public void Init(GameController c)
        {
            _controller = c;
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);

            _doc = new EditorDocument(Rows, Cols);
            _view = new EditorGridView(this);
            _test = new EditorTestMode(c, SetStatus, StopTest);

            BuildCanvas();
            LoadPrefab();

            _editorRoot.SetActive(false);
            ResetGrid();
        }

        // 中键拖拽平移网格（Tilemap 式导航）；中键按下未拖拽则视为选中。
        // 用 Input.mousePosition 差值，避免 GetAxis 平滑/灵敏度导致拖不动。
        private void Update()
        {
            if (_editorRoot == null || !_editorRoot.activeSelf) return;
            if (_test == null || _test.IsTesting) return;

            if (Input.GetMouseButtonDown(2))
            {
                _middleDown = true;
                _middleMoved = false;
                _middleDownPos = Input.mousePosition;
                _panning = true;
                _lastPanMouse = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(2))
            {
                _panning = false;
                // 中键抬起且中途未移动 => 视为点击选中
                if (_middleDown && !_middleMoved && _middleCellR >= 0)
                    Select(_middleCellR, _middleCellC);
                _middleDown = false;
                _middleCellR = _middleCellC = -1;
            }
            if (_panning && _gridContent != null)
            {
                Vector2 cur = Input.mousePosition;
                Vector2 delta = cur - _lastPanMouse;
                _lastPanMouse = cur;
                if ((cur - _middleDownPos).sqrMagnitude > 16f) _middleMoved = true; // 移动超过约 4px 视为拖拽
                float scale = _canvasRoot.GetComponent<Canvas>().scaleFactor;
                if (scale > 0.0001f)
                    _gridContent.anchoredPosition += new Vector2(delta.x / scale, delta.y / scale);
            }
        }

        /* ================= 画布 ================= */
        private void BuildCanvas()
        {
            var canvasGo = new GameObject("EditorCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            _canvasRoot = canvasGo.transform;
        }

        /* ================= 加载并绑定 ================= */
        private void LoadPrefab()
        {
            _editorRoot = Instantiate(Resources.Load<GameObject>("UI/EditorScreen"), _canvasRoot, false);
            foreach (var t in _editorRoot.GetComponentsInChildren<Text>(true)) t.font = _font;
            _editorBg = _editorRoot.GetComponent<Image>();
            _editPanel = _editorRoot.transform.Find("EditPanel").gameObject;
            var testHud = _editorRoot.transform.Find("TestHud").gameObject;

            UiUtil.Find<Button>(_editorRoot, "EditPanel/BackBtn").onClick.AddListener(() => ConfirmDiscard(() => _controller.ShowHome()));

            BindTool("ToolWall", "wall");
            BindTool("ToolGoal", "goal");
            BindTool("ToolBox", "box");
            BindTool("ToolPlayer", "player");
            BindTool("ToolTrigger", "trigger");
            BindTool("ToolGate", "gate");
            SetupTileTools();
            BuildTipEditor();

            _undoBtn = UiUtil.Find<Button>(_editorRoot, "EditPanel/UndoBtn");
            _redoBtn = UiUtil.Find<Button>(_editorRoot, "EditPanel/RedoBtn");
            var testBtn = UiUtil.Find<Button>(_editorRoot, "EditPanel/TestBtn");
            _saveBtn = UiUtil.Find<Button>(_editorRoot, "EditPanel/SaveBtn");
            _undoBtn.onClick.AddListener(UndoEdit);
            _redoBtn.onClick.AddListener(RedoEdit);
            testBtn.onClick.AddListener(() => { if (_test.IsTesting) StopTest(); else StartTest(); });
            _saveBtn.onClick.AddListener(Save);

            _gridContent = UiUtil.Find<RectTransform>(_editorRoot, "EditPanel/GridViewport/Grid");
            _statusText = UiUtil.Find<Text>(_editorRoot, "EditPanel/Status");

            _confirmModal = _editorRoot.transform.Find("ConfirmModal").gameObject;
            UiUtil.Find<Button>(_editorRoot, "ConfirmModal/CancelBtn").onClick.AddListener(() => { _confirmModal.SetActive(false); _confirmCb = null; });
            UiUtil.Find<Button>(_editorRoot, "ConfirmModal/OkBtn").onClick.AddListener(() => { _confirmModal.SetActive(false); var cb = _confirmCb; _confirmCb = null; cb?.Invoke(); });

            // 装配子组件
            _view.Attach(Resources.Load<GameObject>("UI/EditorCell"), _gridContent, Rows, Cols);
            _test.Attach(testHud);
        }

        private void BindTool(string name, string tool)
        {
            var btn = UiUtil.Find<Button>(_editorRoot, "EditPanel/TileScroll/Viewport/Content/" + name);
            btn.onClick.AddListener(() => { _controller.PlayClick(); SelectTool(tool); });
            _toolButtons[tool] = btn;
        }

        // 选中某个绘制工具，并把对应按钮高亮、其余按钮恢复默认色。
        private void SelectTool(string tool)
        {
            _tool = tool;
            foreach (var pair in _toolButtons)
            {
                var colors = pair.Value.colors;
                colors.normalColor = pair.Key == tool ? _toolSelectedColor : _toolNormalColor;
                pair.Value.colors = colors;
            }
        }

        // 把「空地」按钮拆成两个：空（画空格，无内容）与 地面（画 '-'，有内容的地板瓦片）。
        /// 通过运行时克隆避免手改 prefab 的 fileID 引用。
        private void SetupTileTools()
        {
            var content = UiUtil.Find<RectTransform>(_editorRoot, "EditPanel/TileScroll/Viewport/Content");
            var emptyGo = UiUtil.Find<Button>(_editorRoot, "EditPanel/TileScroll/Viewport/Content/ToolFloor").gameObject;

            var floorGo = Instantiate(emptyGo, content, false);
            floorGo.name = "ToolFloor";
            SetToolLabel(floorGo, "地面");

            emptyGo.name = "ToolEmpty";
            SetToolLabel(emptyGo, "空");

            emptyGo.GetComponent<Button>().onClick.AddListener(() => { _controller.PlayClick(); SelectTool("empty"); });
            floorGo.GetComponent<Button>().onClick.AddListener(() => { _controller.PlayClick(); SelectTool("floor"); });
            _toolButtons["empty"] = emptyGo.GetComponent<Button>();
            _toolButtons["floor"] = floorGo.GetComponent<Button>();

            var tipGo = Instantiate(floorGo, content, false);
            tipGo.name = "ToolTip";
            SetToolLabel(tipGo, "提示");
            tipGo.GetComponent<Button>().onClick.AddListener(() => { _controller.PlayClick(); SelectTool("tip"); });
            _toolButtons["tip"] = tipGo.GetComponent<Button>();

            // 9 个图块按钮从左到右重新排布；收窄到 150 宽避免横向滚动
            string[] names = { "ToolWall", "ToolEmpty", "ToolFloor", "ToolGoal", "ToolBox", "ToolPlayer", "ToolTrigger", "ToolGate", "ToolTip" };
            const float step = 160f;
            float x = -step * (names.Length - 1) / 2f;
            for (int i = 0; i < names.Length; i++)
            {
                var rt = UiUtil.Find<RectTransform>(_editorRoot, "EditPanel/TileScroll/Viewport/Content/" + names[i]);
                rt.sizeDelta = new Vector2(150f, rt.sizeDelta.y);
                rt.anchoredPosition = new Vector2(x + i * step, 0f);
            }

            if (content != null)
                content.sizeDelta = new Vector2(step * (names.Length - 1) + 150f, content.sizeDelta.y);

            SelectTool(_tool); // 默认工具初始高亮
        }

        private void SetToolLabel(GameObject btn, string text)
        {
            var label = btn.transform.Find("Label");
            if (label == null) return;
            var t = label.GetComponent<Text>();
            t.text = text;
            if (_font != null) t.font = _font;
        }

        // 提示块说明输入框（悬浮面板），运行时构建，无需改 prefab。
        private void BuildTipEditor()
        {
            var go = new GameObject("TipEditor");
            go.transform.SetParent(_editPanel.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -12f);
            rt.sizeDelta = new Vector2(560f, 64f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.10f, 0.12f, 0.14f, 0.94f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(0.22f, 1f);
            labelRt.offsetMin = new Vector2(12f, 0f);
            labelRt.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<Text>();
            label.text = "提示说明";
            label.font = _font;
            label.fontSize = 20;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;

            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(go.transform, false);
            var inputRt = inputGo.AddComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0.24f, 0.15f);
            inputRt.anchorMax = new Vector2(1f, 0.85f);
            inputRt.offsetMin = Vector2.zero;
            inputRt.offsetMax = new Vector2(-12f, 0f);
            var inputBg = inputGo.AddComponent<Image>();
            inputBg.color = new Color(0.96f, 0.97f, 0.97f);

            var input = inputGo.AddComponent<InputField>();
            input.targetGraphic = inputBg;
            input.lineType = InputField.LineType.SingleLine;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(inputGo.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(12f, 0f);
            textRt.offsetMax = new Vector2(-12f, 0f);
            var text = textGo.AddComponent<Text>();
            text.font = _font;
            text.fontSize = 20;
            text.color = new Color(0.10f, 0.10f, 0.10f);
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            input.textComponent = text;

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(inputGo.transform, false);
            var phRt = phGo.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(12f, 0f);
            phRt.offsetMax = new Vector2(-12f, 0f);
            var ph = phGo.AddComponent<Text>();
            ph.text = "填入中文说明（走到此格时弹出）";
            ph.font = _font;
            ph.fontSize = 20;
            ph.fontStyle = FontStyle.Italic;
            ph.color = new Color(0.5f, 0.5f, 0.5f);
            ph.alignment = TextAnchor.MiddleLeft;
            input.placeholder = ph;

            _tipInput = input;
            _tipEditor = go;
            go.SetActive(false);

            input.onEndEdit.AddListener(_ => CommitTipEdit());
        }

        /* ================= 网格绘制 ================= */
        public void OnCellDown(int r, int c, bool right)
        {
            if (_test.IsTesting) return;
            if (right) { ResetSelection(true); Paint(r, c, true); return; } // 右键：清空该格
            ResetSelection(true); // 左键绘制：取消选中
            // 仅当用「触发器/机关门」工具在两者上按下时才进入连线模式（不绘制）；
            // 其它工具（如「空」）左键按正常绘制/覆盖处理，从而能清除机关块。
            char ch = _doc.At(r, c);
            if ((ch == '!' || ch == '%') && (_tool == "trigger" || _tool == "gate"))
            {
                _linking = true;
                _linkStartR = r; _linkStartC = c;
                _linkEndR = r; _linkEndC = c;
                return;
            }
            _painting = true;
            Paint(r, c, false);
        }

        // 中键按下：记录格子，供 Update 在中键抬起时判断是点击选中还是平移。
        public void OnCellMiddleDown(int r, int c)
        {
            if (_test.IsTesting) return;
            _middleCellR = r;
            _middleCellC = c;
            _middleDownPos = Input.mousePosition;
        }

        public void OnCellEnter(int r, int c)
        {
            if (_linking) { _linkEndR = r; _linkEndC = c; return; }
            if (_painting && !_test.IsTesting) Paint(r, c, false);
        }

        public void OnCellUp()
        {
            if (_linking)
            {
                _linking = false;
                TryCreateLink(_linkStartR, _linkStartC, _linkEndR, _linkEndC);
            }
            _painting = false;
        }

        // 从起点拖到终点：触发器 → 被触发块（或反向）则建立一对一关联。
        private void TryCreateLink(int sr, int sc, int er, int ec)
        {
            if (sr == er && sc == ec) return;
            char sc_ = _doc.At(sr, sc);
            char ec_ = _doc.At(er, ec);
            if (sc_ == '!' && ec_ == '%') AddLink(sr, sc, er, ec);
            else if (sc_ == '%' && ec_ == '!') AddLink(er, ec, sr, sc);
        }

        private void AddLink(int tr, int tc, int gr, int gc)
        {
            _doc.AddLink(tr, tc, gr, gc);
            _view.RenderLinks(_doc.Links);
            SetDirty(true);
        }

        private void Paint(int r, int c, bool erase)
        {
            var (cells, linksChanged) = _doc.Paint(r, c, _tool, erase);
            foreach (var (cr, cc) in cells) _view.UpdateCell(cr, cc, _doc.At(cr, cc));
            if (linksChanged) _view.RenderLinks(_doc.Links);
            SetDirty(true);
        }

        /* ================= 选中 / 提示说明 ================= */
        // 中键点击选中格子：黑色边框高亮；若为提示块则弹出说明输入框。
        private void Select(int r, int c)
        {
            CommitTipEdit(); // 先把上一个输入框的内容落盘
            _selR = r; _selC = c;
            _view.SetSelection(r, c);
            _controller.PlayClick();
            UpdateTipEditor();
        }

        // 重置选中态；commit 为真时先把未提交的说明落盘。
        private void ResetSelection(bool commit)
        {
            if (commit) CommitTipEdit();
            _selR = -1; _selC = -1;
            _view.ClearSelection();
            HideTipEditor();
        }

        // 把输入框当前内容写入其绑定的提示块；无变化则不记快照。
        private void CommitTipEdit()
        {
            if (_tipInput == null || _tipEditR < 0) return;
            string text = _tipInput.text;
            if (_doc.TipText(_tipEditR, _tipEditC) == text) return;
            _doc.SetTipText(_tipEditR, _tipEditC, text);
            SetDirty(true);
        }

        // 根据当前选中格显示 / 隐藏说明输入框。
        private void UpdateTipEditor()
        {
            if (_doc.IsTip(_selR, _selC))
            {
                _tipEditR = _selR; _tipEditC = _selC;
                _tipInput.text = _doc.TipText(_selR, _selC);
                _tipEditor.SetActive(true);
                _tipInput.ActivateInputField();
            }
            else HideTipEditor();
        }

        private void HideTipEditor()
        {
            _tipEditR = -1; _tipEditC = -1;
            if (_tipEditor != null) _tipEditor.SetActive(false);
        }

        /* ================= 清空 ================= */
        private void ResetGrid()
        {
            _doc.Reset();
            _view.Render(_doc);
            ResetSelection(false);
        }

        /* ================= 保存 ================= */
        private void Save()
        {
            CommitTipEdit();
            if (!_doc.Validate(out _, out var err)) { SetStatus(err, true); _controller.Audio.Error(); return; }
            string id = _editingId ?? ("lv-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "-" + UnityEngine.Random.Range(1000, 9999));
            string name = string.IsNullOrEmpty(_editingName) ? ("关卡 " + (SaveManager.GetLevels().Count + 1)) : _editingName;
            var lv = _doc.BuildLevel(id, name);
            SaveManager.SaveLevel(lv);
            _editingId = lv.id;
            _editingName = lv.name;
            SetStatus("已保存「" + lv.name + "」", false);
            _controller.PlayClick();
            SetDirty(false);
            RefreshUndoRedo();
        }

        /* ================= 测试试玩 ================= */
        private void StartTest()
        {
            CommitTipEdit();
            if (!_doc.Validate(out var level, out var err)) { SetStatus(err, true); _controller.Audio.Error(); return; }
            _test.Start(level);
            _editPanel.SetActive(false);
            if (_editorBg != null) _editorBg.enabled = false;
            SetStatus("测试模式：WASD/方向键移动 · J 吸住 · E 转向 · Esc 返回编辑", false);
        }

        private void StopTest()
        {
            _test.Stop();
            _editPanel.SetActive(true);
            if (_editorBg != null) _editorBg.enabled = true;
        }

        public void HandleTestInput() => _test.HandleInput();

        /* ================= 撤销 / 重做 ================= */
        private void UndoEdit()
        {
            ResetSelection(true); // 提交并收起输入框，随后撤销会撤销最近一次（含刚提交的说明）
            if (!_doc.Undo()) return;
            _view.Render(_doc);
            SetDirty(true);
            RefreshUndoRedo();
            _controller.PlayClick();
        }

        private void RedoEdit()
        {
            ResetSelection(true);
            if (!_doc.Redo()) return;
            _view.Render(_doc);
            SetDirty(true);
            RefreshUndoRedo();
            _controller.PlayClick();
        }

        private void RefreshUndoRedo()
        {
            if (_undoBtn != null) _undoBtn.interactable = _doc.CanUndo;
            if (_redoBtn != null) _redoBtn.interactable = _doc.CanRedo;
        }

        private void SetDirty(bool dirty)
        {
            _dirty = dirty;
            if (_saveBtn != null) _saveBtn.interactable = dirty;
            RefreshUndoRedo(); // 每次编辑都刷新撤销/重做按钮的可用状态
        }

        private void ConfirmDiscard(Action proceed)
        {
            CommitTipEdit();
            if (!_dirty) { proceed(); return; }
            _confirmCb = proceed;
            _confirmModal.SetActive(true);
        }

        /* ================= 载入 / 显示 ================= */
        private void LoadFromRows(string[] rows, string[] links, string[] tips, string name)
        {
            _doc.Load(rows, links, tips);
            _editingName = name ?? "";
            _view.Render(_doc);
            ResetSelection(false);
            _gridContent.anchoredPosition = Vector2.zero; // 重置平移
            SetStatus("已载入关卡", false);
        }

        public void ShowEditor() { _editorRoot.SetActive(true); }

        public void OpenNew()
        {
            if (_test.IsTesting) StopTest();
            _editingId = null;
            _editingName = "";
            ResetGrid();
            _gridContent.anchoredPosition = Vector2.zero; // 重置平移
            _editorRoot.SetActive(true);
            SetDirty(false);
            RefreshUndoRedo();
        }

        public void OpenExisting(CustomLevel lv)
        {
            if (_test.IsTesting) StopTest();
            _editingId = lv.id;
            LoadFromRows(lv.rows, lv.links, lv.tips, lv.name);
            SetStatus("正在编辑「" + lv.name + "」", false);
            _editorRoot.SetActive(true);
            SetDirty(false);
            _doc.ClearHistory();
            RefreshUndoRedo();
        }

        public void HideEditor() { if (_test.IsTesting) StopTest(); ResetSelection(true); _editorRoot.SetActive(false); }

        private void SetStatus(string msg, bool err)
        {
            if (_statusText == null) return;
            _statusText.text = msg ?? "";
            _statusText.color = err ? new Color(0.85f, 0.28f, 0.30f) : new Color(0.3f, 0.4f, 0.4f);
        }
    }
}
