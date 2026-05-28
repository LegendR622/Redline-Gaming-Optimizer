using System.Windows;
using System.Windows.Media;

namespace GamingBooster_Pro
{
    /// <summary>
    /// Zentrale Farbpalette – Hell/Dunkel/System. Brushes werden in-place aktualisiert,
    /// damit die komplette UI nach Theme-Wechsel neu aufgebaut werden kann.
    /// </summary>
    public sealed class RedlineTheme
    {
        public SolidColorBrush Bg { get; } = new SolidColorBrush();
        public SolidColorBrush SideBg { get; } = new SolidColorBrush();
        public SolidColorBrush CardBg { get; } = new SolidColorBrush();
        public SolidColorBrush CardBg2 { get; } = new SolidColorBrush();
        public SolidColorBrush Red { get; } = new SolidColorBrush(Color.FromRgb(235, 18, 48));
        public SolidColorBrush DarkRed { get; } = new SolidColorBrush(Color.FromRgb(125, 10, 28));
        public SolidColorBrush Border { get; } = new SolidColorBrush();
        public SolidColorBrush Muted { get; } = new SolidColorBrush();
        public SolidColorBrush TextPrimary { get; } = new SolidColorBrush(Colors.White);
        public SolidColorBrush TextSecondary { get; } = new SolidColorBrush();
        public SolidColorBrush AccentSoft { get; } = new SolidColorBrush();
        public SolidColorBrush LogBackground { get; } = new SolidColorBrush();
        public SolidColorBrush LogForeground { get; } = new SolidColorBrush(Color.FromRgb(120, 255, 160));

        public LinearGradientBrush CardGradient { get; } = new LinearGradientBrush();
        public LinearGradientBrush SidebarGradient { get; } = new LinearGradientBrush();

        public string Mode { get; private set; } = "Dark";

        public RedlineTheme()
        {
            Apply("Dark");
        }

        public void Apply(string mode)
        {
            Mode = mode switch
            {
                "Light" => "Light",
                "System" => "System",
                _ => "Dark"
            };

            if (Mode == "System")
            {
                bool light = SystemParameters.WindowGlassColor.R + SystemParameters.WindowGlassColor.G + SystemParameters.WindowGlassColor.B > 500;
                ApplyPalette(light ? GetLight() : GetDark());
                return;
            }

            ApplyPalette(Mode == "Light" ? GetLight() : GetDark());
        }

        private void ApplyPalette(ThemePalette p)
        {
            Bg.Color = p.Bg;
            SideBg.Color = p.SideBg;
            CardBg.Color = p.CardBg;
            CardBg2.Color = p.CardBg2;
            Border.Color = p.Border;
            Muted.Color = p.Muted;
            TextPrimary.Color = p.TextPrimary;
            TextSecondary.Color = p.TextSecondary;
            AccentSoft.Color = p.AccentSoft;
            LogBackground.Color = p.LogBg;
            LogForeground.Color = p.LogFg;

            CardGradient.StartPoint = new Point(0, 0);
            CardGradient.EndPoint = new Point(1, 1);
            CardGradient.GradientStops.Clear();
            CardGradient.GradientStops.Add(new GradientStop(p.CardGradA, 0));
            CardGradient.GradientStops.Add(new GradientStop(p.CardGradB, 1));

            SidebarGradient.StartPoint = new Point(0, 0);
            SidebarGradient.EndPoint = new Point(0, 1);
            SidebarGradient.GradientStops.Clear();
            SidebarGradient.GradientStops.Add(new GradientStop(p.SideGradA, 0));
            SidebarGradient.GradientStops.Add(new GradientStop(p.SideGradB, 1));
        }

        private static ThemePalette GetDark() => new ThemePalette
        {
            Bg = Color.FromRgb(7, 7, 9),
            SideBg = Color.FromRgb(14, 14, 18),
            CardBg = Color.FromRgb(21, 21, 27),
            CardBg2 = Color.FromRgb(30, 30, 38),
            Border = Color.FromRgb(48, 48, 60),
            Muted = Color.FromRgb(150, 150, 160),
            TextPrimary = Color.FromRgb(245, 245, 248),
            TextSecondary = Color.FromRgb(170, 175, 185),
            AccentSoft = Color.FromArgb(110, 110, 16, 28),
            LogBg = Color.FromRgb(4, 8, 6),
            LogFg = Color.FromRgb(120, 255, 160),
            CardGradA = Color.FromArgb(232, 18, 24, 33),
            CardGradB = Color.FromArgb(210, 10, 14, 20),
            SideGradA = Color.FromRgb(8, 12, 18),
            SideGradB = Color.FromRgb(11, 16, 24)
        };

        private static ThemePalette GetLight() => new ThemePalette
        {
            Bg = Color.FromRgb(228, 232, 240),
            SideBg = Color.FromRgb(255, 255, 255),
            CardBg = Color.FromRgb(255, 255, 255),
            CardBg2 = Color.FromRgb(242, 245, 250),
            Border = Color.FromRgb(186, 194, 210),
            Muted = Color.FromRgb(72, 82, 98),
            TextPrimary = Color.FromRgb(12, 16, 24),
            TextSecondary = Color.FromRgb(48, 56, 72),
            AccentSoft = Color.FromArgb(120, 255, 228, 232),
            LogBg = Color.FromRgb(248, 250, 254),
            LogFg = Color.FromRgb(8, 110, 48),
            CardGradA = Color.FromRgb(255, 255, 255),
            CardGradB = Color.FromRgb(248, 250, 254),
            SideGradA = Color.FromRgb(255, 255, 255),
            SideGradB = Color.FromRgb(240, 243, 250)
        };

        private sealed class ThemePalette
        {
            public Color Bg, SideBg, CardBg, CardBg2, Border, Muted, TextPrimary, TextSecondary, AccentSoft, LogBg, LogFg;
            public Color CardGradA, CardGradB, SideGradA, SideGradB;
        }
    }
}
