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

}