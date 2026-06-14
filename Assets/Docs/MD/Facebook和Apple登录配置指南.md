# FariApp Facebook & Apple 登录 配置指南

> 版本：1.0 | 最后更新：2026-06-14
>
> 本文档涵盖 **Facebook 登录** 和 **Apple 登录**（Sign In With Apple）的完整配置流程。
> Google 登录 请参考同目录下 `Firebase社交登录配置指南.md`。

---

## 目录

- [一、前置条件](#一前置条件)
- [二、Facebook 登录](#二facebook-登录)
  - [2.1 Facebook Developer Console](#21-facebook-developer-console)
  - [2.2 Firebase 控制台配置](#22-firebase-控制台配置)
  - [2.3 安装 Facebook SDK for Unity](#23-安装-facebook-sdk-for-unity)
  - [2.4 Android 平台配置](#24-android-平台配置)
  - [2.5 iOS 平台配置](#25-ios-平台配置)
  - [2.6 Unity Inspector 配置](#26-unity-inspector-配置)
  - [2.7 代码架构](#27-代码架构)
  - [2.8 常见问题](#28-常见问题)
- [三、Apple 登录](#三apple-登录)
  - [3.1 Apple Developer Console 配置](#31-apple-developer-console-配置)
  - [3.2 Firebase 控制台配置](#32-firebase-控制台配置)
  - [3.3 iOS 原生插件架构](#33-ios-原生插件架构)
  - [3.4 Xcode / Unity 构建设置](#34-xcode--unity-构建设置)
  - [3.5 Nonce 安全验证机制](#35-nonce-安全验证机制)
  - [3.6 代码架构](#36-代码架构)
  - [3.7 常见问题](#37-常见问题)
- [四、三种登录方式对比](#四三种登录方式对比)

---

## 一、前置条件

| 依赖项 | 用途 |
|--------|------|
| Firebase Unity SDK (11.x+) | 认证核心 |
| Firebase Auth SDK | 处理 OAuth credential → FirebaseUser |
| Facebook SDK for Unity | Facebook 登录（Android + iOS） |
| Xcode 14+（iOS 构建） | Sign In With Apple Capability |
| Apple Developer 账号（付费） | Apple Sign-In 配置 |

### 账号准备
- **Facebook Developer 账号** — [developers.facebook.com](https://developers.facebook.com/)
- **Apple Developer 账号**（付费 $99/年）— [developer.apple.com](https://developer.apple.com/)

---

## 二、Facebook 登录

### 2.1 Facebook Developer Console

#### 创建应用

1. 打开 [Facebook 开发者平台](https://developers.facebook.com/)
2. 右上角 **My Apps** → **Create App**
3. 选择类型：**Consumer**（非游戏类 App）或 **Games**（游戏类）
4. 填写 Display Name（如 `FariApp`）、联系邮箱
5. 创建

#### 获取 App ID 和 App Secret

1. 应用 Dashboard → **Settings** → **Basic**
2. 复制 **App ID** 和 **App Secret**（App Secret 需要点击 Show 后输入密码确认）
3. 往下滚动，点击 **+ Add Platform** → 选择 **iOS** 和 **Android**
4. Android: 填入 Package Name（`com.canchentechnology.fari`）
5. iOS: 填入 Bundle ID（`com.canchentechnology.fari`）

#### 添加 Facebook Login 产品

1. 左侧菜单 → **Add Product** → **Facebook Login** → **Set Up**
2. 选择平台 → **Web**（Firebase 需要 OAuth Redirect URI）
3. 在 Settings 中，**Valid OAuth Redirect URIs** — 先留空，后面从 Firebase 获取

#### 发布应用

> ⚠️ 开发阶段应用默认处于 "Development Mode"，只有应用管理员和测试用户可以登录。
> 正式上线前需切换到 "Live Mode"。

1. **App Review** → **Permissions and Features**
2. 需要 `public_profile` 和 `email` — 这两个是默认权限，无需额外审核
3. 设置 → **Basic** → 填写 **Privacy Policy URL**
4. 顶部开关 → **Live Mode**

---

### 2.2 Firebase 控制台配置

1. 打开 [Firebase 控制台](https://console.firebase.google.com/) → 选择 fari-app-b2fd2 项目
2. **Authentication** → **Sign-in method** → **Facebook** → 点击编辑 ✏️
3. 选择 **启用**
4. **App ID**: 填入 Facebook Developer Console 的 App ID
5. **App Secret**: 填入 Facebook Developer Console 的 App Secret
6. 点击保存后，Firebase 会显示一个 **OAuth Redirect URI**（格式：`https://fari-app-b2fd2.firebaseapp.com/__/auth/handler`）
7. **复制这个 URI**

#### 将 Redirect URI 添加到 Facebook

回到 Facebook Developer Console：
1. **Facebook Login** → **Settings**
2. **Valid OAuth Redirect URIs** → 粘贴 Firebase 提供的 URI
3. **Save Changes**

---

### 2.3 安装 Facebook SDK for Unity

> 代码使用**反射**调用 Facebook SDK，不依赖编译期引用。SDK 未安装时 Editor 走模拟模式。

1. 下载：https://developers.facebook.com/docs/unity/downloads/
2. 解压后得到 `FacebookSDK.unitypackage`
3. Unity → **Assets** → **Import Package** → **Custom Package**
4. 导入时建议勾选：
   - ✅ Facebook SDK (Core)
   - ✅ Login
   - ❌ Gaming Services（不需要）
   - ❌ 其他按需选择

#### Unity 中配置 Facebook

导入 SDK 后：
1. 菜单栏出现 **Facebook** → **Edit Settings**
2. 填入 **App ID**（来自 Facebook Developer Console）
3. SDK 会自动生成：
   - Android: `Assets/Plugins/Android/FacebookSDK.androidlib/`（Manifest + strings.xml）
   - iOS: Info.plist 配置（构建时自动注入）

---

### 2.4 Android 平台配置

Facebook SDK 导入后会自动生成 `Assets/Plugins/Android/FacebookSDK.androidlib/`。

**确认以下文件存在：**

```
Assets/Plugins/Android/FacebookSDK.androidlib/
├── AndroidManifest.xml        ← 注册 FacebookActivity
├── project.properties
├── libs/                      ← Facebook SDK JAR
└── res/
    └── values/
        └── strings.xml        ← 包含 facebook_app_id 字符串资源
```

**AndroidManifest.xml 应包含：**

```xml
<activity android:name="com.facebook.FacebookActivity"
    android:configChanges="keyboard|keyboardHidden|screenLayout|screenSize|orientation"
    android:theme="@android:style/Theme.Translucent.NoTitleBar" />

<meta-data android:name="com.facebook.sdk.ApplicationId"
    android:value="@string/facebook_app_id" />
```

**strings.xml 应包含：**

```xml
<string name="facebook_app_id">你的AppID</string>
```

> ⚠️ 不要直接在 Manifest 中写死 App ID，用 `@string/facebook_app_id` 引用 strings.xml，方便多环境切换。

---

### 2.5 iOS 平台配置

Facebook SDK 导入后会自动处理 `Info.plist`。构建完成后确认 Xcode 项目中包含：

**Info.plist 添加的字段：**

| Key | Value | 说明 |
|-----|-------|------|
| `FacebookAppID` | `你的AppID` | Facebook 应用 ID |
| `FacebookDisplayName` | `FariApp` | 显示名称 |
| `CFBundleURLTypes` → `CFBundleURLSchemes` | `fb你的AppID` | URL Scheme 回调（如 `fb123456789`） |
| `LSApplicationQueriesSchemes` | `fbapi`, `fb-messenger-api`, `fbauth2` | 允许跳转到 Facebook App |

---

### 2.6 Unity Inspector 配置

在场景中挂载 `FacebookSignInHelper` 组件的 GameObject 上填写：

| 参数 | 说明 | 示例值 |
|------|------|--------|
| **Facebook App Id** | Facebook Developer Console 获取 | `123456789012345` |
| **Permissions** | 请求的权限列表 | `public_profile`, `email` |
| Editor Simulate Delay | Editor 模拟延迟（秒） | 1 |
| Editor Simulate Success | Editor 模拟是否成功 | true |

> ⚠️ FacebookAppId 必须填写，真机登录时 SDK 用它来识别你的应用。

---

### 2.7 代码架构

```
LoginUI 按钮点击
    │
    ▼
FirebaseAuthManager.SignInWithFacebook()
    │
    ▼
FacebookSignInHelper.SignIn(onSuccess, onError)
    │
    ├── #if UNITY_EDITOR → 模拟模式，返回假 Access Token
    │
    └── #else（Android / iOS 真机）
        │
        ├── FB.Init() → SDK 初始化（Awake 中自动调用）
        ├── FB.LogInWithReadPermissions("public_profile", "email", callback)
        │     └── 用户看到 Facebook 登录页 / Facebook App
        └── ILoginResult
            ├── Cancelled → FinishWithError("用户取消了 Facebook 登录")
            ├── Error → FinishWithError(error)
            └── Success → AccessToken.TokenString
                │
                ▼
        FacebookAuthProvider.GetCredential(accessToken)
                │
                ▼
        FirebaseAuthManager.SignInWithCredential()
```

**文件结构：**

```
Assets/Scripts/Platform/Facebook/
└── FacebookSignInHelper.cs     ← 使用反射调用 FB SDK，避免硬依赖

Assets/Plugins/Android/
└── FacebookSDK.androidlib/     ← 安装 SDK 后自动生成

Assets/Firebase/                ← Firebase Unity SDK
```

---

### 2.8 常见问题

#### Q1: "FB" 类型未找到 / Facebook SDK 未安装

**现象**: Console 显示 "Facebook SDK 未安装，登录功能将不可用"
**原因**: 未导入 Facebook SDK for Unity 的 .unitypackage
**解决**:
1. 下载：https://developers.facebook.com/docs/unity/downloads/
2. Assets → Import Package → Custom Package → 选择 FacebookSDK.unitypackage
3. Editor 会自动使用模拟模式（无需 SDK 也能跑通 UI 流程）

#### Q2: 登录弹窗打开后立即关闭 / 无反应

**可能原因**:
1. Facebook App 处于 Development Mode，当前 Facebook 账号不是管理员/测试用户
2. Facebook App 的 Redirect URI 未配置或在 Firebase 中 App ID/Secret 填错
3. Android: 设备未安装 Facebook App 且 WebView 版本过旧

**排查**:
```
1. Facebook Developer Console → App Review → Permissions
   - public_profile 和 email 应该是 "Ready for testing" 状态
2. 确认 Firebase → Auth → Facebook 中 App ID 和 App Secret 正确
3. 确认 Facebook Login → Settings → Valid OAuth Redirect URIs 包含 Firebase 提供的 URI
```

#### Q3: "Invalid OAuth redirect URI"

**原因**: Facebook 和 Firebase 之间的 OAuth 回调地址不匹配
**解决**:
1. Firebase 控制台 → Auth → Facebook → 复制 OAuth Redirect URI
2. Facebook Developer Console → Facebook Login → Settings → 粘贴到 Valid OAuth Redirect URIs
3. Save Changes

#### Q4: iOS 上 Facebook 登录跳到 Safari 而不是 Facebook App

**正常现象**: 如果用户未安装 Facebook App，系统会自动降级到 Safari Web 登录。不影响功能。

---

## 三、Apple 登录

> Apple Sign-In 仅在 **iOS 13+** 和 **macOS 10.15+** 真机上可用。
> Android 和 Editor 自动走模拟模式。

### 3.1 Apple Developer Console 配置

#### 启用 App ID 的 Sign In With Apple

1. 登录 [Apple Developer Portal](https://developer.apple.com/account/)
2. **Certificates, Identifiers & Profiles** → **Identifiers**
3. 选择你的 App ID（Bundle ID: `com.canchentechnology.fari`）或创建新的
4. 勾选 **Sign In With Apple** → 选择 **Enable as a primary App ID**
5. 保存

#### 创建 Services ID（Firebase 需要）

1. **Identifiers** → 点击 **+** → 选择 **Services IDs**
2. Description: `FariApp Apple Sign-In`
3. Identifier: `com.canchentechnology.fari.signin`
4. 勾选 **Sign In With Apple**
5. 点击 **Configure** → **Primary App ID** → 选择上面创建的 App ID
6. **Domains**: 填写 `fari-app-b2fd2.firebaseapp.com`
7. **Return URLs**: 填写 `https://fari-app-b2fd2.firebaseapp.com/__/auth/handler`
8. 注册

#### 生成 Apple 私钥（.p8 文件）

> ⚠️ `.p8` 文件只能下载一次！下载后妥善保管。

1. **Keys** → 点击 **+**
2. Key Name: `FariApp Apple Sign-In Key`
3. 勾选 **Sign In With Apple** → 配置 → 选择 Primary App ID
4. 注册
5. 下载 `.p8` 文件
6. 记录 **Key ID**（10 位字符串，如 `ABC123DEF4`）
7. 记下 **Team ID**（在右上角账号菜单 → Membership 中查看）

---

### 3.2 Firebase 控制台配置

1. [Firebase 控制台](https://console.firebase.google.com/) → Authentication → Sign-in method
2. **Apple** → 点击编辑 ✏️
3. 选择 **启用**
4. 填写以下信息：

| 字段 | 来源 | 说明 |
|------|------|------|
| **Services ID** | Apple Developer → Identifiers → Services IDs | `com.canchentechnology.fari.signin` |
| **Key ID** | Apple Developer → Keys | 10 位字符串 |
| **Team ID** | Apple Developer → Membership | 10 位字符串 |
| **Private Key** | 下载的 .p8 文件 | 用文本编辑器打开，复制完整内容（含 `-----BEGIN PRIVATE KEY-----` 头尾） |

5. 点击 **保存**

---

### 3.3 iOS 原生插件架构

> FariApp 使用**自写原生 Objective-C++ 插件**实现 Apple Sign-In，不需要任何第三方 Unity 插件或 CocoaPod。
> 系统 `AuthenticationServices` 框架是 iOS 13+ 内置的，无需额外依赖。

```
Assets/Plugins/iOS/FariAppleSignIn.mm
├── FariAppleSignInDelegate          ← ASAuthorizationControllerDelegate 代理
│   ├── authorizationController:didCompleteWithAuthorization:
│   │     └── 提取 idToken + authCode + fullName + email
│   └── authorizationController:didCompleteWithError:
│         └── 错误映射: Canceled / Failed / NotHandled
│
└── C 接口 (extern "C")
    ├── _fariAppleIsSupported()      ← 检查 iOS 13+
    └── _fariAppleStartSignIn(gameObject, callback, sha256Nonce)
          ├── ASAuthorizationAppleIDProvider → createRequest
          ├── 设置 requestedScopes (FullName, Email)
          ├── 设置 nonce (SHA256 哈希值)
          └── ASAuthorizationController → performRequests
```

**C# 侧 P/Invoke 绑定**（`AppleSignInHelper.cs`）：

```csharp
[DllImport("__Internal")]
private static extern void _fariAppleStartSignIn(
    string gameObjectName, string callbackMethod, string sha256Nonce);
```

**回调格式**（与 Google 登录体系一致）：

| 回调字符串 | 含义 |
|-----------|------|
| `SUCCESS:idToken\|authCode\|fullName\|email` | 登录成功 |
| `FAILURE:<code>:<message>` | 登录失败 |
| `CANCELED` | 用户取消 |

---

### 3.4 Xcode / Unity 构建设置

#### Unity Player Settings（iOS）

| 设置项 | 位置 | 说明 |
|--------|------|------|
| Bundle Identifier | Player Settings → iOS → Other Settings | 必须与 Apple Developer 的 App ID 一致 |
| Target minimum iOS Version | Player Settings → iOS → Other Settings | 必须 ≥ 13.0 |
| Camera Usage Description | Player Settings → iOS → Other Settings | Face ID 扫描需要（可填 "用于身份验证"） |

#### Xcode Capability

构建出 Xcode 项目后：
1. 打开 `.xcworkspace`（不是 `.xcodeproj`）
2. 选择 Target → **Signing & Capabilities**
3. 点击 **+ Capability** → 搜索 **Sign In With Apple** → 添加
4. 确认 Team 选择了正确的 Apple Developer 账号

#### 文件位置确认

```
Assets/Plugins/iOS/
└── FariAppleSignIn.mm              ← 原生插件（已创建）

Assets/StreamingAssets/
└── GoogleService-Info.plist        ← Firebase 配置（Google 登录已用到）
```

> Apple Sign-In 不需要 CocoaPod（`AuthenticationServices` 是系统框架，不通过 pod 管理）。
> 不需要独立的 Dependencies.xml。

---

### 3.5 Nonce 安全验证机制

Apple Sign-In 使用 Nonce 来防止重放攻击：

```
AppleSignInHelper.SignIn()
    │
    ├── 1. 生成随机 rawNonce（32位十六进制）
    │      rawNonce = GenerateNonce()        // 如: "a1b2c3...8f9e"
    │
    ├── 2. SHA256 哈希
    │      sha256Nonce = SHA256(rawNonce)    // 如: "5e884898..."
    │
    ├── 3. 传 sha256Nonce 给原生插件
    │      _fariAppleStartSignIn(callback, sha256Nonce)
    │
    ├── 4. 原生插件: request.nonce = sha256Nonce
    │      → Apple 服务器在 ID Token 的 nonce 字段中包含此值
    │
    ├── 5. Apple 返回 idToken + authCode + userInfo
    │
    └── 6. C# 用原始 rawNonce + idToken 创建 Firebase credential
           OAuthProvider.GetCredential("apple.com", idToken, rawNonce, null)
```

> ⚠️ Firebase 验证时传入的是**原始 Nonce**，不是 SHA256 哈希值。Apple 要求请求时传入 SHA256 哈希。

---

### 3.6 代码架构

```
LoginUI 按钮点击
    │
    ▼
FirebaseAuthManager.SignInWithApple()
    │
    ▼
AppleSignInHelper.SignIn(onSuccess, onError)
    │
    ├── 生成 Nonce + SHA256(nonce)
    │
    ├── #if UNITY_IOS && !UNITY_EDITOR
    │     └── _fariAppleStartSignIn(gameObject, callback, sha256Nonce)
    │           → ASAuthorizationController
    │           → 用户 Face ID / Touch ID / 密码验证
    │           → UnitySendMessage: "SUCCESS:idToken|authCode|fullName|email"
    │
    └── #else (Editor / Android)
          └── 模拟模式，返回假 Token（1 秒延迟）
                │
                ▼
        OAuthProvider.GetCredential("apple.com", idToken, rawNonce, null)
                │
                ▼
        FirebaseAuthManager.SignInWithCredential()
```

**文件结构：**

```
Assets/
├── Plugins/iOS/
│   └── FariAppleSignIn.mm              ← iOS 原生插件（已创建）
├── Scripts/Platform/Apple/
│   └── AppleSignInHelper.cs            ← C# 封装 + P/Invoke
└── Scripts/Platform/FireBase/
    └── FirebaseAuthManager.cs          ← 统一认证入口
```

---

### 3.7 常见问题

#### Q1: "FAILURE:0:Apple Sign-In requires iOS 13.0 or later"

**原因**: 设备 iOS 版本 < 13.0 或构建的 Deployment Target 太低
**解决**:
1. Unity Player Settings → iOS → Target minimum iOS Version → 选择 13.0 或更高
2. 确认测试设备 iOS 版本 ≥ 13.0

#### Q2: "FAILURE:0:Authorization request was not handled"

**原因**: Xcode 项目未启用 Sign In With Apple Capability
**解决**:
1. Xcode 打开项目 → Target → Signing & Capabilities
2. 添加 **Sign In With Apple** capability
3. 重新构建

#### Q3: 登录成功但 Firebase 报错 "INVALID_ID_TOKEN"

**可能原因**:
1. Nonce 不匹配 — 传给 Firebase 的 rawNonce 和传给 Apple 的 sha256Nonce 不是同一对
2. Firebase 中 Services ID 配置错误
3. Apple 私钥 / Key ID / Team ID 不匹配

**排查**:
```
1. 确认 Firebase → Auth → Apple 中 Services ID / Key ID / Team ID 正确
2. 确认 .p8 文件内容完整（含 BEGIN/END PRIVATE KEY 行）
3. 如果 UseNonce=false，尝试去掉 nonce 看是否能登录成功
```

#### Q4: 第二次登录时拿不到 fullName 和 email？

**这是 Apple 的设计行为**：Apple 只在用户**首次授权**时返回 fullName 和 email。后续登录 Apple 不会再提供这些信息（隐私保护）。

**应对方案**：
- 首次登录成功后，在 Firebase User 中保存用户信息（DisplayName, Email）
- 后续登录从 Firebase 读取，不依赖 Apple 返回

#### Q5: Apple 登录在 Android 上能用吗？

**不能。** Apple Sign-In 是 Apple 生态专属功能，仅限 iOS 13+ / macOS 10.15+ / tvOS 13+。
Android 上代码会自动走模拟模式，不会崩溃。

#### Q6: 编辑器测试 Apple 登录？

Editor 代码自动走**模拟模式**：1 秒延迟后返回假 Token。UI 交互和登录成功后的页面跳转都可以在 Editor 中验证。

---

## 四、三种登录方式对比

| 维度 | Google | Facebook | Apple |
|------|--------|----------|-------|
| **覆盖平台** | Android + iOS | Android + iOS | 仅 iOS 13+ / macOS |
| **是否必需真机测试** | 是（Google Play Services / iOS 系统框架） | 是（Facebook App / WebView） | 是（Face ID / Touch ID） |
| **原生插件实现** | 自写 JAR + .mm | Facebook SDK for Unity | 自写 .mm（系统框架） |
| **所需的第三方依赖** | `play-services-auth` (Android) / `GoogleSignIn` CocoaPod (iOS) | Facebook SDK .unitypackage | 无（iOS 系统内置） |
| **Firebase Credential** | `GoogleAuthProvider.GetCredential(idToken)` | `FacebookAuthProvider.GetCredential(accessToken)` | `OAuthProvider.GetCredential("apple.com", idToken, nonce)` |
| **Token 类型** | ID Token (JWT) | Access Token (opaque) | ID Token (JWT) + Authorization Code |
| **Apple Developer 账号要求** | 不需要 | 不需要 | ✅ 需要（付费） |
| **Editor 模拟** | ✅ | ✅ | ✅ |
| **首次返回信息** | Email, DisplayName, Photo | Email, Name, Photo（按权限） | Email, FullName（仅首次） |
| **代码位置** | `Scripts/Platform/Google/GoogleSignInHelper.cs` | `Scripts/Platform/Facebook/FacebookSignInHelper.cs` | `Scripts/Platform/Apple/AppleSignInHelper.cs` |

---

## 附录 A：快速配置检查清单

### Facebook 登录
- [ ] Facebook Developer Console 已创建应用
- [ ] App ID 和 App Secret 已复制
- [ ] Facebook Login 产品已添加
- [ ] Firebase → Auth → Facebook 已启用（填入 App ID + App Secret）
- [ ] Firebase OAuth Redirect URI 已添加到 Facebook 设置
- [ ] Facebook SDK for Unity 已导入
- [ ] Unity → Facebook → Edit Settings → App ID 已填写
- [ ] Android: `FacebookSDK.androidlib/` 中的 Manifest + strings.xml 正确
- [ ] `FacebookSignInHelper.FacebookAppId` 已填写（Inspector）

### Apple 登录
- [ ] Apple Developer: App ID 已启用 Sign In With Apple
- [ ] Services ID 已创建（含 Domain 和 Return URL）
- [ ] .p8 私钥已下载（Key ID + Team ID 已记录）
- [ ] Firebase → Auth → Apple 已启用（含 Services ID / Key ID / Team ID / Private Key）
- [ ] Xcode: Signing & Capabilities 中已添加 Sign In With Apple
- [ ] Unity: Target minimum iOS Version ≥ 13.0
- [ ] `FariAppleSignIn.mm` 已放入 `Assets/Plugins/iOS/`
- [ ] `AppleSignInHelper` 已挂载到场景 GameObject

---

## 附录 B：与现有 Firebase 体系的关系

三种登录方式最终都汇聚到 `FirebaseAuthManager` 的统一入口：

```csharp
// Google
FirebaseAuthManager.Instance.SignInWithGoogle();

// Facebook
FirebaseAuthManager.Instance.SignInWithFacebook();

// Apple
FirebaseAuthManager.Instance.SignInWithApple();

// 匿名
FirebaseAuthManager.Instance.SignInAnonymously();
```

每个方法内部：
1. 调用对应 Helper 获取平台 Token
2. 用 Token 创建 `Firebase.Auth.Credential`
3. 调用 `auth.SignInWithCredentialAsync(credential)`
4. 成功后同步用户数据到 `UserDataManager`

> 所有登录方式的 Editor 模拟、防重复点击、状态管理逻辑都已内置。
> 参考 `FirebaseAuthManager.cs` 的 `SignInWithCredential` 方法了解完整实现。
