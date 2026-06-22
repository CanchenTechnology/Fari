# Feedback Admin 后台说明

## 入口

- 本地文件：`web-admin/feedback-admin.html`
- 线上地址：`https://fari-app-b2fd2.web.app/feedback-admin`
- 部署目标：Firebase Hosting，`firebase.json` 已配置 `hosting.site = fari-app-b2fd2`、`hosting.public = web-admin`
- 当前状态：已于 2026-06-20 部署到 live channel，并通过 HTTP 200 访问验证。
- 依赖接口：
  - `adminFeedbackList`
  - `adminFeedbackUpdate`

## 权限

后台接口要求 Firebase ID Token 对应用户满足任一条件：

- Firebase Auth custom claim：`admin == true`
- Firebase Auth custom claim：`role == "admin"`
- Firestore 用户文档：`users/{uid}.isAdmin == true`
- Firestore 用户文档：`users/{uid}.role == "admin"`

## 页面能力

- 邮箱密码登录 Firebase Auth。
- 支持手动粘贴 Firebase ID Token。
- 按状态筛选反馈。
- 调整每次加载数量。
- 查看用户、平台、版本、设备、反馈内容。
- 修改处理状态：`new`、`triaged`、`in_progress`、`resolved`、`closed`。
- 写入管理员备注。
- 导出当前列表 CSV。

## 反馈数据流

App 内反馈会写入：

```text
users/{uid}/feedback/{feedbackId}
feedback/{feedbackId}
```

管理员更新状态时，Cloud Function 会同步更新顶层 `feedback/{feedbackId}` 与用户自己的反馈记录。

## 部署

Firebase 重新登录后可执行：

```bash
firebase deploy --only functions,firestore:rules,storage,hosting --project fari-app-b2fd2
```

如果只部署后台静态页：

```bash
firebase deploy --only hosting --project fari-app-b2fd2
```
