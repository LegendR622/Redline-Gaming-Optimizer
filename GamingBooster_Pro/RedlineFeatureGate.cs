namespace GamingBooster_Pro
{
    /// <summary>Feature-Freigaben: In-App-Treiber-Update zuerst nur Entwickler-PC, später Free für alle.</summary>
    internal static class RedlineFeatureGate
    {
        public static bool InAppDriverUpdateEnabled => RedlineDevAuth.IsAuthorizedDeveloperMachine();
    }
}
