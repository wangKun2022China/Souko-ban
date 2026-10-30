using System.Collections.Generic;
using UnityEngine;

namespace SokoBan.Rendering
{
    // 轻量补间器：持有并逐帧推进一组补间（供 Board3DRenderer 的移动 / 缩放动画使用）。
    internal sealed class TweenRunner
    {
        private readonly List<Tween> _tweens = new List<Tween>();

        public void AddTween(float dur, System.Action<float> update, System.Action done, float delay = 0f)
        {
            _tweens.Add(new Tween { Start = Time.time + delay, Dur = dur, Update = update, Done = done });
        }

        public void Tick(float now)
        {
            for (int i = _tweens.Count - 1; i >= 0; i--)
            {
                var tw = _tweens[i];
                if (now < tw.Start) continue;
                float p = Mathf.Clamp01((now - tw.Start) / tw.Dur);
                tw.Update(Easing.EaseInOutCubic(p));
                if (p >= 1f)
                {
                    _tweens.RemoveAt(i);
                    tw.Done?.Invoke();
                }
            }
        }

        public void Clear() => _tweens.Clear();

        private class Tween
        {
            public float Start;
            public float Dur;
            public System.Action<float> Update;
            public System.Action Done;
        }
    }
}
