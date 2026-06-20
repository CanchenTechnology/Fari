# Firebase 接入与后续任务

## 已经实现的内容

1. Firebase CLI 已安装并登录到账号 `1255755615qq@gmail.com`，当前项目绑定为 `fari-app-b2fd2`。
2. 根目录已加入 `.firebaserc`、`firebase.json`、`firestore.rules`、`firestore.indexes.json`。
3. Firestore Rules 和 Indexes 已于 2026-06-20 通过代理部署到 `fari-app-b2fd2`，Firebase 返回 `Deploy complete`。
4. Unity 登录后会把用户资料同步到 `users/{uid}`，新增字段包括：
   - `selectedOracle`：当前神谕师，值为 `tarot`、`astrology`、`sage`
   - `timezone`：设备时区
   - `membershipStatus`：新用户默认 `free`
   - `profileUpdatedAt`、`lastSignInAt`
5. 今日神谕已接入 Firestore 云端缓存：
   - 路径：`users/{uid}/daily_oracles/{yyyy-MM-dd}`
   - 字段：`cardId`、`orientation`、`title`、`oracle`、`detail`、`dos`、`donts`、`microAction`、`locale`、`oracleId`
   - 同一天再次打开会优先读取云端结果，避免重复生成，也方便换设备同步。
6. 占卜记录沿用现有实现，保存到：
   - `users/{uid}/divination_records/{readingId}`
7. 角色切换时会同步 `selectedOracle` 到 Firestore。

## 你这边需要做的事

1. 在 Firebase Console 确认 Firestore Database 已创建，模式选择 Native mode。
2. 在 Authentication 里打开需要的登录方式：
   - Email/Password
   - Google
   - Apple
   - Facebook
3. Firestore Rules 和 Indexes 已部署；如果之后修改了 `firestore.rules` 或 `firestore.indexes.json`，可重新执行：

```bash
HTTPS_PROXY='http://[::1]:7897' firebase deploy --only firestore:rules,firestore:indexes
```

当前本机验证可用的代理分两类：Firebase CLI 部署/列表用 `http://[::1]:7897`，curl/REST smoke 用 `socks5://127.0.0.1:10808`。项目脚本支持同时传 `MOONLY_PROXY` 和 `MOONLY_ALL_PROXY`；如果你自己的代理端口不同，把下面命令里的端口换成实际可用端口即可。

4. 在 Firestore 手动创建 `quick_reading` 配置文档，客户端已有读取逻辑：
   - `quick_reading/tarot`
   - `quick_reading/astrology`
   - `quick_reading/sage`

推荐字段：

```json
{
  "oracleId": "tarot",
  "enabled": true,
  "questions": [
    "我现在最需要看见什么？",
    "这段关系下一步会怎样？",
    "今天适合主动推进吗？"
  ],
  "updatedAt": "2026-06-17"
}
```

5. 支付、AI Key、TTS Key 不要放在 Unity 客户端里。后续建议用 Cloud Functions 做：
   - 会员状态校验
   - 支付回调
   - AI/TTS 请求代理
   - 风控和用量限制

## Cloud Functions 后端代理

已新增 `functions` 目录，并实现以下 HTTPS Functions：

```text
membershipStatus   读取当前用户会员状态
submitIapReceipt   客户端提交 Apple / Google 购买凭证并校验会员状态
aiChat             非流式 AI 代理
aiChatStream       流式 AI 代理
ttsSynthesize      TTS 代理
paymentWebhook     支付回调入口
```

Unity 客户端已改为通过 Firebase ID Token 调用后端代理：

```text
DeepSeekAPI.cs  -> aiChat / aiChatStream
TTSManager.cs   -> ttsSynthesize
UnlockProUI.cs  -> membershipStatus
IapPurchaseManager.cs -> submitIapReceipt
```

注意：Firebase Cloud Functions 需要项目升级到 Blaze 方案。当前项目执行 dry-run 时，Firebase 返回需要升级 Blaze，否则无法启用 Cloud Build / Cloud Functions API。

升级地址：

```text
https://console.firebase.google.com/project/fari-app-b2fd2/usage/details
```

升级后先设置 Secrets。可以用环境变量一次性配置，脚本不会把密钥写入仓库：

```bash
export DEEPSEEK_API_KEY="..."
export VOLC_TTS_API_KEY="..."
export APPLE_SHARED_SECRET="..."
export GOOGLE_PACKAGE_NAME="com.company.moonly"
export GOOGLE_SERVICE_ACCOUNT_JSON_FILE="/absolute/path/google-play-service-account.json"
# 或者直接提供 JSON 字符串：
# export GOOGLE_SERVICE_ACCOUNT_JSON='{"type":"service_account", "...":"..."}'
# 可选：外部支付 webhook 才需要
export PAYMENT_WEBHOOK_SECRET="..."

MOONLY_PROXY='http://[::1]:7897' MOONLY_ALL_PROXY=socks5://127.0.0.1:10808 ./scripts/setup-firebase-secrets.sh
```

也可以继续手动逐个设置：

```bash
HTTPS_PROXY='http://[::1]:7897' firebase functions:secrets:set DEEPSEEK_API_KEY
HTTPS_PROXY='http://[::1]:7897' firebase functions:secrets:set VOLC_TTS_API_KEY
```

`VOLC_TTS_API_KEY` 对应新版火山语音控制台的 API Key，会走 `https://openspeech.bytedance.com/api/v3/tts/unidirectional`，并使用 `X-Api-Key` + `X-Api-Resource-Id` 鉴权。

支付回调密钥单独设置：

```bash
HTTPS_PROXY='http://[::1]:7897' firebase functions:secrets:set PAYMENT_WEBHOOK_SECRET
```

IAP receipt 校验需要额外设置：

```bash
HTTPS_PROXY='http://[::1]:7897' firebase functions:secrets:set APPLE_SHARED_SECRET
HTTPS_PROXY='http://[::1]:7897' firebase functions:secrets:set GOOGLE_PACKAGE_NAME
HTTPS_PROXY='http://[::1]:7897' firebase functions:secrets:set GOOGLE_SERVICE_ACCOUNT_JSON
```

说明：

- `APPLE_SHARED_SECRET` 是 App Store Connect 的订阅共享密钥。
- `GOOGLE_PACKAGE_NAME` 是 Android 包名，例如 `com.company.moonly`。
- `GOOGLE_SERVICE_ACCOUNT_JSON` 是已授权 Google Play Developer API 的服务账号 JSON。

然后部署 Secret 相关 Functions。基础 Functions 已经可以先部署；AI、TTS、IAP 和支付 webhook 需要先运行 `scripts/setup-firebase-secrets.sh` 写入真实密钥，再重新部署并绑定 secrets：

```bash
MOONLY_PROXY='http://[::1]:7897' MOONLY_ALL_PROXY=socks5://127.0.0.1:10808 MOONLY_BIND_ALL_SECRETS=1 ./scripts/deploy-firebase.sh
```

项目根目录提供了标准部署脚本，会先执行 Functions 语法检查，再部署 Firestore rules/indexes 和可安全部署的基础 Functions（`membershipStatus`、`readinessStatus`、`publicConfig`、反馈/后台配置接口）；部署成功后会自动调用 `readinessStatus` 并打印 Firestore、Functions secrets 和仍需动作：

```bash
MOONLY_PROXY='http://[::1]:7897' MOONLY_ALL_PROXY=socks5://127.0.0.1:10808 ./scripts/deploy-firebase.sh
```

默认脚本不会部署缺少 Secret 的 AI/TTS/IAP/Webhook Functions，避免线上出现半配置状态。可用这些开关覆盖：

```bash
# 远端 secrets 已经全部创建后，绑定所有 secrets 并部署完整后端
MOONLY_BIND_ALL_SECRETS=1 ./scripts/deploy-firebase.sh

# 只绑定部分已创建的远端 secrets
MOONLY_DEPLOYED_SECRETS=DEEPSEEK_API_KEY,VOLC_TTS_API_KEY ./scripts/deploy-firebase.sh

# 明确想部署缺配置的函数作为 configuration-error stub 时才使用
MOONLY_DEPLOY_INCOMPLETE_FUNCTIONS=1 ./scripts/deploy-firebase.sh
```

可用 `FIREBASE_ONLY` 覆盖部署范围，例如全量 Functions：`FIREBASE_ONLY=functions MOONLY_BIND_ALL_SECRETS=1 ./scripts/deploy-firebase.sh`。

当前已验证：Firestore Rules/Indexes 已部署；基础 Functions 已部署；`readinessStatus` 在线返回 HTTP 200。线上 Functions 列表显示 `membershipStatus`、`readinessStatus`、`publicConfig`、反馈/后台配置、`aiChat`、`aiChatStream`、`ttsSynthesize`、`paymentWebhook`、`submitIapReceipt` 均已存在；客户端 Functions authenticated smoke test 通过，`aiChat` / `ttsSynthesize` 当前返回 `HTTP 200`；`submitIapReceipt` 无 token 请求返回 401，authenticated smoke test 返回 `HTTP 202` + `pending_configuration`，说明 endpoint 已上线且能接收带 Firebase ID Token 的 receipt。客户端 Game Center 登录入口已接 Firebase `GameCenterAuthProvider.GetCredentialAsync()`，iOS 导出后处理器会自动写入 Game Center / Sign in with Apple / In-App Purchase capabilities，本地 readiness 会检查接线、provider 类型解析和后处理器配置；真实登录仍需要 iOS/tvOS 真机或模拟器、Apple Game Center 后台能力和 Firebase Auth Game Center provider 配置。`readinessStatus.secretDiagnostics` 已明确说明：未绑定给健康检查函数的 secret 会显示为 false，但客户端 smoke 通过时仍以目标函数真实调用结果为准。

当前远端 Functions Secrets 状态：`DEEPSEEK_API_KEY`、`VOLC_TTS_API_KEY`、`PAYMENT_WEBHOOK_SECRET` 已存在并已重新绑定部署到对应函数；`APPLE_SHARED_SECRET`、`GOOGLE_PACKAGE_NAME`、`GOOGLE_SERVICE_ACCOUNT_JSON` 仍缺失，所以真实 IAP receipt 校验仍处于待配置状态。

本地状态可先跑：

```bash
./scripts/check-local-readiness.sh
MOONLY_PROXY='http://[::1]:7897' MOONLY_ALL_PROXY=socks5://127.0.0.1:10808 CURL_MAX_TIME=180 CHECK_REGISTRY=1 CHECK_SECRETS_ENV=1 CHECK_FIREBASE=1 CHECK_FIREBASE_NETWORK=1 CHECK_FIREBASE_FUNCTIONS=1 CHECK_FUNCTIONS_SMOKE=1 CHECK_FUNCTIONS_SMOKE_STRICT=1 CHECK_IAP_SMOKE=1 CHECK_BUILD=1 ./scripts/check-local-readiness.sh
```

第一条只做快速本地检查；第二条会额外验证 Unity registry 中目标包版本、Game Center Firebase Auth 接线、iOS 导出能力后处理器和导出检查脚本、`UNITY_PURCHASING` 编译宏、Android `POST_NOTIFICATIONS` 权限、通知设置 Firestore 同步、好友/关系/Tomorrow Hook 通知触发点、Secrets 环境变量、Firebase CLI 登录/项目绑定、Firebase REST 网络连通性、线上 Functions 列表、客户端 authenticated smoke tests、`submitIapReceipt` authenticated smoke test、C# 编译和 Unity IAP 桥接编译。`CHECK_FUNCTIONS_SMOKE_STRICT=1` 会要求 `aiChat` 与 `ttsSynthesize` 必须真实返回 `HTTP 200`，不能把缺配置的 `HTTP 500` 当成可接受状态。`firebase functions:list` 在代理环境下偶发失败时，脚本会在已启用 smoke tests 的情况下把列表失败降为 warning，并以真实 HTTP 调用结果证明客户端可用性；如果要让函数列表失败直接阻断检查，可额外加 `CHECK_FIREBASE_FUNCTIONS_STRICT=1`。当前 `packages-lock.json` 已包含 `com.unity.purchasing@4.12.2` 与 `com.unity.mobile.notifications@2.3.2`，`ProjectSettings` 已为 Android / iPhone / Standalone 写入 `UNITY_PURCHASING`。

远端 Secrets 可单独验证：

```bash
MOONLY_PROXY='http://[::1]:7897' ./scripts/check-firebase-secrets.sh
```

该脚本不会打印密钥值，只检查 Secret 是否存在、是否非空，并验证 `GOOGLE_SERVICE_ACCOUNT_JSON` 是否为合法服务账号 JSON。`scripts/setup-firebase-secrets.sh` 支持 `GOOGLE_SERVICE_ACCOUNT_JSON_FILE` 文件输入，避免把整段服务账号 JSON 粘进 shell；本地 readiness 也会识别该文件来源并校验 `type == service_account`、`client_email` 和 `private_key`。正式 IAP 上线前可以把它接入总检查：

```bash
MOONLY_PROXY='http://[::1]:7897' MOONLY_ALL_PROXY=socks5://127.0.0.1:10808 CURL_MAX_TIME=180 CHECK_FIREBASE_SECRETS=1 CHECK_REGISTRY=1 CHECK_FIREBASE=1 CHECK_FIREBASE_NETWORK=1 CHECK_FIREBASE_FUNCTIONS=1 CHECK_FUNCTIONS_SMOKE=1 CHECK_FUNCTIONS_SMOKE_STRICT=1 CHECK_IAP_SMOKE=1 CHECK_BUILD=1 ./scripts/check-local-readiness.sh
```

当 Apple / Google IAP 三个 secrets 都配置完成后，可以只部署 IAP 相关函数：

```bash
MOONLY_PROXY='http://[::1]:7897' MOONLY_ALL_PROXY=socks5://127.0.0.1:10808 ./scripts/deploy-iap-functions.sh
```

客户端 Functions 调用面可以单独验证：

```bash
MOONLY_ALL_PROXY=socks5://127.0.0.1:10808 CURL_MAX_TIME=180 REQUIRE_AI_TTS_LIVE=1 ./scripts/smoke-functions-auth.sh
```

该脚本会临时登录测试用户，验证 `publicConfig`、`membershipStatus`、`aiChat`、`ttsSynthesize` 的真实 HTTP 调用。`REQUIRE_AI_TTS_LIVE=1` 会要求 AI/TTS 必须真实可用；当前线上 AI/TTS strict smoke 返回 `HTTP 200`，说明客户端调用面可用；`readinessStatus` 仍主要用于确认 Firestore、基础函数和缺失配置提示。

IAP 提交链路可以单独验证：

```bash
MOONLY_ALL_PROXY=socks5://127.0.0.1:10808 ./scripts/smoke-submit-iap-receipt.sh
```

该脚本会用 Firebase Web API key 临时登录一个测试用户：如果匿名登录被禁用，会创建临时 Email/Password 用户。默认会提交一个 Apple fake receipt 到 `submitIapReceipt`；IAP secrets 缺失时预期返回 `HTTP 202` + `pending_configuration`，IAP secrets 已配置时 fake receipt 预期返回 `HTTP 402` + `invalid`。两种状态都不会误开 Pro。

真实沙盒 receipt 验证：

```bash
MOONLY_ALL_PROXY=socks5://127.0.0.1:10808 \
IAP_SMOKE_MODE=real \
IAP_PRODUCT_ID=moonly.pro.monthly \
IAP_STORE=AppleAppStore \
IAP_RECEIPT='<App Store sandbox receipt or Unity receipt JSON>' \
./scripts/smoke-submit-iap-receipt.sh
```

真实 receipt 模式要求返回 `HTTP 200` + `verified` + `membershipStatus=pro` + `isPro=true`。该模式默认不删除临时 Auth 用户，便于你在 Firebase Console 里检查 `users/{uid}` 的会员状态；如需清理 Auth 测试用户，可显式设置 `CLEANUP_AUTH_USER=1`。

如果 Unity Editor 里 Firestore 报 `Unavailable`、搜索用户一直失败，可以单独跑网络检查：

```bash
./scripts/check-firebase-network.sh
MOONLY_ALL_PROXY=socks5://127.0.0.1:10808 ./scripts/check-firebase-network.sh
```

它会检查 Firestore REST、Firebase Auth、Secure Token、Firebase Management 和 `readinessStatus` 端点是否能连通。`readinessStatus` 返回非 200 时代表函数还没部署或尚未就绪，但只要有 HTTP 响应，就说明网络层能连到 Firebase。

若 Unity 当前已经打开项目，可以在菜单里执行：

```text
Tools/Moonly/Resolve Required Packages
```

它会通过 Unity Package Manager API 依次请求解析 `com.unity.purchasing@4.12.2` 和 `com.unity.mobile.notifications@2.3.2`。解析后再运行 `Tools/Moonly/Log Readiness Report` 或 `./scripts/check-local-readiness.sh` 验证。

若 Unity 已关闭，也可以直接用命令行解析：

```bash
./scripts/resolve-unity-packages.sh
```

该脚本会先检查 `Temp/UnityLockfile`，避免和已打开的 Unity 实例冲突；解析日志写入 `Logs/package-resolve-batch.log`。

iOS 真机前可以导出并验证 Xcode 工程：

```bash
CLEAN_IOS_EXPORT=1 ./scripts/build-ios-xcode.sh
```

该脚本会调用 `CommandLineBuild.BuildIOSProject` 导出到 `Builds/iOS`，然后运行：

```bash
./scripts/check-ios-export.sh Builds/iOS
```

导出检查会验证：

- `Info.plist` 已写入 `NSContactsUsageDescription`。
- `.entitlements` 已包含 `com.apple.developer.game-center` 和 `com.apple.developer.applesignin`。
- `project.pbxproj` 已启用 Game Center、Sign in with Apple、In-App Purchase capability，并指向 entitlements 文件。
- Unity 当前打开时，batchmode 导出脚本会主动停止，避免两个 Unity 实例同时写项目。

如果只想把已有导出工程纳入总检查：

```bash
CHECK_IOS_EXPORT=1 ./scripts/check-local-readiness.sh
```

如果想由总检查触发导出并验证：

```bash
CHECK_IOS_BUILD=1 ./scripts/check-local-readiness.sh
```

Android 真机前可以先检查源配置：

```bash
./scripts/check-android-config.sh
```

该脚本会验证：

- `Assets/Plugins/Android/AndroidManifest.xml` 包含 `READ_CONTACTS`、`POST_NOTIFICATIONS`，并且不是 `debuggable=true`。
- Firebase `google-services.xml` 属于 `fari-app-b2fd2`，并包含 Android / Web client id。
- Google Sign-In bridge activity 不对外导出。
- Facebook ApplicationId / ClientToken 已写入 Manifest。
- Android 包名、minSdk、targetSdk、versionCode、`UNITY_PURCHASING`、Firebase/Facebook Gradle 依赖和 Maven 仓库配置有效。

打 Android APK 并验证：

```bash
CLEAN_ANDROID_BUILD=1 ./scripts/build-android-apk.sh
```

如果后续要启用 Google Play Games 服务，先在 Google Play Console 拿到数字 App ID，再写入插件 manifest：

```bash
GOOGLE_PLAY_GAMES_APP_ID=123456789012 ./scripts/configure-google-play-games.sh
./scripts/check-android-config.sh
```

当前 App 的 Google 登录不依赖 Play Games SDK；未配置 Play Games App ID 只会作为非阻断 warning。

如果只想检查已有 APK：

```bash
./scripts/check-android-config.sh --apk Builds/Android/MoonlyApp.apk
```

也可以接入总检查：

```bash
CHECK_ANDROID_APK=1 ./scripts/check-local-readiness.sh
CHECK_ANDROID_BUILD=1 ./scripts/check-local-readiness.sh
```

当前旧 `Builds/Android/MoonlyApp.apk` 是新增权限和 debuggable 修正前的产物，APK 检查会失败；重新构建后再检查。

部署完成后可以打开健康检查：

```text
https://us-central1-fari-app-b2fd2.cloudfunctions.net/readinessStatus
```

返回内容只包含布尔状态，不会泄露密钥值；它会报告 Firestore 是否可读、基础 Functions 是否已部署、`DEEPSEEK_API_KEY` / `VOLC_TTS_API_KEY` / Apple / Google / webhook secrets 是否已配置，以及仍需要做的外部动作。

支付回调目前是安全入口骨架，要求请求带 `x-fari-signature`。签名算法为：

```text
HMAC_SHA256(rawBody, PAYMENT_WEBHOOK_SECRET)
```

客户端购买成功后走 `submitIapReceipt`：

```text
Unity IAP receipt -> IapPurchaseManager.SubmitPurchaseReceipt -> submitIapReceipt
```

Unity 客户端购买入口：

- `Packages/manifest.json` 和 `Packages/packages-lock.json` 已加入 `com.unity.purchasing@4.12.2`。
- `ProjectSettings` 已为 Android / iPhone / Standalone 写入 `UNITY_PURCHASING`，`IapPurchaseManager.PurchaseSubscription` 会调用 `UnityIapPurchaseBridge`。
- `UnityIapPurchaseBridge` 使用 Unity IAP v4 `IDetailedStoreListener` 初始化 Unity IAP、发起订阅购买、读取商店 receipt、提交到 `submitIapReceipt`，并在失败时返回更详细的商店错误描述。
- `UnlockProUI` 的“恢复购买”会先走 Unity IAP 恢复/提交已有 receipt，再刷新后端会员状态。
- 如果 Unity IAP 尚未完成 Package Manager 解析，代码会安全降级为“Unity IAP 包未安装”提示。
- 当前桥接使用 Unity IAP v4 的 `IDetailedStoreListener` API；Unity IAP v5 已切换到 `StoreController` / `UnityIAPServices`，如未来升级 v5 需要重写桥接层。
- `AppReadinessDiagnostics` 会在启动、Firebase 初始化和登录成功后输出 IAP/通知/Firebase/Functions 就绪状态；如果 `manifest=yes` 但 `packageResolved=no`，说明 `Packages/manifest.json` 已写入但 Unity Editor 还没有完成 Package Manager 解析。通知诊断会额外输出 Android 通知权限、通知设置摘要和最近排程快照。
- Unity Editor 菜单 `Tools/Moonly/Log Readiness Report` 可以手动输出同一份报告；`Tools/Moonly/Copy Firebase Deploy Command` 会复制标准部署命令；`Tools/Moonly/Copy Full Readiness Command` 会复制完整验收命令；`Tools/Moonly/Open Functions Readiness URL` 会打开 `readinessStatus`；`Tools/Moonly/Schedule Test Notification (10s)` 可排程测试通知，`Tools/Moonly/Log Scheduled Notifications` 可查看最近排程状态。
- `scripts/check-firebase-network.sh` 可单独验证 Editor/本机是否能访问 Firebase REST、Auth、Secure Token 和 Functions 端点；遇到 Firestore `Unavailable` 时优先跑它区分网络问题和数据/规则问题。
- `scripts/smoke-functions-auth.sh` 可单独验证 `publicConfig`、`membershipStatus`、`aiChat`、`ttsSynthesize` 的 authenticated 客户端调用面。
- `scripts/smoke-submit-iap-receipt.sh` 可单独验证线上 `submitIapReceipt` 的 Auth 和 receipt 提交流程；在 IAP secrets 缺失时应返回 `pending_configuration`，不会误开 Pro。

`submitIapReceipt` 会：

- Apple：调用 Apple `verifyReceipt`，自动兼容 sandbox 21007。
- Google：用服务账号换取 Android Publisher access token，再校验订阅 purchase token。
- 校验成功：写入 `users/{uid}.membershipStatus = pro` 和 `users/{uid}.proExpiresAt`。
- 密钥缺失：线上函数已可接收 authenticated receipt，并写入 `iap_receipts/{receiptId}.status = pending_configuration`，不误开 Pro。
- 凭证无效或过期：返回失败状态。

支付平台服务端 webhook 可继续走 `paymentWebhook`，写入：

```text
users/{uid}.membershipStatus
users/{uid}.proExpiresAt
payment_events/{eventId}
```

6. 真机测试时重点验证：
   - 首次登录会创建 `users/{uid}`
   - 切换神谕师会更新 `selectedOracle`
   - 今日神谕第一次生成后会创建 `daily_oracles/{当天日期}`
   - 同一天重启 App 不会重新生成今日神谕
   - 三张牌占卜完成后会写入 `divination_records`
   - 通知设置会写入 `users/{uid}/settings/notifications`
   - 打开好友页时，好友请求、关系占卜邀请、好友每日牌动态会根据“朋友互动提醒”设置触发提醒入口
   - 点击“明天再看”会写入 `users/{uid}/tomorrow_hooks/{hookId}`，第二天打开今日神谕会读取到期线索并注入对话记忆上下文

## 当前 Firestore 结构

```text
users/{uid}
  displayName
  email
  photoUrl
  birthday
  birthTime
  city
  avatarType
  loginType
  isEmailVerified
  selectedOracle
  timezone
  membershipStatus
  profileUpdatedAt
  createdAt
  lastSignInAt

users/{uid}/daily_oracles/{yyyy-MM-dd}
  date
  cardId
  cardName
  orientation
  title
  oracle
  detail
  dos
  donts
  microAction
  locale
  oracleId
  createdAtLocal
  updatedAt

users/{uid}/divination_records/{readingId}
  readingId
  question
  scene
  spreadKind
  lockedCards
  shortVerdict
  judgeContent
  adviceContent
  topics
  oracleId
  createdAt
  updatedAt

users/{uid}/dialog_sessions/default
  messages[]
    id
    roleType
    messageType
    content
    oraclePromptId
    oracleScene
    oracleStage
    oracleStageReason
    oracleResponseMode
    oracleRiskLevel
    oracleRiskFlags
  apiMessages[]
  activeReadingId
  activeReadingState
  activeActionKind
  activeRelationshipId

users/{uid}/settings/notifications
  dailyOracleEnabled
  dialogueReplyEnabled
  divinationReturnEnabled
  friendInteractionEnabled
  activitySystemEnabled
  reminderTime
  updatedAt

users/{uid}/tomorrow_hooks/{hookId}
  hookId
  userId
  relationshipId
  sourceReadingId
  sourceConversationId
  hookType
  triggerText
  scheduledForLocalDate
  status
  createdAt
  updatedAt

quick_reading/{oracleId}
  oracleId
  enabled
  questions
  updatedAt
```

## 后续建议优先级

1. P0：设置 Functions Secrets：`DEEPSEEK_API_KEY`、`VOLC_TTS_API_KEY`、`APPLE_SHARED_SECRET`、`GOOGLE_PACKAGE_NAME`、`GOOGLE_SERVICE_ACCOUNT_JSON`。
2. P0：用 `MOONLY_BIND_ALL_SECRETS=1 ./scripts/deploy-firebase.sh` 部署 AI / TTS / `submitIapReceipt`；Firestore Rules/Indexes 和基础 Functions 已部署。
3. P0：在 Unity Editor 完成 `com.unity.purchasing@4.12.2` 和 `com.unity.mobile.notifications@2.3.2` Package Manager 解析后，重新运行并查看 `AppReadinessDiagnostics`。
4. P0：配置 App Store / Google Play 商品，用沙盒账号验证 Unity IAP 购买、恢复和 receipt 校验。
5. P1：真机验证 Android/iOS 通知权限、每日重复提醒、Tomorrow Hook 和好友互动即时提醒。
6. P1：继续扩展 `users/{uid}/memories` 的可视化管理和云端长期画像清理策略。
7. P2：按产品确认结果补邀请码、更多分享入口和社交平台审核项。
