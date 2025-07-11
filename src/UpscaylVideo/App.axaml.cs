using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using UpscaylVideo.Models;
using UpscaylVideo.ViewModels;
using UpscaylVideo.Views;
using HandlebarsDotNet;
using System;
using System.Globalization;

namespace UpscaylVideo;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        // Register Handlebars helper for Now(format) only once
        Handlebars.RegisterHelper("Now", (writer, context, parameters) =>
        {
            string format = parameters.Length > 0 ? Convert.ToString(parameters[0]) ?? "yyyy-MM-dd" : "yyyy-MM-dd";
            writer.WriteSafeString(DateTime.Now.ToString(format));
        });
    }

    public static Window? Window { get; private set; }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            Window = desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
            PageManager.Instance.SetPage(typeof(MainPageViewModel));
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}