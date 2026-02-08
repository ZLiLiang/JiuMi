using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Linq;

namespace JiuMi.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<MainPageViewModelBase> _navigationItems = [];

    [ObservableProperty]
    private ObservableCollection<MainPageViewModelBase> _footerItems = [];

    [ObservableProperty]
    private MainPageViewModelBase? _selectedPage;

    public MainViewModel()
    {
        if (App.Current?.Services != null)
        {
            App.Current.Services.GetServices<MainPageViewModelBase>()
                .ToList()
                .ForEach(vm =>
                {
                    if (vm.ShowsInFooter)
                    {
                        FooterItems.Add(vm);
                    }
                    else
                    {
                        NavigationItems.Add(vm);
                    }
                });
        }

        SelectedPage = NavigationItems.FirstOrDefault();
    }
}
