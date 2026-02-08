using FluentAvalonia.UI.Controls;
using JiuMi.Assets.I18n;
using JiuMi.Attributes;

namespace JiuMi.ViewModels;

public class SettingPageViewModel : MainPageViewModelBase
{
    public override string PageKey => "SettingPage";

    [Localized]
    public override string NavHeader => Strings.SettingPageNavHeader;

    public override Symbol IconKey => Symbol.Setting;

    public override Symbol IconFilledKey => Symbol.SettingsFilled;

    public override bool ShowsInFooter => true;
}
