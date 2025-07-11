using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
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
    [ObservableProperty] private bool _isProcessing;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _processingTask;

    public void EnqueueJob(UpscaleJob job)
    {
        JobQueue.Add(job);
        ShowProgressPanel = true;
        // Start processing if not already running
        if (!IsProcessing)
            _processingTask = Task.Run(ProcessQueueIfNeeded);
    }

    /// <summary>
    /// Public method to start processing the queue if not already running and jobs exist.
    /// </summary>
    public void StartQueueIfStopped()
    {
        if (!IsProcessing && JobQueue.Count > 0)
        {
            _ = ProcessQueueIfNeeded();
        }
    }

    public void RemoveJob(UpscaleJob job)
    {
        // Prevent removing the currently running job
        if (job == CurrentJob)
            return;
        JobQueue.Remove(job);
    }

    public void ClearQueue()
    {
        // Only clear jobs that are not currently running
        for (int i = JobQueue.Count - 1; i >= 0; i--)
        {
            if (JobQueue[i] != CurrentJob)
                JobQueue.RemoveAt(i);
        }
        if (JobQueue.Count == 1 && JobQueue[0] == CurrentJob)
        {
            // Only current job remains
            ShowProgressPanel = true;
        }
        else if (JobQueue.Count == 0)
        {
            ShowProgressPanel = false;
            OverallProgress = 0;
        }
    }

    public void CancelCurrentJob()
    {
        _cancellationTokenSource?.Cancel();
    }

    private async Task ProcessQueueIfNeeded()
    {
        if (IsProcessing || JobQueue.Count == 0)
            return;
        IsProcessing = true;
        _cancellationTokenSource = new CancellationTokenSource();
        try
        {
            int index = 0;
            while (index < JobQueue.Count)
            {
                var job = JobQueue[index];
                CurrentJob = job;
                await RunJobAsync(job, _cancellationTokenSource.Token);
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    // Do not remove current job, just stop processing
                    break;
                }
                // Only remove if not cancelled
                index++;
            }
        }
        finally
        {
            IsProcessing = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            if (JobQueue.Count == 0 || (JobQueue.Count == 1 && JobQueue[0] == CurrentJob))
                ShowProgressPanel = false;
            OverallProgress = 0;
        }
    }

    private async Task RunJobAsync(UpscaleJob job, CancellationToken cancellationToken)
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
        await jobVM.RunAsync(cancellationToken);
    }
}
