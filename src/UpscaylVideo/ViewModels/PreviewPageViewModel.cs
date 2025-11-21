using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using UpscaylVideo.Helpers;
using UpscaylVideo.Services;

namespace UpscaylVideo.ViewModels;

public partial class PreviewPageViewModel : PageBase, IDisposable
{
    // Disable vertical scrolling for this page so the preview fits in the available row
    public override Avalonia.Controls.Primitives.ScrollBarVisibility VerticalScrollBarVisibility => Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;

    [ObservableProperty] private Bitmap? _beforeImage;
    [ObservableProperty] private Bitmap? _afterImage;
    [ObservableProperty] private double _splitPosition = 0.5; // 0..1
    [ObservableProperty] private bool _autoRefresh;
    [ObservableProperty] private int _secondsUntilRefresh = 30;
    [ObservableProperty] private bool _hasFrames;
    [ObservableProperty] private string? _beforeFileName;
    [ObservableProperty] private string? _afterFileName;
    [ObservableProperty] private int _currentFrameIndex = 0;
    [ObservableProperty] private int _totalFrames = 0;
    [ObservableProperty] private string _selectedRefreshInterval = "30s";
    [ObservableProperty] private int _customRefreshInterval = 30;
    [ObservableProperty] private bool _isCustomRefreshInterval = false;

    private string? _currentBeforePath;
    private string? _currentAfterPath;
    private CancellationTokenSource? _refreshCts;
    private readonly JobProcessingService _jobService = JobProcessingService.Instance;
    
    // Refresh interval options for ComboBox
    public string[] RefreshIntervalOptions { get; } = [
        "5s",
        "10s", 
        "15s",
        "30s",
        "60s",
        "Custom"
    ];

    // Debouncing for frame navigation to prevent UI freezing
    private CancellationTokenSource? _frameNavigationCts;
    private string[]? _cachedFrameFiles;
    
    // Periodic cache refresh to pick up new frames
    private CancellationTokenSource? _cacheRefreshCts;
    private const int CacheRefreshIntervalSeconds = 3;

    public PreviewPageViewModel() : base(UpscaylVideo.Localization.PreviewPage_Title)
    {
        var backBtn = CreateToolButton(MaterialIconKind.ArrowBack, UpscaylVideo.Localization.QueuePageView_Back, BackCommand, toolTip: UpscaylVideo.Localization.QueuePageView_Back);
        LeftToolStripControls = [backBtn];

        // Initialize refresh interval from config
        var configInterval = Models.AppConfiguration.Instance.PreviewAutoRefreshInterval;
        SelectedRefreshInterval = configInterval switch
        {
            5 => "5s",
            10 => "10s", 
            15 => "15s",
            30 => "30s",
            60 => "60s",
            _ => "Custom"
        };
        if (SelectedRefreshInterval == "Custom")
        {
            CustomRefreshInterval = configInterval;
            IsCustomRefreshInterval = true;
        }

        // If processing ends while on this page, go back.
        _jobService.PropertyChanged += JobServiceOnPropertyChanged;
    }

    public override async void OnAppearing()
    {
        base.OnAppearing();
        // Clear any cached data from previous sessions
        _cachedFrameFiles = null;
        CurrentFrameIndex = 0;
        TotalFrames = 0;
        
        // Load latest available frames
        await StepAsync(int.MaxValue);
        UpdateAutoRefresh();
        StartCacheRefresh();
    }

    public override void OnDisappearing(PageDisappearingArgs args)
    {
        base.OnDisappearing(args);
        StopAutoRefresh();
        StopCacheRefresh();
        DisposeImages();
        _cachedFrameFiles = null; // Clear cache when leaving page
        _jobService.PropertyChanged -= JobServiceOnPropertyChanged;
        args.ShouldDispose = true;
    }

    private void JobServiceOnPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(JobProcessingService.IsProcessing) && !_jobService.IsProcessing)
        {
            CommonHelpers.TryPostToMainThread(() => UpscaylVideo.Services.PageManager.Instance.SetPage(typeof(MainPageViewModel)));
        }
    }

    partial void OnAutoRefreshChanged(bool value)
    {
        UpdateAutoRefresh();
    }

    partial void OnSelectedRefreshIntervalChanged(string value)
    {
        IsCustomRefreshInterval = value == "Custom";
        if (!IsCustomRefreshInterval)
        {
            var interval = value[..^1]; // Remove 's' suffix
            if (int.TryParse(interval, out var seconds))
            {
                CustomRefreshInterval = seconds;
                SaveRefreshIntervalToConfig();
                UpdateAutoRefresh();
            }
        }
    }

    partial void OnCustomRefreshIntervalChanged(int value)
    {
        if (IsCustomRefreshInterval)
        {
            SaveRefreshIntervalToConfig();
            UpdateAutoRefresh();
        }
    }

    private void UpdateAutoRefresh()
    {
        if (AutoRefresh)
        {
            if (_refreshCts != null) return;
            _refreshCts = new CancellationTokenSource();
            SecondsUntilRefresh = CustomRefreshInterval;
            _ = RunAutoRefreshAsync(_refreshCts.Token);
        }
        else
        {
            StopAutoRefresh();
        }
    }

    private void SaveRefreshIntervalToConfig()
    {
        Models.AppConfiguration.Instance.PreviewAutoRefreshInterval = CustomRefreshInterval;
        Models.AppConfiguration.Instance.Save();
    }

    private void StopAutoRefresh()
    {
        try { _refreshCts?.Cancel(); } catch { }
        _refreshCts?.Dispose();
        _refreshCts = null;
    }

    private void StartCacheRefresh()
    {
        if (_cacheRefreshCts != null) return;
        _cacheRefreshCts = new CancellationTokenSource();
        _ = RunCacheRefreshAsync(_cacheRefreshCts.Token);
    }

    private void StopCacheRefresh()
    {
        try { _cacheRefreshCts?.Cancel(); } catch { }
        _cacheRefreshCts?.Dispose();
        _cacheRefreshCts = null;
    }

    private async Task RunAutoRefreshAsync(CancellationToken token)
    {
        var tickStopwatch = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan waitTime = TimeSpan.FromSeconds(CustomRefreshInterval);
        while (!token.IsCancellationRequested)
        {
            // Jump to the most recent available frame
            if (tickStopwatch.Elapsed >= waitTime)
            {
                await StepAsync(int.MaxValue);
                tickStopwatch.Restart();
                SecondsUntilRefresh = CustomRefreshInterval;
                waitTime = TimeSpan.FromSeconds(CustomRefreshInterval); // Update in case it changed
            }
            // Use for countdown display
            var remaining = waitTime - tickStopwatch.Elapsed;
            var secs = (int)Math.Ceiling(Math.Max(0, remaining.TotalSeconds));
            if (secs != SecondsUntilRefresh)
            {
                SecondsUntilRefresh = secs;
            }
            try { await TaskHelpers.Wait(1_000, token); } catch { }
        }
    }

    private async Task RunCacheRefreshAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await TaskHelpers.Wait(CacheRefreshIntervalSeconds * 1000, token);
                
                // Only refresh if we're actively processing and have a job
                if (_jobService.IsProcessing && _jobService.CurrentJob?.WorkingFolder != null)
                {
                    await RefreshFrameCacheAsync();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Continue on any other errors
            }
        }
    }

    private async Task RefreshFrameCacheAsync()
    {
        var job = _jobService.CurrentJob;
        if (job?.WorkingFolder is null) return;
        
        var upscaleDir = _jobService.CurrentUpscaledBatchPath;
        if (!Directory.Exists(upscaleDir)) return;

        try
        {
            var imageFormat = (job.OutputImageFormat ?? "png").ToLowerInvariant();
            if (imageFormat == "jpeg") imageFormat = "jpg";

            var newFiles = Directory.GetFiles(upscaleDir)
                .OrderBy(f => ParseFrameNumber(Path.GetFileNameWithoutExtension(f)))
                .ToArray();

            // Only update if the count has changed
            if (newFiles.Length != TotalFrames)
            {
                _cachedFrameFiles = newFiles;
                TotalFrames = newFiles.Length;
                
                // If we don't have any current frame loaded, load the latest
                if (CurrentFrameIndex == 0 && newFiles.Length > 0)
                {
                    await LoadFrameAtIndexAsync(newFiles.Length - 1, newFiles);
                }
            }
        }
        catch
        {
            // Ignore errors during cache refresh to avoid disrupting UI
        }
    }

    [RelayCommand]
    private async Task StepLeft()
    {
        await StepAsync(-1);
    }

    [RelayCommand]
    private async Task StepRight()
    {
        await StepAsync(1);
    }

    [RelayCommand]
    private async Task SetFramePosition(int position)
    {
        // Debounce frame navigation to prevent UI freezing
        _frameNavigationCts?.Cancel();
        _frameNavigationCts = new CancellationTokenSource();
        
        try
        {
            await TaskHelpers.Wait(100, _frameNavigationCts.Token); // 100ms debounce
            await StepToIndexAsync(position);
        }
        catch (OperationCanceledException)
        {
            // Debounced, ignore
        }
    }

    private async Task StepAsync(int delta)
    {
        var job = _jobService.CurrentJob;
        if (job?.WorkingFolder is null) return;
        var upscaleDir = _jobService.CurrentUpscaledBatchPath;
        if (!Directory.Exists(upscaleDir)) return;

        // Cache file list for performance
        if (_cachedFrameFiles == null)
        {
            var imageFormat = (job.OutputImageFormat ?? "png").ToLowerInvariant();
            if (imageFormat == "jpeg") imageFormat = "jpg";

            _cachedFrameFiles = Directory.GetFiles(upscaleDir)
                .OrderBy(f => ParseFrameNumber(Path.GetFileNameWithoutExtension(f)))
                .ToArray();
        }

        var files = _cachedFrameFiles;
        if (files.Length == 0) return;

        // Update total frames if changed
        if (TotalFrames != files.Length)
        {
            TotalFrames = files.Length;
        }
        var isFirstLoad = _currentAfterPath == null;
        string? currentAfter = _currentAfterPath;
        int index = currentAfter != null ? Array.IndexOf(files, files.FirstOrDefault(f => f == currentAfter)) : files.Length - 1;
        if (index < 0) index = files.Length - 1;

        // Special deltas to jump to ends
        if (delta == int.MaxValue)
        {
            index = files.Length - 1;
            // Refresh cache on auto-refresh to pick up new frames
            _cachedFrameFiles = null;
        }
        else if (delta == int.MinValue)
        {
            index = 0;
        }
        else
        {
            try
            {
                checked
                {
                    index = Math.Clamp(index + delta, 0, files.Length - 1);
                }
            }
            catch (OverflowException)
            {
                index = delta > 0 ? files.Length - 1 : 0;
            }
        }

        await LoadFrameAtIndexAsync(index, files);
    }

    private async Task StepToIndexAsync(int index)
    {
        var job = _jobService.CurrentJob;
        if (job?.WorkingFolder is null) return;
        var upscaleDir = _jobService.CurrentUpscaledBatchPath;
        if (!Directory.Exists(upscaleDir)) return;

        // Use cached files or refresh if needed
        if (_cachedFrameFiles == null)
        {
            var imageFormat = (job.OutputImageFormat ?? "png").ToLowerInvariant();
            if (imageFormat == "jpeg") imageFormat = "jpg";

            _cachedFrameFiles = Directory.GetFiles(upscaleDir)
                .OrderBy(f => ParseFrameNumber(Path.GetFileNameWithoutExtension(f)))
                .ToArray();
        }

        var files = _cachedFrameFiles;
        if (files.Length == 0) return;

        if (TotalFrames != files.Length)
        {
            TotalFrames = files.Length;
        }

        index = Math.Clamp(index, 0, files.Length - 1);
        await LoadFrameAtIndexAsync(index, files);
    }

    private async Task LoadFrameAtIndexAsync(int index, string[] files)
    {
        var job = _jobService.CurrentJob;
        if (job?.WorkingFolder is null) return;

        var selectedAfter = files[index];
        // Update current frame index
        CurrentFrameIndex = index + 1; // 1-based for UI display

        // Derive before frame number from filename, fallback to closest  
        var fileName = Path.GetFileNameWithoutExtension(selectedAfter);
        var imageFormat = (job.OutputImageFormat ?? "png").ToLowerInvariant();
        if (imageFormat == "jpeg") imageFormat = "jpg";
        
        var beforeDir = Path.Combine(job.WorkingFolder, "Frames");
        var beforeFile = Path.Combine(beforeDir, fileName + "." + imageFormat);
        if (!File.Exists(beforeFile))
        {
            // fallback: choose closest lower frame
            var frames = Directory.GetFiles(beforeDir, $"*.{imageFormat}").OrderBy(f => f).ToArray();
            beforeFile = frames.LastOrDefault(f => string.Compare(Path.GetFileNameWithoutExtension(f), fileName, StringComparison.Ordinal) <= 0)
                         ?? frames.LastOrDefault();
        }

        await LoadImagesAsync(beforeFile, selectedAfter);
        var isFirstLoad = _currentAfterPath == null;
        if (isFirstLoad)
        {
            // Keep centered on first load
            SplitPosition = 0.5;
        }
    }

    private static long ParseFrameNumber(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return long.MinValue;
        return long.TryParse(name, out var n) ? n : long.MinValue;
    }

    private async Task LoadImagesAsync(string? beforePath, string? afterPath)
    {
        // Clear existing images first to release file handles
        DisposeImages();

        if (!string.IsNullOrWhiteSpace(beforePath) && File.Exists(beforePath))
        {
            await using var fs = File.Open(beforePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var ms = new MemoryStream();
            await fs.CopyToAsync(ms);
            ms.Position = 0;
            BeforeImage = new Bitmap(ms);
            _currentBeforePath = beforePath;
            BeforeFileName = Path.GetFileName(beforePath);
        }

        if (!string.IsNullOrWhiteSpace(afterPath) && File.Exists(afterPath))
        {
            await using var fs = File.Open(afterPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var ms = new MemoryStream();
            await fs.CopyToAsync(ms);
            ms.Position = 0;
            AfterImage = new Bitmap(ms);
            _currentAfterPath = afterPath;
            AfterFileName = Path.GetFileName(afterPath);
        }

        HasFrames = BeforeImage != null && AfterImage != null;
    }

    private void DisposeImages()
    {
        try { BeforeImage?.Dispose(); } catch { }
        try { AfterImage?.Dispose(); } catch { }
        BeforeImage = null;
        AfterImage = null;
    }

    [RelayCommand]
    private void Back()
    {
        UpscaylVideo.Services.PageManager.Instance.SetPage(typeof(MainPageViewModel));
    }

    public void Dispose()
    {
        StopAutoRefresh();
        StopCacheRefresh();
        _frameNavigationCts?.Cancel();
        _frameNavigationCts?.Dispose();
        DisposeImages();
        _jobService.PropertyChanged -= JobServiceOnPropertyChanged;
    }
}
