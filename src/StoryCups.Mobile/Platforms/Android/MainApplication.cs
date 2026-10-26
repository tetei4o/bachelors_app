using Android.App;
using Android.Runtime;

namespace StoryCups.Mobile;

// В Debug сървърът за разработка е на http://10.0.2.2:5056 (емулатор) или на IP адреса на компютъра (телефон),
// затова се разрешава нешифриран трафик. В Release остава забранен.
#if DEBUG
[Application(UsesCleartextTraffic = true)]
#else
[Application]
#endif
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
