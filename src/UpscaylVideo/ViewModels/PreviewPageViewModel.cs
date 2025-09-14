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

    private string? _currentBeforePath;
    private string? _currentAfterPath;
    private CancellationTokenSource? _refreshCts;
    private readonly JobProcessingService _jobService = JobProcessingService.Instance;

    public PreviewPageViewModel() : base(UpscaylVideo.Localization.PreviewPage_Title)
    {
        var backBtn = CreateToolButton(MaterialIconKind.ArrowBack, UpscaylVideo.Localization.QueuePageView_Back, BackCommand, toolTip: UpscaylVideo.Localization.QueuePageView_Back);
        LeftToolStripControls = [backBtn];

        // If processing ends while on this page, go back.
        _jobService.PropertyChanged += JobServiceOnPropertyChanged;
    }

    public override async void OnAppearing()
    {
        base.OnAppearing();
        // Load latest available frames
        await StepAsync(int.MaxValue);
        UpdateAutoRefresh();
    }

    public override void OnDisappearing(PageDisappearingArgs args)
    {
        base.OnDisappearing(args);
        StopAutoRefresh();
        DisposeImages();
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

    private void UpdateAutoRefresh()
    {
        if (AutoRefresh)
        {
            if (_refreshCts != null) return;
            _refreshCts = new CancellationTokenSource();
            SecondsUntilRefresh = 30;
            _ = RunAutoRefreshAsync(_refreshCts.Token);
        }
        else
        {
            StopAutoRefresh();
        }
    }

    private void StopAutoRefresh()
    {
        try { _refreshCts?.Cancel(); } catch { }
        _refreshCts?.Dispose();
        _refreshCts = null;
    }

    private async Task RunAutoRefreshAsync(CancellationToken token)
    {
        var tickStopwatch = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan waitTime = TimeSpan.FromSeconds(30);
        while (!token.IsCancellationRequested)
        {
            // Jump to the most recent available frame
            if (tickStopwatch.Elapsed >= waitTime)
            {
                await StepAsync(int.MaxValue);
                tickStopwatch.Restart();
                SecondsUntilRefresh = 30;
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

    private async Task StepAsync(int delta)
    {
        var job = _jobService.CurrentJob;
        if (job?.WorkingFolder is null) return;
        var upscaleDir = _jobService.CurrentUpscaledBatchPath;
        if (!Directory.Exists(upscaleDir)) return;

        // Find all chunk folders and files
        var imageFormat = (job.OutputImageFormat ?? "png").ToLowerInvariant();
        if (imageFormat == "jpeg") imageFormat = "jpg";

        var files = Directory.GetFiles(upscaleDir)
            .OrderBy(f => ParseFrameNumber(Path.GetFileNameWithoutExtension(f)))
            .ToArray();
        if (files.Length == 0) return;
        var isFirstLoad = _currentAfterPath == null;
        string? currentAfter = _currentAfterPath;
        int index = currentAfter != null ? Array.IndexOf(files, files.FirstOrDefault(f => f == currentAfter)) : files.Length - 1;
        if (index < 0) index = files.Length - 1;

        // Special deltas to jump to ends
        if (delta == int.MaxValue)
        {
            index = files.Length - 1;
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

        var selectedAfter = files[index];
        // Derive before frame number from filename, fallback to closest
        var fileName = Path.GetFileNameWithoutExtension(selectedAfter);
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
        DisposeImages();
        _jobService.PropertyChanged -= JobServiceOnPropertyChanged;
    }
}
