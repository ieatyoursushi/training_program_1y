using CommunityToolkit.Mvvm.ComponentModel;
using StandardsCalculator.Desktop.Models;

namespace StandardsCalculator.Desktop.ViewModels;

/// <summary>
/// Shell ViewModel — owns the current page and performs navigation.
/// The MainWindow binds its Content to CurrentPage; the ViewLocator
/// resolves the matching View automatically.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    private readonly string _dataDir;

    public MainWindowViewModel(string dataDir)
    {
        _dataDir     = dataDir;
        _currentPage = new ExerciseSelectViewModel(dataDir, NavigateToResults);
    }

    private void NavigateToResults(SessionModel session)
    {
        CurrentPage = new ResultsViewModel(session);
    }

    /// <summary>Navigate back to the exercise-selection page.</summary>
    public void NavigateBack()
    {
        CurrentPage = new ExerciseSelectViewModel(_dataDir, NavigateToResults);
    }
}
