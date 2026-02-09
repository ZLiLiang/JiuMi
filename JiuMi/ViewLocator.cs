using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using JiuMi.ViewModels;
using Microsoft.Extensions.DependencyInjection;

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

        var typeName = param.GetType().FullName;
        if (string.IsNullOrEmpty(typeName)) 
            return null;

        var name = typeName.Replace("ViewModels", "Pages");
        if (name.EndsWith("ViewModel"))
        {
            name = name[..^"ViewModel".Length];
        }

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
