using System;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
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

    public abstract void UpdateText();

    private void OnLanguageChanged()
    {
        UpdateText();
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