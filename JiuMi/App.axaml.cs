using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
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
            var splashScreen = new JiuMiSplashWindow();
            desktop.MainWindow = splashScreen;
            splashScreen.Show();

            base.OnFrameworkInitializationCompleted();

            await Task.Delay(2000);

            var mainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            splashScreen.Close();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };

            base.OnFrameworkInitializationCompleted();
        }
    }
}
