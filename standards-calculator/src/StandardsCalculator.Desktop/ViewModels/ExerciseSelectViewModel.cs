using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StandardsCalculator.Core.Data;
using StandardsCalculator.Core.Models;
using StandardsCalculator.Desktop.Models;

namespace StandardsCalculator.Desktop.ViewModels;

/// <summary>
/// Page 1 — exercise selection.
/// The user picks an exercise and unit; clicking Continue loads both gender tables
/// and triggers the LMS fit (once), then navigates to Page 2.
/// </summary>
public partial class ExerciseSelectViewModel : ViewModelBase
{
    private readonly string _dataDir;
    private readonly Action<SessionModel> _onContinue;

    // ── Exercise list ─────────────────────────────────────────────────────────
    public ObservableCollection<string> Exercises { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private string? _selectedExercise;

    [ObservableProperty]
    private string _filterText = string.Empty;

    // ── Unit ──────────────────────────────────────────────────────────────────
    [ObservableProperty]
    private bool _isKg;   // false = lb (default)

    // ── Status ────────────────────────────────────────────────────────────────
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public ExerciseSelectViewModel(string dataDir, Action<SessionModel> onContinue)
    {
        _dataDir    = dataDir;
        _onContinue = onContinue;
        LoadExercises();
    }

    private void LoadExercises()
    {
        var repo = new StandardsRepository(_dataDir);
        var all  = repo.AvailableExercises().OrderBy(e => e).ToList();
        foreach (var ex in all) Exercises.Add(ex);
    }

    // ── Filter ────────────────────────────────────────────────────────────────
    partial void OnFilterTextChanged(string value)
    {
        var repo = new StandardsRepository(_dataDir);
        var all  = repo.AvailableExercises()
            .Where(e => e.Contains(value.Trim().ToLower().Replace(' ', '-')))
            .OrderBy(e => e);
        Exercises.Clear();
        foreach (var ex in all) Exercises.Add(ex);
    }

    // ── Continue command ──────────────────────────────────────────────────────
    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task ContinueAsync()
    {
        if (SelectedExercise is null) return;

        IsLoading     = true;
        StatusMessage = $"Loading {SelectedExercise}…";

        try
        {
            var unit = IsKg ? Unit.Kg : Unit.Lb;
            var repo = new StandardsRepository(_dataDir);

            var maleTable   = await repo.GetAsync(SelectedExercise, Gender.Male,   unit);
            var femaleTable = await repo.GetAsync(SelectedExercise, Gender.Female, unit);

            if (maleTable.ByBodyweight.Count == 0 && femaleTable.ByBodyweight.Count == 0)
            {
                StatusMessage = "No bodyweight data for this exercise (may be reps-based).";
                IsLoading = false;
                return;
            }

            StatusMessage = "Fitting models…";
            // LMS fit is CPU-bound — run off the UI thread
            var session = await Task.Run(() =>
                new SessionModel(SelectedExercise, unit, _dataDir, maleTable, femaleTable));

            _onContinue(session);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanContinue() => SelectedExercise is not null && !IsLoading;
}
