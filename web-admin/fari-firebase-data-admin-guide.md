# Fari Firebase 网页数据管理系统手册

> 版本日期：2026-06-23  
> 适用后台：`web-admin/data-admin.html`  
> 适用项目：`fari-app-b2fd2`  
> 文档结构：20 页，每页对应一个管理主题。

<!-- pagebreak -->

## 第 1 页 / 共 20 页：文档范围和使用边界

这份手册说明管理员可以通过网页版后台读取、筛选、新增、修改和删除的 Firebase Firestore 数据。后台面向运营、客服、研发排查和少量数据修复场景，不建议当作批量迁移工具使用。

| 项目 | 内容 |
| --- | --- |
| 后台入口 | `web-admin/data-admin.html` |
| 数据来源 | Firebase Authentication + Firestore + Cloud Functions Admin SDK |
| 可执行操作 | 列集合、查文档、新增、合并更新、覆盖更新、删除、递归删除、导出当前列表 JSON |
| 权限边界 | 只有 Firebase Auth 登录成功且具备管理员标记的账号可以访问后端 CRUD 接口 |
| 主要风险 | 写错 UID、误删集合、修改支付/会员字段、破坏搜索索引、覆盖用户私有记录 |

使用后台前，请先确认当前操作对象是测试用户、投诉工单用户或已明确授权处理的数据。任何涉及支付、会员、私聊、记忆、好友关系的数据，都应先导出当前文档 JSON，再修改。

<!-- pagebreak -->

## 第 2 页 / 共 20 页：后台界面和通用操作

后台页面采用三栏结构：左侧选择数据源和查询条件，中间展示文档列表，右侧编辑当前文档 JSON。所有写操作都会直接影响 Firestore。

| 操作 | 后台做法 |
| --- | --- |
| 读取 | 选择集合，设置排序字段、方向和数量，点击刷新 |
| 精确查找 | 填写文档 ID，后台会读取该集合下的单个文档 |
| 条件查询 | 配置 where 字段、操作符和值，值会尽量按 JSON 解析 |
| 新增 | 点击新增，填写文档 ID 或留空自动生成，再保存 JSON |
| 修改 | 选中文档后编辑 JSON，默认以 merge 方式合并 |
| 覆盖 | 关闭 merge 后保存会覆盖整个文档，慎用 |
| 删除 | 选中文档后删除；如需连带子集合，勾选递归删除 |
| 导出 | 导出当前列表已加载的文档，不代表全库备份 |

推荐流程：先刷新列表，再点选文档，复制路径确认对象，导出当前 JSON，最后再修改或删除。

<!-- pagebreak -->

## 第 3 页 / 共 20 页：管理员登录和权限模型

后台没有默认管理员账号，也没有默认密码。页面中的 `admin@example.com` 只是输入框占位符。登录使用 Firebase Authentication 的邮箱密码账号，密码由 Firebase Auth 管理，不能从项目代码或 Firebase Console 中反查。

| 问题 | 答案 |
| --- | --- |
| 管理员账号是什么 | 需要在 Firebase Authentication 里创建或使用已有邮箱密码账号 |
| 管理员密码是什么 | Firebase 不显示现有密码；忘记时只能重置 |
| 如何授予后台权限 | 给该用户加 custom claim，或在 `users/{uid}` 写管理员字段 |
| Claim 权限 | `admin=true` 或 `role="admin"` |
| Firestore 权限 | `users/{uid}.isAdmin=true` 或 `users/{uid}.role="admin"` |
| 普通用户表现 | 登录成功但调用后台接口会返回 403 |
| 未登录表现 | 调用后台接口会返回 401 |

最简单的授权方式是在 Firebase Console 里找到该 Auth 用户的 UID，然后进入 Firestore 写入 `users/{uid}` 文档字段：

```json
{
  "isAdmin": true,
  "role": "admin"
}
```

管理员离职、外包结束或设备泄露时，应同时禁用 Firebase Auth 用户，并移除 Firestore 管理员字段。

<!-- pagebreak -->

## 第 4 页 / 共 20 页：根集合总览

后台内置了项目代码和规则中已出现的根集合，也支持通过自定义路径读取其他集合。根集合不需要输入用户 UID。

| 集合 | 可获取内容 | 可修改内容 | 风险 |
| --- | --- | --- | --- |
| `users` | 用户资料、会员状态、权限字段 | 资料、会员、管理员标记 | 改错会影响登录后资料和权限 |
| `public_profiles` | 公开昵称、头像、搜索关键字 | 公开资料和搜索索引 | 改错会导致好友搜索异常 |
| `feedback` | 用户反馈镜像 | 状态、备注、分类 | 顶层镜像和用户子集合可能不同步 |
| `app_config` | 公开配置、IAP 商品配置 | 社媒链接、商品 ID、价格展示 | 改错会影响客户端配置 |
| `quick_reading` | 快速占卜配置 | 文案、卡牌或模式配置 | 改错会影响 App 内容读取 |
| `daily_oracle_summaries` | 好友动态摘要 | 可见性、摘要字段 | 可能暴露或隐藏用户动态 |
| `relationship_divinations` | 双人关系占卜 | 状态、参与者、结果字段 | 涉及两位用户关系数据 |
| `iap_receipts` | IAP 收据校验记录 | 审核备注、状态字段 | 直接影响会员排查证据 |
| `usage_limits` | 用量限制 | 计数、周期、限制值 | 改错会导致限额失效 |
| `payment_events` | 支付回调事件 | 状态和排查备注 | 不建议随意改动原始事件 |
| `analytics_events` | 分析事件 | 标签和排查字段 | 不建议修改历史事件事实 |

遇到未内置集合时，可使用“自定义集合路径”，例如 `users/{uid}/memories`。

<!-- pagebreak -->

## 第 5 页 / 共 20 页：`users` 用户资料

`users/{uid}` 是用户核心资料文档，也是后台管理员权限判断、会员状态、资料同步和账号删除的中心。

| 项目 | 内容 |
| --- | --- |
| 路径 | `users/{firebaseUid}` |
| 可获取 | `displayName`、`email`、`photoUrl`、`birthday`、`birthTime`、`city`、`timezone`、`membershipStatus`、`proExpiresAt` |
| 可修改 | 昵称、头像、时区、资料更新时间；会员和权限字段应通过专用动作调整 |
| 后台操作 | 选择 `users`，可搜索、精确查询 UID、手动注册 Firebase Auth 用户，或编辑 JSON 后保存 |
| 风险 | 误改 UID 对应文档会影响该用户登录后的资料、会员判断和搜索同步 |

常见处理场景：

| 场景 | 建议 |
| --- | --- |
| 手动注册用户 | 使用“手动注册用户”，后台会创建 Firebase Auth 账号，并初始化头像、出生资料、`users` / `public_profiles` |
| 授权管理员 | 优先使用 Firebase custom claim；如写 Firestore 管理员字段，只能由后台管理员操作 |
| 会员申诉 | 先核对 `iap_receipts`，再使用“赋予 Pro”动作补偿 |
| 用户资料错误 | 同时检查 `public_profiles/{uid}` 是否需要同步 |
| 删除账号数据 | 优先使用专门删除 Function，不建议手工逐个删 |

不要把用户密码、支付凭证、身份证件、私聊内容写入 `users/{uid}`。

<!-- pagebreak -->

## 第 6 页 / 共 20 页：`public_profiles` 公开资料

`public_profiles/{uid}` 用于好友搜索和公开展示。它只应保存用户愿意公开的轻量资料。

| 项目 | 内容 |
| --- | --- |
| 路径 | `public_profiles/{uid}` |
| 可获取 | `displayName`、`displayNameLower`、`email`、`photoUrl`、`searchKeywords` |
| 可修改 | 昵称、头像、公开简介、搜索关键字 |
| 后台操作 | 选择 `public_profiles`，按 UID 查询或按搜索字段筛选 |
| 风险 | `displayNameLower` 或 `searchKeywords` 写错会导致好友搜索不到 |

搜索异常排查顺序：

| 检查项 | 说明 |
| --- | --- |
| 文档是否存在 | 用户登录并同步后应存在同 UID 文档 |
| `displayNameLower` | 应与昵称的小写/规范化结果一致 |
| `searchKeywords` | 应包含昵称、邮箱本地段、前缀和常用匹配词 |
| 头像 URL | 只写公开头像，不写本地私有路径 |

公开资料不要存储生日、会员、支付、私聊、记忆或管理员备注。

<!-- pagebreak -->

## 第 7 页 / 共 20 页：`feedback` 反馈镜像

用户反馈通常同时存在于 `users/{uid}/feedback/{feedbackId}` 和顶层 `feedback/{feedbackId}`。通用数据后台可以管理顶层镜像；如果要同步用户侧反馈状态，优先使用专门反馈后台。

| 项目 | 内容 |
| --- | --- |
| 路径 | `feedback/{feedbackId}` |
| 可获取 | `content`、`status`、`category`、`tag`、`uid`、`email`、`platform`、`appVersion`、`deviceModel` |
| 可修改 | `status`、`adminNote`、`handledBy`、分类标签、处理时间 |
| 后台操作 | 选择 `feedback`，按状态筛选或按文档 ID 精确查找 |
| 风险 | 只修改顶层镜像不会自动同步 `users/{uid}/feedback` |

建议状态：

| 状态 | 含义 |
| --- | --- |
| `new` | 新反馈，尚未处理 |
| `triaged` | 已分类，等待处理 |
| `in_progress` | 正在处理 |
| `resolved` | 已解决 |
| `closed` | 已关闭，不再跟进 |

反馈内容可能包含用户隐私，导出和转发时请只保留必要字段。

<!-- pagebreak -->

## 第 8 页 / 共 20 页：`app_config` 和 `quick_reading`

`app_config/public` 控制公开配置，`quick_reading/{oracleId}` 控制快速占卜内容。两者都会直接影响客户端展示。

| 集合 | 可获取 | 可修改 | 风险 |
| --- | --- | --- | --- |
| `app_config` | 社媒链接、IAP 商品配置、公开开关 | `socialLinks`、`iapProducts`、价格标签、商品 ID | 商品 ID 错误会影响购买 |
| `quick_reading` | 快速占卜模式、文案、卡牌配置 | oracle 文案、排序、启用状态 | 配置错误会让客户端展示异常 |

操作建议：

| 场景 | 建议 |
| --- | --- |
| 修改社媒链接 | 改完调用 `publicConfig` 或刷新 App 验证 |
| 修改商品 ID | 必须与 App Store、Google Play、Unity IAP 配置一致 |
| 修改快速占卜 | 保留原文档 ID，先复制旧 JSON |
| 下线某项内容 | 优先使用启用字段，不要直接删除 |

这类配置适合小步修改，每次只改一个主题，方便出问题时定位。

<!-- pagebreak -->

## 第 9 页 / 共 20 页：`daily_oracle_summaries` 每日神谕摘要

`daily_oracle_summaries/{uid}_{date}` 是好友动态使用的摘要文档，通常由用户每日神谕记录同步生成。它不应包含完整私密解读。

| 项目 | 内容 |
| --- | --- |
| 路径 | `daily_oracle_summaries/{uid}_{yyyy-MM-dd}` |
| 可获取 | `ownerUid`、`date`、`cardId`、`cardName`、`orientation`、`title`、`oracle`、`visibility` |
| 可修改 | 摘要标题、展示字段、可见性、同步标记 |
| 后台操作 | 选择集合后按 `ownerUid` 或 `date` 筛选 |
| 风险 | 可见性字段错误可能让好友看不到，或让不该看到的人看到 |

关键字段建议：

| 字段 | 说明 |
| --- | --- |
| `summaryOnly` | 应为 `true`，表示只公开摘要 |
| `syncEnabled` | 是否允许同步展示 |
| `visibility` | 例如 `all_friends` 或 `real_friends` |
| `ownerUid` | 摘要所属用户 UID |

不要把完整解读、用户问题、情绪记录等私密内容放到摘要集合。

<!-- pagebreak -->

## 第 10 页 / 共 20 页：`relationship_divinations` 双人关系占卜

`relationship_divinations/{id}` 保存双人关系占卜流程和结果。它涉及两个参与者，修改时需要同时考虑双方体验。

| 项目 | 内容 |
| --- | --- |
| 路径 | `relationship_divinations/{divinationId}` |
| 可获取 | `creatorUid`、`targetUid`、`participants`、`status`、`cards`、`result`、`createdAt` |
| 可修改 | `status`、结果字段、排查备注、异常参与者字段 |
| 后台操作 | 选择集合后按 `creatorUid`、`targetUid` 或 `status` 查询 |
| 风险 | 改错参与者或状态会导致一方看不到结果，或看到错误关系记录 |

状态处理建议：

| 场景 | 建议 |
| --- | --- |
| 用户要求撤回 | 优先设置 `status="cancelled"`，保留审计线索 |
| 结果生成失败 | 标记错误状态并保留原始输入 |
| 参与者异常 | 先核对 `friends` 和 `friend_requests` |
| 误生成记录 | 确认双方 UID 后再删除 |

不要为了“清爽”直接删除争议记录，客服和研发排查通常需要历史证据。

<!-- pagebreak -->

## 第 11 页 / 共 20 页：IAP、会员和支付事件

支付相关数据通常包括 `iap_receipts`、`payment_events` 和 `users/{uid}` 里的会员字段。它们共同支持会员状态判断和客服排查。客服补偿或人工开通时，优先使用用户详情里的“赋予 Pro”动作，让后台函数统一写入会员字段和审计事件。

| 集合 | 可获取 | 可修改 | 风险 |
| --- | --- | --- | --- |
| `iap_receipts` | 收据校验请求、平台、商品、过期时间、校验状态 | 审核备注、排查状态 | 不建议修改原始收据字段 |
| `payment_events` | Webhook 或支付事件原始记录 | 处理状态、备注 | 改动会影响事件追踪 |
| `users/{uid}` | `membershipStatus`、`proExpiresAt`、订阅字段 | 会员补偿、状态修正 | 改错会影响用户权益 |

会员申诉推荐流程：

| 步骤 | 动作 |
| --- | --- |
| 1 | 在 `users/{uid}` 查看当前会员状态 |
| 2 | 在 `iap_receipts` 按 UID 或订单信息查询收据 |
| 3 | 核对平台、商品 ID、过期时间和校验结果 |
| 4 | 必要时只修正会员字段，并写入管理员备注 |
| 5 | 让用户重新打开 App 或触发会员状态刷新 |

不要伪造收据、不要删除支付事件、不要把测试订单当作正式订单补偿。

<!-- pagebreak -->

## 第 12 页 / 共 20 页：`usage_limits` 和 `analytics_events`

`usage_limits` 保存用量限制和计数，`analytics_events` 保存行为分析事件。前者会影响产品可用性，后者主要用于观察和排查。

| 集合 | 可获取 | 可修改 | 风险 |
| --- | --- | --- | --- |
| `usage_limits` | 用户、周期、功能、已用次数、限制值 | 计数、重置时间、限制值 | 改错会导致功能被错误解锁或锁死 |
| `analytics_events` | 事件名、用户、平台、版本、参数、时间 | 标签、备注、排查字段 | 不建议修改历史事实 |

常见场景：

| 场景 | 建议 |
| --- | --- |
| 用户被错误限流 | 先确认周期和功能 key，再重置对应计数 |
| 活动临时放量 | 优先改配置或规则，不要逐个改用户 |
| 分析事件异常 | 保留原始参数，用备注字段标记排查结论 |
| 批量导出 | 导出当前筛选结果后交给分析工具 |

如果不确定某个计数字段的含义，先让研发确认，不要凭字段名直接修改。

<!-- pagebreak -->

## 第 13 页 / 共 20 页：用户子集合 `daily_oracles`

`users/{uid}/daily_oracles/{date}` 保存用户每日神谕的完整记录，属于个人数据。后台读取时必须输入用户 UID。

| 项目 | 内容 |
| --- | --- |
| 路径 | `users/{uid}/daily_oracles/{date}` |
| 可获取 | 抽到的牌、方向、问题、完整解读、情绪字段、创建时间 |
| 可修改 | 卡牌字段、解读字段、同步标记、修复备注 |
| 后台操作 | 选择用户子集合 `daily_oracles`，填写用户 UID，再按日期文档 ID 查询 |
| 风险 | 该集合可能包含用户私密问题和完整解读 |

处理建议：

| 场景 | 建议 |
| --- | --- |
| 当日结果丢失 | 按日期文档 ID 查询，例如 `2026-06-23` |
| 好友动态异常 | 同时检查顶层 `daily_oracle_summaries` |
| 解读内容投诉 | 导出单条 JSON 交研发排查，避免扩散全量记录 |
| 用户要求删除 | 优先使用账号删除链路或只删指定日期记录 |

该集合不适合客服长期浏览，只在明确用户请求或故障排查时查看。

<!-- pagebreak -->

## 第 14 页 / 共 20 页：用户子集合 `divination_records`

`users/{uid}/divination_records/{recordId}` 保存个人占卜历史，通常比摘要更完整。

| 项目 | 内容 |
| --- | --- |
| 路径 | `users/{uid}/divination_records/{recordId}` |
| 可获取 | 占卜类型、问题、卡牌、结果、角色、创建时间 |
| 可修改 | 标题、标签、结果字段、异常状态 |
| 后台操作 | 输入用户 UID 后选择文档，或按 `createdAt` 排序查看最近记录 |
| 风险 | 包含高度个人化问题和答案，导出需谨慎 |

常见处理场景：

| 场景 | 建议 |
| --- | --- |
| 历史记录缺失 | 先确认用户 UID 是否正确，再按时间倒序读取 |
| 单条内容异常 | 只修改该条文档，不要批量覆盖集合 |
| 用户要求删除单条 | 删除指定 `recordId`，并记录客服工单 |
| 需要复现问题 | 复制必要字段给研发，不要转发整段隐私内容 |

如果文档结构由客户端版本变化导致不同，保留旧字段，不要强行统一为新格式。

<!-- pagebreak -->

## 第 15 页 / 共 20 页：用户子集合 `dialog_sessions`

`users/{uid}/dialog_sessions/{sessionId}` 保存对话会话和上下文状态。它可能包含用户输入、AI 回复和上下文摘要。

| 项目 | 内容 |
| --- | --- |
| 路径 | `users/{uid}/dialog_sessions/{sessionId}` |
| 可获取 | 会话标题、消息摘要、角色、最近更新时间、上下文信息 |
| 可修改 | 标题、归档状态、错误状态、排查备注 |
| 后台操作 | 输入用户 UID，按 `updatedAt` 倒序查看最近会话 |
| 风险 | 对话内容敏感，可能包含情感、关系、身份等隐私 |

处理建议：

| 场景 | 建议 |
| --- | --- |
| 会话打不开 | 检查状态字段、消息数组或子结构是否损坏 |
| 用户投诉 AI 内容 | 导出相关会话，脱敏后交模型/安全排查 |
| 需要隐藏会话 | 优先设置归档或隐藏字段，不直接删历史 |
| 用户要求删除 | 删除指定会话，确认是否还有关联记忆 |

不要把完整会话内容复制到公开工单或聊天群中。

<!-- pagebreak -->

## 第 16 页 / 共 20 页：用户子集合 `memories` 和 `tomorrow_hooks`

`memories` 保存长期记忆或用户偏好，`tomorrow_hooks` 保存后续提醒、明日回访或连续体验的触发点。

| 集合 | 可获取 | 可修改 | 风险 |
| --- | --- | --- | --- |
| `users/{uid}/memories` | 记忆内容、来源、权重、更新时间 | 记忆内容、启用状态、删除标记 | 错误记忆会持续影响 AI 回复 |
| `users/{uid}/tomorrow_hooks` | hook 文案、触发日期、状态 | 状态、日期、内容、取消标记 | 改错会造成错误提醒或体验断裂 |

操作建议：

| 场景 | 建议 |
| --- | --- |
| AI 反复提到错误信息 | 查询并停用或删除相关 memory |
| 用户要求遗忘 | 删除或标记对应 memory，不保留敏感描述 |
| 明日提醒异常 | 检查 `triggerDate`、`status` 和时区 |
| 批量关闭体验 | 使用状态字段关闭，不要直接清空集合 |

这些集合不应保存支付、身份凭据、管理员备注或客服内部判断。

<!-- pagebreak -->

## 第 17 页 / 共 20 页：好友关系子集合

好友关系由 `friends`、`friend_requests`、`blocked_users` 共同描述。它们通常需要双向一致，手工修复时要非常谨慎。

| 集合 | 路径 | 可获取 | 可修改 |
| --- | --- | --- | --- |
| `friends` | `users/{uid}/friends/{friendUid}` | 好友 UID、状态、昵称快照、来源、时间 | `status`、备注、快照字段 |
| `friend_requests` | `users/{uid}/friend_requests/{requestId}` | 请求方、接收方、状态、消息、时间 | `status`、处理时间 |
| `blocked_users` | `users/{uid}/blocked_users/{blockedUid}` | 被拉黑 UID、时间、原因 | 拉黑状态、备注 |

常见修复：

| 问题 | 建议 |
| --- | --- |
| A 有 B，B 没有 A | 同时检查双方 `friends` 文档 |
| 请求卡住 | 查看双方 `friend_requests` 状态是否一致 |
| 搜不到好友 | 先查 `public_profiles`，再查好友文档 |
| 误拉黑 | 确认用户授权后删除或改正 `blocked_users` |

好友关系是社交体验核心，修改前请复制双方相关文档路径。

<!-- pagebreak -->

## 第 18 页 / 共 20 页：`virtual_friends`、`settings`、用户反馈

这些子集合偏用户体验配置：虚拟好友、个人设置和用户侧反馈记录。

| 集合 | 路径 | 可获取 | 可修改 | 风险 |
| --- | --- | --- | --- | --- |
| `virtual_friends` | `users/{uid}/virtual_friends/{id}` | 虚拟好友资料、关系设定、状态 | 昵称、设定、状态 | 改错会影响陪伴体验 |
| `settings` | `users/{uid}/settings/{settingId}` | 通知、隐私、偏好配置 | 开关、语言、推送偏好 | 改错会影响用户设置 |
| `feedback` | `users/{uid}/feedback/{feedbackId}` | 用户侧反馈原始记录 | 状态、备注、同步字段 | 顶层反馈镜像可能不同步 |

操作建议：

| 场景 | 建议 |
| --- | --- |
| 虚拟好友资料异常 | 修改单个虚拟好友文档，不影响其他好友 |
| 用户设置不生效 | 检查 settings 文档 ID 和客户端读取路径是否一致 |
| 用户反馈状态不同步 | 同时核对顶层 `feedback/{feedbackId}` |
| 隐私相关修改 | 优先让用户在 App 内自行改，后台只做修复 |

如果客服只需要查看反馈状态，优先使用专门反馈后台；通用后台适合研发排查。

<!-- pagebreak -->

## 第 19 页 / 共 20 页：JSON 编辑格式和特殊 Firestore 类型

右侧编辑器使用 JSON。普通字符串、数字、布尔值、对象和数组可以直接写。Firestore 特殊类型需要使用约定格式。

| 类型 | 写法 |
| --- | --- |
| 时间戳 | `{ "__type": "timestamp", "value": "2026-06-23T12:00:00.000Z" }` |
| 服务端时间 | `{ "__type": "serverTimestamp" }` |
| 删除字段 | `{ "__type": "deleteField" }` |
| 地理坐标 | `{ "__type": "geoPoint", "latitude": 31.2304, "longitude": 121.4737 }` |
| 文档引用 | `{ "__type": "reference", "path": "users/USER_UID" }` |
| Bytes | `{ "__type": "bytes", "base64": "..." }` |

示例：给用户添加管理员权限并记录更新时间。

```json
{
  "isAdmin": true,
  "role": "admin",
  "adminUpdatedAt": { "__type": "serverTimestamp" }
}
```

示例：删除一个错误字段。

```json
{
  "legacyBadField": { "__type": "deleteField" }
}
```

保存前请确认 merge 开关：merge 开启时只合并字段；merge 关闭时会覆盖整个文档。

<!-- pagebreak -->

## 第 20 页 / 共 20 页：上线、部署和验收清单

功能完成后，需要同时部署 Cloud Functions 和 Hosting，线上后台才会完整可用。仅本地打开 HTML 可以看到页面，但线上 CRUD 依赖已部署的 Functions。

| 验收项 | 通过标准 |
| --- | --- |
| 页面加载 | `data-admin.html` 打开无控制台错误 |
| 登录验证 | Firebase Auth 邮箱密码可以登录 |
| 权限验证 | 普通用户调用后台接口返回 403，管理员用户可以列集合 |
| 读取验证 | 能读取 `users` 或测试集合的文档列表 |
| 新增验证 | 能在测试集合新增一条文档 |
| 修改验证 | 能修改测试文档字段，merge 行为符合预期 |
| 删除验证 | 能删除测试文档，递归删除只在确认后使用 |
| 文档验证 | 本 Markdown 包含 20 个页码章节，后台“管理手册”链接指向该文件 |

推荐部署命令：

```bash
firebase deploy --only functions:adminDashboardSummary,functions:adminConfigOverview,functions:adminUserSearch,functions:adminUserDetail,functions:adminMembershipOverview,functions:adminCreateRegisteredUser,functions:adminGrantPro,functions:adminRevokePro,functions:adminFeedbackList,functions:adminFeedbackUpdate,functions:adminDataCollections,functions:adminDataList,functions:adminDataUpsert,functions:adminDataDelete,firestore:rules,hosting --project fari-app-b2fd2
```

部署后建议用一个非管理员账号和一个管理员账号各测一次。非管理员只能证明权限拦截有效，管理员账号才能验证读取、修改和删除链路。
