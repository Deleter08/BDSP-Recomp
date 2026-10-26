// PC platform layer: may this process call into the native Wwise sound engine? NOT shipped code.
// The [PORT] guards in Assets/Wwise/Deployment/Components (AkInitializer, AkGameObj, AkAudioListener) skip every engine call when
// this is false. The arm64 fork ships a universal (x86_64 + arm64) Wwise 2019.2.15 AkSoundEngine.bundle, so both the x86_64 and the
// native arm64 macOS player load it: there the guards must let Wwise run (2026-10-20: they used to test Application.isEditor, which left the
// x86_64 .app silent). The Android player (2026-10-24) loads the Wwise 2019.2.15 arm64-v8a libAkSoundEngine.so
// (Wwise/Deployment/Plugins/Android + Generated/Android bindings, DllImport "AkSoundEngine" - setup/stages/wwise_android.py): usable, and
// the Android build refuses to start without the lib. The iOS player (2026-10-24) links the Wwise 2019.2.15 iOS engine statically
// (Wwise/Deployment/Plugins/iOS/Release/libAkSoundEngine.a + Generated/iOS bindings, DllImport "__Internal" - setup/stages/wwise_ios.py):
// usable, and an app without the lib would not have linked at all.
using System.Runtime.InteropServices;

public static class AkPortGuard
{
    static int usable = -1;

    public static bool NativeEngineUsable
    {
        get
        {
            if (usable < 0)
            {
                bool ok = true;
                if (!UnityEngine.Application.isEditor)
                {
#if UNITY_ANDROID
                    ok = true;   // libAkSoundEngine.so (arm64-v8a, Release) is in the APK: BdspWinBuild.Android.cs fails the build without it
                    UnityEngine.Debug.Log("[PcPlatform] Wwise native engine usable (Android: Wwise 2019.2.15 libAkSoundEngine.so arm64-v8a)");
#elif UNITY_IOS
                    ok = true;   // statically linked libAkSoundEngine.a (arm64)
                    UnityEngine.Debug.Log("[PcPlatform] Wwise native engine usable (iOS: static Wwise 2019.2.15 libAkSoundEngine.a)");
#else
                    ok = true;   // universal (x86_64 + arm64) AkSoundEngine.bundle: either process architecture can load it
                    UnityEngine.Debug.Log("[PcPlatform] Wwise native engine usable (universal x86_64+arm64 AkSoundEngine)"
                                          + " (process " + SafeArch() + ")");
#endif
                }
                usable = ok ? 1 : 0;
            }
            return usable == 1;
        }
    }

    static string SafeArch()
    {
        try { return RuntimeInformation.ProcessArchitecture.ToString(); }
        catch (System.Exception) { return "unknown"; }
    }
}
