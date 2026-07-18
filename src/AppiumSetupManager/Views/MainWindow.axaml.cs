using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.ViewModels;

namespace AppiumSetupManager.Views;

public partial class MainWindow : Window
{
    private const double DefaultLogHeight = 220;

    private double _expandedLogHeight = DefaultLogHeight;

    private bool _shortcutsRegistered;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // The search popup is not light-dismiss (see MainWindow.axaml), so it would float over
        // other apps when the window loses activation — close it explicitly.
        Deactivated += (_, _) => (DataContext as MainWindowViewModel)?.CloseSearchPopup();
    }

    // Global keyboard shortcuts (documented on the Settings screen — SettingsViewModel's badge
    // strings must stay in sync with the gestures registered here). Built in code-behind rather
    // than AXAML so the modifier follows the platform convention: PlatformSettings reports Meta
    // (⌘) on macOS and Control elsewhere. Command wiring only — the actions live on the
    // ViewModel; the sole view-local shortcut (focus search) is pure focus management.
    private void RegisterKeyboardShortcuts(MainWindowViewModel vm)
    {
        if (_shortcutsRegistered)
            return;
        _shortcutsRegistered = true;

        var cmd = PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

        // Cmd/Ctrl+Shift+I — Quick Install
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.I, cmd | KeyModifiers.Shift),
            Command = vm.QuickInstallShortcutCommand,
        });

        // Cmd/Ctrl+` — toggle the command console panel (Oem3 is the backtick/grave key)
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.Oem3, cmd),
            Command = vm.CommandLog.ToggleCollapseCommand,
        });

        // Cmd/Ctrl+Shift+D — run Doctor
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.D, cmd | KeyModifiers.Shift),
            Command = vm.RunDoctorShortcutCommand,
        });

        // Cmd/Ctrl+K — focus the top-bar search box
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.K, cmd),
            Command = new RelayCommand(() => TopBarSearchBox.Focus()),
        });

        // Cmd/Ctrl+Shift+L — toggle light/dark theme
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.L, cmd | KeyModifiers.Shift),
            Command = vm.ToggleThemeCommand,
        });
    }

    // ── Top-bar quick search (R2 step 3) ─────────────────────────────────────
    // Pure focus/keyboard plumbing — what the results ARE and what they DO lives in
    // MainWindowViewModel. The popup is not light-dismiss, so close-on-focus-loss is handled
    // here, deferred one dispatcher tick so a click that moves focus from the TextBox into a
    // result row never closes the popup before the row's Tapped handler runs.

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        switch (e.Key)
        {
            case Key.Enter when vm.IsSearchOpen:
                vm.ExecuteFirstSearchResultCommand.Execute(null);
                Focus(); // popup closed — return focus to the window content
                e.Handled = true;
                break;

            case Key.Escape:
                vm.DismissSearchCommand.Execute(null);
                Focus(); // unfocus the search box
                e.Handled = true;
                break;

            case Key.Down when vm.IsSearchOpen && SearchResultsList.ItemCount > 0:
                SearchResultsList.SelectedIndex = 0;
                var container = SearchResultsList.ContainerFromIndex(0);
                (container ?? SearchResultsList).Focus();
                e.Handled = true;
                break;
        }
    }

    private void OnSearchResultsKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                if (SearchResultsList.SelectedItem is SearchResultViewModel { IsExecutable: true } result)
                {
                    result.ExecuteCommand.Execute(null);
                    Focus();
                }
                e.Handled = true;
                break;

            case Key.Escape:
                vm.DismissSearchCommand.Execute(null);
                TopBarSearchBox.Focus();
                e.Handled = true;
                break;
        }
    }

    private void OnSearchResultTapped(object? sender, TappedEventArgs e)
    {
        var item = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true);
        if (item?.DataContext is SearchResultViewModel { IsExecutable: true } result)
        {
            result.ExecuteCommand.Execute(null);
            Focus();
        }
    }

    private void OnSearchBoxGotFocus(object? sender, GotFocusEventArgs e) =>
        (DataContext as MainWindowViewModel)?.ReopenSearchIfPending();

    // Shared LostFocus handler for the TextBox and the results list: once focus has settled,
    // close the popup only if it landed outside both.
    private void OnSearchFocusMoved(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is not MainWindowViewModel vm || !vm.IsSearchOpen)
                return;

            if (!TopBarSearchBox.IsKeyboardFocusWithin && !SearchResultsList.IsKeyboardFocusWithin)
                vm.CloseSearchPopup();
        });
    }

    // The log panel's row is resizable via GridSplitter while expanded. When the user collapses
    // it via the header button, the row must shrink to just the header — otherwise dragging alone
    // controls height and the collapse button would leave dead space below it. Remembering the
    // last dragged height here (rather than in the ViewModel) keeps this purely a layout concern.
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        RegisterKeyboardShortcuts(vm);
        vm.CommandLog.PropertyChanged += OnCommandLogPropertyChanged;
        ApplyLogCollapsedState(vm.CommandLog.IsCollapsed);
    }

    private void OnCommandLogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandLogViewModel.IsCollapsed) && sender is CommandLogViewModel vm)
            ApplyLogCollapsedState(vm.IsCollapsed);
    }

    private void ApplyLogCollapsedState(bool collapsed)
    {
        var logRow = RightGrid.RowDefinitions[3];

        if (collapsed)
        {
            if (logRow.Height.IsAbsolute)
                _expandedLogHeight = logRow.Height.Value;

            logRow.Height = GridLength.Auto;
        }
        else
        {
            logRow.Height = new GridLength(_expandedLogHeight, GridUnitType.Pixel);
        }

        LogSplitter.IsEnabled = !collapsed;
    }
}
