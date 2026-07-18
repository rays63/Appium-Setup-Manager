using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Services;
using AppiumSetupManager.Localization;
using Avalonia.Threading;

namespace AppiumSetupManager.ViewModels;

/// <summary>
/// Backs the Updates screen: one card per already-installed, card-eligible component, each showing
/// whether it's up to date or has an update available (a live npm registry check when the component
/// has a known npm package, otherwise DetectionService's own baseline floor classification).
/// </summary>
public partial class UpdatesViewModel : ObservableObject, IDisposable
{
    // The only catalog entries that ever become a card on this screen. Xcode, Xcode CLI Tools,
    // Homebrew, JAVA_HOME and ANDROID_HOME are prerequisites/env-vars, not user-facing "toolchain
    // versions" — they never appear here.
    private static readonly string[] EligibleCatalogNames =
    [
        "Node.js",
        "npm",
        "JDK 21",
        "Android SDK",
        "Appium",
        "UiAutomator2 Driver",
        "XCUITest Driver",
        "Appium Inspector",
    ];

    private readonly IDetectionService _detection;
    private readonly IInstallerService _installer;
    private readonly IUpdateCheckService _updateCheck;
    private readonly ISettingsStore _settings;
    private CancellationTokenSource _loadCts = new();
    private CancellationTokenSource _updateCts = new();

    public ObservableCollection<UpdateCardViewModel> Cards { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isUpdatingAll;

    public int AvailableCount => Cards.Count(c => c.Status == DetectionState.Outdated);

    public bool HasUpdatesAvailable => AvailableCount > 0;

    public string SubtitleText => AvailableCount > 0
        ? string.Format(Strings.UpdatesSubtitleAvailableFormat, AvailableCount)
        : Strings.UpdatesSubtitleAllUpToDate;

    private bool CanUpdateAll() => !IsUpdatingAll && HasUpdatesAvailable;

    public UpdatesViewModel(IDetectionService detection, IInstallerService installer, IUpdateCheckService updateCheck, ISettingsStore settings)
    {
        _detection   = detection;
        _installer   = installer;
        _updateCheck = updateCheck;
        _settings    = settings;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loadCts.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsLoading = true;
        try
        {
            var scan = await _detection.ScanAllAsync(ct).ConfigureAwait(false);

            // Resolve each eligible catalog entry to its matching ComponentStatus (alias-aware —
            // "JDK 21" is reported by DetectionService as "JDK", "Android SDK" as "ADB"). An entry
            // with nothing installed (NotFound) or not relevant on this platform (NotApplicable) — the
            // Install screen's job, not this one — produces no card at all.
            var candidates = new List<(CatalogEntry Entry, ComponentStatus Status)>();
            foreach (var catalogName in EligibleCatalogNames)
            {
                var entry = InstallCatalog.All.FirstOrDefault(e => e.ComponentName == catalogName);
                if (entry is null)
                    continue;

                var detectionName = InstallCatalog.DetectionNameAlias.TryGetValue(catalogName, out var alias)
                    ? alias
                    : catalogName;
                var status = scan.FirstOrDefault(s => s.Name == detectionName);

                if (status is null || status.State is DetectionState.NotFound or DetectionState.NotApplicable)
                    continue;

                candidates.Add((entry, status));
            }

            // Kick off every npm registry lookup in parallel — mirrors DetectionService.ScanAllAsync's
            // own Task.WhenAll probe style. When the user has switched automatic update checks off,
            // skip the registry entirely — cards then fall back to DetectionService's baseline-floor
            // classification (the same null-latest path a failed lookup takes).
            var npmTasks = _settings.Current.AutomaticUpdateChecks
                ? candidates
                    .Where(c => c.Entry.NpmPackageName is not null)
                    .ToDictionary(
                        c => c.Entry.ComponentName,
                        c => _updateCheck.GetLatestNpmVersionAsync(c.Entry.NpmPackageName!, ct))
                : new Dictionary<string, Task<string?>>();

            if (npmTasks.Count > 0)
                await Task.WhenAll(npmTasks.Values).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Cards.Clear();
                foreach (var (entry, status) in candidates)
                {
                    var latest = npmTasks.TryGetValue(entry.ComponentName, out var task) ? task.Result : null;
                    Cards.Add(new UpdateCardViewModel(status, entry, latest, UpdateOneAsync));
                }

                NotifyDerivedPropertiesChanged();
            });
        }
        catch (OperationCanceledException)
        {
            // Non-fatal — leave whatever was loaded before cancellation in place.
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Per-card "Update" button handler, delegated from UpdateCardViewModel.UpdateCommand.</summary>
    private async Task UpdateOneAsync(UpdateCardViewModel card)
    {
        if (card.CatalogName is null)
            return;

        card.IsUpdating = true;
        card.LastError = null;
        try
        {
            await DrainUpdateAsync([card.CatalogName], new Dictionary<string, UpdateCardViewModel>(StringComparer.Ordinal)
            {
                [card.CatalogName] = card,
            }).ConfigureAwait(false);
        }
        finally
        {
            card.IsUpdating = false;
        }

        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanUpdateAll))]
    private async Task UpdateAllAsync()
    {
        var outdated = Cards.Where(c => c.Status == DetectionState.Outdated).ToList();
        if (outdated.Count == 0)
            return;

        IsUpdatingAll = true;
        UpdateAllCommand.NotifyCanExecuteChanged();
        foreach (var card in outdated)
            card.IsExternallyBusy = true;

        try
        {
            var names = outdated.Select(c => c.CatalogName!).Distinct(StringComparer.Ordinal).ToList();
            var cardsByCatalogName = outdated
                .Where(c => c.CatalogName is not null)
                .ToDictionary(c => c.CatalogName!, c => c, StringComparer.Ordinal);

            await DrainUpdateAsync(names, cardsByCatalogName).ConfigureAwait(false);
        }
        finally
        {
            foreach (var card in outdated)
                card.IsExternallyBusy = false;
            IsUpdatingAll = false;
            UpdateAllCommand.NotifyCanExecuteChanged();
        }

        await LoadAsync();
    }

    /// <summary>
    /// Drains a single UpdateSelectedAsync call, routing any Failed step's error message to the
    /// matching card so the UI can surface it without needing a separate transcript view.
    /// </summary>
    private async Task DrainUpdateAsync(
        IEnumerable<string> catalogNames,
        IReadOnlyDictionary<string, UpdateCardViewModel> cardsByCatalogName)
    {
        _updateCts.Cancel();
        _updateCts = new CancellationTokenSource();
        var ct = _updateCts.Token;

        try
        {
            await foreach (var step in _installer.UpdateSelectedAsync(catalogNames, ct).ConfigureAwait(false))
            {
                if (step.State != InstallStepState.Failed)
                    continue;

                var captured = step;
                if (cardsByCatalogName.TryGetValue(captured.ComponentName, out var card))
                    await Dispatcher.UIThread.InvokeAsync(() => card.LastError = captured.ErrorMessage);
            }
        }
        catch (OperationCanceledException)
        {
            // Non-fatal — fall through to the caller's re-load so the screen reflects whatever
            // state resulted.
        }
    }

    private void NotifyDerivedPropertiesChanged()
    {
        OnPropertyChanged(nameof(AvailableCount));
        OnPropertyChanged(nameof(HasUpdatesAvailable));
        OnPropertyChanged(nameof(SubtitleText));
        UpdateAllCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _loadCts.Cancel();
        _loadCts.Dispose();
        _updateCts.Cancel();
        _updateCts.Dispose();
    }
}
