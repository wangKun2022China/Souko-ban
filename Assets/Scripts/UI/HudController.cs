using UnityEngine;
using UnityEngine.UI;
using SokoBan.App;

namespace SokoBan.UI
{
    // 游戏 HUD：关卡名 + 撤销/重做/重开/主页按钮。
    public class HudController
    {
        private readonly GameController _controller;

        private GameObject _root;
        private Button _undoBtn, _redoBtn;
        private GameObject _tipPanel;
        private Text _tipText;

        public HudController(GameController controller)
        {
            _controller = controller;
        }

        public void Attach(GameObject hud)
        {
            _root = hud;
            // _levelText = UiUtil.Find<Text>(_root, "LevelName");
            // 提示弹窗复用 HUD 已有文本的字体（已由 SetFontRecursive 应用全局中文字体）
            var anyText = _root.GetComponentInChildren<Text>();
            BuildTipPanel(anyText != null ? anyText.font : null);

            _undoBtn = UiUtil.Find<Button>(_root, "UndoBtn");
            _redoBtn = UiUtil.Find<Button>(_root, "RedoBtn");
            _undoBtn.onClick.AddListener(() => _controller.Undo());
            _redoBtn.onClick.AddListener(() => _controller.Redo());
            UiUtil.Find<Button>(_root, "RestartBtn").onClick.AddListener(() => _controller.Restart());
            UiUtil.Find<Button>(_root, "HomeBtn").onClick.AddListener(() => _controller.ShowHome());
        }

        public void Show(string levelName)
        {
            _root.SetActive(true);
            // _levelText.text = levelName;
            SetButtons(false, false);
            if (_tipPanel != null) _tipPanel.SetActive(false);
        }

        public void Hide() => _root.SetActive(false);

        // 展示 / 隐藏主角所在提示块的说明（空则隐藏）。
        public void SetTip(string text)
        {
            if (_tipPanel == null) return;
            if (string.IsNullOrEmpty(text)) { _tipPanel.SetActive(false); return; }
            _tipText.text = text;
            _tipPanel.SetActive(true);
        }

        // 提示弹窗（运行时构建，无需改 prefab）。
        private void BuildTipPanel(Font font)
        {
            var go = new GameObject("TipPanel");
            go.transform.SetParent(_root.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 200f);
            rt.sizeDelta = new Vector2(620f, 140f);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.12f, 0.14f, 0.94f);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(go.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.62f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(20f, 0f);
            titleRt.offsetMax = new Vector2(-20f, -4f);
            var title = titleGo.AddComponent<Text>();
            title.text = "提示";
            title.font = font;
            title.fontSize = 24;
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(1f, 0.85f, 0.3f);
            title.alignment = TextAnchor.MiddleLeft;

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(go.transform, false);
            var bodyRt = bodyGo.AddComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 0.62f);
            bodyRt.offsetMin = new Vector2(20f, 8f);
            bodyRt.offsetMax = new Vector2(-20f, 0f);
            var body = bodyGo.AddComponent<Text>();
            body.font = font;
            body.fontSize = 22;
            body.color = Color.white;
            body.alignment = TextAnchor.UpperLeft;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            _tipText = body;

            _tipPanel = go;
            go.SetActive(false);
        }

        public void SetButtons(bool canUndo, bool canRedo)
        {
            _undoBtn.interactable = canUndo;
            _redoBtn.interactable = canRedo;
        }
    }
}
