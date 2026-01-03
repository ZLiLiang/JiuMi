using JiuMi.Assets.I18n;
using JiuMi.Services;

namespace JiuMi.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public string Greeting => LanguageService.Current.GetString(Keys.GreetingMessage);
}
