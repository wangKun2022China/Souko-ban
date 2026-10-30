using System;

namespace SokoBan.Rendering
{
    // 缓动与伪随机工具（逐行对照 render3d.js 顶部的纯函数）。
    public static class Easing
    {
        public static float EaseInOutCubic(float t)
            => (float)(t < 0.5 ? 4.0 * t * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 3.0) / 2.0);

        public static float EaseOutBack(float t)
        {
            const double c1 = 1.70158;
            double c3 = c1 + 1;
            return (float)(1.0 + c3 * Math.Pow(t - 1.0, 3.0) + c1 * Math.Pow(t - 1.0, 2.0));
        }

        // 与 JS 完全一致的确定性伪随机：sin(i*127.1 + 311.7) * 43758.5453 的小数部分。
        public static float Seeded(int i)
        {
            double x = Math.Sin(i * 127.1 + 311.7) * 43758.5453;
            return (float)(x - Math.Floor(x));
        }
    }
}
