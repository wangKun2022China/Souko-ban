using UnityEngine;
using SokoBan.Core;
using SokoBan.Rendering;
using SokoBan.UI;
using SokoBan.Audio;
using SokoBan.Persistence;

namespace SokoBan.App
{
    // 职责：主要流程控制
    // 1. 用户界面
    // 2. 关卡载入和切换
    // 2. 输入回调和音频调用。游戏逻辑在Game.cs
    public class GameController : MonoBehaviour
    {
        private Game _game;
        private Board3DRenderer _renderer;
        private UIManager _ui;
        private SynthAudio _audio;
        private LevelEditorUI _editor;

        private int _classicIndex = -1; // 如果是内置关卡,下标是
        private string _id;  // 当前关卡id（内置关卡为 "c"+index，自定义关卡为其 id）

        private bool _won;

        private void Awake()
        {
            _renderer = GetComponent<Board3DRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<Board3DRenderer>();
            _ui = GetComponent<UIManager>();
            if (_ui == null) _ui = gameObject.AddComponent<UIManager>();
            _audio = GetComponent<SynthAudio>();
            if (_audio == null) _audio = gameObject.AddComponent<SynthAudio>();
            _audio.SetEnabled(SaveManager.GetSettings().sound);
            _editor = GetComponent<LevelEditorUI>();
            if (_editor == null) _editor = gameObject.AddComponent<LevelEditorUI>();
        }

        private void Start()
        {
            _ui.Init(this);
            _editor.Init(this);
            ShowHome();
        }

        private void Update()
        {
            if (_editor != null && _editor.IsTesting()) { _editor.HandleTestInput(); return; }
            if (_won || !_ui.IsGameMode()) return;

            _ui.UpdateHud(_game.CanUndo(), _game.CanRedo());

            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) Move(Dir.Up);
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) Move(Dir.Down);
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) Move(Dir.Left);
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) Move(Dir.Right);
            else if (Input.GetKeyDown(KeyCode.Z)) Undo();
            else if (Input.GetKeyDown(KeyCode.Y)) Redo();
            else if (Input.GetKeyDown(KeyCode.J)) Grab();
            else if (Input.GetKeyDown(KeyCode.E)) Turn();
            else if (Input.GetKeyDown(KeyCode.R)) Restart();
            else if (Input.GetKeyDown(KeyCode.N)) NextLevel();
            else if (Input.GetKeyDown(KeyCode.P)) PrevLevel();
            else if (Input.GetKeyDown(KeyCode.Escape)) ShowHome();
        }

        public void PlayClick() => _audio.Click();

        public Board3DRenderer Renderer => _renderer;
        public SynthAudio Audio => _audio;

        /* ---------- 关卡载入 ---------- */
        // 唯一入口：内置关卡按 id（"c0".."c6"）传入，自定义关卡按 id 传入
        public void StartLevel(string id)
        {
            // 内置：按 id 匹配下标
            for (int i = 0; i < Levels.All.Count; i++)
            {
                if (Levels.All[i].Id != id) continue;
                if (i > 0 && !SaveManager.IsCompleted("c" + (i - 1))) {
                    _audio.Error();
                    _ui.Toast("请先完成上一关", true);
                    return;
                }
                var lv = Levels.All[i];
                LoadGame(i, lv.Id, lv.Name, lv.Rows, lv.Links, lv.Tips);
                return;
            }

            // 自定义
            var c = SaveManager.GetLevel(id);
            if (c == null) { _audio.Error(); _ui.Toast("关卡不存在", true); return; }
            LoadGame(-1, id, c.name, c.rows, c.links, c.tips);
        }

        // 唯一加载器：接收已解析的关卡（内置或自定义），写入字段并构建游戏
        private void LoadGame(int classicIndex, string id,
                              string name, string[] rows, string[] links, string[] tips)
        {
            _classicIndex = classicIndex;
            _id = id;

            _game = Game.FromAscii(rows, links, tips);
            _won = false;

            _renderer.SetGame(_game);

            bool custom = classicIndex < 0;
            string display = custom ? name : (classicIndex + 1) + ". " + name;
            _ui.ShowGame(display);
            SyncTip();
        }

        /* ---------- 移动 / 撤销 / 重做 ---------- */
        public void Move(Dir dir)
        {
            if (_game == null || _won) return;
            var res = _game.Move(dir);
            if (res.Outcome == MoveOutcome.Blocked) return; // 被阻挡时静默（对照 ui.js handleMove: if(!res) return）

            if (res.BoxConsumed) _audio.Fill();
            else if (res.Outcome == MoveOutcome.Pushed) _audio.Push();
            else _audio.Move();
            _renderer.Sync();
            SyncTip();
            if (_game.IsWon()) OnWin();
        }

        public void Undo()
        {
            if (_game == null || _won) return;
            if (_game.Undo()) { _renderer.Sync(); SyncTip(); _audio.Undo(); }
        }

        // 吸力：沿朝向吸住直线上最近的木箱（再按一次释放）。吸住后木箱跟着主角移动
        public void Grab()
        {
            if (_game == null || _won) return;
            var res = _game.Grab();
            if (res == GrabOutcome.Grabbed)
            {
                _audio.Grab();
                _renderer.Sync();
            }
            else if (res == GrabOutcome.Released)
            {
                _audio.Release();
                _renderer.Sync();
            }
            else if (res == GrabOutcome.ReleasedFilled)
            {
                _audio.Fill();
                _renderer.Sync();
            }
        }

        // 顺时针转向 90°（只转向，不计步数）
        public void Turn()
        {
            if (_game == null || _won) return;
            _game.TurnClockwise();
            _audio.Click();
            _renderer.Sync();
        }

        public void Redo()
        {
            if (_game == null || _won) return;
            if (_game.Redo()) { _renderer.Sync(); SyncTip(); _audio.Click(); }
        }

        // 重新开始
        public void Restart()
        {
            if (_game == null) return;
            _game.Restart();
            _won = false;
            _renderer.Sync();
            SyncTip();
            _audio.Click();
        }

        public void NextLevel() => Navigate(1);
        public void PrevLevel() => Navigate(-1);

        private void Navigate(int delta)
        {
            if (_classicIndex < 0)
            {
                var list = SaveManager.GetLevels();
                int idx = list.FindIndex(x => x.id == _id);
                int ni = idx + delta;
                if (ni >= 0 && ni < list.Count) { StartLevel(list[ni].id); return; }
                // 向前越过自定义列表开头：回到内置最后一关
                if (delta < 0 && Levels.All.Count > 0) { StartLevel(Levels.All[Levels.All.Count - 1].Id); return; }
                _ui.Toast(delta > 0 ? "已通关全部" : "已是第一关", false);
            }
            else
            {
                int i = _classicIndex + delta;
                if (i >= 0 && i < Levels.All.Count) { StartLevel(Levels.All[i].Id); return; }
                // 向后越过内置列表末尾：进入第一个自定义关卡
                if (delta > 0)
                {
                    var list = SaveManager.GetLevels();
                    if (list.Count > 0) { 
                        StartLevel(list[0].id); return; }
                }
                _ui.Toast(delta > 0 ? "已通关全部" : "已是第一关", false);
            }
        }

        // 判断相对当前关卡,delta方向是否还有可进入关卡
        private bool HasAdjacent(int delta)
        {
            if (_classicIndex < 0)
            {
                var list = SaveManager.GetLevels();
                int idx = list.FindIndex(x => x.id == _id);
                int ni = idx + delta;
                if (ni >= 0 && ni < list.Count) return true;
                return delta < 0 && Levels.All.Count > 0;
            }
            int i = _classicIndex + delta;
            if (i >= 0 && i < Levels.All.Count)
            {
                if (i > 0 && !SaveManager.IsCompleted("c" + (i - 1))) return false;
                return true;
            }
            return delta > 0 && SaveManager.GetLevels().Count > 0; // 向后进入第一个自定义关卡
        }

        // 主角当前所在格的提示说明：有则弹提示，无则隐藏。
        private void SyncTip() => _ui.SetTip(_game != null ? _game.CurrentTip : null);

        /* ---------- 屏幕切换 ---------- */
        public void ShowHome()
        {
            _won = false;
            _editor.HideEditor();
            _ui.ShowHome();
        }

        public void ShowEditor()
        {
            _won = false;
            _ui.HideAll();
            _editor.OpenNew();
        }

        // 从主页「修改」进入编辑器，并载入指定自定义关卡。
        public void EditLevel(string id)
        {
            var lv = SaveManager.GetLevel(id);
            if (lv == null) { _audio.Error(); _ui.Toast("关卡不存在", true); return; }
            _won = false;
            _ui.HideAll();
            _editor.OpenExisting(lv);
        }

        /* ---------- 通关 ---------- */
        private void OnWin()
        {
            _won = true;
            SaveManager.RecordCompletion(_id);
            _audio.Win();
            // 过关不弹结算：视野先变黑 → 切下一关（或回主页）→ 再从黑恢复正常
            _ui.FadeToBlack(0.5f, () =>
            {
                if (HasAdjacent(1)) 
                    Navigate(1);
                else
                    ShowHome();
                _ui.FadeFromBlack(0.5f, null);
            });
        }
    }
}
