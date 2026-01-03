using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace JiuMi;

public partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();

        this.Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (this.DataContext is IDisposable disposableViewModel)
        {
            disposableViewModel.Dispose();
        }

        this.Unloaded -= OnUnloaded;
    }
}