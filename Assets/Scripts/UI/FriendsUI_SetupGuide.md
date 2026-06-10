# FriendsUI 设置指南

## 一、脚本文件清单

| 文件路径 | 说明 |
|---------|------|
| `Scripts/Data/FriendsData.cs` | 好友数据模型（FriendData / FriendGroupData） |
| `Scripts/UI/FriendsUI.cs` | 主窗口逻辑（继承 WindowBase） |
| `Scripts/UI/FriendsUIComponent.cs` | 组件绑定脚本 |
| `Scripts/UI/FriendsGroupItem.cs` | 分组头部 Item（已有好友 / 创建的好友） |
| `Scripts/UI/FriendsChildItem.cs` | 好友卡片子项 Item |
| `Scripts/UI/FriendsActionButtonsItem.cs` | 底部操作按钮 Item |

## 二、Prefab 结构

### 1. FriendsUI.prefab（主窗口）

```
FriendsUI (GameObject)
├── Canvas (Canvas)
│   ├── TitleBar (空物体 / 布局容器)
│   │   ├── TitleText (TextMeshProUGUI) -> 绑定到 FriendsUIComponent.titleText
│   │   ├── NotificationButton (Button) -> 绑定到 FriendsUIComponent.notificationButton
│   │   └── NotificationBadge (TextMeshProUGUI) -> 绑定到 FriendsUIComponent.notificationBadgeText
│   ├── InviteNotification (GameObject) -> 绑定到 FriendsUIComponent.inviteNotificationGo
│   │   ├── Icon (Image)
│   │   ├── TitleText (TextMeshProUGUI)
│   │   ├── DescText (TextMeshProUGUI)
│   │   └── ViewButton (Button) -> 绑定到 FriendsUIComponent.viewInviteButton
│   ├── FriendsContext (GameObject) -> 绑定到 FriendsUIComponent.friendsContextGo
│   │   ├── DescText (TextMeshProUGUI)
│   │   └── AddButton (Button) -> 绑定到 FriendsUIComponent.contextAddButton
│   └── LoopListView (LoopListView2) -> 绑定到 FriendsUIComponent.loopListView
│       ├── ScrollRect
│       ├── Viewport
│       └── Content (RectTransform)
└── (挂载脚本)
    ├── FriendsUI.cs
    └── FriendsUIComponent.cs
```

### 2. Item Prefab 配置（在 LoopListView2 的 ItemPrefabDataList 中注册）

#### FriendsGroupItem.prefab
```
FriendsGroupItem (GameObject, 挂载 FriendsGroupItem.cs)
├── GroupNameText (TextMeshProUGUI) -> groupNameText
├── CountText (TextMeshProUGUI) -> countText
├── Arrow (RectTransform / Image) -> arrowTransform
└── HeaderButton (Button) -> headerButton
```

**关键设置：**
- Arrow 默认 Rotation Z = -90（展开状态）
- 点击 HeaderButton 触发展开/折叠

#### FriendsChildItem.prefab
```
FriendsChildItem (GameObject, 挂载 FriendsChildItem.cs)
├── AvatarImage (Image) -> avatarImage
├── NameText (TextMeshProUGUI) -> nameText
├── TypeText (TextMeshProUGUI) -> typeText
├── CityText (TextMeshProUGUI) -> cityText
└── AtButton (Button) -> atButton
```

#### FriendsActionButtonsItem.prefab
```
FriendsActionButtonsItem (GameObject, 挂载 FriendsActionButtonsItem.cs)
├── AddFriendButton (Button) -> addFriendButton
│   └── AddFriendText (TextMeshProUGUI) -> addFriendText
└── CreateFriendButton (Button) -> createFriendButton
    └── CreateFriendText (TextMeshProUGUI) -> createFriendText
```

## 三、LoopListView2 配置步骤

1. 在 FriendsUI Prefab 中创建 `LoopListView2` 组件（或使用已有的 SuperScrollView 预制体）
2. 将 `LoopListView2` 拖到 FriendsUIComponent 的 `loopListView` 字段
3. 在 `LoopListView2` 的 **ItemPrefabDataList** 中添加 3 个条目：

| 索引 | ItemPrefab | 对应脚本 |
|-----|-----------|---------|
| 0 | FriendsGroupItem.prefab | FriendsGroupItem.cs |
| 1 | FriendsChildItem.prefab | FriendsChildItem.cs |
| 2 | FriendsActionButtonsItem.prefab | FriendsActionButtonsItem.cs |

4. 确保 `LoopListView2` 的 `SupportScrollbar` 按需开启
5. 设置 `ItemDefaultWithPaddingSize` 为合适的高度值（如分组头 60，好友卡片 120）

## 四、UI 框架注册

在 `UIModule` 或窗口配置中将 FriendsUI 注册为可弹出的窗口。例如：

```csharp
// 在合适的初始化位置
UIModule.Instance.PopUpWindow<FriendsUI>();
```

## 五、与底部导航栏联动

底部导航栏（NavigationUI）已包含 `friendToggle`，在其回调中弹出 FriendsUI：

```csharp
// NavigationUI.cs 中
public void OnfriendToggleChange(bool isOn)
{
    if (isOn)
    {
        UIModule.Instance.PopUpWindow<FriendsUI>();
    }
}
```

## 六、展开/折叠效果说明

- 分组头部右侧的数字是实时的好友数量
- 点击分组头部时，箭头会旋转 180°（DOTween 动画）
- 折叠后该分组下的好友卡片会从列表中移除，列表自动重新排版
- 展开/折叠状态会同步到 `FriendGroupData.isExpanded`

## 七、数据扩展

当前 `InitFriendData()` 中使用的是硬编码的模拟数据。接入真实数据时：

1. 从服务器或本地数据库获取好友列表
2. 按 `isRealFriend` 分组到 `FriendGroupData`
3. 调用 `InitTreeView()` 重新刷新列表

```csharp
// 刷新数据示例
public void RefreshFriendData(List<FriendData> allFriends)
{
    mGroupDataList.Clear();
    mTreeItemCountMgr.Clear();
    // ... 分组逻辑
    InitTreeView();
}
```

## 八、注意事项

1. **Item 复用**：LoopListView2 会自动复用滚出视野的 Item，因此 `InitListItem()` 只在首次创建时调用一次，数据绑定在 `SetItemListData()` 中完成
2. **Prefab 名称**：代码中使用的字符串 `"FriendsGroupItem"`、`"FriendsChildItem"`、`"FriendsActionButtonsItem"` 必须与 LoopListView2 的 ItemPrefabDataList 中配置的 Prefab 名称完全一致
3. **层级设置**：FriendsUIComponent 默认 `windowLayer = WindowLayer.MainUI`，与 TodayOracleUI 同级
