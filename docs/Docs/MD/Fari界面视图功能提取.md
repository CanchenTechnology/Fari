# Fari 界面视图功能提取

来源：`Fari 界面视图(3).xmind`  
提取时间：2026-06-18

## 总体结构

XMind 中包含 5 个一级模块：

- 注册：暂时留空，正在重置。
- 每日占卜：每日神谕、翻卡、今日占卜详情、引入对话。
- 对话：AI 聊天、快速占卜、三张牌占卜、占卜结果卡片、会员页。
- 朋友：真实好友、创建好友档案、好友邀请、好友关系占卜、好友主页与同步设置。
- 我的：个人控制台、历史、资料、记忆、账户、通知、反馈、关注、会员购买。

## 每日占卜

### 每日翻卡流程

- 用户每天首次进入“今日神谕”后进入翻卡流程。
- 翻卡流程包含 3 个状态：待翻卡、翻卡中、翻卡结束。
- 每天只走一次完整翻卡流程。
- 当天完成翻卡后，“今日神谕/今日占卜”文本持续展示，不再重复触发翻卡。
- 需要保存每日占卜状态：日期、是否已翻卡、抽到的牌、正逆位、神谕标题、简短文本、完整解读。

### 今日占卜简介卡

- 翻卡完成后显示今日占卜简介。
- 展示内容包括：牌图、牌名、正逆位、标题、简短神谕文案。
- 提供“查看完整解读”按钮，该入口免费。
- 提供“继续和她聊聊/引入对话”按钮。
- 从简介卡引入对话时，需要自动把今日占卜卡片发送到对话列表中。
- 从底部导航直接进入对话时，不应自动发送今日占卜卡片。

### 今日占卜详情

- 展示完整今日占卜解读。
- 需要支持返回上一页。
- 需要展示牌信息、正文解读、行动建议或进一步提示。
- 详情页可作为从每日占卜卡、对话内卡片、历史记录进入的复用页面。

## 对话

### 基础对话

- 支持与神谕师进行聊天。
- 页面顶部支持切换神谕师。
- 底部导航可以进入对话。
- 聊天区支持普通文本消息、占卜邀请卡、占卜结果卡等复合消息。

### 快速占卜

- 当用户间隔 2 小时未对话，并且从导航栏进入对话时，自动弹出快速占卜入口。
- 快速占卜入口用于引导用户进入对话状态。
- 快速占卜卡支持展开。
- 展开后应展示可选占卜问题或快捷入口。
- 用户可以选择快速占卜，也可以继续普通聊天。

### AI 调用占卜工具

- 需要预备占卜工具能力。
- AI 可以在对话中调用占卜工具。
- AI 能填写对应占卜信息，例如问题、牌阵类型、牌位含义等。
- 工具调用结果应生成可交互占卜卡，而不是只生成普通文本。

### 三张牌占卜

- 对话中支持三张牌阵。
- 三张牌阵包含 3 个牌位：状态、阻碍、建议。
- 用户点击“开始三张牌阵”后进入抽牌流程。
- 抽牌过程包含：待抽牌、翻开第一张、翻开第二张、翻开第三张。
- 每张牌翻开后需要显示牌图、牌名、正逆位和对应牌位。
- 三张牌全部翻开后生成占卜结果卡片。

### 占卜详情

- 三张牌占卜详情需要展示：
  - 三张牌概览。
  - 每张牌的牌位、牌名、正逆位、解释。
  - 综合判断。
  - 行动建议。
  - 继续追问建议。
- 支持点击追问问题继续对话。
- 支持“继续和她聊聊”回到对话。
- 支持分享结果。

### 会员页

- 对话模块包含会员页入口或触发点。
- 当功能受限时，需要能引导用户查看或购买 Pro。
- 当前实现：
  - `BackendMembershipClient` 会读取后端会员状态，并缓存当前 Pro / Free 状态。
  - `UsageStatsManager` 记录本地每日使用次数：每日牌、对话消息、牌阵占卜。
  - `MembershipGate` 已接入每日牌、对话消息、单张/三张/五张牌阵抽牌入口；免费额度用完会弹出 `UnlockProUI`。
  - `UnlockProUI` 会显示当前会员状态，并运行时生成月度 Pro / 年度 Pro 购买按钮。
  - `UnlockProUI` 会刷新 prefab 内已有权益卡、当前方案和标题文案；账号已是 Pro 时禁用重复购买按钮，并引导用户进入订阅管理。
  - 购买按钮读取 `PublicAppConfig.iapProducts` 的商品名和价格，配置缺失时使用默认商品 ID。
  - `Packages/manifest.json` 与 `Packages/packages-lock.json` 已加入 `com.unity.purchasing@4.12.2`；`ProjectSettings` 已为 Android / iPhone / Standalone 写入 `UNITY_PURCHASING`，`IapPurchaseManager` 会通过 `UnityIapPurchaseBridge` 发起真实订阅购买。
  - 当前 `IapPurchaseManager` 已提供统一购买入口、恢复购买入口和订阅管理入口。
  - `UnityIapPurchaseBridge` 已升级为 Unity IAP v4 `IDetailedStoreListener`，购买失败时能回传更详细的商店错误。
  - `IapPurchaseManager.SubmitPurchaseReceipt` 可把商店购买凭证提交到 Cloud Functions。
  - Cloud Function `submitIapReceipt` 已接 Apple / Google receipt 校验路径；校验成功才升级 Pro，密钥未配置时会记录 `pending_configuration`。

## 朋友

### 无好友状态

- 当没有好友连接时展示空状态。
- 空状态提供两个主要动作：
  - 添加好友：添加现实中的朋友。
  - 创建好友：创建一个虚拟/手动维护的好友档案。

### 好友列表

- 有好友时展示“朋友 Friends”列表。
- 列表顶部提供添加好友、创建好友两个入口。
- 列表分组：
  - 已有好友：真实连接的好友。
  - 我创建的好友：用户手动创建的人物档案。
- 每个好友条目展示头像、名称、最近互动时间或创建时间。
- 每个好友条目支持进入好友主页。
- 每个好友条目提供 `@` 入口，用于在对话中引用该好友。

### 好友邀请提醒

- 有占卜邀请时，好友页需要显示邀请状态。
- 需要支持从好友页进入待处理占卜邀请。
- 通知铃铛需要显示未读提示。

### 添加真实好友

- 添加好友入口页包含 3 种添加方式：
  - 用户名搜索：通过应用内用户名查找并添加用户。
  - 通讯录 + 短信邀请：从通讯录查找好友，或通过短信邀请加入。
  - Facebook 添加：连接 Facebook，发现并添加好友。
- 支持“稍后再添加”跳过。
- 用户名搜索流程包含：
  - 搜索输入框。
  - 搜索结果页。
  - 发送好友请求。
  - 好友请求已发送状态页。
- 通讯录方式需要处理通讯录权限、联系人列表、短信邀请。
- Facebook 方式需要处理 Facebook 授权、好友发现、添加请求。

### 创建好友档案

- 用户可以创建一个用于独立占卜的好友档案。
- 创建表单包含：
  - 头像上传。
  - 用户名称，限制 20 字符。
  - 生日。
  - 出生时间。
  - 出生城市。
- 表单提示：这些信息用于占卜体验，仅用户可见。
- 支持头像选择与上传。
- 支持出生信息确认。
- 创建成功后进入成功页或好友主页。
- 创建后的好友可以编辑资料。

### 在对话中 @ 好友

- 从好友列表点击 `@` 可进入对话页并带上好友状态。
- 对话输入中支持 `@` 好友提示词。
- AI 对话需要能读取被 `@` 的好友上下文。
- 可能需要区分真实好友和用户创建的好友档案。

### 双人关系占卜

- 可以从好友或对话中发起好友占卜邀请。
- 关系占卜说明：牌阵共三张。
- 三张牌含义：
  - 你可翻开：你的内心与看法，仅你可见。
  - 共同揭示：关系的现状与指引，双方可见。
  - 对方可翻开：对方的内心与想法，仅对方可见。
- 发起者可以先翻自己的牌。
- 发起后进入等待对方翻牌状态。
- 接收者收到占卜邀请后可以加入并翻自己的牌。
- 需要隐私保护提示：双方只看到自己可见的结果，共同牌双方可见。
- 双方都完成后展示双方占卜结果页。
- 需要同步双方状态：已邀请、发起者已翻牌、等待对方、接收者已加入、双方完成。
- 当前实现：
  - 好友列表 `oracleBtn` 可对真实好友发起双人关系占卜邀请，对创建好友档案则生成本地即时关系占卜。
  - 真实好友资料页右上“更多”中可发起关系占卜。
  - 创建好友主页会动态生成“关系占卜”按钮。
  - 真实好友邀请写入 `relationship_divinations/{readingId}`，状态包含 `invited`、`initiator_revealed`、`receiver_joined`、`completed`、`cancelled`。
  - 好友页会读取当前用户收到的未完成关系占卜邀请，并显示运行时邀请卡。
  - 运行时关系占卜面板展示三张牌、当前状态和隐私说明，支持翻开自己的牌、取消邀请、完成后进入对话解读。
  - 完成后会把本次关系占卜保存到当前用户 `users/{uid}/divination_records/{readingId}`，方便后续历史记录复用。
  - 隐私策略：自己的私牌只在自己翻开后可见；共同牌在双方完成后可见；对方私牌不会直接展示给当前用户。

### 好友主页

- 真实好友主页展示好友资料和互动/占卜入口。
- 创建好友主页展示手动创建档案的资料和占卜入口。
- 创建好友支持编辑。
- 真实好友和创建好友主页应有差异化权限与操作。

### 每日占卜同步设置

- 好友模块包含每日占卜同步设置页。
- 需要支持配置是否与好友同步或分享每日占卜相关内容。
- 已接入 `DailyDivinationSyncSettingsUI`：
  - 返回按钮关闭页面。
  - 信息按钮提示只同步摘要，不同步完整解读。
  - 主开关控制是否自动同步每日牌摘要到动态。
  - 可见范围支持“所有好友 / 仅真实好友 / 仅自己”。
  - 关闭主开关时，可见范围选项不可操作。
  - 点击保存会写入本地 `PlayerPrefs` 和 Firestore。
- 已接入每日牌保存流程：
  - `users/{uid}/daily_oracles/{date}` 保存完整每日牌与完整解读，仅本人可读。
  - `daily_oracle_summaries/{uid}_{date}` 只保存摘要字段，供好友动态读取。
  - 保存设置后会同步更新今天的摘要，并刷新最近 30 天已存在摘要的可见性，关闭或改为仅自己可见后旧摘要不会继续出现在好友动态。
- 已接入好友页动态展示：
  - 好友页顶部入口打开 `DailyDivinationSyncSettingsUI`。
  - 好友页会运行时生成“今日好友每日牌”动态区块。
  - 已接受的真实 Firebase 好友如果开启同步，会在区块中展示 TA 的每日牌摘要。
  - 摘要卡片只展示牌名、正逆位、标题、摘要和微行动，不展示完整解读。
  - 点击摘要卡片会进入 `DialogUI`，自动 `@` 该好友，并把 TA 今天同步的每日牌摘要注入 AI 好友上下文。
- Firestore 权限：
  - 完整每日牌记录 `users/{uid}/daily_oracles/{date}` 仅本人可读写。
  - 摘要记录 `daily_oracle_summaries/{uid}_{date}` 仅本人或已接受好友可读。
  - 摘要必须满足 `summaryOnly == true`、`syncEnabled == true`，并且 `visibility` 为 `all_friends` 或 `real_friends`。
  - 好友读取通过 `users/{ownerUid}/friends/{viewerUid}.status == "friend"` 校验。

### 当前已实现的好友系统

实现时间：2026-06-19

#### Firestore 数据结构

真实好友与好友请求：

```text
users/{uid}/friends/{friendUid}
  uid
  displayName
  email
  photoUrl
  status: pendingSent | friend
  source
  createdAt
  updatedAt

users/{uid}/friend_requests/{requesterUid}
  uid
  displayName
  email
  photoUrl
  status: pendingReceived
  source
  createdAt
  updatedAt
```

屏蔽列表：

```text
users/{uid}/blocked_users/{blockedUid}
  uid
  displayName
  email
  photoUrl
  source
  createdAt
  updatedAt
```

虚拟好友档案：

```text
users/{uid}/virtual_friends/{virtualFriendId}
  virtualFriendId
  name
  relationship
  birthday
  birthTime
  city
  notes
  avatarKey
  avatarUrl
  avatarStoragePath
  isDeleted
  createdAt
  updatedAt
```

用户搜索依赖字段：

```text
public_profiles/{uid}
  uid
  displayName
  displayNameLower
  email
  emailLower
  photoUrl
  facebookProviderId
  searchKeywords: []
```

每日占卜同步设置：

```text
users/{uid}/settings/daily_divination_sync
  enabled
  visibility: all_friends | real_friends | only_me
  summaryOnly: true
  updatedAt
```

每日占卜私有完整记录：

```text
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
  syncEnabled
  visibility
  summaryOnly: false
```

好友动态可读摘要：

```text
daily_oracle_summaries/{uid}_{yyyy-MM-dd}
  ownerUid
  date
  cardId
  cardName
  orientation
  title
  oracle
  microAction
  syncEnabled
  visibility
  summaryOnly: true
```

#### 真实好友搜索与请求

- 已支持通过 Firebase `public_profiles` 集合搜索已注册用户。
- `public_profiles` 是公开搜索索引：允许读取，只有本人可以创建或更新自己的公开资料。
- 搜索方式：按 `displayNameLower` 做前缀匹配，例如输入“土豆”会返回用户名以“土豆”开头的用户。
- 同时会写入 `searchKeywords` 数组，搜索时会再用 `array-contains` 兜底匹配昵称、邮箱、本地段、前缀和部分连续字符；例如新写入过公开资料的用户，可以支持更宽松的关键词匹配。
- 如果前缀搜索和 `searchKeywords` 都不足，会再读取一批公开资料做本地包含匹配，用于兼容旧 `public_profiles` 文档没有关键词数组的情况。
- 搜索会多拉取一批候选，再过滤/压缩成当前 UI 需要展示的结果，避免前几条包含自己时挤掉可添加用户。
- Unity Editor 下如果 Firebase Firestore SDK 直连超时，会通过 Firestore REST API 和本地代理端口做搜索兜底；REST 路径同样包含前缀、关键词数组和公开资料扫描三段查询。
- 搜索入口：`UserSearchUI`。
- 搜索结果优先使用 `SuperScrollView.LoopListView2` 渲染，旧的 3 个结果位作为 prefab 兜底。
- 点击邀请后会发送好友请求。
- 发送请求会同时写入：
  - `users/{我的uid}/friends/{对方uid}`，状态为 `pendingSent`。
  - `users/{对方uid}/friend_requests/{我的uid}`，状态为 `pendingReceived`。
- 当前用户好友页不会把 `pendingSent` 显示到“已有好友”；只有对方接受后变成 `friend` 才进入真实好友列表。
- 打开搜索页会拉取当前用户已发送的 `pendingSent` 请求。
- 已发送请求的搜索结果按钮会显示“取消”，点击会删除：
  - `users/{我的uid}/friends/{对方uid}`。
  - `users/{对方uid}/friend_requests/{我的uid}`。
- 已经成为真实好友的搜索结果按钮会显示“已添加”，不能重复发送请求。
- 搜索页会读取 `users/{uid}/blocked_users`。
- 已屏蔽用户如果出现在搜索结果中，按钮显示“解除”，点击会删除屏蔽记录，之后可重新添加好友。

#### 收到好友请求与接受

- 打开好友页时会拉取：
  - `users/{uid}/friends`
  - `users/{uid}/friend_requests`
  - `users/{uid}/virtual_friends`
- 收到的好友请求会进入现有邀请区域。
- 当前 `InviteItem` 会运行时生成“接受 / 拒绝”两个按钮。
- 接受请求后会：
  - 写入 `users/{我}/friends/{对方}`，状态为 `friend`。
  - 写入 `users/{对方}/friends/{我}`，状态为 `friend`。
  - 删除 `users/{我}/friend_requests/{对方}`。
  - 将对方合并进本地真实好友缓存。
  - 从本地邀请缓存移除该请求。
- 拒绝请求会删除 `users/{我}/friend_requests/{对方}`，并从本地邀请缓存移除该请求。

#### 虚拟好友云端同步

- 创建虚拟好友时会生成 `virtualFriendId`。
- 在 `CreateFriendUI` 填写资料后，会先进入 `ConfirmFriendInfoUI` 确认页。
- 确认页展示头像、姓名、生日、出生时间、出生城市。
- 点击确认页的“创建好友”后，才会写入本地缓存并调用 Firestore 同步。
- 确认页支持返回编辑，也支持从姓名、生日、出生时间、出生城市入口回到对应编辑项。
- 创建完成后会显示 `CreateFriendSuccessUI` 成功页，展示好友头像和姓名。
- 成功页支持进入对话并自动关联该好友，也支持返回好友列表。
- Firebase 路径为 `users/{uid}/virtual_friends/{virtualFriendId}`。
- 打开好友页时会从 Firebase 拉取虚拟好友并合并到本地缓存。
- 虚拟好友头像会上传到 Firebase Storage：
  - 路径：`avatars/{uid}/virtual_{virtualFriendId}_512.jpg`。
  - Firestore 保存 `avatarUrl` 与 `avatarStoragePath`。
  - 好友列表、创建好友主页、编辑好友页会在本地无 Sprite 时通过 `avatarUrl` 下载头像兜底。
- 删除虚拟好友会对 `users/{uid}/virtual_friends/{virtualFriendId}` 写入 `isDeleted: true`，本地缓存立即移除。

#### 本地缓存

- 真实好友、虚拟好友、好友请求都会缓存到本地 `PlayerPrefs`。
- 缓存 Key：`Fari_FriendData_v1`。
- 好友页会先显示本地缓存，再拉取 Firebase 数据刷新。
- 本地缓存用于离线兜底和快速展示，不作为真实好友关系的最终来源。
- “已有好友”只展示 Firestore 中 `status == friend` 的真实好友。
- `pendingSent`（已发送好友请求）不会进入“已有好友”列表；发送请求后只保留云端待确认记录。
- 删除真实好友会删除双方 `friends` 记录，本地缓存同步移除。
- 屏蔽列表也会缓存到本地，用于搜索页和好友页快速过滤/展示。

#### 删除与屏蔽

- 好友列表删除按钮会弹出运行时二次确认框。
- 删除真实好友会删除双方 `friends` 记录，本地缓存同步移除。
- 删除虚拟好友会写入 `isDeleted: true` 并从本地缓存移除。
- 真实好友资料页右上“更多”会打开运行时操作菜单：
  - 删除好友：二次确认后删除双方好友关系。
  - 屏蔽好友：二次确认后写入 `users/{uid}/blocked_users/{friendUid}`，同时清理双方好友关系与待处理请求。
- 被屏蔽用户不会作为已有好友展示；在搜索页重新出现时可点击“解除”恢复可添加状态。

#### 对话联动

- 好友列表中的 `@` 按钮会先进入 `JumpToDialogUI`。
- `JumpToDialogUI` 会展示当前好友头像、姓名、`@好友` 标签和关系提示。
- 支持输入自定义问题，也支持选择灵感问题。
- 点击“进入对话”会调用 `DialogUI.SendAtFriendsMessage(...)`，将好友上下文带入对话。
- 如果填写了问题，会继续调用 `DialogUI.SendMessageFromExternal(...)` 发送该问题并触发 AI 对话。
- 移除已选好友会返回好友列表。
- 从 `@` 好友入口进入 `DialogUI` 时，会显示输入区的 `@好友` 标签框。
- 普通入口进入 `DialogUI` 时会隐藏 `@好友` 标签框，并清空好友上下文，避免沿用上一次 `@` 的好友。
- 点击 `@好友` 标签框里的 `x` 会取消当前 `@` 好友，并清空 `activeRelationshipId` 与 `activeFriendContext`，后续消息不再带入该好友上下文。
- `@好友` 标签框会按好友名动态扩展宽度，并同步调整输入框文字区域的左侧留白，避免长名字与输入文字或取消按钮重叠。
- 从好友每日牌动态卡进入对话时，`@好友` 上下文会额外包含该好友公开同步的今日每日牌摘要。
- `DialogSystem` 会为当前 `@` 好友生成稳定关系 ID：
  - 虚拟好友优先使用 `virtual:{virtualFriendId}`。
  - 真实好友优先使用 `firebase:{firebaseUid}`。
  - 没有云端 ID 时才使用 handle/name/local id 兜底。
- UI 卡片继续展示简洁好友资料；AI 请求会额外携带一份更完整的好友上下文包。
- AI 好友上下文包包含：
  - 姓名。
  - Firebase UID 或虚拟好友 ID。
  - 关系。
  - 生日。
  - 出生时间。
  - 城市。
  - notes 背景信息。
  - 与该好友匹配的长期关系记忆。
  - 与该好友匹配的候选记忆。
  - 当前对话中与该好友相关的历史聊天片段。
  - 与该好友关系 ID 匹配的占卜连续性记录。
- OracleRuntime 请求会通过 `ChatPayload.friendContext` 接收这份上下文包。
- 非 OracleRuntime 的旧 AI 请求路径也会追加 system 级好友上下文，保证关闭 OracleRuntime 时仍能围绕 `@` 好友推理。

#### 真实好友资料页

- 好友列表中的 `view` 按钮会根据好友类型分流。
- 真实 Firebase 好友会打开 `FriendProfileUI`。
- 创建的虚拟好友不会打开 `FriendProfileUI`，会打开专用的 `CreateFriendInfoUI`。
- 待确认好友请求不会进入已有好友列表，也不会打开好友资料页。
- `FriendItem` 会自动兜底查找 prefab 内的 `viewBtn`，不依赖手动拖引用。
- 点击真实好友 `view` 会打开 `FriendProfileUI`：
  - 使用本地好友缓存先填充头像、名称、真实好友关系状态和基础信息。
  - 再读取 `public_profiles/{friendUid}` 覆盖公开昵称、邮箱 handle 和头像 URL。
  - 会下载公开头像并赋值到资料页头像区域。
  - 读取好友今天公开同步的每日牌摘要。
  - 如果好友未开启同步或今天没有公开摘要，则显示暂无公开同步记录。
  - 点击公开摘要记录会进入 `DialogUI`，自动 `@` 该好友，并把该摘要作为好友上下文传给 AI。
  - 点击“全部记录 / 查看更多”会读取最近 30 天可见的好友每日牌摘要，并打开运行时历史记录弹窗。
  - 页面底部动态同步开关复用当前用户的每日占卜同步设置，可直接开关并同步到 Firestore。
  - 右上“更多”按钮支持删除真实好友、屏蔽真实好友。

#### 创建好友主页与编辑页

- 创建的虚拟好友在好友列表中也会显示 `view` 按钮。
- 点击虚拟好友 `view` 会打开 `CreateFriendInfoUI`：
  - 填充头像、好友名称、签名/备注、生日、出生时间、出生城市、用户名称。
  - 占卜历史记录使用 `SuperScrollView.LoopListView2` 渲染。
  - 历史记录优先读取 `users/{uid}/daily_oracles` 的近期每日牌记录。
  - 如果云端未就绪，会用当前 `DailyOracleService` 缓存的今日牌作为兜底。
  - “查看更多”入口会打开运行时占卜历史弹窗，展示最近 30 条每日牌记录。
  - 底部“自动将每日占卜同步到动态”开关复用当前用户每日占卜同步设置，并会保存到 Firestore。
- 点击 `CreateFriendInfoUI` 的编辑资料会打开 `EditFriendUI`：
  - 带入当前虚拟好友头像、姓名、签名、生日、出生时间、出生城市。
  - 头像编辑复用 `SelectFriendAvatarUI`。
  - 生日、出生时间、出生城市编辑复用 `SpinPickerUI`。
  - 保存后更新本地 `FriendDataManager` 缓存，并调用 `FirestoreManager.SaveVirtualFriend` 同步到 `users/{uid}/virtual_friends/{virtualFriendId}`。
  - 如果头像是本地选择的图片，会先上传 Firebase Storage，再把 `avatarUrl` 和 `avatarStoragePath` 写入虚拟好友文档。
  - 保存成功后会回调刷新 `CreateFriendInfoUI` 当前显示内容。
  - “查看更多”入口同样会打开运行时占卜历史弹窗。

#### 通讯录、短信与 Facebook 邀请

- 添加好友页和注册后找朋友页中的通讯录入口已接 `NativeContactInviteManager`：
  - Android 真机会申请 `READ_CONTACTS` 权限，读取系统通讯录手机号，并显示运行时联系人邀请列表。
  - iOS 真机会弹出系统 `CNContactPickerViewController` 联系人选择器，选择联系人后打开短信邀请。
  - Editor、无权限、未读取到联系人时，会自动降级为短信邀请/复制邀请文案。
- 通讯录邀请不会把联系人写成本地真实好友；真实好友关系仍必须通过 Firebase 用户搜索、发送请求、对方接受后产生。
- 点击通讯录联系人或邀请按钮会打开短信应用，并带入邀请文案，引导对方进入 App 后通过 Firebase 用户搜索添加。
- Facebook 邀请页会先尝试真实好友发现：
  - Facebook 登录权限包含 `user_friends`。
  - 已登录且有 token 时，通过 Graph API `/me/friends` 获取同样授权本应用的 Facebook 好友。
  - 使用 `public_profiles.facebookProviderId` 映射到 Firebase 注册用户。
  - 找到应用内用户后显示运行时列表，可直接发送 Firebase 好友请求。
  - 没有 SDK、没有 token、没有权限、没有可映射用户时，会降级为系统分享/复制邀请文案。
- 注册后的找朋友页已经接通：
  - 查找用户：打开 `UserSearchUI`。
  - 通讯录邀请：Android/iOS 走原生通讯录选择，失败时打开短信邀请/复制邀请文案。
  - 分享链接：复制/系统分享邀请文案。
  - 跳过/完成：进入主导航。
- 真实好友关系仍只通过 Firebase 搜索、发送请求、对方接受后产生。

#### 好友相关通知入口

- 好友流程中的顶部通知按钮已统一打开 `NotionUI`。
- 已接入口包括：
  - 添加好友入口 `AddFriendUI`。
  - 用户搜索页 `UserSearchUI`。
  - 创建好友页 `CreateFriendUI`。
  - 创建好友成功页 `CreateFriendSuccessUI`。
  - 通讯录邀请页 `ContactsInviteUI`。
  - Facebook 邀请页 `FacebookInviteUI`。
- 通知页本身继续负责每日提醒、好友邀请、关系占卜状态、系统消息等设置展示与同步。
- `NotificationSettingsManager` 保存或加载设置后会调用 `AppNotificationScheduler` 重新排程：
  - 每日神谕提醒：按用户配置时间每日重复。
  - 占卜回访提醒：如果每日提醒在白天，则晚上回访；如果每日提醒在晚上，则次日早晨回访。
  - 活动与系统提醒：每周五晚间重复。
  - 好友互动提醒：好友请求、双人关系占卜邀请、好友每日牌动态读取到未处理内容时触发一次提醒。
- `Packages/manifest.json` 与 `Packages/packages-lock.json` 已加入 `com.unity.mobile.notifications@2.3.2`；包未解析时调度器会安全降级为 PlayerPrefs 记录和 Editor Toast，解析后会尝试调用 Unity Mobile Notifications 统一通知 API。每日提醒会使用 `NotificationDateTimeSchedule + RepeatInterval.Daily`，Android/iOS 都按每天同一时间重复；通知中心初始化会写入 `PresentationOptions`，确保 Android channel 具备弹窗/声音/角标/震动展示能力。
- `AppNotificationScheduler` 已补测试通知与排程快照：`Tools/Moonly/Schedule Test Notification (10s)` 可在 Editor/设备包中安排一条测试通知，`Tools/Moonly/Log Scheduled Notifications` 可输出最近排程的通知 ID、时间、重复模式和 native/fallback 状态。

#### 已实现代码位置

- `Assets/Scripts/Platform/FireBase/FirestoreManager.cs`
  - `SearchUsersByName`
  - `SearchUsersByKeywordArray`
  - `SendFriendRequest`
  - `LoadPendingSentFriendUids`
  - `CancelSentFriendRequest`
  - `LoadFriends`
  - `LoadFriendRequests`
  - `AcceptFriendRequest`
  - `RejectFriendRequest`
  - `RemoveRealFriend`
  - 真实好友删除支持 Firestore 未初始化时先本地移除并写入待同步队列，Firebase ready 或登录同步后会补删双方 `friends` 文档。
  - `BlockRealFriend`
  - `UnblockUser`
  - `LoadBlockedUsers`
  - `SaveVirtualFriend`
  - `LoadVirtualFriends`
  - `DeleteVirtualFriend`
- `Assets/Scripts/Platform/FireBase/DailyOracleFirestore.cs`
  - `LoadRecentFriendSummaries` 读取真实好友最近可见每日牌摘要。
- `Assets/Scripts/Platform/FireBase/RelationshipDivinationFirestore.cs`
  - `relationship_divinations` 双人关系占卜邀请、翻牌状态、取消状态和完成状态同步。
  - 支持真实好友云端邀请与创建好友档案的本地即时关系占卜。
  - 完成后写入个人占卜历史 `users/{uid}/divination_records`。
- `Assets/Scripts/Friend/FriendDataManager.cs`
  - 真实好友、虚拟好友、好友请求的本地缓存与合并。
  - 屏蔽用户列表的本地缓存。
  - `UpdateVirtualFriend` 支持编辑虚拟好友后刷新本地缓存。
  - `SetVirtualFriendCloudAvatar` 支持头像上传后回写本地缓存。
- `Assets/Scripts/Friend/FriendRuntimeUI.cs`
  - 运行时确认弹窗、操作菜单、占卜历史弹窗。
  - 双人关系占卜运行时面板：三张牌展示、隐私可见性、翻牌、取消邀请、进入对话解读。
- `Assets/Scripts/Friend/FriendInviteShareUtility.cs`
  - 构建邀请文案、复制、系统分享、短信邀请、Facebook 邀请兜底。
- `Assets/Scripts/Platform/Facebook/FacebookFriendDiscoveryManager.cs`
  - Facebook Graph `/me/friends` 好友发现。
  - 将 Facebook 好友 ID 映射到 `public_profiles.facebookProviderId`。
  - 运行时展示可添加的 Facebook 应用内好友，并发送 Firebase 好友请求。
- `Assets/Scripts/Platform/Facebook/FacebookUserInfoHelper.cs`
  - 提取 Facebook provider user id。
- `Assets/Scripts/Friend/NativeContactInviteManager.cs`
  - Android 通讯录权限申请、联系人读取、运行时联系人列表、短信邀请。
  - iOS 联系人选择回调接收、短信邀请。
- `Assets/Plugins/iOS/FariNativeContacts.mm`
  - iOS 原生联系人选择器。
- `Assets/Editor/FariIOSContactsPostprocessor.cs`
  - iOS 构建时写入 `NSContactsUsageDescription`。
- `Assets/Plugins/Android/AndroidManifest.xml`
  - 声明 `android.permission.READ_CONTACTS`。
- `Assets/Scripts/Platform/FireBase/AvatarUploadManager.cs`
  - 上传账号头像。
  - 上传虚拟好友头像到 Firebase Storage。
- `Assets/Scripts/Friend/FriendAvatarImageUtility.cs`
  - 选择、持久化、读取本地好友头像。
  - 通过 `avatarUrl` 下载远程好友头像。
- `Assets/Scripts/UI/UserSearchUI.cs`
  - Firebase 用户搜索、邀请、取消已发送请求、已添加状态展示、解除屏蔽。
- `Assets/Scripts/UI/FriendUI.cs`
  - 打开好友页时同步真实好友、好友请求、虚拟好友。
  - 读取并展示收到的双人关系占卜邀请。
- `Assets/Scripts/Friend/FriendItem.cs`
  - `view` 按钮按真实好友/虚拟好友分流到不同资料页。
  - `@` 按钮进入 `JumpToDialogUI`。
  - `oracleBtn` 发起真实好友双人关系占卜，或为创建好友生成本地关系占卜。
  - 运行时生成删除按钮，支持二次确认后删除真实好友或创建的虚拟好友。
- `Assets/Scripts/UI/CreateFriendUI.cs`
  - 创建虚拟好友并同步 Firebase。
- `Assets/Scripts/UI/CreateFriendInfoUI.cs`
  - 创建好友主页、资料赋值、近期占卜历史、同步开关、进入编辑页。
  - 动态生成“关系占卜”按钮，支持创建好友档案的本地关系占卜。
- `Assets/Scripts/UI/EditFriendUI.cs`
  - 创建好友资料编辑、头像选择、日期/时间/城市选择、保存本地和云端。
- `Assets/Scripts/UI/JumpToDialogUI.cs`
  - `@` 好友前的跳转确认页、问题输入与灵感问题选择。
- `Assets/Scripts/UI/DialogUI.cs`
  - 接收 `JumpToDialogUI` 传入的好友和问题，进入对话并发送消息。
- `Assets/Scripts/Dialog/Data/DialogSystem.cs`
  - 生成 `@` 好友上下文包，接入 OracleRuntime 和旧 AI 请求路径。
- `Assets/Scripts/Friend/InviteItem.cs`
  - 接受好友请求。
  - 运行时生成拒绝按钮并接入 `RejectFriendRequest`。
  - 好友请求卡会运行时区分同意/拒绝按钮颜色，补充“好友请求”文案，并防止重复点击造成重复写入。
- `Assets/Scripts/UI/RegisterFindFriendsUI.cs`
  - 注册后找朋友入口的查找、通讯录邀请、分享、跳过/完成。
- `Assets/Scripts/UI/AddFriendUI.cs`
  - 通讯录入口接入 `NativeContactInviteManager`，无权限或无联系人时自动降级到短信邀请。
  - 顶部通知按钮打开 `NotionUI`。
- `Assets/Scripts/UI/ContactsInviteUI.cs`
  - 完整通讯录邀请页的行为改为短信/分享邀请，不再伪造本地真实好友。
  - 顶部通知按钮打开 `NotionUI`。
- `Assets/Scripts/UI/FacebookInviteUI.cs`
  - Facebook 邀请入口先尝试好友发现，再降级分享邀请。
  - 顶部通知按钮打开 `NotionUI`。
- `Assets/Scripts/UI/CreateFriendSuccessUI.cs`
  - 创建好友成功页顶部通知按钮打开 `NotionUI`。

#### 仍待补齐

- 收到好友请求的 UI 已完成运行时交互精修；如果后续有正式高保真设计稿，可再替换 prefab 视觉资源。
- 通讯录完整页面 prefab 当前可通过 `Tools/UI/Rebuild ContactsInviteUI` 生成；Android/iOS 原生通讯录邀请已接入，短信发送结果回调仍受系统短信 App 限制。Facebook 好友发现代码链路已接入，但真实返回数据取决于 Facebook App Review、`user_friends` 权限是否获批，以及好友是否也授权过本应用。
- 已存在的旧 `public_profiles` 文档如果没有 `searchKeywords`，客户端会用公开资料扫描兜底；该用户重新登录或保存资料后，会自动重写自己的 `public_profiles` 并补齐关键词数组。
- `firestore.rules` 已补充取消请求、双向删除、屏蔽列表、每日占卜同步权限，以及 `relationship_divinations` 参与者读写权限；Rules 和 Indexes 已于 2026-06-20 部署到 `fari-app-b2fd2`。基础 Functions、AI/TTS/Webhook 和 readiness 已部署；`submitIapReceipt` 的真实 receipt 校验仍待写入 `APPLE_SHARED_SECRET` 与 `GOOGLE_SERVICE_ACCOUNT_JSON` 后重新部署验证。

## 我的

### 我的首页 / 控制台

- 展示用户头像、昵称、Pro 状态。
- 展示用户签名或简介。
- 展示今日使用额度：
  - 今日占卜，例如 `3/15`。
  - 今日对话，例如 `18/100`。
- 展示最近一次占卜入口，例如“关系牌阵”。
- 提供以下入口：
  - 占卜历史。
  - 记忆管理。
  - 我的个人资料。
  - 账户信息。
  - 通知。
  - 反馈意见。
  - 关注我们。
  - 解锁所有功能。
- 当前实现：
  - `MyUI` 会展示头像、昵称、Free/Pro 状态和用户简介；未填写简介时展示出生信息摘要，资料完整时展示城市、生日和出生时间，未完整时提示补齐出生信息。
  - 会展示今日占卜/牌阵额度、今日对话额度，并根据后端会员状态刷新 Free/Pro 额度文案。
  - 会读取最近一条占卜记录并展示为“最近占卜”；点击最近占卜文本可直达 `DivinationRecordUI` 详情页，没有记录时进入 `HistoryUI`。
  - 首页入口已接占卜历史、个人资料、记忆管理、Pro 解锁、账户信息、通知设置、反馈意见和关注我们。

### 占卜历史

- 支持查看占卜历史列表。
- 历史记录需要展示占卜类型、时间、简要结果或对应牌阵。
- 支持进入占卜记录详情。
- 当前实现：
  - `HistoryUI` 会读取 `DivinationRecordFirestore.LoadAllRecords`，按时间倒序展示占卜类型、问题摘要、卡牌摘要和时间。
  - Firestore 未就绪或用户未登录时会展示本地历史缓存。
  - 点击历史项会进入 `DivinationRecordUI` 详情页。
  - 打开历史页时会校验静态选中记录是否仍属于当前列表，避免跨用户、删除后或无最新记录时打开上一次残留详情。
  - 详情页删除记录需要二次点击确认；在线时删除云端记录，离线或未登录时也会删除本地历史缓存。

### 占卜记录详情

- 展示某次占卜的完整详情。
- 应复用每日占卜详情、三张牌占卜详情或关系占卜详情的展示结构。
- 当前实现：
  - `DivinationRecordUI` 会展示牌阵名、时间、问题、抽到的牌、简短解读、综合判断、行动建议和继续追问话题。
  - 没有传入选中记录时会自动读取最近一条历史；Firestore 未就绪时也会使用本地历史缓存。
  - 没有可展示记录时会显示空状态，不再停留在空白详情页。
  - “继续追问”会恢复 `DivinationEngine` 会话并打开对话页；“保存到日记”在线时同步云端，离线时保留本地历史缓存。

### 个人资料 / 出生信息

- 支持设置或修改个人出生信息。
- 信息包含生日、出生时间、出生城市等与占卜相关的数据。
- 这些数据会影响占卜体验和 AI 上下文。
- 当前实现：
  - `PersonalProfileUI` 支持昵称、个人简介/签名、生日、出生时间、出生城市和头像修改。
  - 简介字段会写入本地 `UserDataManager.ProfileBio` 与云端 `users/{uid}.bio`；当前 prefab 没有固定简介输入框时，页面会运行时在城市输入框下方补一个简介输入框。
  - 保存时会把生日归一化为 `yyyy-MM-dd`，把出生时间归一化为 `HH:mm`；为空可以保存，填写了但格式无法解析时会阻止保存并提示用户。
  - 保存成功后写入本地 `UserDataManager`，并通过 `FirestoreManager.SaveUserData` 同步到云端用户资料和公开资料。
  - 点击头像可走 `AvatarUploadManager` 选择并上传头像，成功后刷新预览。

### 记忆管理

- 支持查看和管理 AI 记忆。
- 需要能展示已记录的用户偏好、关系信息或对话摘要。
- 需要提供删除或清理记忆的能力。
- 当前实现：
  - `MemoryManagementUI` 会展示个人偏好、关系记忆、占卜连续性、候选记忆、明日线索和最近 Prompt 记录。
  - 打开页面时优先读取云端 `memory_source` 并同步到运行时 `DialogSystem`，未初始化 Firestore 时使用本地运行时记忆兜底。
  - “共享全部记忆”开关会控制 AI Prompt / 每日神谕是否读取记忆；关闭时不删除记忆，只向模型传入空记忆源。
  - 支持手动同步最新记忆到云端。
  - 清空全部记忆需要 8 秒内二次点击确认，确认后会清空本地运行时记忆并删除云端记忆源。

### 账户信息

- 展示账户绑定信息。
- 支持删除账户入口。
- 删除账户需要二次确认页。
- 当前实现：
  - `AccountUI` 会展示邮箱、登录类型、用户 ID、注册时间和账号状态。
  - 退出登录会弹出运行时二次确认框，确认后调用 Firebase 登出并清理本地账号字段。
  - 删除账户会弹出运行时二次确认框，确认后删除 Firestore 用户数据、公开资料和 Firebase Auth 账号；未登录 Firebase 时会清理本地账户数据。
  - `FirebaseAuthManager.DeleteUser()` 会优先调用已部署的 `deleteMyAccountData` Cloud Function，由 Admin SDK 分批删除用户子集合、顶层反馈后台镜像、每日牌摘要、关系占卜记录、公开资料和 Firebase Auth 账号；函数不可用时回退到客户端删除流程。
  - 客户端回退清理已改成 Firestore 分批删除，避免历史记录、对话会话、反馈等子集合超过单个 batch 限制时失败；同时会清理顶层每日牌摘要和关系占卜中与该用户相关的记录。

### 登录 / 注册

- 当前实现：
  - Google、Apple、Facebook、游客登录继续走 `FirebaseAuthManager`。
  - Email 登录按钮会运行时打开邮箱登录弹窗，输入邮箱和密码后调用 Firebase `SignInWithEmailAndPasswordAsync`。
  - 同一个弹窗内支持创建邮箱账号，调用 Firebase `CreateUserWithEmailAndPasswordAsync`，可选昵称会写入 Firebase Auth 用户资料。
  - 邮箱登录弹窗内支持发送密码重置邮件，调用 Firebase `SendPasswordResetEmailAsync`。
  - 邮箱登录/注册成功后会同步 `UserDataManager`，并执行 `FirestoreManager.SyncAfterLogin()` 创建或合并 `users/{uid}` 与 `public_profiles/{uid}`。
  - Game Center 登录按钮已接 `FirebaseAuthManager.SignInWithGameCenter()`，iOS/tvOS 真机或模拟器会先走 Apple Game Center 授权，再通过 `GameCenterAuthProvider.GetCredentialAsync()` 登录 Firebase；Unity Editor 和非 Apple 平台会提示当前平台不支持。
  - Game Center 登录成功后会同步 `UserDataManager.LoginType.GameCenter`、Firebase providerId 和 `users/{uid}` / `public_profiles/{uid}` 基础资料。
  - iOS 导出后处理器会自动给 Xcode 工程添加 Game Center、Sign in with Apple、In-App Purchase 能力，并写入通讯录权限说明；仍需在 Apple Developer / App Store Connect / Firebase Console 中启用对应后台配置。

### 通知设置

- 支持通知设置页。
- 已覆盖每日占卜提醒、占卜回访提醒、好友邀请、关系占卜状态、好友每日牌动态、活动/系统消息等通知类型。
- Android 清单已补 `POST_NOTIFICATIONS` 权限，Android 13+ 真机需要用户授权后才能显示系统通知。
- 当前实现：
  - `MyUI` 的通知入口会打开 `NotionUI`。
  - `NotionUI` 支持每日神谕、对话回复、占卜回访、好友互动、活动/系统通知开关，以及每日提醒时间切换；AI 对话回复会在 App 失焦时按“对话回复”开关触发即时提醒。
  - 设置会写入 `NotificationSettingsManager` 本地持久化并触发 `AppNotificationScheduler.SyncFromSettings` 重新排程。
  - 即时通知会标记未读状态，`MyUI` 和好友流程中明确打开 `NotionUI` 的通知按钮会运行时显示未读红点；打开 `NotionUI` 后自动清空未读提示。
  - Firestore 初始化后会读取/保存云端通知设置；页面也会在管理器未预先创建时运行时兜底创建，避免入口空引用。

### 明日线索 / Tomorrow Hook

- 对话牌阵里的“明天再看”会调用 `DivinationEngine.CreateTomorrowHook(...)`。
- `DialogUI` 会把 Hook 写入当前 `MemorySource.tomorrowHooks`，安排一次次日提醒，并通过 `FirestoreManager.SaveTomorrowHook` 保存到：

```text
users/{uid}/tomorrow_hooks/{hookId}
```

- `TodayOracleUI` 显示时会读取 `LoadDueTomorrowHooks`，把今天及更早到期的 pending Hook 合并进 `DialogSystem` 记忆上下文。
- 到期 Hook 会通过 `AppNotificationScheduler.NotifyTomorrowHookCount` 提醒用户回看。

### Oracle Runtime 追踪

- `ContextAssembler` 生成的 `PromptRecord` 已包含 scene、stage、stageReason、responseMode、riskLevel、riskFlags 和 memoryUsed。
- 每条 AI 回复会把以下字段写进 `ChatMessageData`，并随 `DialogHistoryFirestore` 保存到云端对话历史：
  - `oraclePromptId`
  - `oracleScene`
  - `oracleStage`
  - `oracleStageReason`
  - `oracleResponseMode`
  - `oracleRiskLevel`
  - `oracleRiskFlags`
- 这些字段用于后续调试、风险回溯、复盘 AI 回复依据，不影响现有聊天 UI 展示。

### 反馈意见

- 支持反馈意见入口。
- 包含社区反馈页和 Chat 反馈页。
- Chat 反馈页可能用于与客服或反馈机器人对话。
- 当前实现：
  - `FeedbackUI` 支持社区反馈和 Chat 反馈双入口，提交后写入用户反馈记录并镜像到顶层 `feedback/{feedbackId}`。
  - 反馈列表已移除正式环境里的示例假数据；没有真实反馈时展示空状态。
  - 未登录、离线或 Firestore 暂不可用时，反馈会以“待同步”状态写入本地缓存；下次登录并读取云端反馈时会用本地 `feedbackId` 补同步，避免重复生成后台记录。
  - Cloud Functions 提供 `submitFeedback`、`adminFeedbackList`、`adminFeedbackUpdate` 管理接口。
  - `web-admin/feedback-admin.html` 已提供静态反馈后台页面，支持管理员登录、状态筛选、备注、状态更新和 CSV 导出。
  - Firebase Hosting 已配置 `web-admin` 并显式绑定默认站点 `fari-app-b2fd2`；2026-06-20 已部署到 `https://fari-app-b2fd2.web.app/feedback-admin`，HTTP 200 验证通过。

### 关注我们

- 支持关注我们页。
- 当前实现：
  - `FollowusUI` 支持 Instagram、Facebook、X、TikTok、Pinterest 入口。
  - 链接优先读取公开 App 配置 `PublicAppConfig.socialLinks`。
  - 云端某个链接为空时会自动回退到 `SocialLinksConfig.Default`，避免按钮点击后没有反馈。
  - 默认社媒链接只是平台首页占位；客户端会拦截这类占位链接并提示“链接尚未配置”，避免误打开空首页。
- 可能展示社媒链接、社区入口、外部跳转。

### Pro / 解锁所有功能

- 支持已开通 Pro 状态页。
- 支持未开通 Pro 的购买页。
- Pro 与额度系统关联，例如每日占卜次数、对话次数、会员功能解锁。
- 需要在受限功能处能跳转购买页。
- 当前免费限制：
  - 每日牌：Free 用户每天 1 次。
  - 对话消息：Free 用户每天 100 条。
  - 牌阵占卜：Free 用户每天 15 次。
- Pro 状态来自后端会员接口，未登录或接口失败时会安全降级为 Free。
- 商店支付仍依赖 App Store / Google Play 商品配置，以及 Firebase Functions 的 `APPLE_SHARED_SECRET` / `GOOGLE_PACKAGE_NAME` / `GOOGLE_SERVICE_ACCOUNT_JSON`。
- 已新增 `AppReadinessDiagnostics` 运行时诊断，启动、Firebase 初始化和登录成功后会在 Console 输出 Firebase uid、Functions URL（含会员状态、账户删除和 IAP receipt）、Game Center Auth provider 是否可解析、Unity IAP 包解析、`UNITY_PURCHASING` 编译符号、Mobile Notifications API、Android `POST_NOTIFICATIONS` 权限、通知设置与最近排程快照，方便区分代码问题与外部部署/商店/包解析问题。
- Unity Editor 菜单已补 `Tools/Moonly/Log Readiness Report`、`Resolve Required Packages`、`Copy Firebase Deploy Command`、`Copy Full Readiness Command`、`Copy Release Blockers Command`、`Copy Release Blockers Env Command`、`Copy Prepare Release Command`、`Copy Prepare Release Env Command`、`Copy Init Release Env Command`、`Copy Check Release Env Command`、`Copy Android Keystore Check Command`、`Copy Finish Release Env Command`、`Copy iOS Xcode Export Command`、`Copy Android APK Build Command`、`Open Functions Readiness URL`、`Schedule Test Notification (10s)` 和 `Log Scheduled Notifications`。
- Functions 已补 `readinessStatus` 健康检查端点；当前 Firestore Rules/Indexes 和基础 Functions 已部署，`readinessStatus` 在线返回 HTTP 200，可确认 Firestore 可读性，并通过 `secretDiagnostics` 说明健康检查函数是否绑定了可检查的 secrets。`deleteMyAccountData` 已于 2026-06-20 部署并通过未登录 401 smoke 验证。`DASHSCOPE_API_KEY`、`VOLC_TTS_API_KEY`、`GOOGLE_PACKAGE_NAME`、`PAYMENT_WEBHOOK_SECRET` 已存在；`APPLE_SHARED_SECRET`、`GOOGLE_SERVICE_ACCOUNT_JSON` 仍缺失。线上函数列表显示 `submitIapReceipt` 已部署；缺 IAP secrets 时会记录 `pending_configuration`，不会误开 Pro。
- `scripts/deploy-firebase.sh` 会默认部署基础 Functions，并跳过缺少 Secret 的 AI/TTS/IAP/Webhook Functions；部署成功后会自动调用 `readinessStatus`，并打印缺失 secrets、secret diagnostics 与 requiredActions。
- 根目录已新增 `scripts/check-local-readiness.sh`，用于检查 manifest / packages-lock / `WindowConfig.asset` 是否存在重复窗口名或无效 prefab 路径 / Game Center Firebase Auth 接线 / iOS 导出能力后处理器与导出检查脚本 / `UNITY_PURCHASING` 宏 / Android 通知权限 / 通知设置云端同步 / 每日占卜摘要历史权限刷新 / 通知触发点 / Unity registry 版本可用性 / Unity 项目锁 / Functions export / 部署脚本 / 可选 Firebase CLI 登录/项目绑定 / Firebase 网络连通性 / 远端 Secrets / 线上 Functions 列表 / authenticated smoke tests / C# 编译和 Unity IAP 桥接编译。
- 根目录已新增 `scripts/check-release-blockers.sh`，作为发版前总闸门。它不会修改项目或上传密钥，只会检查 Unity 是否仍打开、当前 iOS/Android 构建产物是否缺权限/能力或已经落后于源码、远端 Firebase Secrets 是否已验证、Functions smoke 是否已跑、真实沙盒 IAP receipt 是否完成校验；构建新鲜度覆盖 `Assets/Scripts`、`Assets/GameData`、`Assets/Resources`、`Assets/GamerFrameWork`、插件、Editor、Packages、ProjectSettings 和 Functions，避免 UI prefab / WindowConfig / 框架运行时代码改动后旧包误过。Google Play Games 检查复用 `configure-google-play-games.sh --check`，但当前 Google 登录不依赖 Play Games SDK，所以未配置 App ID 默认只是 warning；只有设置 `REQUIRE_GOOGLE_PLAY_GAMES=1` 时才会作为 blocker。真实 receipt 验证前会先检查 receipt / store / product id 输入，IAP secret 补救命令会按远端实际缺失项生成。只要仍有阻断项，脚本默认返回非 0；临时只看报告时可用 `ALLOW_RELEASE_BLOCKERS=1 ./scripts/check-release-blockers.sh`。
- 根目录已新增 `scripts/prepare-release.sh`，作为发布准备编排入口。默认只运行 release gate；拿到 Apple shared secret、Google Play 服务账号 JSON、真实 sandbox receipt 并关闭 Unity 后，可用 `RUN_IAP_SECRET_SETUP=1 RUN_DEPLOY=1 RUN_BUILDS=1 ./scripts/prepare-release.sh` 串起补 IAP secrets、部署 Functions、重导 iOS、重打 Android 和最终检查。脚本执行前会 dry-run 校验可选的 Google Play Games App ID、IAP secrets 和真实 receipt 输入；`DRY_RUN=1` 不会因 Unity 打开而失败，真实构建时可用 `WAIT_FOR_UNITY_CLOSE=1` 等待 Unity 关闭后继续。`scripts/release.env.example` 提供发布参数模板，真实本地 env 文件由 `.gitignore` 排除。
- 根目录已新增 `scripts/init-release-env.sh`，用于从 `scripts/release.env.example` 初始化本地私密 `scripts/release.env`，自动设置 `600` 权限，并打印后续 `check-release-env.sh`、`check-android-keystore.sh`、`prepare-release.sh` 和 `finish-release.sh` 命令；脚本不会上传、不打印、不提交密钥。
- 根目录已新增 `scripts/check-release-env.sh`，用于在最终续跑前独立校验 `scripts/release.env` 或当前 shell 环境变量；它不上传、不构建、不打印密钥，只检查 Android 签名、Apple/Google IAP secrets、真实 sandbox receipt、IAP store/product 的存在与格式，并会在提供签名密码时调用 Android keystore 预检。
- 根目录已新增 `scripts/check-android-keystore.sh`，用于读取 `ProjectSettings` 里的 keystore 文件和 alias，并通过 Unity Android OpenJDK 的 `keytool` 验证 keystore 密码、alias 存在和 alias 密码是否能读取签名 key；不会修改原 keystore。
- 根目录已新增 `scripts/finish-release.sh`，作为一键最终续跑入口。默认读取 `scripts/release.env`，也支持 `--no-env-file` 直接使用当前 shell 环境变量；真实执行前会先调用 `check-release-env.sh` 校验本地发布材料，校验通过后才会执行 IAP secret 写入、Functions secret bindings 部署、iOS 导出、Android APK 构建和真实 sandbox receipt release gate；可用 `--dry-run` 预演。
- 根目录已新增 `scripts/build-ios-xcode.sh` 和 `scripts/check-ios-export.sh`：
  - `build-ios-xcode.sh` 会在 Unity 关闭时通过 batchmode 导出 iOS Xcode 工程，并立刻调用导出检查；支持 `RELEASE_ENV_FILE=scripts/release.env` 读取本地发布参数。
  - `check-ios-export.sh` 会验证导出的 `Info.plist`、`.entitlements` 和 `project.pbxproj` 中包含通讯录权限说明、Game Center、Sign in with Apple 和 In-App Purchase 能力。
  - 导出检查通过后会写入 `Builds/iOS/.export-stamp`，发布 gate 优先用这个完成戳判断导出是否落后，避免 Unity 退出阶段触碰源文件造成 iOS 旧导出误报。
  - `CHECK_IOS_EXPORT=1 ./scripts/check-local-readiness.sh` 可检查已有导出工程；`CHECK_IOS_BUILD=1 ./scripts/check-local-readiness.sh` 可执行 batchmode 导出和验证。2026-06-20 已重新执行 `CLEAN_IOS_EXPORT=1 ./scripts/build-ios-xcode.sh`，当前 `Builds/iOS` 已通过导出校验且不落后于源码配置。
- 根目录已新增 `scripts/build-android-apk.sh` 和 `scripts/check-android-config.sh`：
  - `check-android-config.sh` 默认检查 Android 源配置：通知权限、通讯录权限、Firebase `google-services.xml`、Google Sign-In bridge、Facebook metadata、包名、SDK 版本、`UNITY_PURCHASING`、Gradle 依赖和仓库。
  - `build-android-apk.sh` 会在 Unity 关闭时通过 batchmode 打 Android APK，并立刻调用 keystore 预检和 APK 检查；支持 `RELEASE_ENV_FILE=scripts/release.env` 读取本地发布参数。当前项目启用了自定义 keystore `user.keystore` / alias `chenhao`，所以脚本会先检查 `ANDROID_KEYSTORE_PASS` 与 `ANDROID_KEYALIAS_PASS` 是否能真的用于签名。
  - Android APK 校验通过后会写入 `Builds/Android/MoonlyApp.apk.build-stamp`，发布 gate 优先用这个完成戳判断 APK 是否落后，避免 Unity 退出阶段触碰源文件造成 Android 旧包误报。
  - `configure-google-play-games.sh` 可在拿到 Google Play Games App ID 后自动写入 `GooglePlayGamesManifest.androidlib/AndroidManifest.xml`，格式为插件要求的 `\u003<APP_ID>`；支持 `DRY_RUN=1` 预演和 `--check` 检查当前 manifest 是否已配置非占位 App ID。
  - `CHECK_ANDROID_APK=1 ./scripts/check-local-readiness.sh` 可检查已有 APK；`CHECK_ANDROID_BUILD=1 ./scripts/check-local-readiness.sh` 可执行 batchmode 构建和验证。
  - 旧的 `Builds/Android/MoonlyApp.apk` 是之前产物，缺少新增通知/通讯录权限且 manifest 为 debuggable；填好 `scripts/release.env` 或设置 `ANDROID_KEYSTORE_PASS` / `ANDROID_KEYALIAS_PASS` 后重新运行 `CLEAN_ANDROID_BUILD=1 ./scripts/build-android-apk.sh` 会按当前源配置重新生成并验证。
- 根目录已新增 `scripts/check-firebase-network.sh`，可单独检查 Firestore REST、Firebase Auth、Secure Token、Firebase Management 和 `readinessStatus` 是否可达，辅助排查 Editor 搜索好友或 Firestore 写入失败。
- 根目录已新增 `scripts/smoke-functions-auth.sh`，可用临时 Firebase 测试用户验证 `publicConfig`、`membershipStatus`、`aiChat`、`ttsSynthesize` 的 authenticated 调用面；2026-06-20 复测时 strict authenticated smoke 全部返回 HTTP 200。
- 根目录已新增 `scripts/smoke-submit-iap-receipt.sh`，可用临时 Firebase 测试用户提交 fake receipt，验证 `submitIapReceipt` authenticated path 返回 `pending_configuration`；2026-06-20 复测 fake receipt 返回 HTTP 202 / `pending_configuration`，会员状态保持 Free，未误开 Pro。
- 根目录已新增 `scripts/setup-firebase-secrets.sh`，从环境变量读取 DashScope、TTS、Apple / Google IAP 和可选 webhook secret，并通过 Firebase CLI 写入 Functions Secrets，不把密钥写进仓库；Google 服务账号支持 `GOOGLE_SERVICE_ACCOUNT_JSON_FILE` 文件输入，并会校验 `type == service_account`、`client_email` 和 `private_key`。脚本也支持 `MOONLY_SECRET_NAMES` / `SET_SECRET_NAMES` 部分写入，比如只补当前缺失的 `APPLE_SHARED_SECRET,GOOGLE_SERVICE_ACCOUNT_JSON`。
- 根目录已新增 `scripts/check-firebase-secrets.sh`，可验证远端 Functions Secrets 是否存在且非空，不输出密钥值；Google 服务账号会额外校验 JSON 格式。当本地 Firebase CLI 账号无法直接读取 Secret Manager 时，脚本会回退读取 `readinessStatus` 暴露的布尔状态，避免把“CLI 无读取权限”误判成已部署函数缺少密钥。
- 根目录已新增 `scripts/deploy-iap-functions.sh`，用于 IAP secrets 全部就绪后一键部署 `readinessStatus` 与 `submitIapReceipt`；`scripts/smoke-submit-iap-receipt.sh` 支持 fake 安全模式和真实 receipt 严格验证模式。
- 根目录已新增 `scripts/check-iap-products.sh`，用于比对 `functions/public-config.example.json`、Functions 默认公开配置、客户端 `IapProductConfig` 默认值、IAP receipt smoke 默认值、IAP 部署示例和 `UnlockProUI` / `UnityIapPurchaseBridge` 接线，确保月度 / 年度 Pro 商品 ID 一致。
- `functions/scripts/set-public-config.js` 已支持 `--dry-run` 写入前验证；会检查社媒链接、IAP 商品 ID、订阅类型、商品 ID 唯一性，完整 readiness 会自动执行 dry-run。`scripts/init-public-config.sh` 可从 `functions/public-config.example.json` 生成本地 `functions/public-config.live.json`，该 live 文件已由 `.gitignore` 排除；发版时可在私密 `scripts/release.env` 中设置 `RUN_PUBLIC_CONFIG_UPDATE=1` 与 `PUBLIC_CONFIG_PATH`，并用 `REQUIRE_REAL_SOCIAL_LINKS=1` 阻止把 Instagram / Facebook / X / TikTok / Pinterest 平台首页占位链接写到线上。
- 根目录已新增 `scripts/resolve-unity-packages.sh`，用于在 Unity 未打开时通过 batchmode 执行 `AppPackageResolverMenu.ResolveRequiredPackagesBatchMode` 自动解析 IAP 和通知包。

## 底部导航与全局入口

XMind 图片中主要出现的底部导航包括：

- 今日神谕。
- 对话。
- 一起玩/朋友。
- 我的。

部分“我的”截图中还出现另一套导航文案：首页、对话、日记、仪式、我的。需要产品侧确认最终导航命名与数量。

全局入口包括：

- 切换神谕师。
- 通知铃铛。
- 添加好友。
- 好友/关系相关入口。

### 前景特效

- 已新增 `OracleForegroundEffects` 运行时覆盖层。
- 当前接入界面：
  - `DialogUI`：对话界面显示轻量光点、烟雾和底部烛光氛围。
  - `TodayOracleUI`：每日神谕界面显示更明显的星点和柔光氛围。
  - `OracleReadingUI`：今日牌结果页显示火焰、光点和烟雾。
  - `CompleteInterpretationUI`：完整解读页延续同一套神谕氛围。
  - `TarorSingleSpreadShuffleUI`：洗牌页显示更克制的火焰和粒子层。
- 特效层使用 `CanvasGroup.blocksRaycasts = false`，不会阻挡按钮、输入框和滚动列表交互。
- 目前为程序化 UI 特效，包含运行时生成的火焰、光晕、星点和烟雾贴图；如果后续有正式蜡烛火焰、烟尘、粒子素材，可以在同一入口替换为美术资源版。

## 待确认点

- 邮箱登录、创建账号、密码重置邮件和 Game Center 登录代码链路已接入；仍需确认是否还要独立的完整注册页设计稿，以及在 iOS/tvOS 真机或模拟器上完成 Game Center 后台配置与登录验证。
- 底部导航命名存在不一致：`今日神谕/对话/一起玩/我的` 与 `首页/对话/日记/仪式/我的` 需要统一。
- 好友关系占卜中是否需要实时 WebSocket，还是轮询即可。
- 每日占卜的每日重置时间：按本地时区、服务器时区，还是用户配置时区。
- Pro 权益明细仍需产品侧最终确认：好友占卜、记忆、历史等是否也纳入限制。
- 真实好友添加是否必须接入 Facebook 和通讯录，还是可分阶段实现。
