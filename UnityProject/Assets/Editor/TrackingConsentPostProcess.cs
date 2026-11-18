#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

/// <summary>
/// iOS build: adds the App Tracking Transparency prompt text to Info.plist and
/// links AppTrackingTransparency.framework (weak, since the minimum is iOS 13).
/// Used by Plugins/iOS/TrackingConsentBridge.mm. Without the plist key, iOS
/// crashes the app when the prompt is requested.
/// </summary>
public static class TrackingConsentPostProcess
{
    // Shown under the system "Allow … to track your activity" title.
    // Must truthfully say what the data is used for (App Review checks it).
    private const string UsageDescription =
        "This lets you sign in with your full Facebook account and helps us measure our ads. " +
        "You can still sign in with Facebook if you don't allow it.";

    // After Facebook's post-process (100), which also edits Info.plist.
    [PostProcessBuild(110)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
            return;

        string plistPath = ResolveInfoPlistPath(pathToBuiltProject);
        if (plistPath == null)
        {
            Debug.LogError("[TrackingConsent] Info.plist not found under " + pathToBuiltProject +
                           " — NSUserTrackingUsageDescription was not added; the ATT prompt will crash.");
        }
        else
        {
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSUserTrackingUsageDescription", UsageDescription);
            plist.WriteToFile(plistPath);
        }

        string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);
        // The .mm compiles into UnityFramework.
        project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(),
                                      "AppTrackingTransparency.framework", true);
        project.WriteToFile(projectPath);
    }

    // Objective-C projects keep Info.plist at the build root; Swift nests it
    // under the .xcodeproj's name.
    private static string ResolveInfoPlistPath(string pathToBuiltProject)
    {
        string atRoot = Path.Combine(pathToBuiltProject, "Info.plist");
        if (File.Exists(atRoot))
            return atRoot;

        string projectName = Path.GetFileNameWithoutExtension(
            Path.GetDirectoryName(PBXProject.GetPBXProjectPath(pathToBuiltProject)));
        string nested = Path.Combine(Path.Combine(pathToBuiltProject, projectName), "Info.plist");
        return File.Exists(nested) ? nested : null;
    }
}
#endif
