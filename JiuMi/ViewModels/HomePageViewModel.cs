using FluentAvalonia.UI.Controls;
using HarfBuzzSharp;
using JiuMi.Assets.I18n;
using JiuMi.Services;

namespace JiuMi.ViewModels;

public class HomePageViewModel : MainPageViewModelBase
{
    public override string PageKey => "HomePage";

    public override string NavHeader => LanguageService.Current.GetString(Keys.HomePageNavHeader);

    public override Symbol IconKey => Symbol.Home;

    public override Symbol IconFilledKey => Symbol.HomeFilled;

    public override bool ShowsInFooter => false;

    public override void UpdateText()
    {
        throw new System.NotImplementedException();
    }
}
