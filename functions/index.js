"use strict";

const crypto = require("node:crypto");
const fs = require("node:fs");
const path = require("node:path");
const admin = require("firebase-admin");
const { onRequest } = require("firebase-functions/v2/https");
const { onDocumentCreated } = require("firebase-functions/v2/firestore");
const { onSchedule } = require("firebase-functions/v2/scheduler");
const { defineSecret } = require("firebase-functions/params");

admin.initializeApp();

const db = admin.firestore();
const realtimeDb = admin.app().database("https://fari-app-b2fd2-default-rtdb.firebaseio.com");

const ALL_SECRET_NAMES = Object.freeze([
  "DEEPSEEK_API_KEY",
  "VOLC_TTS_API_KEY",
  "PAYMENT_WEBHOOK_SECRET",
  "APPLE_SHARED_SECRET",
  "GOOGLE_PACKAGE_NAME",
  "GOOGLE_SERVICE_ACCOUNT_JSON",
]);
function parseSecretNames(source) {
  return String(source || "")
    .split(/[,\s]+/)
    .map((name) => name.trim())
    .filter(Boolean);
}

function readDeploySecretNames() {
  const names = new Set(parseSecretNames(process.env.MOONLY_DEPLOYED_SECRETS));
  const bindingsPath = path.join(__dirname, ".moonly-secret-bindings.json");

  try {
    if (fs.existsSync(bindingsPath)) {
      const parsed = JSON.parse(fs.readFileSync(bindingsPath, "utf8"));
      const fileNames = Array.isArray(parsed) ? parsed : parsed.secrets;
      if (Array.isArray(fileNames)) {
        for (const name of fileNames) {
          if (typeof name === "string" && name.trim()) {
            names.add(name.trim());
          }
        }
      }
    }
  } catch (error) {
    console.warn("[config] Failed to read .moonly-secret-bindings.json", error);
  }

  return names;
}

const DEPLOY_SECRET_NAMES = readDeploySecretNames();
const BIND_ALL_SECRETS = process.env.MOONLY_BIND_ALL_SECRETS === "1"
  || DEPLOY_SECRET_NAMES.has("all");
const IS_FUNCTION_RUNTIME = Boolean(
  process.env.K_SERVICE
    || process.env.FUNCTION_TARGET
    || process.env.FUNCTION_SIGNATURE_TYPE,
);
function shouldBindSecret(name) {
  return BIND_ALL_SECRETS || DEPLOY_SECRET_NAMES.has(name);
}

function optionalSecret(name) {
  if (IS_FUNCTION_RUNTIME || shouldBindSecret(name)) {
    return defineSecret(name);
  }

  return {
    value: () => "",
  };
}

const deepseekApiKey = optionalSecret("DEEPSEEK_API_KEY");
const volcanoTtsApiKey = optionalSecret("VOLC_TTS_API_KEY");
const paymentWebhookSecret = optionalSecret("PAYMENT_WEBHOOK_SECRET");
const appleSharedSecret = optionalSecret("APPLE_SHARED_SECRET");
const googlePackageName = optionalSecret("GOOGLE_PACKAGE_NAME");
const googleServiceAccountJson = optionalSecret("GOOGLE_SERVICE_ACCOUNT_JSON");
const SECRET_PARAMS = Object.freeze({
  DEEPSEEK_API_KEY: deepseekApiKey,
  VOLC_TTS_API_KEY: volcanoTtsApiKey,
  PAYMENT_WEBHOOK_SECRET: paymentWebhookSecret,
  APPLE_SHARED_SECRET: appleSharedSecret,
  GOOGLE_PACKAGE_NAME: googlePackageName,
  GOOGLE_SERVICE_ACCOUNT_JSON: googleServiceAccountJson,
});

const REGION = "us-central1";
const FIRESTORE_TRIGGER_REGION = "asia-east2";
const DEEPSEEK_URL = "https://api.deepseek.com/chat/completions";
const DEEPSEEK_MODEL = "deepseek-chat";
const VOLCANO_TTS_V3_URL = "https://openspeech.bytedance.com/api/v3/tts/unidirectional";

const FREE_AI_DAILY_LIMIT = 30;
const PRO_AI_DAILY_LIMIT = 300;
const FREE_TTS_DAILY_LIMIT = 20;
const PRO_TTS_DAILY_LIMIT = 200;
const FEEDBACK_STATUSES = new Set(["new", "triaged", "in_progress", "resolved", "closed"]);
const DELETE_BATCH_LIMIT = 450;
const USER_OWNED_SUBCOLLECTIONS = Object.freeze([
  "daily_oracles",
  "divination_records",
  "dialog_sessions",
  "dialog_reply_jobs",
  "memories",
  "tomorrow_hooks",
  "friends",
  "friend_requests",
  "blocked_users",
  "virtual_friends",
  "feedback",
  "settings",
  "push_tokens",
  "remote_notifications",
]);
const ADMIN_ROOT_COLLECTIONS = Object.freeze([
  {
    key: "users",
    label: "用户资料",
    guidePage: 5,
    collectionPath: "users",
    scope: "root",
    orderBy: "lastSignInAt",
    orderDirection: "desc",
    idHint: "Firebase UID",
    description: "用户核心资料、会员状态和管理员权限判断。",
    readable: "displayName、email、photoUrl、membershipStatus、proExpiresAt、isAdmin、role",
    writable: "资料字段、会员字段、isAdmin、role、资料更新时间",
    risk: "误改 UID 文档会影响登录资料、会员权益和后台权限。",
    presets: [
      { label: "管理员账号", whereField: "role", whereOp: "==", whereValue: "admin" },
      { label: "Pro 用户", whereField: "membershipStatus", whereOp: "==", whereValue: "pro" },
    ],
    templates: [
      {
        label: "授予管理员",
        merge: true,
        data: {
          isAdmin: true,
          role: "admin",
          adminUpdatedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "public_profiles",
    label: "公开资料",
    guidePage: 6,
    collectionPath: "public_profiles",
    scope: "root",
    orderBy: "displayNameLower",
    orderDirection: "asc",
    idHint: "Firebase UID",
    description: "好友搜索和公开展示资料，只应保存轻量公开信息。",
    readable: "displayName、displayNameLower、email、photoUrl、searchKeywords",
    writable: "昵称、头像、公开简介、搜索关键字",
    risk: "搜索关键字或小写昵称错误会导致好友搜索异常。",
    presets: [
      { label: "昵称搜索", whereField: "searchKeywords", whereOp: "array-contains", whereValue: "name" },
    ],
    templates: [
      {
        label: "公开资料修复",
        merge: true,
        data: {
          displayName: "",
          displayNameLower: "",
          searchKeywords: [],
          updatedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "feedback",
    label: "反馈镜像",
    guidePage: 7,
    collectionPath: "feedback",
    scope: "root",
    orderBy: "createdAt",
    orderDirection: "desc",
    idHint: "feedbackId",
    description: "用户反馈顶层镜像，适合客服处理、分类和排查。",
    readable: "content、status、category、tag、uid、email、platform、appVersion、deviceModel",
    writable: "status、adminNote、handledBy、分类标签、处理时间",
    risk: "只改顶层镜像不会自动同步用户子集合反馈。",
    presets: [
      { label: "新反馈", whereField: "status", whereOp: "==", whereValue: "new" },
      { label: "处理中", whereField: "status", whereOp: "==", whereValue: "in_progress" },
    ],
    templates: [
      {
        label: "标记已解决",
        merge: true,
        data: {
          status: "resolved",
          adminNote: "",
          handledAt: { __type: "serverTimestamp" },
        },
      },
      {
        label: "标记处理中",
        merge: true,
        data: {
          status: "in_progress",
          adminNote: "",
          updatedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "app_config",
    label: "公开配置",
    guidePage: 8,
    collectionPath: "app_config",
    scope: "root",
    orderBy: "__name__",
    orderDirection: "asc",
    idHint: "configId",
    description: "公开链接、IAP 商品和客户端公开配置。",
    readable: "socialLinks、iapProducts、priceLabel、productId",
    writable: "社媒链接、商品 ID、价格标签、公开开关",
    risk: "商品 ID 或公开配置错误会直接影响客户端展示和购买。",
    presets: [
      { label: "public 配置", docId: "public", orderBy: "__name__", orderDirection: "asc" },
    ],
    templates: [
      {
        label: "public 配置",
        docId: "public",
        merge: true,
        data: {
          socialLinks: {
            instagram: "https://www.instagram.com/",
            facebook: "https://www.facebook.com/",
            x: "https://x.com/",
            tiktok: "https://www.tiktok.com/",
            pinterest: "https://www.pinterest.com/",
          },
          updatedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "daily_oracle_summaries",
    label: "每日神谕摘要",
    guidePage: 9,
    collectionPath: "daily_oracle_summaries",
    scope: "root",
    orderBy: "updatedAt",
    orderDirection: "desc",
    idHint: "uid_date",
    description: "好友动态使用的每日神谕摘要，不应包含完整私密解读。",
    readable: "ownerUid、date、cardId、cardName、orientation、title、oracle、visibility",
    writable: "摘要标题、展示字段、可见性、同步标记",
    risk: "可见性错误可能导致动态无法展示或错误公开。",
    presets: [
      { label: "公开摘要", whereField: "summaryOnly", whereOp: "==", whereValue: "true" },
      { label: "已同步", whereField: "syncEnabled", whereOp: "==", whereValue: "true" },
    ],
    templates: [
      {
        label: "修正为摘要",
        merge: true,
        data: {
          summaryOnly: true,
          syncEnabled: true,
          visibility: "real_friends",
          updatedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "relationship_divinations",
    label: "双人关系占卜",
    guidePage: 10,
    collectionPath: "relationship_divinations",
    scope: "root",
    orderBy: "updatedAt",
    orderDirection: "desc",
    idHint: "readingId",
    description: "双人关系占卜流程、参与者和结果记录。",
    readable: "creatorUid、targetUid、participants、status、cards、result、createdAt",
    writable: "status、结果字段、排查备注、异常参与者字段",
    risk: "参与者或状态错误会影响双方看到的关系记录。",
    presets: [
      { label: "生成失败", whereField: "status", whereOp: "==", whereValue: "failed" },
      { label: "已取消", whereField: "status", whereOp: "==", whereValue: "cancelled" },
    ],
    templates: [
      {
        label: "取消记录",
        merge: true,
        data: {
          status: "cancelled",
          adminNote: "",
          updatedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "quick_reading",
    label: "快速占卜配置",
    guidePage: 8,
    collectionPath: "quick_reading",
    scope: "root",
    orderBy: "__name__",
    orderDirection: "asc",
    idHint: "oracleId",
    description: "快速占卜入口、文案和启用状态配置。",
    readable: "oracleId、title、enabled、cards、prompt、updatedAt",
    writable: "占卜文案、排序、启用状态、卡牌或模式配置",
    risk: "配置错误会影响 App 启动后的内容读取和展示。",
    presets: [
      { label: "启用项", whereField: "enabled", whereOp: "==", whereValue: "true" },
    ],
    templates: [
      {
        label: "快速占卜配置",
        merge: true,
        data: {
          enabled: true,
          title: "",
          description: "",
          updatedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "iap_receipts",
    label: "IAP 收据",
    guidePage: 11,
    collectionPath: "iap_receipts",
    scope: "root",
    orderBy: "updatedAt",
    orderDirection: "desc",
    idHint: "receiptId",
    description: "IAP 收据校验记录和会员排查证据。",
    readable: "uid、productId、store、status、valid、transactionId、proExpiresAt",
    writable: "审核备注、排查状态，不建议修改原始收据字段",
    risk: "错误修改会破坏支付申诉证据和会员状态判断。",
    presets: [
      { label: "待配置校验", whereField: "status", whereOp: "==", whereValue: "pending_configuration" },
      { label: "校验成功", whereField: "status", whereOp: "==", whereValue: "verified" },
      { label: "校验失败", whereField: "valid", whereOp: "==", whereValue: "false" },
    ],
    templates: [
      {
        label: "支付排查备注",
        merge: true,
        data: {
          adminNote: "",
          reviewedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "usage_limits",
    label: "用量限制",
    guidePage: 12,
    collectionPath: "usage_limits",
    scope: "root",
    orderBy: "updatedAt",
    orderDirection: "desc",
    idHint: "uid_action_day",
    description: "用户功能用量、周期计数和限制值。",
    readable: "uid、action、day、count、limit、updatedAt",
    writable: "计数、重置时间、限制值",
    risk: "计数错误会导致功能被错误解锁或锁死。",
    presets: [
      { label: "AI 用量", whereField: "action", whereOp: "==", whereValue: "aiChat" },
      { label: "TTS 用量", whereField: "action", whereOp: "==", whereValue: "tts" },
    ],
    templates: [
      {
        label: "重置计数",
        merge: true,
        data: {
          used: 0,
          updatedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "payment_events",
    label: "支付事件",
    guidePage: 11,
    collectionPath: "payment_events",
    scope: "root",
    orderBy: "createdAt",
    orderDirection: "desc",
    idHint: "eventId",
    description: "支付回调或支付事件原始记录。",
    readable: "eventId、uid、provider、type、status、payload、createdAt",
    writable: "处理状态和排查备注，不建议修改原始 payload",
    risk: "修改历史事件会影响支付链路追踪。",
    presets: [
      { label: "失败事件", whereField: "status", whereOp: "==", whereValue: "failed" },
    ],
    templates: [
      {
        label: "事件备注",
        merge: true,
        data: {
          adminNote: "",
          reviewedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "analytics_events",
    label: "分析事件",
    guidePage: 12,
    collectionPath: "analytics_events",
    scope: "root",
    orderBy: "createdAt",
    orderDirection: "desc",
    idHint: "eventId",
    description: "产品行为分析事件和排查标签。",
    readable: "eventName、uid、platform、appVersion、params、createdAt",
    writable: "标签、备注、排查字段，不建议修改历史事实",
    risk: "修改事件事实会污染分析数据。",
    presets: [
      { label: "登录事件", whereField: "eventName", whereOp: "==", whereValue: "login" },
    ],
    templates: [
      {
        label: "分析备注",
        merge: true,
        data: {
          adminTag: "",
          reviewedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "admin_audit_logs",
    label: "管理员审计日志",
    guidePage: 18,
    collectionPath: "admin_audit_logs",
    scope: "root",
    orderBy: "createdAt",
    orderDirection: "desc",
    idHint: "autoId",
    description: "后台管理员对会员、资料、好友、配置、Prompt 和原始数据的高风险操作记录。",
    readable: "action、adminEmail、adminUid、target、details、request、createdAt",
    writable: "只建议系统自动写入，人工修改会影响审计可信度。",
    risk: "审计日志用于追溯管理员操作，不建议删除或覆盖。",
    presets: [
      { label: "会员操作", whereField: "targetType", whereOp: "==", whereValue: "membership" },
      { label: "用户资料", whereField: "targetType", whereOp: "==", whereValue: "user" },
      { label: "Prompt 发布", whereField: "targetType", whereOp: "==", whereValue: "agent_prompt" },
    ],
    templates: [],
  },
]);
const ADMIN_USER_SUBCOLLECTIONS = Object.freeze([
  {
    key: "user_daily_oracles",
    label: "用户/每日神谕",
    guidePage: 13,
    collectionId: "daily_oracles",
    scope: "user",
    orderBy: "date",
    orderDirection: "desc",
    idHint: "yyyy-MM-dd",
    description: "用户每日神谕完整记录，包含个人问题和完整解读。",
    readable: "卡牌、方向、问题、完整解读、情绪字段、创建时间",
    writable: "卡牌字段、解读字段、同步标记、修复备注",
    risk: "包含用户私密问题和完整解读，只在授权排查时查看。",
    presets: [
      { label: "按日期倒序", orderBy: "date", orderDirection: "desc" },
    ],
    templates: [
      {
        label: "同步修正",
        merge: true,
        data: {
          syncEnabled: true,
          updatedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "user_divination_records",
    label: "用户/占卜历史",
    guidePage: 14,
    collectionId: "divination_records",
    scope: "user",
    orderBy: "createdAt",
    orderDirection: "desc",
    idHint: "readingId",
    description: "个人占卜历史，通常比摘要更完整。",
    readable: "占卜类型、问题、卡牌、结果、角色、创建时间",
    writable: "标题、标签、结果字段、异常状态",
    risk: "包含高度个人化问题和答案，导出需谨慎。",
    presets: [
      { label: "最近记录", orderBy: "createdAt", orderDirection: "desc" },
    ],
    templates: [
      {
        label: "记录备注",
        merge: true,
        data: {
          adminNote: "",
          updatedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "user_dialog_sessions",
    label: "用户/对话会话",
    guidePage: 15,
    collectionId: "dialog_sessions",
    scope: "user",
    orderBy: "__name__",
    orderDirection: "asc",
    idHint: "sessionId",
    description: "对话会话、上下文状态和会话摘要。",
    readable: "会话标题、消息摘要、角色、最近更新时间、上下文信息",
    writable: "标题、归档状态、错误状态、排查备注",
    risk: "对话内容可能包含情感、关系、身份等隐私。",
    presets: [
      { label: "归档会话", whereField: "archived", whereOp: "==", whereValue: "true" },
    ],
    templates: [
      {
        label: "归档会话",
        merge: true,
        data: {
          archived: true,
          updatedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "user_memories",
    label: "用户/记忆",
    guidePage: 16,
    collectionId: "memories",
    scope: "user",
    orderBy: "__name__",
    orderDirection: "asc",
    idHint: "memoryId",
    description: "长期记忆、用户偏好和 AI 个性化上下文。",
    readable: "记忆内容、来源、权重、更新时间",
    writable: "记忆内容、启用状态、删除标记",
    risk: "错误记忆会持续影响 AI 回复。",
    presets: [
      { label: "已启用", whereField: "enabled", whereOp: "==", whereValue: "true" },
    ],
    templates: [
      {
        label: "停用记忆",
        merge: true,
        data: {
          enabled: false,
          disabledAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "user_tomorrow_hooks",
    label: "用户/明日提醒",
    guidePage: 16,
    collectionId: "tomorrow_hooks",
    scope: "user",
    orderBy: "createdAt",
    orderDirection: "desc",
    idHint: "hookId",
    description: "后续提醒、明日回访和连续体验触发点。",
    readable: "hook 文案、触发日期、状态、创建时间",
    writable: "状态、日期、内容、取消标记",
    risk: "配置错误会造成错误提醒或体验断裂。",
    presets: [
      { label: "待触发", whereField: "status", whereOp: "==", whereValue: "pending" },
    ],
    templates: [
      {
        label: "取消提醒",
        merge: true,
        data: {
          status: "cancelled",
          cancelledAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "user_friends",
    label: "用户/好友",
    guidePage: 17,
    collectionId: "friends",
    scope: "user",
    orderBy: "updatedAt",
    orderDirection: "desc",
    idHint: "friendUid",
    description: "用户好友关系、快照和状态。",
    readable: "好友 UID、状态、昵称快照、来源、时间",
    writable: "status、备注、快照字段",
    risk: "好友关系通常需要双方镜像一致，手工修改需核对双方。",
    presets: [
      { label: "正式好友", whereField: "status", whereOp: "==", whereValue: "friend" },
    ],
    templates: [
      {
        label: "标记好友",
        merge: true,
        data: {
          status: "friend",
          updatedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "user_friend_requests",
    label: "用户/好友请求",
    guidePage: 17,
    collectionId: "friend_requests",
    scope: "user",
    orderBy: "updatedAt",
    orderDirection: "desc",
    idHint: "requesterUid",
    description: "好友申请、接收方、请求方和处理状态。",
    readable: "请求方、接收方、状态、消息、时间",
    writable: "status、处理时间",
    risk: "请求状态异常会导致邀请卡住或重复显示。",
    presets: [
      { label: "待处理", whereField: "status", whereOp: "==", whereValue: "pendingReceived" },
      { label: "已接受", whereField: "status", whereOp: "==", whereValue: "accepted" },
    ],
    templates: [
      {
        label: "标记接受",
        merge: true,
        data: {
          status: "accepted",
          handledAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "user_blocked_users",
    label: "用户/拉黑列表",
    guidePage: 17,
    collectionId: "blocked_users",
    scope: "user",
    orderBy: "blockedAt",
    orderDirection: "desc",
    idHint: "blockedUid",
    description: "用户拉黑关系和原因。",
    readable: "被拉黑 UID、时间、原因",
    writable: "拉黑状态、备注",
    risk: "误删或误加会影响用户社交安全边界。",
    presets: [
      { label: "最近拉黑", orderBy: "blockedAt", orderDirection: "desc" },
    ],
    templates: [
      {
        label: "拉黑备注",
        merge: true,
        data: {
          adminNote: "",
          reviewedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "user_virtual_friends",
    label: "用户/虚拟好友",
    guidePage: 18,
    collectionId: "virtual_friends",
    scope: "user",
    orderBy: "updatedAt",
    orderDirection: "desc",
    idHint: "virtualFriendId",
    description: "虚拟好友资料、关系设定和状态。",
    readable: "虚拟好友资料、关系设定、状态、更新时间",
    writable: "昵称、设定、状态",
    risk: "改错会影响陪伴体验和对话上下文。",
    presets: [
      { label: "启用虚拟好友", whereField: "enabled", whereOp: "==", whereValue: "true" },
    ],
    templates: [
      {
        label: "虚拟好友状态",
        merge: true,
        data: {
          enabled: true,
          updatedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "user_feedback",
    label: "用户/反馈",
    guidePage: 18,
    collectionId: "feedback",
    scope: "user",
    orderBy: "createdAt",
    orderDirection: "desc",
    idHint: "feedbackId",
    description: "用户侧反馈原始记录。",
    readable: "content、status、category、tag、appVersion、platform、createdAt",
    writable: "status、备注、同步字段",
    risk: "与顶层 feedback 镜像可能不同步。",
    presets: [
      { label: "新反馈", whereField: "status", whereOp: "==", whereValue: "new" },
    ],
    templates: [
      {
        label: "用户反馈已解决",
        merge: true,
        data: {
          status: "resolved",
          adminNote: "",
          handledAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
  {
    key: "user_settings",
    label: "用户/设置",
    guidePage: 18,
    collectionId: "settings",
    scope: "user",
    orderBy: "__name__",
    orderDirection: "asc",
    idHint: "settingId",
    description: "通知、隐私、语言和个人偏好配置。",
    readable: "通知、隐私、语言、推送偏好",
    writable: "开关、语言、推送偏好",
    risk: "改错会影响用户设置和通知体验。",
    presets: [
      { label: "按 ID 查看", orderBy: "__name__", orderDirection: "asc" },
    ],
    templates: [
      {
        label: "通知设置",
        docId: "notifications",
        merge: true,
        data: {
          pushEnabled: true,
          updatedAt: { __type: "serverTimestamp" },
        },
      },
      {
        label: "隐私设置",
        docId: "privacy",
        merge: true,
        data: {
          profileVisible: true,
          updatedAt: { __type: "serverTimestamp" },
        },
      },
    ],
  },
]);
const ADMIN_COLLECTION_DEFINITIONS = Object.freeze([
  ...ADMIN_ROOT_COLLECTIONS,
  ...ADMIN_USER_SUBCOLLECTIONS,
]);

const DEFAULT_PUBLIC_CONFIG = Object.freeze({
  socialLinks: Object.freeze({
    instagram: "https://www.instagram.com/",
    facebook: "https://www.facebook.com/",
    x: "https://x.com/",
    tiktok: "https://www.tiktok.com/",
    pinterest: "https://www.pinterest.com/",
  }),
  iapProducts: Object.freeze({
    proMonthly: Object.freeze({
      productId: "fari.pro.monthly",
      type: "subscription",
      store: "app_store_google_play",
      displayName: "Fari Pro 月度会员",
      priceLabel: "",
    }),
    proYearly: Object.freeze({
      productId: "fari.pro.yearly",
      type: "subscription",
      store: "app_store_google_play",
      displayName: "Fari Pro 年度会员",
      priceLabel: "",
    }),
  }),
});

const AGENT_ADMIN_CONFIG_PATH = path.join(__dirname, "agent-admin.config.json");
const AGENT_PROMPT_ROOT = path.join(__dirname, "agent-prompts");
const AGENT_TRACE_COLLECTION = "agent_traces";
const AGENT_PROMPT_FILES_COLLECTION = "agent_prompt_files";
const AGENT_PROMPT_DRAFTS_COLLECTION = "agent_prompt_drafts";
const AGENT_PROMPT_RELEASES_COLLECTION = "agent_prompt_releases";
const AGENT_TURN_RATINGS_COLLECTION = "agent_turn_ratings";
const ADMIN_AUDIT_LOG_COLLECTION = "admin_audit_logs";
const ADMIN_AUDIT_REDACTION_KEYS = Object.freeze([
  "password",
  "token",
  "secret",
  "credential",
  "receipt",
  "authorization",
  "content",
  "baseContent",
  "previousContent",
  "payload",
]);

const runtime = {
  region: REGION,
  cors: true,
  invoker: "public",
  timeoutSeconds: 120,
  memory: "512MiB",
};

function boundSecrets(...names) {
  return names
    .filter(shouldBindSecret)
    .map((name) => SECRET_PARAMS[name])
    .filter(Boolean);
}

function boundSecretNames() {
  return ALL_SECRET_NAMES.filter((name) => Boolean(getOptionalSecret(SECRET_PARAMS[name])));
}

function json(res, status, body) {
  res.status(status).json(body);
}

function requireMethod(req, res, method) {
  if (req.method === method) return true;
  json(res, 405, { error: `Use ${method}` });
  return false;
}

async function requireAuth(req, res) {
  const header = req.get("authorization") || "";
  const match = header.match(/^Bearer\s+(.+)$/i);
  if (!match) {
    json(res, 401, { error: "Missing Firebase ID token" });
    return null;
  }

  try {
    return await admin.auth().verifyIdToken(match[1]);
  } catch (error) {
    console.error("[auth] verify failed", error);
    json(res, 401, { error: "Invalid Firebase ID token" });
    return null;
  }
}

async function requireAdmin(req, res) {
  const decoded = await requireAuth(req, res);
  if (!decoded) return null;

  if (decoded.admin === true || decoded.role === "admin") {
    return decoded;
  }

  const userSnap = await db.collection("users").doc(decoded.uid).get();
  const userData = userSnap.exists ? userSnap.data() : {};
  if (userData?.isAdmin === true || userData?.role === "admin") {
    return decoded;
  }

  json(res, 403, { error: "Admin permission required" });
  return null;
}

const PUSH_TOKEN_COLLECTION = "push_tokens";
const PUSH_OUTBOX_COLLECTION = "remote_notifications";
const DIALOG_SESSION_COLLECTION = "dialog_sessions";
const DIALOG_REPLY_JOBS_COLLECTION = "dialog_reply_jobs";
const DEFAULT_DIALOG_SESSION_ID = "default";
const DIALOG_REPLY_JOB_STALE_MS = 3 * 60 * 1000;
const DIALOG_REPLY_JOB_SCAN_LIMIT = 50;
const PUSH_CHANNEL_ID = "moonly_reminders";
const INVALID_PUSH_TOKEN_CODES = new Set([
  "messaging/invalid-registration-token",
  "messaging/registration-token-not-registered",
  "messaging/invalid-argument",
]);

function toPushStringMap(data) {
  const result = {};
  for (const [key, value] of Object.entries(data || {})) {
    if (value === undefined || value === null) continue;
    result[key] = String(value).slice(0, 900);
  }
  return result;
}

function truncatePushText(value, fallback, maxLength = 80) {
  const text = String(value || fallback || "").replace(/\s+/g, " ").trim();
  if (text.length <= maxLength) return text;
  return `${text.slice(0, maxLength - 1)}…`;
}

async function isPushPreferenceEnabled(uid, preferenceKey) {
  if (!preferenceKey) return true;

  const snap = await db.collection("users")
    .doc(uid)
    .collection("settings")
    .doc("notifications")
    .get();
  const settings = snap.exists ? snap.data() || {} : {};

  if (preferenceKey === "friendInteractionEnabled") {
    return settings.friendInteractionEnabled === true;
  }

  return settings[preferenceKey] !== false;
}

async function getUserPushTokenDocs(uid) {
  const snap = await db.collection("users")
    .doc(uid)
    .collection(PUSH_TOKEN_COLLECTION)
    .where("enabled", "==", true)
    .limit(100)
    .get();

  return snap.docs
    .map((doc) => ({ ref: doc.ref, id: doc.id, token: String(doc.data()?.token || "") }))
    .filter((entry) => entry.token.length > 0);
}

function buildRemotePushMessage(tokens, title, body, data) {
  return {
    tokens,
    notification: {
      title: truncatePushText(title, "Nocturne Oracle"),
      body: truncatePushText(body, "你有一条新的提醒", 140),
    },
    data: toPushStringMap(data),
    android: {
      priority: "high",
      notification: {
        channelId: PUSH_CHANNEL_ID,
        sound: "default",
      },
    },
    apns: {
      headers: {
        "apns-priority": "10",
        "apns-push-type": "alert",
      },
      payload: {
        aps: {
          sound: "default",
          badge: 1,
        },
      },
    },
  };
}

function isInvalidPushTokenError(error) {
  return Boolean(error?.code && INVALID_PUSH_TOKEN_CODES.has(error.code));
}

async function cleanupInvalidPushTokens(tokenDocs, responses) {
  const batch = db.batch();
  let dirty = false;

  responses.forEach((response, index) => {
    if (response.success || !isInvalidPushTokenError(response.error)) return;
    const tokenDoc = tokenDocs[index];
    if (!tokenDoc?.ref) return;

    dirty = true;
    batch.set(tokenDoc.ref, {
      enabled: false,
      disabledAt: admin.firestore.FieldValue.serverTimestamp(),
      lastError: response.error.code || "messaging-error",
    }, { merge: true });
  });

  if (dirty) await batch.commit();
}

async function sendRemotePushToUser(uid, payload) {
  if (!uid) return { sent: 0, failureCount: 0, skipped: "missing-uid" };

  const preferenceKey = payload.preferenceKey || "";
  const enabled = await isPushPreferenceEnabled(uid, preferenceKey);
  if (!enabled) return { sent: 0, failureCount: 0, skipped: "preference-disabled" };

  const tokenDocs = await getUserPushTokenDocs(uid);
  if (tokenDocs.length === 0) return { sent: 0, failureCount: 0, skipped: "no-token" };

  const message = buildRemotePushMessage(
    tokenDocs.map((entry) => entry.token),
    payload.title,
    payload.body,
    {
      type: payload.type || "remote_notification",
      clickAction: payload.clickAction || payload.type || "remote_notification",
      ...payload.data,
    },
  );

  const response = await admin.messaging().sendEachForMulticast(message);
  await cleanupInvalidPushTokens(tokenDocs, response.responses);

  return {
    sent: response.successCount,
    failureCount: response.failureCount,
    skipped: "",
  };
}

function shouldNotifyDialogueReply(body) {
  if (!body) return false;
  if (body.notifyOnComplete !== true) return false;
  return cleanString(body.notificationType || body.notifyType, "", 80) === "dialogue_reply";
}

function buildNotificationId(prefix, value) {
  const cleaned = String(value || "")
    .trim()
    .replace(/[^a-zA-Z0-9_-]/g, "_")
    .slice(0, 120);
  if (cleaned) return `${prefix}_${cleaned}`;
  return `${prefix}_${Date.now()}_${crypto.randomBytes(4).toString("hex")}`;
}

async function enqueueDialogueReplyNotification(uid, body, content) {
  if (!uid || !shouldNotifyDialogueReply(body)) return null;

  const preview = truncatePushText(content, "神谕师有新的回复，回来继续这段对话。", 90);
  const notificationId = buildNotificationId("dialogue_reply", body.clientRequestId || body.requestId);
  const payload = {
    type: "dialogue_reply",
    clickAction: "dialogue_reply",
    title: cleanString(body.notificationTitle, "神谕师回复了你", 80),
    body: preview,
    preferenceKey: "dialogueReplyEnabled",
    payload: {
      source: "ai_chat",
      clientRequestId: cleanString(body.clientRequestId || body.requestId, "", 120),
      preview,
      completedAt: new Date().toISOString(),
    },
    status: "queued",
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  };

  await db.collection("users")
    .doc(uid)
    .collection(PUSH_OUTBOX_COLLECTION)
    .doc(notificationId)
    .set(payload, { merge: true });

  return notificationId;
}

function collectStreamTextFromChunk(state, chunkText) {
  state.buffer += chunkText;

  let newlineIndex;
  while ((newlineIndex = state.buffer.indexOf("\n")) >= 0) {
    const line = state.buffer.slice(0, newlineIndex).trim();
    state.buffer = state.buffer.slice(newlineIndex + 1);
    if (!line.startsWith("data:")) continue;

    const data = line.slice(5).trim();
    if (!data || data === "[DONE]") continue;

    try {
      const parsed = JSON.parse(data);
      const delta = parsed?.choices?.[0]?.delta?.content || "";
      if (delta) state.fullText += delta;
    } catch (error) {
      console.warn("[aiChatStream] Failed to parse SSE chunk for notification preview", error.message);
    }
  }
}

function normalizeDialogueReplyJobId(value) {
  return String(value || "")
    .trim()
    .replace(/[^a-zA-Z0-9_-]/g, "_")
    .slice(0, 120);
}

function getDialogueReplyJobId(body) {
  return normalizeDialogueReplyJobId(body?.clientRequestId || body?.requestId);
}

function buildDialogueReplyNotificationBody(job, jobId) {
  return {
    notifyOnComplete: true,
    notificationType: "dialogue_reply",
    notificationTitle: cleanString(job?.notificationTitle, "神谕师回复了你", 80),
    clientRequestId: cleanString(job?.clientRequestId || jobId, jobId, 120),
  };
}

function getTimestampMillis(value) {
  if (!value) return 0;
  if (typeof value.toMillis === "function") return value.toMillis();
  const parsed = Date.parse(String(value));
  return Number.isFinite(parsed) ? parsed : 0;
}

function findLastUserMessage(messages) {
  const sanitized = sanitizeMessages(messages);
  for (let i = sanitized.length - 1; i >= 0; i--) {
    if (sanitized[i].role === "user") return sanitized[i].content;
  }
  return "";
}

function buildDialogSessionMessage(id, roleType, content, divinerType, jobId) {
  return {
    id,
    roleType,
    messageType: "Str",
    content: cleanString(content, "", 6000),
    options: [],
    divinerType: cleanString(divinerType, "Tarot", 40),
    spreadKind: "",
    spreadCardsDrawn: false,
    spreadDrawnCards: [],
    spreadDrawResultAddedToHistory: false,
    readingId: "",
    divinationQuestion: "",
    divinationScene: "",
    divinationCreatedAt: "",
    shortVerdict: "",
    judgeContent: "",
    adviceContent: "",
    followupTopics: [],
    friendName: "",
    friendContext: "",
    ttsAudioReady: false,
    ttsDurationSeconds: 0,
    contextAttachments: [],
    oraclePromptId: "",
    oracleScene: "",
    oracleStage: "",
    oracleStageReason: "",
    oracleResponseMode: "",
    oracleRiskLevel: "",
    oracleRiskFlags: [],
    serverReplyJobId: jobId,
    createdAtServer: new Date().toISOString(),
  };
}

function getNextDialogMessageId(messages) {
  let nextId = 0;
  for (const message of messages || []) {
    const id = Number(message?.id);
    if (Number.isFinite(id)) nextId = Math.max(nextId, id + 1);
  }
  return nextId;
}

function sessionHasUserMessage(messages, text) {
  const normalized = cleanString(text, "", 6000);
  if (!normalized) return true;
  return (messages || []).some((message) =>
    message?.roleType === "User" && cleanString(message?.content, "", 6000) === normalized);
}

function sessionHasReplyJob(messages, jobId) {
  if (!jobId) return false;
  return (messages || []).some((message) => message?.serverReplyJobId === jobId);
}

async function markDialogueReplyJobProcessing(uid, body, source) {
  const jobId = getDialogueReplyJobId(body);
  if (!uid || !jobId) return null;

  const jobRef = db.collection("users")
    .doc(uid)
    .collection(DIALOG_REPLY_JOBS_COLLECTION)
    .doc(jobId);

  await jobRef.set({
    status: source === "scheduler" ? "processing_queue" : "processing_http",
    processingSource: source,
    processingStartedAt: admin.firestore.FieldValue.serverTimestamp(),
    updatedAt: admin.firestore.FieldValue.serverTimestamp(),
  }, { merge: true });

  return jobId;
}

async function completeDialogueReplyJob(uid, body, content, source) {
  const jobId = getDialogueReplyJobId(body);
  if (!uid || !jobId || !content) return false;

  const userRef = db.collection("users").doc(uid);
  const jobRef = userRef.collection(DIALOG_REPLY_JOBS_COLLECTION).doc(jobId);
  const sessionRef = userRef.collection(DIALOG_SESSION_COLLECTION).doc(DEFAULT_DIALOG_SESSION_ID);
  let completed = false;

  await db.runTransaction(async (tx) => {
    const jobSnap = await tx.get(jobRef);
    const job = jobSnap.exists ? jobSnap.data() || {} : {};
    if (job.status === "completed") return;

    const sessionSnap = await tx.get(sessionRef);
    const session = sessionSnap.exists ? sessionSnap.data() || {} : {};
    const messages = Array.isArray(session.messages) ? [...session.messages] : [];
    const apiMessages = Array.isArray(session.apiMessages) ? [...session.apiMessages] : [];
    const divinerType = cleanString(job.divinerType || body.divinerType, "Tarot", 40);
    const lastUserMessage = cleanString(
      job.lastUserMessage || findLastUserMessage(job.messages) || findLastUserMessage(body.messages),
      "",
      6000,
    );

    let nextId = getNextDialogMessageId(messages);
    if (!sessionHasUserMessage(messages, lastUserMessage)) {
      messages.push(buildDialogSessionMessage(nextId, "User", lastUserMessage, divinerType, jobId));
      nextId += 1;
    }

    if (!sessionHasReplyJob(messages, jobId)) {
      messages.push(buildDialogSessionMessage(nextId, "AI", content, divinerType, jobId));
    }

    if (!apiMessages.some((message) => message?.serverReplyJobId === jobId && message?.role === "assistant")) {
      apiMessages.push({
        role: "assistant",
        content: cleanString(content, "", 6000),
        serverReplyJobId: jobId,
      });
    }

    tx.set(sessionRef, {
      schemaVersion: 1,
      messages,
      apiMessages,
      savedAtServer: new Date().toISOString(),
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });

    tx.set(jobRef, {
      status: "completed",
      assistantContent: cleanString(content, "", 6000),
      completedBy: source,
      completedAt: admin.firestore.FieldValue.serverTimestamp(),
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });

    completed = true;
  });

  return completed;
}

async function failDialogueReplyJob(jobRef, error, source) {
  await jobRef.set({
    status: "failed",
    failedBy: source,
    error: cleanString(error?.message || error, "Dialogue reply job failed", 500),
    failedAt: admin.firestore.FieldValue.serverTimestamp(),
    updatedAt: admin.firestore.FieldValue.serverTimestamp(),
  }, { merge: true });
}

async function claimStaleDialogueReplyJob(jobRef, cutoffMillis) {
  let claimed = false;
  await db.runTransaction(async (tx) => {
    const snap = await tx.get(jobRef);
    if (!snap.exists) return;

    const job = snap.data() || {};
    const status = job.status || "";
    if (status !== "queued" && status !== "client_streaming") return;

    const createdAtMillis = getTimestampMillis(job.createdAt) || getTimestampMillis(job.createdAtClient);
    if (status === "client_streaming" && createdAtMillis && createdAtMillis > cutoffMillis) return;

    tx.set(jobRef, {
      status: "processing_queue",
      processingSource: "scheduler",
      processingStartedAt: admin.firestore.FieldValue.serverTimestamp(),
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });
    claimed = true;
  });
  return claimed;
}

async function processDialogueReplyJobDoc(jobSnap) {
  const jobRef = jobSnap.ref;
  const parentUserRef = jobRef.parent.parent;
  const uid = parentUserRef?.id || "";
  if (!uid) return { processed: 0, skipped: "missing-uid" };

  const cutoffMillis = Date.now() - DIALOG_REPLY_JOB_STALE_MS;
  const claimed = await claimStaleDialogueReplyJob(jobRef, cutoffMillis);
  if (!claimed) return { processed: 0, skipped: "not-claimable" };

  const latestSnap = await jobRef.get();
  const job = latestSnap.exists ? latestSnap.data() || {} : {};
  const messages = sanitizeMessages(job.messages);
  if (messages.length === 0) {
    await failDialogueReplyJob(jobRef, "missing messages", "scheduler");
    return { processed: 0, skipped: "missing-messages" };
  }

  try {
    const membership = await getMembership(uid);
    await incrementUsage(uid, "aiChat", membership.isPro ? PRO_AI_DAILY_LIMIT : FREE_AI_DAILY_LIMIT);

    const response = await callDeepSeek({
      messages,
      temperature: typeof job.temperature === "number" ? job.temperature : 0.7,
      max_tokens: Number(job.max_tokens || job.maxTokens || 2000),
    }, false);
    const data = await response.json();
    const content = data?.choices?.[0]?.message?.content || "";
    const body = buildDialogueReplyNotificationBody(job, jobRef.id);

    await completeDialogueReplyJob(uid, { ...body, messages, divinerType: job.divinerType }, content, "scheduler");
    await enqueueDialogueReplyNotification(uid, body, content);
    return { processed: 1, skipped: "" };
  } catch (error) {
    console.error("[processDialogueReplyJobDoc]", { uid, jobId: jobRef.id, error });
    await failDialogueReplyJob(jobRef, error, "scheduler");
    return { processed: 0, skipped: error.code || "error" };
  }
}

async function deleteQueryInBatches(query) {
  let deleted = 0;

  while (true) {
    const snap = await query.limit(DELETE_BATCH_LIMIT).get();
    if (snap.empty) return deleted;

    const batch = db.batch();
    for (const doc of snap.docs) {
      batch.delete(doc.ref);
    }
    await batch.commit();
    deleted += snap.size;

    if (snap.size < DELETE_BATCH_LIMIT) return deleted;
  }
}

async function deleteUserOwnedData(uid) {
  const userRef = db.collection("users").doc(uid);
  let deleted = 0;

  for (const collectionName of USER_OWNED_SUBCOLLECTIONS) {
    deleted += await deleteQueryInBatches(userRef.collection(collectionName));
  }

  deleted += await deleteQueryInBatches(
    db.collection("daily_oracle_summaries").where("ownerUid", "==", uid),
  );
  deleted += await deleteQueryInBatches(
    db.collection("relationship_divinations").where("initiatorUid", "==", uid),
  );
  deleted += await deleteQueryInBatches(
    db.collection("relationship_divinations").where("receiverUid", "==", uid),
  );
  deleted += await deleteQueryInBatches(
    db.collection("feedback").where("uid", "==", uid),
  );
  deleted += await deleteQueryInBatches(
    db.collection("iap_receipts").where("uid", "==", uid),
  );
  deleted += await deleteQueryInBatches(
    db.collection("usage_limits").where("uid", "==", uid),
  );
  deleted += await deleteQueryInBatches(
    db.collection("payment_events").where("uid", "==", uid),
  );

  const batch = db.batch();
  batch.delete(db.collection("public_profiles").doc(uid));
  batch.delete(userRef);
  await batch.commit();

  return deleted + 2;
}

async function getMembership(uid) {
  const snap = await db.collection("users").doc(uid).get();
  const data = snap.exists ? snap.data() : {};
  const status = data.membershipStatus || "free";
  const expiresAt = data.proExpiresAt || null;
  const isActivePro = status === "pro" && (!expiresAt || expiresAt.toMillis() > Date.now());

  return {
    status: isActivePro ? "pro" : "free",
    rawStatus: status,
    proExpiresAt: expiresAt,
    isPro: isActivePro,
  };
}

async function incrementUsage(uid, action, limit) {
  const day = new Date().toISOString().slice(0, 10);
  const usageId = `${uid}_${action}_${day}`;
  const ref = db.collection("usage_limits").doc(usageId);

  await db.runTransaction(async (tx) => {
    const snap = await tx.get(ref);
    const used = snap.exists ? Number(snap.data().count || 0) : 0;
    if (used >= limit) {
      const err = new Error("Daily quota exceeded");
      err.code = "quota-exceeded";
      err.used = used;
      err.limit = limit;
      throw err;
    }

    tx.set(ref, {
      uid,
      action,
      day,
      count: used + 1,
      limit,
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
      createdAt: snap.exists
        ? snap.data().createdAt || admin.firestore.FieldValue.serverTimestamp()
        : admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });
  });
}

async function refundUsage(uid, action) {
  const day = new Date().toISOString().slice(0, 10);
  const usageId = `${uid}_${action}_${day}`;
  const ref = db.collection("usage_limits").doc(usageId);

  await db.runTransaction(async (tx) => {
    const snap = await tx.get(ref);
    if (!snap.exists) return;

    const used = Number(snap.data().count || 0);
    tx.set(ref, {
      count: Math.max(used - 1, 0),
      refundedCount: admin.firestore.FieldValue.increment(1),
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });
  });
}

async function refundUsageSafely(uid, action) {
  try {
    await refundUsage(uid, action);
  } catch (error) {
    console.error("[usage] refund failed", { uid, action, error });
  }
}

function sanitizeMessages(messages) {
  if (!Array.isArray(messages)) return [];
  return messages
    .slice(-20)
    .map((message) => ({
      role: ["system", "user", "assistant"].includes(message.role) ? message.role : "user",
      content: String(message.content || "").slice(0, 6000),
    }))
    .filter((message) => message.content.length > 0);
}

function cleanString(value, fallback, maxLength) {
  const text = String(value || fallback || "").trim();
  return text.slice(0, maxLength);
}

function cleanOptionalUrl(value, maxLength = 1000) {
  const text = cleanString(value, "", maxLength);
  if (!text) return "";

  let parsed;
  try {
    parsed = new URL(text);
  } catch (error) {
    throw createHttpError(400, "Avatar URL is invalid");
  }

  if (parsed.protocol !== "http:" && parsed.protocol !== "https:") {
    throw createHttpError(400, "Avatar URL must start with http:// or https://");
  }

  return parsed.toString();
}

function normalizeOptionalBirthday(value) {
  const text = cleanString(value, "", 40);
  if (!text) return "";

  const normalized = text
    .replace(/年/g, "-")
    .replace(/月/g, "-")
    .replace(/日/g, "")
    .replace(/[/.]/g, "-")
    .replace(/\s+/g, "");
  const match = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(normalized);
  if (!match) {
    throw createHttpError(400, "Birthday must be YYYY-MM-DD");
  }

  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  const date = new Date(Date.UTC(year, month - 1, day));
  const today = new Date();
  const todayUtc = Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate());
  if (
    year < 1900
      || month < 1
      || month > 12
      || day < 1
      || day > 31
      || date.getUTCFullYear() !== year
      || date.getUTCMonth() !== month - 1
      || date.getUTCDate() !== day
      || date.getTime() > todayUtc
  ) {
    throw createHttpError(400, "Birthday is invalid");
  }

  return `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

function normalizeOptionalBirthTime(value) {
  const text = cleanString(value, "", 20);
  if (!text) return "";

  let normalized = text
    .replace(/[点时]/g, ":")
    .replace(/分/g, "")
    .replace(/\s+/g, "");
  if (normalized.endsWith(":")) {
    normalized += "00";
  }

  const match = /^(\d{1,2})(?::(\d{1,2}))?$/.exec(normalized);
  if (!match) {
    throw createHttpError(400, "Birth time must be HH:mm");
  }

  const hour = Number(match[1]);
  const minute = Number(match[2] || 0);
  if (hour < 0 || hour > 23 || minute < 0 || minute > 59) {
    throw createHttpError(400, "Birth time is invalid");
  }

  return `${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")}`;
}

function normalizeSearchText(value) {
  return String(value || "").trim().toLowerCase();
}

function addSearchKeyword(keywords, keyword) {
  if (!keyword) return;
  const normalized = normalizeSearchText(keyword).slice(0, 32);
  if (normalized) keywords.add(normalized);
}

function addSearchKeywordsForValue(keywords, rawValue) {
  const value = normalizeSearchText(rawValue);
  if (!value) return;

  addSearchKeyword(keywords, value);
  for (let len = 1; len <= value.length && keywords.size < 80; len += 1) {
    addSearchKeyword(keywords, value.slice(0, len));
  }

  for (let start = 0; start < value.length && keywords.size < 80; start += 1) {
    for (let len = 2; start + len <= value.length && keywords.size < 80; len += 1) {
      addSearchKeyword(keywords, value.slice(start, start + len));
    }
  }

  for (const token of value.split(/[ ._@-]+/).filter(Boolean)) {
    addSearchKeyword(keywords, token);
  }
}

function buildSearchKeywords(displayName, email) {
  const keywords = new Set();
  addSearchKeywordsForValue(keywords, displayName);
  addSearchKeywordsForValue(keywords, email);

  const emailLower = normalizeSearchText(email);
  const atIndex = emailLower.indexOf("@");
  if (atIndex > 0) {
    addSearchKeywordsForValue(keywords, emailLower.slice(0, atIndex));
  }

  return [...keywords];
}

function mergePublicConfig(data) {
  const source = data || {};
  const socialLinks = {
    ...DEFAULT_PUBLIC_CONFIG.socialLinks,
    ...(source.socialLinks || {}),
  };
  const iapProducts = {
    proMonthly: {
      ...DEFAULT_PUBLIC_CONFIG.iapProducts.proMonthly,
      ...(source.iapProducts?.proMonthly || {}),
    },
    proYearly: {
      ...DEFAULT_PUBLIC_CONFIG.iapProducts.proYearly,
      ...(source.iapProducts?.proYearly || {}),
    },
  };

  return {
    socialLinks,
    iapProducts,
    updatedAt: serializeTimestamp(source.updatedAt),
  };
}

function normalizeUrlForCompare(url) {
  return String(url || "").trim().replace(/\/+$/, "").toLowerCase();
}

function buildPublicConfigReadiness(config) {
  const socialLinks = config?.socialLinks || {};
  const placeholderSocialLinks = [];
  for (const [key, defaultUrl] of Object.entries(DEFAULT_PUBLIC_CONFIG.socialLinks)) {
    const current = normalizeUrlForCompare(socialLinks[key]);
    const placeholder = normalizeUrlForCompare(defaultUrl);
    if (!current || current === placeholder) {
      placeholderSocialLinks.push(key);
    }
  }

  const products = config?.iapProducts || {};
  const requiredProducts = ["proMonthly", "proYearly"];
  const missingProductIds = [];
  const missingPriceLabels = [];
  for (const key of requiredProducts) {
    if (!cleanString(products[key]?.productId, "", 120)) {
      missingProductIds.push(key);
    }
    if (!cleanString(products[key]?.priceLabel, "", 40)) {
      missingPriceLabels.push(key);
    }
  }

  return {
    configured: placeholderSocialLinks.length === 0 && missingProductIds.length === 0,
    socialLinksConfigured: placeholderSocialLinks.length === 0,
    placeholderSocialLinks,
    iapProductsConfigured: missingProductIds.length === 0,
    missingProductIds,
    missingPriceLabels,
    warnings: [
      placeholderSocialLinks.length ? `Placeholder social links: ${placeholderSocialLinks.join(", ")}` : "",
      missingPriceLabels.length ? `Missing price labels: ${missingPriceLabels.join(", ")}` : "",
    ].filter(Boolean),
  };
}

function serializeTimestamp(value) {
  if (!value) return null;
  if (typeof value.toDate === "function") return value.toDate().toISOString();
  if (value instanceof Date) return value.toISOString();
  return value;
}

function createHttpError(status, message) {
  const error = new Error(message);
  error.status = status;
  return error;
}

function normalizeFirestorePath(pathValue) {
  const pathValueText = String(pathValue || "").trim();
  const path = pathValueText.replace(/^\/+|\/+$/g, "").replace(/\/{2,}/g, "/");
  if (!path) {
    throw createHttpError(400, "Firestore path is required");
  }
  if (path.length > 1500 || path.includes("..")) {
    throw createHttpError(400, "Firestore path is invalid");
  }

  const segments = path.split("/");
  if (segments.some((segment) => !segment || segment === "." || segment === "..")) {
    throw createHttpError(400, "Firestore path has invalid segments");
  }
  return path;
}

function validateFirestorePath(pathValue, expectedKind) {
  const path = normalizeFirestorePath(pathValue);
  const segmentCount = path.split("/").length;
  const isCollectionPath = segmentCount % 2 === 1;
  if (expectedKind === "collection" && !isCollectionPath) {
    throw createHttpError(400, "Expected a collection path");
  }
  if (expectedKind === "document" && isCollectionPath) {
    throw createHttpError(400, "Expected a document path");
  }
  return path;
}

function validateDocumentId(docId) {
  const value = String(docId || "").trim();
  if (!value || value.includes("/") || value === "." || value === ".." || value.length > 1500) {
    throw createHttpError(400, "Document ID is invalid");
  }
  return value;
}

function getAdminCollectionDefinition(key) {
  return ADMIN_COLLECTION_DEFINITIONS.find((definition) => definition.key === key) || null;
}

function getAdminRequestSource(req) {
  return req.method === "GET" ? (req.query || {}) : (req.body || {});
}

function sanitizeAdminAuditValue(value, depth = 0) {
  if (depth > 6) return "[max-depth]";
  if (value === null || value === undefined) return null;
  if (typeof value === "string") return value.slice(0, 1200);
  if (typeof value === "number" || typeof value === "boolean") return value;
  if (value instanceof admin.firestore.Timestamp) return serializeTimestamp(value);
  if (value instanceof Date) return value.toISOString();
  if (Array.isArray(value)) {
    return value.slice(0, 40).map((item) => sanitizeAdminAuditValue(item, depth + 1));
  }
  if (typeof value === "object") {
    const result = {};
    for (const [key, child] of Object.entries(value).slice(0, 60)) {
      const lowerKey = key.toLowerCase();
      if (ADMIN_AUDIT_REDACTION_KEYS.some((redactionKey) => lowerKey.includes(redactionKey.toLowerCase()))) {
        result[key] = "[redacted]";
      } else {
        result[key] = sanitizeAdminAuditValue(child, depth + 1);
      }
    }
    return result;
  }
  return String(value).slice(0, 1200);
}

function getAdminAuditRequestInfo(req) {
  if (!req) return {};
  const forwardedFor = cleanString(req.get?.("x-forwarded-for"), "", 300);
  return {
    ip: cleanString(forwardedFor.split(",")[0] || req.ip, "", 80),
    userAgent: cleanString(req.get?.("user-agent"), "", 500),
    origin: cleanString(req.get?.("origin"), "", 500),
  };
}

async function writeAdminAuditLog(decoded, action, target = {}, details = {}, req = null) {
  try {
    const cleanAction = cleanString(action, "", 120);
    if (!decoded?.uid || !cleanAction) return;
    const cleanTarget = sanitizeAdminAuditValue(target);
    const targetType = getAdminScalarText(cleanTarget?.type, 120);
    const targetId = getAdminScalarText(cleanTarget?.id, 300);
    const targetPath = getAdminScalarText(cleanTarget?.path, 900);
    const targetUid = getAdminScalarText(cleanTarget?.uid, 200);
    await db.collection(ADMIN_AUDIT_LOG_COLLECTION).add({
      action: cleanAction,
      adminUid: decoded.uid,
      adminEmail: cleanString(decoded.email, "", 200),
      target: cleanTarget,
      targetType,
      targetId,
      targetPath,
      targetUid,
      details: sanitizeAdminAuditValue(details),
      request: getAdminAuditRequestInfo(req),
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
    });
  } catch (error) {
    console.warn("[adminAudit] write failed", error.message);
  }
}

function serializeAdminAuditLog(doc) {
  const data = doc.data() || {};
  return {
    id: doc.id,
    path: doc.ref.path,
    action: getAdminScalarText(data.action, 120),
    adminUid: getAdminScalarText(data.adminUid, 200),
    adminEmail: getAdminScalarText(data.adminEmail, 200),
    targetType: getAdminScalarText(data.targetType, 120),
    targetId: getAdminScalarText(data.targetId, 300),
    targetPath: getAdminScalarText(data.targetPath, 900),
    targetUid: getAdminScalarText(data.targetUid, 200),
    target: serializeFirestoreValue(data.target || {}),
    details: serializeFirestoreValue(data.details || {}),
    request: serializeFirestoreValue(data.request || {}),
    createdAt: serializeTimestamp(data.createdAt),
  };
}

function resolveAdminCollection(source) {
  const collectionKey = cleanString(source.collectionKey, "", 120);
  if (collectionKey) {
    const definition = getAdminCollectionDefinition(collectionKey);
    if (!definition) {
      throw createHttpError(400, "Unknown collectionKey");
    }

    if (definition.scope === "user") {
      const parentUid = validateDocumentId(source.parentUid);
      return {
        definition,
        collectionPath: `users/${parentUid}/${definition.collectionId}`,
        parentUid,
      };
    }

    return {
      definition,
      collectionPath: validateFirestorePath(definition.collectionPath, "collection"),
      parentUid: "",
    };
  }

  const collectionPath = cleanString(source.collectionPath, "", 800);
  if (!collectionPath) {
    throw createHttpError(400, "collectionKey or collectionPath is required");
  }

  return {
    definition: {
      key: "custom",
      label: collectionPath,
      collectionPath,
      scope: "custom",
      orderBy: "__name__",
      orderDirection: "asc",
      idHint: "documentId",
      custom: true,
    },
    collectionPath: validateFirestorePath(collectionPath, "collection"),
    parentUid: "",
  };
}

function resolveAdminDocumentRef(source) {
  const documentPath = cleanString(source.documentPath, "", 900);
  if (documentPath) {
    return db.doc(validateFirestorePath(documentPath, "document"));
  }

  const { collectionPath } = resolveAdminCollection(source);
  const docId = validateDocumentId(source.docId);
  return db.collection(collectionPath).doc(docId);
}

function parseJsonishValue(value) {
  if (value === undefined || value === null || value === "") return undefined;
  if (typeof value !== "string") return value;

  try {
    return JSON.parse(value);
  } catch (error) {
    return value;
  }
}

function getOrderByValue(fieldName) {
  if (!fieldName || fieldName === "__name__") {
    return admin.firestore.FieldPath.documentId();
  }
  if (!/^[A-Za-z0-9_.-]{1,160}$/.test(fieldName)) {
    throw createHttpError(400, "orderBy contains unsupported characters");
  }
  return fieldName;
}

function serializeFirestoreValue(value) {
  if (value === null || value === undefined) return null;
  if (value instanceof admin.firestore.Timestamp) {
    return {
      __type: "timestamp",
      value: value.toDate().toISOString(),
    };
  }
  if (value instanceof admin.firestore.GeoPoint) {
    return {
      __type: "geoPoint",
      latitude: value.latitude,
      longitude: value.longitude,
    };
  }
  if (value && typeof value === "object" && typeof value.path === "string" && value.firestore) {
    return {
      __type: "reference",
      path: value.path,
    };
  }
  if (Buffer.isBuffer(value) || value instanceof Uint8Array) {
    return {
      __type: "bytes",
      base64: Buffer.from(value).toString("base64"),
    };
  }
  if (Array.isArray(value)) {
    return value.map(serializeFirestoreValue);
  }
  if (typeof value === "object") {
    const result = {};
    for (const [key, child] of Object.entries(value)) {
      result[key] = serializeFirestoreValue(child);
    }
    return result;
  }
  if (typeof value === "bigint") return value.toString();
  return value;
}

function deserializeFirestoreValue(value) {
  if (value === null || value === undefined) return null;
  if (Array.isArray(value)) return value.map(deserializeFirestoreValue);
  if (typeof value !== "object") return value;

  const type = typeof value.__type === "string" ? value.__type : "";
  if (type === "timestamp") {
    const date = new Date(value.value || value.iso || "");
    if (Number.isNaN(date.getTime())) {
      throw createHttpError(400, "Invalid timestamp value");
    }
    return admin.firestore.Timestamp.fromDate(date);
  }
  if (type === "serverTimestamp") {
    return admin.firestore.FieldValue.serverTimestamp();
  }
  if (type === "deleteField") {
    return admin.firestore.FieldValue.delete();
  }
  if (type === "geoPoint") {
    const latitude = Number(value.latitude);
    const longitude = Number(value.longitude);
    if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) {
      throw createHttpError(400, "Invalid geoPoint value");
    }
    return new admin.firestore.GeoPoint(latitude, longitude);
  }
  if (type === "reference") {
    return db.doc(validateFirestorePath(value.path, "document"));
  }
  if (type === "bytes") {
    return Buffer.from(String(value.base64 || ""), "base64");
  }

  const result = {};
  for (const [key, child] of Object.entries(value)) {
    if (["__proto__", "constructor", "prototype"].includes(key)) {
      throw createHttpError(400, `Unsupported field key: ${key}`);
    }
    result[key] = deserializeFirestoreValue(child);
  }
  return result;
}

function serializeAdminDocument(doc) {
  return {
    id: doc.id,
    path: doc.ref.path,
    parentPath: doc.ref.parent.path,
    createTime: serializeTimestamp(doc.createTime),
    updateTime: serializeTimestamp(doc.updateTime),
    data: serializeFirestoreValue(doc.data() || {}),
  };
}

function getDocumentSummary(data) {
  const source = data || {};
  const preferred = [
    "displayName",
    "email",
    "question",
    "title",
    "status",
    "category",
    "tag",
    "productId",
    "membershipStatus",
    "date",
    "content",
  ];
  for (const key of preferred) {
    const value = source[key];
    if (value !== undefined && value !== null && typeof value !== "object" && String(value).trim()) {
      return String(value).slice(0, 140);
    }
  }
  return Object.keys(source).slice(0, 6).join(", ");
}

function getAdminScalarText(value, maxLength = 160) {
  if (value === undefined || value === null || typeof value === "object") return "";
  return String(value).trim().slice(0, maxLength);
}

async function getAdminRealtimePresenceByUid(uid) {
  const cleanUid = getAdminScalarText(uid, 160);
  if (!cleanUid || /[.#$[\]/]/.test(cleanUid)) return {};

  const snap = await realtimeDb.ref(`presence/${cleanUid}`).get().catch((error) => {
    console.warn("[adminPresence] realtime lookup failed", { uid: cleanUid, error: error.message });
    return null;
  });
  if (!snap?.exists()) return {};

  const data = snap.val() || {};
  const lastActiveUnixMs = Number(data.lastActiveUnixMs || data.updatedAt || 0) || 0;
  return {
    hasPresence: true,
    isOnline: data.isOnline === true,
    lastActiveUnixMs,
    presenceSource: "realtime_database",
  };
}

function getAdminPublicProfilePresence(publicProfileData = {}) {
  const hasPresence = Object.prototype.hasOwnProperty.call(publicProfileData, "isOnline")
    || Object.prototype.hasOwnProperty.call(publicProfileData, "lastActiveUnixMs")
    || Object.prototype.hasOwnProperty.call(publicProfileData, "lastActiveAt")
    || Object.prototype.hasOwnProperty.call(publicProfileData, "presenceUpdatedAt");
  if (!hasPresence) return {};

  const presence = {
    hasPresence: true,
    isOnline: publicProfileData.isOnline === true,
    lastActiveUnixMs: Number(publicProfileData.lastActiveUnixMs || 0) || 0,
    presenceSource: "public_profiles",
  };
  if (publicProfileData.lastActiveAt) {
    presence.lastActiveAt = serializeFirestoreValue(publicProfileData.lastActiveAt);
  }
  if (publicProfileData.presenceUpdatedAt) {
    presence.presenceUpdatedAt = serializeFirestoreValue(publicProfileData.presenceUpdatedAt);
  }
  return presence;
}

function chooseAdminPresence(realtimePresence = {}, publicProfilePresence = {}) {
  if (realtimePresence.hasPresence) return realtimePresence;
  if (publicProfilePresence.hasPresence) return publicProfilePresence;
  return {};
}

function getAdminPresenceDataForClient(presence = {}) {
  if (!presence.hasPresence) return {};
  const data = {
    isOnline: presence.isOnline === true,
    lastActiveUnixMs: Number(presence.lastActiveUnixMs || 0) || 0,
    presenceSource: getAdminScalarText(presence.presenceSource, 80),
  };
  if (presence.lastActiveAt) data.lastActiveAt = presence.lastActiveAt;
  if (presence.presenceUpdatedAt) data.presenceUpdatedAt = presence.presenceUpdatedAt;
  return data;
}

function shouldEnrichAdminUser(definition) {
  return [
    "users",
    "feedback",
    "user_feedback",
    "iap_receipts",
    "payment_events",
    "usage_limits",
  ].includes(definition.key);
}

async function getAdminUserProfilesByUid(uids) {
  const uniqueUids = [...new Set(uids.map((uid) => getAdminScalarText(uid)).filter(Boolean))];
  if (uniqueUids.length === 0) return new Map();

  const pairs = await Promise.all(uniqueUids.map(async (uid) => {
    const [userSnap, publicProfileSnap, realtimePresence] = await Promise.all([
      db.collection("users").doc(uid).get().catch(() => null),
      db.collection("public_profiles").doc(uid).get().catch(() => null),
      getAdminRealtimePresenceByUid(uid),
    ]);
    const user = userSnap?.exists ? userSnap.data() || {} : {};
    const publicProfile = publicProfileSnap?.exists ? publicProfileSnap.data() || {} : {};
    const presence = chooseAdminPresence(
      realtimePresence,
      getAdminPublicProfilePresence(publicProfile),
    );
    const displayName = getAdminScalarText(
      user.displayName
        || user.name
        || user.nickname
        || publicProfile.displayName
        || publicProfile.name
        || publicProfile.nickname,
      120,
    );
    const email = getAdminScalarText(user.email || publicProfile.email, 160);
    const photoUrl = getAdminScalarText(user.photoUrl || user.photoURL || publicProfile.photoUrl, 1000);
    return [uid, {
      displayName,
      email,
      photoUrl,
      hasPresence: presence.hasPresence === true,
      ...getAdminPresenceDataForClient(presence),
    }];
  }));

  return new Map(pairs);
}

function getAdminFriendDisplayName(data, fallback = "") {
  return getAdminScalarText(
    data.displayName
      || data.name
      || data.nickname
      || data.friendName
      || data.handle
      || fallback,
    120,
  );
}

function serializeAdminChatMessage(message, index, sessionId) {
  const data = message && typeof message === "object" ? message : {};
  return {
    sessionId,
    index,
    id: getAdminScalarText(data.id, 80) || String(index),
    role: getAdminScalarText(data.roleType || data.role || data.sender, 40),
    messageType: getAdminScalarText(data.messageType || data.type, 40),
    divinerType: getAdminScalarText(data.divinerType, 40),
    content: getAdminScalarText(data.content || data.text || data.message, 1400),
    friendName: getAdminScalarText(data.friendName, 120),
    friendContext: getAdminScalarText(data.friendContext, 700),
    createdAt: serializeTimestamp(data.createdAt || data.timestamp || data.time),
  };
}

function getAdminFriendTokens(friend) {
  const rawTokens = [
    friend.id,
    friend.uid,
    friend.firebaseUid,
    friend.virtualFriendId,
    friend.displayName,
    friend.name,
    friend.email,
    friend.handle,
    friend.uid ? `firebase:${friend.uid}` : "",
    friend.firebaseUid ? `firebase:${friend.firebaseUid}` : "",
    friend.virtualFriendId ? `virtual:${friend.virtualFriendId}` : "",
  ];
  return [...new Set(rawTokens
    .map((token) => String(token || "").trim().toLowerCase())
    .filter((token) => token.length >= 2))];
}

function adminTextContainsAnyToken(text, tokens) {
  const lower = String(text || "").toLowerCase();
  if (!lower) return false;
  return tokens.some((token) => lower.includes(token));
}

function buildAdminFriendChatPreview(friend, sessions, limit = 24) {
  const tokens = getAdminFriendTokens(friend);
  const matchedMessages = [];
  const matchedSessionIds = new Set();

  for (const session of sessions) {
    const sessionText = [
      session.id,
      session.activeFriendContext,
      session.activeRelationshipId,
    ].filter(Boolean).join("\n");
    const sessionMatches = adminTextContainsAnyToken(sessionText, tokens);
    const messages = Array.isArray(session.messages) ? session.messages : [];

    for (let index = 0; index < messages.length; index += 1) {
      const message = messages[index] || {};
      const messageText = [
        message.friendName,
        message.friendContext,
        message.content,
        message.divinationQuestion,
        message.oracleScene,
      ].filter(Boolean).join("\n");
      if (sessionMatches || adminTextContainsAnyToken(messageText, tokens)) {
        matchedSessionIds.add(session.id);
        matchedMessages.push(serializeAdminChatMessage(message, index, session.id));
      }
    }
  }

  return {
    sessionIds: [...matchedSessionIds],
    matchedCount: matchedMessages.length,
    messages: matchedMessages.slice(-limit),
  };
}

function serializeAdminDialogSession(doc) {
  const data = doc.data() || {};
  const messages = Array.isArray(data.messages) ? data.messages : [];
  const apiMessages = Array.isArray(data.apiMessages) ? data.apiMessages : [];
  return {
    id: doc.id,
    messageCount: messages.length,
    apiMessageCount: apiMessages.length,
    activeReadingId: getAdminScalarText(data.activeReadingId, 120),
    activeRelationshipId: getAdminScalarText(data.activeRelationshipId, 120),
    activeFriendContext: getAdminScalarText(data.activeFriendContext, 700),
    updatedAt: serializeTimestamp(data.updatedAt || data.savedAt || data.savedAtLocal),
    messages,
  };
}

async function getAdminUserSocialBundle(uid, options = {}) {
  const cleanUid = validateDocumentId(uid);
  const limit = getAdminLimit(options.limit, 12, 50);
  const baseRef = db.collection("users").doc(cleanUid);
  const [
    friendsSnap,
    friendRequestsSnap,
    virtualFriendsSnap,
    dialogSessionsSnap,
  ] = await Promise.all([
    baseRef.collection("friends").limit(100).get().catch(() => null),
    baseRef.collection("friend_requests").limit(100).get().catch(() => null),
    baseRef.collection("virtual_friends").limit(100).get().catch(() => null),
    baseRef.collection("dialog_sessions").limit(30).get().catch(() => null),
  ]);

  const friendUids = friendsSnap?.docs?.map((doc) => doc.id) || [];
  const requestUids = friendRequestsSnap?.docs?.map((doc) => doc.id) || [];
  const profilesByUid = await getAdminUserProfilesByUid([...friendUids, ...requestUids]);
  const sessions = (dialogSessionsSnap?.docs || []).map(serializeAdminDialogSession);

  const realFriends = (friendsSnap?.docs || [])
    .filter((doc) => {
      const data = doc.data() || {};
      return (getAdminScalarText(data.status, 40) || "friend") === "friend";
    })
    .map((doc) => {
      const data = doc.data() || {};
      const profile = profilesByUid.get(doc.id) || {};
      const displayName = getAdminFriendDisplayName(data, profile.displayName || doc.id);
      const friend = {
        type: "real",
        id: doc.id,
        uid: doc.id,
        displayName,
        email: getAdminScalarText(data.email || profile.email, 160),
        photoUrl: getAdminScalarText(data.photoUrl, 1000),
        status: getAdminScalarText(data.status, 40) || "friend",
        source: getAdminScalarText(data.source, 80),
        updatedAt: serializeTimestamp(data.updatedAt || data.createdAt),
      };
      return {
        ...friend,
        chat: buildAdminFriendChatPreview(friend, sessions, limit),
      };
    })
    .sort((a, b) => String(b.updatedAt || "").localeCompare(String(a.updatedAt || "")));

  const virtualFriends = (virtualFriendsSnap?.docs || [])
    .map((doc) => {
      const data = doc.data() || {};
      const displayName = getAdminFriendDisplayName(data, doc.id);
      const friend = {
        type: "virtual",
        id: doc.id,
        virtualFriendId: getAdminScalarText(data.virtualFriendId, 160) || doc.id,
        displayName,
        relationship: getAdminScalarText(data.relationship, 120),
        birthday: getAdminScalarText(data.birthday, 40),
        birthTime: getAdminScalarText(data.birthTime, 20),
        city: getAdminScalarText(data.city, 160),
        notes: getAdminScalarText(data.notes, 700),
        photoUrl: getAdminScalarText(data.avatarUrl || data.photoUrl, 1000),
        isDeleted: data.isDeleted === true,
        updatedAt: serializeTimestamp(data.updatedAt || data.createdAt),
      };
      return {
        ...friend,
        chat: buildAdminFriendChatPreview(friend, sessions, limit),
      };
    })
    .filter((friend) => !friend.isDeleted)
    .sort((a, b) => String(b.updatedAt || "").localeCompare(String(a.updatedAt || "")));

  const pendingSentRequests = (friendsSnap?.docs || [])
    .filter((doc) => {
      const data = doc.data() || {};
      return getAdminScalarText(data.status, 40) === "pendingSent";
    })
    .map((doc) => {
      const data = doc.data() || {};
      const profile = profilesByUid.get(doc.id) || {};
      return {
        direction: "sent",
        id: doc.id,
        uid: doc.id,
        displayName: getAdminFriendDisplayName(data, profile.displayName || doc.id),
        email: getAdminScalarText(data.email || profile.email, 160),
        photoUrl: getAdminScalarText(data.photoUrl || profile.photoUrl, 1000),
        status: getAdminScalarText(data.status, 40),
        source: getAdminScalarText(data.source, 80),
        note: getAdminScalarText(data.adminNote || data.message, 500),
        updatedAt: serializeTimestamp(data.updatedAt || data.createdAt),
      };
    });

  const pendingReceivedRequests = (friendRequestsSnap?.docs || [])
    .filter((doc) => {
      const data = doc.data() || {};
      return (getAdminScalarText(data.status, 40) || "pendingReceived") === "pendingReceived";
    })
    .map((doc) => {
      const data = doc.data() || {};
      const profile = profilesByUid.get(doc.id) || {};
      return {
        direction: "received",
        id: doc.id,
        uid: doc.id,
        displayName: getAdminFriendDisplayName(data, profile.displayName || doc.id),
        email: getAdminScalarText(data.email || profile.email, 160),
        photoUrl: getAdminScalarText(data.photoUrl || profile.photoUrl, 1000),
        status: getAdminScalarText(data.status, 40) || "pendingReceived",
        source: getAdminScalarText(data.source, 80),
        note: getAdminScalarText(data.adminNote || data.message, 500),
        updatedAt: serializeTimestamp(data.updatedAt || data.createdAt),
      };
    });

  return {
    realFriends,
    virtualFriends,
    pendingRequests: [...pendingReceivedRequests, ...pendingSentRequests]
      .sort((a, b) => String(b.updatedAt || "").localeCompare(String(a.updatedAt || ""))),
    dialogSessions: sessions.map((session) => ({
      id: session.id,
      messageCount: session.messageCount,
      apiMessageCount: session.apiMessageCount,
      activeReadingId: session.activeReadingId,
      activeRelationshipId: session.activeRelationshipId,
      activeFriendContext: session.activeFriendContext,
      updatedAt: session.updatedAt,
    })),
  };
}

async function enrichAdminDataItems(items, definition, parentUid = "") {
  if (!shouldEnrichAdminUser(definition) || items.length === 0) {
    return items;
  }

  const fallbackUid = getAdminScalarText(parentUid);
  const uids = items.map((item) => (
    getAdminScalarText(item.data?.uid)
      || getAdminScalarText(item.data?.userId)
      || getAdminScalarText(item.id)
      || fallbackUid
  ));
  const profilesByUid = await getAdminUserProfilesByUid(uids);

  return items.map((item) => {
    const data = item.data || {};
    const uid = getAdminScalarText(data.uid)
      || getAdminScalarText(data.userId)
      || getAdminScalarText(item.id)
      || fallbackUid;
    const profile = profilesByUid.get(uid) || {};
    const displayName = getAdminScalarText(
      data.displayName
        || data.name
        || data.nickname
        || profile.displayName,
      120,
    );
    const email = getAdminScalarText(data.email || profile.email, 160);
    const nextData = { ...data };

    if (uid && !nextData.uid) nextData.uid = uid;
    if (displayName) {
      nextData.displayName = displayName;
      nextData.userDisplayName = displayName;
    }
    if (email) {
      nextData.email = email;
      nextData.userEmail = email;
    }
    if (profile.photoUrl && !nextData.photoUrl) nextData.photoUrl = profile.photoUrl;
    if (profile.hasPresence) {
      nextData.isOnline = profile.isOnline === true;
      nextData.lastActiveUnixMs = profile.lastActiveUnixMs || 0;
      if (profile.lastActiveAt) nextData.lastActiveAt = profile.lastActiveAt;
      if (profile.presenceUpdatedAt) nextData.presenceUpdatedAt = profile.presenceUpdatedAt;
      nextData.presenceSource = profile.presenceSource || "public_profiles";
    }

    return {
      ...item,
      data: nextData,
      summary: getDocumentSummary(nextData),
    };
  });
}

function buildAdminUserItem(uid, firestoreSnap, authRecord = null, publicProfileData = {}, presenceOverride = {}) {
  const presence = chooseAdminPresence(
    presenceOverride,
    getAdminPublicProfilePresence(publicProfileData),
  );
  const presenceData = getAdminPresenceDataForClient(presence);
  const authData = authRecord ? {
    email: getAdminScalarText(authRecord.email, 160),
    displayName: getAdminScalarText(authRecord.displayName, 120),
    photoUrl: getAdminScalarText(authRecord.photoURL, 500),
    emailVerified: authRecord.emailVerified === true,
    disabled: authRecord.disabled === true,
    authCreatedAt: getAdminScalarText(authRecord.metadata?.creationTime, 80),
    lastSignInAt: getAdminScalarText(authRecord.metadata?.lastSignInTime, 80),
  } : {};

  if (firestoreSnap?.exists) {
    const item = serializeAdminDocument(firestoreSnap);
    const data = { ...authData, ...(item.data || {}), ...presenceData };
    return {
      ...item,
      data,
      summary: getDocumentSummary(data),
    };
  }

  const data = {
    uid,
    ...authData,
    ...presenceData,
    authOnly: true,
  };
  return {
    id: uid,
    path: `users/${uid}`,
    parentPath: "users",
    createTime: authData.authCreatedAt || null,
    updateTime: authData.lastSignInAt || null,
    data,
    summary: getDocumentSummary(data),
  };
}

async function addAdminUserByUid(target, uid, authRecord = null) {
  const cleanUid = getAdminScalarText(uid, 160);
  if (!cleanUid || target.has(cleanUid)) return;

  const [snap, publicProfileSnap, realtimePresence] = await Promise.all([
    db.collection("users").doc(cleanUid).get().catch(() => null),
    db.collection("public_profiles").doc(cleanUid).get().catch(() => null),
    getAdminRealtimePresenceByUid(cleanUid),
  ]);
  if (snap?.exists || authRecord) {
    target.set(cleanUid, buildAdminUserItem(
      cleanUid,
      snap,
      authRecord,
      publicProfileSnap?.exists ? publicProfileSnap.data() || {} : {},
      realtimePresence,
    ));
  }
}

async function addAdminUserDocsFromQuery(target, query) {
  const snap = await query.get().catch((error) => {
    console.warn("[adminUserSearch] query failed", error.message);
    return null;
  });
  if (!snap) return;

  for (const doc of snap.docs) {
    if (!target.has(doc.id)) {
      const [publicProfileSnap, realtimePresence] = await Promise.all([
        db.collection("public_profiles").doc(doc.id).get().catch(() => null),
        getAdminRealtimePresenceByUid(doc.id),
      ]);
      target.set(doc.id, buildAdminUserItem(
        doc.id,
        doc,
        null,
        publicProfileSnap?.exists ? publicProfileSnap.data() || {} : {},
        realtimePresence,
      ));
    }
  }
}

function getAdminSortableTime(value) {
  if (!value) return 0;
  if (typeof value.toDate === "function") return value.toDate().getTime();
  if (value instanceof Date) return value.getTime();
  if (typeof value === "number") return Number.isFinite(value) ? value : 0;
  if (typeof value === "object") {
    if (value.value) return getAdminSortableTime(value.value);
    if (value.seconds) return Number(value.seconds || 0) * 1000;
  }

  const parsed = Date.parse(String(value));
  return Number.isNaN(parsed) ? 0 : parsed;
}

function getAdminUserListSortTime(item) {
  const data = item.data || {};
  return Math.max(
    getAdminSortableTime(data.lastActiveUnixMs),
    getAdminSortableTime(data.lastSignInAt),
    getAdminSortableTime(data.presenceUpdatedAt),
    getAdminSortableTime(data.lastActiveAt),
    getAdminSortableTime(data.profileUpdatedAt),
    getAdminSortableTime(data.updatedAt),
    getAdminSortableTime(data.createdAt),
    getAdminSortableTime(data.authCreatedAt),
    getAdminSortableTime(item.updateTime),
    getAdminSortableTime(item.createTime),
  );
}

function sortAdminUserListItems(items) {
  return [...items].sort((a, b) => {
    const timeDiff = getAdminUserListSortTime(b) - getAdminUserListSortTime(a);
    if (timeDiff) return timeDiff;
    const aName = getAdminScalarText(a.data?.displayName || a.data?.email || a.id, 200);
    const bName = getAdminScalarText(b.data?.displayName || b.data?.email || b.id, 200);
    return aName.localeCompare(bName);
  });
}

async function listAdminUsersForAdmin(options = {}) {
  const limit = getAdminLimit(options.limit, 80, options.max || 200);
  const fetchLimit = getAdminLimit(options.fetchLimit, Math.max(limit * 4, 200), 1000);
  const usersRef = db.collection("users");
  const results = new Map();

  await Promise.all([
    addAdminUserDocsFromQuery(
      results,
      usersRef.orderBy(admin.firestore.FieldPath.documentId()).limit(fetchLimit),
    ),
    addAdminUserDocsFromQuery(results, usersRef.orderBy("lastSignInAt", "desc").limit(limit)),
    addAdminUserDocsFromQuery(results, usersRef.orderBy("updatedAt", "desc").limit(limit)),
    addAdminUserDocsFromQuery(results, usersRef.orderBy("createdAt", "desc").limit(limit)),
  ]);

  return sortAdminUserListItems([...results.values()]).slice(0, limit);
}

function getAuthRecordProviderLabel(authRecord) {
  const providers = Array.isArray(authRecord?.providerData)
    ? authRecord.providerData.map((provider) => getAdminScalarText(provider.providerId, 80)).filter(Boolean)
    : [];
  if (providers.some((provider) => provider.includes("google"))) return "Google";
  if (providers.some((provider) => provider.includes("apple"))) return "Apple";
  if (providers.some((provider) => provider.includes("password"))) return "Email";
  if (providers.length) return providers.join(", ");
  return authRecord?.email ? "Email" : "Unknown";
}

function authTimestamp(value) {
  const parsed = Date.parse(String(value || ""));
  if (!Number.isFinite(parsed)) return admin.firestore.FieldValue.serverTimestamp();
  return admin.firestore.Timestamp.fromDate(new Date(parsed));
}

function buildAdminAuthUserSnapshot(authRecord, userSnap, publicProfileSnap) {
  const authData = {
    uid: authRecord.uid,
    email: getAdminScalarText(authRecord.email, 160),
    displayName: getAdminScalarText(authRecord.displayName, 120),
    photoUrl: getAdminScalarText(authRecord.photoURL, 1000),
    emailVerified: authRecord.emailVerified === true,
    disabled: authRecord.disabled === true,
    loginType: getAuthRecordProviderLabel(authRecord),
    createdAt: getAdminScalarText(authRecord.metadata?.creationTime, 80),
    lastSignInAt: getAdminScalarText(authRecord.metadata?.lastSignInTime, 80),
    providerIds: Array.isArray(authRecord.providerData)
      ? authRecord.providerData.map((provider) => getAdminScalarText(provider.providerId, 80)).filter(Boolean)
      : [],
  };

  const hasUserDoc = userSnap?.exists === true;
  const hasPublicProfile = publicProfileSnap?.exists === true;
  return {
    ...authData,
    hasUserDoc,
    hasPublicProfile,
    missingUserDoc: !hasUserDoc,
    missingPublicProfile: !hasPublicProfile,
    needsRepair: !hasUserDoc || !hasPublicProfile,
  };
}

async function listAdminAuthUsersForReconcile(options = {}) {
  const limit = getAdminLimit(options.limit, 100, 1000);
  const pageToken = cleanString(options.pageToken, "", 1000) || undefined;
  const onlyMissing = options.onlyMissing === true || String(options.onlyMissing || "") === "true";
  const authList = await admin.auth().listUsers(limit, pageToken);
  const refs = [];
  for (const authRecord of authList.users) {
    refs.push(db.collection("users").doc(authRecord.uid));
    refs.push(db.collection("public_profiles").doc(authRecord.uid));
  }
  const snaps = refs.length ? await db.getAll(...refs) : [];
  const items = authList.users.map((authRecord, index) => {
    const userSnap = snaps[index * 2] || null;
    const publicProfileSnap = snaps[index * 2 + 1] || null;
    return buildAdminAuthUserSnapshot(authRecord, userSnap, publicProfileSnap);
  });
  const filteredItems = onlyMissing ? items.filter((item) => item.needsRepair) : items;

  return {
    items: filteredItems,
    scannedCount: authList.users.length,
    count: filteredItems.length,
    missingCount: items.filter((item) => item.needsRepair).length,
    nextPageToken: authList.pageToken || "",
    onlyMissing,
  };
}

function buildAdminUserRepairDocsFromAuth(authRecord, decoded, existingUser = {}, existingPublicProfile = {}) {
  const email = getAdminScalarText(existingUser.email || existingPublicProfile.email || authRecord.email, 160).toLowerCase();
  const displayName = cleanString(
    existingUser.displayName || existingPublicProfile.displayName || authRecord.displayName,
    email.includes("@") ? email.split("@")[0] : "Fari User",
    120,
  );
  const photoUrl = getAdminScalarText(
    existingUser.photoUrl || existingUser.photoURL || existingPublicProfile.photoUrl || authRecord.photoURL,
    1000,
  );
  const displayNameLower = normalizeSearchText(displayName);
  const emailLower = normalizeSearchText(email);
  const searchKeywords = buildSearchKeywords(displayName, email);
  const serverTimestamp = admin.firestore.FieldValue.serverTimestamp();
  const createdAt = authTimestamp(authRecord.metadata?.creationTime);
  const loginType = getAuthRecordProviderLabel(authRecord);

  return {
    userData: {
      uid: authRecord.uid,
      displayName,
      displayNameLower,
      searchKeywords,
      email,
      emailLower,
      photoUrl,
      avatarStoragePath: getAdminScalarText(existingUser.avatarStoragePath, 1000),
      birthday: getAdminScalarText(existingUser.birthday, 40),
      birthTime: getAdminScalarText(existingUser.birthTime, 20),
      city: getAdminScalarText(existingUser.city || existingUser.address, 160),
      bio: getAdminScalarText(existingUser.bio || existingPublicProfile.bio, 500),
      loginType,
      isEmailVerified: authRecord.emailVerified === true,
      membershipStatus: getAdminScalarText(existingUser.membershipStatus, 40) || "free",
      timezone: getAdminScalarText(existingUser.timezone, 80),
      createdAt,
      profileUpdatedAt: serverTimestamp,
      updatedAt: serverTimestamp,
      adminRepaired: true,
      adminRepairedBy: decoded.uid,
      adminRepairedAt: serverTimestamp,
    },
    publicProfileData: {
      uid: authRecord.uid,
      displayName,
      displayNameLower,
      searchKeywords,
      email,
      emailLower,
      photoUrl,
      avatarStoragePath: getAdminScalarText(existingPublicProfile.avatarStoragePath || existingUser.avatarStoragePath, 1000),
      bio: getAdminScalarText(existingPublicProfile.bio || existingUser.bio, 500),
      updatedAt: serverTimestamp,
      adminRepaired: true,
      adminRepairedBy: decoded.uid,
    },
  };
}

function getAdminLimit(value, fallback = 20, max = 100) {
  const parsed = Number(value || fallback);
  if (!Number.isFinite(parsed)) return fallback;
  return Math.max(1, Math.min(Math.floor(parsed), max));
}

async function getCollectionCount(query, label) {
  try {
    const snap = await query.count().get();
    return Number(snap.data().count || 0);
  } catch (error) {
    console.warn(`[${label || "count"}] aggregate count failed, using capped fallback`, error.message);
    const snap = await query.limit(1000).get();
    return snap.size;
  }
}

async function getRecentAdminDocs(collectionPath, options = {}) {
  const limit = getAdminLimit(options.limit, 20, options.max || 100);
  const orderBy = options.orderBy || "updatedAt";
  const direction = options.direction === "asc" ? "asc" : "desc";
  let snap;

  try {
    snap = await db.collection(collectionPath).orderBy(getOrderByValue(orderBy), direction).limit(limit).get();
  } catch (error) {
    console.warn(`[admin] recent ${collectionPath} ordered query failed`, error.message);
    snap = await db.collection(collectionPath).limit(limit).get();
  }

  return snap.docs.map((doc) => {
    const item = serializeAdminDocument(doc);
    return { ...item, summary: getDocumentSummary(item.data) };
  });
}

async function getAdminDocsByUid(collectionPath, uid, options = {}) {
  const limit = getAdminLimit(options.limit, 10, options.max || 50);
  const orderBy = options.orderBy || "updatedAt";
  const direction = options.direction === "asc" ? "asc" : "desc";
  let query = db.collection(collectionPath).where("uid", "==", uid);
  let snap;

  try {
    snap = await query.orderBy(orderBy, direction).limit(limit).get();
  } catch (error) {
    console.warn(`[admin] ${collectionPath} uid query failed with order`, error.message);
    snap = await query.limit(limit).get();
  }

  return snap.docs.map((doc) => {
    const item = serializeAdminDocument(doc);
    return { ...item, summary: getDocumentSummary(item.data) };
  });
}

async function getAuthRecordSafely(uid) {
  if (!uid) return null;
  return admin.auth().getUser(uid).catch((error) => {
    if (error.code !== "auth/user-not-found") {
      console.warn("[admin] auth user lookup failed", error.message);
    }
    return null;
  });
}

async function resolveAdminUserUid(source) {
  const explicitUid = cleanString(source.uid, "", 160);
  if (explicitUid) return validateDocumentId(explicitUid);

  const explicitEmail = cleanString(source.email, "", 160).toLowerCase();
  if (explicitEmail) {
    const authRecord = await admin.auth().getUserByEmail(explicitEmail).catch(() => null);
    if (authRecord?.uid) return authRecord.uid;

    const snap = await db.collection("users").where("emailLower", "==", explicitEmail).limit(1).get();
    if (!snap.empty) return snap.docs[0].id;
  }

  const q = cleanString(source.q || source.search, "", 200);
  if (!q) return "";

  if (q.includes("@")) {
    const authRecord = await admin.auth().getUserByEmail(q.toLowerCase()).catch(() => null);
    if (authRecord?.uid) return authRecord.uid;

    const emailSnap = await db.collection("users")
      .where("emailLower", "==", q.toLowerCase())
      .limit(1)
      .get()
      .catch(() => null);
    if (emailSnap && !emailSnap.empty) return emailSnap.docs[0].id;
  }

  if (!q.includes("/") && q !== "." && q !== ".." && q.length <= 1500) {
    const userSnap = await db.collection("users").doc(q).get().catch(() => null);
    if (userSnap?.exists) return q;

    const authRecord = await getAuthRecordSafely(q);
    if (authRecord?.uid) return authRecord.uid;
  }

  const qLower = q.toLowerCase();
  const profileSnaps = await Promise.all([
    db.collection("users").where("displayNameLower", "==", qLower).limit(1).get().catch(() => null),
    db.collection("users").where("displayName", "==", q).limit(1).get().catch(() => null),
    db.collection("public_profiles").where("displayNameLower", "==", qLower).limit(1).get().catch(() => null),
  ]);
  for (const snap of profileSnaps) {
    if (snap && !snap.empty) return snap.docs[0].id;
  }

  return "";
}

async function resolveUidByTransactionId(transactionId) {
  const cleanTransactionId = cleanString(transactionId, "", 220);
  if (!cleanTransactionId) return "";

  const directSnaps = await Promise.all([
    db.collection("iap_receipts").doc(cleanTransactionId).get().catch(() => null),
    db.collection("payment_events").doc(cleanTransactionId).get().catch(() => null),
  ]);
  for (const snap of directSnaps) {
    if (snap?.exists && getAdminScalarText(snap.data()?.uid)) {
      return getAdminScalarText(snap.data().uid);
    }
  }

  const querySnaps = await Promise.all([
    db.collection("iap_receipts").where("transactionId", "==", cleanTransactionId).limit(1).get().catch(() => null),
    db.collection("iap_receipts").where("originalTransactionId", "==", cleanTransactionId).limit(1).get().catch(() => null),
    db.collection("payment_events").where("transactionId", "==", cleanTransactionId).limit(1).get().catch(() => null),
    db.collection("payment_events").where("eventId", "==", cleanTransactionId).limit(1).get().catch(() => null),
  ]);

  for (const snap of querySnaps) {
    if (snap && !snap.empty) {
      return getAdminScalarText(snap.docs[0].data()?.uid);
    }
  }

  return "";
}

function buildAdminMembershipSummary(userData) {
  const data = userData || {};
  const rawStatus = getAdminScalarText(data.membershipStatus, 40) || "free";
  const expiresAt = data.proExpiresAt || null;
  const expiresAtMillis = expiresAt && typeof expiresAt.toMillis === "function"
    ? expiresAt.toMillis()
    : Date.parse(String(expiresAt || ""));
  const hasValidExpiry = Number.isFinite(expiresAtMillis);
  const isPro = rawStatus === "pro" && (!expiresAt || (hasValidExpiry && expiresAtMillis > Date.now()));

  return {
    status: isPro ? "pro" : "free",
    rawStatus,
    isPro,
    proExpiresAt: serializeTimestamp(expiresAt),
    provider: getAdminScalarText(data.membershipProvider, 80),
    productId: getAdminScalarText(data.membershipProductId, 160),
    updatedAt: serializeTimestamp(data.membershipUpdatedAt),
    manualProGrantedBy: getAdminScalarText(data.manualProGrantedBy, 160),
    manualProGrantReason: getAdminScalarText(data.manualProGrantReason, 500),
  };
}

async function buildAdminUserBundle(uid, options = {}) {
  const cleanUid = validateDocumentId(uid);
  const limit = getAdminLimit(options.limit, 10, 50);
  const [
    authRecord,
    userSnap,
    publicProfileSnap,
    realtimePresence,
    feedback,
    receipts,
    paymentEvents,
    usage,
    social,
  ] = await Promise.all([
    getAuthRecordSafely(cleanUid),
    db.collection("users").doc(cleanUid).get(),
    db.collection("public_profiles").doc(cleanUid).get().catch(() => null),
    getAdminRealtimePresenceByUid(cleanUid),
    getAdminDocsByUid("feedback", cleanUid, { orderBy: "createdAt", limit }),
    getAdminDocsByUid("iap_receipts", cleanUid, { orderBy: "updatedAt", limit }),
    getAdminDocsByUid("payment_events", cleanUid, { orderBy: "createdAt", limit }),
    getAdminDocsByUid("usage_limits", cleanUid, { orderBy: "updatedAt", limit: Math.min(limit * 2, 50) }),
    getAdminUserSocialBundle(cleanUid, { limit }),
  ]);

  if (!userSnap.exists && !authRecord) {
    throw createHttpError(404, "User not found");
  }

  const publicProfile = publicProfileSnap?.exists
    ? { ...serializeAdminDocument(publicProfileSnap), summary: getDocumentSummary(publicProfileSnap.data() || {}) }
    : null;

  return {
    uid: cleanUid,
    user: buildAdminUserItem(
      cleanUid,
      userSnap,
      authRecord,
      publicProfileSnap?.exists ? publicProfileSnap.data() || {} : {},
      realtimePresence,
    ),
    auth: authRecord ? {
      uid: authRecord.uid,
      email: getAdminScalarText(authRecord.email, 160),
      displayName: getAdminScalarText(authRecord.displayName, 120),
      photoUrl: getAdminScalarText(authRecord.photoURL, 500),
      emailVerified: authRecord.emailVerified === true,
      disabled: authRecord.disabled === true,
      createdAt: getAdminScalarText(authRecord.metadata?.creationTime, 80),
      lastSignInAt: getAdminScalarText(authRecord.metadata?.lastSignInTime, 80),
    } : null,
    membership: buildAdminMembershipSummary(userSnap.exists ? userSnap.data() || {} : {}),
    publicProfile,
    feedback,
    receipts,
    paymentEvents,
    usage,
    social,
  };
}

function readAgentAdminConfig() {
  try {
    const parsed = JSON.parse(fs.readFileSync(AGENT_ADMIN_CONFIG_PATH, "utf8"));
    return {
      version: Number(parsed.version || 1),
      defaultProductId: getAdminScalarText(parsed.defaultProductId, 120) || "nocturne-oracle",
      products: Array.isArray(parsed.products) ? parsed.products : [],
    };
  } catch (error) {
    console.warn("[adminAgent] failed to read config", error.message);
    return {
      version: 1,
      defaultProductId: "nocturne-oracle",
      products: [],
    };
  }
}

function getAgentProduct(productId = "") {
  const config = readAgentAdminConfig();
  const cleanProductId = getAdminScalarText(productId, 120) || config.defaultProductId;
  const product = config.products.find((item) => item.productId === cleanProductId)
    || config.products.find((item) => item.productId === config.defaultProductId)
    || null;
  if (!product) {
    throw createHttpError(404, "Agent product not found");
  }
  return { config, product };
}

function getAgentProductId(source = {}) {
  const { config, product } = getAgentProduct(source.productId || source.product || "");
  return product.productId || config.defaultProductId;
}

function getAgentPromptRoot(product) {
  const promptRoot = getAdminScalarText(product.promptRoot, 240);
  if (promptRoot && !path.isAbsolute(promptRoot) && !promptRoot.includes("..")) {
    return path.join(__dirname, promptRoot);
  }
  return path.join(AGENT_PROMPT_ROOT, product.productId);
}

function normalizeAgentPromptPath(value) {
  const promptPath = cleanString(value, "", 500)
    .replace(/\\/g, "/")
    .replace(/^\/+/, "");
  if (
    !promptPath
      || promptPath.includes("\0")
      || promptPath.includes("*")
      || promptPath.split("/").some((segment) => !segment || segment === "." || segment === "..")
  ) {
    throw createHttpError(400, "Prompt path is invalid");
  }
  return promptPath;
}

function getAgentPromptDocId(productId, promptPath) {
  return crypto.createHash("sha256").update(`${productId}|${promptPath}`).digest("hex");
}

function getAgentTurnRatingDocId(turnId) {
  return crypto.createHash("sha256").update(String(turnId || "")).digest("hex");
}

function encodeAgentToken(payload) {
  return Buffer.from(JSON.stringify(payload), "utf8").toString("base64url");
}

function decodeAgentToken(token) {
  try {
    const parsed = JSON.parse(Buffer.from(String(token || ""), "base64url").toString("utf8"));
    if (!parsed || typeof parsed !== "object") throw new Error("empty token");
    return parsed;
  } catch (error) {
    throw createHttpError(400, "Agent record id is invalid");
  }
}

function listBundledAgentPromptFiles(product) {
  const root = getAgentPromptRoot(product);
  if (!fs.existsSync(root)) return [];

  const files = [];
  const walk = (directory, prefix = "") => {
    const entries = fs.readdirSync(directory, { withFileTypes: true })
      .sort((a, b) => a.name.localeCompare(b.name));
    for (const entry of entries) {
      if (entry.name.startsWith(".")) continue;
      const fullPath = path.join(directory, entry.name);
      const relativePath = prefix ? `${prefix}/${entry.name}` : entry.name;
      if (entry.isDirectory()) {
        walk(fullPath, relativePath);
      } else if (entry.isFile()) {
        const stats = fs.statSync(fullPath);
        files.push({
          path: relativePath,
          size: stats.size,
          updatedAt: stats.mtime.toISOString(),
          source: "bundle",
          editable: !relativePath.startsWith("user_memory/"),
        });
      }
    }
  };
  walk(root);
  return files;
}

function readBundledAgentPromptFile(product, promptPath) {
  const root = getAgentPromptRoot(product);
  const fullPath = path.join(root, promptPath);
  const relative = path.relative(root, fullPath);
  if (relative.startsWith("..") || path.isAbsolute(relative)) {
    throw createHttpError(400, "Prompt path is invalid");
  }
  if (!fs.existsSync(fullPath) || !fs.statSync(fullPath).isFile()) {
    return null;
  }
  return {
    content: fs.readFileSync(fullPath, "utf8"),
    source: "bundle",
    updatedAt: fs.statSync(fullPath).mtime.toISOString(),
  };
}

async function getPublishedAgentPrompt(product, promptPath) {
  const productId = product.productId;
  const docId = getAgentPromptDocId(productId, promptPath);
  const snap = await db.collection(AGENT_PROMPT_FILES_COLLECTION).doc(docId).get().catch(() => null);
  if (snap?.exists) {
    const data = snap.data() || {};
    return {
      path: promptPath,
      content: getAdminScalarText(data.content, 200000),
      source: "firestore",
      updatedAt: serializeTimestamp(data.updatedAt || data.publishedAt),
      publishedBy: getAdminScalarText(data.publishedBy, 160),
    };
  }

  const bundled = readBundledAgentPromptFile(product, promptPath);
  if (!bundled) {
    throw createHttpError(404, "Prompt file not found");
  }
  return {
    path: promptPath,
    ...bundled,
  };
}

async function listAgentPromptFiles(product) {
  const bundled = listBundledAgentPromptFiles(product);
  const byPath = new Map(bundled.map((item) => [item.path, item]));
  const snap = await db.collection(AGENT_PROMPT_FILES_COLLECTION)
    .where("productId", "==", product.productId)
    .limit(200)
    .get()
    .catch((error) => {
      console.warn("[adminAgent] prompt file query failed", error.message);
      return null;
    });

  for (const doc of snap?.docs || []) {
    const data = doc.data() || {};
    const promptPath = getAdminScalarText(data.path, 500);
    if (!promptPath) continue;
    const existing = byPath.get(promptPath) || {};
    byPath.set(promptPath, {
      ...existing,
      id: doc.id,
      path: promptPath,
      size: String(data.content || "").length,
      updatedAt: serializeTimestamp(data.updatedAt || data.publishedAt),
      source: existing.source ? "bundle+firestore" : "firestore",
      editable: !promptPath.startsWith("user_memory/"),
      publishedBy: getAdminScalarText(data.publishedBy, 160),
    });
  }

  return [...byPath.values()].sort((a, b) => a.path.localeCompare(b.path));
}

function serializeAgentPromptDraft(doc) {
  const data = doc.data() || {};
  return {
    draftId: doc.id,
    productId: getAdminScalarText(data.productId, 120),
    path: getAdminScalarText(data.path, 500),
    status: getAdminScalarText(data.status, 40) || "draft",
    title: getAdminScalarText(data.title, 160),
    note: getAdminScalarText(data.note, 500),
    createdBy: getAdminScalarText(data.createdBy, 160),
    approvedBy: getAdminScalarText(data.approvedBy, 160),
    publishedBy: getAdminScalarText(data.publishedBy, 160),
    contentPreview: getAdminScalarText(data.content, 220),
    createdAt: serializeTimestamp(data.createdAt),
    updatedAt: serializeTimestamp(data.updatedAt),
    approvedAt: serializeTimestamp(data.approvedAt),
    publishedAt: serializeTimestamp(data.publishedAt),
  };
}

function serializeAgentPromptRelease(doc) {
  const data = doc.data() || {};
  return {
    releaseId: doc.id,
    productId: getAdminScalarText(data.productId, 120),
    path: getAdminScalarText(data.path, 500),
    type: getAdminScalarText(data.type, 40) || "publish",
    draftId: getAdminScalarText(data.draftId, 160),
    releasedBy: getAdminScalarText(data.releasedBy, 160),
    note: getAdminScalarText(data.note, 500),
    createdAt: serializeTimestamp(data.createdAt || data.releasedAt),
  };
}

async function listAgentPromptDrafts(productId, promptPath = "") {
  const draftQuery = db.collection(AGENT_PROMPT_DRAFTS_COLLECTION).where("productId", "==", productId);
  const releaseQuery = db.collection(AGENT_PROMPT_RELEASES_COLLECTION).where("productId", "==", productId);
  const [draftSnap, releaseSnap] = await Promise.all([
    draftQuery.limit(80).get().catch(() => null),
    releaseQuery.limit(80).get().catch(() => null),
  ]);
  const drafts = (draftSnap?.docs || [])
    .map(serializeAgentPromptDraft)
    .filter((draft) => !promptPath || draft.path === promptPath)
    .sort((a, b) => String(b.updatedAt || b.createdAt || "").localeCompare(String(a.updatedAt || a.createdAt || "")));
  const releases = (releaseSnap?.docs || [])
    .map(serializeAgentPromptRelease)
    .filter((release) => !promptPath || release.path === promptPath)
    .sort((a, b) => String(b.createdAt || "").localeCompare(String(a.createdAt || "")));
  return { drafts, releases };
}

function normalizeAgentRole(message = {}) {
  const raw = getAdminScalarText(message.roleType || message.role || message.sender || message.author, 40).toLowerCase();
  if (raw.includes("user") || raw === "human") return "user";
  if (raw.includes("assistant") || raw.includes("ai") || raw.includes("tarot") || raw.includes("astro")) return "assistant";
  return raw || "system";
}

function getAgentMessageText(message = {}, maxLength = 4000) {
  const keys = [
    "content",
    "text",
    "message",
    "divinationQuestion",
    "shortVerdict",
    "judgeContent",
    "adviceContent",
    "friendContext",
    "assistantContent",
  ];
  for (const key of keys) {
    const value = message?.[key];
    if (value !== undefined && value !== null && typeof value !== "object" && String(value).trim()) {
      return String(value).trim().slice(0, maxLength);
    }
  }
  return "";
}

function getAgentSessionHaystack(session = {}) {
  const data = session.data || session;
  const messages = Array.isArray(data.messages) ? data.messages : [];
  return [
    session.id,
    data.activeReadingId,
    data.activeRelationshipId,
    data.activeFriendContext,
    data.divinationScene,
    data.oracleScene,
    ...messages.slice(-10).flatMap((message) => [
      message?.messageType,
      message?.divinerType,
      message?.divinationScene,
      message?.oracleScene,
      message?.readingId,
      getAgentMessageText(message, 300),
    ]),
  ].filter(Boolean).join(" ").toLowerCase();
}

function classifyAgentCategory(product, source = {}) {
  const categories = Array.isArray(product.categories) ? product.categories : [];
  const haystack = typeof source === "string" ? source.toLowerCase() : getAgentSessionHaystack(source);
  for (const category of categories) {
    const patterns = Array.isArray(category.patterns) ? category.patterns : [];
    if (patterns.some((pattern) => haystack.includes(String(pattern).toLowerCase()))) {
      return category;
    }
  }
  return categories.find((category) => category.key === "chat_companion") || categories[0] || {
    key: "chat_companion",
    label: "普通对话",
  };
}

function serializeAgentUserFromItem(item) {
  const data = item.data || {};
  const presence = getAdminPresenceDataForClient({
    hasPresence: Object.prototype.hasOwnProperty.call(data, "isOnline")
      || Object.prototype.hasOwnProperty.call(data, "lastActiveUnixMs"),
    isOnline: data.isOnline === true,
    lastActiveUnixMs: Number(data.lastActiveUnixMs || 0) || 0,
    presenceSource: getAdminScalarText(data.presenceSource, 80),
    lastActiveAt: data.lastActiveAt,
    presenceUpdatedAt: data.presenceUpdatedAt,
  });
  return {
    uid: item.id || getAdminScalarText(data.uid, 160),
    displayName: getAdminScalarText(data.displayName || data.name || data.nickname, 120) || item.id,
    email: getAdminScalarText(data.email, 160),
    photoUrl: getAdminScalarText(data.photoUrl || data.photoURL, 1000),
    membershipStatus: getAdminScalarText(data.membershipStatus || data.plan, 40) || "free",
    createdAt: serializeTimestamp(data.createdAt || item.createTime),
    updatedAt: serializeTimestamp(data.updatedAt || item.updateTime),
    ...presence,
  };
}

async function getAgentContextUsers(source = {}) {
  const limit = getAdminLimit(source.limit, 50, 100);
  const query = cleanString(source.q || source.search, "", 160);
  if (query) {
    const uid = await resolveAdminUserUid({ search: query });
    if (!uid) return { users: [], count: 0, search: query };
    const bundle = await buildAdminUserBundle(uid, { limit: 6 });
    return {
      users: [serializeAgentUserFromItem(bundle.user)],
      count: 1,
      search: query,
    };
  }

  const docs = await getRecentAdminDocs("users", {
    orderBy: "createdAt",
    direction: "desc",
    limit,
    max: 100,
  });
  const items = await enrichAdminDataItems(docs, { key: "users" });
  return {
    users: items.map(serializeAgentUserFromItem),
    count: items.length,
    search: "",
  };
}

async function getAgentDialogSessions(uid, limit = 50) {
  const cleanUid = validateDocumentId(uid);
  const collection = db.collection("users").doc(cleanUid).collection(DIALOG_SESSION_COLLECTION);
  let snap;
  try {
    snap = await collection.orderBy("updatedAt", "desc").limit(limit).get();
  } catch (error) {
    console.warn("[adminAgent] dialog session ordered query failed", error.message);
    snap = await collection.limit(limit).get();
  }
  return snap.docs.map(serializeAdminDialogSession);
}

async function getAgentTraceDocsForUser(uid, productId, limit = 100) {
  const snap = await db.collection(AGENT_TRACE_COLLECTION)
    .where("uid", "==", uid)
    .limit(limit)
    .get()
    .catch((error) => {
      console.warn("[adminAgent] trace query failed", error.message);
      return null;
    });
  return (snap?.docs || []).filter((doc) => {
    const data = doc.data() || {};
    return !productId || getAdminScalarText(data.productId, 120) === productId;
  });
}

function buildAgentRecordFromDialogSession(product, uid, session) {
  const category = classifyAgentCategory(product, session);
  const messages = Array.isArray(session.messages) ? session.messages : [];
  const lastMessage = [...messages].reverse().find((message) => getAgentMessageText(message));
  return {
    recordId: encodeAgentToken({ kind: "dialogSession", uid, sessionId: session.id }),
    source: "dialog_sessions",
    uid,
    sessionId: session.id,
    categoryKey: category.key,
    categoryLabel: category.label,
    title: session.id || "default",
    preview: getAgentMessageText(lastMessage || {}, 220) || session.activeFriendContext || "暂无消息内容",
    turnCount: messages.length,
    updatedAt: session.updatedAt,
    hasTrace: false,
  };
}

function buildAgentRecordFromTraceDoc(product, doc) {
  const data = doc.data() || {};
  const category = classifyAgentCategory(product, [
    data.categoryKey,
    data.category,
    data.scene,
    data.recordId,
    data.userInput,
    data.assistantOutput,
  ].filter(Boolean).join(" "));
  return {
    recordId: encodeAgentToken({ kind: "trace", traceId: doc.id }),
    source: "agent_traces",
    uid: getAdminScalarText(data.uid, 160),
    sessionId: getAdminScalarText(data.sessionId || data.recordId, 160),
    categoryKey: getAdminScalarText(data.categoryKey || data.category, 80) || category.key,
    categoryLabel: getAdminScalarText(data.categoryLabel, 120) || category.label,
    title: getAdminScalarText(data.title || data.recordId || doc.id, 160),
    preview: getAdminScalarText(data.userInput || data.assistantOutput, 220),
    turnCount: Array.isArray(data.turns) ? data.turns.length : 1,
    updatedAt: serializeTimestamp(data.updatedAt || data.createdAt),
    hasTrace: true,
  };
}

function buildAgentTurnFromDialogMessage(product, uid, session, messages, index) {
  const message = messages[index] || {};
  const role = normalizeAgentRole(message);
  const nextAssistantIndex = role === "user"
    ? messages.findIndex((candidate, candidateIndex) => (
      candidateIndex > index && normalizeAgentRole(candidate) === "assistant"
    ))
    : index;
  const assistant = nextAssistantIndex >= 0 ? messages[nextAssistantIndex] || {} : {};
  const assistantOutput = normalizeAgentRole(assistant) === "assistant" ? getAgentMessageText(assistant, 6000) : "";
  const userInput = role === "user" ? getAgentMessageText(message, 6000) : "";
  const category = classifyAgentCategory(product, [
    session.activeReadingId,
    session.activeFriendContext,
    message.messageType,
    message.divinerType,
    message.divinationScene,
    message.oracleScene,
    userInput,
    assistantOutput,
  ].filter(Boolean).join(" "));
  return {
    turnId: encodeAgentToken({ kind: "dialogTurn", uid, sessionId: session.id, index }),
    recordId: encodeAgentToken({ kind: "dialogSession", uid, sessionId: session.id }),
    source: "dialog_sessions",
    uid,
    sessionId: session.id,
    index,
    role,
    categoryKey: category.key,
    categoryLabel: category.label,
    title: userInput || assistantOutput || getAgentMessageText(message, 220) || `消息 ${index + 1}`,
    userInput,
    assistantOutput,
    promptAvailable: false,
    createdAt: serializeTimestamp(message.createdAt || message.timestamp || message.time) || session.updatedAt,
  };
}

function buildAgentTurnsFromDialogSession(product, uid, session) {
  const messages = Array.isArray(session.messages) ? session.messages : [];
  const turns = [];
  for (let index = 0; index < messages.length; index += 1) {
    const role = normalizeAgentRole(messages[index]);
    const previousRole = index > 0 ? normalizeAgentRole(messages[index - 1]) : "";
    if (role === "assistant" && previousRole === "user") continue;
    turns.push(buildAgentTurnFromDialogMessage(product, uid, session, messages, index));
  }
  return turns;
}

function getAgentPromptLayersForCategory(product, categoryKey) {
  const category = (Array.isArray(product.categories) ? product.categories : [])
    .find((item) => item.key === categoryKey);
  const categoryLabel = category?.label || "";
  return (Array.isArray(product.promptUsage) ? product.promptUsage : []).filter((usage) => {
    const usedBy = Array.isArray(usage.usedBy) ? usage.usedBy : [];
    return usedBy.includes(categoryLabel)
      || usage.path === "persona/nocturne_oracle.md"
      || usage.path === "policies/safety_boundaries.md";
  });
}

async function getAgentContextCategories(source = {}) {
  const productId = getAgentProductId(source);
  const { product } = getAgentProduct(productId);
  const uid = await resolveAdminUserUid(source);
  if (!uid) throw createHttpError(400, "uid, email, or search is required");
  const [sessions, traceDocs] = await Promise.all([
    getAgentDialogSessions(uid, getAdminLimit(source.limit, 60, 120)),
    getAgentTraceDocsForUser(uid, productId, 120),
  ]);
  const counts = new Map();
  for (const session of sessions) {
    const category = classifyAgentCategory(product, session);
    counts.set(category.key, (counts.get(category.key) || 0) + 1);
  }
  for (const doc of traceDocs) {
    const record = buildAgentRecordFromTraceDoc(product, doc);
    counts.set(record.categoryKey, (counts.get(record.categoryKey) || 0) + 1);
  }
  const categories = (Array.isArray(product.categories) ? product.categories : []).map((category) => ({
    ...category,
    count: counts.get(category.key) || 0,
  }));
  return {
    uid,
    productId,
    categories,
    totalRecords: sessions.length + traceDocs.length,
  };
}

async function getAgentContextRecords(source = {}) {
  const productId = getAgentProductId(source);
  const { product } = getAgentProduct(productId);
  const uid = await resolveAdminUserUid(source);
  if (!uid) throw createHttpError(400, "uid, email, or search is required");
  const categoryKey = cleanString(source.categoryKey || source.category, "", 80);
  const [sessions, traceDocs] = await Promise.all([
    getAgentDialogSessions(uid, getAdminLimit(source.limit, 60, 120)),
    getAgentTraceDocsForUser(uid, productId, 120),
  ]);
  const records = [
    ...sessions.map((session) => buildAgentRecordFromDialogSession(product, uid, session)),
    ...traceDocs.map((doc) => buildAgentRecordFromTraceDoc(product, doc)),
  ]
    .filter((record) => !categoryKey || record.categoryKey === categoryKey)
    .sort((a, b) => String(b.updatedAt || "").localeCompare(String(a.updatedAt || "")));
  return {
    uid,
    productId,
    categoryKey,
    records,
    count: records.length,
  };
}

async function getAgentContextTurns(source = {}) {
  const productId = getAgentProductId(source);
  const { product } = getAgentProduct(productId);
  const record = decodeAgentToken(source.recordId || source.id || "");
  if (record.kind === "dialogSession") {
    const uid = validateDocumentId(record.uid);
    const sessionId = validateDocumentId(record.sessionId);
    const snap = await db.collection("users").doc(uid).collection(DIALOG_SESSION_COLLECTION).doc(sessionId).get();
    if (!snap.exists) throw createHttpError(404, "Dialog session not found");
    const session = serializeAdminDialogSession(snap);
    const turns = buildAgentTurnsFromDialogSession(product, uid, session);
    return {
      productId,
      record: buildAgentRecordFromDialogSession(product, uid, session),
      turns,
      count: turns.length,
    };
  }
  if (record.kind === "trace") {
    const snap = await db.collection(AGENT_TRACE_COLLECTION).doc(validateDocumentId(record.traceId)).get();
    if (!snap.exists) throw createHttpError(404, "Trace not found");
    const data = snap.data() || {};
    const turns = Array.isArray(data.turns) ? data.turns : [{
      userInput: data.userInput,
      assistantOutput: data.assistantOutput,
      actualPrompt: data.actualPrompt,
      messages: data.messages,
      sourceFiles: data.sourceFiles,
    }];
    return {
      productId,
      record: buildAgentRecordFromTraceDoc(product, snap),
      turns: turns.map((turn, index) => ({
        turnId: encodeAgentToken({ kind: "traceTurn", traceId: snap.id, index }),
        recordId: source.recordId || source.id,
        source: "agent_traces",
        index,
        uid: getAdminScalarText(data.uid, 160),
        categoryKey: getAdminScalarText(data.categoryKey || data.category, 80),
        categoryLabel: getAdminScalarText(data.categoryLabel, 120),
        title: getAdminScalarText(turn.userInput || turn.assistantOutput || data.title, 220),
        userInput: getAdminScalarText(turn.userInput, 6000),
        assistantOutput: getAdminScalarText(turn.assistantOutput, 6000),
        promptAvailable: Boolean(turn.actualPrompt || data.actualPrompt),
        createdAt: serializeTimestamp(data.createdAt),
      })),
      count: turns.length,
    };
  }
  throw createHttpError(400, "Unsupported agent record kind");
}

async function getAgentContextTurnDetail(source = {}) {
  const productId = getAgentProductId(source);
  const { product } = getAgentProduct(productId);
  const token = decodeAgentToken(source.turnId || source.id || "");
  if (token.kind === "dialogTurn") {
    const uid = validateDocumentId(token.uid);
    const sessionId = validateDocumentId(token.sessionId);
    const snap = await db.collection("users").doc(uid).collection(DIALOG_SESSION_COLLECTION).doc(sessionId).get();
    if (!snap.exists) throw createHttpError(404, "Dialog session not found");
    const session = serializeAdminDialogSession(snap);
    const messages = Array.isArray(session.messages) ? session.messages : [];
    const turn = buildAgentTurnFromDialogMessage(product, uid, session, messages, Number(token.index || 0));
    const start = Math.max(0, Number(token.index || 0) - 2);
    const end = Math.min(messages.length, Number(token.index || 0) + 4);
    const ratingSnap = await db.collection(AGENT_TURN_RATINGS_COLLECTION)
      .doc(getAgentTurnRatingDocId(source.turnId || source.id))
      .get()
      .catch(() => null);
    return {
      productId,
      turn,
      rating: ratingSnap?.exists ? serializeFirestoreValue(ratingSnap.data() || {}) : null,
      prompt: {
        available: false,
        reason: "历史 dialog_sessions 只保存了消息结果，没有保存实际 Prompt。接入 traceIngest 后可显示完整 Prompt 和模型参数。",
      },
      promptLayers: getAgentPromptLayersForCategory(product, turn.categoryKey),
      runtimeSections: [
        {
          label: "会话字段",
          content: JSON.stringify({
            sessionId,
            activeReadingId: session.activeReadingId,
            activeRelationshipId: session.activeRelationshipId,
            activeFriendContext: session.activeFriendContext,
            updatedAt: session.updatedAt,
          }, null, 2),
        },
      ],
      messages: messages.slice(start, end).map((message, offset) => ({
        index: start + offset,
        role: normalizeAgentRole(message),
        content: getAgentMessageText(message, 6000),
        meta: {
          messageType: getAdminScalarText(message.messageType || message.type, 80),
          divinerType: getAdminScalarText(message.divinerType, 80),
          createdAt: serializeTimestamp(message.createdAt || message.timestamp || message.time),
        },
      })),
    };
  }
  if (token.kind === "traceTurn") {
    const snap = await db.collection(AGENT_TRACE_COLLECTION).doc(validateDocumentId(token.traceId)).get();
    if (!snap.exists) throw createHttpError(404, "Trace not found");
    const data = serializeFirestoreValue(snap.data() || {});
    const turns = Array.isArray(data.turns) ? data.turns : [data];
    const turn = turns[Number(token.index || 0)] || {};
    return {
      productId,
      turn: {
        turnId: source.turnId || source.id,
        source: "agent_traces",
        index: Number(token.index || 0),
        uid: data.uid || "",
        categoryKey: data.categoryKey || data.category || "",
        categoryLabel: data.categoryLabel || "",
        title: turn.userInput || turn.assistantOutput || data.title || snap.id,
        userInput: turn.userInput || data.userInput || "",
        assistantOutput: turn.assistantOutput || data.assistantOutput || "",
        promptAvailable: Boolean(turn.actualPrompt || data.actualPrompt),
        createdAt: data.createdAt || "",
      },
      prompt: {
        available: Boolean(turn.actualPrompt || data.actualPrompt),
        content: turn.actualPrompt || data.actualPrompt || "",
        messages: turn.messages || data.messages || [],
      },
      promptLayers: turn.sourceFiles || data.sourceFiles || getAgentPromptLayersForCategory(product, data.categoryKey || data.category),
      runtimeSections: [
        { label: "Trace JSON", content: JSON.stringify(data, null, 2) },
      ],
      messages: turn.messages || data.messages || [],
    };
  }
  throw createHttpError(400, "Unsupported agent turn kind");
}

function sanitizeAgentTraceValue(value, redactionKeys = [], depth = 0) {
  if (depth > 8) return "[max-depth]";
  if (value === null || value === undefined) return null;
  if (typeof value === "string") return value.slice(0, 20000);
  if (typeof value === "number" || typeof value === "boolean") return value;
  if (Array.isArray(value)) {
    return value.slice(0, 200).map((item) => sanitizeAgentTraceValue(item, redactionKeys, depth + 1));
  }
  if (typeof value === "object") {
    const result = {};
    for (const [key, child] of Object.entries(value).slice(0, 200)) {
      const lowerKey = key.toLowerCase();
      if (redactionKeys.some((redactionKey) => lowerKey.includes(String(redactionKey).toLowerCase()))) {
        result[key] = "[redacted]";
      } else {
        result[key] = sanitizeAgentTraceValue(child, redactionKeys, depth + 1);
      }
    }
    return result;
  }
  return String(value).slice(0, 20000);
}

async function createAgentPromptDraft(source, decoded) {
  const productId = getAgentProductId(source);
  const { product } = getAgentProduct(productId);
  const promptPath = normalizeAgentPromptPath(source.path);
  const content = cleanString(source.content, "", 200000);
  if (!content) throw createHttpError(400, "Draft content is required");
  const published = await getPublishedAgentPrompt(product, promptPath).catch(() => null);
  const draftRef = db.collection(AGENT_PROMPT_DRAFTS_COLLECTION).doc();
  await draftRef.set({
    productId,
    path: promptPath,
    title: cleanString(source.title, promptPath, 160),
    note: cleanString(source.note, "", 500),
    content,
    baseContent: published?.content || cleanString(source.baseContent, "", 200000),
    status: "draft",
    createdBy: decoded.email || decoded.uid,
    createdUid: decoded.uid,
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
    updatedAt: admin.firestore.FieldValue.serverTimestamp(),
  });
  const snap = await draftRef.get();
  return { ok: true, draft: serializeAgentPromptDraft(snap) };
}

async function applyAgentPromptDraftAction(source, decoded) {
  const action = cleanString(source.draftAction || source.operation, "", 40);
  const draftId = validateDocumentId(source.draftId);
  const draftRef = db.collection(AGENT_PROMPT_DRAFTS_COLLECTION).doc(draftId);
  const draftSnap = await draftRef.get();
  if (!draftSnap.exists) throw createHttpError(404, "Draft not found");
  const draft = draftSnap.data() || {};
  const productId = getAdminScalarText(draft.productId, 120);
  const promptPath = normalizeAgentPromptPath(draft.path);
  const actor = decoded.email || decoded.uid;

  if (action === "approve") {
    await draftRef.set({
      status: "approved",
      approvedBy: actor,
      approvedUid: decoded.uid,
      approvedAt: admin.firestore.FieldValue.serverTimestamp(),
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });
    return { ok: true, action, draft: serializeAgentPromptDraft(await draftRef.get()) };
  }

  if (action === "delete") {
    await draftRef.delete();
    return { ok: true, action, draftId };
  }

  if (action === "publish") {
    if (getAdminScalarText(draft.status, 40) !== "approved") {
      throw createHttpError(400, "Draft must be approved before publish");
    }
    const { product } = getAgentProduct(productId);
    const previous = await getPublishedAgentPrompt(product, promptPath).catch(() => null);
    const fileRef = db.collection(AGENT_PROMPT_FILES_COLLECTION).doc(getAgentPromptDocId(productId, promptPath));
    const releaseRef = db.collection(AGENT_PROMPT_RELEASES_COLLECTION).doc();
    await db.runTransaction(async (transaction) => {
      transaction.set(fileRef, {
        productId,
        path: promptPath,
        content: String(draft.content || ""),
        previousContent: previous?.content || "",
        draftId,
        publishedBy: actor,
        publishedUid: decoded.uid,
        publishedAt: admin.firestore.FieldValue.serverTimestamp(),
        updatedAt: admin.firestore.FieldValue.serverTimestamp(),
      }, { merge: true });
      transaction.set(releaseRef, {
        productId,
        path: promptPath,
        type: "publish",
        draftId,
        previousContent: previous?.content || "",
        content: String(draft.content || ""),
        note: cleanString(source.note || draft.note, "", 500),
        releasedBy: actor,
        releasedUid: decoded.uid,
        createdAt: admin.firestore.FieldValue.serverTimestamp(),
      });
      transaction.set(draftRef, {
        status: "published",
        publishedBy: actor,
        publishedUid: decoded.uid,
        publishedAt: admin.firestore.FieldValue.serverTimestamp(),
        updatedAt: admin.firestore.FieldValue.serverTimestamp(),
      }, { merge: true });
    });
    return { ok: true, action, draft: serializeAgentPromptDraft(await draftRef.get()) };
  }

  throw createHttpError(400, "Unsupported draft action");
}

async function rollbackAgentPromptRelease(source, decoded) {
  const releaseId = validateDocumentId(source.releaseId);
  const releaseRef = db.collection(AGENT_PROMPT_RELEASES_COLLECTION).doc(releaseId);
  const releaseSnap = await releaseRef.get();
  if (!releaseSnap.exists) throw createHttpError(404, "Release not found");
  const release = releaseSnap.data() || {};
  const productId = getAdminScalarText(release.productId, 120);
  const promptPath = normalizeAgentPromptPath(release.path);
  const previousContent = String(release.previousContent || "");
  const actor = decoded.email || decoded.uid;
  if (!previousContent) throw createHttpError(400, "Release does not have previous content");

  const fileRef = db.collection(AGENT_PROMPT_FILES_COLLECTION).doc(getAgentPromptDocId(productId, promptPath));
  const rollbackRef = db.collection(AGENT_PROMPT_RELEASES_COLLECTION).doc();
  const currentSnap = await fileRef.get().catch(() => null);
  const currentContent = currentSnap?.exists ? String(currentSnap.data()?.content || "") : "";
  await db.runTransaction(async (transaction) => {
    transaction.set(fileRef, {
      productId,
      path: promptPath,
      content: previousContent,
      previousContent: currentContent,
      rollbackOf: releaseId,
      publishedBy: actor,
      publishedUid: decoded.uid,
      publishedAt: admin.firestore.FieldValue.serverTimestamp(),
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });
    transaction.set(rollbackRef, {
      productId,
      path: promptPath,
      type: "rollback",
      rollbackOf: releaseId,
      previousContent: currentContent,
      content: previousContent,
      note: cleanString(source.note, "Rollback from admin console", 500),
      releasedBy: actor,
      releasedUid: decoded.uid,
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
    });
  });
  return { ok: true, action: "rollback", release: serializeAgentPromptRelease(await rollbackRef.get()) };
}

async function handleAdminAgentAction(action, source, req, decoded) {
  if (action === "products") {
    const config = readAgentAdminConfig();
    return { ok: true, ...config };
  }

  if (action === "contextUsers") {
    const productId = getAgentProductId(source);
    return { ok: true, productId, ...(await getAgentContextUsers(source)) };
  }

  if (action === "contextCategories") return { ok: true, ...(await getAgentContextCategories(source)) };
  if (action === "contextRecords") return { ok: true, ...(await getAgentContextRecords(source)) };
  if (action === "contextTurns") return { ok: true, ...(await getAgentContextTurns(source)) };
  if (action === "contextTurn") return { ok: true, ...(await getAgentContextTurnDetail(source)) };

  if (action === "rateTurn") {
    if (req.method !== "POST") throw createHttpError(405, "Use POST");
    const turnId = cleanString(source.turnId, "", 1200);
    if (!turnId) throw createHttpError(400, "turnId is required");
    const rating = cleanString(source.rating, "", 40);
    if (!["good", "unclear", "bad", "unsafe", "bug"].includes(rating)) {
      throw createHttpError(400, "rating is invalid");
    }
    const docId = getAgentTurnRatingDocId(turnId);
    await db.collection(AGENT_TURN_RATINGS_COLLECTION).doc(docId).set({
      turnId,
      rating,
      note: cleanString(source.note, "", 1000),
      ratedBy: decoded.email || decoded.uid,
      ratedUid: decoded.uid,
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });
    await writeAdminAuditLog(decoded, "agent.turn_rate", {
      type: "agent_turn",
      id: docId,
      path: `${AGENT_TURN_RATINGS_COLLECTION}/${docId}`,
    }, {
      turnId,
      rating,
      hasNote: Boolean(cleanString(source.note, "", 1000)),
    }, req);
    return { ok: true, rating };
  }

  if (action === "prompts") {
    const productId = getAgentProductId(source);
    const { product } = getAgentProduct(productId);
    return { ok: true, productId, files: await listAgentPromptFiles(product) };
  }

  if (action === "promptFile") {
    const productId = getAgentProductId(source);
    const { product } = getAgentProduct(productId);
    const promptPath = normalizeAgentPromptPath(source.path);
    const file = await getPublishedAgentPrompt(product, promptPath);
    const { drafts, releases } = await listAgentPromptDrafts(productId, promptPath);
    return { ok: true, productId, file, drafts, releases };
  }

  if (action === "promptUsage") {
    const productId = getAgentProductId(source);
    const { product } = getAgentProduct(productId);
    return {
      ok: true,
      productId,
      promptUsage: product.promptUsage || [],
      contextFlows: product.contextFlows || [],
      contextExplainers: product.contextExplainers || [],
      categories: product.categories || [],
    };
  }

  if (action === "promptDrafts") {
    const productId = getAgentProductId(source);
    const promptPath = source.path ? normalizeAgentPromptPath(source.path) : "";
    return { ok: true, productId, ...(await listAgentPromptDrafts(productId, promptPath)) };
  }

  if (action === "createPromptDraft") {
    if (req.method !== "POST") throw createHttpError(405, "Use POST");
    const result = await createAgentPromptDraft(source, decoded);
    await writeAdminAuditLog(decoded, "agent.prompt_draft.create", {
      type: "agent_prompt",
      id: result.draft?.draftId || "",
      path: `${AGENT_PROMPT_DRAFTS_COLLECTION}/${result.draft?.draftId || ""}`,
    }, {
      productId: result.draft?.productId || "",
      promptPath: result.draft?.path || "",
      title: result.draft?.title || "",
    }, req);
    return result;
  }

  if (action === "draftAction") {
    if (req.method !== "POST") throw createHttpError(405, "Use POST");
    const result = await applyAgentPromptDraftAction(source, decoded);
    await writeAdminAuditLog(decoded, "agent.prompt_draft.action", {
      type: "agent_prompt",
      id: result.draft?.draftId || result.draftId || "",
      path: `${AGENT_PROMPT_DRAFTS_COLLECTION}/${result.draft?.draftId || result.draftId || ""}`,
    }, {
      draftAction: result.action || cleanString(source.draftAction || source.operation, "", 40),
      productId: result.draft?.productId || "",
      promptPath: result.draft?.path || "",
    }, req);
    return result;
  }

  if (action === "rollbackRelease") {
    if (req.method !== "POST") throw createHttpError(405, "Use POST");
    const result = await rollbackAgentPromptRelease(source, decoded);
    await writeAdminAuditLog(decoded, "agent.prompt_release.rollback", {
      type: "agent_prompt",
      id: result.release?.releaseId || "",
      path: `${AGENT_PROMPT_RELEASES_COLLECTION}/${result.release?.releaseId || ""}`,
    }, {
      rollbackOf: cleanString(source.releaseId, "", 160),
      productId: result.release?.productId || "",
      promptPath: result.release?.path || "",
    }, req);
    return result;
  }

  if (action === "traceIngest") {
    if (req.method !== "POST") throw createHttpError(405, "Use POST");
    const productId = getAgentProductId(source);
    const { product } = getAgentProduct(productId);
    const uid = validateDocumentId(source.uid);
    const redactionKeys = Array.isArray(product.redactionKeys) ? product.redactionKeys : [];
    const payload = sanitizeAgentTraceValue(source.trace || source, redactionKeys);
    const traceRef = db.collection(AGENT_TRACE_COLLECTION).doc();
    await traceRef.set({
      ...payload,
      productId,
      uid,
      categoryKey: cleanString(source.categoryKey || source.category, "", 80),
      categoryLabel: cleanString(source.categoryLabel, "", 120),
      sessionId: cleanString(source.sessionId || source.recordId, "", 160),
      ingestedBy: decoded.email || decoded.uid,
      ingestedUid: decoded.uid,
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });
    await writeAdminAuditLog(decoded, "agent.trace_ingest", {
      type: "agent_trace",
      id: traceRef.id,
      uid,
      path: `${AGENT_TRACE_COLLECTION}/${traceRef.id}`,
    }, {
      productId,
      categoryKey: cleanString(source.categoryKey || source.category, "", 80),
      sessionId: cleanString(source.sessionId || source.recordId, "", 160),
    }, req);
    return { ok: true, traceId: traceRef.id };
  }

  throw createHttpError(400, "Unknown agent admin action");
}

function serializeFeedback(doc) {
  const data = doc.data() || {};
  return {
    id: doc.id,
    feedbackId: data.feedbackId || doc.id,
    uid: data.uid || "",
    displayName: data.displayName || "",
    email: data.email || "",
    category: data.category || "community",
    tag: data.tag || "general",
    content: data.content || "",
    source: data.source || "app",
    status: data.status || "new",
    adminNote: data.adminNote || "",
    appVersion: data.appVersion || "",
    platform: data.platform || "",
    deviceModel: data.deviceModel || "",
    createdAt: serializeTimestamp(data.createdAt),
    updatedAt: serializeTimestamp(data.updatedAt),
  };
}

function cleanUrlMap(value) {
  const source = value && typeof value === "object" ? value : {};
  const result = {};
  for (const key of ["instagram", "facebook", "x", "tiktok", "pinterest"]) {
    if (source[key] !== undefined) {
      result[key] = cleanString(source[key], "", 500);
    }
  }
  return result;
}

function cleanIapProduct(value) {
  const source = value && typeof value === "object" ? value : {};
  return {
    productId: cleanString(source.productId, "", 120),
    type: cleanString(source.type, "subscription", 40),
    store: cleanString(source.store, "app_store_google_play", 80),
    displayName: cleanString(source.displayName, "", 120),
    priceLabel: cleanString(source.priceLabel, "", 40),
  };
}

function cleanIapProducts(value) {
  const source = value && typeof value === "object" ? value : {};
  const result = {};
  if (source.proMonthly) result.proMonthly = cleanIapProduct(source.proMonthly);
  if (source.proYearly) result.proYearly = cleanIapProduct(source.proYearly);
  return result;
}

function cleanReceiptPayload(body) {
  const source = body || {};
  return {
    productId: cleanString(source.productId, "", 160),
    store: cleanString(source.store, "unknown", 80),
    transactionId: cleanString(source.transactionId, "", 200),
    receipt: cleanString(source.receipt, "", 20000),
    packageName: cleanString(source.packageName, "", 200),
    platform: cleanString(source.platform, "", 80),
    appVersion: cleanString(source.appVersion, "", 80),
  };
}

function buildReceiptId(uid, payload) {
  const stableSource = [
    uid,
    payload.store,
    payload.productId,
    payload.transactionId || payload.receipt,
  ].join("|");
  return crypto.createHash("sha256").update(stableSource).digest("hex");
}

function getOptionalSecret(secretParam) {
  try {
    return secretParam.value() || "";
  } catch (error) {
    return "";
  }
}

function buildSecretReadiness() {
  return {
    deepseekApiKey: Boolean(getOptionalSecret(deepseekApiKey)),
    volcanoTtsApiKey: Boolean(getOptionalSecret(volcanoTtsApiKey)),
    paymentWebhookSecret: Boolean(getOptionalSecret(paymentWebhookSecret)),
    appleSharedSecret: Boolean(getOptionalSecret(appleSharedSecret)),
    googlePackageName: Boolean(getOptionalSecret(googlePackageName)),
    googleServiceAccountJson: Boolean(getOptionalSecret(googleServiceAccountJson)),
  };
}

function buildSecretDiagnostics(secrets) {
  const inspectedSecrets = [
    secrets.deepseekApiKey ? "DEEPSEEK_API_KEY" : "",
    secrets.volcanoTtsApiKey ? "VOLC_TTS_API_KEY" : "",
    secrets.paymentWebhookSecret ? "PAYMENT_WEBHOOK_SECRET" : "",
    secrets.appleSharedSecret ? "APPLE_SHARED_SECRET" : "",
    secrets.googlePackageName ? "GOOGLE_PACKAGE_NAME" : "",
    secrets.googleServiceAccountJson ? "GOOGLE_SERVICE_ACCOUNT_JSON" : "",
  ].filter(Boolean);

  return {
    readinessStatusCanInspectAnySecret: inspectedSecrets.length > 0,
    inspectedSecrets,
    note: "A false secret flag means readinessStatus cannot read that secret. If the client-facing smoke tests pass, the target function may still be correctly configured.",
  };
}

function buildReadinessActions(secrets, firestoreOk, publicConfigReadiness) {
  const actions = [];
  if (!firestoreOk) {
    actions.push("Check Firestore database and service account permissions.");
  }
  if (publicConfigReadiness?.placeholderSocialLinks?.length) {
    actions.push(`Replace placeholder social links in app_config/public: ${publicConfigReadiness.placeholderSocialLinks.join(", ")}.`);
  }
  if (publicConfigReadiness?.missingProductIds?.length) {
    actions.push(`Set IAP product IDs in app_config/public: ${publicConfigReadiness.missingProductIds.join(", ")}.`);
  }
  if (publicConfigReadiness?.missingPriceLabels?.length) {
    actions.push(`Set optional IAP price labels in app_config/public for display polish: ${publicConfigReadiness.missingPriceLabels.join(", ")}.`);
  }
  if (!secrets.deepseekApiKey) {
    actions.push("Set DEEPSEEK_API_KEY for aiChat / aiChatStream.");
  }
  if (!secrets.volcanoTtsApiKey) {
    actions.push("Set VOLC_TTS_API_KEY for ttsSynthesize.");
  }
  if (!secrets.appleSharedSecret) {
    actions.push("Set APPLE_SHARED_SECRET for App Store receipt verification.");
  }
  if (!secrets.googlePackageName && !secrets.googleServiceAccountJson) {
    actions.push("Set GOOGLE_PACKAGE_NAME and GOOGLE_SERVICE_ACCOUNT_JSON for Google Play verification.");
  } else if (!secrets.googlePackageName) {
    actions.push("Set GOOGLE_PACKAGE_NAME for Google Play receipt verification.");
  } else if (!secrets.googleServiceAccountJson) {
    actions.push("Set GOOGLE_SERVICE_ACCOUNT_JSON for Google Play receipt verification.");
  }
  if (!secrets.paymentWebhookSecret) {
    actions.push("Set PAYMENT_WEBHOOK_SECRET if external payment webhooks are used.");
  }
  return actions;
}

function resolveIapStore(payload) {
  const combined = `${payload.store} ${payload.platform}`.toLowerCase();
  if (combined.includes("apple") || combined.includes("app_store") || combined.includes("ios")) {
    return "apple";
  }
  if (combined.includes("google") || combined.includes("play") || combined.includes("android")) {
    return "google";
  }

  const receipt = payload.receipt || "";
  if (receipt.includes("\"Store\":\"AppleAppStore\"") || receipt.includes("\"Store\":\"MacAppStore\"")) {
    return "apple";
  }
  if (receipt.includes("\"Store\":\"GooglePlay\"")) {
    return "google";
  }
  return "unknown";
}

function extractUnityReceiptPayload(receipt) {
  const text = String(receipt || "").trim();
  if (!text) return "";

  try {
    const parsed = JSON.parse(text);
    if (parsed && typeof parsed.Payload === "string") {
      return parsed.Payload;
    }
  } catch (error) {
    // Receipt may be a raw Apple base64 receipt or Google purchase token.
  }

  return text;
}

function extractGooglePurchaseData(payload) {
  const rawPayload = extractUnityReceiptPayload(payload.receipt);
  let purchaseToken = rawPayload;
  let packageName = payload.packageName || "";
  let productId = payload.productId || "";

  try {
    const wrapper = JSON.parse(rawPayload);
    const jsonText = typeof wrapper.json === "string" ? wrapper.json : rawPayload;
    const purchaseJson = JSON.parse(jsonText);
    purchaseToken = purchaseJson.purchaseToken || purchaseJson.token || purchaseToken;
    packageName = purchaseJson.packageName || packageName;
    productId = purchaseJson.productId || purchaseJson.subscriptionId || productId;
  } catch (error) {
    // Raw purchase token is acceptable for manual tests.
  }

  return {
    purchaseToken: cleanString(purchaseToken, "", 2000),
    packageName: cleanString(packageName, "", 200),
    productId: cleanString(productId, payload.productId, 160),
  };
}

async function verifyAppleReceipt(payload) {
  const sharedSecret = getOptionalSecret(appleSharedSecret);
  if (!sharedSecret) {
    return {
      status: "pending_configuration",
      valid: null,
      message: "APPLE_SHARED_SECRET is not configured.",
    };
  }

  const receiptData = extractUnityReceiptPayload(payload.receipt);
  const requestBody = {
    "receipt-data": receiptData,
    password: sharedSecret,
    "exclude-old-transactions": true,
  };

  let response = await postAppleReceipt("https://buy.itunes.apple.com/verifyReceipt", requestBody);
  if (response.status === 21007) {
    response = await postAppleReceipt("https://sandbox.itunes.apple.com/verifyReceipt", requestBody);
  }

  if (response.status !== 0) {
    return {
      status: "invalid",
      valid: false,
      providerStatus: response.status,
      message: `Apple receipt invalid: ${response.status}`,
    };
  }

  const purchase = findApplePurchase(response, payload.productId);
  if (!purchase) {
    return {
      status: "invalid",
      valid: false,
      message: "Apple receipt does not contain requested product.",
    };
  }

  const expiresMs = Number(purchase.expires_date_ms || 0);
  const expiresAt = expiresMs > 0 ? new Date(expiresMs) : buildFallbackExpiry(payload.productId);
  const active = !expiresAt || expiresAt.getTime() > Date.now();
  return {
    status: active ? "verified" : "expired",
    valid: active,
    transactionId: purchase.original_transaction_id || purchase.transaction_id || payload.transactionId,
    proExpiresAt: expiresAt,
    rawProductId: purchase.product_id,
    message: active ? "Apple receipt verified." : "Apple subscription expired.",
  };
}

async function postAppleReceipt(url, body) {
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  return response.json();
}

function findApplePurchase(response, productId) {
  const candidates = []
    .concat(Array.isArray(response.latest_receipt_info) ? response.latest_receipt_info : [])
    .concat(response.receipt && Array.isArray(response.receipt.in_app) ? response.receipt.in_app : []);

  return candidates
    .filter((item) => item && item.product_id === productId)
    .sort((a, b) => Number(b.expires_date_ms || 0) - Number(a.expires_date_ms || 0))[0] || null;
}

async function verifyGoogleReceipt(payload) {
  const serviceAccount = getOptionalSecret(googleServiceAccountJson);
  const configuredPackage = getOptionalSecret(googlePackageName);
  if (!serviceAccount || !configuredPackage) {
    return {
      status: "pending_configuration",
      valid: null,
      message: "GOOGLE_SERVICE_ACCOUNT_JSON or GOOGLE_PACKAGE_NAME is not configured.",
    };
  }

  const purchaseData = extractGooglePurchaseData(payload);
  const packageName = purchaseData.packageName || configuredPackage;
  if (packageName !== configuredPackage) {
    return {
      status: "invalid",
      valid: false,
      message: "Google receipt packageName does not match configured package.",
    };
  }

  if (!purchaseData.purchaseToken || !purchaseData.productId) {
    return {
      status: "invalid",
      valid: false,
      message: "Google purchaseToken or productId is missing.",
    };
  }

  const accessToken = await createGoogleAccessToken(serviceAccount);
  const url = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications/"
    + `${encodeURIComponent(packageName)}/purchases/subscriptions/`
    + `${encodeURIComponent(purchaseData.productId)}/tokens/${encodeURIComponent(purchaseData.purchaseToken)}`;
  const response = await fetch(url, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  const body = await response.json();
  if (!response.ok) {
    return {
      status: "invalid",
      valid: false,
      providerStatus: response.status,
      message: `Google receipt invalid: ${JSON.stringify(body).slice(0, 300)}`,
    };
  }

  const expiresMs = Number(body.expiryTimeMillis || 0);
  const expiresAt = expiresMs > 0 ? new Date(expiresMs) : null;
  const active = (!expiresAt || expiresAt.getTime() > Date.now())
    && (body.cancelReason === undefined || Number(body.cancelReason) !== 1);
  return {
    status: active ? "verified" : "expired",
    valid: active,
    transactionId: body.orderId || payload.transactionId || purchaseData.purchaseToken,
    proExpiresAt: expiresAt,
    rawProductId: purchaseData.productId,
    message: active ? "Google receipt verified." : "Google subscription expired.",
  };
}

async function createGoogleAccessToken(serviceAccountJson) {
  const account = JSON.parse(serviceAccountJson);
  const nowSeconds = Math.floor(Date.now() / 1000);
  const assertion = [
    base64UrlJson({ alg: "RS256", typ: "JWT" }),
    base64UrlJson({
      iss: account.client_email,
      scope: "https://www.googleapis.com/auth/androidpublisher",
      aud: "https://oauth2.googleapis.com/token",
      exp: nowSeconds + 3600,
      iat: nowSeconds,
    }),
  ].join(".");
  const signature = crypto
    .createSign("RSA-SHA256")
    .update(assertion)
    .sign(account.private_key, "base64url");

  const tokenResponse = await fetch("https://oauth2.googleapis.com/token", {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      grant_type: "urn:ietf:params:oauth:grant-type:jwt-bearer",
      assertion: `${assertion}.${signature}`,
    }),
  });
  const tokenBody = await tokenResponse.json();
  if (!tokenResponse.ok || !tokenBody.access_token) {
    throw new Error(`Google access token failed: ${JSON.stringify(tokenBody).slice(0, 300)}`);
  }
  return tokenBody.access_token;
}

function base64UrlJson(value) {
  return Buffer.from(JSON.stringify(value)).toString("base64url");
}

function buildFallbackExpiry(productId) {
  const now = new Date();
  const lower = String(productId || "").toLowerCase();
  if (lower.includes("year")) return new Date(now.getTime() + 366 * 24 * 60 * 60 * 1000);
  if (lower.includes("month")) return new Date(now.getTime() + 31 * 24 * 60 * 60 * 1000);
  return null;
}

async function callDeepSeek(body, stream) {
  const key = deepseekApiKey.value();
  if (!key) throw new Error("DEEPSEEK_API_KEY is not configured");

  const messages = sanitizeMessages(body.messages);
  if (messages.length === 0) {
    const err = new Error("messages is required");
    err.status = 400;
    throw err;
  }

  const payload = {
    model: body.model || DEEPSEEK_MODEL,
    messages,
    temperature: typeof body.temperature === "number" ? body.temperature : 0.7,
    max_tokens: Math.min(Number(body.max_tokens || body.maxTokens || 2000), 4000),
    stream,
  };

  const response = await fetch(DEEPSEEK_URL, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${key}`,
    },
    body: JSON.stringify(payload),
  });

  if (!response.ok) {
    const text = await response.text();
    const err = new Error(`DeepSeek error ${response.status}: ${text.slice(0, 500)}`);
    err.status = response.status;
    throw err;
  }

  return response;
}

exports.membershipStatus = onRequest(runtime, async (req, res) => {
  const decoded = await requireAuth(req, res);
  if (!decoded) return;

  const membership = await getMembership(decoded.uid);
  json(res, 200, {
    uid: decoded.uid,
    membershipStatus: membership.status,
    isPro: membership.isPro,
    proExpiresAt: membership.proExpiresAt ? membership.proExpiresAt.toDate().toISOString() : null,
  });
});

exports.readinessStatus = onRequest({
  ...runtime,
  secrets: boundSecrets(...ALL_SECRET_NAMES),
}, async (req, res) => {
  if (!requireMethod(req, res, "GET")) return;

  let firestoreOk = false;
  let firestoreMessage = "not checked";
  let publicConfig = mergePublicConfig({});
  try {
    const publicConfigSnap = await db.collection("app_config").doc("public").get();
    publicConfig = mergePublicConfig(publicConfigSnap.exists ? publicConfigSnap.data() : {});
    firestoreOk = true;
    firestoreMessage = "ok";
  } catch (error) {
    firestoreMessage = error.message || "Firestore read failed";
  }

  const secrets = buildSecretReadiness();
  const publicConfigReadiness = buildPublicConfigReadiness(publicConfig);
  const requiredActions = buildReadinessActions(secrets, firestoreOk, publicConfigReadiness);
  const secretDiagnostics = buildSecretDiagnostics(secrets);
  json(res, 200, {
    ok: firestoreOk
      && publicConfigReadiness.configured
      && secrets.deepseekApiKey
      && secrets.volcanoTtsApiKey
      && secrets.appleSharedSecret
      && secrets.googlePackageName
      && secrets.googleServiceAccountJson,
    checkedAt: new Date().toISOString(),
    projectId: process.env.GCLOUD_PROJECT || process.env.GCP_PROJECT || "",
    region: REGION,
    firestore: {
      ok: firestoreOk,
      message: firestoreMessage,
    },
    publicConfig: {
      configured: publicConfigReadiness.configured,
      socialLinksConfigured: publicConfigReadiness.socialLinksConfigured,
      placeholderSocialLinks: publicConfigReadiness.placeholderSocialLinks,
      iapProductsConfigured: publicConfigReadiness.iapProductsConfigured,
      missingProductIds: publicConfigReadiness.missingProductIds,
      missingPriceLabels: publicConfigReadiness.missingPriceLabels,
      warnings: publicConfigReadiness.warnings,
    },
    functions: {
      membershipStatus: {
        deployed: true,
        configured: true,
      },
      readinessStatus: {
        deployed: true,
        configured: true,
      },
      publicConfig: {
        deployed: true,
        configured: true,
      },
      feedback: {
        deployed: true,
        configured: true,
      },
      aiChat: {
        deployedByDefaultWhenConfigured: true,
        configured: secrets.deepseekApiKey,
        missingSecrets: secrets.deepseekApiKey ? [] : ["DEEPSEEK_API_KEY"],
      },
      aiChatStream: {
        deployedByDefaultWhenConfigured: true,
        configured: secrets.deepseekApiKey,
        missingSecrets: secrets.deepseekApiKey ? [] : ["DEEPSEEK_API_KEY"],
      },
      ttsSynthesize: {
        deployedByDefaultWhenConfigured: true,
        configured: secrets.volcanoTtsApiKey,
        missingSecrets: secrets.volcanoTtsApiKey ? [] : ["VOLC_TTS_API_KEY"],
      },
      submitIapReceipt: {
        deployedByDefaultWhenConfigured: true,
        configured: secrets.appleSharedSecret
          && secrets.googlePackageName
          && secrets.googleServiceAccountJson,
        missingSecrets: [
          secrets.appleSharedSecret ? "" : "APPLE_SHARED_SECRET",
          secrets.googlePackageName ? "" : "GOOGLE_PACKAGE_NAME",
          secrets.googleServiceAccountJson ? "" : "GOOGLE_SERVICE_ACCOUNT_JSON",
        ].filter(Boolean),
      },
      paymentWebhook: {
        deployedByDefaultWhenConfigured: true,
        configured: secrets.paymentWebhookSecret,
        missingSecrets: secrets.paymentWebhookSecret ? [] : ["PAYMENT_WEBHOOK_SECRET"],
      },
    },
    boundSecrets: boundSecretNames(),
    secrets,
    secretDiagnostics,
    requiredActions,
  });
});

exports.submitIapReceipt = onRequest({
  ...runtime,
  secrets: boundSecrets("APPLE_SHARED_SECRET", "GOOGLE_PACKAGE_NAME", "GOOGLE_SERVICE_ACCOUNT_JSON"),
}, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAuth(req, res);
  if (!decoded) return;

  const payload = cleanReceiptPayload(req.body);
  if (!payload.productId || !payload.receipt) {
    json(res, 400, { error: "productId and receipt are required" });
    return;
  }

  try {
    const receiptHash = crypto.createHash("sha256").update(payload.receipt).digest("hex");
    const receiptId = buildReceiptId(decoded.uid, payload);
    const receiptRef = db.collection("iap_receipts").doc(receiptId);
    const provider = resolveIapStore(payload);
    let verification;
    if (provider === "apple") {
      verification = await verifyAppleReceipt(payload);
    } else if (provider === "google") {
      verification = await verifyGoogleReceipt(payload);
    } else {
      verification = {
        status: "pending_verification",
        valid: null,
        message: "Unknown store. Receipt stored for manual verification.",
      };
    }

    const proExpiresAt = verification.proExpiresAt
      ? admin.firestore.Timestamp.fromDate(verification.proExpiresAt)
      : null;

    await db.runTransaction(async (tx) => {
      tx.set(receiptRef, {
        uid: decoded.uid,
        productId: payload.productId,
        store: payload.store,
        resolvedProvider: provider,
        transactionId: verification.transactionId || payload.transactionId,
        receiptHash,
        platform: payload.platform,
        appVersion: payload.appVersion,
        status: verification.status,
        valid: verification.valid,
        providerStatus: verification.providerStatus || null,
        rawProductId: verification.rawProductId || "",
        proExpiresAt,
        message: verification.message || "",
        updatedAt: admin.firestore.FieldValue.serverTimestamp(),
        createdAt: admin.firestore.FieldValue.serverTimestamp(),
      }, { merge: true });

      if (verification.valid === true && verification.status === "verified") {
        tx.set(db.collection("users").doc(decoded.uid), {
          membershipStatus: "pro",
          proExpiresAt,
          membershipProvider: provider,
          membershipProductId: payload.productId,
          membershipTransactionId: verification.transactionId || payload.transactionId || receiptId,
          membershipUpdatedAt: admin.firestore.FieldValue.serverTimestamp(),
        }, { merge: true });
      }
    });

    const membership = await getMembership(decoded.uid);
    const httpStatus = verification.valid === false ? 402 : (verification.valid === true ? 200 : 202);
    json(res, httpStatus, {
      ok: verification.valid !== false,
      receiptId,
      status: verification.status,
      membershipStatus: membership.status,
      isPro: membership.isPro,
      proExpiresAt: membership.proExpiresAt ? membership.proExpiresAt.toDate().toISOString() : null,
      message: verification.message || "Receipt processed.",
    });
  } catch (error) {
    console.error("[submitIapReceipt]", error);
    json(res, 500, {
      error: error.message || "Submit receipt failed",
      code: "submit-receipt-error",
    });
  }
});

exports.publicConfig = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "GET")) return;

  try {
    const snap = await db.collection("app_config").doc("public").get();
    const config = mergePublicConfig(snap.exists ? snap.data() : {});
    json(res, 200, config);
  } catch (error) {
    console.error("[publicConfig]", error);
    json(res, 200, mergePublicConfig({}));
  }
});

exports.adminPublicConfigUpdate = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const update = {
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
      updatedBy: decoded.uid,
    };

    const socialLinks = cleanUrlMap(req.body?.socialLinks);
    if (Object.keys(socialLinks).length > 0) {
      update.socialLinks = socialLinks;
    }

    const iapProducts = cleanIapProducts(req.body?.iapProducts);
    if (Object.keys(iapProducts).length > 0) {
      update.iapProducts = iapProducts;
    }

    if (!update.socialLinks && !update.iapProducts) {
      json(res, 400, { error: "socialLinks or iapProducts is required" });
      return;
    }

    await db.collection("app_config").doc("public").set(update, { merge: true });
    const snap = await db.collection("app_config").doc("public").get();
    await writeAdminAuditLog(decoded, "public_config.update", {
      type: "config",
      id: "public",
      path: "app_config/public",
    }, {
      socialLinksUpdated: Boolean(update.socialLinks),
      iapProductsUpdated: Boolean(update.iapProducts),
    }, req);
    json(res, 200, { ok: true, config: mergePublicConfig(snap.data() || {}) });
  } catch (error) {
    console.error("[adminPublicConfigUpdate]", error);
    json(res, 500, { error: error.message || "Update public config failed" });
  }
});

exports.adminDashboardSummary = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "GET")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const limit = getAdminLimit(req.query.limit, 10, 30);
    const [
      usersCount,
      proUsersCount,
      feedbackNewCount,
      feedbackTriagedCount,
      feedbackInProgressCount,
      receiptsCount,
      paymentEventsCount,
      recentUsers,
      recentFeedbackDocs,
      recentPayments,
      recentAuditLogs,
      publicConfigSnap,
    ] = await Promise.all([
      getCollectionCount(db.collection("users"), "adminDashboardSummary.users"),
      getCollectionCount(
        db.collection("users").where("membershipStatus", "==", "pro"),
        "adminDashboardSummary.proUsers",
      ),
      getCollectionCount(
        db.collection("feedback").where("status", "==", "new"),
        "adminDashboardSummary.feedbackNew",
      ),
      getCollectionCount(
        db.collection("feedback").where("status", "==", "triaged"),
        "adminDashboardSummary.feedbackTriaged",
      ),
      getCollectionCount(
        db.collection("feedback").where("status", "==", "in_progress"),
        "adminDashboardSummary.feedbackInProgress",
      ),
      getCollectionCount(db.collection("iap_receipts"), "adminDashboardSummary.receipts"),
      getCollectionCount(db.collection("payment_events"), "adminDashboardSummary.paymentEvents"),
      listAdminUsersForAdmin({ limit }),
      getRecentAdminDocs("feedback", { orderBy: "createdAt", limit }),
      getRecentAdminDocs("payment_events", { orderBy: "createdAt", limit }),
      db.collection(ADMIN_AUDIT_LOG_COLLECTION)
        .orderBy("createdAt", "desc")
        .limit(limit)
        .get()
        .then((snap) => snap.docs.map(serializeAdminAuditLog))
        .catch((error) => {
          console.warn("[adminDashboardSummary] recent audit failed", error.message);
          return [];
        }),
      db.collection("app_config").doc("public").get().catch(() => null),
    ]);

    const publicConfig = mergePublicConfig(publicConfigSnap?.exists ? publicConfigSnap.data() : {});
    json(res, 200, {
      ok: true,
      summary: {
        users: usersCount,
        proUsers: proUsersCount,
        feedbackNew: feedbackNewCount,
        feedbackOpen: feedbackNewCount + feedbackTriagedCount + feedbackInProgressCount,
        receipts: receiptsCount,
        paymentEvents: paymentEventsCount,
        publicConfigReady: buildPublicConfigReadiness(publicConfig).configured,
      },
      recent: {
        users: recentUsers,
        feedback: recentFeedbackDocs,
        paymentEvents: recentPayments,
        auditLogs: recentAuditLogs,
      },
    });
  } catch (error) {
    console.error("[adminDashboardSummary]", error);
    json(res, error.status || 500, { error: error.message || "Load admin dashboard failed" });
  }
});

exports.adminConfigOverview = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "GET")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const limit = getAdminLimit(req.query.limit, 50, 100);
    const [publicConfigSnap, appConfigDocs, quickReadingDocs] = await Promise.all([
      db.collection("app_config").doc("public").get().catch(() => null),
      getRecentAdminDocs("app_config", { orderBy: "__name__", direction: "asc", limit }),
      getRecentAdminDocs("quick_reading", { orderBy: "__name__", direction: "asc", limit }),
    ]);
    const publicConfig = mergePublicConfig(publicConfigSnap?.exists ? publicConfigSnap.data() : {});

    json(res, 200, {
      ok: true,
      publicConfig,
      readiness: buildPublicConfigReadiness(publicConfig),
      appConfig: appConfigDocs,
      quickReading: quickReadingDocs,
      endpoints: {
        updatePublicConfig: "/adminPublicConfigUpdate",
        saveAdvancedConfig: "/adminDataUpsert",
      },
    });
  } catch (error) {
    console.error("[adminConfigOverview]", error);
    json(res, error.status || 500, { error: error.message || "Load config overview failed" });
  }
});

exports.submitFeedback = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAuth(req, res);
  if (!decoded) return;

  const content = cleanString(req.body?.content, "", 4000);
  if (content.length < 3) {
    json(res, 400, { error: "content is required" });
    return;
  }

  try {
    const feedbackId = cleanString(req.body?.feedbackId, crypto.randomUUID().replace(/-/g, ""), 80);
    const data = {
      feedbackId,
      uid: decoded.uid,
      displayName: cleanString(req.body?.displayName || decoded.name, "", 120),
      email: cleanString(req.body?.email || decoded.email, "", 160),
      category: cleanString(req.body?.category, "community", 80),
      tag: cleanString(req.body?.tag, "general", 80),
      content,
      source: cleanString(req.body?.source, "app", 80),
      status: "new",
      appVersion: cleanString(req.body?.appVersion, "", 40),
      platform: cleanString(req.body?.platform, "", 40),
      deviceModel: cleanString(req.body?.deviceModel, "", 120),
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    };

    const batch = db.batch();
    batch.set(
      db.collection("users").doc(decoded.uid).collection("feedback").doc(feedbackId),
      data,
      { merge: true },
    );
    batch.set(db.collection("feedback").doc(feedbackId), data, { merge: true });
    await batch.commit();

    json(res, 200, { ok: true, feedbackId });
  } catch (error) {
    console.error("[submitFeedback]", error);
    json(res, 500, { error: error.message || "Submit feedback failed" });
  }
});

exports.adminFeedbackList = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "GET")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const limit = Math.max(1, Math.min(Number(req.query.limit || 50), 100));
    const status = cleanString(req.query.status, "", 40);
    const snap = await db.collection("feedback")
      .orderBy("createdAt", "desc")
      .limit(limit)
      .get();

    let items = snap.docs.map(serializeFeedback);
    if (status && FEEDBACK_STATUSES.has(status)) {
      items = items.filter((item) => item.status === status);
    }

    json(res, 200, { items, count: items.length });
  } catch (error) {
    console.error("[adminFeedbackList]", error);
    json(res, 500, { error: error.message || "List feedback failed" });
  }
});

exports.adminFeedbackUpdate = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  const feedbackId = cleanString(req.body?.feedbackId, "", 100);
  const status = cleanString(req.body?.status, "", 40);
  if (!feedbackId || !FEEDBACK_STATUSES.has(status)) {
    json(res, 400, { error: "feedbackId and valid status are required" });
    return;
  }

  try {
    const feedbackRef = db.collection("feedback").doc(feedbackId);
    const snap = await feedbackRef.get();
    if (!snap.exists) {
      json(res, 404, { error: "Feedback not found" });
      return;
    }

    const existing = snap.data() || {};
    const update = {
      status,
      adminNote: cleanString(req.body?.adminNote, existing.adminNote || "", 2000),
      handledBy: decoded.uid,
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    };

    const batch = db.batch();
    batch.set(feedbackRef, update, { merge: true });
    if (existing.uid) {
      batch.set(
        db.collection("users").doc(existing.uid).collection("feedback").doc(feedbackId),
        update,
        { merge: true },
      );
    }
    await batch.commit();

    await writeAdminAuditLog(decoded, "feedback.update", {
      type: "feedback",
      id: feedbackId,
      uid: existing.uid || "",
      path: `feedback/${feedbackId}`,
    }, {
      previousStatus: existing.status || "",
      status,
      adminNoteChanged: Boolean(update.adminNote),
    }, req);
    json(res, 200, { ok: true });
  } catch (error) {
    console.error("[adminFeedbackUpdate]", error);
    json(res, 500, { error: error.message || "Update feedback failed" });
  }
});

exports.adminDataCollections = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "GET")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const rootCollections = await db.listCollections();
    const rootIds = new Set(rootCollections.map((collection) => collection.id));
    const knownRootPaths = new Set(ADMIN_ROOT_COLLECTIONS.map((definition) => definition.collectionPath));
    const known = ADMIN_COLLECTION_DEFINITIONS.map((definition) => ({
      ...definition,
      exists: definition.scope === "root" ? rootIds.has(definition.collectionPath) : null,
    }));
    const discovered = rootCollections
      .map((collection) => collection.id)
      .filter((collectionId) => !knownRootPaths.has(collectionId))
      .sort()
      .map((collectionId) => ({
        key: `path:${collectionId}`,
        label: collectionId,
        collectionPath: collectionId,
        scope: "root",
        orderBy: "__name__",
        orderDirection: "asc",
        idHint: "documentId",
        custom: true,
        exists: true,
      }));

    json(res, 200, {
      items: [...known, ...discovered],
      userSubcollections: USER_OWNED_SUBCOLLECTIONS,
    });
  } catch (error) {
    console.error("[adminDataCollections]", error);
    json(res, error.status || 500, { error: error.message || "List admin collections failed" });
  }
});

exports.adminUserSearch = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "GET")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const source = getAdminRequestSource(req);
    const q = cleanString(source.q || source.search, "", 160);
    const qLower = q.toLowerCase();
    const limit = Math.max(1, Math.min(Number(source.limit || 50), 100));
    if (!q) {
      throw createHttpError(400, "q is required");
    }

    const usersRef = db.collection("users");
    const publicProfilesRef = db.collection("public_profiles");
    const results = new Map();

    await addAdminUserByUid(results, q);

    if (q.includes("@")) {
      const authRecord = await admin.auth().getUserByEmail(q).catch((error) => {
        if (error.code !== "auth/user-not-found") {
          console.warn("[adminUserSearch] auth email lookup failed", error.message);
        }
        return null;
      });
      if (authRecord) {
        await addAdminUserByUid(results, authRecord.uid, authRecord);
      }
    }

    await Promise.all([
      addAdminUserDocsFromQuery(results, usersRef.where("email", "==", q).limit(limit)),
      addAdminUserDocsFromQuery(results, usersRef.where("email", "==", qLower).limit(limit)),
      addAdminUserDocsFromQuery(results, usersRef.where("emailLower", "==", qLower).limit(limit)),
      addAdminUserDocsFromQuery(results, usersRef.where("displayName", "==", q).limit(limit)),
      addAdminUserDocsFromQuery(results, usersRef.where("name", "==", q).limit(limit)),
      addAdminUserDocsFromQuery(results, usersRef.where("nickname", "==", q).limit(limit)),
      addAdminUserDocsFromQuery(
        results,
        usersRef
          .where("displayNameLower", ">=", qLower)
          .where("displayNameLower", "<=", `${qLower}\uf8ff`)
          .orderBy("displayNameLower")
          .limit(limit),
      ),
    ]);

    const publicProfileSnaps = await Promise.all([
      publicProfilesRef.where("email", "==", q).limit(limit).get().catch(() => null),
      publicProfilesRef.where("email", "==", qLower).limit(limit).get().catch(() => null),
      publicProfilesRef.where("displayName", "==", q).limit(limit).get().catch(() => null),
      publicProfilesRef
        .where("displayNameLower", ">=", qLower)
        .where("displayNameLower", "<=", `${qLower}\uf8ff`)
        .orderBy("displayNameLower")
        .limit(limit)
        .get()
        .catch(() => null),
    ]);
    for (const snap of publicProfileSnaps) {
      if (!snap) continue;
      for (const doc of snap.docs) {
        await addAdminUserByUid(results, doc.id);
      }
    }

    if (results.size < limit) {
      const fallbackSnap = await usersRef.limit(200).get().catch(() => null);
      if (fallbackSnap) {
        for (const doc of fallbackSnap.docs) {
          if (results.size >= limit) break;
          const data = doc.data() || {};
          const haystack = [
            doc.id,
            data.email,
            data.displayName,
            data.name,
            data.nickname,
            data.displayNameLower,
          ].filter(Boolean).join(" ").toLowerCase();
          if (haystack.includes(qLower) && !results.has(doc.id)) {
            const [publicProfileSnap, realtimePresence] = await Promise.all([
              publicProfilesRef.doc(doc.id).get().catch(() => null),
              getAdminRealtimePresenceByUid(doc.id),
            ]);
            results.set(doc.id, buildAdminUserItem(
              doc.id,
              doc,
              null,
              publicProfileSnap?.exists ? publicProfileSnap.data() || {} : {},
              realtimePresence,
            ));
          }
        }
      }
    }

    const items = [...results.values()].slice(0, limit);
    json(res, 200, {
      collectionPath: "users",
      collectionKey: "users",
      search: q,
      items,
      count: items.length,
    });
  } catch (error) {
    console.error("[adminUserSearch]", error);
    json(res, error.status || 500, { error: error.message || "Search users failed" });
  }
});

exports.adminUserList = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "GET")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const source = getAdminRequestSource(req);
    const limit = getAdminLimit(source.limit, 80, 200);
    const [items, total] = await Promise.all([
      listAdminUsersForAdmin({ limit }),
      getCollectionCount(db.collection("users"), "adminUserList.users"),
    ]);

    json(res, 200, {
      collectionPath: "users",
      collectionKey: "users",
      items,
      count: items.length,
      total,
      truncated: total > items.length,
      note: "Default user list includes documents without createdAt; sorted by recent activity when available.",
    });
  } catch (error) {
    console.error("[adminUserList]", error);
    json(res, error.status || 500, { error: error.message || "List users failed" });
  }
});

exports.adminAuthUserReconcile = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "GET")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const source = getAdminRequestSource(req);
    const result = await listAdminAuthUsersForReconcile({
      limit: source.limit,
      pageToken: source.pageToken,
      onlyMissing: source.onlyMissing,
    });
    json(res, 200, {
      ok: true,
      ...result,
    });
  } catch (error) {
    console.error("[adminAuthUserReconcile]", error);
    json(res, error.status || 500, { error: error.message || "Reconcile Auth users failed" });
  }
});

exports.adminRepairAuthUserProfile = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const uid = validateDocumentId(req.body?.uid);
    const authRecord = await admin.auth().getUser(uid).catch((error) => {
      if (error.code === "auth/user-not-found") throw createHttpError(404, "Auth user not found");
      throw error;
    });
    const userRef = db.collection("users").doc(uid);
    const publicProfileRef = db.collection("public_profiles").doc(uid);
    const [userSnap, publicProfileSnap] = await Promise.all([
      userRef.get(),
      publicProfileRef.get().catch(() => null),
    ]);
    const existingUser = userSnap.exists ? userSnap.data() || {} : {};
    const existingPublicProfile = publicProfileSnap?.exists ? publicProfileSnap.data() || {} : {};
    const { userData, publicProfileData } = buildAdminUserRepairDocsFromAuth(
      authRecord,
      decoded,
      existingUser,
      existingPublicProfile,
    );
    const repairUserDoc = !userSnap.exists;
    const repairPublicProfile = !publicProfileSnap?.exists;

    if (repairUserDoc || repairPublicProfile) {
      const batch = db.batch();
      if (repairUserDoc) {
        batch.set(userRef, userData, { merge: true });
      }
      if (repairPublicProfile) {
        batch.set(publicProfileRef, publicProfileData, { merge: true });
      }
      await batch.commit();
    }

    await writeAdminAuditLog(decoded, "user.auth_repair", {
      type: "user",
      id: uid,
      uid,
      path: `users/${uid}`,
    }, {
      repairUserDoc,
      repairPublicProfile,
      email: getAdminScalarText(authRecord.email, 160),
      loginType: getAuthRecordProviderLabel(authRecord),
    }, req);

    json(res, 200, {
      ok: true,
      uid,
      repairUserDoc,
      repairPublicProfile,
      repaired: repairUserDoc || repairPublicProfile,
      ...(await buildAdminUserBundle(uid, { limit: 12 })),
    });
  } catch (error) {
    console.error("[adminRepairAuthUserProfile]", error);
    json(res, error.status || 500, { error: error.message || "Repair Auth user profile failed" });
  }
});

exports.adminUserDetail = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "GET")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const source = getAdminRequestSource(req);
    const uid = await resolveAdminUserUid(source);
    if (!uid) {
      throw createHttpError(400, "uid, email, or search is required");
    }

    json(res, 200, {
      ok: true,
      ...(await buildAdminUserBundle(uid, { limit: source.limit })),
    });
  } catch (error) {
    console.error("[adminUserDetail]", error);
    json(res, error.status || 500, { error: error.message || "Load user detail failed" });
  }
});

exports.adminAgentConsole = onRequest(runtime, async (req, res) => {
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const source = getAdminRequestSource(req);
    const action = cleanString(source.action, "", 80);
    const result = await handleAdminAgentAction(action, source, req, decoded);
    json(res, 200, result);
  } catch (error) {
    console.error("[adminAgentConsole]", error);
    json(res, error.status || 500, { error: error.message || "Agent admin action failed" });
  }
});

exports.adminAuditLogs = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "GET")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const source = getAdminRequestSource(req);
    const limit = getAdminLimit(source.limit, 80, 200);
    const actionFilter = cleanString(source.action, "", 120).toLowerCase();
    const adminFilter = cleanString(source.admin, "", 200).toLowerCase();
    const targetFilter = cleanString(source.target, "", 300).toLowerCase();
    const fetchLimit = actionFilter || adminFilter || targetFilter ? Math.min(limit * 4, 400) : limit;
    let snap;

    try {
      snap = await db.collection(ADMIN_AUDIT_LOG_COLLECTION)
        .orderBy("createdAt", "desc")
        .limit(fetchLimit)
        .get();
    } catch (error) {
      console.warn("[adminAuditLogs] ordered query failed, retrying without order", error.message);
      snap = await db.collection(ADMIN_AUDIT_LOG_COLLECTION).limit(fetchLimit).get();
    }

    let items = snap.docs.map(serializeAdminAuditLog);
    if (actionFilter) {
      items = items.filter((item) => item.action.toLowerCase().includes(actionFilter));
    }
    if (adminFilter) {
      items = items.filter((item) => (
        item.adminEmail.toLowerCase().includes(adminFilter)
        || item.adminUid.toLowerCase().includes(adminFilter)
      ));
    }
    if (targetFilter) {
      items = items.filter((item) => [
        item.targetType,
        item.targetId,
        item.targetPath,
        item.targetUid,
        JSON.stringify(item.target || {}),
      ].join(" ").toLowerCase().includes(targetFilter));
    }
    items = items.slice(0, limit);

    json(res, 200, {
      ok: true,
      items,
      count: items.length,
      collectionPath: ADMIN_AUDIT_LOG_COLLECTION,
    });
  } catch (error) {
    console.error("[adminAuditLogs]", error);
    json(res, error.status || 500, { error: error.message || "Load admin audit logs failed" });
  }
});

exports.adminMembershipOverview = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "GET")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const source = getAdminRequestSource(req);
    let uid = await resolveAdminUserUid(source);
    const transactionId = cleanString(source.transactionId || source.txn || "", "", 220);
    const q = cleanString(source.q || source.search, "", 220);

    if (!uid && transactionId) {
      uid = await resolveUidByTransactionId(transactionId);
    }
    if (!uid && q) {
      uid = await resolveUidByTransactionId(q);
    }
    if (!uid) {
      throw createHttpError(400, "uid, email, or transactionId is required");
    }

    const bundle = await buildAdminUserBundle(uid, { limit: source.limit || 20 });
    json(res, 200, {
      ok: true,
      ...bundle,
      actions: {
        grantPro: "/adminGrantPro",
        revokePro: "/adminRevokePro",
        refresh: "/adminMembershipOverview",
      },
    });
  } catch (error) {
    console.error("[adminMembershipOverview]", error);
    json(res, error.status || 500, { error: error.message || "Load membership overview failed" });
  }
});

exports.adminCreateRegisteredUser = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  let authRecord = null;
  let firestoreCommitted = false;
  try {
    const email = cleanString(req.body?.email, "", 160).toLowerCase();
    const password = String(req.body?.password || "");
    const displayName = cleanString(
      req.body?.displayName,
      email.includes("@") ? email.split("@")[0] : "Fari User",
      120,
    );
    const photoUrl = cleanOptionalUrl(req.body?.photoUrl || req.body?.photoURL, 1000);
    const birthday = normalizeOptionalBirthday(req.body?.birthday);
    const birthTime = normalizeOptionalBirthTime(req.body?.birthTime);
    const city = cleanString(req.body?.city || req.body?.address, "", 160);

    if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email)) {
      throw createHttpError(400, "Valid email is required");
    }
    if (password.length < 6 || password.length > 128) {
      throw createHttpError(400, "Password must be 6 to 128 characters");
    }

    authRecord = await admin.auth().createUser({
      email,
      password,
      displayName,
      photoURL: photoUrl || undefined,
      emailVerified: false,
      disabled: req.body?.disabled === true,
    });

    const uid = authRecord.uid;
    const displayNameLower = normalizeSearchText(displayName);
    const emailLower = normalizeSearchText(email);
    const searchKeywords = buildSearchKeywords(displayName, email);
    const serverTimestamp = admin.firestore.FieldValue.serverTimestamp();
    const userData = {
      uid,
      displayName,
      displayNameLower,
      searchKeywords,
      email,
      emailLower,
      photoUrl,
      avatarStoragePath: "",
      birthday,
      birthTime,
      city,
      bio: "",
      loginType: "Email",
      isEmailVerified: false,
      membershipStatus: "free",
      timezone: "",
      profileUpdatedAt: serverTimestamp,
      createdAt: serverTimestamp,
      adminCreated: true,
      adminCreatedBy: decoded.uid,
      adminCreatedAt: serverTimestamp,
    };
    const publicProfileData = {
      uid,
      displayName,
      displayNameLower,
      searchKeywords,
      email,
      emailLower,
      photoUrl,
      avatarStoragePath: "",
      bio: "",
      updatedAt: serverTimestamp,
    };

    const batch = db.batch();
    batch.set(db.collection("users").doc(uid), userData, { merge: true });
    batch.set(db.collection("public_profiles").doc(uid), publicProfileData, { merge: true });
    await batch.commit();
    firestoreCommitted = true;

    const snap = await db.collection("users").doc(uid).get();
    const doc = serializeAdminDocument(snap);
    await writeAdminAuditLog(decoded, "user.create", {
      type: "user",
      id: uid,
      uid,
      path: `users/${uid}`,
    }, {
      email,
      displayName,
      hasPhotoUrl: Boolean(photoUrl),
      hasBirthData: Boolean(birthday || birthTime || city),
    }, req);
    json(res, 200, {
      ok: true,
      uid,
      email,
      doc: { ...doc, summary: getDocumentSummary(doc.data) },
    });
  } catch (error) {
    if (authRecord && !firestoreCommitted) {
      await admin.auth().deleteUser(authRecord.uid).catch((deleteError) => {
        console.error("[adminCreateRegisteredUser] rollback failed", deleteError);
      });
    }

    console.error("[adminCreateRegisteredUser]", error);
    const isDuplicateEmail = error?.code === "auth/email-already-exists";
    json(res, error.status || (isDuplicateEmail ? 409 : 500), {
      error: isDuplicateEmail ? "Email already exists" : error.message || "Create registered user failed",
      code: isDuplicateEmail ? "email-already-exists" : "create-user-error",
    });
  }
});

exports.adminUpdateUserProfile = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const body = req.body || {};
    const hasBodyField = (...fields) => fields.some((field) => (
      Object.prototype.hasOwnProperty.call(body, field)
    ));
    const uid = validateDocumentId(req.body?.uid);
    const userRef = db.collection("users").doc(uid);
    const publicProfileRef = db.collection("public_profiles").doc(uid);
    const [userSnap, publicProfileSnap, authRecord] = await Promise.all([
      userRef.get(),
      publicProfileRef.get().catch(() => null),
      getAuthRecordSafely(uid),
    ]);
    if (!userSnap.exists && !authRecord) {
      throw createHttpError(404, "User not found");
    }

    const existingUser = userSnap.exists ? userSnap.data() || {} : {};
    const existingPublicProfile = publicProfileSnap?.exists ? publicProfileSnap.data() || {} : {};
    const existingEmail = getAdminScalarText(
      existingUser.email || existingPublicProfile.email || authRecord?.email,
      160,
    ).toLowerCase();
    const displayName = cleanString(
      hasBodyField("displayName") ? body.displayName : undefined,
      existingUser.displayName || existingPublicProfile.displayName || authRecord?.displayName || "Fari User",
      120,
    );
    if (!displayName) {
      throw createHttpError(400, "Display name is required");
    }

    const existingPhotoUrl = getAdminScalarText(
      existingUser.photoUrl || existingUser.photoURL || existingPublicProfile.photoUrl || authRecord?.photoURL,
      1000,
    );
    const photoUrl = hasBodyField("photoUrl", "photoURL")
      ? cleanOptionalUrl(body.photoUrl ?? body.photoURL ?? "", 1000)
      : existingPhotoUrl;
    const birthday = hasBodyField("birthday")
      ? normalizeOptionalBirthday(body.birthday)
      : getAdminScalarText(existingUser.birthday, 40);
    const birthTime = hasBodyField("birthTime")
      ? normalizeOptionalBirthTime(body.birthTime)
      : getAdminScalarText(existingUser.birthTime, 20);
    const city = hasBodyField("city", "address")
      ? cleanString(body.city ?? body.address, "", 160)
      : getAdminScalarText(existingUser.city || existingUser.address, 160);
    const bio = hasBodyField("bio")
      ? cleanString(body.bio, "", 500)
      : getAdminScalarText(existingUser.bio || existingPublicProfile.bio, 500);
    const displayNameLower = normalizeSearchText(displayName);
    const emailLower = normalizeSearchText(existingEmail);
    const searchKeywords = buildSearchKeywords(displayName, existingEmail);
    const serverTimestamp = admin.firestore.FieldValue.serverTimestamp();

    if (authRecord) {
      await admin.auth().updateUser(uid, {
        displayName,
        photoURL: photoUrl || null,
      });
    }

    const userData = {
      uid,
      displayName,
      displayNameLower,
      searchKeywords,
      photoUrl,
      avatarStoragePath: existingUser.avatarStoragePath || "",
      birthday,
      birthTime,
      city,
      bio,
      profileUpdatedAt: serverTimestamp,
      updatedAt: serverTimestamp,
      adminProfileUpdatedBy: decoded.uid,
      adminProfileUpdatedAt: serverTimestamp,
    };
    if (existingEmail) {
      userData.email = existingEmail;
      userData.emailLower = emailLower;
    }

    const publicProfileData = {
      uid,
      displayName,
      displayNameLower,
      searchKeywords,
      photoUrl,
      avatarStoragePath: existingPublicProfile.avatarStoragePath || existingUser.avatarStoragePath || "",
      bio,
      updatedAt: serverTimestamp,
      adminProfileUpdatedBy: decoded.uid,
    };
    if (existingEmail) {
      publicProfileData.email = existingEmail;
      publicProfileData.emailLower = emailLower;
    }

    const batch = db.batch();
    batch.set(userRef, userData, { merge: true });
    batch.set(publicProfileRef, publicProfileData, { merge: true });
    await batch.commit();

    await writeAdminAuditLog(decoded, "user.profile_update", {
      type: "user",
      id: uid,
      uid,
      path: `users/${uid}`,
    }, {
      updatedFields: [
        hasBodyField("displayName") ? "displayName" : "",
        hasBodyField("photoUrl", "photoURL") ? "photoUrl" : "",
        hasBodyField("birthday") ? "birthday" : "",
        hasBodyField("birthTime") ? "birthTime" : "",
        hasBodyField("city", "address") ? "city" : "",
        hasBodyField("bio") ? "bio" : "",
      ].filter(Boolean),
      displayName,
      hasPhotoUrl: Boolean(photoUrl),
    }, req);
    const bundle = await buildAdminUserBundle(uid, { limit: 12 });
    json(res, 200, {
      ok: true,
      uid,
      user: bundle.user,
      membership: bundle.membership,
      publicProfile: bundle.publicProfile,
    });
  } catch (error) {
    console.error("[adminUpdateUserProfile]", error);
    json(res, error.status || 500, { error: error.message || "Update user profile failed" });
  }
});

async function getAdminUserFriendProfile(uid) {
  const cleanUid = validateDocumentId(uid);
  const [authRecord, userSnap, publicProfileSnap] = await Promise.all([
    getAuthRecordSafely(cleanUid),
    db.collection("users").doc(cleanUid).get().catch(() => null),
    db.collection("public_profiles").doc(cleanUid).get().catch(() => null),
  ]);
  const user = userSnap?.exists ? userSnap.data() || {} : {};
  const publicProfile = publicProfileSnap?.exists ? publicProfileSnap.data() || {} : {};
  return {
    uid: cleanUid,
    displayName: getAdminScalarText(
      user.displayName
        || user.name
        || user.nickname
        || publicProfile.displayName
        || publicProfile.name
        || publicProfile.nickname
        || authRecord?.displayName
        || cleanUid,
      120,
    ),
    email: getAdminScalarText(user.email || publicProfile.email || authRecord?.email, 160),
    photoUrl: getAdminScalarText(user.photoUrl || user.photoURL || publicProfile.photoUrl || authRecord?.photoURL, 1000),
  };
}

function buildManualRealFriendDoc(targetProfile, decoded, note = "") {
  return {
    uid: targetProfile.uid,
    displayName: targetProfile.displayName,
    email: targetProfile.email,
    photoUrl: targetProfile.photoUrl,
    status: "friend",
    source: "adminManual",
    adminNote: note,
    adminAddedBy: decoded.uid,
    adminAddedAt: admin.firestore.FieldValue.serverTimestamp(),
    updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  };
}

function buildManualFriendInviteOutgoingDoc(targetProfile, decoded, note = "") {
  return {
    uid: targetProfile.uid,
    displayName: targetProfile.displayName,
    email: targetProfile.email,
    photoUrl: targetProfile.photoUrl,
    status: "pendingSent",
    source: "adminInvite",
    adminNote: note,
    adminAddedBy: decoded.uid,
    adminAddedAt: admin.firestore.FieldValue.serverTimestamp(),
    updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  };
}

function buildManualFriendInviteIncomingDoc(requesterProfile, decoded, note = "") {
  return {
    uid: requesterProfile.uid,
    displayName: requesterProfile.displayName,
    email: requesterProfile.email,
    photoUrl: requesterProfile.photoUrl,
    status: "pendingReceived",
    source: "adminInvite",
    message: note || "管理员代发好友邀请",
    adminNote: note,
    adminAddedBy: decoded.uid,
    adminAddedAt: admin.firestore.FieldValue.serverTimestamp(),
    updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  };
}

exports.adminAddRealFriend = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const ownerUid = await resolveAdminUserUid({
      uid: req.body?.uid || req.body?.ownerUid,
      email: req.body?.email || req.body?.ownerEmail,
      search: req.body?.ownerSearch,
    });
    const friendUid = await resolveAdminUserUid({
      uid: req.body?.friendUid,
      email: req.body?.friendEmail,
      search: req.body?.friendSearch || req.body?.friend || req.body?.q,
    });
    if (!ownerUid || !friendUid) {
      throw createHttpError(400, "Owner user and friend user are required");
    }
    if (ownerUid === friendUid) {
      throw createHttpError(400, "Cannot add the user as their own friend");
    }

    const note = cleanString(req.body?.note || req.body?.reason, "管理员手动添加真实好友", 500);
    const [ownerProfile, friendProfile] = await Promise.all([
      getAdminUserFriendProfile(ownerUid),
      getAdminUserFriendProfile(friendUid),
    ]);

    const rawMode = cleanString(req.body?.mode || req.body?.action, "force", 40).toLowerCase();
    const mode = ["invite", "request", "pending"].includes(rawMode) ? "invite" : "force";
    const batch = db.batch();

    if (mode === "invite") {
      batch.set(
        db.collection("users").doc(friendUid).collection("friends").doc(ownerUid),
        buildManualFriendInviteOutgoingDoc(ownerProfile, decoded, note),
        { merge: true },
      );
      batch.set(
        db.collection("users").doc(ownerUid).collection("friend_requests").doc(friendUid),
        buildManualFriendInviteIncomingDoc(friendProfile, decoded, note),
        { merge: true },
      );
    } else {
      batch.set(
        db.collection("users").doc(ownerUid).collection("friends").doc(friendUid),
        buildManualRealFriendDoc(friendProfile, decoded, note),
        { merge: true },
      );
      batch.set(
        db.collection("users").doc(friendUid).collection("friends").doc(ownerUid),
        buildManualRealFriendDoc(ownerProfile, decoded, note),
        { merge: true },
      );
      batch.delete(db.collection("users").doc(ownerUid).collection("friend_requests").doc(friendUid));
      batch.delete(db.collection("users").doc(friendUid).collection("friend_requests").doc(ownerUid));
    }
    await batch.commit();

    await writeAdminAuditLog(decoded, "friend.real_add", {
      type: "friend",
      id: `${ownerUid}_${friendUid}`,
      uid: ownerUid,
      path: `users/${ownerUid}/friends/${friendUid}`,
    }, {
      mode,
      ownerUid,
      friendUid,
      friendEmail: friendProfile.email,
      note,
    }, req);
    json(res, 200, {
      ok: true,
      mode,
      uid: ownerUid,
      friendUid,
      owner: ownerProfile,
      friend: friendProfile,
    });
  } catch (error) {
    console.error("[adminAddRealFriend]", error);
    json(res, error.status || 500, { error: error.message || "Add real friend failed" });
  }
});

exports.adminAddVirtualFriend = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const uid = await resolveAdminUserUid({
      uid: req.body?.uid || req.body?.ownerUid,
      email: req.body?.email || req.body?.ownerEmail,
      search: req.body?.ownerSearch,
    });
    if (!uid) {
      throw createHttpError(400, "Owner user is required");
    }

    const virtualFriendId = cleanString(
      req.body?.virtualFriendId,
      crypto.randomUUID().replace(/-/g, ""),
      120,
    ).replace(/[^A-Za-z0-9_-]/g, "_");
    const name = cleanString(req.body?.name || req.body?.displayName, "", 120);
    if (!name) {
      throw createHttpError(400, "Virtual friend name is required");
    }

    const avatarUrl = cleanOptionalUrl(req.body?.avatarUrl || req.body?.photoUrl || "", 1000);
    const data = {
      virtualFriendId,
      name,
      relationship: cleanString(req.body?.relationship, "好友", 120),
      birthday: normalizeOptionalBirthday(req.body?.birthday),
      birthTime: normalizeOptionalBirthTime(req.body?.birthTime),
      city: cleanString(req.body?.city || req.body?.address, "", 160),
      notes: cleanString(req.body?.notes || req.body?.background, "", 2000),
      avatarKey: "",
      avatarUrl,
      avatarStoragePath: cleanString(req.body?.avatarStoragePath, "", 1000),
      lastOperatedUnixMs: Date.now(),
      isDeleted: false,
      source: "adminManual",
      adminAddedBy: decoded.uid,
      adminAddedAt: admin.firestore.FieldValue.serverTimestamp(),
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
    };

    await db.collection("users")
      .doc(uid)
      .collection("virtual_friends")
      .doc(validateDocumentId(virtualFriendId))
      .set(data, { merge: true });

    await writeAdminAuditLog(decoded, "friend.virtual_add", {
      type: "virtual_friend",
      id: virtualFriendId,
      uid,
      path: `users/${uid}/virtual_friends/${virtualFriendId}`,
    }, {
      name,
      relationship: data.relationship,
      hasAvatarUrl: Boolean(avatarUrl),
      hasBirthData: Boolean(data.birthday || data.birthTime || data.city),
    }, req);
    json(res, 200, { ok: true, uid, virtualFriendId, data });
  } catch (error) {
    console.error("[adminAddVirtualFriend]", error);
    json(res, error.status || 500, { error: error.message || "Add virtual friend failed" });
  }
});

exports.adminDataList = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "GET")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const source = getAdminRequestSource(req);
    const { definition, collectionPath, parentUid } = resolveAdminCollection(source);
    const limit = Math.max(1, Math.min(Number(source.limit || 50), 200));
    const docId = cleanString(source.docId, "", 1500);

    if (docId) {
      const snap = await db.collection(collectionPath).doc(validateDocumentId(docId)).get();
      const docs = snap.exists ? [serializeAdminDocument(snap)] : [];
      const items = await enrichAdminDataItems(
        docs.map((doc) => ({ ...doc, summary: getDocumentSummary(doc.data) })),
        definition,
        parentUid,
      );
      json(res, 200, {
        collectionPath,
        collectionKey: definition.key,
        parentUid,
        items,
        count: items.length,
      });
      return;
    }

    let query = db.collection(collectionPath);
    const whereField = cleanString(source.whereField, "", 160);
    if (whereField) {
      if (!/^[A-Za-z0-9_.-]{1,160}$/.test(whereField)) {
        throw createHttpError(400, "whereField contains unsupported characters");
      }
      const whereOp = cleanString(source.whereOp, "==", 24);
      if (!["==", "!=", "<", "<=", ">", ">=", "array-contains"].includes(whereOp)) {
        throw createHttpError(400, "Unsupported whereOp");
      }
      const parsedWhereValue = parseJsonishValue(source.whereValue);
      if (parsedWhereValue === undefined) {
        throw createHttpError(400, "whereValue is required when whereField is set");
      }
      query = query.where(whereField, whereOp, deserializeFirestoreValue(parsedWhereValue));
    }

    const orderBy = cleanString(source.orderBy, definition.orderBy || "__name__", 160);
    const requestedDirection = cleanString(
      source.orderDirection,
      definition.orderDirection || "desc",
      8,
    );
    const orderDirection = requestedDirection === "asc" ? "asc" : "desc";

    let snap;
    try {
      snap = await query
        .orderBy(getOrderByValue(orderBy), orderDirection)
        .limit(limit)
        .get();
    } catch (error) {
      if (!orderBy || orderBy === "__name__") throw error;
      console.warn("[adminDataList] ordered query failed, retrying without order", {
        collectionPath,
        orderBy,
        error: error.message,
      });
      snap = await query.limit(limit).get();
    }

    const items = await enrichAdminDataItems(
      snap.docs
        .map(serializeAdminDocument)
        .map((doc) => ({ ...doc, summary: getDocumentSummary(doc.data) })),
      definition,
      parentUid,
    );
    json(res, 200, {
      collectionPath,
      collectionKey: definition.key,
      parentUid,
      items,
      count: items.length,
    });
  } catch (error) {
    console.error("[adminDataList]", error);
    json(res, error.status || 500, { error: error.message || "List admin data failed" });
  }
});

exports.adminDataUpsert = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const source = getAdminRequestSource(req);
    const rawData = source.data;
    if (!rawData || typeof rawData !== "object" || Array.isArray(rawData)) {
      throw createHttpError(400, "data must be a JSON object");
    }

    let docRef;
    const documentPath = cleanString(source.documentPath, "", 900);
    if (documentPath) {
      docRef = db.doc(validateFirestorePath(documentPath, "document"));
    } else {
      const { collectionPath } = resolveAdminCollection(source);
      const docId = cleanString(source.docId, "", 1500);
      docRef = docId
        ? db.collection(collectionPath).doc(validateDocumentId(docId))
        : db.collection(collectionPath).doc();
    }

    const data = deserializeFirestoreValue(rawData);
    const merge = source.merge !== false;
    await docRef.set(data, { merge });
    const snap = await docRef.get();
    const doc = serializeAdminDocument(snap);
    await writeAdminAuditLog(decoded, "data.upsert", {
      type: "data",
      id: docRef.id,
      path: docRef.path,
    }, {
      merge,
      collectionPath: docRef.parent.path,
      fieldCount: Object.keys(rawData).length,
    }, req);
    json(res, 200, {
      ok: true,
      doc: { ...doc, summary: getDocumentSummary(doc.data) },
      updatedBy: decoded.uid,
    });
  } catch (error) {
    console.error("[adminDataUpsert]", error);
    json(res, error.status || 500, { error: error.message || "Save admin data failed" });
  }
});

exports.adminDataDelete = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const source = getAdminRequestSource(req);
    const docRef = resolveAdminDocumentRef(source);
    const recursive = source.recursive === true;

    if (recursive) {
      if (typeof db.recursiveDelete !== "function") {
        throw createHttpError(500, "recursiveDelete is not available in this runtime");
      }
      await db.recursiveDelete(docRef);
    } else {
      await docRef.delete();
    }

    await writeAdminAuditLog(decoded, "data.delete", {
      type: "data",
      id: docRef.id,
      path: docRef.path,
    }, {
      recursive,
      collectionPath: docRef.parent.path,
    }, req);
    json(res, 200, {
      ok: true,
      documentPath: docRef.path,
      recursive,
      deletedBy: decoded.uid,
    });
  } catch (error) {
    console.error("[adminDataDelete]", error);
    json(res, error.status || 500, { error: error.message || "Delete admin data failed" });
  }
});

exports.deleteMyAccountData = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAuth(req, res);
  if (!decoded) return;

  try {
    const deletedDocs = await deleteUserOwnedData(decoded.uid);
    let authDeleted = true;

    try {
      await admin.auth().deleteUser(decoded.uid);
    } catch (error) {
      if (error?.code === "auth/user-not-found") {
        authDeleted = false;
      } else {
        throw error;
      }
    }

    json(res, 200, { ok: true, uid: decoded.uid, deletedDocs, authDeleted });
  } catch (error) {
    console.error("[deleteMyAccountData]", error);
    json(res, 500, {
      error: error.message || "Delete account data failed",
      code: "delete-account-data-failed",
    });
  }
});

exports.aiChat = onRequest({ ...runtime, secrets: boundSecrets("DEEPSEEK_API_KEY") }, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAuth(req, res);
  if (!decoded) return;

  let usageReserved = false;
  try {
    const requestBody = req.body || {};
    const membership = await getMembership(decoded.uid);
    await incrementUsage(decoded.uid, "aiChat", membership.isPro ? PRO_AI_DAILY_LIMIT : FREE_AI_DAILY_LIMIT);
    usageReserved = true;

    if (shouldNotifyDialogueReply(requestBody)) {
      await markDialogueReplyJobProcessing(decoded.uid, requestBody, "http");
    }

    const response = await callDeepSeek(requestBody, false);
    const data = await response.json();
    const content = data?.choices?.[0]?.message?.content || "";
    await completeDialogueReplyJob(decoded.uid, requestBody, content, "http");
    const notificationId = await enqueueDialogueReplyNotification(decoded.uid, requestBody, content);
    json(res, 200, {
      content,
      usage: data.usage || null,
      model: data.model || null,
      membershipStatus: membership.status,
      notificationId,
    });
  } catch (error) {
    console.error("[aiChat]", error);
    if (usageReserved && error.code !== "quota-exceeded") {
      await refundUsageSafely(decoded.uid, "aiChat");
    }
    json(res, error.status || (error.code === "quota-exceeded" ? 429 : 500), {
      error: error.message || "AI request failed",
      code: error.code || "ai-error",
      limit: error.limit,
      used: error.used,
    });
  }
});

exports.aiChatStream = onRequest({ ...runtime, secrets: boundSecrets("DEEPSEEK_API_KEY") }, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAuth(req, res);
  if (!decoded) return;

  let usageReserved = false;
  try {
    const requestBody = req.body || {};
    const membership = await getMembership(decoded.uid);
    await incrementUsage(decoded.uid, "aiChat", membership.isPro ? PRO_AI_DAILY_LIMIT : FREE_AI_DAILY_LIMIT);
    usageReserved = true;

    if (shouldNotifyDialogueReply(requestBody)) {
      await markDialogueReplyJobProcessing(decoded.uid, requestBody, "http");
    }

    const response = await callDeepSeek(requestBody, true);
    const decoder = new TextDecoder();
    const streamState = { buffer: "", fullText: "" };
    let clientOpen = true;
    res.on("close", () => {
      clientOpen = false;
    });

    res.setHeader("Content-Type", "text/event-stream; charset=utf-8");
    res.setHeader("Cache-Control", "no-cache, no-transform");
    res.setHeader("Connection", "keep-alive");

    for await (const chunk of response.body) {
      const text = decoder.decode(chunk, { stream: true });
      collectStreamTextFromChunk(streamState, text);
      if (clientOpen && !res.destroyed && !res.writableEnded) {
        try {
          res.write(chunk);
        } catch (writeError) {
          clientOpen = false;
          console.warn("[aiChatStream] client stream write failed; continuing server-side completion", writeError.message);
        }
      }
    }
    const finalText = decoder.decode();
    if (finalText) collectStreamTextFromChunk(streamState, finalText);
    if (streamState.buffer) collectStreamTextFromChunk(streamState, "\n");

    await completeDialogueReplyJob(decoded.uid, requestBody, streamState.fullText, "http");
    await enqueueDialogueReplyNotification(decoded.uid, requestBody, streamState.fullText);

    if (clientOpen && !res.destroyed && !res.writableEnded) {
      res.end();
    }
  } catch (error) {
    console.error("[aiChatStream]", error);
    if (usageReserved && !res.headersSent && error.code !== "quota-exceeded") {
      await refundUsageSafely(decoded.uid, "aiChat");
    }
    if (!res.headersSent) {
      json(res, error.status || (error.code === "quota-exceeded" ? 429 : 500), {
        error: error.message || "AI stream failed",
        code: error.code || "ai-stream-error",
      });
      return;
    }
    if (!res.destroyed && !res.writableEnded) {
      res.write(`event: error\ndata: ${JSON.stringify({ error: error.message })}\n\n`);
      res.end();
    }
  }
});

exports.processStaleDialogueReplyJobs = onSchedule({
  schedule: "every 5 minutes",
  region: FIRESTORE_TRIGGER_REGION,
  timeoutSeconds: 300,
  memory: "512MiB",
  secrets: boundSecrets("DEEPSEEK_API_KEY"),
}, async () => {
  let processed = 0;
  let skipped = 0;

  for (const status of ["queued", "client_streaming"]) {
    const snap = await db.collectionGroup(DIALOG_REPLY_JOBS_COLLECTION)
      .where("status", "==", status)
      .limit(DIALOG_REPLY_JOB_SCAN_LIMIT)
      .get();

    for (const doc of snap.docs) {
      const result = await processDialogueReplyJobDoc(doc);
      processed += result.processed || 0;
      if (!result.processed) skipped += 1;
    }
  }

  console.log("[processStaleDialogueReplyJobs]", { processed, skipped });
});

exports.ttsSynthesize = onRequest({
  ...runtime,
  secrets: boundSecrets("VOLC_TTS_API_KEY"),
}, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAuth(req, res);
  if (!decoded) return;

  let usageReserved = false;
  try {
    const text = String(req.body?.text || "").trim();
    if (!text) {
      json(res, 400, { error: "text is required" });
      return;
    }
    if (text.length > 1200) {
      json(res, 400, { error: "text is too long" });
      return;
    }

    const membership = await getMembership(decoded.uid);
    await incrementUsage(decoded.uid, "tts", membership.isPro ? PRO_TTS_DAILY_LIMIT : FREE_TTS_DAILY_LIMIT);
    usageReserved = true;

    const encoding = req.body?.encoding || "mp3";
    const apiKey = volcanoTtsApiKey.value();
    if (!apiKey) throw new Error("VOLC_TTS_API_KEY is not configured");

    const result = await callVolcanoTtsV3({
      apiKey,
      uid: decoded.uid,
      text,
      encoding,
      speaker: resolveVolcanoTtsSpeaker(req.body),
      resourceId: resolveVolcanoTtsResourceId(req.body),
      speedRatio: Number(req.body?.speedRatio || 1.0),
      volumeRatio: Number(req.body?.volumeRatio || 1.0),
    });

    json(res, 200, {
      audioBase64: result.audioBase64,
      encoding: result.encoding || encoding,
      reqid: result.reqid,
      membershipStatus: membership.status,
    });
  } catch (error) {
    console.error("[ttsSynthesize]", error);
    if (usageReserved && error.code !== "quota-exceeded") {
      await refundUsageSafely(decoded.uid, "tts");
    }
    json(res, error.code === "quota-exceeded" ? 429 : 500, {
      error: error.message || "TTS request failed",
      code: error.code || "tts-error",
    });
  }
});

function resolveVolcanoTtsSpeaker(body) {
  const explicitSpeaker = cleanString(body?.speaker, "", 120);
  if (explicitSpeaker) return explicitSpeaker;

  const legacyVoiceType = cleanString(body?.voiceType, "", 120);
  if (legacyVoiceType.includes("bigtts")) return legacyVoiceType;

  return "zh_female_vv_uranus_bigtts";
}

function resolveVolcanoTtsResourceId(body) {
  return cleanString(body?.resourceId, "seed-tts-2.0", 120) || "seed-tts-2.0";
}

async function callVolcanoTtsV3(options) {
  const requestId = crypto.randomUUID();
  const format = normalizeVolcanoV3Format(options.encoding);
  const payload = {
    user: {
      uid: options.uid,
    },
    namespace: "BidirectionalTTS",
    req_params: {
      text: options.text,
      speaker: options.speaker,
      audio_params: {
        format,
        sample_rate: 24000,
        speech_rate: ratioToVolcanoRate(options.speedRatio),
        loudness_rate: ratioToVolcanoRate(options.volumeRatio),
      },
    },
  };

  const ttsResponse = await fetch(VOLCANO_TTS_V3_URL, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Api-Key": options.apiKey,
      "X-Api-Resource-Id": options.resourceId,
      "X-Api-Request-Id": requestId,
    },
    body: JSON.stringify(payload),
  });

  const responseText = await ttsResponse.text();
  if (!ttsResponse.ok) {
    throw new Error(`Volcano TTS V3 error ${ttsResponse.status}: ${responseText.slice(0, 500)}`);
  }

  const audioBase64 = extractVolcanoV3AudioBase64(responseText);
  if (!audioBase64) {
    throw new Error(`Volcano TTS V3 response error: ${responseText.slice(0, 500)}`);
  }

  return {
    audioBase64,
    encoding: format === "ogg_opus" ? "ogg" : format,
    reqid: requestId,
  };
}

function extractVolcanoV3AudioBase64(responseText) {
  const chunks = [];
  const regex = /"data"\s*:\s*"((?:\\.|[^"])*)"/g;
  let match;
  while ((match = regex.exec(responseText)) !== null) {
    if (match[1]) chunks.push(match[1].replace(/\\\//g, "/").replace(/\\n|\\r|\\t/g, ""));
  }

  if (!chunks.length) return "";
  return Buffer.concat(chunks.map((chunk) => Buffer.from(chunk, "base64"))).toString("base64");
}

function normalizeVolcanoV3Format(encoding) {
  const value = String(encoding || "mp3").toLowerCase();
  if (value === "ogg") return "ogg_opus";
  if (value === "wav") return "mp3";
  return value;
}

function ratioToVolcanoRate(ratio) {
  const value = Number(ratio || 1.0);
  if (value >= 1) return Math.round(Math.min(value - 1, 1) * 100);
  return Math.round(Math.max(value - 1, -0.5) * 100);
}

function timingSafeHexEqual(value, expected) {
  const normalized = String(value || "").replace(/^sha256=/i, "").trim();
  if (!/^[a-f0-9]{64}$/i.test(normalized)) return false;

  const actualBuffer = Buffer.from(normalized, "hex");
  const expectedBuffer = Buffer.from(expected, "hex");
  if (actualBuffer.length !== expectedBuffer.length) return false;

  return crypto.timingSafeEqual(actualBuffer, expectedBuffer);
}

function parseOptionalTimestamp(value) {
  if (!value) return null;

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    const err = new Error("proExpiresAt must be a valid date string");
    err.status = 400;
    throw err;
  }

  return admin.firestore.Timestamp.fromDate(date);
}

function parseGrantProDays(value) {
  const days = Number(value || 30);
  if (!Number.isFinite(days) || days <= 0 || days > 3660) {
    throw createHttpError(400, "days must be between 1 and 3660");
  }
  return Math.ceil(days);
}

exports.adminGrantPro = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const uid = validateDocumentId(req.body?.uid);
    const days = parseGrantProDays(req.body?.days);
    const reason = cleanString(req.body?.reason, "manual_admin_grant", 500);
    const now = Date.now();
    const proExpiresAtDate = new Date(now + days * 24 * 60 * 60 * 1000);
    const proExpiresAt = admin.firestore.Timestamp.fromDate(proExpiresAtDate);
    const userRef = db.collection("users").doc(uid);
    const eventRef = db.collection("payment_events").doc(`manual_pro_${uid}_${now}`);

    await db.runTransaction(async (tx) => {
      const userSnap = await tx.get(userRef);
      if (!userSnap.exists) {
        throw createHttpError(404, "User not found");
      }

      tx.set(userRef, {
        membershipStatus: "pro",
        proExpiresAt,
        membershipProvider: "admin",
        membershipProductId: "manual_pro_grant",
        membershipUpdatedAt: admin.firestore.FieldValue.serverTimestamp(),
        manualProGrantedBy: decoded.uid,
        manualProGrantedAt: admin.firestore.FieldValue.serverTimestamp(),
        manualProGrantReason: reason,
      }, { merge: true });

      tx.set(eventRef, {
        uid,
        provider: "admin",
        type: "manual_pro_grant",
        status: "granted",
        valid: true,
        membershipStatus: "pro",
        productId: "manual_pro_grant",
        proExpiresAt,
        handledBy: decoded.uid,
        adminNote: reason,
        createdAt: admin.firestore.FieldValue.serverTimestamp(),
      });
    });

    await writeAdminAuditLog(decoded, "membership.grant_pro", {
      type: "membership",
      id: uid,
      uid,
      path: `users/${uid}`,
    }, {
      days,
      reason,
      proExpiresAt: proExpiresAtDate.toISOString(),
      eventPath: eventRef.path,
    }, req);
    json(res, 200, {
      ok: true,
      uid,
      days,
      membershipStatus: "pro",
      proExpiresAt: proExpiresAtDate.toISOString(),
    });
  } catch (error) {
    console.error("[adminGrantPro]", error);
    json(res, error.status || 500, {
      error: error.message || "Grant Pro failed",
      code: error.status === 400 ? "invalid-grant-pro-payload" : "grant-pro-error",
    });
  }
});

exports.adminRevokePro = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAdmin(req, res);
  if (!decoded) return;

  try {
    const uid = validateDocumentId(req.body?.uid);
    const reason = cleanString(req.body?.reason, "manual_admin_revoke", 500);
    const now = Date.now();
    const userRef = db.collection("users").doc(uid);
    const eventRef = db.collection("payment_events").doc(`manual_pro_revoke_${uid}_${now}`);

    await db.runTransaction(async (tx) => {
      const userSnap = await tx.get(userRef);
      if (!userSnap.exists) {
        throw createHttpError(404, "User not found");
      }

      tx.set(userRef, {
        membershipStatus: "free",
        proExpiresAt: admin.firestore.FieldValue.delete(),
        membershipProvider: "admin",
        membershipProductId: "manual_pro_revoke",
        membershipUpdatedAt: admin.firestore.FieldValue.serverTimestamp(),
        manualProRevokedBy: decoded.uid,
        manualProRevokedAt: admin.firestore.FieldValue.serverTimestamp(),
        manualProRevokeReason: reason,
      }, { merge: true });

      tx.set(eventRef, {
        uid,
        provider: "admin",
        type: "manual_pro_revoke",
        status: "revoked",
        valid: false,
        membershipStatus: "free",
        productId: "manual_pro_revoke",
        handledBy: decoded.uid,
        adminNote: reason,
        createdAt: admin.firestore.FieldValue.serverTimestamp(),
      });
    });

    await writeAdminAuditLog(decoded, "membership.revoke_pro", {
      type: "membership",
      id: uid,
      uid,
      path: `users/${uid}`,
    }, {
      reason,
      eventPath: eventRef.path,
    }, req);
    json(res, 200, {
      ok: true,
      uid,
      membershipStatus: "free",
    });
  } catch (error) {
    console.error("[adminRevokePro]", error);
    json(res, error.status || 500, {
      error: error.message || "Revoke Pro failed",
      code: error.status === 400 ? "invalid-revoke-pro-payload" : "revoke-pro-error",
    });
  }
});

exports.sendTestPush = onRequest(runtime, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;
  const decoded = await requireAuth(req, res);
  if (!decoded) return;

  try {
    const body = req.body || {};
    const result = await sendRemotePushToUser(decoded.uid, {
      type: "diagnostic",
      title: body.title || "Nocturne Oracle",
      body: body.body || "远程推送测试成功",
      data: {
        source: "sendTestPush",
        sentAt: new Date().toISOString(),
      },
    });

    json(res, 200, { ok: result.sent > 0, ...result });
  } catch (error) {
    console.error("[sendTestPush]", error);
    json(res, 500, { error: error.message || "Send test push failed" });
  }
});

exports.friendRequestRemotePush = onDocumentCreated({
  region: FIRESTORE_TRIGGER_REGION,
  document: "users/{uid}/friend_requests/{requesterUid}",
}, async (event) => {
  const uid = event.params.uid;
  const requesterUid = event.params.requesterUid;
  if (!uid || !requesterUid || uid === requesterUid) return;

  const data = event.data?.data() || {};
  if (data.status && data.status !== "pendingReceived") return;

  const displayName = truncatePushText(data.displayName || data.email, "有人", 32);
  const result = await sendRemotePushToUser(uid, {
    preferenceKey: "friendInteractionEnabled",
    type: "friend_request",
    title: "新的好友请求",
    body: `${displayName} 想添加你为好友`,
    data: {
      requesterUid,
      source: "friend_requests",
    },
  });

  console.log("[friendRequestRemotePush]", { uid, requesterUid, ...result });
});

exports.relationshipInviteRemotePush = onDocumentCreated({
  region: FIRESTORE_TRIGGER_REGION,
  document: "relationship_divinations/{readingId}",
}, async (event) => {
  const readingId = event.params.readingId;
  const data = event.data?.data() || {};
  const receiverUid = String(data.receiverUid || "");
  const initiatorUid = String(data.initiatorUid || "");
  if (!receiverUid || receiverUid === initiatorUid) return;
  if (receiverUid.startsWith("virtual:") || receiverUid.startsWith("debug_")) return;
  if (data.status && data.status !== "invited") return;

  const initiatorName = truncatePushText(data.initiatorName, "好友", 32);
  const result = await sendRemotePushToUser(receiverUid, {
    preferenceKey: "friendInteractionEnabled",
    type: "relationship_invite",
    title: "新的双人占卜邀请",
    body: `${initiatorName} 邀请你一起抽牌`,
    data: {
      readingId,
      initiatorUid,
      source: "relationship_divinations",
    },
  });

  console.log("[relationshipInviteRemotePush]", { receiverUid, readingId, ...result });
});

exports.remoteNotificationOutboxPush = onDocumentCreated({
  region: FIRESTORE_TRIGGER_REGION,
  document: "users/{uid}/remote_notifications/{notificationId}",
}, async (event) => {
  const uid = event.params.uid;
  const notificationId = event.params.notificationId;
  const data = event.data?.data() || {};

  try {
    const result = await sendRemotePushToUser(uid, {
      preferenceKey: data.preferenceKey || "",
      type: data.type || "remote_notification",
      title: data.title || "Nocturne Oracle",
      body: data.body || "你有一条新的提醒",
      clickAction: data.clickAction || "",
      data: {
        notificationId,
        source: PUSH_OUTBOX_COLLECTION,
        ...(data.payload || {}),
      },
    });

    await event.data.ref.set({
      pushStatus: result.skipped || (result.sent > 0 ? "sent" : "failed"),
      pushSentCount: result.sent,
      pushFailureCount: result.failureCount,
      pushedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });

    console.log("[remoteNotificationOutboxPush]", { uid, notificationId, ...result });
  } catch (error) {
    console.error("[remoteNotificationOutboxPush]", error);
    await event.data.ref.set({
      pushStatus: "error",
      pushError: error.message || "Remote notification push failed",
      pushedAt: admin.firestore.FieldValue.serverTimestamp(),
    }, { merge: true });
  }
});

exports.paymentWebhook = onRequest({ ...runtime, secrets: boundSecrets("PAYMENT_WEBHOOK_SECRET") }, async (req, res) => {
  if (!requireMethod(req, res, "POST")) return;

  const secret = paymentWebhookSecret.value();
  if (!secret) {
    json(res, 503, { error: "PAYMENT_WEBHOOK_SECRET is not configured" });
    return;
  }

  const signature = req.get("x-fari-signature") || "";
  const rawBody = req.rawBody || Buffer.from(JSON.stringify(req.body || {}));
  const expected = crypto.createHmac("sha256", secret).update(rawBody).digest("hex");
  if (!timingSafeHexEqual(signature, expected)) {
    json(res, 401, { error: "Invalid webhook signature" });
    return;
  }

  const { uid, membershipStatus, proExpiresAt, provider, transactionId, eventId } = req.body || {};
  if (!uid || !["free", "pro"].includes(membershipStatus)) {
    json(res, 400, { error: "uid and valid membershipStatus are required" });
    return;
  }

  try {
    const parsedProExpiresAt = parseOptionalTimestamp(proExpiresAt);
    const userRef = db.collection("users").doc(uid);
    const eventRef = db.collection("payment_events").doc(eventId || transactionId || crypto.randomUUID());

    await db.runTransaction(async (tx) => {
      const eventSnap = await tx.get(eventRef);
      if (eventSnap.exists) return;

      tx.set(eventRef, {
        uid,
        provider: provider || "unknown",
        transactionId: transactionId || "",
        membershipStatus,
        proExpiresAt: parsedProExpiresAt,
        payload: req.body || {},
        createdAt: admin.firestore.FieldValue.serverTimestamp(),
      });

      tx.set(userRef, {
        membershipStatus,
        proExpiresAt: parsedProExpiresAt,
        membershipProvider: provider || "unknown",
        membershipUpdatedAt: admin.firestore.FieldValue.serverTimestamp(),
      }, { merge: true });
    });

    json(res, 200, { ok: true });
  } catch (error) {
    console.error("[paymentWebhook]", error);
    json(res, error.status || 500, {
      error: error.message || "Payment webhook failed",
      code: error.status === 400 ? "invalid-payment-payload" : "payment-webhook-error",
    });
  }
});
