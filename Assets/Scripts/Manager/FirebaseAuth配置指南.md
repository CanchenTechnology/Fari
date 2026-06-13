# Firebase 第三方登录配置指南

## 概述
本指南介绍如何在Unity项目中配置Firebase Authentication，实现Google、Apple、Facebook登录。

---

## 1. 安装Firebase SDK

### 1.1 通过Unity Asset Store安装
1. 打开Unity Asset Store，搜索 "Firebase SDK"
2. 下载并导入以下包：
   - FirebaseApp (必需)
   - FirebaseAuth (必需)
   - FirebaseDatabase (可选)
   - FirebaseRemoteConfig (可选)

### 1.2 通过Package Manager安装 (推荐)
1. 打开 `Window > Package Manager`
2. 点击 `+` > `Add package from git URL`
3. 添加以下URL：
   ```
   com.google.firebase.app
   com.google.firebase.auth
   ```

### 1.3 使用Firebase Unity Editor工具
1. 从 [Firebase Console](https://console.firebase.google.com/) 下载 `google-services.json` (Android) 和 `GoogleService-Info.plist` (iOS)
2. 将文件拖入Unity项目的 `Assets` 文件夹
3. 点击菜单 `Assets > External Dependency Manager > Android Resolver > Force Resolve`

---

## 2. Firebase控制台配置

### 2.1 创建Firebase项目
1. 访问 [Firebase Console](https://console.firebase.google.com/)
2. 点击 "添加项目"
3. 输入项目名称，启用/禁用Google Analytics（可选）
4. 创建项目

### 2.2 添加Android应用
1. 在项目概览中，点击 "添加应用" > Android图标
2. 输入包名（必须与Unity的 `Player Settings > Package Name` 一致）
3. 下载 `google-services.json`，放入 `Assets` 文件夹

### 2.3 添加iOS应用
1. 在项目概览中，点击 "添加应用" > iOS图标
2. 输入Bundle ID（必须与Unity的 `Player Settings > Bundle Identifier` 一致）
3. 下载 `GoogleService-Info.plist`，放入 `Assets` 文件夹

### 2.4 启用认证提供商
1. 在Firebase控制台，进入 `Authentication > Sign-in method`
2. 启用以下提供商：
   - **Email/Password** (邮箱密码登录)
   - **Google** (Google登录)
   - **Apple** (Apple登录)
   - **Facebook** (Facebook登录)

---

## 3. Google登录配置

### 3.1 Firebase控制台配置
1. 在 `Authentication > Sign-in method > Google` 中启用
2. 记录下 **Web SDK配置中的客户端ID**（用于服务器验证）

### 3.2 Android配置
1. 在 [Google Cloud Console](https://console.cloud.google.com/) 启用 `Google Play Games Services`
2. 配置OAuth 2.0客户端ID
3. 在 `google-services.json` 中确认已包含 `oauth_client`

### 3.2 iOS配置
1. 在 [Apple Developer](https://developer.apple.com/) 配置App ID
2. 启用 `Sign In with Apple` 能力
3. 配置团队ID和Bundle ID

### 3.3 安装Google Sign-In SDK (可选，用于更完整的Google登录体验)
1. 从 [GitHub](https://github.com/googlesamples/google-signin-unity) 下载Google Sign-In SDK
2. 导入Unity项目
3. 在 `FirebaseAuthManager.cs` 中取消注释Google登录部分代码

**重要配置文件位置：**
- Android: `Assets/google-services.json`
- iOS: `Assets/GoogleService-Info.plist`

---

## 4. Apple登录配置

### 4.1 Apple Developer配置
1. 访问 [Apple Developer](https://developer.apple.com/)
2. 创建App ID，启用 `Sign In with Apple` 能力
3. 创建Services ID
4. 配置回调URL：`https://your-project-id.firebaseapp.com/__/auth/handler`

### 4.2 Firebase控制台配置
1. 在 `Authentication > Sign-in method > Apple` 中启用
2. 输入Apple Developer的 **Team ID**
3. 输入 **Key ID** 和 **Private Key**（从Apple Developer获取）

### 4.3 Unity配置
1. 安装 [Unity Apple Sign In 插件](https://assetstore.unity.com/packages/tools/integration/apple-sign-in-146081)
2. 或者在iOS平台使用Native API（需要编写Objective-C代码）

**Apple登录只支持iOS 13+平台和macOS平台**

---

## 5. Facebook登录配置

### 5.1 Facebook Developer配置
1. 访问 [Facebook Developers](https://developers.facebook.com/)
2. 创建新应用
3. 添加 `Facebook Login` 产品
4. 配置有效的OAuth重定向URI：
   ```
   https://your-project-id.firebaseapp.com/__/auth/handler
   ```

### 5.2 获取App ID和App Secret
1. 在Facebook应用设置中，找到 **App ID** 和 **App Secret**
2. 记录这些信息

### 5.3 Firebase控制台配置
1. 在 `Authentication > Sign-in method > Facebook` 中启用
2. 输入Facebook的 **App ID**
3. 输入Facebook的 **App Secret**

### 5.4 安装Facebook SDK for Unity
1. 从 [Facebook SDK for Unity GitHub](https://github.com/facebook/facebook-sdk-for-unity/) 下载SDK
2. 导入Unity项目
3. 在 `Facebook > Edit Settings` 中配置 **App Name** 和 **App ID**
4. 在 `FirebaseAuthManager.cs` 中取消注释Facebook登录部分代码

**重要：Facebook登录需要HTTPS，确保您的游戏使用HTTPS连接**

---

## 6. Unity项目设置

### 6.1 Player Settings配置
1. 打开 `Edit > Project Settings > Player`
2. 配置 **Package Name** (Android) 或 **Bundle Identifier** (iOS)
3. 确保与Firebase控制台中配置的一致

### 6.2 Android配置
1. `Player Settings > Other Settings > Package Name`: `com.yourcompany.yourapp`
2. `Player Settings > Other Settings > Minimum API Level`: API 21 (Android 5.0) 或更高
3. `Player Settings > Publishing Settings > Keystore`: 配置签名密钥

### 6.3 iOS配置
1. `Player Settings > Other Settings > Bundle Identifier`: `com.yourcompany.yourapp`
2. `Player Settings > Other Settings > Target SDK`: Device SDK
3. `Player Settings > Other Settings > Minimum iOS Version`: 11.0 或更高
4. `Player Settings > Signing Team ID`: 输入Apple Developer Team ID

---

## 7. 代码配置

### 7.1 初始化Firebase
在项目启动时调用（已在 `FirebaseAuthManager.cs` 中实现）：
```csharp
FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
{
    if (task.Result == DependencyStatus.Available)
    {
        // Firebase初始化成功
    }
});
```

### 7.2 各平台登录实现

#### Google登录 (移动平台)
```csharp
// 需要使用Google Sign-In SDK
GoogleSignIn.Configuration = new GoogleSignInConfiguration
{
    WebClientId = "YOUR_WEB_CLIENT_ID",
    RequestIdToken = true
};
GoogleSignIn.DefaultInstance.SignIn().ContinueWithOnMainThread(task =>
{
    string idToken = task.Result.IdToken;
    Credential credential = GoogleAuthProvider.GetCredential(idToken);
    auth.SignInWithCredentialAsync(credential);
});
```

#### Apple登录 (iOS)
```csharp
// 需要使用Apple Sign In插件
var appleAuthManager = new AppleAuthManager(...);
appleAuthManager.LoginWithAppleId(
    LoginOptions.IncludeFullName | LoginOptions.IncludeEmail,
    credential =>
    {
        string idToken = credential.IdToken;
        Credential firebaseCredential = OAuthProvider.GetCredential("apple.com", idToken);
        auth.SignInWithCredentialAsync(firebaseCredential);
    },
    error => { /* 处理错误 */ });
```

#### Facebook登录
```csharp
// 需要初始化Facebook SDK
FB.Init(() =>
{
    var perms = new List<string>() { "public_profile", "email" };
    FB.LogInWithReadPermissions(perms, AuthCallback);
});

private void AuthCallback(ILoginResult result)
{
    if (FB.IsLoggedIn)
    {
        var accessToken = AccessToken.CurrentAccessToken.TokenString;
        Credential credential = FacebookAuthProvider.GetCredential(accessToken);
        auth.SignInWithCredentialAsync(credential);
    }
}
```

---

## 8. 测试

### 8.1 编辑器中测试
- Firebase Auth支持在Unity编辑器中测试匿名登录和邮箱登录
- Google/Apple/Facebook登录需要在真实移动设备上测试

### 8.2 Android测试
1. 构建APK并安装到Android设备
2. 确保设备已安装Google Play Services
3. 测试Google登录

### 8.3 iOS测试
1. 构建Xcode项目
2. 在Xcode中配置签名和团队
3. 部署到iOS设备测试

---

## 9. 常见问题

### Q1: Firebase初始化失败
- 检查 `google-services.json` 和 `GoogleService-Info.plist` 是否正确放置
- 运行 `Assets > External Dependency Manager > Android Resolver > Force Resolve`

### Q2: Google登录失败
- 确认 `google-services.json` 中包含正确的OAuth客户端ID
- 检查包名是否一致

### Q3: Apple登录失败
- 确认已在Apple Developer启用Sign In with Apple
- 检查Team ID和Bundle ID配置

### Q4: Facebook登录失败
- 确认App ID和App Secret正确
- 检查OAuth重定向URI配置

---

## 10. 安全建议

1. **不要在客户端存储敏感信息**（如App Secret）
2. **使用Firebase Security Rules** 保护数据库
3. **验证Firebase ID Token** 在后端服务器
4. **启用邮箱验证** 提高安全性
5. **定期更新SDK** 获取安全补丁

---

## 11. 参考资料

- [Firebase Unity SDK文档](https://firebase.google.com/docs/unity/setup)
- [Firebase Auth文档](https://firebase.google.com/docs/auth)
- [Google Sign-In for Unity](https://developers.google.com/identity/sign-in/unity)
- [Facebook SDK for Unity](https://developers.facebook.com/docs/unity/)
- [Apple Sign In文档](https://developer.apple.com/sign-in-with-apple/)

---

**配置完成后，您的Unity项目就可以通过Firebase实现Google、Apple、Facebook登录了！**
