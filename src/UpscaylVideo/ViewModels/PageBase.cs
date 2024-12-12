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

    public virtual void OnAppearing()
    {
        
    }

    public virtual void OnDisappearing(PageDisappearingArgs args)
    {
        
    }

    public class PageDisappearingArgs
    {
        public bool Cancel { get; set; }
        
        public bool ShouldDispose { get; set; }
    }
}