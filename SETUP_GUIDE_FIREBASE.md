# MoonlyApp — 占卜记录 Firebase 云存储 设置指南

> **自动生成日期**: 2026-06-17  
> **前置条件**: 项目中已有 Firebase SDK，且 FirebaseAuth 已配置并可正常登录。

---

## 一、需要你在 Unity Editor 中手动完成的步骤

### 1. 将 DivinationRecordFirestore 挂载到初始场景

`DivinationRecordFirestore` 是一个 `MonoSingleton`，需要在游戏启动时就存在。

**操作**:
- 打开你的**初始场景**（通常是 `Splash` 或 `Bootstrap` 场景）
- 在 Hierarchy 中创建一个空 GameObject，命名为 `DivinationRecordFirestore`
- 将 `Assets/Scripts/Platform/FireBase/DivinationRecordFirestore.cs` 脚本拖到该 GameObject 上
- **或者**：找到已有的 `GameManager` / `AppBoot` 等全局单例 GameObject，将脚本添加上去

> ⚠️ 确保该 GameObject 在场景加载顺序中**早于** `DialogUI` 和 `HistoryUI`。

---

### 2. 确认 Firebase Firestore 已在项目中启用

在 Unity Editor 中：
- 菜单 `Window → Firebase → Firestore`
- 确认 Firestore 已被添加到项目中（`Add Firestore SDK` 按钮应该显示为灰色/已添加）

如果未添加：
1. 打开 `Firebase Console` → 你的项目 → `Firestore Database`
2. 创建数据库（选择 `asia-southeast1` 或其他离用户近的区域）
3. 回到 Unity，`Window → Firebase → Firestore` → 点击安装

---

### 3. HistoryUI Prefab 配置

`HistoryUI` 预制体需要以下组件正确绑定：

| GameObject 路径 | 组件 | 说明 |
|---|---|---|
| `HistoryUI` (根节点) | `HistoryUI.cs` | 已自动生成 |
| `HistoryUI` (根节点) | `HistoryUIComponent.cs` | **必须**存在，处理按钮事件绑定 |
| 子节点 | `ScrollRect` (命名为 `HistoryListScrollScrollRect`) | 列表滚动容器 |
| 子节点 | `Button` (命名为 `BackButton`) | 返回按钮 |
| 子节点 | `Button` (命名为 `viewButton`) | 查看详情按钮 |

**在 `DivinationRecordUIComponent.cs` 中检查**：
- `HistoryListScrollScrollRect` 字段是否正确拖入了 ScrollRect 组件

---

### 4. DivinationRecordUI Prefab 配置

| GameObject 路径 | 组件 | 说明 |
|---|---|---|
| `DivinationRecordUI` (根节点) | `DivinationRecordUI.cs` | 已自动生成 |
| `DivinationRecordUI` (根节点) | `DivinationRecordUIComponent.cs` | **必须**存在 |
| 子节点 | `ScrollRect` (命名为 `RecordScrollContainerScrollRect`) | 详情内容滚动容器 |
| 子节点 | `Button` (命名为 `BackButton`) | 返回按钮 |
| 子节点 | `Button` (命名为 `ContinueAskButton`) | 继续追问按钮 |
| 子节点 | `Button` (命名为 `SaveToDiaryButton`) | 保存到日记按钮 |
| 子节点 | `Button` (命名为 `ShareResultButton`) | 分享结果按钮 |
| 子节点 | `Button` (命名为 `DeleteRecordButton`) | 删除记录按钮 |

**在 `DivinationRecordUIComponent.cs` 中检查**：
- 所有 Button 字段是否都已拖入正确的按钮对象

---

### 5. 在 Unity Editor 的 Player Settings 中确认

- `Edit → Project Settings → Player → Other Settings → Scripting Define Symbols`
- 确保没有 `DISABLE_FIREBASE` 等会禁用 Firebase 的宏定义

---

### 6. Firebase Firestore 安全规则（可选）

如果你希望用户在未登录时也能查看历史记录（但只能看自己的），在 Firebase Console → Firestore → Rules 中设置：

```javascript
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    match /users/{userId}/divination_records/{recordId} {
      allow read, write, delete: if request.auth != null && request.auth.uid == userId;
      allow create: if request.auth != null && request.auth.uid == userId;
    }
  }
}
```

---

## 二、功能验证清单

完成上述步骤后，按以下流程验证：

1. ✅ 启动 App → 登录 Firebase
2. ✅ 进入对话界面 → 问一个问题
3. ✅ AI 回复后，点击「为这个问题选牌阵」
4. ✅ 选择一个牌阵 → AI 解读
5. ✅ 解读完成后，Console 应输出：`[DivinationEngine] 占卜记录已同步至 Firestore`
6. ✅ 回到主界面 → 进入历史记录页（HistoryUI）
7. ✅ 应能看到刚才的占卜记录
8. ✅ 点击记录 → 进入详情页（DivinationRecordUI）→ 看到完整信息
9. ✅ 测试删除按钮 → 记录被删除

---

## 三、Firestore 数据结构

```
users/{uid}/divination_records/{readingId}
  ├── readingId:      string   (12位 hex UUID)
  ├── question:       string   (用户问题)
  ├── scene:          string   (场景)
  ├── spreadKind:     string   (牌阵类型)
  ├── lockedCards:    array    (抽到的牌详情)
  │     ├── positionKey:  string
  │     ├── position:     string
  │     ├── cardId:       string
  │     ├── cardName:     string
  │     └── orientation:  string  ("upright" / "reversed")
  ├── shortVerdict:   string   (AI 解读全文)
  ├── oracleId:       string   ("tarot" / "astrology" / "sage")
  ├── createdAt:      Timestamp
  └── updatedAt:      Timestamp
```

---

## 四、修改的文件清单

| 文件 | 操作 | 说明 |
|---|---|---|
| `Assets/Scripts/Platform/FireBase/DivinationRecordFirestore.cs` | **新建** | Firestore CRUD 管理器 + DivinationRecordData 数据模型 |
| `Assets/Scripts/UI/HistoryUI.cs` | **重写** | 从 Firestore 加载历史列表，动态生成列表项 |
| `Assets/Scripts/UI/DivinationRecordUI.cs` | **重写** | 详情页展示、继续追问、分享、删除 |
| `Assets/Scripts/Dialog/Data/DivinationEngine.cs` | **修改** | `CompleteDivination() `增加自动保存 Firestore |
| `Assets/Scripts/UI/DialogUI.cs` | **修改** | 流式完成时检测 `CardsLocked` 阶段并调用 `CompleteDivination` |

---

## 五、常见问题

**Q: Console 输出 "Firestore 未就绪，跳过自动保存"？**  
A: Firebase 可能尚未完成初始化。检查 `FirebaseAuthManager` 是否正确配置和初始化。

**Q: HistoryUI 显示"暂无占卜记录"？**  
A: 确保用户已登录 Firebase。匿名登录也可以，但 `FirebaseAuth.CurrentUser` 不能为空。

**Q: 编译报错找不到 `DivinationRecordFirestore`？**  
A: 确认文件已添加到 Unity 项目中，且 Firebase SDK 已正确安装。

**Q: 删除记录后 HistoryUI 没有刷新？**  
A: HistoryUI 在 `OnShow()` 时会自动调用 `RefreshList()` 重新从 Firestore 加载。返回历史页时会自动刷新。
