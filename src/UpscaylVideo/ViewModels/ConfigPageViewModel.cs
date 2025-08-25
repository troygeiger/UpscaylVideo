using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
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
        // Build toolstrip controls
        var backBtn = CreateToolButton(MaterialIconKind.ArrowBack, "Back", BackCommand, toolTip: "Back", showText: false);
        var applyBtn = CreateToolButton(MaterialIconKind.Check, "Apply", ApplyCommand, toolTip: "Apply", showText: false);

        LeftToolStripControls = new Control[] { backBtn };
        RightToolStripControls = new Control[] { applyBtn };
    }

    public override void OnAppearing()
    {
        this.Configuration = AppConfiguration.Instance;
        // Force initial validation so Apply CanExecute reflects current state
        Configuration.ValidateAll();
        ApplyCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void Back()
    {
        UpscaylVideo.Services.PageManager.Instance.SetPage(typeof(MainPageViewModel));
    }

    private bool CanApply()
    {
        return !Configuration.HasErrors;
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        ConfigurationHelper.SaveConfig(Configuration, AppConfigurationJsonContext.Default.AppConfiguration);
        Back();
    }

    private async Task<Uri?> BrowseFolder(Uri? startingFolder = null)
    {
        var provider = App.Window?.StorageProvider;
        if (provider is null)
            return null;
        var startingLocation = await startingFolder.TryGetStorageFolderAsync(provider);
        var folder = await provider.OpenFolderPickerAsync(new()
        {
            SuggestedStartLocation = startingLocation,
        });
        
        if (!folder.Any())
            return null;
        
        return folder.First().Path;
    }
    
    [RelayCommand]
    private async Task BrowseUpscayl()
    {
        var result = await BrowseFolder();
        if (result is null)
            return;

        Configuration.UpscaylPath = result.ToUnescapedAbsolutePath();
    }

    [RelayCommand]
    private async Task BrowseFFMpeg()
    {
        var result = await BrowseFolder();
        if (result is null)
            return;

        Configuration.FFmpegBinariesPath = result.ToUnescapedAbsolutePath();
    }

    [RelayCommand]
    private async Task BrowseWorkingFolder()
    {
        var result = await BrowseFolder(Configuration.LastBrowsedWorkingFolder);
        if (result is null)
            return;

        Configuration.TempWorkingFolder = result.ToUnescapedAbsolutePath();
        Configuration.LastBrowsedWorkingFolder = result;
    }

    [RelayCommand]
    private async Task BrowseOutputPath()
    {
        var result = await BrowseFolder(Configuration.LastBrowsedOutputPath);
        if (result is null)
            return;
        Configuration.OutputPath = result.ToUnescapedAbsolutePath();
        Configuration.LastBrowsedOutputPath = result;
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

    partial void OnConfigurationChanged(AppConfiguration value)
    {
        OnPropertyChanged(nameof(IsOutputFileNameTemplateCustom));
        value.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AppConfiguration.OutputFileNameTemplate))
                OnPropertyChanged(nameof(IsOutputFileNameTemplateCustom));
        };
        value.ErrorsChanged += (s, e) =>
        {
            ApplyCommand.NotifyCanExecuteChanged();
        };
    }

    [RelayCommand]
    private void ResetOutputFileNameTemplate()
    {
        Configuration.OutputFileNameTemplate = AppConfiguration.DefaultOutputFileNameTemplate;
        OnPropertyChanged(nameof(IsOutputFileNameTemplateCustom));
    }
}