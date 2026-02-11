using Avalonia.Controls;
using Avalonia.Threading;
using JiuMi.Helpers;
using JiuMi.ViewModels;
using System;

namespace JiuMi.Views;

public partial class JiuMiSplashWindow : Window
{
    public JiuMiSplashWindow()
    {
        InitializeComponent();
    }

    public void StartAnimation(Action? onAnimationFinished)
    {
        var imgControl = this.FindControl<Image>("GifImageControl");
        if (imgControl == null) return;

        var _player = new GifPlayer(imgControl);
        _player.Load("avares://JiuMi/Assets/jiumi.gif");

        if (DataContext is not JiuMiSplashViewModel vm)
            throw new InvalidOperationException("DataContext must be SplashViewModel");

        _player.Play(
            checkInitComplete: () => vm.IsInitializationComplete,
            onComplete: () =>
            {
                onAnimationFinished?.Invoke();
                Dispatcher.UIThread.Post(() =>
                {
                    _player?.Dispose();
                    _player = null;
                }, DispatcherPriority.Background);
            });
    }
}