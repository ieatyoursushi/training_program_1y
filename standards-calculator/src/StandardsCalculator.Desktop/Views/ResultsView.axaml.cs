using Avalonia.Controls;
using Avalonia.Interactivity;
using StandardsCalculator.Desktop.ViewModels;

namespace StandardsCalculator.Desktop.Views;

public partial class ResultsView : UserControl
{
    public ResultsView()
    {
        InitializeComponent();
    }

    private void OnBackClicked(object? sender, RoutedEventArgs e)
    {
        // Walk up to MainWindow and trigger navigation back
        var mainWindow = TopLevel.GetTopLevel(this) as Window;
        if (mainWindow?.DataContext is MainWindowViewModel vm)
            vm.NavigateBack();
    }
}
