using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using System;
using System.Linq;

namespace JiuMi.Services;

public class LanguageService
{
    public static LanguageService Current { get; } = new LanguageService();

    public event Action? LanguageChanged;

    public void SwitchLanguage(string cultureCode)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => SwitchLanguage(cultureCode));
            return;
        }

        var app = Application.Current;
        if (app == null) return;

        var targetUri = new Uri($"avares://JiuMi/Assets/I18n/{cultureCode}.axaml");
        var existingResources = app.Resources.MergedDictionaries
            .OfType<ResourceInclude>()
            .Where(r => r.Source?.OriginalString.Contains("/Assets/I18n/") == true)
            .ToList();

        foreach (var resource in existingResources)
        {
            app.Resources.MergedDictionaries.Remove(resource);
        }

        app.Resources.MergedDictionaries.Add(new ResourceInclude(targetUri)
        {
            Source = targetUri
        });

        LanguageChanged?.Invoke();
    }

    public string GetString(string key)
    {
        if (Application.Current!.TryFindResource(key, out var res) && res is string str)
        {
            return str;
        }
        return $"[{key}]";
    }
}
