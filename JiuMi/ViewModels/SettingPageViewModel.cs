 using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using JiuMi.Assets.I18n;
using JiuMi.Attributes;
using System.Collections.Generic;

namespace JiuMi.ViewModels;

public partial class SettingPageViewModel : MainPageViewModelBase
{
    public override string PageKey => "SettingPage";

    [Localized]
    public override string NavHeader => Strings.SettingPageNavHeader;

    public override Symbol IconKey => Symbol.Setting;

    public override Symbol IconFilledKey => Symbol.SettingsFilled;

    public override bool ShowsInFooter => true;

    public List<string> Themes { get; } = [nameof(ThemeVariant.Default), nameof(ThemeVariant.Light), nameof(ThemeVariant.Dark)];

    [ObservableProperty]
    private string _selectedTheme;

    public Dictionary<string, string> Languages { get; } = new()
    {
        { "zh-CN", "简体中文" },
        { "en-US", "English" }
    };

    [ObservableProperty]
    private string _selectedLanguage;
}
