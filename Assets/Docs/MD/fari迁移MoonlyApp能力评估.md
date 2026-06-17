# Fari → MoonlyApp 迁移能力评估文档

> 源项目：`/Users/kittenhao/Desktop/fari-code-20260608-v2`（Vanilla JS SPA + Node.js 后端）  
> 目标项目：`/Users/kittenhao/Unity/UnityDemo/MoonlyApp`（Unity + GamerFrameWork + Firebase）  
> 评估日期：2026-06-15

---

## 一、项目现状对比

| 功能模块 | fari-code（源） | MoonlyApp（目标） | 迁移难度 |
|----------|----------------|-------------------|:---:|
| **用户认证** | 自定义 Token + JSON 存储 | Firebase Auth（Google/Apple/Facebook/匿名） | 🟢 已超越 |
| **后端数据库** | 本地 JSON 文件 | Firestore NoSQL | 🟢 已超越 |
| **AI 对话** | DeepSeek/Ark + SSE 流式 | DeepSeek（硬编码 key，非流式） | 🟡 部分完成 |
| **聊天 UI** | innerHTML 渲染 | LoopListView2 虚拟滚动 | 🟢 已超越 |
| **三神谕师** | 塔罗/占星/冥想 | 塔罗/占星/冥想（含 ScriptableObject 配置） | 🟢 已对齐 |
| **每日神谕** | 每日单卡翻牌+动画 | DailyCardBox + TodayOracleUI | 🟡 需增强 |
| **快速占卜** | 话题Tab+问题列表 | QuickDivinationPanel + QuickDivinationData | 🟢 已对齐 |
| **三牌占卜** | 完整流程（prepare→start→reveal→result） | 未实现 | 🔴 需新建 |
| **好友关系** | 搜索/邀请/创建/合盘占卜 | 部分 UI 已有，逻辑未实现 | 🔴 需新建 |
| **会员系统** | 3套餐+付费墙+功能限制 | 未实现 | 🔴 需新建 |
| **TTS 语音** | 火山引擎 TTS，流式合成+缓存 | 框架就绪，stub | 🟡 需接入 |
| **注册引导** | 多步引导流程 | 已有多步 UI（SetBirthDate/SetName/ChooseGuide 等） | 🟢 已对齐 |
| **Oracle Runtime 引擎** | 37模块（场景规划/风险分类/记忆/疗法等） | 仅简单系统提示词 | 🔴 需新建 |
| **塔罗牌数据** | 78张 JSON 定义（名称/关键词/正逆位） | 仅78张图片，无结构化数据 | 🔴 需新建 |
| **牌阵模板** | 5种预设牌阵 JSON | 无 | 🔴 需新建 |
| **历史记录** | 对话历史+占卜历史 | DivinationRecordUI 存在 | 🟡 需增强 |
| **"我的"页面** | 个人中心+设置+记忆管理 | MyUI + MemoryManagementUI 存在 | 🟢 基本对齐 |
| **推送/提醒** | Tomorrow Hook 系统 | NotificationSettingsManager 存在 | 🟡 需增强 |
| **前景特效** | CSS 蜡烛火焰/粒子/烟尘 | 无 | 🔴 需新建 |

---

## 二、我能做的事（AI 可直接完成）

### 2.1 代码编写（100% 可完成）

| 类别 | 具体工作 |
|------|---------|
| **C# 脚本** | 所有业务逻辑类、数据模型、Manager、Panel、Item 脚本 |
| **UI 预制体** | 用 UGUIBuilder 工具生成 Prefab（已在项目中验证过） |
| **Firestore 数据模型** | 设计并编写 Firestore 的 Collection/Document 结构和 CRUD 封装 |
| **EventSystem 集成** | 用刚修好的 EventSystem 做模块间解耦通信 |
| **AI Prompt 工程** | 迁移 fari 的 prompts/ 目录为 Unity C# 字符串资源 |
| **塔罗牌数据** | 将 data/tarot_deck.json 转为 ScriptableObject 或 JSON 资源 |
| **牌阵模板** | 将 data/spread_templates.json 转为 Unity 可用格式 |
| **Oracle Runtime 迁移** | 将 src/oracle-runtime/ 37个模块的逻辑翻译为 C# |
| **API 层封装** | 封装 DeepSeek API 调用（支持流式）、TTS API 调用 |

### 2.2 配置/资源产出

| 类别 | 具体工作 |
|------|---------|
| **MD 技术文档** | 架构设计、模块说明、Firestore 安全规则、部署指南 |
| **Firestore 安全规则** | 编写 firestore.rules |
| **ScriptableObject 资产** | 塔罗牌78张数据、3个神谕师角色增强数据 |
| **JSON 配置文件** | 牌阵模板、话题分类、快速占卜问题库 |

### 2.3 架构设计

- Firestore 数据模型设计（Collection/Document/Subcollection 结构）
- 模块拆分方案（UIModule / GameManager / 数据层职责划分）
- 事件驱动架构（用 EventSystem 泛型方法做类型安全的事件通信）

---

## 三、我做不到的事（需要你来完成）

### 3.1 必须在 Unity Editor 中操作

| 事项 | 原因 |
|------|------|
| **Unity 报错排查** | 编译错误、Prefab 引用丢失、场景绑定断裂都需要在 Editor 里看 Console 解决 |
| **Prefab 实例绑定** | 脚本中 public 字段需要拖拽绑定 GameObject/RectTransform/Button 等，必须在 Editor 操作 |
| **Animator / Animation Clip** | 翻牌动画、过渡动画等需要 Animation 窗口制作 |
| **UI 动效调试** | DOTween 序列的时长、缓动曲线需要运行时微调看效果 |
| **Canvas 层级调试** | Sorting Order、Raycast 遮挡问题需要在 Game 视图交互验证 |
| **图集/资源导入设置** | Sprite 切割、压缩格式、Max Size 等 Inspector 配置 |

### 3.2 平台/原生相关

| 事项 | 原因 |
|------|------|
| **Firebase 项目配置** | 需要在 Firebase Console 启用 Auth Provider、创建 Firestore 数据库、配置索引 |
| **iOS/Android 原生插件调试** | Google Sign-In、Apple Sign-In 的 URL Scheme、plist 配置需要原生层验证 |
| **TTS 音频播放测试** | Unity 的 AudioSource 在真机上表现需要实测 |
| **包体大小优化** | AssetBundle 分包策略需要结合 YooAsset 在真机验证 |
| **App Store / Google Play 上架** | 审核、证书、描述、截图需要你来操作 |

### 3.3 需要外部账号/服务配置

| 事项 | 说明 |
|------|------|
| **DeepSeek API Key** | 已迁移到 Firebase Cloud Functions Secret `DEEPSEEK_API_KEY`，不应放在 Unity 客户端 |
| **火山引擎 TTS Key** | 已预留 Firebase Cloud Functions Secret：`VOLC_TTS_APP_ID`、`VOLC_TTS_ACCESS_TOKEN` |
| **Firebase 项目** | `fari-app-b2fd2` 已存在，如换新项目需重建 |
| **Apple Developer 账号** | Sign in with Apple 需要 Apple Developer 后台配置 |
| **Facebook App** | Facebook 登录需要 Facebook Developer 后台配置 |

### 3.4 设计/美术资源

| 事项 | 说明 |
|------|------|
| **fari 的 UI 切图** | fari 项目用了大量 PNG 切图做皮肤（assets/ui/nav/, assets/pro_oracle/, assets/friends/ 等），需要导入 Unity 并切成 Sprite |
| **新页面 UI 设计** | 三牌占卜、付费墙等新页面需要你来定视觉方案（或用 fari 的设计稿参考） |
| **动画/特效** | 翻牌动画、火焰粒子、过渡动效需要确认是否按 fari 的 CSS 动画效果还原 |

---

## 四、推荐迁移路线（按优先级排序）

### 第一阶段：补齐核心体验（1-2周）

```
P0 - 必须完成：
├── 1. 塔罗牌 ScriptableObject 数据（78张含义/关键词/正逆位）
├── 2. 三牌占卜完整流程（Prepare → Start → Reveal → Result → Detail）
├── 3. DeepSeek API 流式输出（当前非流式，体验差）
├── 4. 会员系统（付费墙 + 功能限制逻辑）
└── 5. Firestore 数据结构迁移（用户/对话/占卜/会员数据）
```

### 第二阶段：体验增强（2-3周）

```
P1 - 提升品质：
├── 6. TTS 语音接入（火山引擎，消息级+每日神谕）
├── 7. Oracle Runtime 引擎迁移（场景识别/风险分类/记忆系统）
├── 8. 每日神谕翻牌动画（牌背→翻开→过渡→结果）
├── 9. 好友关系 + 合盘占卜
└── 10. 注册引导完整流程
```

### 第三阶段：丰富生态（1-2周）

```
P2 - 锦上添花：
├── 11. 前景特效（火焰/粒子/烛光）
├── 12. Tomorrow Hook 推送提醒
├── 13. 对话历史搜索/管理
├── 14. 多语言本地化（I2 已集成）
└── 15. 数据统计/埋点
```

---

## 五、关键架构差异说明

### 5.1 后端架构变化

| 维度 | fari-code | MoonlyApp |
|------|-----------|-----------|
| 服务端 | Node.js 独立服务 | 无需独立服务端 |
| 数据库 | 本地 JSON | Firestore（Firebase 提供） |
| 认证 | 自定义 Token | Firebase Auth |
| LLM 调用 | 服务端代理 → 安全 | 客户端直调（建议加 Cloud Function 代理） |
| TTS | 服务端合成 → 返回音频 | 客户端直调或 Cloud Function |
| 部署 | Ubuntu + nginx + systemd | Unity 打包 APK/IPA，无服务端运维 |

**核心变化：fari 的 Node.js 服务端 40+ API 全部去掉**，改为：
- **Firestore 直读直写**（客户端 SDK）
- **Firebase Auth**（客户端 SDK）
- **LLM API 直调或通过 Cloud Functions 代理**（保护 API Key）
- **TTS 通过 Cloud Functions 代理**（保护 Key）

### 5.2 安全提醒

⚠️ AI / TTS Key 不应打包进客户端。当前方案已改为通过 Firebase Cloud Functions 做 LLM / TTS 代理，并用 Functions Secret 保存密钥。

---

## 六、下一步行动

请回复你希望我从哪个模块开始，我会立即动手。建议先做 **P0-1（塔罗牌数据 ScriptableObject）**，因为三牌占卜、每日神谕都依赖它。
