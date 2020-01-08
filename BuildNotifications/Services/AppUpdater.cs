﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Anotar.NLog;
using Newtonsoft.Json;

namespace BuildNotifications.Services
{
    internal class AppUpdater : IAppUpdater
    {
        public AppUpdater(bool includePreReleases, INotifier notifier)
        {
            _includePreReleases = includePreReleases;
            _notifier = notifier;
            _updateExePath = FindUpdateExe();
            _packagesFolder = FindPackagesFolder();
            LogTo.Info($"Update.exe should be located at {_updateExePath}");
        }

        private async Task DownloadFullNupkgFile(string targetFilePath, string version)
        {
            var fileName = Path.GetFileName(targetFilePath);
            var url = new Uri(await GetUpdateUrl(version) + "/" + fileName);

            if (File.Exists(targetFilePath))
                File.Delete(targetFilePath);

            var baseAddress = new Uri(url.Scheme + "://" + url.Host);

            using var client = new HttpClient {BaseAddress = baseAddress};
            var stream = await client.GetStreamAsync(url.AbsolutePath.TrimStart('/'));

            await using var fileStream = File.OpenWrite(targetFilePath);
            await stream.CopyToAsync(fileStream);
        }

        private async Task DownloadReleasesFile(string targetFilePath, string version)
        {
            var url = new Uri(await GetUpdateUrl(version) + "/RELEASES");

            if (File.Exists(targetFilePath))
                File.Delete(targetFilePath);

            var baseAddress = new Uri(url.Scheme + "://" + url.Host);

            using var client = new HttpClient {BaseAddress = baseAddress};
            var stream = await client.GetStreamAsync(url.AbsolutePath.TrimStart('/'));

            await using var fileStream = File.OpenWrite(targetFilePath);
            await stream.CopyToAsync(fileStream);
        }

        private bool FilterRelease(Release x)
        {
            if (_includePreReleases)
                return true;

            if (!x.PreRelease)
                return true;

            LogTo.Debug($"Ignoring pre-release at \"{x.HtmlUrl}\"");
            return false;
        }

        private static string FindPackagesFolder()
        {
            var rootDirectory = FindRootDirectory();

            var fullPath = Path.Combine(rootDirectory, "packages");
            return fullPath;
        }

        private static string FindRootDirectory()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation)
                                    ?? Directory.GetCurrentDirectory();

            return Path.Combine(assemblyDirectory, "..");
        }

        private static string FindUpdateExe()
        {
            var rootDirectory = FindRootDirectory();

            var fullPath = Path.Combine(rootDirectory, "update.exe");
            return fullPath;
        }

        private Task<string> GetLatestUpdateUrl()
        {
            return GetUpdateUrl(string.Empty);
        }

        private async Task<string> GetUpdateUrl(string version)
        {
            if (_updateUrlCache.TryGetValue(version, out var url))
                return url;

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0);
            var userAgent = new ProductInfoHeaderValue(AppName, currentVersion.ToString(3));

            var repoUri = new Uri(UpdateUrl);
            var releasesApiBuilder = new StringBuilder("repos")
                .Append(repoUri.AbsolutePath)
                .Append("/releases");

            var baseAddress = new Uri("https://api.github.com/");

            using var client = new HttpClient {BaseAddress = baseAddress};
            client.DefaultRequestHeaders.UserAgent.Add(userAgent);
            var response = await client.GetAsync(releasesApiBuilder.ToString());
            response.EnsureSuccessStatusCode();

            var releases = JsonConvert.DeserializeObject<List<Release>>(await response.Content.ReadAsStringAsync());
            var release = releases
                .Where(x => string.IsNullOrEmpty(version) || x.HtmlUrl.Contains(version))
                .Where(FilterRelease)
                .OrderByDescending(x => x.PublishedAt)
                .First();

            var updateUrl = release.HtmlUrl.Replace("/tag/", "/download/");
            _updateUrlCache[version] = updateUrl;
            return updateUrl;
        }

        private async Task SanitizePackages()
        {
            LogTo.Info($"Sanitizing packages folder at {_packagesFolder}");

            if (!Directory.Exists(_packagesFolder))
            {
                LogTo.Debug("Folder missing. Creating.");
                Directory.CreateDirectory(_packagesFolder);
            }

            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
            if (version == null)
            {
                LogTo.Debug("Unable to determine current version");
                return;
            }

            var releasesFilePath = Path.Combine(_packagesFolder, "RELEASES");
            if (!File.Exists(releasesFilePath))
            {
                LogTo.Debug("RELEASES file does not exist. Downloading.");
                await DownloadReleasesFile(releasesFilePath, version);
            }

            var currentNupkgName = $"{AppName}-{version}-full.nupkg";
            var currentNupkgFilePath = Path.Combine(_packagesFolder, currentNupkgName);
            if (!File.Exists(currentNupkgFilePath))
            {
                LogTo.Debug("Current full nupkg does not exist. Downloading");
                await DownloadFullNupkgFile(currentNupkgFilePath, version);
            }

            LogTo.Info("Packages folder is sanitized");
        }

        public async Task<UpdateCheckResult?> CheckForUpdates(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(_updateExePath))
            {
                LogTo.Warn($"Update.exe not found. Expected it to be located at {_updateExePath}");
                return null;
            }

            LogTo.Info($"Checking for updates (include pre-releases: {_includePreReleases})");

            await SanitizePackages();

            var latestUpdateUrl = await GetLatestUpdateUrl();

            return await Task.Run(() =>
            {
                var pi = new ProcessStartInfo
                {
                    FileName = _updateExePath,
                    Arguments = $"--checkForUpdate={latestUpdateUrl}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };

                var p = new Process
                {
                    StartInfo = pi
                };

                string? textResult = null;
                p.OutputDataReceived += (s, e) =>
                {
                    LogTo.Debug($"Checking: {e.Data}");
                    if (e.Data?.StartsWith("{") ?? false)
                        textResult = e.Data;
                };

                p.Start();
                p.BeginOutputReadLine();
                p.WaitForExit();

                if (!string.IsNullOrWhiteSpace(textResult))
                {
                    LogTo.Debug($"Updater response is: {textResult}");
                    return JsonConvert.DeserializeObject<UpdateCheckResult>(textResult);
                }

                LogTo.Info("Got no meaningful response from updater");
                return null;
            }, cancellationToken);
        }

        public async Task PerformUpdate(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(_updateExePath))
            {
                LogTo.Warn($"Update.exe not found. Expected it to be located at {_updateExePath}");
                return;
            }

            var latestUpdateUrl = await GetLatestUpdateUrl();

            await Task.Run(() =>
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _updateExePath,
                    RedirectStandardOutput = true,
                    Arguments = $"--update={latestUpdateUrl}",
                    UseShellExecute = false
                };

                var p = new Process
                {
                    StartInfo = startInfo
                };
                p.OutputDataReceived += (s, e) => { Debug.WriteLine($"Updating: {e.Data}"); };

                p.Start();
                p.BeginOutputReadLine();
                p.WaitForExit();
            }, cancellationToken);

            _notifier.ShowNotifications(new[] {new UpdateNotification()});
        }

        private readonly Dictionary<string, string> _updateUrlCache = new Dictionary<string, string>();
        private readonly bool _includePreReleases;
        private readonly INotifier _notifier;
        private readonly string _packagesFolder;
        private readonly string _updateExePath;
        private const string AppName = "BuildNotifications";
        private const string UpdateUrl = "https://github.com/grollmus/BuildNotifications";

        [DataContract]
        private class Release
        {
            public Release()
            {
                HtmlUrl = string.Empty;
            }

            [DataMember(Name = "html_url")]
            public string HtmlUrl { get; set; }

            // ReSharper disable once StringLiteralTypo
            [DataMember(Name = "prerelease")]
            public bool PreRelease { get; set; }

            [DataMember(Name = "published_at")]
            public DateTime PublishedAt { get; set; }
        }
    }
}