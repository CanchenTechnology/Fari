# MoonlyApp TTS 文本转语音接入指南

> 状态：代码已完成 | 需要你在 Unity Editor 中完成配置

---

## 一、已完成的工作（代码层面）

### 1.1 新建文件

| 文件 | 说明 |
|------|------|
| `Assets/Scripts/TTS/TTSManager.cs` | 火山引擎 TTS 核心客户端，支持三级缓存（内存 → 磁盘 → API） |

### 1.2 修改文件

| 文件 | 改动 | 说明 |
|------|------|------|
| `Assets/Scripts/Dialog/Item/ChatItem.cs` | +28行 | 增加 `ttsPlayButton` + `ttsLoadingIcon` 字段、`OnTTSPlayButtonClick` 回调、`ShowTTSLoading()` |
| `Assets/Scripts/UI/DialogUI.cs` | +60行 | 增加 `InitTTSManager()`、`OnChatItemTTSPlay()`、`StopTTSPlayback()`、流式 TTS 按钮联动 |
| `Assets/GamerFrameWork/XFGameFrameWork/AudioSystem/AudioManager.cs` | +8行 | 增加 `IsVoicePlaying()` 方法 |

### 1.3 架构

```
用户点击 Speaker 按钮
    → ChatItem.OnTTSPlayButtonClick()
    → DialogUI.OnChatItemTTSPlay()
    → TTSManager.Speak(text)
        ├── 内存缓存命中 → 直接返回 AudioClip
        ├── 磁盘缓存命中 → LoadFromDisk()
        └── 缓存未命中 → POST 火山引擎 API
            → 解析 base64 音频
            → 写入磁盘缓存
            → 加载为 AudioClip
    → AudioManager.PlayVoice(clip)
```

---

## 二、你需要做的事

### Step 1：获取火山引擎 TTS 密钥

1. 打开 [火山引擎控制台](https://console.volcengine.com/)
2. 进入 **语音合成**（TTS）服务
3. 获取：
   - **App ID**（应用ID）
   - **Access Token**（访问令牌）
4. 记录这两个值备用

> 参考文档：火山引擎 TTS API 文档 → `https://www.volcengine.com/docs/6561/79823`

### Step 2：在 Unity Editor 中配置 TTSManager

1. 在场景 **Hierarchy** 中找到 `DialogSystem` GameObject（或 `GameManager` / `AppStart` 等常驻节点）
2. 找到上面的 `TTSManager` 组件（如果没有，手动 Add Component → `TTSManager`）
3. 在 Inspector 中填入：

| 字段 | 值 | 说明 |
|------|-----|------|
| **App Id** | `你的火山引擎 AppID` | 从火山引擎控制台获取 |
| **Access Token** | `你的火山引擎 Access Token` | 从火山引擎控制台获取 |
| **Voice Type** | `zh_female_qingxin` | 音色ID，见下方音色表 |
| **Speed Ratio** | `1.0` | 语速 (0.5 ~ 2.0) |
| **Volume Ratio** | `1.0` | 音量 (0.5 ~ 2.0) |
| **Encoding** | `mp3` | 音频格式 (mp3/wav/ogg) |
| **Enable Disk Cache** | ✅ | 启用磁盘缓存，避免重复请求 |

#### 常用音色 ID

| Voice Type | 描述 | 适用场景 |
|------------|------|----------|
| `zh_female_qingxin` | 清新女声 | 塔罗师/神谕（推荐） |
| `zh_female_shuangkuaidv2` | 爽快女声 | 占星师 |
| `zh_male_qingrun` | 清润男声 | 冥想师 |
| `zh_female_wenrou` | 温柔女声 | 日常引导 |

### Step 3：在 ChatItem Prefab 中添加 TTS 播放按钮

**此步骤必须在 Unity Editor 中操作：**

1. 找到 AI 消息的 Prefab：
   - 路径：`Assets/...` 中的 `LeftDialogItem`（或类似名称的 Prefab）
   - 在 `DialogUI` 的 `LoopListView2` 中查看 Item Prefab 引用

2. 打开 Prefab，在 Canvas 下添加：
   - **Button**（作为 "播放语音" 按钮） → 命名 `BtnTTSPlay`
     - 给这个 Button 放一个 Speaker 图标（用小图片或 Text 显示 🔊）
     - 放在消息气泡右侧
   - **Image/GameObject**（可选，加载中旋转） → 命名 `TTSLoading`
     - 一个小的 loading 圈，默认隐藏

3. 将这两个控件拖到 `ChatItem` 脚本的对应字段：
   - `ttsPlayButton` ← 拖 `BtnTTSPlay`
   - `ttsLoadingIcon` ← 拖 `TTSLoading`

4. 确保 **RightDialogItem**（用户消息 Prefab）的 `ChatItem` 组件上这两个字段留空或不启用（用户消息不需要 TTS 按钮，代码已自动隐藏）。

### Step 4：确认 DailyCardBox（今日牌）也可以连 TTS

如果今日神谕页面也需要语音朗读：

1. 在 `TodayOracleUI` 组件所在的 Panel 中
2. 添加一个 `Button` 调用：
   ```csharp
   TTSManager.Instance?.PlayText(oraclePayload.title + "。" + oraclePayload.detail);
   ```

### Step 5：测试

1. 运行 Unity Editor
2. 打开对话界面
3. 发送一个问题（如 "今天运势怎么样"）
4. 等待 AI 回复
5. 点击气泡旁边的 **🔊 播放按钮**
6. 应该能听到 TTS 语音
7. 查看 Console：
   - `[TTSManager] TTS 合成完成: xxx... → 4.2s`
   - `[DialogUI] TTS 开始播放, 时长=4.2s`

---

## 三、音色更换与缓存清理

### 更换音色

在 TTSManager 组件的 Inspector 中修改 `Voice Type` 字段。

> ⚠️ 更换音色后，需要清除缓存才能对同文本重新合成：
> 调用 `TTSManager.Instance.ClearCache()`
> 或者在代码中临时调用：修改 DialogUI 的 `OnRegenerateVoiceClick()` 添加清除逻辑。

### 手动清理磁盘缓存

缓存目录：`Application.persistentDataPath/TTSCache/`（Android 通常在 `/data/data/包名/files/TTSCache/`）

---

## 四、安全提醒

> ⚠️ 当前实现是客户端直调火山引擎 TTS API，App ID 和 Access Token 会打包在 APK/IPA 中，任何反编译者都能看到。

**建议上线前改为 Cloud Functions 代理模式：**

```
客户端 → Firebase Cloud Function → 火山引擎 TTS API
```

这样可以保护 API Key，且 Cloud Function 可以统一管理调用频率和配额。

---

## 五、常见问题

| 问题 | 可能原因 | 解决 |
|------|----------|------|
| 点击播放按钮无反应 | ttsPlayButton 未绑定 | Step 3 |
| TTS 合成失败 code!=3000 | AppId/Token 错误 | Step 2，检查密钥 |
| 语音播放没有声音 | AudioManager 未初始化 | 确保场景中有 AudioManager |
| 每次点击都重新请求 | 磁盘缓存未启用 | 勾选 Enable Disk Cache |
| mp3 文件加载报错 | MP3 解码需要平台支持 | 尝试切换 encoding 为 wav |

---

## 六、下一步

以上 Step 1~5 完成后，TTS 即可在对话中工作。如果需要扩展到更多入口（如每日神谕完整解读朗读、注册引导语音），只需在任何需要朗读的地方调用：

```csharp
TTSManager.Instance?.PlayText("要朗读的文本内容");
```

---

_文档生成时间：2026-06-17 | 关联：fari迁移MoonlyApp能力评估.md → TTS_
