using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SokoBan.UI
{
    // 编辑器网格单元：实现拖拽绘制（按下 / 进入 / 抬起）。
    public class EditorCell : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerUpHandler
    {
        public int r, c;
        public LevelEditorUI editor;
        public Image bg;
        public Image marker;
        public GameObject sel; // 选中高亮框（黑色边框，动态创建）

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Middle) { editor.OnCellMiddleDown(r, c); return; } // 中键：点击选中（拖拽平移在 Update 处理）
            editor.OnCellDown(r, c, e.button == PointerEventData.InputButton.Right);
        }
        public void OnPointerEnter(PointerEventData e) => editor.OnCellEnter(r, c);
        public void OnPointerUp(PointerEventData e) => editor.OnCellUp();
    }
}
