using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SokoBan.App;
using SokoBan.Core;
using SokoBan.Persistence;

namespace SokoBan.UI
{
    // 主页：关卡列表渲染（经典 + 自定义卡片）。经典与自定义用卡片底色区分；
    // 自定义卡片带「修改 / 删除」，末尾追加「新增关卡」项。
    public class HomeScreen
    {
        private readonly GameController _controller;
        private readonly Font _font;

        private GameObject _root;
        private GameObject _cardPrefab;
        private RectTransform _levelListContent;
        private ScrollRect _levelScroll;

        // 删除确认弹窗（运行时构建）
        private GameObject _confirmModal;
        private Text _confirmText;
        private string _deleteId;

        // 卡片底色 / 数字角标配色
        private static readonly Color ClassicCard = new Color(1f, 0.94f, 0.82f, 1f);  // 暖沙色：内置
        private static readonly Color ClassicBadge = new Color(0.55f, 0.43f, 0.29f, 1f);
        private static readonly Color CustomCard = new Color(0.73f, 0.87f, 0.90f, 1f);  // 青蓝：自定义
        private static readonly Color CustomBadge = new Color(0.20f, 0.45f, 0.50f, 1f);
        private static readonly Color NewCard = new Color(0.88f, 0.93f, 0.82f, 1f);     // 淡绿：新增

        // 缩略图缓存：按关卡内容（ASCII 行拼接）为键复用 Sprite，避免每次刷新重复建纹理泄漏显存。
        private static readonly Dictionary<string, Sprite> MinimapCache = new Dictionary<string, Sprite>();

        public HomeScreen(GameController controller, Font font)
        {
            _controller = controller;
            _font = font;
        }

        public void Attach(GameObject home, GameObject cardPrefab)
        {
            _root = home;
            _cardPrefab = cardPrefab;

            // 去掉右上角「关卡编辑器」入口（改由列表末尾「新增关卡」进入）。
            var editorBtn = _root.transform.Find("EditorBtn");
            if (editorBtn != null) editorBtn.gameObject.SetActive(false);

            _levelScroll = UiUtil.Find<ScrollRect>(_root, "LevelScroll");
            _levelListContent = UiUtil.Find<RectTransform>(_root, "LevelScroll/Viewport/Content");

            BuildConfirmModal();
        }

        public void Show()
        {
            _root.SetActive(true);
            RenderLevels();
        }

        public void Hide() => _root.SetActive(false);

        private void RenderLevels()
        {
            if (_levelListContent == null) return;
            for (int i = _levelListContent.childCount - 1; i >= 0; i--)
                Object.Destroy(_levelListContent.GetChild(i).gameObject);

            int num = 1;
            for (int i = 0; i < Levels.All.Count; i++) { AddClassicNode(i, num); num++; }
            foreach (var lv in SaveManager.GetLevels()) { AddCustomNode(lv, num); num++; }
            AddNewLevelNode();

            if (_levelScroll != null)
                _levelScroll.verticalNormalizedPosition = 1f; // 回到顶部
        }

        private GameObject SpawnCard()
        {
            var go = Object.Instantiate(_cardPrefab, _levelListContent, false);
            UiUtil.SetFontRecursive(go, _font);
            return go; // 位置由 GridLayoutGroup 自动排布
        }

        // 卡片底色区分：改 Button 的 ColorTint 各态颜色，避免被切换动画覆盖。
        private static void SetCardTint(GameObject go, Color tint)
        {
            var btn = go.GetComponent<Button>();
            var c = btn.colors;
            c.normalColor = tint;
            c.highlightedColor = tint;
            c.selectedColor = tint;
            c.pressedColor = new Color(tint.r * 0.85f, tint.g * 0.85f, tint.b * 0.85f, tint.a);
            btn.colors = c;
        }

        private void AddClassicNode(int index, int num)
        {
            var lv = Levels.All[index];
            bool locked = index > 0 && !SaveManager.IsCompleted("c" + (index - 1));

            var go = SpawnCard();
            SetCardTint(go, locked ? new Color(0.86f, 0.88f, 0.88f, 1f) : ClassicCard);

            var thumb = UiUtil.Find<Image>(go, "Thumb");
            thumb.sprite = MakeMinimap(lv.Rows);
            thumb.color = locked ? new Color(0.6f, 0.63f, 0.63f) : Color.white;

            var circle = UiUtil.Find<Image>(go, "Circle");
            circle.color = locked ? new Color(0.7f, 0.72f, 0.72f) : ClassicBadge;

            var number = UiUtil.Find<Text>(go, "Number");
            number.text = num.ToString();
            number.color = locked ? new Color(0.82f, 0.82f, 0.82f) : Color.white;

            var btn = go.GetComponent<Button>();
            btn.interactable = !locked;
            if (!locked) btn.onClick.AddListener(() => _controller.StartLevel(lv.Id));
        }

        private void AddCustomNode(CustomLevel lv, int num)
        {
            var go = SpawnCard();
            SetCardTint(go, CustomCard);

            var thumb = UiUtil.Find<Image>(go, "Thumb");
            thumb.sprite = MakeMinimap(lv.rows);
            thumb.color = Color.white;

            UiUtil.Find<Image>(go, "Circle").color = CustomBadge;

            var number = UiUtil.Find<Text>(go, "Number");
            number.text = num.ToString();
            number.color = Color.white;

            go.GetComponent<Button>().onClick.AddListener(() => _controller.StartLevel(lv.id));

            AddCardActions(go, lv);
        }

        private void AddNewLevelNode()
        {
            var go = SpawnCard();
            SetCardTint(go, NewCard);

            var thumb = UiUtil.Find<Image>(go, "Thumb");
            thumb.sprite = null;
            thumb.color = new Color(0.82f, 0.9f, 0.76f, 0.6f);

            // 隐藏数字角标，用居中的大号「+」表示新增。
            var circle = go.transform.Find("Circle");
            if (circle != null) circle.gameObject.SetActive(false);

            var number = UiUtil.Find<Text>(go, "Number");
            number.text = "+";
            number.fontSize = 72;
            number.color = new Color(0.42f, 0.52f, 0.34f);
            number.alignment = TextAnchor.MiddleCenter;
            var nrt = number.GetComponent<RectTransform>();
            nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.5f);
            nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.anchoredPosition = new Vector2(0f, 15f);
            nrt.sizeDelta = new Vector2(160f, 160f);

            go.GetComponent<Button>().onClick.AddListener(() => _controller.ShowEditor());
        }

        // 自定义卡片底部加「修改 / 删除」两个按钮。
        private void AddCardActions(GameObject card, CustomLevel lv)
        {
            var edit = CreateActionButton(card.transform, "EditBtn", "修改",
                new Vector2(-48f, -112f), new Vector2(84f, 36f), new Color(0.28f, 0.56f, 0.42f),
                () => { _controller.PlayClick(); _controller.EditLevel(lv.id); });

            var del = CreateActionButton(card.transform, "DeleteBtn", "删除",
                new Vector2(48f, -112f), new Vector2(84f, 36f), new Color(0.72f, 0.34f, 0.32f),
                () => { _controller.PlayClick(); AskDelete(lv); });
        }

        private Button CreateActionButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = bg;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None; // 保持自定义底色，不做悬停变色
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.Fill();
            var txt = labelGo.AddComponent<Text>();
            txt.text = label;
            txt.font = _font;
            txt.fontSize = 20;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;

            return btn;
        }

        /* ---------- 删除确认弹窗 ---------- */
        private void BuildConfirmModal()
        {
            var overlay = new GameObject("ConfirmModal");
            overlay.transform.SetParent(_root.transform, false);
            var ort = overlay.AddComponent<RectTransform>();
            ort.Fill();
            var oimg = overlay.AddComponent<Image>();
            oimg.color = new Color(0f, 0f, 0f, 0.55f);
            oimg.raycastTarget = true; // 挡住底层点击

            var panel = new GameObject("Panel");
            panel.transform.SetParent(overlay.transform, false);
            var prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(480f, 220f);
            var pimg = panel.AddComponent<Image>();
            pimg.color = new Color(0.13f, 0.15f, 0.17f, 0.98f);

            var msgGo = new GameObject("Message");
            msgGo.transform.SetParent(panel.transform, false);
            var mrt = msgGo.AddComponent<RectTransform>();
            mrt.anchorMin = new Vector2(0f, 0.55f);
            mrt.anchorMax = new Vector2(1f, 1f);
            mrt.offsetMin = new Vector2(24f, 0f);
            mrt.offsetMax = new Vector2(-24f, -8f);
            _confirmText = msgGo.AddComponent<Text>();
            _confirmText.font = _font;
            _confirmText.fontSize = 26;
            _confirmText.color = Color.white;
            _confirmText.alignment = TextAnchor.MiddleCenter;
            _confirmText.raycastTarget = false;

            CreateModalButton(panel.transform, "CancelBtn", "取消", new Vector2(-100f, -78f), new Vector2(150f, 52f), new Color(0.4f, 0.42f, 0.44f),
                () => { _controller.PlayClick(); CloseConfirm(); });
            CreateModalButton(panel.transform, "OkBtn", "删除", new Vector2(100f, -78f), new Vector2(150f, 52f), new Color(0.72f, 0.34f, 0.32f),
                ConfirmDelete);

            overlay.SetActive(false);
            _confirmModal = overlay;
        }

        private Button CreateModalButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bg;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.Fill();
            var txt = labelGo.AddComponent<Text>();
            txt.text = label;
            txt.font = _font;
            txt.fontSize = 24;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            return btn;
        }

        private void AskDelete(CustomLevel lv)
        {
            _deleteId = lv.id;
            _confirmText.text = "确定删除关卡「" + lv.name + "」吗？\n此操作不可撤销。";
            _confirmModal.SetActive(true);
        }

        private void CloseConfirm()
        {
            _deleteId = null;
            _confirmModal.SetActive(false);
        }

        private void ConfirmDelete()
        {
            _controller.PlayClick();
            if (_deleteId != null) SaveManager.DeleteLevel(_deleteId);
            _deleteId = null;
            _confirmModal.SetActive(false);
            RenderLevels();
        }

        // 把关卡 ASCII 画成像素风俯视缩略图（墙/箱/人/目标/地板着色）。按内容缓存复用。
        private static Sprite MakeMinimap(string[] rows)
        {
            string key = string.Join("\n", rows);
            if (MinimapCache.TryGetValue(key, out var cached)) return cached;

            int rn = rows.Length;
            int cn = 0;
            foreach (var s in rows) cn = Mathf.Max(cn, s.Length);
            int cell = 16;
            int w = Mathf.Max(1, cn) * cell;
            int h = Mathf.Max(1, rn) * cell;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            for (int r = 0; r < rn; r++)
                for (int c = 0; c < cn; c++)
                {
                    char ch = c < rows[r].Length ? rows[r][c] : ' ';
                    Color col;
                    if (ch == '#') col = LayoutConst.Wall;
                    else if (ch == '%') col = LayoutConst.Gate;
                    else if (ch == '!') col = LayoutConst.Trigger;
                    else if (ch == '$' || ch == '*') col = LayoutConst.Box;
                    else if (ch == '@' || ch == '+') col = LayoutConst.Player;
                    else if (ch == '.') col = LayoutConst.Goal;
                    else if (ch == '-') col = LayoutConst.Floor;
                    else if (ch == 'i') col = LayoutConst.Tip; // 提示块
                    else col = LayoutConst.Empty; // 空格：空（无内容）

                    int baseX = c * cell;
                    int baseY = (rn - 1 - r) * cell; // 翻转：ASCII 第 0 行显示在顶部
                    for (int py = 0; py < cell; py++)
                        for (int px = 0; px < cell; px++)
                            tex.SetPixel(baseX + px, baseY + py, col);
                }

            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            MinimapCache[key] = sprite;
            return sprite;
        }
    }
}
