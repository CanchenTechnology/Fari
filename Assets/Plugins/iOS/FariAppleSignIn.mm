// ======================================================================
//  FariAppleSignIn.mm — iOS 原生 Apple Sign-In 插件
//
//  平台: iOS 13.0+
//  框架: AuthenticationServices (系统内置，无需 CocoaPod)
//
//  C# 绑定: [DllImport("__Internal")] AppleSignInHelper.cs
//  回调格式 (同 Google Sign-In 插件):
//    SUCCESS:<idToken>|<authorizationCode>
//    FAILURE:<code>:<message>
//    CANCELED
// ======================================================================

#import <UIKit/UIKit.h>
#import <AuthenticationServices/AuthenticationServices.h>

// Unity 引擎提供的回调函数声明
extern void UnitySendMessage(const char *gameObject, const char *method, const char *message);

// ======================================================================
//  FariAppleSignInDelegate — ASAuthorizationController 代理
// ======================================================================

@interface FariAppleSignInDelegate : NSObject
    <ASAuthorizationControllerDelegate,
     ASAuthorizationControllerPresentationContextProviding>

@property (nonatomic, copy) NSString *gameObjectName;
@property (nonatomic, copy) NSString *callbackMethod;

@end

@implementation FariAppleSignInDelegate

#pragma mark - ASAuthorizationControllerPresentationContextProviding

- (ASPresentationAnchor)presentationAnchorForAuthorizationController:(ASAuthorizationController *)controller
{
    // 使用 keyWindow 作为授权弹窗的宿主
    UIWindow *keyWindow = nil;

#if __IPHONE_OS_VERSION_MAX_ALLOWED >= 130000
    if (@available(iOS 13.0, *))
    {
        for (UIWindowScene *windowScene in [UIApplication sharedApplication].connectedScenes)
        {
            if (windowScene.activationState == UISceneActivationStateForegroundActive)
            {
                for (UIWindow *window in windowScene.windows)
                {
                    if (window.isKeyWindow)
                    {
                        keyWindow = window;
                        break;
                    }
                }
                if (keyWindow) break;
            }
        }
    }
#endif

    if (!keyWindow)
    {
        keyWindow = [UIApplication sharedApplication].keyWindow;
    }

    return keyWindow;
}

#pragma mark - ASAuthorizationControllerDelegate

- (void)authorizationController:(ASAuthorizationController *)controller
   didCompleteWithAuthorization:(ASAuthorization *)authorization
{
    ASAuthorizationAppleIDCredential *credential =
        (ASAuthorizationAppleIDCredential *)authorization.credential;

    // 提取 identityToken（JWT 格式的 ID Token）
    NSString *idToken = @"";
    if (credential.identityToken != nil)
    {
        idToken = [[NSString alloc] initWithData:credential.identityToken
                                        encoding:NSUTF8StringEncoding];
    }

    // 提取 authorizationCode（用于服务端验证，Firebase 可能用到）
    NSString *authCode = @"";
    if (credential.authorizationCode != nil)
    {
        authCode = [[NSString alloc] initWithData:credential.authorizationCode
                                         encoding:NSUTF8StringEncoding];
    }

    // 提取用户信息（仅首次登录时会返回，后续登录 Apple 不会再提供）
    NSString *fullName = @"";
    if (credential.fullName)
    {
        NSPersonNameComponentsFormatter *formatter = [[NSPersonNameComponentsFormatter alloc] init];
        fullName = [formatter stringFromPersonNameComponents:credential.fullName] ?: @"";
    }
    NSString *email = credential.email ?: @"";

    if (idToken != nil && idToken.length > 0)
    {
        // 格式: SUCCESS:<idToken>|<authCode>|<fullName>|<email>
        NSString *result = [NSString stringWithFormat:@"SUCCESS:%@|%@|%@|%@",
                            idToken, authCode, fullName, email];
        UnitySendMessage(self.gameObjectName.UTF8String,
                         self.callbackMethod.UTF8String,
                         result.UTF8String);
    }
    else
    {
        UnitySendMessage(self.gameObjectName.UTF8String,
                         self.callbackMethod.UTF8String,
                         "FAILURE:0:Apple ID Token is empty");
    }
}

- (void)authorizationController:(ASAuthorizationController *)controller
           didCompleteWithError:(NSError *)error
{
    if (error.code == ASAuthorizationErrorCanceled)
    {
        UnitySendMessage(self.gameObjectName.UTF8String,
                         self.callbackMethod.UTF8String,
                         "CANCELED");
    }
    else if (error.code == ASAuthorizationErrorFailed)
    {
        // 可能是网络问题或授权被拒绝
        NSString *msg = [NSString stringWithFormat:@"FAILURE:%ld:Authorization failed — %@",
                         (long)error.code,
                         error.localizedDescription ?: @"unknown"];
        UnitySendMessage(self.gameObjectName.UTF8String,
                         self.callbackMethod.UTF8String,
                         msg.UTF8String);
    }
    else if (error.code == ASAuthorizationErrorNotHandled)
    {
        UnitySendMessage(self.gameObjectName.UTF8String,
                         self.callbackMethod.UTF8String,
                         "FAILURE:0:Authorization request was not handled (did you enable Sign In With Apple capability?)");
    }
    else
    {
        NSString *msg = [NSString stringWithFormat:@"FAILURE:%ld:%@",
                         (long)error.code,
                         error.localizedDescription ?: @"unknown"];
        UnitySendMessage(self.gameObjectName.UTF8String,
                         self.callbackMethod.UTF8String,
                         msg.UTF8String);
    }
}

@end

// ======================================================================
//  单例代理实例 — 保持引用防止被 GC
// ======================================================================

static FariAppleSignInDelegate *_fariAppleDelegate = nil;

// ======================================================================
//  C 接口 — 供 C# 通过 [DllImport("__Internal")] 调用
// ======================================================================

extern "C"
{
    /// 检查设备是否支持 Apple Sign-In（iOS 13+ 且未禁用）
    /// 返回: "1" = 支持, "0" = 不支持
    const char *_fariAppleIsSupported()
    {
        if (@available(iOS 13.0, *))
        {
            return "1";
        }
        return "0";
    }

    /// 启动 Apple Sign-In
    /// 参数:
    ///   gameObjectName  — C# GameObject 名称 (UnitySendMessage 目标)
    ///   callbackMethod  — C# 方法名
    ///   sha256Nonce     — SHA256 哈希后的 nonce（可选，传 "" 表示不使用）
    void _fariAppleStartSignIn(const char *gameObjectName,
                                const char *callbackMethod,
                                const char *sha256Nonce)
    {
        if (@available(iOS 13.0, *))
        {
            // 复用或创建 delegate 实例（避免每次重新分配）
            if (!_fariAppleDelegate)
            {
                _fariAppleDelegate = [[FariAppleSignInDelegate alloc] init];
            }

            _fariAppleDelegate.gameObjectName = [NSString stringWithUTF8String:gameObjectName];
            _fariAppleDelegate.callbackMethod  = [NSString stringWithUTF8String:callbackMethod];

            // 构建 Apple ID 授权请求
            ASAuthorizationAppleIDProvider *provider = [[ASAuthorizationAppleIDProvider alloc] init];
            ASAuthorizationAppleIDRequest *request = [provider createRequest];

            // 请求用户全名和邮箱（仅首次授权时会返回）
            request.requestedScopes = @[ASAuthorizationScopeFullName,
                                         ASAuthorizationScopeEmail];

            // 设置 nonce（用于 Firebase 验证，SHA256 哈希值）
            if (sha256Nonce != NULL && strlen(sha256Nonce) > 0)
            {
                request.nonce = [NSString stringWithUTF8String:sha256Nonce];
            }

            // 创建并启动授权控制器
            ASAuthorizationController *authController =
                [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[request]];
            authController.delegate = _fariAppleDelegate;
            authController.presentationContextProvider = _fariAppleDelegate;

            [authController performRequests];
        }
        else
        {
            UnitySendMessage(gameObjectName, callbackMethod,
                             "FAILURE:0:Apple Sign-In requires iOS 13.0 or later");
        }
    }
}
