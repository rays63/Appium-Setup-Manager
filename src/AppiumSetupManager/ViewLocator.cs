using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace AppiumSetupManager;

public class ViewLocator : IDataTemplate
{
    public bool Match(object? data) =>
        data is not null && data.GetType().Name.EndsWith("ViewModel");

    public Control Build(object? param)
    {
        if (param is null)
            return new TextBlock { Text = "View not found: null" };

        // Derive view type name from the view-model's fully qualified name.
        // e.g.  AppiumSetupManager.ViewModels.DashboardViewModel
        //    -> AppiumSetupManager.Views.DashboardView
        var typeName = param.GetType().FullName!
            .Replace(".ViewModels.", ".Views.")
            .Replace("ViewModel", "View");

        var assembly = Assembly.GetExecutingAssembly();
        var type     = assembly.GetType(typeName);

        if (type is not null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = $"View not found: {typeName}" };
    }
}
