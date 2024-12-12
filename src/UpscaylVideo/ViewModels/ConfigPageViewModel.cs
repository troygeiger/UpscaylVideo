using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
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
        this.Configuration = ConfigurationHelper.LoadConfig(AppConfigurationJsonContext.Default.AppConfiguration);
    }

    [RelayCommand]
    private void Back()
    {
        PageManager.Instance.SetPage(typeof(MainPageViewModel));
    }

    [RelayCommand]
    private void Apply()
    {
        ConfigurationHelper.SaveConfig(Configuration, AppConfigurationJsonContext.Default.AppConfiguration);
        Back();
    }

    [RelayCommand]
    private async Task BrowseUpscayl()
    {
        var provider = App.Window?.StorageProvider;
        if (provider is null)
            return;
        var folder = await provider.OpenFolderPickerAsync(new()
        {
            
        });
        
        if (!folder.Any())
            return;
        
        Configuration.UpscaylPath = folder.First().Path.AbsolutePath;
    }
    
}