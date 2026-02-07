using System;
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

    private bool _disposedValue;

    public abstract string PageKey { get; }

    public abstract string NavHeader { get; }

    public abstract Symbol IconKey { get; }

    public abstract Symbol IconFilledKey { get; }

    public abstract bool ShowsInFooter { get; }

    public virtual void RefreshLocalizedStrings()
    {

    }

    private void OnLanguageChanged()
    {
        RefreshLocalizedStrings();

        var properties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (Attribute.IsDefined(prop, typeof(LocalizedAttribute)))
            {
                OnPropertyChanged(prop.Name);
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                LanguageService.Current.LanguageChanged -= OnLanguageChanged;
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}