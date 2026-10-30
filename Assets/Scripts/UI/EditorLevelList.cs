using System;
using UnityEngine;
using UnityEngine.UI;
using SokoBan.App;
using SokoBan.Persistence;

namespace SokoBan.UI
{
    // 编辑器左侧关卡列表：自制 + 新增入口（内置关卡不可编辑，不列出）。
    public class EditorLevelList
    {
        private readonly GameController _controller;
        private readonly Font _font;

        private RectTransform _content;
        private ScrollRect _scroll;
        private GameObject _itemPrefab;
        private Action<CustomLevel> _onExisting;
        private Action _onNew;

        public EditorLevelList(GameController controller, Font font)
        {
            _controller = controller;
            _font = font;
        }

        public void Attach(RectTransform content, ScrollRect scroll, GameObject itemPrefab,
            Action<CustomLevel> onExisting, Action onNew)
        {
            _content = content;
            _scroll = scroll;
            _itemPrefab = itemPrefab;
            _onExisting = onExisting;
            _onNew = onNew;
        }

        public void Render()
        {
            if (_content == null) return;
            for (int i = _content.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);

            int y = 0;
            foreach (var lv in SaveManager.GetLevels())
            {
                AddItem(lv.name, () => _onExisting(lv), y);
                y += 58;
            }
            AddItem("+ 新增关卡", () => _onNew(), y);
            y += 58;

            _content.sizeDelta = new Vector2(0f, Mathf.Max(y, 1f));
            if (_scroll != null)
                _scroll.verticalNormalizedPosition = 1f; // 回到顶部
        }

        private void AddItem(string label, Action onClick, int y)
        {
            var go = UnityEngine.Object.Instantiate(_itemPrefab, _content, false);
            UiUtil.SetFontRecursive(go, _font);
            go.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -y);
            go.transform.Find("Label").GetComponent<Text>().text = label;
            go.GetComponent<Button>().onClick.AddListener(() => { _controller.PlayClick(); onClick(); });
        }
    }
}
