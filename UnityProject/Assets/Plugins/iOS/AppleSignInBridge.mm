// Sign in with Apple for Unity on iOS, via AuthenticationServices.
// Shows Apple's sign-in sheet and returns the identity token (a JWT the server
// verifies against Apple's keys, aud = our bundle ID). Called from
// AppleAuthProvider.cs. Capability + framework: Editor/AppleSignInPostProcess.cs.
//
// Result codes passed to the callback:
//   0 = success (payload = JSON: identityToken, authorizationCode, user,
//       email, givenName, familyName — the last three only on first sign-in)
//   1 = cancelled
//   2 = error (payload = message)

#import <UIKit/UIKit.h>
#import <AuthenticationServices/AuthenticationServices.h>

extern UIViewController* UnityGetGLViewController();

typedef void (*AppleSignInCallback)(int code, const char* payload);

API_AVAILABLE(ios(13.0))
@interface AppleSignInDelegate : NSObject <ASAuthorizationControllerDelegate,
                                           ASAuthorizationControllerPresentationContextProviding>
@property (nonatomic, assign) AppleSignInCallback callback;
@end

// Kept alive for the duration of a request; ASAuthorizationController holds
// its delegate weakly.
static id s_activeDelegate = nil;

@implementation AppleSignInDelegate

- (void)finishWithCode:(int)code payload:(NSString*)payload
{
    AppleSignInCallback cb = self.callback;
    s_activeDelegate = nil;
    if (cb != NULL)
        cb(code, payload != nil ? payload.UTF8String : NULL);
}

- (void)authorizationController:(ASAuthorizationController*)controller
   didCompleteWithAuthorization:(ASAuthorization*)authorization API_AVAILABLE(ios(13.0))
{
    if (![authorization.credential isKindOfClass:[ASAuthorizationAppleIDCredential class]])
    {
        [self finishWithCode:2 payload:@"Unexpected credential type."];
        return;
    }

    ASAuthorizationAppleIDCredential* credential =
        (ASAuthorizationAppleIDCredential*)authorization.credential;

    NSString* identityToken = credential.identityToken != nil
        ? [[NSString alloc] initWithData:credential.identityToken encoding:NSUTF8StringEncoding]
        : nil;

    if (identityToken.length == 0)
    {
        [self finishWithCode:2 payload:@"Apple returned no identity token."];
        return;
    }

    NSMutableDictionary* result = [NSMutableDictionary dictionary];
    result[@"identityToken"] = identityToken;

    if (credential.authorizationCode != nil)
    {
        NSString* code = [[NSString alloc] initWithData:credential.authorizationCode
                                               encoding:NSUTF8StringEncoding];
        if (code.length > 0)
            result[@"authorizationCode"] = code;
    }

    if (credential.user.length > 0)
        result[@"user"] = credential.user;

    // Apple sends email and name on the first authorisation only.
    if (credential.email.length > 0)
        result[@"email"] = credential.email;
    if (credential.fullName.givenName.length > 0)
        result[@"givenName"] = credential.fullName.givenName;
    if (credential.fullName.familyName.length > 0)
        result[@"familyName"] = credential.fullName.familyName;

    NSData* json = [NSJSONSerialization dataWithJSONObject:result options:0 error:nil];
    NSString* payload = json != nil
        ? [[NSString alloc] initWithData:json encoding:NSUTF8StringEncoding]
        : nil;

    if (payload == nil)
        [self finishWithCode:2 payload:@"Could not encode the Apple credential."];
    else
        [self finishWithCode:0 payload:payload];
}

- (void)authorizationController:(ASAuthorizationController*)controller
           didCompleteWithError:(NSError*)error API_AVAILABLE(ios(13.0))
{
    if (error.code == ASAuthorizationErrorCanceled)
        [self finishWithCode:1 payload:nil];
    else
        [self finishWithCode:2 payload:error.localizedDescription];
}

- (ASPresentationAnchor)presentationAnchorForAuthorizationController:(ASAuthorizationController*)controller
    API_AVAILABLE(ios(13.0))
{
    UIWindow* window = UnityGetGLViewController().view.window;
    return window != nil ? window : UIApplication.sharedApplication.keyWindow;
}

@end

extern "C" {

bool _AppleSignInIsAvailable()
{
    if (@available(iOS 13.0, *))
        return true;
    return false;
}

// hashedNonce: SHA-256 (hex) of the raw nonce the server will check; may be null.
void _AppleSignInSignIn(const char* hashedNonce, AppleSignInCallback callback)
{
    NSString* nonce = hashedNonce != NULL ? [NSString stringWithUTF8String:hashedNonce] : nil;

    dispatch_async(dispatch_get_main_queue(), ^{
        if (@available(iOS 13.0, *))
        {
            ASAuthorizationAppleIDRequest* request =
                [[[ASAuthorizationAppleIDProvider alloc] init] createRequest];
            request.requestedScopes = @[ ASAuthorizationScopeFullName, ASAuthorizationScopeEmail ];
            if (nonce.length > 0)
                request.nonce = nonce;

            AppleSignInDelegate* delegate = [[AppleSignInDelegate alloc] init];
            delegate.callback = callback;
            s_activeDelegate = delegate;

            ASAuthorizationController* controller =
                [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[ request ]];
            controller.delegate = delegate;
            controller.presentationContextProvider = delegate;
            [controller performRequests];
        }
        else
        {
            callback(2, "Sign in with Apple needs iOS 13 or later.");
        }
    });
}

}
