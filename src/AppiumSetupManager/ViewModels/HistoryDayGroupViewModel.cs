using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppiumSetupManager.ViewModels;

/// <summary>
/// One day-card on the History screen: a "Today"/"Yesterday"/"MMM d, yyyy" header label (uppercased
/// at presentation time via ToUpperConverter, matching DoctorView's group headers) plus that day's
/// entries, newest first.
/// </summary>
public partial class HistoryDayGroupViewModel : ObservableObject
{
    public string DayLabel { get; }

    public ObservableCollection<HistoryEntryViewModel> Entries { get; }

    public HistoryDayGroupViewModel(string dayLabel, IEnumerable<HistoryEntryViewModel> entries)
    {
        DayLabel = dayLabel;
        Entries  = new ObservableCollection<HistoryEntryViewModel>(entries);
    }
}
