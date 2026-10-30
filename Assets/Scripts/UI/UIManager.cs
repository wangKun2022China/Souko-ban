using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SokoBan.App;

namespace SokoBan.UI
{
    // 
    /// UI 门面：构建画布、装配各屏幕组件（主页 / HUD / Toast / 黑场转场），
    /// 并把 GameController 的调用转发给对应组件。
    /// 
    public class UIManager : MonoBehaviour
    {
        private GameController _controller;
        private Font _font;
        private bool _gameMode;

        private Transform _canvasRoot;

        private HomeScreen _home;
        private HudController _hud;
        private Toast _toast;
        private ScreenFader _fader;

        public bool IsGameMode() => _gameMode;

        public void Init(GameController c)
        {
            _controller = c;
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);

            _home = new HomeScreen(_controller, _font);
            _hud = new HudController(_controller);
            _toast = new Toast(this);
            _fader = new ScreenFader(this);

            BuildCanvas();
            LoadPrefabs();
            _fader.Create(_canvasRoot);
            ShowHome();
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
            _canvasRoot = canvasGo.transform;
        }

        private void LoadPrefabs()
        {
            var cardPrefab = Resources.Load<GameObject>("UI/LevelCard");

            _home.Attach(UiUtil.InstantiatePrefab("Home", _canvasRoot, _font), cardPrefab);
            _hud.Attach(UiUtil.InstantiatePrefab("Hud", _canvasRoot, _font));
            _toast.Attach(UiUtil.InstantiatePrefab("Toast", _canvasRoot, _font));

            _hud.Hide();
        }

        /* ================= 屏幕切换（转发） ================= */
        public void ShowHome()
        {
            _gameMode = false;
            _home.Show();
            _hud.Hide();
        }

        public void HideAll()
        {
            _gameMode = false;
            _home.Hide();
            _hud.Hide();
        }

        public void ShowGame(string levelName)
        {
            _gameMode = true;
            _home.Hide();
            _hud.Show(levelName);
        }

        public void UpdateHud(bool canUndo, bool canRedo)
            => _hud.SetButtons(canUndo, canRedo);

        public void SetTip(string text) => _hud.SetTip(text);

        public void Toast(string msg, bool error) => _toast.Show(msg, error);

        public void FadeToBlack(float duration, System.Action onDone = null) => _fader.FadeToBlack(duration, onDone);
        public void FadeFromBlack(float duration, System.Action onDone = null) => _fader.FadeFromBlack(duration, onDone);
    }
}
