using FluentAvalonia.UI.Controls;
using JiuMi.Assets.I18n;
using JiuMi.Attributes;
using JiuMi.Services;

namespace JiuMi.ViewModels;

public class HomePageViewModel : MainPageViewModelBase
{
    public override string PageKey => "HomePage";

    [Localized]
    public override string NavHeader => LanguageService.Current.GetString(Keys.HomePageNavHeader);

    public override Symbol IconKey => Symbol.Home;

    public override Symbol IconFilledKey => Symbol.HomeFilled;

    public override bool ShowsInFooter => false;
}
