using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using UpscaylVideo.ViewModels;

namespace UpscaylVideo.Models;

public partial class PageManager : ObservableObject
{
    public static PageManager Instance { get; } = new();

    private readonly Dictionary<Type, ViewModelBase> _loadedPages = new();

    private PageManager()
    {
        SetPage(typeof(MainPageViewModel));
    }
    
    public IEnumerable<Type> AvailablePages { get; } =
    [
        typeof(MainPageViewModel)
    ];

    [ObservableProperty] private string _title = GlobalConst.AppTitle;

    [ObservableProperty] private ViewModelBase? _currentPage;

    [ObservableProperty] private IEnumerable<ToolStripButtonDefinition> _leftToolbarButtons = [];
    
    [ObservableProperty] private IEnumerable<ToolStripButtonDefinition> _rightToolbarButtons = [];

    public void SetPage(Type pageType)
    {
        if (!pageType.IsSubclassOf(typeof(ViewModelBase)))
            return;
        if (!_loadedPages.TryGetValue(pageType, out var viewModel))
        {
            viewModel = Activator.CreateInstance(pageType) as ViewModelBase;
        }

        SetPage(viewModel);
    }

    public void SetPage(ViewModelBase? viewModel)
    {
        if (viewModel is null)
            return;

        CurrentPage = viewModel;
        
        string? pageTitle = null;
        
        if (viewModel is PageBase page)
        {
            LeftToolbarButtons = page.ToolStripButtonDefinitions.Where(d => d.Location == ToolStripButtonLocations.Left).ToArray();
            RightToolbarButtons = page.ToolStripButtonDefinitions.Where(d => d.Location == ToolStripButtonLocations.Right).ToArray();
            pageTitle = page.PageTitle;
        }
        
        Title = string.IsNullOrEmpty(pageTitle) ? GlobalConst.AppTitle : $"{GlobalConst.AppTitle} - {pageTitle}";
    }
}