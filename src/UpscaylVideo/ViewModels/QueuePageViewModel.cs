using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using UpscaylVideo.Models;
using UpscaylVideo.Services;

namespace UpscaylVideo.ViewModels;

public partial class QueuePageViewModel : PageBase
{
    public ObservableCollection<UpscaleJob> JobQueue => JobProcessingService.Instance.JobQueue;

    [RelayCommand]
    private void RemoveJob(UpscaleJob job)
    {
        JobProcessingService.Instance.RemoveJob(job);
    }

    [RelayCommand]
    private void ClearQueue()
    {
        JobProcessingService.Instance.ClearQueue();
    }

    private Button? _startButton;
    private Button? _cancelButton;

    public QueuePageViewModel() : base("Job Queue")
    {
        _startButton = CreateToolButton(MaterialIconKind.PlayArrow, "Start", StartCommand, toolTip: "Start", showText: true);
        _cancelButton = CreateToolButton(MaterialIconKind.Cancel, "Cancel", CancelJobCommand!, toolTip: "Cancel", showText: true);
        var backBtn = CreateToolButton(MaterialIconKind.ArrowBack, "Back", BackCommand, toolTip: "Back", showText: false);
        var clearBtn = CreateToolButton(MaterialIconKind.Delete, "Clear Queue", ClearQueueCommand, toolTip: "Clear Queue", showText: false);

        _startButton.IsVisible = CanStart();
        _cancelButton.IsVisible = JobProcessingService.Instance.IsProcessing;

        LeftToolStripControls = new Control[] { backBtn };
        RightToolStripControls = new Control[] { _startButton, _cancelButton, clearBtn };

        // Update Start and Cancel button visibility when queue or processing state changes
        JobProcessingService.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(JobProcessingService.IsProcessing))
            {
                UpdateStartButtonVisibility();
                UpdateCancelButtonVisibility();
            }
        };
        JobProcessingService.Instance.JobQueue.CollectionChanged += (s, e) => UpdateStartButtonVisibility();
    }

    [RelayCommand]
    private void Start()
    {
        // If not already processing, trigger the queue processor directly
        JobProcessingService.Instance.StartQueueIfStopped();
    }

    private bool CanStart() => !JobProcessingService.Instance.IsProcessing && JobProcessingService.Instance.JobQueue.Any();

    private void UpdateStartButtonVisibility()
    {
        if (_startButton != null)
            _startButton.IsVisible = CanStart();
    }

    [RelayCommand]
    private async Task CancelJob()
    {
        await JobProcessingService.Instance.CancelCurrentJobAsync();
    }


    private void UpdateCancelButtonVisibility()
    {
        if (_cancelButton != null)
            _cancelButton.IsVisible = JobProcessingService.Instance.IsProcessing;
    }

    [RelayCommand]
    private void Back()
    {
        PageManager.Instance.SetPage(typeof(MainPageViewModel));
    }

    [ObservableProperty] private UpscaleJob? _selectedJob;
}
