using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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

    private ToolStripButtonDefinition _startButton;
    private ToolStripButtonDefinition _cancelButton;

    public QueuePageViewModel() : base("Job Queue")
    {
        _startButton = new ToolStripButtonDefinition(ToolStripButtonLocations.Right, MaterialIconKind.PlayArrow, "Start", StartCommand)
        {
            ShowText = true,
            Visible = CanStart()
        };
        _cancelButton = new ToolStripButtonDefinition(ToolStripButtonLocations.Right, MaterialIconKind.Cancel, "Cancel", CancelJobCommand!)
        {
            ShowText = true,
            Visible = JobProcessingService.Instance.IsProcessing
        };
        base.ToolStripButtonDefinitions =
        [
            new(ToolStripButtonLocations.Left, MaterialIconKind.ArrowBack, "Back", BackCommand),
            _startButton,
            _cancelButton,
            new(ToolStripButtonLocations.Right, MaterialIconKind.Delete, "Clear Queue", ClearQueueCommand)
        ];

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
        _startButton.Visible = CanStart();
    }

    [RelayCommand]
    private async Task CancelJob()
    {
        await JobProcessingService.Instance.CancelCurrentJobAsync();
    }


    private void UpdateCancelButtonVisibility()
    {
        _cancelButton.Visible = JobProcessingService.Instance.IsProcessing;
    }

    [RelayCommand]
    private void Back()
    {
        PageManager.Instance.SetPage(typeof(MainPageViewModel));
    }

    [ObservableProperty] private UpscaleJob? _selectedJob;
}
