using System.Collections.Generic;

namespace FoodBuffOverlay
{
    internal sealed class ReleaseAsset
    {
        public string Name { get; set; }
        public string DownloadUrl { get; set; }
        public long Size { get; set; }
    }

    internal sealed class ReleaseInfo
    {
        public string Tag { get; set; }
        public SemVersion Version { get; set; }
        public string Name { get; set; }
        public string Notes { get; set; }
        public List<ReleaseAsset> Assets { get; set; } = new List<ReleaseAsset>();
    }

    internal enum UpdateCheckStatus { RepositoryNotConfigured, UpToDate, UpdateAvailable, NoStableRelease, Failed }

    internal sealed class UpdateCheckResult
    {
        public UpdateCheckStatus Status { get; set; }
        public ReleaseInfo Release { get; set; }
        public string Error { get; set; }
    }

    internal sealed class PreparedUpdate
    {
        public string StagedDllPath { get; set; }
        public string StagedUpdaterPath { get; set; }
        public string DllSha256 { get; set; }
        public ReleaseInfo Release { get; set; }
    }
}
