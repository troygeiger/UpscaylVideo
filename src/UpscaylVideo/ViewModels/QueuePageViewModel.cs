using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using UpscaylVideo.Models;
using UpscaylVideo.Services;

namespace UpscaylVideo.ViewModels;

public partial class QueuePageViewModel : PageBase
{
    public ObservableCollection<UpscaleJob> JobQueue => JobQueueService.Instance.JobQueue;

    [RelayCommand]
    private void RemoveJob(UpscaleJob job)
    {
        JobQueueService.Instance.RemoveJob(job);
    }

    [RelayCommand]
    private void ClearQueue()
    {
        JobQueueService.Instance.ClearQueue();
    }

    public QueuePageViewModel() : base("Job Queue")
    {
        base.ToolStripButtonDefinitions =
        [
            new(ToolStripButtonLocations.Left, MaterialIconKind.ArrowBack, "Back", BackCommand),
            new(ToolStripButtonLocations.Right, MaterialIconKind.Delete, "Clear Queue", ClearQueueCommand)
        ];
    }

    [RelayCommand]
    private void Back()
    {
        
        PageManager.Instance.SetPage(typeof(MainPageViewModel));
    }

    [ObservableProperty] private UpscaleJob? _selectedJob;
}
