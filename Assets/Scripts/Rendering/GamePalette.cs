using UnityEngine;

namespace SokoBan.Rendering
{
    // 
    /// 全局调色板 + 渲染参数资产：3D 与 2D（编辑器/缩略图）共用一份颜色，避免魔法数字分叉。
    /// 资产位置 Assets/Resources/SokoBan/GamePalette.asset，由菜单 SokoBan/生成资源 生成。
    /// 类内字段值仅作为默认值，运行期实际读取资产里可被美术调过的值。
    /// 
    [CreateAssetMenu(menuName = "SokoBan/Game Palette", fileName = "GamePalette")]
    public class GamePalette : ScriptableObject
    {
        public const string ResourcePath = "SokoBan/GamePalette";

        [Header("3D 环境")]
        public Color background = Hex(0xd8, 0xe6, 0xe2);
        public Color warmGround = Hex(0xc9, 0xb8, 0xa0);
        public Color sunColor = Hex(0xff, 0xf2, 0xdd);
        public Color waterColor = Hex(0x74, 0xa4, 0xa6);
        public Color islandColor = Hex(0xe0, 0xcf, 0xa4);

        [Header("3D 棋盘元素")]
        public Color[] wallColors =
        {
            Hex(0xcb, 0xc3, 0xb6), Hex(0xc2, 0xba, 0xad), Hex(0xb9, 0xb1, 0xa4), Hex(0xb0, 0xa8, 0x9b),
            Hex(0xa7, 0x9f, 0x92), Hex(0xbe, 0xb6, 0xa9), Hex(0xb4, 0xac, 0x9f),
        };
        public Color floorA = Hex(0x96, 0xb2, 0x5e);
        public Color floorB = Hex(0x8a, 0xa7, 0x55);
        public Color goalColor = Hex(0xd0, 0x6a, 0x4a);
        public Color boxColor = Hex(0xc8, 0x92, 0x5a);
        public Color boxOnGoal = Hex(0x2f, 0x9e, 0x6f);
        public Color triggerColor = Hex(0x4a, 0x9e, 0xae);
        public Color gateColor = Hex(0x55, 0x5f, 0x7a);
        public Color rockColor = Hex(0x8b, 0x95, 0xa0);
        public Color heldColor = Hex(0x3a, 0xa0, 0xf2);

        [Header("3D 角色")]
        public Color cream = Hex(0xf7, 0xf2, 0xe8);
        public Color dark = Hex(0x2a, 0x2f, 0x38);
        public Color orange = Hex(0xe0, 0x8a, 0x2e);

        [Header("2D 调色板（编辑器 / 缩略图）")]
        public Color wall2D = new Color(0.42f, 0.47f, 0.47f);
        public Color empty2D = new Color(0.68f, 0.80f, 0.81f); // 空：低饱和水色
        public Color floor2D = new Color(0.62f, 0.72f, 0.42f);
        public Color goal2D = new Color(0.816f, 0.416f, 0.290f);
        public Color box2D = new Color(0.784f, 0.573f, 0.353f);
        public Color player2D = new Color(0.95f, 0.90f, 0.80f);
        public Color trigger2D = new Color(0.28f, 0.62f, 0.68f);
        public Color gate2D = new Color(0.36f, 0.38f, 0.50f);
        public Color link2D = new Color(0.98f, 0.72f, 0.20f);
        public Color tip2D = new Color(0.35f, 0.78f, 0.92f); // 提示块（可自由通行）

        [Header("渲染参数")]
        public float floorTop = 0.06f;
        public float boxHeight = 0.4f;
        public float wallMinHeight = 0.55f;
        public float wallHeightVariation = 0.22f;
        public float islandPad = 1.6f;
        public float islandDepth = 0.5f;
        public float islandY = -0.25f;
        public float waterSize = 13f;
        public float waterY = -0.42f;
        public float fogStart = 9f;
        public float fogEnd = 90f;
        public float cameraFov = 40f;
        public float cameraNear = 0.1f;
        public float cameraFar = 140f;
        public float sunIntensity = 1.15f;
        public float ambientIntensity = 0.75f;

        public static GamePalette Load() => Resources.Load<GamePalette>(ResourcePath);

        private static Color Hex(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);
    }
}
