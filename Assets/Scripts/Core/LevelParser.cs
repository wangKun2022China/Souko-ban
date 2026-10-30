namespace SokoBan.Core
{
    // ASCII 关卡格式解析（对应 js/game.js 的 parseLevel）。与游戏引擎解耦，供 Game / 编辑器复用。
    public static class LevelParser
    {
        public static LevelData Parse(string[] rows) => Parse(rows, null, null);

        public static LevelData Parse(string[] rows, string[] links) => Parse(rows, links, null);

        public static LevelData Parse(string[] rows, string[] links, string[] tips)
        {
            var level = ParseRows(rows);
            if (links != null)
                foreach (var link in links)
                {
                    var parsed = ParseLink(link);
                    if (parsed != null) level.TriggerLinks[parsed.Value.trigger] = parsed.Value.gate;
                }
            if (tips != null)
                foreach (var tip in tips)
                {
                    var parsed = ParseTip(tip);
                    if (parsed != null) level.Tips[parsed.Value.coord] = parsed.Value.text;
                }
            return level;
        }

        private static ((int r, int c) trigger, (int r, int c) gate)? ParseLink(string link)
        {
            if (string.IsNullOrEmpty(link)) return null;
            var parts = link.Replace(" ", "").Split('>');
            if (parts.Length != 2) return null;
            var t = ParseCoord(parts[0]);
            var g = ParseCoord(parts[1]);
            if (t == null || g == null) return null;
            return (t.Value, g.Value);
        }

        private static (int r, int c)? ParseCoord(string s)
        {
            var p = s.Split(',');
            if (p.Length != 2) return null;
            if (!int.TryParse(p[0], out int r) || !int.TryParse(p[1], out int c)) return null;
            return (r, c);
        }

        // 提示说明边数据：形如 "r,c:说明文字"（无冒号时说明为空）。
        private static ((int r, int c) coord, string text)? ParseTip(string tip)
        {
            if (string.IsNullOrEmpty(tip)) return null;
            var parts = tip.Split(new[] { ':' }, 2);
            var coord = ParseCoord(parts[0]);
            if (coord == null) return null;
            return (coord.Value, parts.Length > 1 ? parts[1] : "");
        }

        private static LevelData ParseRows(string[] rows)
        {
            var level = new LevelData();
            int rn = rows.Length;
            int cn = 0;
            for (int r = 0; r < rn; r++) if (rows[r].Length > cn) cn = rows[r].Length;

            int id = 0;
            for (int r = 0; r < rn; r++)
            {
                string line = rows[r];
                for (int c = 0; c < cn; c++)
                {
                    char ch = c < line.Length ? line[c] : ' ';
                    switch (ch)
                    {
                        case '#': level.Walls.Add((r, c)); break;
                        case '.': level.Goals.Add((r, c)); break;
                        case '$': level.Boxes.Add(new Box(id++, r, c)); break;
                        case '*': level.Goals.Add((r, c)); level.Boxes.Add(new Box(id++, r, c)); break;
                        case '@': level.Player = (r, c); break;
                        case '+': level.Goals.Add((r, c)); level.Player = (r, c); break;
                        case '!': level.Triggers.Add((r, c)); break;
                        case '%': level.Gates.Add((r, c)); break;
                        case 'i': level.Tips[(r, c)] = ""; break; // 提示块：可自由通行，走到时弹出说明
                        case ' ': level.Voids.Add((r, c)); break; // 空：无内容，不可通行的坑洞
                        default: break; // 地面 '-' 及其它可行走地板
                    }
                }
            }

            level.Rows = rn;
            level.Cols = cn;
            return level;
        }
    }
}
