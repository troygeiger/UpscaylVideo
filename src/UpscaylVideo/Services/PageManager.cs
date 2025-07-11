using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using UpscaylVideo.Models;
using UpscaylVideo.ViewModels;

namespace UpscaylVideo.Services;

public partial class PageManager : ObservableObject
{
    public static PageManager Instance { get; } = new();

    private readonly Dictionary<Type, ViewModelBase> _loadedPages = new();

    // Reference the job queue service for queue/progress
    public JobQueueService JobQueueService => JobQueueService.Instance;

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
            if (viewModel == null)
                return;
            _loadedPages.Add(pageType, viewModel);
        }

        SetPage(viewModel);
    }

    public void SetPage(ViewModelBase? viewModel)
    {
        if (viewModel is null)
            return;

        if (CurrentPage is PageBase currentPage && NotifyClosePage(currentPage))
            return;

        CurrentPage = viewModel;
        
        string? pageTitle = null;
        
        if (viewModel is PageBase page)
        {
            LeftToolbarButtons = page.ToolStripButtonDefinitions.Where(d => d.Location == ToolStripButtonLocations.Left).ToArray();
            RightToolbarButtons = page.ToolStripButtonDefinitions.Where(d => d.Location == ToolStripButtonLocations.Right).ToArray();
            pageTitle = page.PageTitle;
            page.OnAppearing();
        }
        
        Title = string.IsNullOrEmpty(pageTitle) ? GlobalConst.AppTitle : $"{GlobalConst.AppTitle} - {pageTitle}";
    }

    /// <summary>
    /// Notifies the page that it is closing page. Returns true if cancelled by page.
    /// </summary>
    /// <param name="page"></param>
    /// <returns></returns>
    private bool NotifyClosePage(PageBase page)
    {
        var pageType = page.GetType();
        var args = new PageBase.PageDisappearingArgs();
        page.OnDisappearing(args);

        if (args.ShouldDispose && _loadedPages.ContainsKey(pageType))
        {
            _loadedPages.Remove(pageType);
        }

        if (args.ShouldDispose && page is IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        return args.Cancel;
    }
}
