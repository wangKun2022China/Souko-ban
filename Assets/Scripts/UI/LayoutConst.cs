using UnityEngine;
using SokoBan.Rendering;

namespace SokoBan.UI
{
    // 编辑器网格布局常量 + 2D 调色板。颜色统一取自 GamePalette 资产，避免 2D/3D 分叉。
    public static class LayoutConst
    {
        public const int EditorCellSize = 80;
        public const int EditorSpacing = 4; // 白线间隙（加粗）

        private static GamePalette _p;

        private static GamePalette P
        {
            get
            {
                if (_p == null)
                {
                    _p = GamePalette.Load();
                    if (_p == null) throw new System.InvalidOperationException("找不到调色板资产 Assets/Resources/SokoBan/GamePalette.asset，请运行菜单 SokoBan/生成资源。");
                }
                return _p;
            }
        }

        public static Color Wall => P.wall2D;
        public static Color Empty => P.empty2D;
        public static Color Floor => P.floor2D;
        public static Color Goal => P.goal2D;
        public static Color Box => P.box2D;
        public static Color Player => P.player2D;
        public static Color Trigger => P.trigger2D;
        public static Color Gate => P.gate2D;
        public static Color Link => P.link2D;
        public static Color Tip => P.tip2D;
    }
}
