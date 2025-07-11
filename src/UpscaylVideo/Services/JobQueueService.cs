using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using UpscaylVideo.Models;
using UpscaylVideo.ViewModels;

namespace UpscaylVideo.Services;

public partial class JobQueueService : ObservableObject
{
    public static JobQueueService Instance { get; } = new();

    public ObservableCollection<UpscaleJob> JobQueue { get; } = new();
    [ObservableProperty] private bool _showProgressPanel;
    [ObservableProperty] private double _overallProgress;
    [ObservableProperty] private UpscaleJob? _currentJob;
    private bool _isProcessing;

    public void EnqueueJob(UpscaleJob job)
    {
        JobQueue.Add(job);
        ShowProgressPanel = true;
        //ProcessQueueIfNeeded();
    }

    public void RemoveJob(UpscaleJob job)
    {
        JobQueue.Remove(job);
    }

    public void ClearQueue()
    {
        JobQueue.Clear();
        ShowProgressPanel = false;
        OverallProgress = 0;
    }

    private async void ProcessQueueIfNeeded()
    {
        if (_isProcessing || JobQueue.Count == 0)
            return;
        _isProcessing = true;
        while (JobQueue.Count > 0)
        {
            CurrentJob = JobQueue[0];
            await RunJobAsync(CurrentJob);
            JobQueue.RemoveAt(0);
        }
        _isProcessing = false;
        ShowProgressPanel = false;
        OverallProgress = 0;
    }

    private async Task RunJobAsync(UpscaleJob job)
    {
        // For now, use JobPageViewModel for execution, but this will be replaced
        OverallProgress = 0;
        var jobVM = new JobPageViewModel(job);
        jobVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(jobVM.ProgressOverall))
            {
                OverallProgress = jobVM.ProgressOverall;
            }
        };
        await jobVM.RunAsync();
    }
}
