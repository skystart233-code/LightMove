// Hallmark · modern-minimal · Workbench · white paper / restrained blue / native CJK.
// Pre-emit critique: P5 H5 E4 S5 R5 V4. WinForms uses the sRGB equivalents of tokens.css.
using System.Drawing;

namespace grapher.CurveEditing
{
    public static class LightTheme
    {
        public const string FontFamily = "Microsoft YaHei UI";
        public static readonly Color Page = Color.FromArgb(245, 245, 247);
        public static readonly Color Surface = Color.White;
        public static readonly Color SurfaceAlt = Color.FromArgb(240, 241, 244);
        public static readonly Color Ink = Color.FromArgb(29, 29, 31);
        public static readonly Color Secondary = Color.FromArgb(86, 89, 98);
        public static readonly Color Muted = Color.FromArgb(100, 104, 113);
        public static readonly Color Rule = Color.FromArgb(224, 227, 232);
        public static readonly Color Grid = Color.FromArgb(241, 243, 246);
        public static readonly Color Accent = Color.FromArgb(0, 102, 204);
        public static readonly Color AccentSoft = Color.FromArgb(232, 242, 255);
        public static readonly Color AccentPressed = Color.FromArgb(0, 80, 166);
        public static readonly Color AccentInk = Color.White;
        public static readonly Color Success = Color.FromArgb(22, 114, 66);
        public static readonly Color Live = Color.FromArgb(201, 40, 69);
        public static readonly Color Preview = Color.FromArgb(151, 94, 0);
        public static readonly Color Disabled = Color.FromArgb(145, 150, 160);
        public static readonly Color Baseline = Color.FromArgb(133, 141, 153);
        public const int SpaceSmall = 8, SpaceMedium = 16, SpaceLarge = 24;
        public const int PageInset = 32, CardInset = 24, ControlHeight = 40;
        public const float Radius = 12;
    }
}
