# MoonlyApp Firebase 社交登录 配置指南

> 版本：4.0 | 最后更新：2026-06-13
>
> ⚠️ **v4.0 重大变更**：Google 登录已从 Play Games SDK 迁移到 **Android 原生 Google Sign-In API**（通过 AndroidJavaClass 调用）。
> 原因：Play Games SDK 强制应用类型为"游戏"，不适合非游戏类 App（如 MoonlyApp）。
> 新方案不需要 Play Console PGS 配置、不需要 Resources Definition，App 类型正常选"应用"即可。

---

## 目录

- [一、前置条件](#一前置条件)
- [二、核心概念：Firebase 与第三方 SDK 的分工](#二核心概念firebase-与第三方-sdk-的分工)
- [三、Firebase 项目基础配置](#三firebase-项目基础配置)
- [四、Google 登录配置](#四google-登录配置)
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
│ moonly-app   │  ◄── 同一个项目 ──►│ moonly-app       │
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
2. 点击「**添加项目**」→ 输入项目名（如 `moonly-app`）
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
    │   ├── Android 包名：com.yourcompany.moonly
    │   │   （与 Unity Player Settings 的 Package Name 完全一致）
    │   ├── 应用昵称：随意（如 MoonlyApp）
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
    │   ├── iOS Bundle ID：com.yourcompany.moonly
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
| **Bundle ID 必须一致** | Android 包名和 iOS Bundle ID 通常用同一个值（如 `com.yourcompany.moonly`） |
| **同一个 Firebase 项目** | 不要分别建两个项目！iOS 和 Android 在同一个项目里各注册一个 App |
| **改了包名要重新下载** | 在 Unity 里改了 Package Name / Bundle ID 后，必须回 Firebase 重新生成并替换这两个文件 |
| **SHA 签名** | 开发阶段可先不填；正式上线前必须加 release 签名的 SHA |

> 💡 如果还没确定最终包名，可以先填一个临时的（如 `com.test.moonly`），等定下来了再去 Firebase 更新并重新下载配置文件替换。

### 3.6 启用匿名登录

> 匿名登录是跳过登录功能的基础。

1. 进入 **Authentication** → **Sign-in method**
2. 点击 **匿名** → 启用 → **保存**

---

## 四、Google 登录配置（原生 Google Sign-In API）

> ⚠️ **v4.0 重大变更**：项目已从 Play Games SDK 迁移到 **Android 原生 Google Sign-In API**。
>
> **为什么要迁移？**
> - Play Games SDK 要求在 Play Console 中将应用类型设为"游戏"，导致 App 在 Play Store 上出现在游戏分类下
> - MoonlyApp 是塔罗/占卜类 App，应出现在"生活"或"健康"分类，不适合游戏分类
> - Play Games 登录弹窗显示"Play 游戏"风格 UI，与普通 App 体验不符
>
> **新方案优势**：
> - ✅ App 类型正常选"应用"，不需要 Play Console PGS 配置
> - ✅ 不需要 Resources Definition XML
> - ✅ 标准 Google 登录弹窗 UI
> - ✅ 通过 `AndroidJavaClass` 直接调用原生 API，不需要额外 Unity 插件
> - ✅ 支持 arm64 架构

### 4.1 旧插件清理

在继续之前，**必须先删除旧版插件文件**：

```
在 Unity Project 窗口中，右键删除以下目录（如果存在）：
❌ Assets/GoogleSignIn/                  ← 旧版 google-signin-unity 插件（整个删除）
❌ Assets/GeneratedLocalRepo/GoogleSignIn/  ← 旧插件缓存（整个删除）
❌ Assets/GooglePlayGames/               ← Play Games SDK 插件（整个删除，如果已安装）
```

> ⚠️ 必须在 Unity Editor 中删除（不是直接在文件管理器删），让 Unity 正确处理 .meta 文件。

删除后检查 `Assets/Plugins/Android/mainTemplate.gradle`，确认以下旧依赖行已移除（如果存在）：
```gradle
// 删除这行（如果存在）：
implementation 'com.google.signin:google-signin-support:1.0.4'
```

然后运行：**菜单栏 Assets → External Dependency Manager → Android Resolver → Force Resolve**

### 4.2 添加 play-services-auth 依赖

在 `Assets/Plugins/Android/mainTemplate.gradle` 的 dependencies 中确认包含：

```gradle
implementation 'com.google.android.gms:play-services-auth:21.2.0' // Google Sign-In 原生 API
```

> 💡 此依赖提供 `GoogleSignIn`、`GoogleSignInOptions`、`GoogleSignInClient` 等 Java 类，
> 供 `GoogleSignInHelper.cs` 通过 `AndroidJavaClass` 调用。

然后运行：**菜单栏 Assets → External Dependency Manager → Android Resolver → Force Resolve**

### 4.3 Firebase 控制台启用 Google 登录

1. 打开 [Firebase 控制台](https://console.firebase.google.com/)
2. **Authentication** → **Sign-in method** → 点击 **Google**
3. 选择 **启用**
4. 填写 **项目支持邮箱**
5. **⭐ 复制 Web Client ID**（格式类似 `86394...ffm.apps.googleusercontent.com`）
6. 点击 **保存**

> ⚠️ **Web Client ID 是关键配置**，必须保存好，后面 Unity Inspector 中要填！

### 4.4 Unity Inspector 配置

在场景中找到挂载 `GoogleSignInHelper` 组件的 GameObject，填写：

| 参数 | 说明 | 值 |
|------|------|-----|
| **Web Client Id** | Firebase 控制台的 Web Client ID | `86394...ffm.apps.googleusercontent.com` |
| Editor Simulate Delay | Editor 模拟延迟（秒） | 1 |
| Editor Simulate Success | Editor 模拟是否成功 | true |

> ⚠️ **Web Client Id 必须填写**，否则 Android 真机登录会报错！
> 这个 ID 来自 Firebase 控制台 → Authentication → Google → Web Client ID。

### 4.5 原生 Google Sign-In 登录流程说明

```
当前流程（v4.0 原生 API）：
  GoogleSignInHelper → AndroidJavaClass → GoogleSignInClient
    ├── 先尝试 silentSignIn()（静默登录，已授权用户无弹窗）
    │   └── 成功 → 获取 ID Token
    └── 静默失败 → 启动 Google 系统登录 Activity（标准 Google 登录弹窗）
        └── 用户选择账号后返回 → 再次 silentSignIn() → 获取 ID Token

  ID Token → GoogleAuthProvider.GetCredential(idToken, null) → Firebase

对比旧流程：
  ❌ 旧版 google-signin-unity：反射调用 → ID Token（插件已废弃，无 arm64）
  ❌ Play Games SDK：Server Auth Code → PlayGamesAuthProvider（强制游戏分类）
  ✅ 当前方案：ID Token → GoogleAuthProvider（无插件依赖，App 类型正常）
```

### 4.6 技术实现原理

`GoogleSignInHelper.cs` 通过 Unity 的 `AndroidJavaClass` / `AndroidJavaObject` 机制，直接调用 Android 原生 Java API：

```
C# 层 (GoogleSignInHelper.cs)
  │
  ├── AndroidJavaClass("com.google.android.gms.auth.api.signin.GoogleSignIn")
  ├── AndroidJavaObject("com.google.android.gms.auth.api.signin.GoogleSignInOptions$Builder")
  │     .Call("requestIdToken", webClientId)
  │     .Call("requestEmail")
  │     .Call("build")
  │
  ├── GoogleSignIn.getClient(activity, options) → GoogleSignInClient
  │
  ├── client.Call("silentSignIn") → Task<GoogleSignInAccount>
  │     ├── addOnSuccessListener → 获取 account.getIdToken()
  │     └── addOnFailureListener → 需要交互式登录
  │
  └── client.Call("getSignInIntent") → startActivity(intent)
        → 用户操作后返回 → OnApplicationPause → 再次 silentSignIn()

回调使用 AndroidJavaProxy：
  ├── OnSuccessListenerProxy : AndroidJavaProxy("...OnSuccessListener")
  └── OnFailureListenerProxy : AndroidJavaProxy("...OnFailureListener")
```

### 4.7 常见问题

#### Q: 静默登录总是失败？

**原因**：首次使用 App 的用户不会有缓存的 Google 凭证，静默登录必然失败。这是正常行为，会自动降级到交互式登录。

#### Q: 交互式登录后 silentSignIn 还是失败？

**可能原因**：
1. **设备没有 Google Play Services** — 某些国产 ROM 可能不支持
2. **Web Client ID 不匹配** — 确认 Firebase 控制台和 Inspector 中填的 ID 完全一致
3. **SHA 签名不匹配** — Firebase 控制台中注册的 Android 应用需要包含当前签名的 SHA-1
4. **网络问题** — Google 服务在某些地区可能需要代理

#### Q: 如何添加 SHA 签名？

```bash
# 开发签名（debug）
keytool -list -v -alias androiddebugkey -keystore ~/.android/debug.keystore
# 密码: android

# 发布签名（release）
keytool -list -v -alias <your-alias> -keystore <your-keystore>
```

然后到 Firebase 控制台 → 项目设置 → Android 应用 → 添加 SHA 证书指纹。

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
2. Description 填写（如 `MoonlyApp Apple Sign-In`）
3. Identifier 填写反向域名格式（如 `com.yourcompany.moonly.signin`）
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
3. 填写 Display Name（如 `MoonlyApp`）、联系邮箱
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

1. 在 Xcode 中打开 Export 出的 iOS 项目
2. **Signing & Capabilities** 中确保已添加 **Facebook** capability（或通过 `Info.plist` 手动添加）
3. `Info.plist` 中添加：
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
| GoogleSignInHelper | `GoogleSignInHelper` | Google 登录辅助（Play Games SDK） |
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
- [ ] Capability 包含 **Sign In With Apple**
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
├── GooglePlayGames/                   ✅ 已删除（不使用 Play Games SDK）
└── Scripts/
    └── GameManager/
        ├── FirebaseAuthManager.cs    ✅
        ├── GoogleSignInHelper.cs     ✅（已改用 Play Games SDK）
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
| `GoogleSignInHelper` | AndroidJavaClass 封装，返回 ID Token | MonoSingleton + 协程 |
| `AppleSignInHelper` | Apple Sign-In 封装，返回 ID Token + Auth Code | MonoSingleton + 条件编译 |
| `FacebookSignInHelper` | Facebook SDK 封装，返回 Access Token | MonoSingleton + 反射调用 |
| `UserDataManager` | 用户数据内存管理 + PlayerPrefs 持久化 | MonoSingleton |
| `LoginUI` | 登录界面交互层，事件绑定与 UI 反馈 | WindowBase |

### 9.3 数据流向

```
第三方 SDK (Play Games/FB/Apple)
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
- [ ] Play Games SDK 插件未安装（不需要）
- [ ] `play-services-auth` 依赖已添加到 `mainTemplate.gradle`
- [ ] `Assets/Plugins/Android/mainTemplate.gradle` 中无旧版 `google-signin-support` 依赖
- [ ] External Dependency Manager → Force Resolve 已执行
- [ ] google-services.json 在 Assets/ 根目录下
- [ ] GoogleService-Info.plist 在 Assets/StreamingAssets/ 下
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
