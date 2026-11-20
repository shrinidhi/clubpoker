#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// iOS build: adds the Sign in with Apple capability (entitlement
/// com.apple.developer.applesignin) to the app target and links
/// AuthenticationServices.framework into UnityFramework, where
/// Plugins/iOS/AppleSignInBridge.mm compiles. Without the entitlement the
/// sheet fails with ASAuthorizationError 1000.
///
/// The App ID game.club.poker must also have Sign in with Apple enabled in
/// the Apple Developer portal, and the provisioning profile regenerated.
/// </summary>
public static class AppleSignInPostProcess
{
    private const string DefaultEntitlementsFile = "Unity-iPhone/ClubPoker.entitlements";

    // After Facebook (100), ATT (110) and Google (120).
    [PostProcessBuild(130)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
            return;

        string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);

        string mainTarget = project.GetUnityMainTargetGuid();
        project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(),
                                      "AuthenticationServices.framework", false);

        // Reuse an entitlements file something else already set up.
        string entitlements = project.GetBuildPropertyForAnyConfig(mainTarget, "CODE_SIGN_ENTITLEMENTS");
        if (string.IsNullOrEmpty(entitlements))
            entitlements = DefaultEntitlementsFile;

        project.WriteToFile(projectPath);

        var capabilities = new ProjectCapabilityManager(projectPath, entitlements, null, mainTarget);
        capabilities.AddSignInWithApple();
        capabilities.WriteToFile();
    }
}
#endif
