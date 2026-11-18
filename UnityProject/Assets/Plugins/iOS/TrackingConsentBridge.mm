// App Tracking Transparency (ATT) bridge for TrackingConsent.cs.
// Status values match ATTrackingManagerAuthorizationStatus:
//   0 = notDetermined, 1 = restricted, 2 = denied, 3 = authorized.

#import <Foundation/Foundation.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>

typedef void (*TrackingConsentCallback)(int status);

extern "C" {

int _TrackingConsentGetStatus()
{
    if (@available(iOS 14, *))
        return (int)[ATTrackingManager trackingAuthorizationStatus];

    // Before iOS 14 there is no ATT; tracking is allowed.
    return 3;
}

void _TrackingConsentRequest(TrackingConsentCallback callback)
{
    if (@available(iOS 14, *))
    {
        [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:
            ^(ATTrackingManagerAuthorizationStatus status) {
                // The handler runs on a background queue; Unity runs on main.
                dispatch_async(dispatch_get_main_queue(), ^{
                    callback((int)status);
                });
            }];
        return;
    }

    callback(3);
}

}
