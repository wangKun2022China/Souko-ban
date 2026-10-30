using System.Collections.Generic;
using System.Linq;

namespace SokoBan.Core
{
    internal class State
    {
        public (int r, int c) Player;
        public List<Box> Boxes;
        public HashSet<(int r, int c)> Voids;
        public HashSet<(int r, int c)> Filled;
        public int Moves;
        public int Pushes;
        public int HeldBoxId;
    }

    public class Game
    {
        public int Rows { get; private set; }
        public int Cols { get; private set; }
        public int Moves { get; private set; }
        public int Pushes { get; private set; }

        private HashSet<(int r, int c)> walls;
        private HashSet<(int r, int c)> goals;
        private HashSet<(int r, int c)> triggers;
        private HashSet<(int r, int c)> gates;
        private HashSet<(int r, int c)> voids;
        private HashSet<(int r, int c)> filled; // 被木箱填平的坑洞（变成特殊可通行路面）
        private Dictionary<(int r, int c), string> tips; // 提示块（i）→ 中文说明
        private List<Box> boxes;
        private (int r, int c) player;
        private int heldBoxId = -1;              // 吸住中的木箱 Id（-1 表示无）
        private Dictionary<(int r, int c), (int r, int c)> gateToTrigger; // 被触发块 → 触发器（一对一）

        private readonly Stack<State> history = new Stack<State>();
        private readonly Stack<State> redo = new Stack<State>();
        private readonly State initial;

        public IReadOnlyList<Box> Boxes => boxes;
        public (int r, int c) Player => player;

        /// 主角朝向（吸力方向）。Down 为初始朝向，对应 3D 角色初始朝向
        public Dir Facing { get; set; } = Dir.Down;

        public Game(LevelData levelData)
        {
            Rows = levelData.Rows;
            Cols = levelData.Cols;
            walls = new HashSet<(int r, int c)>(levelData.Walls);
            goals = new HashSet<(int r, int c)>(levelData.Goals);
            triggers = new HashSet<(int r, int c)>(levelData.Triggers);
            gates = new HashSet<(int r, int c)>(levelData.Gates);
            voids = new HashSet<(int r, int c)>(levelData.Voids);
            filled = new HashSet<(int r, int c)>();
            tips = new Dictionary<(int r, int c), string>(levelData.Tips);
            boxes = levelData.Boxes.Select(b => b.Clone()).ToList();
            player = levelData.Player;
            Moves = 0;
            Pushes = 0;

            gateToTrigger = new Dictionary<(int r, int c), (int r, int c)>();
            foreach (var kv in levelData.TriggerLinks)
                if (gates.Contains(kv.Value) && triggers.Contains(kv.Key))
                    gateToTrigger[kv.Value] = kv.Key;

            initial = CaptureState();
        }

        public static Game FromAscii(string[] rows) => new Game(LevelParser.Parse(rows));

        // 带机关关联解析（links 每项形如 "tr,tc>gr,gc"，触发器坐标 → 被触发块坐标）
        public static Game FromAscii(string[] rows, string[] links) => new Game(LevelParser.Parse(rows, links));

        // 带提示说明解析（tips 每项形如 "r,c:说明"）
        public static Game FromAscii(string[] rows, string[] links, string[] tips) => new Game(LevelParser.Parse(rows, links, tips));

        // 将 ASCII 行数组解析为关卡（对应 game.js 的 parseLevel）
        public static LevelData ParseLevel(string[] rows) => LevelParser.Parse(rows);

        public static LevelData ParseLevel(string[] rows, string[] links) => LevelParser.Parse(rows, links);

        public static LevelData ParseLevel(string[] rows, string[] links, string[] tips) => LevelParser.Parse(rows, links, tips);

        public bool InBounds(int r, int c) => r >= 0 && r < Rows && c >= 0 && c < Cols;

        // 阻挡移动：越界、墙体、空（坑洞），以及未开启的被触发块
        public bool IsWall(int r, int c) => !InBounds(r, c) || walls.Contains((r, c)) || voids.Contains((r, c)) || (gates.Contains((r, c)) && !IsGateOpen(r, c));

        // 仅墙体（不含机关门与坑洞），用于推箱阻挡 / 吸住视线等不能误报的场景
        public bool IsSolidWall(int r, int c) => !InBounds(r, c) || walls.Contains((r, c));

        public bool IsGoal(int r, int c) => goals.Contains((r, c));
        public bool IsTrigger(int r, int c) => triggers.Contains((r, c));
        public bool IsGate(int r, int c) => gates.Contains((r, c));

        // 空（无内容）——不可通行，3D 下不铺地板（坑洞）
        public bool IsVoid(int r, int c) => voids.Contains((r, c));

        // 被木箱填平的坑洞——变成特殊的可通行路面
        public bool IsFilled(int r, int c) => filled.Contains((r, c));

        // 提示块（i）：可自由通行，走到时弹出说明
        public bool IsTip(int r, int c) => tips.ContainsKey((r, c));
        public string TipAt(int r, int c) => tips.TryGetValue((r, c), out var s) ? s : null;
        public string CurrentTip
        {
            get { var s = TipAt(player.r, player.c); return string.IsNullOrEmpty(s) ? null : s; }
        }

        // 是否正吸住某个木箱（吸住后木箱跟着主角移动）
        public bool IsHolding => heldBoxId != -1;

        // 当前被吸住的木箱 Id（-1 表示无）
        public int HeldBoxId => heldBoxId;

        // 指定被触发块是否开启：其关联的触发器上有箱子（一对一）。未关联的机关门视为永久关闭
        public bool IsGateOpen(int r, int c)
        {
            if (!gates.Contains((r, c))) return false;
            if (!gateToTrigger.TryGetValue((r, c), out var trig)) return false;
            return BoxIndexAt(trig.Item1, trig.Item2) != -1;
        }

        public int BoxIndexAt(int r, int c)
        {
            for (int i = 0; i < boxes.Count; i++)
                if (boxes[i].R == r && boxes[i].C == c) return i;
            return -1;
        }

        private int BoxIndexById(int id)
        {
            for (int i = 0; i < boxes.Count; i++)
                if (boxes[i].Id == id) return i;
            return -1;
        }

        public bool IsWon()
        {
            // 新规则：主角到达终点即过关（不再要求把所有木箱推到终点）。
            return IsGoal(player.r, player.c);
        }

        public MoveResult Move(Dir dir)
        {
            Facing = dir; // 即使被阻挡也更新朝向
            var (dr, dc) = Direction.Delta(dir);
            int nr = player.r + dr, nc = player.c + dc;
            if (IsWall(nr, nc)) return new MoveResult { Outcome = MoveOutcome.Blocked };

            int bi = BoxIndexAt(nr, nc);
            if (bi == -1)
            {
                // 面前是空地：若正吸住某木箱，则拖拽它跟着主角移动一格（保持相对位置）
                if (heldBoxId != -1)
                {
                    int hi = BoxIndexById(heldBoxId);
                    if (hi == -1) { heldBoxId = -1; }
                    else
                    {
                        int hr = boxes[hi].R + dr, hc = boxes[hi].C + dc;
                        if (!HeldBoxCanMoveTo(hr, hc)) return new MoveResult { Outcome = MoveOutcome.Blocked };
                        Snapshot();
                        player = (nr, nc);
                        Moves++;
                        ApplyHeldMove(hi, hr, hc);
                        return new MoveResult { Outcome = MoveOutcome.Moved };
                    }
                }

                Snapshot();
                player = (nr, nc);
                Moves++;
                return new MoveResult { Outcome = MoveOutcome.Moved };
            }

            // 吸住中不能推箱（只能拖拽被吸住的箱子）
            if (heldBoxId != -1) return new MoveResult { Outcome = MoveOutcome.Blocked };

            int br = nr + dr, bc = nc + dc;
            if (IsSolidWall(br, bc)) return new MoveResult { Outcome = MoveOutcome.Blocked };
            if (gates.Contains((br, bc)) && !IsGateOpen(br, bc)) return new MoveResult { Outcome = MoveOutcome.Blocked };
            if (BoxIndexAt(br, bc) != -1) return new MoveResult { Outcome = MoveOutcome.Blocked };

            Snapshot();
            player = (nr, nc);
            Moves++;
            Pushes++;

            if (voids.Contains((br, bc)))
            {
                // 木箱被推进空：下沉填平该坑洞，变成特殊的可通行路面（木箱消失）
                boxes.RemoveAt(bi);
                voids.Remove((br, bc));
                filled.Add((br, bc));
                return new MoveResult { Outcome = MoveOutcome.Pushed, BoxIndex = bi, BoxConsumed = true };
            }

            boxes[bi].R = br; 
            boxes[bi].C = bc;
            return new MoveResult { Outcome = MoveOutcome.Pushed, BoxIndex = bi };
        }

        // 被吸住的木箱能否被拖到 (r,c)：墙/未开启的门/其他箱子阻挡；空（坑洞）可进入，仅跟随、不在此下沉
        private bool HeldBoxCanMoveTo(int r, int c)
        {
            if (IsSolidWall(r, c)) return false;
            if (gates.Contains((r, c)) && !IsGateOpen(r, c)) return false;
            if (BoxIndexAt(r, c) != -1) return false;
            return true;
        }

        // 把被吸住的木箱拖到 (r,c)：只跟随主角移动，不在此下沉；松开（Grab 释放）时才下沉填平坑洞
        private void ApplyHeldMove(int hi, int r, int c)
        {
            boxes[hi].R = r; boxes[hi].C = c;
        }

        // 吸力：沿当前朝向吸住直线上最近的木箱（墙 / 未开启的门挡住视线，空不挡）。
        // 吸住后木箱不瞬移，而是跟着主角移动；再次调用则释放。吸住/释放各计 1 步、可撤销。
        public GrabOutcome Grab()
        {
            if (heldBoxId != -1)
            {
                Snapshot();
                int hi = BoxIndexById(heldBoxId);
                heldBoxId = -1;
                Moves++;

                // 松开时木箱若停在坑洞上，则下沉填平该坑洞
                if (hi != -1)
                {
                    var b = boxes[hi];
                    if (voids.Contains((b.R, b.C)))
                    {
                        boxes.RemoveAt(hi);
                        voids.Remove((b.R, b.C));
                        filled.Add((b.R, b.C));
                        return GrabOutcome.ReleasedFilled;
                    }
                }
                return GrabOutcome.Released;
            }

            var (dr, dc) = Direction.Delta(Facing);
            for (int d = 1; ; d++)
            {
                int r = player.r + dr * d, c = player.c + dc * d;
                if (!InBounds(r, c)) return GrabOutcome.Failed;
                if (walls.Contains((r, c))) return GrabOutcome.Failed;
                if (gates.Contains((r, c)) && !IsGateOpen(r, c)) return GrabOutcome.Failed;

                int bi = BoxIndexAt(r, c);
                if (bi != -1)
                {
                    Snapshot();
                    heldBoxId = boxes[bi].Id;
                    Moves++;
                    return GrabOutcome.Grabbed;
                }
            }
        }

        // 顺时针旋转朝向 90°（只转向，不移动、不改变地图、不计步数）
        public void TurnClockwise()
        {
            switch (Facing)
            {
                case Dir.Up: Facing = Dir.Right; break;
                case Dir.Right: Facing = Dir.Down; break;
                case Dir.Down: Facing = Dir.Left; break;
                case Dir.Left: Facing = Dir.Up; break;
                default: Facing = Dir.Down; break;
            }
        }

        public bool CanUndo() => history.Count > 0;
        public bool CanRedo() => redo.Count > 0;

        public bool Undo()
        {
            if (history.Count == 0) return false;
            redo.Push(CaptureState());
            Restore(history.Pop());
            return true;
        }

        public bool Redo()
        {
            if (redo.Count == 0) return false;
            history.Push(CaptureState());
            Restore(redo.Pop());
            return true;
        }

        public void Restart()
        {
            Restore(initial);
            history.Clear();
            redo.Clear();
            Facing = Dir.Down;
        }

        private State CaptureState()
        {
            return new State
            {
                Player = player,
                Boxes = boxes.Select(b => b.Clone()).ToList(),
                Voids = new HashSet<(int r, int c)>(voids),
                Filled = new HashSet<(int r, int c)>(filled),
                Moves = Moves,
                Pushes = Pushes,
                HeldBoxId = heldBoxId,
            };
        }

        private void Restore(State s)
        {
            player = s.Player;
            boxes = s.Boxes.Select(b => b.Clone()).ToList();
            voids = new HashSet<(int r, int c)>(s.Voids);
            filled = new HashSet<(int r, int c)>(s.Filled);
            Moves = s.Moves;
            Pushes = s.Pushes;
            heldBoxId = s.HeldBoxId;
        }

        private void Snapshot()
        {
            history.Push(CaptureState());
            redo.Clear();
        }
    }
}
