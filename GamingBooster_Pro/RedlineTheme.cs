using System.Windows;
using System.Windows.Media;

namespace GamingBooster_Pro
{
    /// <summary>
    /// Zentrale Farbpalette – Premium-Dunkel (Standard) oder Hell.
    /// </summary>
    public sealed class RedlineTheme
    {
        public SolidColorBrush Bg { get; } = new SolidColorBrush();
        public SolidColorBrush SideBg { get; } = new SolidColorBrush();
        public SolidColorBrush CardBg { get; } = new SolidColorBrush();
        public SolidColorBrush CardBg2 { get; } = new SolidColorBrush();
        public SolidColorBrush Red { get; } = new SolidColorBrush(Color.FromRgb(237, 28, 56));
        public SolidColorBrush DarkRed { get; } = new SolidColorBrush(Color.FromRgb(140, 12, 32));
        public SolidColorBrush Border { get; } = new SolidColorBrush();
        public SolidColorBrush Muted { get; } = new SolidColorBrush();
        public SolidColorBrush TextPrimary { get; } = new SolidColorBrush();
        public SolidColorBrush TextSecondary { get; } = new SolidColorBrush();
        public SolidColorBrush AccentSoft { get; } = new SolidColorBrush();
        public SolidColorBrush LogBackground { get; } = new SolidColorBrush();
        public SolidColorBrush LogForeground { get; } = new SolidColorBrush(Color.FromRgb(110, 235, 165));
        public SolidColorBrush PanelElevated { get; } = new SolidColorBrush();
        public SolidColorBrush GlowRed { get; } = new SolidColorBrush(Color.FromArgb(80, 237, 28, 56));

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

            // Redline = Gaming-Dunkel (auch bei Windows-Hellmodus)
            if (Mode is "Dark" or "System")
                ApplyPalette(GetDark());
            else
                ApplyPalette(GetLight());
        }

        private void ApplyPalette(ThemePalette p)
        {
            Bg.Color = p.Bg;
            SideBg.Color = p.SideBg;
            CardBg.Color = p.CardBg;
            CardBg2.Color = p.CardBg2;
            PanelElevated.Color = p.PanelElevated;
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

        /// <summary>Premium Gaming Dark – tiefer als Weiß, Rot-Akzente, hoher Kontrast.</summary>
        private static ThemePalette GetDark() => new ThemePalette
        {
            Bg = Color.FromRgb(4, 6, 11),
            SideBg = Color.FromRgb(9, 12, 20),
            CardBg = Color.FromRgb(16, 21, 32),
            CardBg2 = Color.FromRgb(22, 28, 42),
            PanelElevated = Color.FromRgb(26, 32, 48),
            Border = Color.FromRgb(42, 52, 72),
            Muted = Color.FromRgb(136, 152, 178),
            TextPrimary = Color.FromRgb(236, 240, 248),
            TextSecondary = Color.FromRgb(176, 186, 204),
            AccentSoft = Color.FromArgb(90, 120, 18, 36),
            LogBg = Color.FromRgb(6, 10, 16),
            LogFg = Color.FromRgb(110, 235, 165),
            CardGradA = Color.FromRgb(22, 28, 42),
            CardGradB = Color.FromRgb(12, 16, 26),
            SideGradA = Color.FromRgb(10, 14, 24),
            SideGradB = Color.FromRgb(14, 10, 18)
        };

        private static ThemePalette GetLight() => new ThemePalette
        {
            Bg = Color.FromRgb(228, 232, 240),
            SideBg = Color.FromRgb(255, 255, 255),
            CardBg = Color.FromRgb(255, 255, 255),
            CardBg2 = Color.FromRgb(242, 245, 250),
            PanelElevated = Color.FromRgb(248, 250, 254),
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
            public Color Bg, SideBg, CardBg, CardBg2, PanelElevated, Border, Muted, TextPrimary, TextSecondary, AccentSoft, LogBg, LogFg;
            public Color CardGradA, CardGradB, SideGradA, SideGradB;
        }
    }
}
