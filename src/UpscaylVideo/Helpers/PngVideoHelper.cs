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
    // private Process? _ffmpegProcess;
    // private Thread? _ffmpegProcessTask;
    private Queue<string> _frameQueue = new();
    private bool _isRuning = false;
    private bool _shouldStop = false;
    private readonly string _outputPath;
    private readonly double _framerate;
    private readonly double? _frameInterpolationFps;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly CancellationToken _cancellationToken;
    private readonly string _imageFormat;


    public PngVideoHelper(string outputPath, double framerate, CancellationToken parentCancellationToken, string imageFormat = "png", double? frameInterpolationFps = null)
    {
        _outputPath = outputPath;
        _framerate = framerate;
        _frameInterpolationFps = frameInterpolationFps;
        _imageFormat = string.IsNullOrWhiteSpace(imageFormat) ? "png" : imageFormat.ToLowerInvariant();
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(parentCancellationToken);
        _cancellationToken = _cancellationTokenSource.Token;
    }


    public string OutputPath => _outputPath;

    public double Framerate => _framerate;

    public bool IsRunning => _isRuning;

    public Exception? LastError { get; private set; }

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
        (var ffProcess, var ffmpegStream) = FFMpeg.StartFramesToVideoPipe(_outputPath, Framerate, _imageFormat, null, _frameInterpolationFps);

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
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
            {
                LastError = ex;
            }
            // Ignore cancellation
        }

        ffmpegStream.Dispose();
        
        ffProcess.WaitForExit();
        _isRuning = false;
    }

    private void AddFrames(string framePath, Stream outVideoStream)
    {
        var pattern = $"*.{_imageFormat}";
        var frameFiles = Directory.GetFiles(framePath, pattern);
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