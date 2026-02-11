using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using JiuMi.Attributes;
using JiuMi.Services;

namespace JiuMi.ViewModels;

public class ViewModelBase : ObservableObject
{
}

public abstract class MainPageViewModelBase : ViewModelBase, IDisposable
{
    public MainPageViewModelBase()
    {
        LanguageService.Current.LanguageChanged += OnLanguageChanged;
    }

    private bool _disposed = false;

    public abstract string PageKey { get; }

    public abstract string NavHeader { get; }

    public abstract Symbol IconKey { get; }

    public abstract Symbol IconFilledKey { get; }

    public abstract bool ShowsInFooter { get; }

    public virtual void RefreshLocalizedStrings()
    {

    }

    private static readonly Dictionary<Type, List<string>> _localizedPropertiesCache = [];

    private void OnLanguageChanged()
    {
        RefreshLocalizedStrings();

        var type = this.GetType();

        if (!_localizedPropertiesCache.TryGetValue(type, out var propNames))
        {
            propNames = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => Attribute.IsDefined(p, typeof(LocalizedAttribute)))
                .Select(p => p.Name)
                .ToList();
            _localizedPropertiesCache[type] = propNames;
        }

        foreach (var name in propNames)
        {
            OnPropertyChanged(name);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            LanguageService.Current.LanguageChanged -= OnLanguageChanged;
        }

        _disposed = true;

    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}