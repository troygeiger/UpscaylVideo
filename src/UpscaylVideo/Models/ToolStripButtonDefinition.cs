using System.Windows.Input;
using Material.Icons;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UpscaylVideo.Models;

public partial class ToolStripButtonDefinition : ObservableObject
{
    public ToolStripButtonDefinition(ToolStripButtonLocations location, MaterialIconKind icon, string text, ICommand command)
    {
        Location = location;
        Icon = icon;
        Text = text;
        Command = command;
    }

    public ToolStripButtonLocations Location { get; init; }
    public MaterialIconKind Icon { get; init; }
    [ObservableProperty] private string text;
    public ICommand Command { get; init; }
    public bool ShowText { get; init; } = false;

    public void Deconstruct(out ToolStripButtonLocations location, out MaterialIconKind icon, out string text, out ICommand command)
    {
        location = Location;
        icon = Icon;
        text = Text;
        command = Command;
    }
}