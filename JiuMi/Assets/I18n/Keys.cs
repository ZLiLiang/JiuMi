using JiuMi.Services;

namespace JiuMi.Assets.I18n;

public static class Keys
{
    /// <summary>
    /// Key: GreetingMessage
    /// </summary>
    public const string GreetingMessage = "GreetingMessage";

    /// <summary>
    /// Key: HomePageNavHeader
    /// </summary>
    public const string HomePageNavHeader = "HomePageNavHeader";

    /// <summary>
    /// Key: SettingPageNavHeader
    /// </summary>
    public const string SettingPageNavHeader = "SettingPageNavHeader";

}

public static class Strings
{
    /// <summary>
    /// Value of: GreetingMessage
    /// </summary>
    public static string GreetingMessage => LanguageService.Current.GetString(Keys.GreetingMessage);

    /// <summary>
    /// Value of: HomePageNavHeader
    /// </summary>
    public static string HomePageNavHeader => LanguageService.Current.GetString(Keys.HomePageNavHeader);

    /// <summary>
    /// Value of: SettingPageNavHeader
    /// </summary>
    public static string SettingPageNavHeader => LanguageService.Current.GetString(Keys.SettingPageNavHeader);

}