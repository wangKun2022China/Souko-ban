using System;
using UnityEngine;
using UnityEngine.UI;
using SokoBan.App;
using SokoBan.Core;

namespace SokoBan.UI
{
    // 编辑器测试试玩：在临时 Game 上跑输入、播放音效并同步 HUD 
    public class EditorTestMode
    {
        private readonly GameController _controller;
        private readonly Action<string, bool> _setStatus;
        private readonly Action _onStop;

        private GameObject _testHud;
        private Game _testGame;
        private bool _testing;
        private GameObject _tipPanel;
        private Text _tipText;

        public EditorTestMode(GameController controller, Action<string, bool> setStatus, Action onStop)
        {
            _controller = controller;
            _setStatus = setStatus;
            _onStop = onStop;
        }

        public bool IsTesting => _testing;

        public void Attach(GameObject testHud)
        {
            _testHud = testHud;
            UiUtil.Find<Button>(_testHud, "UndoBtn").onClick.AddListener(Undo);
            UiUtil.Find<Button>(_testHud, "RestartBtn").onClick.AddListener(Restart);
            UiUtil.Find<Button>(_testHud, "StopBtn").onClick.AddListener(() => _onStop());
        }

        public void Start(LevelData level)
        {
            _testing = true;
            _testGame = new Game(level);
            _testHud.SetActive(true);
            _controller.Renderer.SetGame(_testGame, true);
            SyncTip();
        }

        public void Stop()
        {
            _testing = false;
            _testGame = null;
            _testHud.SetActive(false);
        }

        public void HandleInput()
        {
            if (!_testing) return;
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) Move(Dir.Up);
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) Move(Dir.Down);
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) Move(Dir.Left);
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) Move(Dir.Right);
            else if (Input.GetKeyDown(KeyCode.Z)) Undo();
            else if (Input.GetKeyDown(KeyCode.J)) Grab();
            else if (Input.GetKeyDown(KeyCode.E)) Turn();
            else if (Input.GetKeyDown(KeyCode.R)) Restart();
            else if (Input.GetKeyDown(KeyCode.Escape)) _onStop();
        }

        private void Move(Dir dir)
        {
            if (_testGame == null) return;
            var res = _testGame.Move(dir);
            if (res.Outcome == MoveOutcome.Blocked) return;
            if (res.BoxConsumed) _controller.Audio.Fill();
            else if (res.Outcome == MoveOutcome.Pushed) _controller.Audio.Push();
            else _controller.Audio.Move();
            _controller.Renderer.Sync();
            SyncTip();
            if (_testGame.IsWon()) { _controller.Audio.Win(); _setStatus("✓ 测试通过：该关卡可完成", false); }
        }

        private void Undo()
        {
            if (!_testing || _testGame == null) return;
            if (_testGame.Undo()) {
                _controller.Renderer.Sync(); 
                SyncTip();
                _controller.Audio.Undo();
            }
        }

        private void Grab()
        {
            if (!_testing || _testGame == null) return;
            var res = _testGame.Grab();
            if (res == GrabOutcome.Grabbed) _controller.Audio.Grab();
            else if (res == GrabOutcome.Released) _controller.Audio.Release();
            else if (res == GrabOutcome.ReleasedFilled) _controller.Audio.Fill();
            else return;
            _controller.Renderer.Sync();
            SyncTip();
            if (_testGame.IsWon()) { _controller.Audio.Win(); _setStatus("✓ 测试通过：该关卡可完成", false); }
        }

        private void Turn()
        {
            if (!_testing || _testGame == null) return;
            _testGame.TurnClockwise();
            _controller.Renderer.Sync();
            _controller.Audio.Click();
        }

        private void Restart()
        {
            if (!_testing || _testGame == null) return;
            _testGame.Restart();
            _controller.Renderer.Sync();
            SyncTip();
        }

        // 展示 / 隐藏测试主角所在提示块的说明。
        private void SyncTip()
        {
            if (_tipPanel == null) return;
            string s = _testGame != null ? _testGame.CurrentTip : null;
            if (string.IsNullOrEmpty(s)) { _tipPanel.SetActive(false); return; }
            _tipText.text = s;
            _tipPanel.SetActive(true);
        }

        // 提示弹窗（运行时构建，无需改 prefab）。
        private void BuildTipPanel(Font font)
        {
            var go = new GameObject("TipPanel");
            go.transform.SetParent(_testHud.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 160f);
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
    }
}
