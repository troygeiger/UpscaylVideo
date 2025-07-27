using System;
using System.Text.Json.Serialization;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using UpscaylVideo.FFMpegWrap;
using UpscaylVideo.Helpers;

namespace UpscaylVideo.Models;

public partial class AppConfiguration : ObservableObject
{
    public const string DefaultOutputFileNameTemplate = "{{OriginalFile}}-upscaled{{OriginalExtension}}";
    private static AppConfiguration? _instance;

    public static AppConfiguration Instance
    {
        get
        {
            if (_instance is null)
                _instance = ConfigurationHelper.LoadConfig(AppConfigurationJsonContext.Default.AppConfiguration);
            return _instance;
        }
    }
    
    private string? _upscaylPath;

    public string? UpscaylPath
    {
        get => _upscaylPath;
        set => SetProperty(ref _upscaylPath, value);
    }

    public string? FFmpegBinariesPath
    {
        get => FFMpegOptions.Global.FFMpegFolder;
        set
        {
            FFMpegOptions.Global.FFMpegFolder = value ?? string.Empty;
            OnPropertyChanged(nameof(FFmpegBinariesPath));  
        } 
    }
    
    private int[] _gpuNumbers = [];

    public int[] GpuNumbers
    {
        get => _gpuNumbers;
        set => SetProperty(ref _gpuNumbers, value);
    }

    public int LastScale { get; set; } = 4;

    public int LastUpscaleFrameChunkSize { get; set; } = 1000;
    
    
    
    public string? LastModelUsed { get; set; }

    private string? _tempWorkingFolder;
    public string? TempWorkingFolder
    {
        get => _tempWorkingFolder;
        set => SetProperty(ref _tempWorkingFolder, value);
    }
    
    private string? _outputPath;
    public string? OutputPath
    {
        get => _outputPath;
        set => SetProperty(ref _outputPath, value);
    }

    private string _outputFileNameTemplate = DefaultOutputFileNameTemplate;
    public string OutputFileNameTemplate
    {
        get => _outputFileNameTemplate;
        set => SetProperty(ref _outputFileNameTemplate, value);
    }

    public Uri? LastBrowsedVideoPath { get; set; }
    
    public Uri? LastBrowsedWorkingFolder { get; set; }
    
    public Uri? LastBrowsedOutputPath { get; set; }

    public void Save()
    {
        ConfigurationHelper.SaveConfig(this, AppConfigurationJsonContext.Default.AppConfiguration);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfiguration))]
public partial class AppConfigurationJsonContext : JsonSerializerContext
{
}