using UnityEngine;
using UnityEngine.UI;

namespace SokoBan.UI
{
    // UI 共用小工具：prefab 引用查找 / 实例化 / 字体递归赋值。
    internal static class UiUtil
    {
        // 按路径查找组件。prefab 缺子节点或组件时抛异常（带明确路径），
        /// 避免返回 null 后调用方继续 .text/.onClick 引发难以定位的 NRE。
        internal static T Find<T>(GameObject root, string path) where T : Component
        {
            var tr = path == null ? root.transform : root.transform.Find(path);
            if (tr == null)
                throw new System.InvalidOperationException("[UI] 找不到节点 " + root.name + "/" + path);
            var comp = tr.GetComponent<T>();
            if (comp == null)
                throw new System.InvalidOperationException("[UI] 节点缺少组件 " + typeof(T).Name + "：" + root.name + "/" + path);
            return comp;
        }

        internal static GameObject InstantiatePrefab(string name, Transform parent, Font font)
        {
            var go = Object.Instantiate(Resources.Load<GameObject>("UI/" + name), parent, false);
            SetFontRecursive(go, font);
            return go;
        }

        internal static void SetFontRecursive(GameObject go, Font font)
        {
            foreach (var t in go.GetComponentsInChildren<Text>(true)) t.font = font;
        }
    }

    // RectTransform 布局扩展：铺满父容器。
    public static class RectTransformExtensions
    {
        public static void Fill(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
