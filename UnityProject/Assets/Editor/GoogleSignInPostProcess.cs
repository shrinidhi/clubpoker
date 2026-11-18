#if UNITY_IOS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClubPoker.Core;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

/// <summary>
/// iOS build: registers each AppConfig's Google iOS client ID, reversed
/// (com.googleusercontent.apps.…), as a URL scheme in Info.plist. The Google
/// Sign-In SDK refuses to start sign-in without it. Used by
/// Plugins/iOS/GoogleSignInBridge.mm.
///
/// Every AppConfig asset is read because the one used at runtime is picked in
/// Bootstrap.unity, not at build time.
/// </summary>
public static class GoogleSignInPostProcess
{
    // After Facebook's post-process (100), which also writes CFBundleURLTypes.
    [PostProcessBuild(120)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
            return;

        List<string> schemes = AssetDatabase.FindAssets("t:" + nameof(AppConfig))
            .Select(guid => AssetDatabase.LoadAssetAtPath<AppConfig>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(config => config != null && !string.IsNullOrWhiteSpace(config.googleIosClientId))
            .Select(config => ReverseClientId(config.googleIosClientId.Trim()))
            .Distinct()
            .ToList();

        if (schemes.Count == 0)
        {
            Debug.LogWarning("[GoogleSignIn] No AppConfig has googleIosClientId set — " +
                             "Google sign-in will not work in this iOS build.");
            return;
        }

        string plistPath = ResolveInfoPlistPath(pathToBuiltProject);
        if (plistPath == null)
        {
            Debug.LogError("[GoogleSignIn] Info.plist not found under " + pathToBuiltProject +
                           " — Google URL scheme not added; Google sign-in will fail.");
            return;
        }

        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        PlistElementArray urlTypes = plist.root["CFBundleURLTypes"] as PlistElementArray
                                     ?? plist.root.CreateArray("CFBundleURLTypes");

        HashSet<string> existing = new HashSet<string>(
            urlTypes.values
                .OfType<PlistElementDict>()
                .Select(dict => dict["CFBundleURLSchemes"] as PlistElementArray)
                .Where(array => array != null)
                .SelectMany(array => array.values)
                .Select(value => value.AsString()),
            StringComparer.Ordinal);

        foreach (string scheme in schemes)
        {
            if (existing.Contains(scheme))
                continue;

            PlistElementDict entry = urlTypes.AddDict();
            entry.SetString("CFBundleURLName", "google-sign-in");
            entry.CreateArray("CFBundleURLSchemes").AddString(scheme);
        }

        plist.WriteToFile(plistPath);
    }

    // "123-abc.apps.googleusercontent.com" → "com.googleusercontent.apps.123-abc"
    private static string ReverseClientId(string clientId)
    {
        string[] parts = clientId.Split('.');
        Array.Reverse(parts);
        return string.Join(".", parts);
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
