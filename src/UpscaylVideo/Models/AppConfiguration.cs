using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using UpscaylVideo.FFMpegWrap;
using UpscaylVideo.Helpers;

namespace UpscaylVideo.Models;

public partial class AppConfiguration : ObservableValidator
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
    
    // New: last-used image format (-f) and tile size (-t)
    public string LastImageFormat { get; set; } = "png";
    public int LastTileSize { get; set; } = 31;
    
    
    
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

    // Global upscayl threads config (-j load:proc:save). Examples: 1:2:2 or 1:2,2,2:2
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [RegularExpression("^\\d+:\\d+(?:,\\d+)*:\\d+$", ErrorMessage = "Invalid thread format")] // UI also shows localized message
    private string _upscaylThreadConfig = "1:2:2";

    // Hacky way to allow serialization of UpscaylThreadConfig with source generation
    [JsonInclude, JsonPropertyName("UpscaylThreadConfig")]
    public string UpscaylThreadConfigSerialization
    {
        get => UpscaylThreadConfig;
        set => UpscaylThreadConfig = value;
    }
    
    partial void OnUpscaylThreadConfigChanged(string value)
    {
        IsUpscaylThreadConfigInvalid = HasErrors && GetErrors(nameof(UpscaylThreadConfig)).Any();
    }

    [JsonIgnore, ObservableProperty]
    private bool _isUpscaylThreadConfigInvalid = false;

    public void Save()
    {
        ConfigurationHelper.SaveConfig(this, AppConfigurationJsonContext.Default.AppConfiguration);
    }

    public void ValidateAll()
    {
        ValidateAllProperties();
        OnPropertyChanged(nameof(IsUpscaylThreadConfigInvalid));
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfiguration))]
public partial class AppConfigurationJsonContext : JsonSerializerContext
{
}