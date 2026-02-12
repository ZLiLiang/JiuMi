using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JiuMi.Extensions;
using JiuMi.ViewModels;
using JiuMi.Views;
using Microsoft.Extensions.DependencyInjection;

namespace JiuMi;

public partial class App : Application
{
    public new static App? Current => Application.Current as App;
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var collection = new ServiceCollection();

        collection.AddJiuMiUIServices();

        Services = collection.BuildServiceProvider();

        BindingPlugins.DataValidators.RemoveAt(0);

        DataTemplates.Add(new ViewLocator(Services));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var splashVM = new JiuMiSplashViewModel();
            var splashScreen = new JiuMiSplashWindow
            {
                DataContext = splashVM,
            };
            desktop.MainWindow = splashScreen;
            splashScreen.Show();

            var mainWindow = new MainWindow
            {
                DataContext = new MainViewModel(),
                Opacity = 0,
                ShowInTaskbar = false
            };

            splashScreen.StartAnimation(onAnimationFinished: () =>
            {
                mainWindow.ShowInTaskbar = true;
                mainWindow.Opacity = 1;
                mainWindow.Activate();
                desktop.MainWindow = mainWindow;
                splashScreen.Close();
            });

            mainWindow.Show();
            splashScreen.Activate();

            _ = splashVM.Initialize();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };

        }

        base.OnFrameworkInitializationCompleted();
    }
}
