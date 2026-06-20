using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using StandardsCalculator.Desktop.ViewModels;
using StandardsCalculator.Desktop.Views;

namespace StandardsCalculator.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string dataDir = FindDataDir(AppContext.BaseDirectory);
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(dataDir),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Walk up the directory tree to find the standards data folder.
    /// Works both from the build output and when run from the repo root.
    /// </summary>
    private static string FindDataDir(string start)
    {
        string? dir = start;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "data", "standards");
            if (Directory.Exists(candidate)) return candidate;
            if (File.Exists(Path.Combine(dir, "StandardsCalculator.sln")))
                return Path.Combine(dir, "data", "standards");
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Path.Combine(start, "data", "standards");
    }
}
