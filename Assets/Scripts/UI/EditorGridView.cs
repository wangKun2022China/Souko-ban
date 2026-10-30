using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SokoBan.UI
{
    // 编辑器网格视图：按数据模型实例化 / 上色格子，并在网格上绘制机关连线。
    public class EditorGridView
    {
        private readonly LevelEditorUI _editor;
        private GameObject _cellPrefab;
        private RectTransform _gridContent;
        private RectTransform _linkLayer;
        private EditorCell[,] _cells;
        private int _rows, _cols;

        public EditorGridView(LevelEditorUI editor) { _editor = editor; }

        public void Attach(GameObject cellPrefab, RectTransform gridContent, int rows, int cols)
        {
            _cellPrefab = cellPrefab;
            _gridContent = gridContent;
            _rows = rows;
            _cols = cols;

            // 关闭自动排版，改用手动定位，便于在网格上绘制机关连线
            var layout = _gridContent.GetComponent<GridLayoutGroup>();
            if (layout != null) layout.enabled = false;

            var linkGo = new GameObject("LinkLayer");
            linkGo.transform.SetParent(_gridContent, false);
            var linkRt = linkGo.AddComponent<RectTransform>();
            linkRt.anchorMin = linkRt.anchorMax = new Vector2(0.5f, 0.5f);
            linkRt.pivot = new Vector2(0.5f, 0.5f);
            linkRt.sizeDelta = Vector2.zero;
            linkRt.anchoredPosition = Vector2.zero;
            _linkLayer = linkRt;
        }

        // 按数据模型重建整张网格。
        public void Render(EditorDocument doc)
        {
            for (int i = _gridContent.childCount - 1; i >= 0; i--)
            {
                var go = _gridContent.GetChild(i).gameObject;
                if (go == _linkLayer.gameObject) continue;
                Object.Destroy(go);
            }

            _cells = new EditorCell[_rows, _cols];
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                {
                    _cells[r, c] = CreateCell(r, c);
                    UpdateCell(r, c, doc.At(r, c)); // 先赋值再上色
                }

            _gridContent.sizeDelta = new Vector2(
                _cols * LayoutConst.EditorCellSize + (_cols - 1) * LayoutConst.EditorSpacing,
                _rows * LayoutConst.EditorCellSize + (_rows - 1) * LayoutConst.EditorSpacing);
            RenderLinks(doc.Links);
        }

        // 按当前字符重绘单个格子。
        public void UpdateCell(int r, int c, char ch)
        {
            var cell = _cells[r, c];
            if (cell == null) return;

            if (ch == '#') cell.bg.color = LayoutConst.Wall;
            else if (ch == '%') cell.bg.color = LayoutConst.Gate;
            else if (ch == '-') cell.bg.color = LayoutConst.Floor; // 地面
            else if (ch == 'i') cell.bg.color = LayoutConst.Tip; // 提示块（可自由通行）
            else cell.bg.color = LayoutConst.Empty; // 空（无内容）

            if (ch == '.') { cell.marker.color = LayoutConst.Goal; cell.marker.gameObject.SetActive(true); }
            else if (ch == '$' || ch == '*') { cell.marker.color = LayoutConst.Box; cell.marker.gameObject.SetActive(true); }
            else if (ch == '@' || ch == '+') { cell.marker.color = LayoutConst.Player; cell.marker.gameObject.SetActive(true); }
            else if (ch == '!') { cell.marker.color = LayoutConst.Trigger; cell.marker.gameObject.SetActive(true); }
            else cell.marker.gameObject.SetActive(false);
        }

        // 高亮选中格子（黑色边框）。
        public void SetSelection(int r, int c)
        {
            ClearSelection();
            var cell = _cells[r, c];
            if (cell == null || cell.sel == null) return;
            cell.sel.SetActive(true);
        }

        // 清除所有选中高亮。
        public void ClearSelection()
        {
            if (_cells == null) return;
            foreach (var cell in _cells)
                if (cell != null && cell.sel != null) cell.sel.SetActive(false);
        }

        // 重绘机关连线（触发器中心 → 被触发块中心）。
        public void RenderLinks(IReadOnlyList<EditorDocument.Link> links)
        {
            if (_linkLayer == null) return;
            for (int i = _linkLayer.childCount - 1; i >= 0; i--)
                Object.Destroy(_linkLayer.GetChild(i).gameObject);

            foreach (var l in links)
            {
                Vector2 a = CellPos(l.tr, l.tc);
                Vector2 b = CellPos(l.gr, l.gc);
                Vector2 mid = (a + b) / 2f;
                float len = Vector2.Distance(a, b);
                float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;

                var go = new GameObject("link");
                go.transform.SetParent(_linkLayer, false);
                var img = go.AddComponent<Image>();
                img.color = LayoutConst.Link;
                img.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(len, 3f);
                rt.anchoredPosition = mid;
                rt.localRotation = Quaternion.Euler(0f, 0f, ang);
            }

            _linkLayer.SetAsLastSibling();
        }

        private EditorCell CreateCell(int r, int c)
        {
            var go = Object.Instantiate(_cellPrefab, _gridContent, false);
            go.name = "cell_" + r + "_" + c;
            var cell = go.AddComponent<EditorCell>();
            cell.r = r; cell.c = c; cell.editor = _editor;
            cell.bg = go.GetComponent<Image>();
            cell.marker = go.transform.Find("Marker").GetComponent<Image>();
            cell.sel = CreateSelectionHighlight(go);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(LayoutConst.EditorCellSize, LayoutConst.EditorCellSize);
            rt.anchoredPosition = CellPos(r, c);
            return cell;
        }

        // 动态创建一个选中边框（黑色四边细条，初始隐藏），无需改动 prefab。
        private GameObject CreateSelectionHighlight(GameObject parent)
        {
            var go = new GameObject("Sel");
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            const float t = 3f; // 边框线宽
            AddBorderBar(go.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -t), Vector2.zero);
            AddBorderBar(go.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, t));
            AddBorderBar(go.transform, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(t, 0f));
            AddBorderBar(go.transform, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-t, 0f), Vector2.zero);

            go.SetActive(false);
            return go;
        }

        // 在边框容器里加一条黑色细条。
        private void AddBorderBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var bar = new GameObject(name);
            bar.transform.SetParent(parent, false);
            var img = bar.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            var rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        // 网格坐标 → 内容局部坐标（中心为原点，列向右、行向下）
        private Vector2 CellPos(int r, int c)
        {
            float step = LayoutConst.EditorCellSize + LayoutConst.EditorSpacing;
            return new Vector2((c - (_cols - 1) / 2f) * step, ((_rows - 1) / 2f - r) * step);
        }
    }
}
