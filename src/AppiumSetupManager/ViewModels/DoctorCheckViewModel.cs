using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Models;

namespace AppiumSetupManager.ViewModels;

public partial class DoctorCheckViewModel : ObservableObject
{
    public string  Name               { get; }
    public string  Group              { get; }
    public bool    CanAutoFix         { get; }
    public string? RemediationCommand { get; }

    [ObservableProperty] private CheckResult _result;
    [ObservableProperty] private string      _description = string.Empty;
    [ObservableProperty] private bool        _isFixing;

    // Injected by DoctorViewModel — avoids ancestor binding in AXAML
    public ICommand? FixCommand { get; }

    public DoctorCheckViewModel(DoctorCheck check, ICommand? fixCommand = null)
    {
        Name               = check.Name;
        Group              = check.Group;
        CanAutoFix         = check.CanAutoFix;
        RemediationCommand = check.RemediationCommand;
        _result            = check.Result;
        _description       = check.Description;
        FixCommand         = fixCommand;
    }

    public void ApplyUpdate(DoctorCheck check)
    {
        Result      = check.Result;
        Description = check.Description;
        IsFixing    = false;
    }
}
