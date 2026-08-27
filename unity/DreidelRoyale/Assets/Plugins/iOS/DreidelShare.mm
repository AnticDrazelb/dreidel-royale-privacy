// UIActivityViewController has no managed equivalent, so the share sheet needs this much
// native code and no more. Presented from the topmost view controller so it still works when
// something else is already up, and anchored on iPad, where an unanchored popover is a crash
// rather than a layout problem.
#import <UIKit/UIKit.h>

extern "C" void _DreidelShareText(const char* subject, const char* body)
{
    NSString* subjectStr = subject ? [NSString stringWithUTF8String:subject] : @"";
    NSString* bodyStr    = body    ? [NSString stringWithUTF8String:body]    : @"";

    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController* root = [[[UIApplication sharedApplication] keyWindow] rootViewController];
        while (root.presentedViewController) root = root.presentedViewController;
        if (!root) return;

        UIActivityViewController* sheet =
            [[UIActivityViewController alloc] initWithActivityItems:@[bodyStr]
                                             applicationActivities:nil];
        [sheet setValue:subjectStr forKey:@"subject"];

        if (sheet.popoverPresentationController) {
            sheet.popoverPresentationController.sourceView = root.view;
            sheet.popoverPresentationController.sourceRect =
                CGRectMake(CGRectGetMidX(root.view.bounds), CGRectGetMidY(root.view.bounds), 0, 0);
            sheet.popoverPresentationController.permittedArrowDirections = 0;
        }
        [root presentViewController:sheet animated:YES completion:nil];
    });
}
