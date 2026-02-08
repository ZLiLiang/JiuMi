using Avalonia.Controls;
using JiuMi.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Reflection;

namespace JiuMi.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddJiuMiUIServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        var viewModels = assembly.GetTypes()
            .Where(t => typeof(MainPageViewModelBase).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var vmType in viewModels)
        {
            services.AddTransient(typeof(MainPageViewModelBase), vmType);
        }

        var views = assembly.GetTypes()
            .Where(t => typeof(UserControl).IsAssignableFrom(t) && !t.IsAbstract && t.Namespace?.StartsWith("JiuMi.Pages") == true);

        foreach (var viewType in views)
        {
            services.AddTransient(viewType);
        }
    }
}
