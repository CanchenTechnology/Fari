# Firebase 后台材料获取与交接清单

这份清单用于补齐 Moonly/Fari 后台上线前还缺的外部材料。你拿到材料后，可以选择：

- 发给我材料内容或本机文件路径，让我继续写入 Firebase Secrets 并部署。
- 或者填到本机 `scripts/release.env`，不要提交到 Git。

当前最关键缺口：

- `APPLE_SHARED_SECRET`
- `GOOGLE_SERVICE_ACCOUNT_JSON`
- 真实 App Store / Google Play 商品配置和沙盒购买 receipt
- Firebase App Check 控制台确认
- 真实社媒链接、商品价格文案

## 1. Apple Shared Secret

用途：给 Firebase Function `submitIapReceipt` 校验 App Store 订阅收据。

你需要拿到：一个字符串，写入 Firebase Secret `APPLE_SHARED_SECRET`。

操作路径：

1. 打开 [App Store Connect](https://appstoreconnect.apple.com/)。
2. 进入 **Apps**。
3. 选择你的 app。
4. 进入 app 的 **Subscriptions** 页面。
5. 找到 app-specific shared secret / app 专用共享密钥。
6. 如果还没有，点击生成；如果已有，复制当前值。

注意：

- 推荐用 **app-specific shared secret**，不要用整个账号通用 shared secret。
- Apple 文档说明 app-specific shared secret 位于 app 的 Subscriptions 页面，且生成后不能删除，只能重新生成。
- 如果重新生成，旧密钥相关服务也要同步替换。

给我的内容：

```text
APPLE_SHARED_SECRET=<复制到的 shared secret>
```

## 2. Google Play 服务账号 JSON

用途：给 Firebase Function `submitIapReceipt` 校验 Google Play 订阅/购买凭证。

你需要拿到：一个 `.json` 文件，写入 Firebase Secret `GOOGLE_SERVICE_ACCOUNT_JSON`。

同时确认：Firebase Secret `GOOGLE_PACKAGE_NAME` 已是你的 Android 包名：

```text
com.canchentechnology.fari
```

操作路径：

1. 打开 [Google Play Console](https://play.google.com/console/)。
2. 选择开发者账号和你的 app。
3. 进入 **Setup / 设置** -> **API access / API 访问权限**。
4. 确认已关联一个 Google Cloud project。
5. 到 Google Cloud Console 的 **Service Accounts / 服务账号** 页面。
6. 创建一个服务账号，例如 `moonly-iap-verifier`。
7. 给服务账号创建 **JSON key**，下载得到 `.json` 文件。
8. 回到 Google Play Console 的 **Users and permissions / 用户和权限**。
9. 邀请这个服务账号邮箱。
10. 给它至少能查看订单/订阅/财务相关数据的权限，用于 purchase/subscription verification。
11. 确认 Google Play Android Developer API 已启用。

给我的内容：

```text
GOOGLE_SERVICE_ACCOUNT_JSON_FILE=/你的本机路径/google-play-service-account.json
```

不要把 JSON 内容提交到 Git。可以只告诉我本机绝对路径。

## 3. App Store / Google Play 商品

用途：让 Unity IAP 的商品 ID 和后台商品完全一致。

当前项目默认商品 ID：

```text
moonly.pro.monthly
moonly.pro.yearly
```

你需要在两个商店后台都确认：

- 月订阅商品存在：`moonly.pro.monthly`
- 年订阅商品存在：`moonly.pro.yearly`
- 商品状态可用于沙盒测试
- 价格、地区、订阅组配置完成
- 测试账号可以购买

如果你想换商品 ID，先告诉我，我来同步：

- Firebase `publicConfig`
- Unity 默认配置
- 文档
- 后端校验流程

## 4. 真实沙盒购买 Receipt

用途：最终验证 `submitIapReceipt` 真的能校验 Apple/Google receipt，而不是只验证接口存在。

你需要：

1. 用真机或商店沙盒环境登录。
2. 在 app 里购买 `moonly.pro.monthly` 或 `moonly.pro.yearly`。
3. 确认 Unity IAP 返回 receipt。
4. 把 receipt 用于 smoke test。

如果你不知道 receipt 在哪里，我可以继续帮你在 Unity 日志里加安全的调试输出，注意不在正式包打印完整 receipt。

## 5. Firebase App Check

用途：减少别人绕过客户端直接刷你的 Firestore、Storage、Functions、AI/TTS 用量。

操作路径：

1. 打开 Firebase Console。
2. 进入项目 `fari-app-b2fd2`。
3. 进入 **Build** -> **App Check**。
4. Android app 注册 Play Integrity provider。
5. iOS app 注册 DeviceCheck 或 App Attest provider。
6. 先用 Monitor / 监控模式观察一段时间。
7. 真机验证登录、头像上传、AI、TTS、IAP 都正常。
8. 上线前再对 Firestore、Storage、Cloud Functions 开启 Enforce / 强制执行。

注意：

- 不要一开始就强制开启，先监控，避免测试包突然全部 401/permission denied。
- Unity 侧需要接 Firebase App Check SDK；如果还没接，我可以继续补。

## 6. 公开配置和反馈后台

当前后台页面：

```text
https://fari-app-b2fd2.web.app/feedback-admin
```

当前公开配置还需要你给真实值：

```text
instagram=<你的 Instagram 链接>
facebook=<你的 Facebook 链接>
x=<你的 X/Twitter 链接>
tiktok=<你的 TikTok 链接>
pinterest=<你的 Pinterest 链接>
monthly price label=<例如 $4.99/月>
yearly price label=<例如 $29.99/年>
```

我拿到这些后会更新：

- `app_config/public`
- `functions/public-config.live.json`
- Firebase Hosting / Functions 相关验证

## 7. 你拿到后怎么交给我

最简单：

```text
APPLE_SHARED_SECRET=...
GOOGLE_SERVICE_ACCOUNT_JSON_FILE=/Users/你的用户名/Downloads/google-play-service-account.json
社媒链接：
Instagram=...
Facebook=...
X=...
TikTok=...
Pinterest=...
价格：
Monthly=...
Yearly=...
```

更安全：

1. 打开 `scripts/release.env`。
2. 填入：

```bash
APPLE_SHARED_SECRET=...
GOOGLE_SERVICE_ACCOUNT_JSON_FILE=/absolute/path/google-play-service-account.json
GOOGLE_PACKAGE_NAME=com.canchentechnology.fari
```

3. 告诉我“我填好了”，我来跑脚本。

## 8. 我拿到材料后会做什么

我会继续执行：

```bash
MOONLY_SECRET_NAMES=APPLE_SHARED_SECRET,GOOGLE_SERVICE_ACCOUNT_JSON \
APPLE_SHARED_SECRET='<secret>' \
GOOGLE_SERVICE_ACCOUNT_JSON_FILE='/path/to/service-account.json' \
./scripts/setup-firebase-secrets.sh

MOONLY_BIND_ALL_SECRETS=1 ./scripts/deploy-firebase.sh

./scripts/smoke-submit-iap-receipt.sh
```

如果有真实沙盒 receipt，我会再跑真实校验模式。

## 参考官方文档

- Apple：Generate a shared secret to verify receipts  
  https://developer.apple.com/help/app-store-connect/configure-in-app-purchase-settings/generate-a-shared-secret-to-verify-receipts/
- Google：Google Play Developer API - use a service account  
  https://developers.google.com/android-publisher/getting_started
- Firebase：App Check overview  
  https://firebase.google.com/docs/app-check
- Firebase：Get started using App Check in Unity apps  
  https://firebase.google.com/docs/app-check/unity/default-providers
