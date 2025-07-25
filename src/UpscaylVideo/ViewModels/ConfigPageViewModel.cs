using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using UpscaylVideo.Helpers;
using UpscaylVideo.Models;

namespace UpscaylVideo.ViewModels;

public partial class ConfigPageViewModel : PageBase
{
    [ObservableProperty] private AppConfiguration _configuration;
    
    public ConfigPageViewModel() : base("Configuration")
    {
        _configuration = new AppConfiguration();
        base.ToolStripButtonDefinitions =
        [
            new(ToolStripButtonLocations.Left, MaterialIconKind.ArrowBack, "Back", BackCommand),
            new(ToolStripButtonLocations.Right, MaterialIconKind.Check, "Apply", ApplyCommand)
        ];
    }

    public override void OnAppearing()
    {
        this.Configuration = AppConfiguration.Instance;
    }

    [RelayCommand]
    private void Back()
    {
        UpscaylVideo.Services.PageManager.Instance.SetPage(typeof(MainPageViewModel));
    }

    [RelayCommand]
    private void Apply()
    {
        ConfigurationHelper.SaveConfig(Configuration, AppConfigurationJsonContext.Default.AppConfiguration);
        Back();
    }

    private async Task<string?> BrowseFolder()
    {
        var provider = App.Window?.StorageProvider;
        if (provider is null)
            return null;
        var folder = await provider.OpenFolderPickerAsync(new()
        {
            
        });
        
        if (!folder.Any())
            return null;
        
        return Uri.UnescapeDataString(folder.First().Path.AbsolutePath);
    }
    
    [RelayCommand]
    private async Task BrowseUpscayl()
    {
        var result = await BrowseFolder();
        if (result is null)
            return;
        
        Configuration.UpscaylPath = Uri.UnescapeDataString(result);
    }

    [RelayCommand]
    private async Task BrowseFFMpeg()
    {
        var result = await BrowseFolder();
        if (result is null)
            return;
        
        Configuration.FFmpegBinariesPath = Uri.UnescapeDataString(result);
    }

    [RelayCommand]
    private async Task BrowseWorkingFolder()
    {
        var result = await BrowseFolder();
        if (result is null)
            return;

        Configuration.TempWorkingFolder = Uri.UnescapeDataString(result);
    }

    [RelayCommand]
    private async Task BrowseOutputPath()
    {
        var provider = App.Window?.StorageProvider;
        if (provider is null)
            return;
        var folder = await provider.OpenFolderPickerAsync(new());
        if (!folder.Any())
            return;
        Configuration.OutputPath = Uri.UnescapeDataString(folder.First().Path.AbsolutePath);
    }

    [RelayCommand]
    private void SetOutputFileNameTemplate(string? template)
    {
        if (!string.IsNullOrWhiteSpace(template))
        {
            Configuration.OutputFileNameTemplate = template;
        }
    }

    public bool IsOutputFileNameTemplateCustom =>
        !string.IsNullOrWhiteSpace(Configuration.OutputFileNameTemplate) &&
        Configuration.OutputFileNameTemplate.Trim() != AppConfiguration.DefaultOutputFileNameTemplate;

    partial void OnConfigurationChanged(AppConfiguration? value)
    {
        OnPropertyChanged(nameof(IsOutputFileNameTemplateCustom));
        if (value != null)
        {
            value.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AppConfiguration.OutputFileNameTemplate))
                    OnPropertyChanged(nameof(IsOutputFileNameTemplateCustom));
            };
        }
    }

    [RelayCommand]
    private void ResetOutputFileNameTemplate()
    {
        Configuration.OutputFileNameTemplate = AppConfiguration.DefaultOutputFileNameTemplate;
        OnPropertyChanged(nameof(IsOutputFileNameTemplateCustom));
    }
}