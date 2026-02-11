using System.Threading.Tasks;

namespace JiuMi.ViewModels;

public class JiuMiSplashViewModel : ViewModelBase
{
    public bool IsInitializationComplete { get; private set; } = false;

    public async Task Initialize()
    {
        IsInitializationComplete = true;
    }
}
