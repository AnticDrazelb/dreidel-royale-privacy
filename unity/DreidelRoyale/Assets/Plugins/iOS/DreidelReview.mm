// StoreKit's review prompt. iOS decides whether it actually appears — it is quota-limited
// per app per year — so this asks and does not check, which is the documented contract.
#import <StoreKit/StoreKit.h>
#import <UIKit/UIKit.h>

extern "C" void _DreidelRequestReview(void)
{
    if (@available(iOS 14.0, *)) {
        UIWindowScene *scene = nil;
        for (UIScene *s in UIApplication.sharedApplication.connectedScenes) {
            if ([s isKindOfClass:[UIWindowScene class]]
                && s.activationState == UISceneActivationStateForegroundActive) {
                scene = (UIWindowScene *)s;
                break;
            }
        }
        if (scene) { [SKStoreReviewController requestReviewInScene:scene]; return; }
    }
    if (@available(iOS 10.3, *)) { [SKStoreReviewController requestReview]; }
}
