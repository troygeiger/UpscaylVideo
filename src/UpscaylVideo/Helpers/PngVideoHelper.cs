using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UpscaylVideo.FFMpegWrap;

namespace UpscaylVideo.Helpers;

public class PngVideoHelper : IDisposable
{
    private Process? _ffmpegProcess;
    private Task _ffmpegProcessTask = Task.CompletedTask;
    private Queue<string> _frameQueue = new();
    private bool _isRuning = false;
    private readonly string _outputPath;
    private readonly double _framerate;
    private readonly CancellationToken _parentCancellationToken;
    private readonly int? _frameInterpolationFps;
    private CancellationTokenSource _cancellationTokenSource;
    private CancellationToken _cancellationToken;


    public PngVideoHelper(string outputPath, double framerate, CancellationToken parentCancellationToken, int? frameInterpolationFps = null)
    {
        _outputPath = outputPath;
        _framerate = framerate;
        _parentCancellationToken = parentCancellationToken;
        _frameInterpolationFps = frameInterpolationFps;
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_parentCancellationToken);
        _cancellationToken = _cancellationTokenSource.Token;
    }


    public string OutputPath => _outputPath;

    public double Framerate => _framerate;

    public bool IsRunning => _isRuning;

    public void EnqueueFramePath(string framePath)
    {
        _frameQueue.Enqueue(framePath);
    }

    public void Start()
    {
        if (_isRuning)
            return;
        _isRuning = true;
        _ffmpegProcessTask = QueueRunner();
    }

    private async Task QueueRunner()
    {
        (var ffProcess, var ffmpegStream) = FFMpeg.StartPngFramesToVideoPipe(_outputPath, _framerate, null, _frameInterpolationFps);

        try
        {
            while (!_cancellationToken.IsCancellationRequested && ffProcess.HasExited == false)
            {
                if (_frameQueue.TryDequeue(out var path))
                {
                    AddFrames(path, ffmpegStream);
                }
                
                if (_isRuning == false && !_frameQueue.Any())
                    break;

                await TaskHelpers.Wait(1000, _cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        ffmpegStream.Dispose();
        await ffProcess.WaitForExitAsync(_cancellationToken);
        _isRuning = false;
    }

    private void AddFrames(string framePath, Stream outVideoStream)
    {
        var frameFiles = Directory.GetFiles(framePath, "*.png");
        Array.Sort(frameFiles);

        foreach (var frameFile in frameFiles)
        {
            using var frameStream = File.OpenRead(frameFile);
            frameStream.CopyTo(outVideoStream);
        }
        
        Directory.Delete(framePath, true);
    }

    public async Task CompleteAsync()
    {
        _isRuning = false;
        await _ffmpegProcessTask.ConfigureAwait(false);
    }

    public Task WaitForCompleteAsync() => _ffmpegProcessTask;

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
    }
}