using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GamingBooster_Pro
{
    /// <summary>
    /// Gemeinsame UI-Helfer: Status-Log (nicht editierbar), Pro-Sperren, Badges.
    /// </summary>
    internal static class RedlineUi
    {
        public static Border CreateStatusLogPanel(string title, string startText, RedlineTheme theme, out TextBlock body)
        {
            body = new TextBlock
            {
                Text = startText,
                Foreground = theme.LogForeground,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            };

            ScrollViewer scroll = new ScrollViewer
            {
                Height = 400,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = body,
                Focusable = false
            };

            StackPanel stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = theme.Red,
                FontSize = 13,
                FontWeight = FontWeights.UltraBold,
                Margin = new Thickness(0, 0, 0, 10)
            });
            stack.Children.Add(scroll);

            return new Border
            {
                Background = theme.CardGradient,
                BorderBrush = theme.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(14),
                Child = stack
            };
        }

        public static void AppendLog(TextBlock? block, string line)
        {
            if (block == null)
                return;
            if (string.IsNullOrEmpty(block.Text))
                block.Text = line;
            else
                block.Text += Environment.NewLine + line;
        }

        public static Border ProBadge()
        {
            Border b = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(110, 16, 28)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(6, 0, 0, 0)
            };
            b.Child = new TextBlock
            {
                Text = "PRO",
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            return b;
        }

        public static Border LockBadge(string label = "PRO")
        {
            Border b = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(90, 40, 44, 54)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(90, 98, 112)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3, 8, 3)
            };
            b.Child = new TextBlock
            {
                Text = "🔒 " + label,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 178, 192)),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            return b;
        }
    }
}
