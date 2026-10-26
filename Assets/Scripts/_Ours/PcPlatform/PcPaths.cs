// PC platform layer: where the game's StreamingAssets data lives. NOT shipped code.
// Everywhere except the Android player this is Application.streamingAssetsPath, unchanged. On Android that path is inside the APK
// (jar:file://...!/assets), which the game's File.Exists / FileStream reads of Dpr.bin and AssetBundle.LoadFromFile of ~5 GB of bundles
// cannot use - and an APK that size would need an OBB. The Android build (BdspWinBuild.Android.cs) leaves the data out of the APK and
// setup/deploy_android.sh pushes it to <Application.persistentDataPath>/StreamingAssets (the app's external files dir,
// /storage/emulated/0/Android/data/com.opendpr.bdsp/files - readable by the app without a storage permission).
// iOS (2026-10-24): Application.streamingAssetsPath (<app>/Data/Raw) IS a plain directory there, but ~6 GB inside the .app would be
// re-installed with every re-sign (a free Apple ID signs for 7 days). The iOS build (BdspWinBuild.iOS.cs) leaves the data out too and
// writes Builds/ios_<time>/data/StreamingAssets; it is copied once into the app's Documents (Finder > iPhone > Files > Dpr, the
// build turns UIFileSharingEnabled on) = <Application.persistentDataPath>/StreamingAssets, which a re-install keeps. A build that
// does carry the data in the .app (BDSP_IOS_DATA=app) has no Documents copy and reads Application.streamingAssetsPath.
using System.IO;
using UnityEngine;

namespace PcPlatform
{
    public static class PcPaths
    {
        static string streamingAssets;

        public static string StreamingAssets
        {
            get
            {
                if (streamingAssets == null)
                {
#if UNITY_ANDROID && !UNITY_EDITOR
                    streamingAssets = Application.persistentDataPath + "/StreamingAssets";
                    string manifest = streamingAssets + "/AssetAssistant/" + Application.productName + ".bin";
                    if (File.Exists(manifest))
                        Debug.Log("[PcPlatform] StreamingAssets data: " + streamingAssets);
                    else
                        Debug.LogError("[PcPlatform] StreamingAssets data MISSING: no " + manifest +
                                       " - push it with: sh setup/deploy_android.sh (it copies Builds/android_<time>/data/StreamingAssets there)");
#elif UNITY_IOS && !UNITY_EDITOR
                    string documents = Application.persistentDataPath + "/StreamingAssets";
                    string manifest = "/AssetAssistant/" + Application.productName + ".bin";
                    if (File.Exists(documents + manifest))
                    {
                        streamingAssets = documents;
                        // ~5 GB of game data must not go into iCloud / device backups (saves stay backed up)
                        try { UnityEngine.iOS.Device.SetNoBackupFlag(documents); } catch (System.Exception) { }
                    }
                    else
                        streamingAssets = Application.streamingAssetsPath;
                    // UnityEngine.Debug: the bare Debug is the shipped game's stripped one (Assets/Scripts/DebugLog/Debug.cs, Conditional "DUMMY")
                    if (File.Exists(streamingAssets + manifest))
                        UnityEngine.Debug.Log("[PcPlatform] StreamingAssets data: " + streamingAssets);
                    else
                        UnityEngine.Debug.LogError("[PcPlatform] StreamingAssets data MISSING: no " + documents + manifest + " (nor in the app) - copy " +
                                       "Builds/ios_<time>/data/StreamingAssets into the app's Documents (Finder > iPhone > Files > Dpr)");
#else
                    streamingAssets = Application.streamingAssetsPath;
#endif
                }
                return streamingAssets;
            }
        }
    }
}
