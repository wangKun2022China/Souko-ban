using NUnit.Framework;
using SokoBan.Core;

namespace SokoBan.Tests
{
    public class GameTests
    {
        private static int CountGoals(Game g)
        {
            int n = 0;
            for (int r = 0; r < g.Rows; r++)
                for (int c = 0; c < g.Cols; c++)
                    if (g.IsGoal(r, c)) n++;
            return n;
        }

        [Test]
        public void AllLevels_ParseCorrectly()
        {
            foreach (var lv in Levels.All)
            {
                var g = Game.FromAscii(lv.Rows, lv.Links);
                Assert.AreEqual(lv.Rows.Length, g.Rows, $"关卡 {lv.Id} 行数不符");
                Assert.GreaterOrEqual(g.Player.r, 0, $"关卡 {lv.Id} 无出生点");
                Assert.GreaterOrEqual(CountGoals(g), 1, $"关卡 {lv.Id} 无终点");
            }
        }

        [Test]
        public void Move_IntoWall_IsBlocked()
        {
            // 关 1: 玩家 (1,1)，向上是墙
            var g = Game.FromAscii(Levels.All[0].Rows);
            var res = g.Move(Dir.Up);
            Assert.AreEqual(MoveOutcome.Blocked, res.Outcome);
            Assert.AreEqual(0, g.Moves);
        }

        [Test]
        public void Move_ReachGoal_Wins()
        {
            // 关 1: 玩家 (1,1) 终点 (1,2)，向右一步即过关
            var g = Game.FromAscii(Levels.All[0].Rows);
            var res = g.Move(Dir.Right);
            Assert.AreEqual(MoveOutcome.Moved, res.Outcome);
            Assert.AreEqual(1, g.Moves);
            Assert.IsTrue(g.IsWon());
        }

        [Test]
        public void FloorTile_IsWalkable()
        {
            // 地面（-）为可行走地板；玩家可踏上
            var g = Game.FromAscii(new[] { "###", "#@-#", "###" });
            Assert.IsFalse(g.IsWall(1, 1));
            var res = g.Move(Dir.Right);
            Assert.AreEqual(MoveOutcome.Moved, res.Outcome);
            Assert.AreEqual(1, g.Moves);
        }

        [Test]
        public void Void_IsBlocked()
        {
            // 空（空格）为不可通行的坑洞；玩家无法踏入
            var g = Game.FromAscii(new[] { "###", "#@ #", "###" });
            Assert.IsTrue(g.IsVoid(1, 2));
            Assert.IsTrue(g.IsWall(1, 2));
            var res = g.Move(Dir.Right);
            Assert.AreEqual(MoveOutcome.Blocked, res.Outcome);
            Assert.AreEqual(0, g.Moves);
        }

        [Test]
        public void BlockedMove_StillChangesFacing()
        {
            // 关 1: 玩家 (1,1) 向上是墙，被阻挡也应改变朝向
            var g = Game.FromAscii(Levels.All[0].Rows);
            Assert.AreNotEqual(Dir.Up, g.Facing);
            var res = g.Move(Dir.Up);
            Assert.AreEqual(MoveOutcome.Blocked, res.Outcome);
            Assert.AreEqual(Dir.Up, g.Facing);
        }

        [Test]
        public void TurnClockwise_Rotates90EachPress()
        {
            // 每次按键顺时针 90°：上 → 右 → 下 → 左 → 上
            var g = Game.FromAscii(Levels.All[0].Rows);
            g.Facing = Dir.Up;
            g.TurnClockwise();
            Assert.AreEqual(Dir.Right, g.Facing);
            g.TurnClockwise();
            Assert.AreEqual(Dir.Down, g.Facing);
            g.TurnClockwise();
            Assert.AreEqual(Dir.Left, g.Facing);
            g.TurnClockwise();
            Assert.AreEqual(Dir.Up, g.Facing);
            Assert.AreEqual(0, g.Moves); // 只转向，不计步数
        }

        [Test]
        public void Grab_HoldsNearestBoxAlongFacing()
        {
            // 玩家 (0,0) 朝右，跨过空 (0,1) 吸住 (0,2) 的箱；箱不被瞬移，仍在原位
            var g = Game.FromAscii(new[] { "@ $  ", "     ", "     " });
            g.Facing = Dir.Right;

            var res = g.Grab();
            Assert.AreEqual(GrabOutcome.Grabbed, res);
            Assert.IsTrue(g.IsHolding);
            Assert.AreEqual(1, g.Moves);
            Assert.AreEqual(0, g.Boxes[0].R);
            Assert.AreEqual(2, g.Boxes[0].C);
        }

        [Test]
        public void Grab_SecondCall_Releases()
        {
            var g = Game.FromAscii(new[] { "@ $  ", "     ", "     " });
            g.Facing = Dir.Right;
            Assert.AreEqual(GrabOutcome.Grabbed, g.Grab());

            var res = g.Grab();
            Assert.AreEqual(GrabOutcome.Released, res);
            Assert.IsFalse(g.IsHolding);
            Assert.AreEqual(2, g.Moves);
        }

        [Test]
        public void Grab_StopsAtWall()
        {
            // 墙挡住视线，墙后的箱吸不到
            var g = Game.FromAscii(new[] { "@#$", "   " });
            g.Facing = Dir.Right;
            Assert.AreEqual(GrabOutcome.Failed, g.Grab());
            Assert.IsFalse(g.IsHolding);
            Assert.AreEqual(0, g.Moves);
        }

        [Test]
        public void Drag_HeldBoxFollowsPlayer()
        {
            // 吸住 (0,2) 的箱后向下移动一格，箱跟着向下移动一格（保持相对位置，不瞬移）
            var g = Game.FromAscii(new[] { "@-$", "---", "---" });
            g.Facing = Dir.Right;
            Assert.AreEqual(GrabOutcome.Grabbed, g.Grab());

            var res = g.Move(Dir.Down);
            Assert.AreEqual(MoveOutcome.Moved, res.Outcome);
            Assert.AreEqual(1, g.Player.r);
            Assert.AreEqual(0, g.Player.c);
            Assert.AreEqual(1, g.Boxes[0].R);
            Assert.AreEqual(2, g.Boxes[0].C);
            Assert.AreEqual(2, g.Moves); // 吸住 1 步 + 拖动 1 步
        }

        [Test]
        public void WhileHolding_CannotPushBox()
        {
            // 吸住中不能推箱（只能拖拽被吸住的箱子）
            var g = Game.FromAscii(new[] { "@$ ", "   ", "   " });
            g.Facing = Dir.Right;
            Assert.AreEqual(GrabOutcome.Grabbed, g.Grab());

            var res = g.Move(Dir.Right);
            Assert.AreEqual(MoveOutcome.Blocked, res.Outcome);
            Assert.AreEqual(1, g.Boxes.Count);
            Assert.AreEqual(0, g.Boxes[0].R);
            Assert.AreEqual(1, g.Boxes[0].C);
        }

        [Test]
        public void PushBox_IntoVoid_FillsIt()
        {
            // 木箱被推进空：下沉填平坑洞，变成特殊的可通行路面
            var g = Game.FromAscii(new[] { "@$ ", "   ", "   " });
            Assert.IsTrue(g.IsVoid(0, 2));

            var res = g.Move(Dir.Right);
            Assert.AreEqual(MoveOutcome.Pushed, res.Outcome);
            Assert.IsTrue(res.BoxConsumed);
            Assert.AreEqual(0, g.Boxes.Count);
            Assert.IsFalse(g.IsVoid(0, 2));
            Assert.IsTrue(g.IsFilled(0, 2));
            Assert.IsFalse(g.IsWall(0, 2));
            Assert.AreEqual(0, g.Player.r);
            Assert.AreEqual(1, g.Player.c);
            Assert.AreEqual(1, g.Moves);
            Assert.AreEqual(1, g.Pushes);
        }

        [Test]
        public void PushBox_IntoVoid_Undo_RestoresBoxAndVoid()
        {
            var g = Game.FromAscii(new[] { "@$ ", "   ", "   " });
            Assert.AreEqual(MoveOutcome.Pushed, g.Move(Dir.Right).Outcome);
            Assert.AreEqual(0, g.Boxes.Count);
            Assert.IsTrue(g.IsFilled(0, 2));

            Assert.IsTrue(g.Undo());
            Assert.AreEqual(1, g.Boxes.Count);
            Assert.AreEqual(0, g.Boxes[0].R);
            Assert.AreEqual(1, g.Boxes[0].C);
            Assert.IsTrue(g.IsVoid(0, 2));
            Assert.IsFalse(g.IsFilled(0, 2));
        }

        [Test]
        public void Drag_HeldBoxOverVoid_KeepsFollowing()
        {
            // 拖拽被吸住的箱经过空：不立即下沉，箱继续跟随主角移动
            var g = Game.FromAscii(new[] { "@-$", "-  ", "---" });
            g.Facing = Dir.Right;
            Assert.AreEqual(GrabOutcome.Grabbed, g.Grab());

            var res = g.Move(Dir.Down);
            Assert.AreEqual(MoveOutcome.Moved, res.Outcome);
            Assert.IsFalse(res.BoxConsumed);
            Assert.AreEqual(1, g.Boxes.Count); // 箱仍在
            Assert.IsTrue(g.IsHolding);        // 仍吸住
            Assert.AreEqual(1, g.Boxes[0].R);
            Assert.AreEqual(2, g.Boxes[0].C);
            Assert.IsTrue(g.IsVoid(1, 2));     // 空仍未填
            Assert.IsFalse(g.IsFilled(1, 2));

            // 继续向下，箱离开空回到地面，空依旧未被填
            var res2 = g.Move(Dir.Down);
            Assert.AreEqual(MoveOutcome.Moved, res2.Outcome);
            Assert.AreEqual(2, g.Boxes[0].R);
            Assert.AreEqual(2, g.Boxes[0].C);
            Assert.IsTrue(g.IsHolding);
            Assert.IsTrue(g.IsVoid(1, 2));
            Assert.IsFalse(g.IsFilled(1, 2));

            // 松开时箱已在地面：普通释放，不填坑
            Assert.AreEqual(GrabOutcome.Released, g.Grab());
            Assert.IsFalse(g.IsHolding);
            Assert.AreEqual(1, g.Boxes.Count);
            Assert.IsTrue(g.IsVoid(1, 2));
            Assert.IsFalse(g.IsFilled(1, 2));
        }

        [Test]
        public void Release_HeldBoxOnVoid_FillsIt()
        {
            // 吸住的箱停在空上，松开后才下沉填平
            var g = Game.FromAscii(new[] { "@-$", "-  ", "---" });
            g.Facing = Dir.Right;
            Assert.AreEqual(GrabOutcome.Grabbed, g.Grab());
            Assert.AreEqual(MoveOutcome.Moved, g.Move(Dir.Down).Outcome);
            Assert.IsTrue(g.IsHolding);
            Assert.AreEqual(1, g.Boxes.Count);
            Assert.IsTrue(g.IsVoid(1, 2));

            var res = g.Grab();
            Assert.AreEqual(GrabOutcome.ReleasedFilled, res);
            Assert.IsFalse(g.IsHolding);
            Assert.AreEqual(0, g.Boxes.Count);
            Assert.IsFalse(g.IsVoid(1, 2));
            Assert.IsTrue(g.IsFilled(1, 2));
            Assert.IsFalse(g.IsWall(1, 2));
        }

        [Test]
        public void PushBox_OnTrigger_OpensLinkedGate()
        {
            var rows = new[] { "#####", "#@%.#", "#-$!#", "#####" };
            var links = new[] { "2,3>1,2" };
            var g = Game.FromAscii(rows, links);

            Assert.IsTrue(g.IsWall(1, 2));   // 门初始关闭
            g.Move(Dir.Down);                // (1,1)->(2,1)
            g.Move(Dir.Right);               // 推箱 (2,2)->(2,3) 触发器
            Assert.IsFalse(g.IsWall(1, 2));  // 门已开启
            g.Move(Dir.Up);                  // (2,2)->(1,2)
            g.Move(Dir.Right);               // (1,2)->(1,3) 终点
            Assert.IsTrue(g.IsWon());
        }

        [Test]
        public void UndoRedo_RestoresState()
        {
            var g = Game.FromAscii(Levels.All[0].Rows);
            var startPlayer = g.Player;
            g.Move(Dir.Right); // 到达终点
            Assert.IsTrue(g.IsWon());

            Assert.IsTrue(g.Undo());
            Assert.IsFalse(g.IsWon());
            Assert.AreEqual(startPlayer, g.Player);
            Assert.AreEqual(0, g.Moves);

            Assert.IsTrue(g.Redo());
            Assert.IsTrue(g.IsWon());
            Assert.AreEqual(1, g.Moves);
        }

        [Test]
        public void Restart_ResetsEverything()
        {
            var g = Game.FromAscii(Levels.All[0].Rows);
            g.Move(Dir.Right);
            Assert.IsTrue(g.IsWon());
            g.Restart();
            Assert.AreEqual(0, g.Moves);
            Assert.AreEqual(0, g.Pushes);
            Assert.IsFalse(g.IsWon());
        }
    }
}
