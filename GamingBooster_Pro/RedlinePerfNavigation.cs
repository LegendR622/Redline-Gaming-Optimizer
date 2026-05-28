using System;

namespace GamingBooster_Pro
{
    internal enum PerfDetailAction
    {
        GameModeSettings,
        PowerPlan,
        GraphicsSettings,
        VisualEffects,
        Services,
        NavigateStartup,
        GameBar,
        NavigateSettings
    }

    /// <summary>Performance-Kachel-Pfeil: Ziel aus Titel (testbar ohne UI).</summary>
    internal static class RedlinePerfNavigation
    {
        public static PerfDetailAction Resolve(string title)
        {
            string t = (title ?? "").ToLowerInvariant();

            if (t.Contains("autostart"))
                return PerfDetailAction.NavigateStartup;

            if (t.Contains("game") && !t.Contains("bar"))
                return PerfDetailAction.GameModeSettings;

            if (t.Contains("fps") || t.Contains("boost") || t.Contains("game bar") || t.Contains("gamebar"))
                return PerfDetailAction.GameBar;

            if (t.Contains("hoch") || t.Contains("high") || t.Contains("power") || t.Contains("leistung") || t.Contains("performance"))
                return PerfDetailAction.PowerPlan;

            if (t.Contains("grafik") || t.Contains("graphic") || t.Contains("hardware") || t.Contains("gpu"))
                return PerfDetailAction.GraphicsSettings;

            if (t.Contains("hinter") || t.Contains("background") || t.Contains("dienst") || t.Contains("service"))
                return PerfDetailAction.Services;

            if (t.Contains("visuell") || t.Contains("visual"))
                return PerfDetailAction.VisualEffects;

            return PerfDetailAction.NavigateSettings;
        }

        public static string ExpectedDryRunToken(PerfDetailAction action) => action switch
        {
            PerfDetailAction.GameModeSettings => "uri:ms-settings:gaming-gamemode",
            PerfDetailAction.PowerPlan => "proc:powercfg.cpl",
            PerfDetailAction.GraphicsSettings => "uri:ms-settings:display-advancedgraphics",
            PerfDetailAction.VisualEffects => "proc:SystemPropertiesPerformance.exe",
            PerfDetailAction.Services => "proc:services.msc",
            PerfDetailAction.NavigateStartup => "nav:Startup",
            PerfDetailAction.GameBar => "uri:ms-settings:gaming-gamebar",
            PerfDetailAction.NavigateSettings => "nav:Settings",
            _ => "?"
        };
    }

    internal static class RedlineTestHooks
    {
        public static bool DryRun { get; set; }
        public static string? LastAction { get; private set; }

        public static void Record(string action) => LastAction = action;

        public static void Reset() => LastAction = null;
    }
}
