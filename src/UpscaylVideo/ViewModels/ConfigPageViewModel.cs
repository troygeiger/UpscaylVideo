using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using UpscaylVideo.Models;

namespace UpscaylVideo.ViewModels;

public partial class ConfigPageViewModel : PageBase
{
    public ConfigPageViewModel() : base("Configuration")
    {
        base.ToolStripButtonDefinitions =
        [
            new(ToolStripButtonLocations.Left, MaterialIconKind.ArrowBack, "Back", BackCommand),
            new(ToolStripButtonLocations.Right, MaterialIconKind.Check, "Apply", ApplyCommand)
        ];
    }

    [RelayCommand]
    private void Back()
    {
        PageManager.Instance.SetPage(typeof(MainPageViewModel));
    }

    [RelayCommand]
    private void Apply()
    {
    }
}