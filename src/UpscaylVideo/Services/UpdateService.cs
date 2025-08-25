using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UpscaylVideo.Services;

public partial class UpdateService : ObservableObject
{
    public static UpdateService Instance { get; } = new();

    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private bool _isUpdateAvailable;
    [ObservableProperty] private string? _currentVersion;
    [ObservableProperty] private string? _latestVersion;
    [ObservableProperty] private string? _latestReleaseUrl;
    [ObservableProperty] private string _statusMessage = string.Empty;

    private UpdateService()
    {
        try
        {
            TG.Common.AssemblyInfo.ReferenceAssembly = typeof(UpdateService).Assembly;
            CurrentVersion = TG.Common.AssemblyInfo.InformationVersion;
        }
        catch
        {
            // ignore
        }
    }

    public async Task CheckForUpdatesAsync(bool includePreReleases = false)
    {
        if (IsChecking) return;
        IsChecking = true;
        StatusMessage = Localization.Update_Checking;
        try
        {
            var owner = GlobalConst.RepoOwner;
            var repo = GlobalConst.RepoName;
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            {
                StatusMessage = Localization.Update_NotConfigured;
                return;
            }

            _http.DefaultRequestHeaders.UserAgent.ParseAdd("UpscaylVideo/UpdateCheck");

            GitHubRelease? latest = null;

            if (!includePreReleases)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/releases/latest");
                using var res = await _http.SendAsync(req);
                if (res.StatusCode == HttpStatusCode.NotFound)
                {
                    latest = await FetchLatestFromListAsync(owner, repo, allowPrerelease: true);
                }
                else
                {
                    res.EnsureSuccessStatusCode();
                    // Inline JSON to avoid temporary variable warning
                    latest = JsonSerializer.Deserialize<GitHubRelease>(await res.Content.ReadAsStringAsync(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
            }
            else
            {
                latest = await FetchLatestFromListAsync(owner, repo, allowPrerelease: true);
            }

            if (latest == null)
            {
                StatusMessage = Localization.Update_NoReleases;
                return;
            }

            ProcessRelease(latest);
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Localization.Update_Failed, ex.Message);
        }
        finally
        {
            IsChecking = false;
        }
    }

    private async Task<GitHubRelease?> FetchLatestFromListAsync(string owner, string repo, bool allowPrerelease)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/releases");
        using var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize<GitHubRelease[]>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? Array.Empty<GitHubRelease>();
        foreach (var r in list)
        {
            if (r.Draft) continue;
            if (!allowPrerelease && r.Prerelease) continue;
            return r;
        }
        foreach (var r in list)
        {
            if (!r.Draft) return r;
        }
        return null;
    }

    private void ProcessRelease(GitHubRelease latest)
    {
        LatestVersion = latest.TagName;
        LatestReleaseUrl = latest.HtmlUrl;

        var current = CurrentVersion;
        var cmp = CompareVersions(current, LatestVersion);

        if (cmp < 0)
        {
            IsUpdateAvailable = true;
            StatusMessage = string.Format(Localization.Update_Available, LatestVersion);
        }
        else
        {
            IsUpdateAvailable = false;
            StatusMessage = Localization.Update_UpToDate;
        }
    }

    public static string NormalizeVersion(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return "0.0.0";
        v = v.Trim();
        while (v.Length > 0 && !char.IsDigit(v[0])) v = v[1..];
        return v;
    }

    public static int CompareVersions(string? a, string? b)
    {
        // Treat null/whitespace as unknown (lower than any non-empty version)
        var aEmpty = string.IsNullOrWhiteSpace(a);
        var bEmpty = string.IsNullOrWhiteSpace(b);
        if (aEmpty && bEmpty) return 0;
        if (aEmpty) return -1;
        if (bEmpty) return 1;

        var aa = NormalizeVersion(a);
        var bb = NormalizeVersion(b);
        var ai = aa.IndexOf('-');
        var bi = bb.IndexOf('-');
        var acore = ai >= 0 ? aa[..ai] : aa;
        var bcore = bi >= 0 ? bb[..bi] : bb;
        if (Version.TryParse(acore, out var va) && Version.TryParse(bcore, out var vb))
        {
            var baseCmp = va.CompareTo(vb);
            if (baseCmp != 0) return baseCmp;
            var apre = ai >= 0 ? aa[(ai + 1)..] : null;
            var bpre = bi >= 0 ? bb[(bi + 1)..] : null;
            if (string.IsNullOrEmpty(apre) && string.IsNullOrEmpty(bpre)) return 0;
            if (string.IsNullOrEmpty(apre)) return 1;
            if (string.IsNullOrEmpty(bpre)) return -1;
            return ComparePrerelease(apre, bpre);
        }
        return string.CompareOrdinal(aa, bb);
    }

    private static int ComparePrerelease(string a, string b)
    {
        var aspan = a.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var bspan = b.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var len = Math.Max(aspan.Length, bspan.Length);
        for (int i = 0; i < len; i++)
        {
            var atok = i < aspan.Length ? aspan[i] : "0";
            var btok = i < bspan.Length ? bspan[i] : "0";
            var an = int.TryParse(atok, out var ai) ? (int?)ai : null;
            var bn = int.TryParse(btok, out var bi) ? (int?)bi : null;
            int cmp;
            if (an.HasValue && bn.HasValue)
                cmp = an.Value.CompareTo(bn.Value);
            else if (an.HasValue)
                cmp = -1;
            else if (bn.HasValue)
                cmp = 1;
            else
                cmp = string.CompareOrdinal(atok, btok);
            if (cmp != 0) return cmp;
        }
        return 0;
    }

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = string.Empty;
        [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = string.Empty;
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
    }
}
