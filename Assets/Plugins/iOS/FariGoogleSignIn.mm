// FariGoogleSignIn.mm
// iOS native plugin for Google Sign-In (Unity)
//
// Dependencies: GoogleSignIn (~> 7.0) via CocoaPods
// Plist req:    GoogleService-Info.plist in bundle (Firebase Unity SDK auto-copies)
// URL scheme:    REVERSED_CLIENT_ID from GoogleService-Info.plist → Info.plist CFBundleURLTypes
//
// C# callback contract (same as Android FariSignInActivity):
//   SUCCESS:<idToken>
//   FAILURE:<code>:<message>
//   CANCELED

#import <GoogleSignIn/GoogleSignIn.h>

extern "C" {
    extern UIViewController* UnityGetGLViewController(void);
    extern void UnitySendMessage(const char* obj, const char* method, const char* msg);
}

#pragma mark - Helpers

// Get iOS client ID from GoogleService-Info.plist (bundle)
static NSString* FariGetIOSClientID(void) {
    NSString* path = [[NSBundle mainBundle] pathForResource:@"GoogleService-Info" ofType:@"plist"];
    if (!path) return nil;
    NSDictionary* d = [NSDictionary dictionaryWithContentsOfFile:path];
    return d[@"CLIENT_ID"];
}

// Send result to Unity on main thread
static void FariSendToUnity(NSString* go, NSString* method, NSString* result) {
    dispatch_async(dispatch_get_main_queue(), ^{
        UnitySendMessage([go UTF8String], [method UTF8String], [result UTF8String]);
    });
}

#pragma mark - Silent Sign-In (restore previous)

extern "C" void _fariRestorePreviousSignIn(const char* gameObjectName, const char* callbackMethod) {
    NSString* go = [NSString stringWithUTF8String:gameObjectName];
    NSString* method = [NSString stringWithUTF8String:callbackMethod];

    [GIDSignIn.sharedInstance restorePreviousSignInWithCompletion:^(GIDGoogleUser *user, NSError *error) {
        NSString* result;
        if (user && !error) {
            NSString* token = user.idToken.tokenString;
            if (token.length > 0) {
                result = [NSString stringWithFormat:@"SUCCESS:%@", token];
            } else {
                result = @"FAILURE:0:ID Token is empty";
            }
        } else {
            // No cached sign-in → need interactive
            result = @"FAILURE:4:SIGN_IN_REQUIRED";
        }
        FariSendToUnity(go, method, result);
    }];
}

#pragma mark - Interactive Sign-In

extern "C" void _fariStartGoogleSignIn(const char* webClientId, const char* gameObjectName, const char* callbackMethod) {
    NSString* serverClientID = [NSString stringWithUTF8String:webClientId];
    NSString* go = [NSString stringWithUTF8String:gameObjectName];
    NSString* method = [NSString stringWithUTF8String:callbackMethod];

    // iOS client ID comes from GoogleService-Info.plist (Firebase auto-generates)
    NSString* iosClientID = FariGetIOSClientID();
    if (!iosClientID) {
        NSString* err = @"FAILURE:0:GoogleService-Info.plist missing CLIENT_ID — Firebase iOS not configured?";
        FariSendToUnity(go, method, err);
        return;
    }

    dispatch_async(dispatch_get_main_queue(), ^{
        GIDConfiguration* cfg = [[GIDConfiguration alloc] initWithClientID:iosClientID
                                                            serverClientID:serverClientID];
        GIDSignIn.sharedInstance.configuration = cfg;

        UIViewController* vc = UnityGetGLViewController();
        [GIDSignIn.sharedInstance signInWithPresentingViewController:vc
                                                          completion:^(GIDSignInResult *signInResult, NSError *error) {
            NSString* result;
            if (error) {
                // kGIDSignInErrorCodeCanceled = -5
                if (error.code == -5) {
                    result = @"CANCELED";
                } else {
                    result = [NSString stringWithFormat:@"FAILURE:%ld:%@", (long)error.code, error.localizedDescription];
                }
            } else if (signInResult.user) {
                GIDGoogleUser *user = signInResult.user;
                NSString* token = user.idToken.tokenString;
                if (token.length > 0) {
                    result = [NSString stringWithFormat:@"SUCCESS:%@", token];
                } else {
                    result = @"FAILURE:0:ID Token empty — check serverClientID in GIDConfiguration";
                }
            } else {
                result = @"FAILURE:0:Unknown error from GIDSignIn";
            }
            FariSendToUnity(go, method, result);
        }];
    });
}

#pragma mark - Sign Out

extern "C" void _fariGoogleSignOut(void) {
    [GIDSignIn.sharedInstance signOut];
}
