using System.Collections.Generic;
using UnityEngine;
using SokoBan.Core;
using SokoBan.Persistence;

namespace SokoBan.UI
{
    // 编辑器数据模型：网格 + 机关一对一关联 + 撤销/重做栈 + 校验 / 裁剪 / 序列化。
    /// 纯数据逻辑，不接触 UI；渲染由 EditorGridView 负责。
    public class EditorDocument
    {
        public int Rows { get; }
        public int Cols { get; }

        private string[] _grid;
        private List<Link> _links = new List<Link>();
        private Dictionary<(int r, int c), string> _tips = new Dictionary<(int r, int c), string>();
        private readonly Stack<EditState> _undo = new Stack<EditState>();
        private readonly Stack<EditState> _redo = new Stack<EditState>();

        public EditorDocument(int rows, int cols)
        {
            Rows = rows;
            Cols = cols;
            _grid = BlankGrid();
        }

        public string[] Grid => _grid;
        public IReadOnlyList<Link> Links => _links;
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        // 重置为全空画布，并清空关联与撤销/重做历史。
        public void Reset()
        {
            _grid = BlankGrid();
            _links = new List<Link>();
            _tips = new Dictionary<(int r, int c), string>();
            _undo.Clear();
            _redo.Clear();
        }

        // 只清空撤销/重做历史（载入关卡后调用）。
        public void ClearHistory()
        {
            _undo.Clear();
            _redo.Clear();
        }

        private string[] BlankGrid()
        {
            var g = new string[Rows];
            for (int r = 0; r < Rows; r++) g[r] = new string(' ', Cols);
            return g;
        }

        public char At(int r, int c) => _grid[r][c];

        // 是否为提示块（i）
        public bool IsTip(int r, int c) => _grid[r][c] == 'i';

        // 提示块说明（无则空串）
        public string TipText(int r, int c) => _tips.TryGetValue((r, c), out var s) ? s : "";

        // 设置提示块说明（空串则移除；计入撤销/重做）
        public void SetTipText(int r, int c, string text)
        {
            Snapshot();
            if (string.IsNullOrEmpty(text)) _tips.Remove((r, c));
            else _tips[(r, c)] = text;
        }

        // 把格子设为指定字符（不记快照，由调用方决定是否先 Snapshot）。
        public void SetCell(int r, int c, char ch)
        {
            var arr = _grid[r].ToCharArray();
            arr[c] = ch;
            _grid[r] = new string(arr);
        }

        // 绘制一个格子（含擦除 / 玩家唯一性 / 工具映射）。
        /// 返回被改动的格子坐标，以及是否有机关关联因覆盖而失效（需重绘连线）。
        public (List<(int r, int c)> Cells, bool LinksChanged) Paint(int r, int c, string tool, bool erase)
        {
            Snapshot();
            var cells = new List<(int r, int c)>();

            if (erase)
            {
                SetCell(r, c, ' ');
                cells.Add((r, c));
            }
            else if (tool == "player")
            {
                // 玩家唯一：先抹掉旧出生点（含踩在终点上的形态）
                for (int rr = 0; rr < Rows; rr++)
                    for (int cc = 0; cc < Cols; cc++)
                    {
                        char old = _grid[rr][cc];
                        if (old == '@') { SetCell(rr, cc, ' '); cells.Add((rr, cc)); }
                        else if (old == '+') { SetCell(rr, cc, '.'); cells.Add((rr, cc)); }
                    }
                SetCell(r, c, '@');
                cells.Add((r, c));
            }
            else
            {
                SetCell(r, c, ToolChar(tool));
                cells.Add((r, c));
            }

            // 清理被覆盖格子的提示说明（格子不再是 i 时移除）
            for (int i = 0; i < cells.Count; i++)
            {
                var (cr, cc) = cells[i];
                if (At(cr, cc) != 'i') _tips.Remove((cr, cc));
            }

            bool linksChanged = PruneLinks();
            return (cells, linksChanged);
        }

        private static char ToolChar(string tool) =>
            tool == "wall" ? '#' : tool == "empty" ? ' ' : tool == "floor" ? '-' : tool == "goal" ? '.' : tool == "box" ? '$' : tool == "trigger" ? '!' : tool == "gate" ? '%' : tool == "tip" ? 'i' : '@';

        // 新增一对机关关联（触发器 → 被触发块）。一对一：先移除涉及同端的旧关联。
        public void AddLink(int tr, int tc, int gr, int gc)
        {
            Snapshot();
            _links.RemoveAll(l => (l.tr == tr && l.tc == tc) || (l.gr == gr && l.gc == gc));
            _links.Add(new Link(tr, tc, gr, gc));
        }

        // 清除因格子被改画而失效的关联（触发器或被触发块类型被覆盖时）。
        private bool PruneLinks()
        {
            return _links.RemoveAll(l => _grid[l.tr][l.tc] != '!' || _grid[l.gr][l.gc] != '%') > 0;
        }

        /* ================= 撤销 / 重做 ================= */
        public void Snapshot()
        {
            _undo.Push(CaptureEdit());
            _redo.Clear();
        }

        public bool Undo()
        {
            if (_undo.Count == 0) return false;
            _redo.Push(CaptureEdit());
            RestoreEdit(_undo.Pop());
            return true;
        }

        public bool Redo()
        {
            if (_redo.Count == 0) return false;
            _undo.Push(CaptureEdit());
            RestoreEdit(_redo.Pop());
            return true;
        }

        private EditState CaptureEdit()
        {
            return new EditState
            {
                Grid = (string[])_grid.Clone(),
                Links = _links.ConvertAll(l => l.Clone()),
                Tips = new Dictionary<(int r, int c), string>(_tips),
            };
        }

        private void RestoreEdit(EditState s)
        {
            _grid = s.Grid;
            _links = s.Links ?? new List<Link>();
            _tips = s.Tips ?? new Dictionary<(int r, int c), string>();
        }

        /* ================= 载入 ================= */
        // 把关卡内容载入大画布，居中（超网格则左上对齐裁剪）。
        public void Load(string[] rows, string[] links) => Load(rows, links, null);

        public void Load(string[] rows, string[] links, string[] tips)
        {
            _grid = BlankGrid();

            int rn = rows.Length;
            int cn = 0;
            foreach (var s in rows) cn = Mathf.Max(cn, s.Length);
            int offR = Mathf.Max(0, (Rows - rn) / 2);
            int offC = Mathf.Max(0, (Cols - cn) / 2);

            for (int r = 0; r < rn && offR + r < Rows; r++)
            {
                string s = rows[r];
                if (s.Length > cn) s = s.Substring(0, cn);
                var arr = _grid[offR + r].ToCharArray();
                for (int c = 0; c < s.Length && offC + c < Cols; c++)
                    arr[offC + c] = s[c];
                _grid[offR + r] = new string(arr);
            }

            _links = new List<Link>();
            if (links != null)
            {
                foreach (var s in links)
                {
                    var p = s.Replace(" ", "").Split('>');
                    if (p.Length != 2) continue;
                    var t = ParseLinkCoord(p[0]);
                    var g = ParseLinkCoord(p[1]);
                    if (t != null && g != null)
                        _links.Add(new Link(t.Value.r + offR, t.Value.c + offC, g.Value.r + offR, g.Value.c + offC));
                }
            }

            _tips = new Dictionary<(int r, int c), string>();
            if (tips != null)
                foreach (var s in tips)
                {
                    var p = s.Split(new[] { ':' }, 2);
                    var coord = ParseLinkCoord(p[0]);
                    if (coord != null)
                        _tips[(coord.Value.r + offR, coord.Value.c + offC)] = p.Length > 1 ? p[1] : "";
                }
        }

        private static (int r, int c)? ParseLinkCoord(string s)
        {
            var p = s.Split(',');
            if (p.Length != 2) return null;
            if (!int.TryParse(p[0], out int r) || !int.TryParse(p[1], out int c)) return null;
            return (r, c);
        }

        /* ================= 校验 / 序列化 ================= */
        public bool Validate(out LevelData level, out string error)
        {
            var (minR, minC, _, _) = GridBounds();
            level = Game.ParseLevel(CropToBounds(), CropLinks(minR, minC), CropTips(minR, minC));
            var errs = new List<string>();
            if (level.Player.r < 0) errs.Add("缺少出生点（@）");
            if (level.Goals.Count == 0) errs.Add("缺少终点（.）");
            error = string.Join("；", errs);
            return errs.Count == 0;
        }

        public CustomLevel BuildLevel(string id, string name)
        {
            var (minR, minC, _, _) = GridBounds();
            return new CustomLevel
            {
                id = id,
                name = name,
                rows = CropToBounds(),
                links = CropLinks(minR, minC),
                tips = CropTips(minR, minC),
            };
        }

        private (int minR, int minC, int maxR, int maxC) GridBounds()
        {
            int minR = Rows, maxR = -1, minC = Cols, maxC = -1;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    if (_grid[r][c] != ' ')
                    {
                        if (r < minR) minR = r;
                        if (r > maxR) maxR = r;
                        if (c < minC) minC = c;
                        if (c > maxC) maxC = c;
                    }
            return (minR, minC, maxR, maxC);
        }

        // 裁掉四周空格，只保留有效包围盒内的内容。
        private string[] CropToBounds()
        {
            var (minR, minC, maxR, maxC) = GridBounds();
            if (maxR < minR) return new[] { " " }; // 全空（正常不会到这里，Validate 会拦截）
            var result = new string[maxR - minR + 1];
            for (int r = minR; r <= maxR; r++)
                result[r - minR] = _grid[r].Substring(minC, maxC - minC + 1);
            return result;
        }

        // 把网格坐标的机关关联转换为裁剪后的坐标。
        private string[] CropLinks(int minR, int minC)
        {
            if (_links.Count == 0) return null;
            var arr = new string[_links.Count];
            for (int i = 0; i < _links.Count; i++)
            {
                var l = _links[i];
                arr[i] = (l.tr - minR) + "," + (l.tc - minC) + ">" + (l.gr - minR) + "," + (l.gc - minC);
            }
            return arr;
        }

        // 把网格坐标的提示说明转换为裁剪后的坐标（"r,c:说明"），仅保留仍是提示块的格子。
        private string[] CropTips(int minR, int minC)
        {
            var list = new List<string>();
            foreach (var kv in _tips)
            {
                int r = kv.Key.r, c = kv.Key.c;
                if (r < 0 || c < 0 || r >= Rows || c >= Cols) continue;
                if (_grid[r][c] != 'i') continue;
                list.Add((r - minR) + "," + (c - minC) + ":" + kv.Value);
            }
            return list.Count == 0 ? null : list.ToArray();
        }

        // 一条机关关联：触发器 (tr,tc) → 被触发块 (gr,gc)。
        public class Link
        {
            public int tr, tc, gr, gc;
            public Link(int a, int b, int c, int d) { tr = a; tc = b; gr = c; gc = d; }
            public Link Clone() => new Link(tr, tc, gr, gc);
        }

        // 编辑快照：网格 + 机关关联，用于撤销/重做。
        private class EditState
        {
            public string[] Grid;
            public List<Link> Links;
            public Dictionary<(int r, int c), string> Tips;
        }
    }
}
