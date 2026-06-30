# Unity 实现视频版三张塔罗洗牌动画方案

## 目标

把参考视频中的塔罗三张牌仪式流程，用 Unity UGUI 实时实现到当前项目的抽牌界面中：

1. 黑底、顶部说明、中央牌堆、底部提示。
2. 用户轻触牌堆后进入洗牌。
3. 牌堆分成两摞，交错洗牌，再合牌。
4. 牌堆展开成大弧形牌扇。
5. 用户从牌扇中挑选 3 张牌，牌飞入下方 3 个牌位。
6. 3 张牌进入结果流程，并复用现有牌阵数据、聊天记录、结果页逻辑。

本方案优先复用当前已有结构：

- UI 入口：`Assets/GameData/UI/Main/TodayDivination/TarorSingleSpreadShuffleUI.prefab`
- UI 脚本：`Assets/Scripts/UI/TarorSingleSpreadShuffleUI.cs`
- 桥接状态：`Assets/Scripts/UI/SpreadShuffleBridge.cs`
- 卡槽组件：`Assets/Scripts/Item/CardSlotItem.cs`
- 抽牌数据：`Assets/Scripts/Dialog/Data/TarotDeck.cs`
- 牌面资源：`Assets/Resources/TarotCards`
- 动画库：`Assets/GamerFrameWork/Plugins/Demigiant/DOTween`

## 实现策略

不要把动画做成 mp4 或序列帧。使用 UGUI `Image + RectTransform + DOTween` 动态生成卡牌背面，所有卡牌都是 UI 节点，可以响应点击、拖拽、长按和悬停。

现有 `TarorSingleSpreadShuffleUI.cs` 已经有牌扇和抽牌基础，但文件较大，继续堆动画会越来越难维护。建议新增一个独立视图控制器：

```text
Assets/Scripts/UI/TarotRitual/
  TarotRitualShuffleView.cs
  TarotRitualCardView.cs
  TarotRitualLayout.cs
  TarotRitualShuffleConfig.cs
```

`TarorSingleSpreadShuffleUI` 保留窗口生命周期、抽牌数据、`SpreadShuffleBridge` 通知；新脚本负责表现层动画和输入。

## Prefab 层级

在 `TarorSingleSpreadShuffleUI.prefab` 下整理出如下节点：

```text
TarorSingleSpreadShuffleUI
  SafeArea
    Background
    TopCopy
      TitleText
      SubtitleText
    RitualStage
      DeckRoot
      FanRoot
      FlyingRoot
      CardTemplate
    BottomPanel
      InstructionText
      SlotRoot
        CardSlotItem1
        CardSlotItem2
        CardSlotItem3
```

节点职责：

- `DeckRoot`：开场单张牌堆、分堆、合牌阶段的父节点。
- `FanRoot`：洗完后弧形牌扇的父节点，可横向拖动。
- `FlyingRoot`：选中牌飞向卡槽时临时挂载，避免被牌扇遮挡。
- `CardTemplate`：一张背面牌模板，运行时复制，编辑器里隐藏。
- `SlotRoot`：下方 3 个牌位，继续使用 `CardSlotItem`。

建议不要继续把所有引用写入自动生成的 `TarorSingleSpreadShuffleUIComponent`，因为它可能被 UI 工具覆盖。新增 `TarotRitualShuffleView` 挂在 prefab 根节点或 `RitualStage` 节点上，用手动序列化字段维护引用。

## 新增脚本设计

### TarotRitualShuffleConfig

用 `ScriptableObject` 或 `Serializable class` 管理动画参数。第一版可以先作为 `TarotRitualShuffleView` 的 `[Serializable]` 字段，避免多一个 asset。

核心参数：

```csharp
[Serializable]
public class TarotRitualShuffleConfig
{
    public int visualDeckCount = 54;
    public int selectableDeckCount = 36;

    public float introDuration = 0.35f;
    public float splitDuration = 0.45f;
    public float riffleCycleDuration = 0.9f;
    public int riffleCycles = 2;
    public float gatherDuration = 0.38f;
    public float fanOutDuration = 0.75f;
    public float selectFlyDuration = 0.48f;
    public float flipDuration = 0.4f;

    public float fanArcDegrees = 128f;
    public float fanRadius = 930f;
    public float fanYOffset = -420f;
    public float fanCardRotation = 34f;
    public float hoverLift = 42f;
    public float selectedSlotScale = 1f;
}
```

推荐默认表现：

| 阶段 | 时长 | Ease |
| --- | ---: | --- |
| 中央牌堆入场 | 0.35s | `OutCubic` |
| 分成左右两摞 | 0.45s | `InOutCubic` |
| 交错洗牌 | 0.9s * 2 | `InOutSine` |
| 合牌 | 0.38s | `OutBack` |
| 展开弧形牌扇 | 0.75s | `OutCubic` |
| 选中牌飞入槽位 | 0.48s | `InOutCubic` |
| 翻牌 | 0.40s | 手写 scaleX 翻转或 DOTween sequence |

### TarotRitualCardView

每张可视卡牌一个组件，封装 UI 和输入。

字段：

```csharp
public sealed class TarotRitualCardView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    public int Index { get; private set; }
    public bool IsSelected { get; private set; }
    public RectTransform Rect { get; private set; }
    public Image Image { get; private set; }
}
```

事件：

```csharp
public event Action<TarotRitualCardView> Clicked;
public event Action<TarotRitualCardView, Vector2> Dragged;
public event Action<TarotRitualCardView, Vector2> DragEnded;
```

卡牌只负责把输入抛出去，不直接修改抽牌结果。

### TarotRitualLayout

专门计算位置，保持动画代码干净。

需要提供 4 类布局：

1. `GetStackPose(index, count)`：中央牌堆，轻微错位和旋转。
2. `GetSplitPose(index, count)`：左右两摞。
3. `GetRifflePose(index, count, progress)`：两摞交错洗牌。
4. `GetFanPose(index, count, deckOffsetX)`：弧形牌扇。

牌扇计算建议：

```csharp
float t = count <= 1 ? 0.5f : index / (float)(count - 1);
float angle = Mathf.Lerp(-arcDegrees * 0.5f, arcDegrees * 0.5f, t);
float rad = angle * Mathf.Deg2Rad;

float x = Mathf.Sin(rad) * radius + deckOffsetX;
float y = Mathf.Cos(rad) * radius + fanYOffset;
float zRot = -angle * rotationMultiplier;
```

屏幕适配：

- `radius` 按 `stageRect.rect.width` 和 `stageRect.rect.height` 动态缩放。
- 宽屏可显示 54 张，窄屏显示 36 张但逻辑仍从 78 张牌里抽。
- 底部卡槽要留出至少 220px 安全空间，避免牌扇盖住卡槽。

### TarotRitualShuffleView

表现层主控制器。它不直接打开/关闭窗口，只向 `TarorSingleSpreadShuffleUI` 汇报完成。

状态机：

```csharp
private enum RitualState
{
    IdleIntro,
    WaitingTap,
    Splitting,
    RiffleShuffling,
    Gathering,
    FanOut,
    Selecting,
    Completing
}
```

公开 API：

```csharp
public void Initialize(
    int cardCount,
    IReadOnlyList<CardSlotItem> slots,
    Sprite cardBackSprite,
    Func<IReadOnlyList<(TarotCard card, bool upright)>> drawProvider,
    Action<IReadOnlyList<(TarotCard card, bool upright)>> onCompleted);

public void PlayIntro();
public void StartShuffle();
public void ResetView();
```

职责：

- 创建和回收可视牌。
- 播放入场、洗牌、展开牌扇动画。
- 在进入 `Selecting` 前调用 `drawProvider` 一次，锁定 3 张真实结果。
- 用户选牌时，按选择顺序把真实结果绑定到卡槽。
- 选满 3 张后调用 `onCompleted`。

## 接入现有 TarorSingleSpreadShuffleUI

### OnShow

保留现有逻辑：

- 读取 `SpreadShuffleBridge.PendingSpread`
- 计算 `_cardCount`
- `BuildSlotArray()`
- `ConfigureUI()`
- `ResolveCardBackSprite()`

替换或短路现有 `PrepareInteractiveDeck()`：

```csharp
_ritualView.Initialize(
    _cardCount,
    _slots.Take(_cardCount).ToList(),
    ResolveCardBackSprite(),
    DrawCardsForRitual,
    OnRitualCompleted);

_ritualView.PlayIntro();
```

### DrawCardsForRitual

只抽一次牌，保证用户选牌期间结果稳定：

```csharp
private IReadOnlyList<(TarotCard card, bool upright)> DrawCardsForRitual()
{
    _drawnCards = TarotDeck.DrawMultiple(_cardCount);
    SyncToDivinationEngine();
    return _drawnCards;
}
```

### OnRitualCompleted

复用当前完成流程：

```csharp
private void OnRitualCompleted(IReadOnlyList<(TarotCard card, bool upright)> draws)
{
    _drawnCards = draws.ToList();
    CompleteShuffle();
}
```

这样 `SaveDrawnCardsToPendingMessage()`、`SpreadShuffleBridge.NotifyComplete()`、`DivinationInfoUI` 打开逻辑都不用重写。

## 动画流程细节

### 1. 入场与轻触

画面：

- 顶部显示：“经典的「过去-现在-未来」三张牌牌阵……”
- 中央一张或一小摞背面牌。
- 牌下方显示：“轻触以洗牌”。
- 底部显示：“明确并专注于心中的问题”。

实现：

- `DeckRoot` 初始 `scale = 0.94`、`alpha = 0`、`anchoredPosition.y -= 24`。
- `DOFade(1, 0.35)`、`DOScale(1, 0.35)`、`DOAnchorPosY(targetY, 0.35)`。
- “轻触以洗牌”使用 `CanvasGroup` 做 1.0 到 0.65 的循环呼吸。

### 2. 分堆

把 `visualDeckCount` 张背面牌实例化出来。为了看起来像厚牌堆，每张牌有微小偏移：

```text
x = index * 0.6
y = -index * 0.25
rotation = sin(index * 0.7) * 1.2
```

分成左右两摞：

- 左摞移动到 `(-180, 0)`，旋转 `-4` 度。
- 右摞移动到 `(180, 0)`，旋转 `4` 度。
- 后半摞 sibling index 更高，保证遮挡关系自然。

### 3. 交错洗牌

每轮洗牌：

1. 左右两摞向中间靠近。
2. 按奇偶顺序交错插入。
3. 中间牌边缘产生 4 到 8px 的上下波动。
4. 合成一摞后再轻微弹回。

用 DOTween Sequence：

```csharp
Sequence seq = DOTween.Sequence();
for (int i = 0; i < cards.Count; i++)
{
    float delay = (i % half) * 0.012f;
    Vector2 target = GetRiffleTarget(i);
    seq.Join(cards[i].Rect.DOAnchorPos(target, 0.32f).SetDelay(delay).SetEase(Ease.InOutSine));
    seq.Join(cards[i].Rect.DORotate(new Vector3(0, 0, targetRot), 0.32f).SetDelay(delay));
}
```

### 4. 展开弧形牌扇

合牌后，`DeckRoot` 的牌移动到 `FanRoot`。所有牌沿弧形展开：

- 中心牌在屏幕中上方。
- 两侧牌延伸到屏幕外一点，模拟视频里的大弧。
- 牌数量过多时只显示 `selectableDeckCount` 张，其他牌可不实例化。

关键点：

- 每张牌 `Image.raycastTarget = true`。
- sibling order 按 `y` 或中心距离排序，中间牌盖在两侧上面。
- 展开完成后进入 `Selecting`。

### 5. 选择 3 张牌

支持 3 种输入：

- 点击牌：直接选中。
- 向上拖动超过阈值：选中。
- 长按 0.45s：选中。

交互反馈：

- hover：牌向上浮 `hoverLift`，加一点 glow 或 outline。
- drag：牌跟随手指上移，横向限制在 60px 内。
- release 未达阈值：回到牌扇位置。
- selected：关闭该牌输入，飞向当前卡槽。

飞入卡槽：

1. 复制一张临时牌到 `FlyingRoot`。
2. 原牌在牌扇中淡出或缩小。
3. 临时牌飞到目标 `CardSlotItem` 的世界位置。
4. 到位后把卡槽设为背面牌或直接翻面。

第一版建议：飞入后立即翻开，能更清楚看到“已选第几张”。如果产品想保持仪式感，也可以等 3 张都落槽后统一翻开。

### 6. 翻牌

复用项目已有水平缩放翻牌逻辑：

```text
scaleX 1 -> 0
切换 sprite
逆位则 rotationZ = 180
scaleX 0 -> 1
```

正逆位：

- `draw.upright == true`：`rotationZ = 0`
- `draw.upright == false`：`rotationZ = 180`

牌面图：

```csharp
Sprite sprite = TarotSpriteLoader.Load(draw.card.cardId);
```

## 文案

顶部主文案：

```text
经典的「过去-现在-未来」三张牌牌阵。洗牌、抽牌、拖拽、翻牌——让仪式感不止是文字，而是你亲手完成的过程。
```

底部阶段文案：

| 阶段 | 文案 |
| --- | --- |
| 等待轻触 | 明确并专注于心中的问题 |
| 洗牌中 | 让牌在你的问题里重新排序 |
| 展开牌扇 | 挑选 3 张牌 |
| 第 1 张 | 请选择第一张牌 |
| 第 2 张 | 请选择第二张牌 |
| 第 3 张 | 请选择第三张牌 |
| 完成 | 牌阵已经就位 |

如果 `SpreadShuffleBridge.PendingSpread.positions` 有位置名，则卡槽使用传入标签；否则三张牌默认：

```text
过去 / 现在 / 未来
```

## 资源需求

最低资源：

- 一张塔罗背面图，走 `TarorSingleSpreadShuffleUIComponent.cardBackSprite`。
- 78 张塔罗正面图，走 `TarotSpriteLoader.Load(cardId)`。
- 轻微光晕可以用 UGUI `Image` + 半透明径向图，也可以第一版只用颜色和 scale。

可选音效：

- `shuffle_start`
- `riffle`
- `card_select`
- `card_flip`

音效先预留接口，不阻塞动画实现。

## 代码落地步骤

### 第 1 步：Prefab 整理

1. 打开 `TarorSingleSpreadShuffleUI.prefab`。
2. 新增或确认 `RitualStage/DeckRoot/FanRoot/FlyingRoot/CardTemplate`。
3. `CardTemplate` 配置为背面牌 Image，`raycastTarget = false`，默认隐藏。
4. 新增 `TarotRitualShuffleView`，绑定节点和三个 `CardSlotItem`。

验收：打开界面时可以看到中央牌堆和文案，不影响返回按钮。

### 第 2 步：实现卡牌池

1. 新增 `TarotRitualCardView.cs`。
2. 新增 `TarotRitualShuffleView.CreateCards()`。
3. 支持运行时生成 `visualDeckCount` 张背面牌。
4. 支持 `ResetView()` 清理所有 tween 和临时节点。

验收：反复打开/关闭 UI，不残留运行时卡牌，不报 DOTween target destroyed 警告。

### 第 3 步：实现入场、分堆、交错洗牌

1. `PlayIntro()`：中央牌堆淡入。
2. `StartShuffle()`：停止提示呼吸，进入状态机。
3. `PlaySplit()`：左右分堆。
4. `PlayRiffle()`：按奇偶交错。
5. `PlayGather()`：合成一摞。

验收：点击牌堆后能看到类似视频中“分开、交错、合牌”的连续动画。

### 第 4 步：实现弧形展开

1. 新增 `TarotRitualLayout.GetFanPose()`。
2. `PlayFanOut()` 把牌展开成大弧形。
3. 根据屏幕宽度动态调整半径和数量。
4. 展开后启用卡牌输入。

验收：3160x1080 宽屏下牌扇接近视频效果；普通手机竖屏下不遮挡下方 3 个卡槽。

### 第 5 步：实现选牌飞入槽位

1. `TarotRitualCardView` 抛出点击/拖动事件。
2. `TarotRitualShuffleView.SelectCard(cardView)` 根据 `_selectedCount` 找目标卡槽。
3. 飞行牌挂到 `FlyingRoot`，从源牌世界坐标飞到槽位世界坐标。
4. 飞行完成后刷新 `CardSlotItem`。
5. 三张选满后调用 `onCompleted(draws)`。

验收：可以连续选择 3 张牌，每张按顺序进入对应卡槽，选中的牌不能重复选。

### 第 6 步：接入现有结果流程

1. `TarorSingleSpreadShuffleUI.OnShow()` 初始化 `_ritualView`。
2. 新增 `DrawCardsForRitual()`。
3. 新增 `OnRitualCompleted()` 调用现有 `CompleteShuffle()`。
4. 保持 `SaveDrawnCardsToPendingMessage()` 不变。

验收：抽完 3 张后，聊天消息、占卜记录、`DivinationInfoUI` 和现有结果页仍然能拿到同一组牌。

### 第 7 步：打磨与验证

1. 加 `CanvasGroup` 避免动画中重复点击。
2. 所有 Sequence 用 `SetTarget(this)`，窗口关闭时统一 `DOTween.Kill(this)`。
3. 检查 iOS/Android 安全区。
4. 检查低端机 `visualDeckCount`，必要时降到 36。

验收：打开/关闭 20 次无残留，无空引用，无明显卡顿。

## 关键风险与处理

| 风险 | 处理 |
| --- | --- |
| `TarorSingleSpreadShuffleUI.cs` 已经很大 | 新增表现层脚本，只在窗口脚本中做生命周期接入 |
| 自动生成的 Component 文件会覆盖手动字段 | 新增 `TarotRitualShuffleView` 保存引用，不依赖自动生成字段 |
| 54 张 UI Image 可能影响移动端 | 第一版 36 张可选牌，宽屏/高性能设备提高到 54 |
| 牌扇在竖屏遮挡卡槽 | `GetFanPose()` 根据 stage 高度动态降低半径和 fanYOffset |
| 结果牌与用户所点牌绑定混乱 | 进入 Selecting 前一次性抽好 `_drawnCards`，按选择顺序绑定结果 |
| 关闭窗口时 tween 还在跑 | `ResetView()` + `DOTween.Kill(this)` + 清理运行时对象 |

## 验收清单

- [ ] 进入抽牌界面后，看到黑底、顶部说明、中央牌堆、底部提示。
- [ ] 点击牌堆后，牌堆分成两摞并交错洗牌。
- [ ] 洗牌后，牌展开成弧形牌扇。
- [ ] 可通过点击、拖动或长按选择牌。
- [ ] 选择 3 张牌后，牌按顺序飞入 3 个卡槽。
- [ ] 3 张牌能正确显示正位/逆位。
- [ ] 抽牌结果写回 `SpreadShuffleBridge.PendingMessageData`。
- [ ] 完成后能进入 `DivinationInfoUI`。
- [ ] 多次打开关闭没有残留牌、残留 tween 或控制台错误。
- [ ] 宽屏、手机竖屏、平板分辨率下文案和卡牌不重叠。

## 建议排期

1. 半天：Prefab 整理、卡牌池、入场动画。
2. 半天：分堆、交错洗牌、合牌。
3. 半天：弧形展开、响应式布局。
4. 半天：选牌输入、飞入卡槽、翻牌。
5. 半天：接入现有结果流程、移动端适配、清理边界问题。

第一版可在 2 到 3 天内做出可运行版本；后续再加音效、粒子、光晕和更精细的物理手感。
