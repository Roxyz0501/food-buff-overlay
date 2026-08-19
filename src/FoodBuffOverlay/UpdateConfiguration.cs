using System.Linq;

namespace FoodBuffOverlay
{
    internal static class UpdateConfiguration
    {
        public const string RepositoryOwner = "Roxyz0501";
        public const string RepositoryName = "food-buff-overlay";
        public const string PluginAssemblyName = "FoodBuffOverlay";
        public const string PluginFileName = "FoodBuffOverlay.dll";
        public const string UpdaterFileName = "FoodBuffOverlay.Updater.exe";

        public static bool IsConfigured => IsSafeSegment(RepositoryOwner) && IsSafeSegment(RepositoryName);

        public static string ReleasesApiUrl => "https://api.github.com/repos/" + RepositoryOwner + "/" + RepositoryName + "/releases?per_page=10";

        private static bool IsSafeSegment(string value) => !string.IsNullOrWhiteSpace(value) &&
            value.All(x => char.IsLetterOrDigit(x) || x == '-' || x == '_' || x == '.');
    }
}
