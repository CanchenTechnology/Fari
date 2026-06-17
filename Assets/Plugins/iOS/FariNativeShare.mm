#import <UIKit/UIKit.h>

extern "C" void _fariShareText(const char *text)
{
    NSString *shareText = text != NULL ? [NSString stringWithUTF8String:text] : @"";

    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *rootViewController = UIApplication.sharedApplication.keyWindow.rootViewController;
        if (rootViewController == nil) {
            rootViewController = UIApplication.sharedApplication.windows.firstObject.rootViewController;
        }

        if (rootViewController == nil) {
            return;
        }

        UIActivityViewController *activityViewController =
            [[UIActivityViewController alloc] initWithActivityItems:@[shareText] applicationActivities:nil];

        if (activityViewController.popoverPresentationController != nil) {
            activityViewController.popoverPresentationController.sourceView = rootViewController.view;
            activityViewController.popoverPresentationController.sourceRect =
                CGRectMake(CGRectGetMidX(rootViewController.view.bounds),
                           CGRectGetMidY(rootViewController.view.bounds),
                           1,
                           1);
        }

        [rootViewController presentViewController:activityViewController animated:YES completion:nil];
    });
}
