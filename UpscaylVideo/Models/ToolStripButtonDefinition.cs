using System.Windows.Input;
using Material.Icons;

namespace UpscaylVideo.Models;

public class ToolStripButtonDefinition(ToolStripButtonLocations Location, MaterialIconKind Icon, string Text, ICommand Command)
{
    public ToolStripButtonLocations Location { get; init; } = Location;

    public MaterialIconKind Icon { get; init; } = Icon;

    public string Text { get; init; } = Text;

    public ICommand Command { get; init; } = Command;
    
    public bool ShowText { get; init; } = false;

    public void Deconstruct(out ToolStripButtonLocations Location, out MaterialIconKind Icon, out string Text, out ICommand Command)
    {
        Location = this.Location;
        Icon = this.Icon;
        Text = this.Text;
        Command = this.Command;
    }
}