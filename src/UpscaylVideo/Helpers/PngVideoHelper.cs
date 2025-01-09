using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UpscaylVideo.FFMpegWrap;
using UpscaylVideo.FFMpegWrap.Models.Probe;

namespace UpscaylVideo.Helpers;

public class PngVideoHelper : IDisposable
{
    private Process? _ffmpegProcess;
    private Thread? _ffmpegProcessTask;
    private Queue<string> _frameQueue = new();
    private bool _isRuning = false;
    private bool _shouldStop = false;
    private readonly string _outputPath;
    private readonly double _framerate;
    private readonly CancellationToken _parentCancellationToken;
    private readonly double? _frameInterpolationFps;
    private CancellationTokenSource _cancellationTokenSource;
    private CancellationToken _cancellationToken;


    public PngVideoHelper(string outputPath, double framerate, CancellationToken parentCancellationToken, double? frameInterpolationFps = null)
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

    public async Task StartAsync()
    {
        if (_isRuning)
            return;
        ThreadPool.QueueUserWorkItem(QueueRunner);
        var delayToken = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!_isRuning)
        {
            await Task.Delay(100, delayToken.Token).ConfigureAwait(false);
        }
    }

    private void QueueRunner(object? state)
    {
        _isRuning = true;
        (var ffProcess, var ffmpegStream) = FFMpeg.StartPngFramesToVideoPipe(_outputPath, Framerate, null, _frameInterpolationFps);

        try
        {
            while (!_cancellationToken.IsCancellationRequested && ffProcess.HasExited == false)
            {
                if (_frameQueue.TryDequeue(out var path))
                {
                    AddFrames(path, ffmpegStream);
                }

                if (_shouldStop && !_frameQueue.Any())
                    break;

                Thread.Sleep(1000);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }

        ffmpegStream.Dispose();
        
        ffProcess.WaitForExit();
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
        _shouldStop = true;
        while (_isRuning)
        {
            await TaskHelpers.Wait(500, _cancellationToken);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
    }
}