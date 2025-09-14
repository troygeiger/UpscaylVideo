using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Material.Icons;
using Material.Icons.Avalonia;

namespace UpscaylVideo.ViewModels;

public abstract class PageBase : ViewModelBase
{
    public PageBase()
    {
        
    }

    public PageBase(string title)
    {
        PageTitle = title;
    }
    
    public IEnumerable<Control> LeftToolStripControls { get; protected set; } = [];
    
    public IEnumerable<Control> RightToolStripControls { get; protected set; } = [];
    
    public string PageTitle { get; protected set; } = string.Empty;

    public virtual void OnAppearing()
    {
        
    }

    public virtual void OnDisappearing(PageDisappearingArgs args)
    {
        
    }

    // Allow pages to control the main ScrollViewer's vertical behavior (default Auto)
    public virtual ScrollBarVisibility VerticalScrollBarVisibility => ScrollBarVisibility.Auto;

    // Helper to create a toolbar button with Material icon and optional text
    protected Button CreateToolButton(MaterialIconKind iconKind, string text, System.Windows.Input.ICommand command, string? toolTip = null, bool showText = false)
        => CreateToolButton(iconKind, text, command, out _, toolTip, showText);

    protected Button CreateToolButton(MaterialIconKind iconKind, string text, System.Windows.Input.ICommand command, out TextBlock? textBlock, string? toolTip = null, bool showText = false)
    {
        var icon = new MaterialIcon
        {
            Kind = iconKind,
            Width = 24,
            Height = 24
        };
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };
        panel.Children.Add(icon);
        textBlock = null;
        if (showText)
        {
            textBlock = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            panel.Children.Add(textBlock);
        }

        var button = new Button
        {
            Content = panel,
            Command = command,
            Padding = new Thickness(8),
            Margin = new Thickness(3, 0, 3, 0)
        };
        if (!string.IsNullOrWhiteSpace(toolTip))
        {
            ToolTip.SetTip(button, toolTip);
        }
        return button;
    }

    protected SplitButton CreateSplitButton(MaterialIconKind iconKind, string text, System.Windows.Input.ICommand command, IEnumerable<MenuItem> menuItems, string? toolTip = null, bool showText = false)
    {
        var icon = new MaterialIcon
        {
            Kind = iconKind,
            Width = 24,
            Height = 24
        };
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };
        panel.Children.Add(icon);
        if (showText)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            panel.Children.Add(textBlock);
        }

        var button = new SplitButton
        {
            Content = panel,
            Command = command,
            Padding = new Thickness(8),
            Margin = new Thickness(3, 0, 3, 0),
            Flyout = new MenuFlyout()
            {
                ItemsSource = menuItems
            },
        };
        if (!string.IsNullOrWhiteSpace(toolTip))
        {
            ToolTip.SetTip(button, toolTip);
        }
        return button;
    }

    protected MenuItem CreateMenuItem(string header, MaterialIconKind? iconKind, System.Windows.Input.ICommand command)
    {
        return new MenuItem
        {
            Header = header,
            Icon = iconKind.HasValue ? new MaterialIcon { Kind = iconKind.Value, Width = 20, Height = 20 } : null,
            Command = command
        };
    }

    public class PageDisappearingArgs
    {
        public bool Cancel { get; set; }
        
        public bool ShouldDispose { get; set; }
    }
}