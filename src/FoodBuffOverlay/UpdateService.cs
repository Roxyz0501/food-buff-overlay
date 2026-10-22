using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoodBuffOverlay
{
    internal sealed class UpdateService : IDisposable
    {
        private const long MaximumDownloadBytes = 100L * 1024 * 1024;
        private readonly HttpClient client;

        public UpdateService()
        {
            client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FoodBuffOverlay/" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }

        public async Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken)
        {
            if (!UpdateConfiguration.IsConfigured)
                return new UpdateCheckResult { Status = UpdateCheckStatus.RepositoryNotConfigured };
            try
            {
                EnsureAllowedUri(new Uri(UpdateConfiguration.ReleasesApiUrl), true);
                using (var response = await client.GetAsync(UpdateConfiguration.ReleasesApiUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength > 2 * 1024 * 1024) throw new InvalidDataException("InvalidResponse");
                    var json = await ReadLimitedTextAsync(response.Content, 2 * 1024 * 1024, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    return EvaluateResponse(json, currentVersion);
                }
            }
            catch (Exception ex)
            {
                return new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Error = ex.GetBaseException().Message };
            }
        }

        internal static async Task<UpdateCheckResult> CheckFromFetcherAsync(Func<Task<string>> fetcher, Version currentVersion)
        {
            try
            {
                if (fetcher == null) throw new ArgumentNullException("fetcher");
                return EvaluateResponse(await fetcher().ConfigureAwait(false), currentVersion);
            }
            catch (Exception ex)
            {
                return new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Error = ex.GetBaseException().Message };
            }
        }

        internal static UpdateCheckResult EvaluateResponse(string json, Version currentVersion)
        {
            try
            {
                var release = ReleaseParser.ParseStableReleases(json).FirstOrDefault();
                if (release == null) return new UpdateCheckResult { Status = UpdateCheckStatus.NoStableRelease };
                SemVersion parsedCurrent;
                if (currentVersion == null || !SemVersion.TryParse(currentVersion.Major + "." + currentVersion.Minor + "." + Math.Max(0, currentVersion.Build), out parsedCurrent))
                    throw new FormatException("Current version is not valid SemVer.");
                return new UpdateCheckResult
                {
                    Status = release.Version.CompareTo(parsedCurrent) > 0 ? UpdateCheckStatus.UpdateAvailable : UpdateCheckStatus.UpToDate,
                    Release = release
                };
            }
            catch (Exception ex)
            {
                return new UpdateCheckResult { Status = UpdateCheckStatus.Failed, Error = ex.GetBaseException().Message };
            }
        }

        public async Task<PreparedUpdate> DownloadAndVerifyAsync(ReleaseInfo release, CancellationToken cancellationToken)
        {
            if (release == null) throw new ArgumentNullException("release");
            var zip = release.Assets.FirstOrDefault(x => x.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && x.Name.IndexOf("FoodBuffOverlay", StringComparison.OrdinalIgnoreCase) >= 0);
            var manifest = release.Assets.FirstOrDefault(x => x.Name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase) || x.Name.EndsWith("SHA256SUMS", StringComparison.OrdinalIgnoreCase));
            if (zip == null || manifest == null) throw new InvalidDataException("UpdateAssetMissing");
            if (zip.Size <= 0 || zip.Size > MaximumDownloadBytes || manifest.Size > 1024 * 1024) throw new InvalidDataException("InvalidPackage");

            var root = Path.Combine(Path.GetTempPath(), "FoodBuffOverlayUpdater", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var zipPath = Path.Combine(root, "package.zip");
            var manifestPath = Path.Combine(root, "package.sha256");
            try
            {
                await DownloadAsync(zip.DownloadUrl, zipPath, cancellationToken).ConfigureAwait(false);
                await DownloadAsync(manifest.DownloadUrl, manifestPath, cancellationToken).ConfigureAwait(false);
                var expected = UpdatePackageVerifier.FindManifestHash(File.ReadAllText(manifestPath, Encoding.UTF8), zip.Name);
                if (!UpdatePackageVerifier.VerifySha256(zipPath, expected)) throw new InvalidDataException("HashFailed");

                var extracted = Path.Combine(root, "extracted");
                UpdatePackageVerifier.ExtractValidated(zipPath, extracted, cancellationToken);
                var dll = Directory.GetFiles(extracted, UpdateConfiguration.PluginFileName, SearchOption.AllDirectories).SingleOrDefault();
                if (dll == null) throw new InvalidDataException("InvalidPackage");
                UpdatePackageVerifier.ValidatePluginAssembly(dll, release.Version);
                return new PreparedUpdate { StagedDllPath = dll, DllSha256 = UpdatePackageVerifier.ComputeSha256(dll), Release = release };
            }
            catch
            {
                try { Directory.Delete(root, true); } catch { }
                throw;
            }
        }

        private async Task DownloadAsync(string url, string destination, CancellationToken cancellationToken)
        {
            var uri = new Uri(url);
            EnsureAllowedUri(uri, false);
            HttpResponseMessage response = null;
            for (var redirect = 0; redirect <= 5; redirect++)
            {
                response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                var status = (int)response.StatusCode;
                if (status < 300 || status >= 400) break;
                var location = response.Headers.Location;
                response.Dispose(); response = null;
                if (location == null) throw new InvalidDataException("InvalidPackage");
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                EnsureAllowedUri(uri, false);
            }
            if (response == null) throw new InvalidDataException("InvalidPackage");
            using (response)
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > MaximumDownloadBytes)
                    throw new InvalidDataException("InvalidPackage");
                using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var output = File.Create(destination))
                {
                    var buffer = new byte[81920];
                    long total = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        total += read;
                        if (total > MaximumDownloadBytes) throw new InvalidDataException("InvalidPackage");
                        await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        private static async Task<string> ReadLimitedTextAsync(HttpContent content, int maximumBytes, CancellationToken cancellationToken)
        {
            using (var input = await content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[8192]; int count;
                while ((count = await input.ReadAsync(chunk, 0, chunk.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (buffer.Length + count > maximumBytes) throw new InvalidDataException("InvalidResponse");
                    buffer.Write(chunk, 0, count);
                }
                return Encoding.UTF8.GetString(buffer.ToArray());
            }
        }

        internal static void EnsureAllowedUri(Uri uri, bool api)
        {
            if (uri == null || uri.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException("Only HTTPS update URLs are allowed.");
            var host = uri.Host.ToLowerInvariant();
            var allowed = api ? host == "api.github.com" :
                host == "github.com" || host == "objects.githubusercontent.com" || host.EndsWith(".githubusercontent.com", StringComparison.Ordinal);
            if (!allowed) throw new InvalidDataException("Unexpected update host.");
        }

        public void Dispose() { client.Dispose(); }
    }
}
