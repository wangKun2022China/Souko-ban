using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SokoBan.UI
{
    // 轻提示条：短暂显示后自动消失。
    public class Toast
    {
        private readonly MonoBehaviour _host;

        private GameObject _root;
        private Text _text;
        private Coroutine _co;

        public Toast(MonoBehaviour host)
        {
            _host = host;
        }

        public void Attach(GameObject toastGo)
        {
            _root = toastGo;
            _text = UiUtil.Find<Text>(_root, "Text");
            _root.SetActive(false);
        }

        public void Show(string msg, bool error)
        {
            if (string.IsNullOrEmpty(msg)) return;
            _root.SetActive(true);
            _text.text = msg;
            _text.color = error ? new Color(1f, 0.85f, 0.85f) : Color.white;
            if (_co != null) _host.StopCoroutine(_co);
            _co = _host.StartCoroutine(Routine());
        }

        private IEnumerator Routine()
        {
            yield return new WaitForSeconds(2.2f);
            _root.SetActive(false);
        }
    }
}
