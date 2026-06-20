#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <Contacts/Contacts.h>
#import <ContactsUI/ContactsUI.h>

extern "C" void UnitySendMessage(const char *gameObject, const char *method, const char *message);

@interface FariContactPickerDelegate : NSObject <CNContactPickerDelegate>
@property(nonatomic, copy) NSString *gameObjectName;
@property(nonatomic, copy) NSString *successMethod;
@property(nonatomic, copy) NSString *cancelMethod;
@end

static FariContactPickerDelegate *fariContactPickerDelegate = nil;

static UIViewController *FariTopViewController(void)
{
    UIViewController *rootViewController = nil;

    for (UIScene *scene in UIApplication.sharedApplication.connectedScenes) {
        if (![scene isKindOfClass:UIWindowScene.class]) {
            continue;
        }

        UIWindowScene *windowScene = (UIWindowScene *)scene;
        for (UIWindow *window in windowScene.windows) {
            if (window.isKeyWindow) {
                rootViewController = window.rootViewController;
                break;
            }
        }

        if (rootViewController != nil) {
            break;
        }
    }

    if (rootViewController == nil) {
        for (UIScene *scene in UIApplication.sharedApplication.connectedScenes) {
            if (![scene isKindOfClass:UIWindowScene.class]) {
                continue;
            }

            UIWindowScene *windowScene = (UIWindowScene *)scene;
            rootViewController = windowScene.windows.firstObject.rootViewController;
            if (rootViewController != nil) {
                break;
            }
        }
    }

    while (rootViewController.presentedViewController != nil) {
        rootViewController = rootViewController.presentedViewController;
    }

    return rootViewController;
}

static NSString *FariEscapeForUnityMessage(NSString *value)
{
    if (value == nil) {
        value = @"";
    }

    NSCharacterSet *allowed = NSCharacterSet.URLQueryAllowedCharacterSet;
    return [value stringByAddingPercentEncodingWithAllowedCharacters:allowed] ?: @"";
}

@implementation FariContactPickerDelegate

- (void)contactPickerDidCancel:(CNContactPickerViewController *)picker
{
    if (self.gameObjectName.length > 0 && self.cancelMethod.length > 0) {
        UnitySendMessage(self.gameObjectName.UTF8String, self.cancelMethod.UTF8String, "cancelled");
    }
}

- (void)contactPicker:(CNContactPickerViewController *)picker didSelectContact:(CNContact *)contact
{
    NSString *name = [CNContactFormatter stringFromContact:contact style:CNContactFormatterStyleFullName];
    if (name.length == 0) {
        name = @"联系人";
    }

    NSString *phone = @"";
    if (contact.phoneNumbers.count > 0) {
        CNLabeledValue<CNPhoneNumber *> *phoneValue = contact.phoneNumbers.firstObject;
        phone = phoneValue.value.stringValue ?: @"";
    }

    NSString *payload = [NSString stringWithFormat:@"%@|%@",
                         FariEscapeForUnityMessage(name),
                         FariEscapeForUnityMessage(phone)];

    if (self.gameObjectName.length > 0 && self.successMethod.length > 0) {
        UnitySendMessage(self.gameObjectName.UTF8String, self.successMethod.UTF8String, payload.UTF8String);
    }
}

@end

extern "C" void _fariPickContactForInvite(const char *gameObjectName,
                                           const char *successMethod,
                                           const char *cancelMethod)
{
    NSString *go = gameObjectName != NULL ? [NSString stringWithUTF8String:gameObjectName] : @"";
    NSString *success = successMethod != NULL ? [NSString stringWithUTF8String:successMethod] : @"";
    NSString *cancel = cancelMethod != NULL ? [NSString stringWithUTF8String:cancelMethod] : @"";

    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *rootViewController = FariTopViewController();
        if (rootViewController == nil) {
            if (go.length > 0 && cancel.length > 0) {
                UnitySendMessage(go.UTF8String, cancel.UTF8String, "no_root_view_controller");
            }
            return;
        }

        fariContactPickerDelegate = [FariContactPickerDelegate new];
        fariContactPickerDelegate.gameObjectName = go;
        fariContactPickerDelegate.successMethod = success;
        fariContactPickerDelegate.cancelMethod = cancel;

        CNContactPickerViewController *picker = [CNContactPickerViewController new];
        picker.delegate = fariContactPickerDelegate;
        picker.displayedPropertyKeys = @[CNContactPhoneNumbersKey];

        [rootViewController presentViewController:picker animated:YES completion:nil];
    });
}
