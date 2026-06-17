# Firebase 接入与后续任务

## 已经实现的内容

1. Firebase CLI 已安装并登录到账号 `1255755615qq@gmail.com`，当前项目绑定为 `fari-app-b2fd2`。
2. 根目录已加入 `.firebaserc`、`firebase.json`、`firestore.rules`、`firestore.indexes.json`。
3. Unity 登录后会把用户资料同步到 `users/{uid}`，新增字段包括：
   - `selectedOracle`：当前神谕师，值为 `tarot`、`astrology`、`sage`
   - `timezone`：设备时区
   - `membershipStatus`：新用户默认 `free`
   - `profileUpdatedAt`、`lastSignInAt`
4. 今日神谕已接入 Firestore 云端缓存：
   - 路径：`users/{uid}/daily_oracles/{yyyy-MM-dd}`
   - 字段：`cardId`、`orientation`、`title`、`oracle`、`detail`、`dos`、`donts`、`microAction`、`locale`、`oracleId`
   - 同一天再次打开会优先读取云端结果，避免重复生成，也方便换设备同步。
5. 占卜记录沿用现有实现，保存到：
   - `users/{uid}/divination_records/{readingId}`
6. 角色切换时会同步 `selectedOracle` 到 Firestore。

## 你这边需要做的事

1. 在 Firebase Console 确认 Firestore Database 已创建，模式选择 Native mode。
2. 在 Authentication 里打开需要的登录方式：
   - Email/Password
   - Google
   - Apple
   - Facebook
3. 发布 Firestore Rules 和 Indexes：

```bash
HTTPS_PROXY=http://127.0.0.1:7897 HTTP_PROXY=http://127.0.0.1:7897 ALL_PROXY=socks5://127.0.0.1:7897 firebase deploy --only firestore:rules,firestore:indexes
```

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
```

注意：Firebase Cloud Functions 需要项目升级到 Blaze 方案。当前项目执行 dry-run 时，Firebase 返回需要升级 Blaze，否则无法启用 Cloud Build / Cloud Functions API。

升级地址：

```text
https://console.firebase.google.com/project/fari-app-b2fd2/usage/details
```

升级后先设置 Secrets：

```bash
HTTPS_PROXY=http://127.0.0.1:7897 HTTP_PROXY=http://127.0.0.1:7897 ALL_PROXY=socks5://127.0.0.1:7897 firebase functions:secrets:set DEEPSEEK_API_KEY
HTTPS_PROXY=http://127.0.0.1:7897 HTTP_PROXY=http://127.0.0.1:7897 ALL_PROXY=socks5://127.0.0.1:7897 firebase functions:secrets:set VOLC_TTS_API_KEY
```

`VOLC_TTS_API_KEY` 对应新版火山语音控制台的 API Key，会走 `https://openspeech.bytedance.com/api/v3/tts/unidirectional`，并使用 `X-Api-Key` + `X-Api-Resource-Id` 鉴权。

旧版火山控制台参数仍保留兼容，只有未配置 `VOLC_TTS_API_KEY` 时才会使用：

```bash
HTTPS_PROXY=http://127.0.0.1:7897 HTTP_PROXY=http://127.0.0.1:7897 ALL_PROXY=socks5://127.0.0.1:7897 firebase functions:secrets:set VOLC_TTS_APP_ID
HTTPS_PROXY=http://127.0.0.1:7897 HTTP_PROXY=http://127.0.0.1:7897 ALL_PROXY=socks5://127.0.0.1:7897 firebase functions:secrets:set VOLC_TTS_ACCESS_TOKEN
HTTPS_PROXY=http://127.0.0.1:7897 HTTP_PROXY=http://127.0.0.1:7897 ALL_PROXY=socks5://127.0.0.1:7897 firebase functions:secrets:set VOLC_TTS_CLUSTER
```

支付回调密钥单独设置：

```bash
HTTPS_PROXY=http://127.0.0.1:7897 HTTP_PROXY=http://127.0.0.1:7897 ALL_PROXY=socks5://127.0.0.1:7897 firebase functions:secrets:set PAYMENT_WEBHOOK_SECRET
```

然后部署：

```bash
HTTPS_PROXY=http://127.0.0.1:7897 HTTP_PROXY=http://127.0.0.1:7897 ALL_PROXY=socks5://127.0.0.1:7897 firebase deploy --only functions
```

支付回调目前是安全入口骨架，要求请求带 `x-fari-signature`。签名算法为：

```text
HMAC_SHA256(rawBody, PAYMENT_WEBHOOK_SECRET)
```

支付平台正式接入 Apple / Google / Stripe 时，需要在 `paymentWebhook` 内补各平台的收据验签逻辑，再写入：

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

quick_reading/{oracleId}
  oracleId
  enabled
  questions
  updatedAt
```

## 后续建议优先级

1. P0：发布 rules，完成真机登录和每日神谕云端缓存验证。
2. P0：升级 Blaze，设置 Functions Secrets，部署 Cloud Functions。
3. P0：接入 Apple / Google / Stripe 正式支付验签。
4. P1：把记忆系统落到 `users/{uid}/memories`，用于长期画像和关系记忆。
5. P1：把明日 hook 落到 `users/{uid}/tomorrow_hooks`，用于第二天召回。
6. P2：好友、分享、邀请码再接 `friends` 或单独公共集合。
