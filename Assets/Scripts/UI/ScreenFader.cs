using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SokoBan.UI
{
    // <summary>全屏黑场转场：过关时淡入淡出。
    public class ScreenFader
    {
        private readonly MonoBehaviour _host;

        private Image _fade;
        private Coroutine _co;

        public ScreenFader(MonoBehaviour host)
        {
            _host = host;
        }

        public void Create(Transform canvasRoot)
        {
            var go = new GameObject("Fade");
            go.transform.SetParent(canvasRoot, false);
            _fade = go.AddComponent<Image>();
            _fade.color = new Color(0f, 0f, 0f, 0f);
            _fade.raycastTarget = false;
            _fade.rectTransform.Fill();
            go.transform.SetAsLastSibling(); // 置顶
            go.SetActive(false);
        }

        public void FadeToBlack(float duration, Action onDone = null) => FadeTo(1f, duration, onDone);
        public void FadeFromBlack(float duration, Action onDone = null) => FadeTo(0f, duration, onDone);

        private void FadeTo(float target, float duration, Action onDone)
        {
            if (_co != null) _host.StopCoroutine(_co);
            _co = _host.StartCoroutine(Routine(target, duration, onDone));
        }

        private IEnumerator Routine(float target, float duration, Action onDone)
        {
            _fade.gameObject.SetActive(true);
            _fade.raycastTarget = true; // 黑屏期间挡住点击
            float start = _fade.color.a;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(start, target, Mathf.Clamp01(t / Mathf.Max(0.0001f, duration)));
                _fade.color = new Color(0f, 0f, 0f, a);
                yield return null;
            }
            _fade.color = new Color(0f, 0f, 0f, target);
            if (target <= 0.001f)
            {
                _fade.raycastTarget = false;
                _fade.gameObject.SetActive(false);
            }
            onDone?.Invoke();
        }
    }
}
