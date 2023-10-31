using System;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Squirrel.CommandLine.Commands;
using Squirrel.SimpleSplat;
using Squirrel.Sources;

namespace Squirrel.CommandLine.Sync
{
    /*
    /// <summary>
    /// For CI/CD and command line tools
    /// </summary>
    internal static class GiteaRepository
    {
        internal readonly static IFullLogger Log = SquirrelLocator.Current.GetService<ILogManager>().GetLogger(typeof(GiteaRepository));

        public static async Task DownloadRecentPackages(GiteaDownloadCommand options)
        {
            var releaseDirectoryInfo = options.GetReleaseDirectory();

            if (String.IsNullOrWhiteSpace(options.Token))
                Log.Warn("No Gitea access token provided.");

            Log.Info("Fetching RELEASES...");
            var source = new GiteaSource(options.RepoUrl, options.Token, options.Pre); ///USES SOURCE.!!!!!!!!
            var latestReleaseEntries = await source.GetReleaseFeed();

            if (latestReleaseEntries == null || latestReleaseEntries.Length == 0) {
                Log.Warn("No gitea release or assets found.");
                return;
            }

            Log.Info($"Found {latestReleaseEntries.Length} assets in RELEASES file for Gitea version {source.Release.Name}.");

            var releasesToDownload = latestReleaseEntries
                .Where(x => !x.IsDelta)
                .OrderByDescending(x => x.Version)
                .Take(1)
                .Select(x => new {
                    Obj = x,
                    LocalPath = Path.Combine(releaseDirectoryInfo.FullName, x.Filename),
                    Filename = x.Filename,
                });

            foreach (var entry in releasesToDownload) {
                if (File.Exists(entry.LocalPath)) {
                    Log.Warn($"File '{entry.Filename}' exists on disk, skipping download.");
                    continue;
                }

                Log.Info($"Downloading {entry.Filename}...");
                await source.DownloadReleaseEntry(entry.Obj, entry.LocalPath, (p) => { });
            }

            ReleaseEntry.BuildReleasesFile(releaseDirectoryInfo.FullName);
            Log.Info("Done.");
        }

        public static async Task UploadMissingPackages(GiteaUploadCommand options)
        {
            if (String.IsNullOrWhiteSpace(options.Token))
                throw new InvalidOperationException("Must provide access token to create a Gitea release.");

            var releaseDirectoryInfo = options.GetReleaseDirectory();

             http://localhost:3000/repoOwner/repoName/ 

            var repoUri = new Uri(options.RepoUrl);
            var repoParts = repoUri.AbsolutePath.Trim('/').Split('/');
            if (repoParts.Length != 2)
                throw new Exception($"Invalid Gitea URL, '{repoUri.AbsolutePath}' should be in the format 'owner/repo'");

            var repoOwner = repoParts[0];
            var repoName = repoParts[1];

            var client = new GiteaClient(new ProductHeaderValue("Clowd.Squirrel")); //TODO copy over client from github code and change to work with gitea
            // Credentials = new Credentials(options.Token) //TODO: implement token

            var releasesPath = Path.Combine(releaseDirectoryInfo.FullName, "RELEASES"); //used to get release notes

            if (!File.Exists(releasesPath))
                ReleaseEntry.BuildReleasesFile(releaseDirectoryInfo.FullName);

            var releases = ReleaseEntry.ParseReleaseFile(File.ReadAllText(releasesPath)).ToArray();
            if (releases.Length == 0)
                throw new Exception("There are no nupkg's in the releases directory to upload");

            var ver = Enumerable.MaxBy(releases, x => x.Version);
            if (ver == null)
                throw new Exception("There are no nupkg's in the releases directory to upload");
            var semVer = ver.Version;

            Log.Info($"Preparing to upload latest local release to Gitea");

            //I dont know how to get ID, maybe I should get a list of IDs then pick an unused one

            //string.IsNullOrWhiteSpace

            GiteaRelease newReleaseRequest = new GiteaRelease(semVer.ToString()) {
                Body = ver.GetReleaseNotes(releaseDirectoryInfo.FullName, ReleaseNotesFormat.Markdown),
                Draft = true,
                Prerelease = semVer.HasMetadata || semVer.IsPrerelease,
                Name = string.IsNullOrWhiteSpace(options.ReleaseName)
                    ? semVer.ToString()
                    : options.ReleaseName,
                Id = 1337,
                //Tag already set 
                //public bool Prerelease { get; set; }
                //public DateTime PublishedAt { get; set; }
                //public GiteaReleaseAsset[] Assets { get; set; }
            };

            Log.Info($"Creating draft release titled '{semVer.ToString()}'");

            var existingReleases = await client.Repository.Release.GetAll(repoOwner, repoName);

            if (existingReleases.Any(r => r.TagName == semVer.ToString())) {
                throw new Exception($"There is already an existing release tagged '{semVer}'. Please delete this release or choose a new version number.");
            }

            var release = await client.Repository.Release.Create(repoOwner, repoName, newReleaseReq);

            // locate files to upload
            var files = releaseDirectoryInfo.GetFiles("*", SearchOption.TopDirectoryOnly);
            var msiFile = files.SingleOrDefault(f => f.FullName.EndsWith(".msi", StringComparison.InvariantCultureIgnoreCase));
            var setupFile = files.Where(f => f.FullName.EndsWith("Setup.exe", StringComparison.InvariantCultureIgnoreCase))
                .ContextualSingle("release directory", "Setup.exe file");

            var releasesToUpload = releases.Where(x => x.Version == semVer).ToArray();
            MemoryStream releasesFileToUpload = new MemoryStream();
            ReleaseEntry.WriteReleaseFile(releasesToUpload, releasesFileToUpload);
            var releasesBytes = releasesFileToUpload.ToArray();

            // upload nupkg's
            foreach (var r in releasesToUpload) {
                var path = Path.Combine(releaseDirectoryInfo.FullName, r.Filename);
                await UploadFileAsAsset(client, release, path);
            }

            // other files
            await UploadFileAsAsset(client, release, setupFile.FullName);
            if (msiFile != null) await UploadFileAsAsset(client, release, msiFile.FullName);

            // RELEASES
            Log.Info($"Uploading RELEASES");
            var data = new ReleaseAssetUpload("RELEASES", "application/octet-stream", new MemoryStream(releasesBytes), TimeSpan.FromMinutes(1));
            await client.Repository.Release.UploadAsset(release, data, CancellationToken.None);

            Log.Info($"Done creating draft Gitea release.");

            // convert draft to full release
            if (options.Publish) {
                Log.Info("Converting draft to full published release.");
                var upd = release.ToUpdate();
                upd.Draft = false;
                release = await client.Repository.Release.Edit(repoOwner, repoName, release.Id, upd);
            }

            Log.Info("Release URL: " + release.HtmlUrl);
        }

        private static async Task UploadFileAsAsset(GiteaClient client, Release release, string filePath)
        {
            Log.Info($"Uploading asset '{Path.GetFileName(filePath)}'");
            using var stream = File.OpenRead(filePath);
            var data = new ReleaseAssetUpload(Path.GetFileName(filePath), "application/octet-stream", stream, TimeSpan.FromMinutes(30));
            await client.Repository.Release.UploadAsset(release, data, CancellationToken.None);
        }
    }

    internal class GiteaClient
    {
        readonly System.Net.Http.Headers.ProductHeaderValue productHeaderValue;

        // Credentials 

        public GiteaClient(System.Net.Http.Headers.ProductHeaderValue productHeaderValue) 
        {
            this.productHeaderValue = productHeaderValue;
        }

        public GiteaClient(System.Net.Http.Headers.ProductHeaderValue productHeaderValue, GiteaUploadCommand options)
        {
            this.productHeaderValue = productHeaderValue;
        }
    }*/
}