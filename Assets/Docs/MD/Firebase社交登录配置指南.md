# FariApp Firebase 社交登录 配置指南

> 版本：4.3 | 最后更新：2026-06-13
>
> ⚠️ **v4.3 重大变更**：
> - **v4.2**：Android 端从 `AndroidJavaClass` 直接调用改为 **Java 插件方案**（`FariSignInActivity`），解决 `onActivityResult` 无法接收的问题
> - **v4.3**：新增 **iOS 平台支持**（Objective-C++ 原生插件 `FariGoogleSignIn.mm` + CocoaPod `GoogleSignIn ~> 7.0`）
> - 原因：Play Games SDK 强制应用类型为"游戏"，不适合非游戏类 App（如 FariApp）
> - 新方案不需要 Play Console PGS 配置、不需要 Resources Definition，App 类型正常选"应用"即可

---

## 目录

- [一、前置条件](#一前置条件)
- [二、核心概念：Firebase 与第三方 SDK 的分工](#二核心概念firebase-与第三方-sdk-的分工)
- [三、Firebase 项目基础配置](#三firebase-项目基础配置)
- [四、Google 登录配置](#四google-登录配置)
  - [4.1 旧插件清理](#41-旧插件清理)
  - [4.2 添加 play-services-auth 依赖（Android）](#42-添加-play-services-auth-依赖android)
  - [4.3 添加 GoogleSignIn CocoaPod 依赖（iOS）](#43-添加-googlesignin-cocoapod-依赖ios)
  - [4.4 Firebase 控制台启用 Google 登录](#44-firebase-控制台启用-google-登录)
  - [4.5 添加 SHA-1 签名到 Firebase（Android 必须）](#45-添加-sha-1-签名到-firebaseandroid-必须)
  - [4.6 Unity Inspector 配置](#46-unity-inspector-配置)
  - [4.7 iOS 额外配置](#47-ios-额外配置)
  - [4.8 双平台登录流程说明](#48-双平台登录流程说明)
  - [4.9 Android Java 插件架构](#49-android-java-插件架构)
  - [4.10 iOS 原生插件架构](#410-ios-原生插件架构)
  - [4.11 技术实现原理](#411-技术实现原理)
  - [4.12 登录后获取用户信息](#412-登录后获取用户信息)
  - [4.13 常见问题](#413-常见问题)
- [五、Apple 登录配置](#五apple-登录配置)
- [六、Facebook 登录配置](#六facebook-登录配置)
- [七、Unity 项目侧配置](#七unity-项目侧配置)
- [八、配置完成后的下一步：挂载组件与测试](#八配置完成后的下一步挂载组件与测试)
- [九、代码架构说明](#九代码架构说明)
- [十、常见问题排查](#十常见问题排查)

---

## 一、前置条件

| 依赖项 | 版本要求 | 用途 |
|--------|----------|------|
| Unity | 2019.4+ (推荐 2022.3+) | 引擎 |
| Firebase Unity SDK | 11.x+ (本项目使用 13.12.0) | 认证核心 |
| Google Sign-In (play-services-auth) | 21.2.0+ | Google 登录（通过 AndroidJavaClass 调用原生 API） |
| Facebook SDK for Unity | 最新版 | Facebook 登录 |
| apple-signin-unity 插件（可选） | 最新版 | Apple 登录增强 |
| External Dependency Manager (EDM) | 内置于 Firebase SDK | 自动管理 Android 依赖 |

### 账号准备
- **Google 账号** — 用于 Firebase 控制台和 Google Cloud Console
- **Apple Developer 账号**（付费）— 用于 Apple Developer Console
- **Facebook Developer 账号** — 用于 Facebook Developer Console

---

## 二、核心概念：Firebase 与第三方 SDK 的分工

### 为什么启用 Firebase Google 登录后还要装额外的插件？

这是最常见的疑问。简单来说：**Firebase 没有替代 Google 登录 SDK，而是站在它上面做了一层统一认证。**

### 两步理解整个流程

#### 第一步：第三方 SDK（插件）— 获取用户凭证

```
用户手机
  │
  ▼ 点击 Google 登录按钮
  │
  ├─ Google Sign-In 插件（你额外装的）
  │    → 调用系统 Google Play Services / GMS（Android）
  │    → 或调用系统 Web 认证框架（iOS）
  │    → 弹出 Google 官方登录弹窗（选账号、确认授权）
  │    → 拿到一个 **ID Token** 或 **Access Token**
  │    └── 这步 Firebase 做不了！因为这是操作系统层面的 OAuth 流程
```

> **为什么 Firebase 做不了这一步？**
>
> - Google 登录需要调用设备上的 **Google Play Services**（Android）或 **系统 Web 认证框架**（iOS）
> - 这涉及原生平台 API、UI 弹窗、账号选择器 —— 这些是 **客户端 UI/SDK 层面的事**
> - Firebase Auth 是一个**服务端认证服务**，不负责"弹出 Google 选择账号的窗口"

#### 第二步：Firebase — 凭证验证 + 统一用户管理

```
拿到 ID Token 后
  │
  ▼
  FirebaseAuthManager.SignInWithCredential(token)
  │
  ├─ 把 Token 发给 Firebase 服务端
  ├─ Firebase 验证这个 Token 是否是 Google 签发的、是否有效
  ├─ 验证通过后：
  │   ├── 创建或返回 Firebase 用户记录（FirebaseUid）
  │   ├── 关联到你的 Firebase 项目
  │   └── 返回完整的 FirebaseUser 对象
  │
  └── 你拿到的不再是一个 "Google 用户"，而是一个 "Firebase 用户"
      （只是恰好是用 Google 方式登录的）
```

### 一张图看明白整体架构

```
┌─────────────────────────────────────────────────────┐
│                   你的 App                           │
│                                                     │
│  LoginUI                                            │
│     │                                               │
│     ▼                                               │
│  FirebaseAuthManager  ←───────── 这里是 Firebase 的作用 │
│     │                         (服务端身份验证 + 统一)  │
│     │ SignInWithCredential()                        │
│     │                                              │
│     ▼                                              │
│  GoogleSignInHelper  ←─── AndroidJavaClass (原生 Google Sign-In)│
│     │                    (获取 ID Token)                         │
│     ▼                                             │
│  系统: Google Play Services                         │
│     (标准 Google 登录弹窗、账号选择、OAuth 授权)      │
└─────────────────────────────────────────────────────┘

         ↑                                          ↑
    客户端 SDK 层                              服务端认证层
    (AndroidJavaClass 原生 API)               (Firebase 提供)
```

### 如果不用 Firebase 会怎样？

你可以只装 Google SDK 不用 Firebase —— 但接下来你需要：
- 自己建数据库存用户？
- 自己写服务端验证 Token 有效性？
- 要支持 Apple/Facebook 登录又要再写一套？

**Firebase 的价值**：
- ✅ **统一入口**：不管用什么方式登录（Google/Apple/Facebook/邮箱/匿名），到手里都是同一个 `FirebaseUser` 对象
- ✅ **Token 验证**：自动验证第三方 Token 的有效性、过期时间
- ✅ **账号关联**：同一用户先用 Google 登、再用 Facebook 登，可以关联成同一个账号
- ✅ **用户管理后台**：Firebase Console 直接查看所有用户列表、禁用账号等
- ✅ **安全规则**：配合 Firestore/Realtime Database 做权限控制

### 类比记忆

| 角色 | 类比 |
|------|------|
| **AndroidJavaClass 原生 API** | 护照（证明你是你，由 Google Play Services 发行） |
| **Firebase Auth** | 入境海关（验证护照真假、给你发签证/FirebaseUser） |
| **你的 App** | 目的地国家（只认 Firebase 签证，不关心你怎么进来的） |

---

## 三、Firebase 项目基础配置

### 3.1 Firebase 项目与 GCP 项目的关系

> ⚡ **重要概念**：当你创建 Firebase 项目时，Google 会在背后**同时创建一个同名的 GCP（Google Cloud Platform）项目**。两者是 **1:1 映射关系**。

```
Firebase Console                    Google Cloud Console
     │                                      │
     ▼                                      ▼
┌──────────────┐                  ┌──────────────────┐
│ fari-app   │  ◄── 同一个项目 ──►│ fari-app       │
│ (Firebase 视角)│                   │ (GCP 视角)        │
└──────────────┘                  └──────────────────┘
```

**如何跳转到 GCP Console：**
1. 打开 [Firebase 控制台](https://console.firebase.google.com/)
2. 点击左侧 **⚙️ 设置（齿轮图标）**
3. 进入设置页 → 往下滚动 → 找到 **"您的云平台项目"** 区域
4. 点击旁边的链接即可跳转（通常显示为 **"在 Google Cloud Console 中管理"**）

> 💡 **实际上你可能不需要去 GCP Console**。大多数情况下 Firebase 启用第三方登录时会自动创建所需的 OAuth 凭据。只有需要自定义 OAuth 配置时才需要手动操作。

### 3.2 创建 Firebase 项目

1. 打开 [Firebase 控制台](https://console.firebase.google.com/)
2. 点击「**添加项目**」→ 输入项目名（如 `fari-app`）
3. 按向导完成创建（可关闭 Analytics）

### 3.3 注册应用（双端）

在 Firebase 控制台 → **项目设置** → **您的应用** 中，分别注册 iOS 和 Android 应用。

> ⚠️ **关键规则：iOS 和 Android 必须在同一个 Firebase 项目中各注册为一个 App！不要分别建两个项目。**

#### Android 应用 — 获取 `google-services.json`

```
Firebase 控制台 → 项目设置 → 您的应用
    │
    ├── 点击「添加应用」→ 选择 🤖 Android 图标
    │
    ├── 填写：
    │   ├── Android 包名：com.yourcompany.fari
    │   │   （与 Unity Player Settings 的 Package Name 完全一致）
    │   ├── 应用昵称：随意（如 FariApp）
    │   └── 调试签名证书 SHA-1/SHA-256：
    │       keytool -list -v -alias androiddebugkey -keystore ~/.android/debug.keystore
    │       # 密码: android
    │       # 开发阶段可先不填，后面再加
    │
    ├── 点「注册应用」→ 「继续」
    │
    └── 下载 google-services.json → 放入 Assets/ 根目录
```

#### iOS 应用 — 获取 `GoogleService-Info.plist`

```
Firebase 控制台 → 项目设置 → 您的应用
    │
    ├── 点击「添加应用」→ 选择 🍎 Apple (iOS) 图标
    │
    ├── 填写：
    │   ├── iOS Bundle ID：com.yourcompany.fari
    │   │   （与 Unity Player Settings 的 Bundle Identifier 一致）
    │   │   通常和 Android 包名使用同一个值
    │   ├── 应用昵称：随意
    │   └── App Store ID：暂可不填（上架后再补）
    │
    ├── 点「注册应用」→ 「继续」
    │
    └── 下载 GoogleService-Info.plist → 放入 Assets/StreamingAssets/
```

### 3.4 最终文件结构

```
Assets/
├── google-services.json              ← Android 配置文件（放根目录）
├── StreamingAssets/
│   └── GoogleService-Info.plist      ← iOS 配置文件（放这里）
│
├── Firebase/
├── GooglePlayGames/                   ← 已移除（不再使用 Play Games SDK）
└── Scripts/
```

### 3.5 双端配置注意事项

| 要点 | 说明 |
|------|------|
| **Bundle ID 必须一致** | Android 包名和 iOS Bundle ID 通常用同一个值（如 `com.yourcompany.fari`） |
| **同一个 Firebase 项目** | 不要分别建两个项目！iOS 和 Android 在同一个项目里各注册一个 App |
| **改了包名要重新下载** | 在 Unity 里改了 Package Name / Bundle ID 后，必须回 Firebase 重新生成并替换这两个文件 |
| **SHA 签名** | 开发阶段可先不填；正式上线前必须加 release 签名的 SHA |

> 💡 如果还没确定最终包名，可以先填一个临时的（如 `com.test.fari`），等定下来了再去 Firebase 更新并重新下载配置文件替换。

### 3.6 启用匿名登录

> 匿名登录是跳过登录功能的基础。

1. 进入 **Authentication** → **Sign-in method**
2. 点击 **匿名** → 启用 → **保存**

---

## 四、Google 登录配置（Android 原生 Java 插件 + iOS 原生 ObjC++ 插件）

> ⚠️ **v4.2+ 重大变更**：
>
> **Android 端（v4.2）**：从 `AndroidJavaClass` 直接调用改为 **Java 插件方案**（`FariSignInActivity`）
> - 原因：Unity 2022.3 没有 `IActivityResultListener` 接口，`startActivityForResult` 的结果无法在 C# 中接收
> - 新方案：Java 插件自行处理 `onActivityResult`，通过 `UnitySendMessage` 回调 C#
>
> **iOS 端（v4.3）**：新增 **iOS 平台完整支持**
> - 原生 Objective-C++ 插件 `FariGoogleSignIn.mm`
> - 通过 P/Invoke `[DllImport("__Internal")]` 调用
> - CocoaPod 依赖 `GoogleSignIn ~> 7.0`
>
> **两平台统一回调格式**：`SUCCESS:<idToken>` / `FAILURE:<code>:<msg>` / `CANCELED`

### 4.1 旧插件清理

在继续之前，**必须先删除旧版插件文件**：

```
在 Unity Project 窗口中，右键删除以下目录（如果存在）：
❌ Assets/GoogleSignIn/                  ← 旧版 google-signin-unity 插件（整个删除）
❌ Assets/GeneratedLocalRepo/GoogleSignIn/  ← 旧插件缓存（整个删除）
❌ Assets/GooglePlayGames/               ← Play Games SDK 插件（整个删除，如果已安装）
❌ Assets/Plugins/iOS/GoogleSignIn/      ← 旧版 iOS Google Sign-In 插件（整个删除）
```

> ⚠️ 必须在 Unity Editor 中删除（不是直接在文件管理器删），让 Unity 正确处理 .meta 文件。

删除后检查 `Assets/Plugins/Android/mainTemplate.gradle`，确认以下旧依赖行已移除（如果存在）：
```gradle
// 删除这行（如果存在）：
implementation 'com.google.signin:google-signin-support:1.0.4'
```

然后运行：**菜单栏 Assets → External Dependency Manager → Android Resolver → Force Resolve**

### 4.2 添加 play-services-auth 依赖（Android）

在 `Assets/Plugins/Android/mainTemplate.gradle` 的 dependencies 中确认包含：

```gradle
implementation 'com.google.android.gms:play-services-auth:21.2.0' // Google Sign-In 原生 API
```

> 💡 此依赖提供 `GoogleSignIn`、`GoogleSignInOptions`、`GoogleSignInClient` 等 Java 类，
> 供 `FariSignInActivity` Java 插件调用。

然后运行：**菜单栏 Assets → External Dependency Manager → Android Resolver → Force Resolve**

### 4.3 添加 GoogleSignIn CocoaPod 依赖（iOS）

在 `Assets/Plugins/iOS/FariGoogleSignInDependencies.xml` 中声明：

```xml
<?xml version="1.0" encoding="UTF-8"?>
<dependencies>
  <iosPod name="GoogleSignIn" version="~> 7.0" />
</dependencies>
```

> 💡 Unity iOS Resolver 会在 Xcode 构建时自动执行 `pod install`。
> 确保构建前已运行 **Assets → External Dependency Manager → iOS Resolver → Settings** 检查 Podfile 生成配置。

### 4.4 Firebase 控制台启用 Google 登录

1. 打开 [Firebase 控制台](https://console.firebase.google.com/)
2. **Authentication** → **Sign-in method** → 点击 **Google**
3. 选择 **启用**
4. 填写 **项目支持邮箱**
5. **⭐ 复制 Web Client ID**（格式类似 `86394...ffm.apps.googleusercontent.com`）
6. 点击 **保存**

> ⚠️ **Web Client ID 是关键配置**，必须保存好，后面 Unity Inspector 中要填！

### 4.5 ⭐ 添加 SHA-1 签名到 Firebase（Android 必须）

> ⚠️ **这是 Google 登录最常见的坑！不添加 SHA-1 签名，交互式登录会直接返回 CANCELED，用户根本没机会操作选择账号。**

#### 获取签名 SHA-1

```bash
# 开发签名（debug keystore）
keytool -list -v -alias androiddebugkey -keystore ~/.android/debug.keystore
# 密码: android

# 自定义签名（release keystore）
keytool -list -v -keystore <your-keystore-path> -alias <your-alias>
# 输入你的 keystore 密码
```

#### 添加到 Firebase

1. 打开 Firebase Console → 项目设置 → 你的 Android 应用
2. 找到 **SHA 证书指纹** 部分
3. 点击 **添加指纹**，粘贴上面的 SHA-1 值
4. **下载最新的 `google-services.json`** 替换到项目 `Assets/` 目录
5. **重新打包部署**（SHA-1 变更后必须重新构建）

> ⚠️ **常见错误**：用 debug keystore 调试但 Firebase 只加了 release 的 SHA-1（或反过来），导致登录直接 CANCELED。
> 确保你当前打包使用的 keystore 的 SHA-1 已注册。

### 4.6 Unity Inspector 配置

在场景中找到挂载 `GoogleSignInHelper` 组件的 GameObject，填写：

| 参数 | 说明 | 值 |
|------|------|-----|
| **Web Client Id** | Firebase 控制台的 Web Client ID | `86394...ffm.apps.googleusercontent.com` |
| Editor Simulate Delay | Editor 模拟延迟（秒） | 1 |
| Editor Simulate Success | Editor 模拟是否成功 | true |

> ⚠️ **Web Client Id 必须填写**，否则 Android/iOS 真机登录都会报错！
> 这个 ID 来自 Firebase 控制台 → Authentication → Google → Web Client ID。

### 4.7 iOS 额外配置

#### GoogleService-Info.plist

确保 `GoogleService-Info.plist` 已放入 `Assets/StreamingAssets/` 目录，iOS 构建时会自动包含到 App Bundle 中。
原生插件通过此文件读取 iOS Client ID（`CLIENT_ID` 字段）。

#### REVERSED_CLIENT_ID URL Scheme

在 iOS Player Settings 中需要添加 URL Scheme，让 Google Sign-In 流程能正确返回你的 App：

1. Unity → Player Settings → iOS → Other Settings → URL Types
2. 添加一个 URL Scheme，Identifier 填 `REVERSED_CLIENT_ID`
3. URL Scheme 填写 `GoogleService-Info.plist` 中的 `REVERSED_CLIENT_ID` 值（格式如 `com.googleusercontent.apps.XXXXX`）

> ⚠️ 如果不配置这个 URL Scheme，Google 登录完成后无法返回 App，流程会卡住。

### 4.8 双平台登录流程说明

```
GoogleSignInHelper.SignIn()
    │
    ├── #if UNITY_EDITOR
    │   └── 模拟登录，返回 mock ID Token
    │
    ├── #elif UNITY_ANDROID（Java 插件方案 v4.2）
    │   ├── TrySilentSignIn()
    │   │   ├── AndroidJavaClass("com.google.android.gms.auth.api.signin.GoogleSignIn")
    │   │   ├── silentSignIn() → addOnSuccessListener → getIdToken() → SUCCESS:<token>
    │   │   └── addOnFailureListener → 需要交互式登录
    │   └── StartInteractiveSignIn()
    │       ├── FariSignInActivity.startSignIn(activity, webClientId, gameObject, callback)
    │       ├── Java 插件内部：startActivityForResult(signInIntent, RC_SIGN_IN)
    │       ├── 用户选择账号 → onActivityResult → getSignedInAccountFromIntent
    │       └── UnitySendMessage("SUCCESS:<idToken>" / "FAILURE:..." / "CANCELED")
    │
    └── #elif UNITY_IOS（ObjC++ 插件方案 v4.3）
        ├── TrySilentSignInIOS()
        │   ├── _fariRestorePreviousSignIn() → P/Invoke
        │   ├── GIDSignIn.sharedInstance.restorePreviousSignInWithCallback
        │   └── 成功 → SUCCESS:<idToken> / 失败 → 交互式
        └── StartInteractiveSignInIOS()
            ├── _fariStartGoogleSignIn() → P/Invoke
            ├── GIDSignIn.sharedInstance.signInWithConfiguration:presentingViewController:callback:
            └── 回调 → UnitySendMessage("SUCCESS:<idToken>" / "FAILURE:..." / "CANCELED")

统一回调 → OnNativeSignInResult(string result)
    │
    ├── SUCCESS:<idToken> → GoogleAuthProvider.GetCredential(idToken, null) → Firebase
    ├── FAILURE:<code>:<msg> → FinishWithError
    └── CANCELED → FinishWithError("用户取消了 Google 登录")
```

### 4.9 Android Java 插件架构

```
Assets/Plugins/Android/FariGoogleSignIn.androidlib/
├── AndroidManifest.xml          ← 注册 FariSignInActivity（透明主题）
├── project.properties           ← android library 标识
├── libs/
│   └── FariGoogleSignIn.jar  ← 编译后的 Java 类
└── java/                        ← 源码参考（不参与编译）
    └── com/fari/googlesignin/
        └── FariSignInActivity.java

FariSignInActivity 职责：
1. 接收 C# 传入的 webClientId
2. 构建 GoogleSignInOptions（requestIdToken + requestEmail）
3. startActivityForResult(signInIntent, RC_SIGN_IN)
4. onActivityResult → GoogleSignIn.getSignedInAccountFromIntent(data)
5. 成功 → UnitySendMessage(gameObjectName, "OnNativeSignInResult", "SUCCESS:<idToken>")
6. 失败 → UnitySendMessage(gameObjectName, "OnNativeSignInResult", "FAILURE:<code>:<msg>")
7. 取消 → UnitySendMessage(gameObjectName, "OnNativeSignInResult", "CANCELED")
```

### 4.10 iOS 原生插件架构

```
Assets/Plugins/iOS/
├── FariGoogleSignIn.mm              ← Objective-C++ 原生插件
└── FariGoogleSignInDependencies.xml ← CocoaPod 依赖声明

FariGoogleSignIn.mm 职责：
1. FariGetIOSClientID() — 从 GoogleService-Info.plist 读取 CLIENT_ID
2. _fariRestorePreviousSignIn() — 静默恢复登录状态
3. _fariStartGoogleSignIn() — 交互式登录
4. _fariGoogleSignOut() — 登出

P/Invoke 接口（C# 侧）：
[DllImport("__Internal")]
private static extern void _fariRestorePreviousSignIn(string gameObjectName, string callbackMethod);
[DllImport("__Internal")]
private static extern void _fariStartGoogleSignIn(string gameObjectName, string callbackMethod, string webClientId);
[DllImport("__Internal")]
private static extern void _fariGoogleSignOut();
```

### 4.11 技术实现原理

**Android 端**（`GoogleSignInHelper.cs` → Java 插件 `FariSignInActivity`）：

```
C# 层 (GoogleSignInHelper.cs)
  │
  ├── 静默登录：AndroidJavaClass("...GoogleSignIn") → silentSignIn()
  │     ├── OnSuccessListenerProxy → getIdToken() → SUCCESS
  │     └── OnFailureListenerProxy → 需要交互式登录
  │
  └── 交互式登录：FariSignInActivity.startSignIn(activity, webClientId, gameObject, callback)
        → Java 插件内部处理 onActivityResult → UnitySendMessage 回调 C#
```

**iOS 端**（`GoogleSignInHelper.cs` → ObjC++ 插件 `FariGoogleSignIn.mm`）：

```
C# 层 (GoogleSignInHelper.cs)
  │
  ├── 静默登录：_fariRestorePreviousSignIn() → P/Invoke
  │     → GIDSignIn.sharedInstance.restorePreviousSignInWithCallback
  │
  └── 交互式登录：_fariStartGoogleSignIn() → P/Invoke
        → GIDSignIn.sharedInstance.signInWithConfiguration:presentingViewController:callback:
        → 回调通过 UnitySendMessage 返回 C#
```

**回调统一格式**（双平台共用 `OnNativeSignInResult`）：

| 回调字符串 | 含义 | 处理 |
|-----------|------|------|
| `SUCCESS:<idToken>` | 登录成功，携带 ID Token | → GoogleAuthProvider.GetCredential → Firebase |
| `FAILURE:<code>:<msg>` | 登录失败 | → FinishWithError |
| `CANCELED` | 用户取消或被拒绝 | → FinishWithError("用户取消了 Google 登录") |

### 4.12 登录后获取用户信息

Google 登录成功后，通过 `GoogleUserInfoHelper` 提取用户信息：

```csharp
// 方式1：从当前 Firebase 登录用户提取
var info = GoogleUserInfoHelper.GetCurrentUser();
Debug.Log(info.ToString());
// 输出: [GoogleUserInfo] UID=xxx, Email=xxx@gmail.com, Name=Zhang San, Photo=https://..., ...

// 方式2：在 FirebaseAuthManager 登录成功回调里用
private void OnFirebaseSignInSuccess(FirebaseUser user)
{
    var info = GoogleUserInfoHelper.ExtractFromFirebaseUser(user);
    Debug.Log($"欢迎, {info.displayName}!");
    StartCoroutine(LoadAvatar(info.photoUrl));
}
```

**FirebaseUser 能直接获取的信息**（无需改原生代码）：

| 字段 | 类型 | 说明 |
|------|------|------|
| `UserId` | string | Firebase UID，唯一标识 |
| `Email` | string | 邮箱 |
| `DisplayName` | string | 全名（如 "Zhang San"） |
| `PhotoUrl` | Uri | 头像图片 URL，用 `UnityWebRequestTexture` 加载 |
| `IsEmailVerified` | bool | 邮箱是否验证 |
| `IsAnonymous` | bool | 是否匿名用户 |
| `Metadata.CreationTimestamp` | long | 账号创建时间（毫秒时间戳） |
| `Metadata.LastSignInTimestamp` | long | 上次登录时间 |

> 💡 `CreationTime` 和 `LastSignInTime` 已封装为 `DateTime` 属性，直接 `info.CreationTime` 即可。

**需要改原生回调才能获取的信息**：

| 字段 | 说明 | 改动量 |
|------|------|--------|
| `GivenName` | 名（first name） | 改 Java + ObjC++ 回调传回 |
| `FamilyName` | 姓（last name） | 改 Java + ObjC++ 回调传回 |
| `ServerAuthCode` | 一次性授权码，用于服务端换 refresh token | 改 Java + ObjC++ 回调传回 |
| Google 原始 Account ID | Google 数字账号 ID | 改 Java + ObjC++ 回调传回 |

> 如果需要这些信息，告诉我要改哪些字段，我帮你改原生回调把数据传回 C#。

### 4.13 常见问题

#### Q: 交互式登录刚启动就返回 CANCELED，用户没看到账号选择窗？

**这是最经典的问题，99% 是 SHA-1 签名未注册到 Firebase。**

现象：日志显示 `静默登录失败，启动交互式登录...` 紧接着 `收到原生插件回调: CANCELED`，中间没有任何用户操作时间。

排查步骤：

```
1. 获取当前打包所用 keystore 的 SHA-1：
   keytool -list -v -keystore <your-keystore-path>

2. 打开 Firebase Console → 项目设置 → Android 应用
3. 检查 SHA 证书指纹 是否包含上面的 SHA-1
4. 如果不一致 → 添加正确的 SHA-1
5. 下载最新的 google-services.json 替换到 Assets/ 根目录
6. 重新打包部署
```

> ⚠️ **常见错误**：
> - 用 debug keystore 调试但 Firebase 只加了 release keystore 的 SHA-1
> - 用自定义 keystore 打包但 Firebase 加的是 debug keystore 的 SHA-1
> - 添加了 SHA-1 但忘了重新下载 `google-services.json`（必须重新下载！）

#### Q: 静默登录总是失败？

**原因**：首次使用 App 的用户不会有缓存的 Google 凭证，静默登录必然失败。这是正常行为，会自动降级到交互式登录。

#### Q: 交互式登录后还是获取不到 ID Token？

**可能原因**：
1. **设备没有 Google Play Services** — 某些国产 ROM 可能不支持
2. **Web Client ID 不匹配** — 确认 Firebase 控制台和 Inspector 中填的 ID 完全一致
3. **SHA 签名不匹配** — 见上一个问题
4. **网络问题** — Google 服务在某些地区可能需要代理

#### Q: 如何获取 keystore 的 SHA-1？

```bash
# 开发签名（debug keystore）
keytool -list -v -alias androiddebugkey -keystore ~/.android/debug.keystore
# 密码: android
# 输出中找到 "SHA1:" 行

# 自定义签名（release keystore）
keytool -list -v -keystore "D:/my_demo/Unity/user.keystore" -storepass <密码>
```

> 💡 FariApp 使用的 keystore：
> - 路径：`D:/my_demo/Unity/user.keystore`
> - 密码：`12345678`
> - SHA-1：`A2:B8:20:BB:FE:15:81:DA:A4:09:B8:E7:34:24:00:EA:39:F2:BC:81`

#### Q: iOS 登录提示 "The operation couldn't be completed"？

**可能原因**：
1. `GoogleService-Info.plist` 未正确放入 `Assets/StreamingAssets/`
2. iOS Player Settings 中未配置 `REVERSED_CLIENT_ID` URL Scheme
3. CocoaPod `GoogleSignIn` 未正确安装（检查 Xcode 项目的 Podfile）

#### Q: 登录弹窗一闪而过？

**原因**：Web Client ID 配置错误或 SHA 签名不匹配，Google 服务立即拒绝。检查 Firebase 控制台的 OAuth 配置。

---

## 五、Apple 登录配置

### 5.1 Apple Developer Console 配置

1. 登录 [Apple Developer Portal](https://developer.apple.com/account/)
2. 进入 **Certificates, Identifiers & Profiles**

#### 创建 App ID（如果还没有）
1. **Identifiers** → 点击 **+** → 选 **App IDs**
2. 选择 **App**，填写 Description 和 **Bundle ID**（必须与 Unity 一致）
3. 勾选 **Sign In With Apple** 能力
4. 注册

#### 创建 Services ID（用于 Web/OAuth 流程）
1. **Identifiers** → 点击 **+** → 选 **Services IDs**
2. Description 填写（如 `FariApp Apple Sign-In`）
3. Identifier 填写反向域名格式（如 `com.yourcompany.fari.signin`）
4. 勾选 **Sign In With Apple**
5. 配置 **Primary App ID** 为上面创建的 App ID
6. **Domains and Subdomains**：填写你的域名（可选）
7. **Return URLs**：留空即可（Firebase 会处理回调 URL）
8. 注册后记录此 Services ID

### 5.2 Firebase 控制台启用 Apple 登录

1. **Authentication** → **Sign-in method** → **Apple** → **启用**
2. **Services ID**：填写上一步创建的 Services ID（Identifier 部分）
3. **Key ID** 和 **Team ID**：后续生成私钥后填入（见下一步）
4. **保存**

### 5.3 生成 Apple 私钥

1. 在 Apple Developer Portal → **Certificates, Identifiers & Profiles** → **Keys**
2. 点击 **+** → Key Name 随意填写
3. 勾选 **Sign In With Apple**
4. 注册并 **下载 `.p8` 文件**（仅下载一次！妥善保存）
5. 记录 **Key ID**（在 Keys 列表中显示）
6. 回到 Firebase 控制台 Apple 设置页，填入 **Key ID** 和 **Team ID**

### 5.4 Xcode / Unity 构建设置

在 Unity 中：
1. **Player Settings** → **iOS** → **Other Settings** → **Signing**
   - 确保 Team 已选择正确的 Developer Account
2. **Player Settings** → **iOS** → **Other Settings** → **Configuration**
   - **Capability** 中勾选 **Sign In With Apple**
3. 如果使用插件方式，安装 [apple-signin-unity](https://github.com/lupidan/apple-signin-unity)：
   ```bash
   # 通过 Package Manager 安装（推荐）
   # 或直接下载 .unitypackage 导入
   ```

### 5.5 编辑器模拟说明

代码中已内置编辑器模拟模式：非 iOS 平台会自动使用模拟 Token 进行开发测试。**真机测试必须在 iOS 设备上进行。**

---

## 六、Facebook 登录配置

### 6.1 Facebook Developer Console 创建应用

1. 打开 [Facebook 开发者平台](https://developers.facebook.com/)
2. **My Apps** → **Create App** → 类型选 **Consumer**（或 **Games** 如果是游戏类）
3. 填写 Display Name（如 `FariApp`）、联系邮箱
4. 创建后在 Dashboard 获取 **App ID**（后续需要）

### 6.2 Facebook 产品设置

在应用的左侧菜单中：

1. **添加产品** → **Facebook Login** → **设置**
   - **有效的 OAuth 重定向 URI**：将 Firebase 提供的 URI 填入（见下方 6.3 步骤）
2. （可选）**添加产品** → **Graph API Explorer** — 用于测试 API 调用

### 6.3 Firebase 控制台启用 Facebook 登录

1. **Authentication** → **Sign-in method** → **Facebook** → **启用**
2. **App ID**：填入 Facebook Developer Console 的 App ID
3. **App Secret**：填入 App Secret（Settings → Basic → App Secret）
4. 启用后，Firebase 会提供一个 **OAuth Redirect URI**，复制它

### 6.4 将 Firebase Redirect URI 添加到 Facebook

回到 Facebook Developer Console：

1. **产品** → **Facebook Login** → **设置**
2. 在 **有效的 OAuth 重定向 URI** 列表中添加 Firebase 提供的 URI
3. 保存更改

### 6.5 安装 Facebook SDK for Unity

1. 从 Facebook 官网下载最新 SDK：
   > https://developers.facebook.com/docs/unity/downloads/
2. 解压后得到 `FacebookSDK.unitypackage`
3. Unity 中 **Assets** → **Import Package** → **Custom Package** → 导入
4. 导入时建议只选择以下模块：
   - ✅ Facebook SDK（核心）
   - ✅ Login（登录模块）
   - ✅ Share（如果需要分享功能）
   - ❌ 其他模块按需选择

### 6.6 Unity 中配置 Facebook

导入 SDK 后：

1. 菜单栏出现 **Facebook** → **Edit Settings**
2. 填入 **App ID**
3. （可选）勾选 **Logging** 以便开发调试

### 6.7 Android 额外配置

Facebook SDK 需要 `AndroidManifest.xml` 中的额外配置。导入 SDK 时通常会自动生成 `Assets/Plugins/Android/FacebookSDK.androidlib`。确认其中包含：

```xml
<activity
    android:name="com.facebook.FacebookActivity"
    android:configChanges="keyboard|keyboardHidden|screenLayout|screenSize|orientation"
    android:theme="@style/com_facebook_activity_theme" />
<meta-data
    android:name="com.facebook.sdk.ApplicationId"
    android:value="@string/facebook_app_id" />
```

以及 `res/values/strings.xml` 中的 facebook_app_id 字符串资源。

### 6.8 iOS 额外配置

1. Unity 导出 iOS 项目时，`FariIOSContactsPostprocessor` 会自动添加 Game Center、Sign in with Apple、In-App Purchase capability。
2. 导出后运行 `./scripts/check-ios-export.sh Builds/iOS`，确认 entitlements 和 Xcode capability 已写入。
3. Facebook 仍主要依赖 `Info.plist` URL Scheme / Queries Schemes，而不是 Xcode capability；确认 `Info.plist` 中包含：
   - `CFBundleURLSchemes` → 添加 `fb{YOUR_APP_ID}`
   - `FacebookAppID` → 你的 App ID
   - `FacebookDisplayName` → 应用名称
   - `LSApplicationQueriesSchemes` → 添加 `fbapi`, `fb-messenger-share-api` 等

---

## 七、Unity 项目侧配置

### 7.1 Firebase SDK 安装

```
# 方式1：通过 Unity Package Manager (.tgz)
# 下载 Firebase Unity SDK 解压后的 .tgz 文件

# 方式2：通过 .unitypackage
# 从 https://firebase.google.com/docs/unity/setup 下载
```

安装时至少包含以下模块：
- ✅ **Firebase Auth**（必需）
- ✅ **Firebase Core / App**（必需）

### 7.2 场景中的 Manager 对象

确保启动场景中有以下 Singleton GameObject（或由框架自动创建）：

| GameObject | 组件 | 说明 |
|------------|------|------|
| FirebaseAuthManager | `FirebaseAuthManager` | Firebase 认证核心管理器 |
| GoogleSignInHelper | `GoogleSignInHelper` | Google 登录辅助（Android Java 插件 + iOS 原生插件） |
| AppleSignInHelper | `AppleSignInHelper` | Apple 登录辅助 |
| FacebookSignInHelper | `FacebookSignInHelper` | Facebook 登录辅助 |

建议统一放在一个名为 `Managers` 的空 GameObject 下，或使用 DontDestroyOnLoad。

### 7.3 关键配置项汇总

| 组件 | 关键配置字段 | 获取来源 |
|------|-------------|----------|
| GoogleSignInHelper | `WebClientId` | Firebase 控制台 → Authentication → Google → Web Client ID |
| FacebookSignInHelper | `FacebookAppId` | Facebook Developer Console → App Dashboard |
| FirebaseAuthManager | 无需额外配置 | 自动读取 Firebase 配置文件 |

### 7.4 平台构建检查清单

#### Android 构建前确认
- [ ] `Assets/` 目录下存在 `google-services.json`
- [ ] EDM 已正确解析 Google Play Services / Facebook SDK 依赖
- [ ] Build Settings → Min API Level ≥ 21（推荐 ≥ 23）
- [ ] Custom Gradle Template 未被错误修改

#### iOS 构建前确认
- [ ] `Assets/StreamingAssets/` 目录下存在 `GoogleService-Info.plist`
- [ ] Player Settings → Signing 正确配置了 Developer Team
- [ ] 运行 `./scripts/check-ios-export.sh Builds/iOS`，确认 Capability 包含 Game Center / Sign In With Apple / In-App Purchase
- [ ] Info.plist 包含 Facebook 所需的 URL Scheme 和 Queries Schemes
- [ ] Pod install 成功（Export Xcode Project 后执行 `cd iOS && pod install`）

---

## 八、配置完成后的下一步：挂载组件与测试

> 配完 Inspector 参数后，按以下顺序操作。

### 8.1 确认所有配置文件到位

在 Unity Project 窗口中检查文件结构：

```
Assets/
├── google-services.json              ✅ Android 配置（放根目录）
├── StreamingAssets/
│   └── GoogleService-Info.plist      ✅ iOS 配置（放这里）
├── Firebase/                          ✅ SDK 已导入
├── Plugins/
│   ├── Android/
│   │   └── FariGoogleSignIn.androidlib/  ✅ Android Java 插件
│   └── iOS/
│       ├── FariGoogleSignIn.mm              ✅ iOS 原生插件
│       └── FariGoogleSignInDependencies.xml  ✅ CocoaPod 依赖声明
└── Scripts/
    └── GameManager/
        ├── FirebaseAuthManager.cs    ✅
        ├── GoogleSignInHelper.cs     ✅（Android Java 插件 + iOS ObjC++ 插件）
        ├── GoogleUserInfoHelper.cs  ✅（登录后获取用户信息）
        └── ...
```

> ⚠️ 缺少任何一个配置文件都会导致 Firebase 初始化失败。

### 8.2 在场景中挂载组件

确保**启动/管理器场景**（通常是 `Main`、`Boot` 或包含 `DontDestroyOnLoad` 对象的场景）中有一个 GameObject 挂载了以下脚本：

```
📦 GameObject: AppManager（或任何常驻对象，使用 DontDestroyOnLoad）
│
├── 📄 FirebaseAuthManager      ← 自动初始化 Firebase（Awake 中调用 CheckAndFixDependenciesAsync）
├── 📄 GoogleSignInHelper       ← Web Client ID 已填写（见 §4.5）
├── 📄 UserDataManager          ← 用户数据管理（已有）
└── 📄 NotificationSettingsManager ← 设置偏好（已有）
```

**关键说明**：
- `FirebaseAuthManager` 的 `Awake()` 会自动调用 `FirebaseApp.CheckAndFixDependenciesAsync()`
- **只要场景中有这个组件，进入游戏就会自动初始化 Firebase**
- 无需手动调用任何初始化代码

### 8.3 确认 LoginUI 可正常打开

确保 **LoginUI Prefab / Scene** 存在且完整：

| 检查项 | 说明 |
|--------|------|
| Prefab 挂载 `LoginUI` 脚本 | 继承 WindowBase |
| Prefab 挂载 `LoginUIComponent` 组件 | 自动生成组件引用 |
| 按钮引用已拖好 | GooglePlaySignInButton、AppleSignInButton、FaceBookSignInButton 等 |
| LoginUI 在 UIModule 中注册 | 能通过 PopUpWindow 正常打开 |

### 8.4 真机/模拟器测试流程

#### Android 测试

```
1. Unity → File → Build Settings → Platform 切换到 Android
2. Connect（连接真机）或在模拟器中运行
3. 点击 Build And Run
4. 进入 LoginUI 界面
5. 点击 Google 登录按钮

预期行为：
  ├── 弹出 Google 系统级账号选择窗口（非 App 内弹窗）
  ├── 选择已有账号 → 确认授权
  ├── 窗口关闭 → 回到游戏
  ├── Toast 显示 "Google 登录成功"
  └── 自动跳转到 NavigationUI 主界面
```

#### iOS 测试

```
1. Build Settings → 切换到 iOS → Build
2. Xcode 打开生成的 .xcworkspace 文件（注意是 .xcworkspace 不是 .xcodeproj）
3. Signing & Capabilities → 选择正确的 Developer Team
4. 运行到真机（iPhone / iPad）

⚠️ 重要：iOS 模拟器不支持以下功能：
  - Apple Sign-In（需要系统级 Touch ID / Face ID / 密码验证）
  - Google Sign-In（需要原生 OAuth 流程）
  
必须在真机上测试！
```

### 8.5 各登录方式预期结果对照表

| 按钮 | 预期行为 | 特殊说明 |
|------|----------|----------|
| **Google** | 弹出系统 Google 账号选择窗 → 选号授权 → 成功 | Android/iOS 均需真机 |
| **Apple** | 弹出系统 Apple ID 窗 → 生物识别/密码 → 成功 | **仅 iOS/macOS 真机**；编辑器下走模拟 Token |
| **Facebook** | 弹出 Facebook 登录 / 切换到 Facebook App → 授权 → 成功 | 需要先安装 Facebook App 或有浏览器登录态 |
| **匿名登录** | 无弹窗，静默创建匿名用户 → 直接成功 | 不依赖任何第三方 |
| **跳过登录** | 不经过 Firebase，直接进入 NavigationUI | 以游客身份使用 |

### 8.6 问题排查速查

#### 场景一：点了按钮没反应

```
排查顺序：
1. 查看 Console 有没有红色报错
2. 检查 FirebaseAuthManager.IsFirebaseInitialized 是否为 true
3. 是否显示了 Toast "Firebase 初始化中" → 说明 Firebase 还没就绪
4. 是否显示了 Toast "正在登录中" → 说明正在等待回调
5. 检查按钮 onClick 事件是否正确绑定了方法
```

#### 场景二：弹出选择窗但选完报错

```
排查顺序：
1. Inspector 上 Web Client ID 是否填对（和 Firebase 控制台完全一致）
2. google-services.json 是否在 Assets 根目录（Android）
3. GoogleService-Info.plist 是否在 StreamingAssets/（iOS）
4. 查看 Console 具体错误信息：
   - "DEVELOPER_ERROR" → Web Client ID 错误
   - "NetworkError" → 网络问题 / 代理设置
   - "ApiException 10/16" → SHA 签名不匹配（Android release 包常见）
```

#### 场景三：Android 打包/Gradle 报错

```
解决步骤：
1. 菜单栏 → Assets → External Dependency Manager → Version Handler → Force Resolve
2. 如果 Gradle 同步失败：
   - 检查 JDK 版本（建议 JDK 17）
   - 检查 Android Gradle Plugin 版本兼容性
   - 清理缓存：删除 Library 文件夹后重新打开项目
3. 检查 Custom Gradle Template 是否被错误修改（恢复默认）
```

### 8.7 当前进度总览

完成以上所有步骤后，你的项目应该处于这个状态：

```
✅ Firebase 控制台：项目已创建
✅ Authentication：Google / Apple / Facebook / 匿名 已启用
✅ google-services.json 已下载并放入 Assets/
✅ GoogleService-Info.plist 已下载并放入 StreamingAssets/
✅ Firebase Unity SDK 已导入
✅ 旧版 Google Sign-In 插件已删除（Assets/GoogleSignIn/）
✅ Google Sign-In (play-services-auth) 依赖已添加到 mainTemplate.gradle
✅ GoogleSignInHelper.WebClientId 已填写（Firebase Web Client ID）
✅ 代码已完成（FirebaseAuthManager + GoogleSignInHelper + LoginUI 集成 + UserDataManager 扩展）
✅ 场景中 Manager 对象已挂载
📍 下一步：Build & Run 到真机测试
```

---

## 九、代码架构说明

### 9.1 登录流程图

```
用户点击登录按钮
       │
       ▼
  ┌─ LoginUI.cs ─────────────────────┐
  │ OnXxxSignInButtonClick()         │
  │  ├── CheckFirebaseReady()        │ ← 检查初始化状态
  │  ├── SetButtonsInteractable()    │ ← 禁用按钮防重复点击
  │  └── FirebaseAuthManager.Xxx()   │ ← 发起第三方登录
  └──────────┬───────────────────────┘
             │
             ▼
  ┌─ FirebaseAuthManager.cs ──────────────┐
  │ SignInWithProvider()                   │
  │  ├── Helper.Instance.SignIn()         │ ← 调用对应 Helper
  │  │     ↓ 获取 Token                    │
  │  ├── GetCredential(token)             │ ← 构建 Credential
  │  └── SignInWithCredential(cred)       │ ← 统一认证入口
  │        ↓                              │
  │  SyncUserToDataManager()              │ ← 同步到本地
  │  OnLoginSuccess?.Invoke()             │ ← 触发成功事件
  └──────────┬───────────────────────────┘
             │
             ▼
  ┌─ LoginUI.OnFirebaseLoginSuccess() ────┐
  │  ├── Toast("登录成功")                  │
  │  ├── SetButtonsInteractable(true)      │
  │  ├── HideWindow<LoginUI>()            │
  │  └── PopUpWindow<NavigationUI>()      │ ← 进入主界面
  └───────────────────────────────────────┘
```

### 9.2 核心类职责

| 类名 | 职责 | 设计模式 |
|------|------|----------|
| `FirebaseAuthManager` | Firebase 初始化、Credential 登录、账号关联、登出 | MonoSingleton |
| `GoogleSignInHelper` | Android: Java 插件封装; iOS: P/Invoke ObjC++ 封装，返回 ID Token | MonoSingleton + 平台条件编译 |
| `AppleSignInHelper` | Apple Sign-In 封装，返回 ID Token + Auth Code | MonoSingleton + 条件编译 |
| `FacebookSignInHelper` | Facebook SDK 封装，返回 Access Token | MonoSingleton + 反射调用 |
| `GoogleUserInfoHelper` | 从 FirebaseUser 提取 Google 账号信息 | 静态工具类 |
| `UserDataManager` | 用户数据内存管理 + PlayerPrefs 持久化 | MonoSingleton |
| `LoginUI` | 登录界面交互层，事件绑定与 UI 反馈 | WindowBase |

### 9.3 数据流向

```
第三方 SDK (Google/Apple/Facebook)
        ↓ 返回 Token/AuthCode
FirebaseAuthManager.SignInWithCredential()
        ↓ Firebase Auth 验证
FirebaseUser 对象
        ↓
SyncUserToDataManager() → UserDataManager.SyncFromFirebaseUser()
        ↓
PlayerPrefs 持久化 (Email, UserId, PhotoUrl, FirebaseUid, ...)
```

### 9.4 事件机制

`FirebaseAuthManager` 通过事件解耦 UI 层和业务层：

```csharp
// LoginUI 中注册事件
FirebaseAuthManager.Instance.OnLoginSuccess += OnFirebaseLoginSuccess;
FirebaseAuthManager.Instance.OnLoginFailed += OnFirebaseLoginFailed;

// 其他地方也可以监听（无需引用 LoginUI）
FirebaseAuthManager.Instance.OnUserInfoUpdated += RefreshAvatar;
FirebaseAuthManager.Instance.OnLogout += ShowLoginScreen;
```

---

## 十、常见问题排查

### Q1: "Firebase 初始化失败" / DependencyStatus.UnavailableDisabled

**原因**：Firebase SDK 依赖未正确解析  
**解决**：
- 确认 `google-services.json`（Android）或 `GoogleService-Info.plist`（iOS）已放入正确位置
- Android：运行 **Assets → Play Services Resolver → Force Resolve** 
- 重新打开 Unity Editor 让 EDM 重新解析

### Q2: Google 登录报错 "静默登录失败"

**原因**：首次使用 App 的用户不会有缓存的 Google 凭证，静默登录必然失败  
**这是正常行为**：代码会自动降级到交互式登录（弹出 Google 账号选择窗口）  
**排查**：如果交互式登录后仍然失败，检查：
- WebClientId 是否正确填写（Inspector 中）
- Firebase 控制台 → Android 应用的 SHA 签名是否匹配
- 设备是否安装了 Google Play Services

### Q3: Google 登录报错 "WebClientId 未配置"

**原因**：GoogleSignInHelper 组件的 WebClientId 字段为空  
**解决**：在 Inspector 中填写 Firebase 控制台 → Authentication → Google → Web Client ID

### Q4: Apple 登录编辑器中无法工作

**正常现象**：Apple Sign-In 仅支持 iOS/macOS 真机设备  
**解决**：编辑器模式下代码会自动走模拟逻辑，真机测试请在 iPhone/iPad 上进行

### Q5: Facebook 登录 "无效的 OAuth Redirect URI"

**原因**：Facebook 应用设置的 Redirect URI 与 Firebase 不匹配  
**解决**：
1. Firebase 控制台 → Auth → Facebook → 复制完整的 Redirect URI
2. Facebook Developer Console → 你的应用 → Facebook Login → Settings → 粘贴该 URI

### Q6: Android 构建报错 "Duplicate class" / 依赖冲突

**原因**：多个 SDK 引入了相同库的不同版本  
**解决**：
- 使用 EDM 的 **Version Handler** 工具检查冲突
- 在 `Assets/Firebase/Editor/` 下的 `FirebaseDependencyVersion.xml` 中强制指定版本
- 清理 Library 缓存后重新 Resolve

### Q7: iOS 构建报错 "No matching provisioning profile found"

**原因**：Apple Developer 账号的 App ID 与 Bundle Identifier 不匹配  
**解决**：
1. 确认 Apple Developer Portal 中的 App ID Bundle Identifier 与 Unity Player Settings 完全一致
2. 在 Apple Developer Portal 为该 App ID 创建 Development/Distribution Provisioning Profile
3. Xcode Signing & Capabilities 中选择正确的 Team 和 Profile

### Q8: 登录成功但用户数据没同步

**原因**：UserDataManager.SyncFromFirebaseUser 中的数据可能为空  
**解决**：在 Firebase Console → Authentication → Users 中查看用户信息，确认 Email/Display Name/Photo URL 是否有值（取决于用户在第三方平台的隐私设置）

### Q9: Google 登录报错 "DEVELOPER_ERROR" 或 10

**原因**：SHA 签名不匹配或 Web Client ID 配置错误  
**解决**：
1. 确认 Firebase 控制台 → Android 应用中添加了正确的 SHA-1 签名
2. 确认 Inspector 中 WebClientId 与 Firebase 控制台完全一致
3. 重新下载 google-services.json 替换到 Assets/ 根目录

### Q10: Android 打包报错 "Duplicate class" / 依赖冲突

**错误信息**：
```
error CS0029: Cannot implicitly convert type 
'System.Threading.Tasks.Task<Firebase.Auth.FirebaseUser>' 
to 'Firebase.Auth.FirebaseUser'
```

### Q11: 登录成功但用户数据没同步

**问题**：Google / Apple 登录依赖手机系统服务，Editor 不能运行，难道每次都要 Build？

**答案：不需要。已内置 Editor 模拟模式。**

整个登录链路现在支持 **三层模拟**：

```
点击 Google 按钮
    │
    ▼
GoogleSignInHelper.SignIn()
    │
    ├── 真机（Android/iOS）→ 调用真实 SDK ✅
    └── Editor → 自动走模拟流程，1秒后返回假 Token ✅
            │
            ▼
FirebaseAuthManager.SignInWithCredential()
    │
    ├── 收到真实 Token → 调用 Firebase API ✅
    └── 收到模拟 Token（含 "mock" 关键字）→ 自动跳过 Firebase，构造假用户数据 ✅
            │
            ▼
UserDataManager.SyncFromFirebaseUser() ← 接受 UserInfo 对象，不区分真假
            │
            ▼
LoginUI.OnFirebaseLoginSuccess()  ← 完全一样的 UI 跳转和 Toast 提示
```

**各 Helper 的 Editor 行为**：

| Helper | Editor 下的行为 | 延迟 |
|--------|----------------|------|
| `GoogleSignInHelper` | 模拟成功，返回含 `mock` 的假 ID Token | 1 秒 |
| `AppleSignInHelper` | 已有模拟模式，返回假 Token | 1 秒 |
| `FacebookSignInHelper` | 模拟成功，返回假 Access Token | 1 秒 |
| `FirebaseAuthManager` | 检测到 mock Token → 构造假用户数据 → 触发 OnLoginSuccess | 即时 |
| 匿名登录 | Editor 下直接跳过 Firebase API 调用，模拟成功 | 即时 |

**模拟数据示例**：
```csharp
// Editor 模拟登录后 UserDataManager 中的数据：
DisplayName = "Google 测试用户"   // 根据 provider 变化
Email = "test@google.com"
FirebaseUid = "mock_editor_user_1234567890"
AuthProviderId = "google.com"
IsFirebaseAuthenticated = true     // 标记为已认证
CurrentLoginType = LoginType.Google
```

**使用建议**：

| 开发场景 | 在哪测 |
|----------|--------|
| UI 交互、按钮绑定、Toast 显示、界面跳转 | ✅ Editor 直接测 |
| 用户数据同步逻辑、PlayerPrefs 存储 | ✅ Editor 直接测 |
| 防重复点击、事件注册注销 | ✅ Editor 直接测 |
| **真实的第三方 OAuth 弹窗** | ❌ 必须真机 |
| **Firebase Token 验证** | ❌ 必须真机（或 Firebase 初始化正常的 Editor） |
| **实际账号信息获取** | ❌ 必须真机 |

> 💡 **90% 的业务逻辑可以在 Editor 里验证**，只有涉及真实第三方 SDK 调用的部分才需要真机。

---

## 附录 A：快速配置检查清单

### Firebase 控制台
- [ ] 项目已创建
- [ ] iOS 应用已注册（已下载 GoogleService-Info.plist 到 StreamingAssets/）
- [ ] Android 应用已注册（已下载 google-services.json 到 Assets/）
- [ ] **匿名** 登录已启用
- [ ] **Google** 登录已启用（已复制 Web Client ID）
- [ ] **Apple** 登录已启用（已配置 Services ID / Key ID / Team ID）
- [ ] **Facebook** 登录已启用（已配置 App ID / App Secret / Redirect URI）

### 第三方控制台
- [ ] Apple Developer：App ID 已启用 Sign In With Apple，私钥(.p8)已下载
- [ ] Facebook Developer：App 已创建，Redirect URI 已双向配置

### Unity 项目
- [ ] Firebase Auth SDK 已安装
- [ ] 旧版 Google Sign-In 插件已删除（`Assets/GoogleSignIn/`）
- [ ] `Assets/Plugins/Android/FariGoogleSignIn.androidlib/` 已就位（Android Java 插件）
- [ ] `Assets/Plugins/iOS/FariGoogleSignIn.mm` 已就位（iOS 原生插件）
- [ ] `Assets/Plugins/iOS/FariGoogleSignInDependencies.xml` 已就位（iOS CocoaPod 声明）
- [ ] `play-services-auth:21.2.0` 依赖已添加到 `mainTemplate.gradle`
- [ ] `Assets/Plugins/Android/mainTemplate.gradle` 中无旧版 `google-signin-support` 依赖
- [ ] External Dependency Manager → Force Resolve（Android）已执行
- [ ] CocoaPods 依赖已通过 iOS Resolver 解析（或构建后 `pod install` 成功）
- [ ] google-services.json 在 Assets/ 根目录下
- [ ] GoogleService-Info.plist 在 Assets/StreamingAssets/ 下
- [ ] **SHA-1 签名已添加到 Firebase 控制台**（Android 必须）
- [ ] **iOS Player Settings → URL Types 已添加 REVERSED_CLIENT_ID**（iOS 必须）
- [ ] GoogleSignInHelper.WebClientId 已填写（Firebase 控制台 → Authentication → Google → Web Client ID）
- [ ] FacebookSignInHelper.FacebookAppId 已填写（如需要）

### 场景与组件挂载
- [ ] FirebaseAuthManager 已挂载到常驻 GameObject（自动初始化 Firebase）
- [ ] GoogleSignInHelper 已挂载（WebClientId 必须在 Inspector 中填写）
- [ ] LoginUI Prefab 按钮引用已绑定（Google/Apple/Facebook/匿名登录按钮）
- [ ] LoginUI 可通过 UIModule 正常打开

### 测试验证
- [ ] Android 真机/模拟器：点击 Google 登录 → 弹出系统账号选择窗 → 授权成功 → 跳转主界面
- [ ] iOS 真机：点击 Apple/Google 登录 → 系统弹窗 → 授权成功 → 跳转主界面
- [ ] 匿名登录：静默成功，无弹窗
- [ ] 跳过登录：直接进入主界面（不经过 Firebase）
- [ ] Toast 提示正常显示（登录成功/失败/初始化中）
