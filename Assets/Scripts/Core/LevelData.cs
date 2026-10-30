using System.Collections.Generic;

namespace SokoBan.Core
{
    public enum Dir { Up, Down, Left, Right }

    public static class Direction
    {
        public static (int dr, int dc) Delta(Dir d)
        {
            switch (d)
            {
                case Dir.Up: return (-1, 0);
                case Dir.Down: return (1, 0);
                case Dir.Left: return (0, -1);
                case Dir.Right: return (0, 1);
                default: return (0, 0);
            }
        }
    }

    public class Box
    {
        public int Id;
        public int R;
        public int C;

        public Box(int id, int r, int c) { Id = id; R = r; C = c; }
        public Box Clone() => new Box(Id, R, C);
    }

    // 解析后的关卡数据（对应 js/game.js 的 parseLevel 返回结构）
    public class LevelData
    {
        public int Rows;
        public int Cols;
        public HashSet<(int r, int c)> Walls = new HashSet<(int r, int c)>();
        public HashSet<(int r, int c)> Goals = new HashSet<(int r, int c)>();
        public HashSet<(int r, int c)> Triggers = new HashSet<(int r, int c)>(); // 触发器（压板）
        public HashSet<(int r, int c)> Gates = new HashSet<(int r, int c)>();   // 被触发块（机关门）
        public HashSet<(int r, int c)> Voids = new HashSet<(int r, int c)>();   // 空（无内容，不可通行的坑洞）
        public Dictionary<(int r, int c), string> Tips = new Dictionary<(int r, int c), string>(); // 提示块（i）→ 中文说明
        public List<Box> Boxes = new List<Box>();
        public (int r, int c) Player = (-1, -1);
        // 触发器 → 被触发块 的一对一关联（key=触发器坐标，value=被触发块坐标）
        public Dictionary<(int r, int c), (int r, int c)> TriggerLinks = new Dictionary<(int r, int c), (int r, int c)>();
    }

    public enum MoveOutcome { Blocked, Moved, Pushed }

    public class MoveResult
    {
        public MoveOutcome Outcome;
        public int BoxIndex;     // 仅当 Outcome == Pushed 时有效
        public bool BoxConsumed; // 木箱被推进空，下沉填平
    }

    public enum GrabOutcome { Failed, Grabbed, Released, ReleasedFilled }
}
