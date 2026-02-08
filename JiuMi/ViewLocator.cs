using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using JiuMi.Pages;
using JiuMi.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace JiuMi;

public class ViewLocator(IServiceProvider serviceProvider) : IDataTemplate
{
    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }

    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var count = "ViewModel".Length;
        var name = param.GetType().FullName?[..^count].Replace("ViewModels", "Pages");
        
        var type = Type.GetType(name);

        if (type != null)
        {
            try
            {
                return (Control)serviceProvider.GetRequiredService(type);
            }
            catch (InvalidOperationException)
            {
                return (Control)Activator.CreateInstance(type)!;
            }
        }

        return new TextBlock { Text = "Not Found: " + name };
    }
}
