// Google sign-in for Unity on iOS, via the Google Sign-In iOS SDK
// (pod GoogleSignIn, declared in Editor/GoogleSignInDependencies.xml).
// Shows Google's sign-in sheet and returns the chosen account's ID token,
// which the server verifies. Called from GoogleAuthProvider.cs.
//
// Result codes passed to the callback:
//   0 = success (payload = ID token), 1 = cancelled, 2 = error (payload = message).

#import <UIKit/UIKit.h>
#import <GoogleSignIn/GoogleSignIn.h>
#import "AppDelegateListener.h"

extern UIViewController* UnityGetGLViewController();

typedef void (*GoogleSignInCallback)(int code, const char* payload);

// Hands redirect URLs back to the SDK. iOS 12+ finishes sign-in inside
// ASWebAuthenticationSession, but Google's docs still ask apps to forward
// openURL, so do it in case the SDK falls back to a URL redirect.
@interface GoogleSignInURLListener : NSObject <AppDelegateListener>
@end

@implementation GoogleSignInURLListener

+ (void)load
{
    static GoogleSignInURLListener* instance;
    instance = [[GoogleSignInURLListener alloc] init];
    UnityRegisterAppDelegateListener(instance);
}

- (void)onOpenURL:(NSNotification*)notification
{
    NSURL* url = notification.userInfo[@"url"];
    if (url != nil)
        [GIDSignIn.sharedInstance handleURL:url];
}

@end

static NSString* ToNSString(const char* s)
{
    return s != NULL ? [NSString stringWithUTF8String:s] : nil;
}

extern "C" {

void _GoogleSignInSignIn(const char* iosClientId, const char* webClientId, GoogleSignInCallback callback)
{
    NSString* clientId = ToNSString(iosClientId);
    NSString* serverClientId = ToNSString(webClientId);

    dispatch_async(dispatch_get_main_queue(), ^{
        // serverClientID makes the ID token's audience our web client — the
        // one the server verifies against, same as on Android.
        GIDSignIn.sharedInstance.configuration =
            [[GIDConfiguration alloc] initWithClientID:clientId serverClientID:serverClientId];

        UIViewController* presenter = UnityGetGLViewController();
        while (presenter.presentedViewController != nil)
            presenter = presenter.presentedViewController;

        [GIDSignIn.sharedInstance signInWithPresentingViewController:presenter
            completion:^(GIDSignInResult* result, NSError* error) {
                if (error != nil)
                {
                    if (error.code == kGIDSignInErrorCodeCanceled)
                        callback(1, NULL);
                    else
                        callback(2, error.localizedDescription.UTF8String);
                    return;
                }

                NSString* idToken = result.user.idToken.tokenString;
                if (idToken.length == 0)
                    callback(2, "Google returned no ID token.");
                else
                    callback(0, idToken.UTF8String);
            }];
    });
}

void _GoogleSignInSignOut()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        [GIDSignIn.sharedInstance signOut];
    });
}

}
