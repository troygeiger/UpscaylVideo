using System.Collections.Generic;
using UpscaylVideo.Models;

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
    
    public IEnumerable<ToolStripButtonDefinition> ToolStripButtonDefinitions { get; protected set; } = [];
    
    public string PageTitle { get; protected set; } = string.Empty;
}