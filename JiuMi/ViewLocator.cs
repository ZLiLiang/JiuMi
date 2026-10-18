using Avalonia.Controls;
using Avalonia.Controls.Templates;
using JiuMi.ViewModels;

namespace JiuMi;

/// <summary>
/// Resolves a view from its view model by naming convention:
/// <c>XxxViewModel</c> maps to <c>XxxView</c>, in the sibling <c>Views</c>
/// namespace when the view model lives under <c>ViewModels</c>, otherwise in
/// its own namespace.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    /// <inheritdoc />
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        Type viewModelType = param.GetType();
        string? namespaceName = viewModelType.Namespace;
        if (namespaceName is null)
        {
            return null;
        }

        string viewName = viewModelType.Name.Replace("ViewModel", "View", StringComparison.Ordinal);
        string fullViewName = $"{GetViewNamespace(namespaceName)}.{viewName}";
        Type? viewType = viewModelType.Assembly.GetType(fullViewName);
        if (viewType is null)
        {
            return CreateMissingView(fullViewName);
        }

        object? view = Activator.CreateInstance(viewType);
        return view as Control ?? CreateMissingView(fullViewName);
    }

    /// <inheritdoc />
    public bool Match(object? data) => data is ViewModelBase;

    private static string GetViewNamespace(string viewModelNamespace)
    {
        const string viewModelsSuffix = ".ViewModels";
        if (!viewModelNamespace.EndsWith(viewModelsSuffix, StringComparison.Ordinal))
        {
            return viewModelNamespace;
        }

        return string.Concat(
            viewModelNamespace.AsSpan(0, viewModelNamespace.Length - "ViewModels".Length),
            "Views");
    }

    private static TextBlock CreateMissingView(string viewName) => new() { Text = $"Not Found: {viewName}" };
}
