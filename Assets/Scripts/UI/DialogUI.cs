/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/9/2026 1:01:55 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using SuperScrollView;
using System.Collections.Generic;
using System.Collections;
using I2.Loc;

public class DialogUI : WindowBase
{
    public DialogUIComponent uiComponent;

    private LoopListView2 chatListView;

    private string userItemPrefabName = "ChatRootRight";
    private string aiItemPrefabName = "MessageItem";

    private DialogSystem dialogSystem;

    #region 生命周期函数
    // 调用机制与 Mono Awake 一致
    public override void OnAwake()
    {
        uiComponent = gameObject.GetComponent<DialogUIComponent>();
        uiComponent.InitComponent(this);
        this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
        base.OnAwake();

        dialogSystem = DialogSystem.Instance;

        chatListView = uiComponent.ChatScrollViewLoopListView2;

        chatListView.InitListView(0, OnGetChatItemByIndex);


        UpdateDivinerInfo();
    }
    // 物体显示时执行
    public override void OnShow()
    {
        base.OnShow();
    }
    // 物体隐藏时执行
    public override void OnHide()
    {
        base.OnHide();
    }
    // 物体销毁时执行
    public override void OnDestroy()
    {
        base.OnDestroy();
    }
    #endregion

    #region 初始化

    /// <summary>
    /// 初始化对话框系统
    /// </summary>
    private void InitDialogSystem()
    {
        if (dialogSystem == null)
        {

        }
    }

    /// <summary>
    /// 初始化聊天列表
    /// </summary>
    private void InitChatListView()
    {
        // 如果未在Inspector中赋值，尝试自动查找
        if (chatListView == null)
        {
            chatListView = uiComponent.ChatScrollViewLoopListView2;
            if (chatListView == null)
            {
                Debug.LogError("ChatListView is not found! Please add LoopListView2 component to a child GameObject.");
                return;
            }
        }

        chatListView.InitListView(0, OnGetChatItemByIndex);
    }

    /// <summary>
    /// 更新占卜师信息显示
    /// </summary>
    private void UpdateDivinerInfo()
    {
        if (dialogSystem == null) return;

        if (uiComponent.NameText != null)
        {
            uiComponent.NameText.text = dialogSystem.GetCurrentDivinerName();
        }
    }

    #endregion

    #region 列表项获取

    /// <summary>
    /// LoopListView2 获取项回调
    /// </summary>
    LoopListViewItem2 OnGetChatItemByIndex(LoopListView2 listView, int index)
    {

        if (index < 0 || index >= dialogSystem.GetMessageCount())
        {
            return null;
        }

        ChatMessageData msgData = dialogSystem.GetMessageByIndex(index);
        if (msgData == null)
        {
            return null;
        }

        LoopListViewItem2 item = null;

        if (msgData.roleType == DialogRoleType.AI)
        {
            item = listView.NewListViewItem("ItemPrefab1");
        }
        else
        {
            item = listView.NewListViewItem("ItemPrefab2");
        }
        ChatItem itemScript = item.GetComponent<ChatItem>();
        // 只初始化一次（避免重复执行）
        if (item.IsInitHandlerCalled == false)
        {
            item.IsInitHandlerCalled = true;
            itemScript.Init();
        }

        // 设置数据（刷新UI内容）
        itemScript.SetItemData(msgData, index);

        return item;
    }

    #endregion

    #region 发送和接收消息

    /// <summary>
    /// 发送用户消息
    /// </summary>
    private void SendUserMessage(string content)
    {
        // 添加用户消息到数据层
        dialogSystem.AddUserMessage(content);

        // 更新列表 - 移除 RefreshAllShownItem，让 ScrollView 自动处理
        int msgCount = dialogSystem.GetMessageCount();
        Debug.Log($"发送用户消息后，当前消息数量：{msgCount}");
        chatListView.SetListItemCount(msgCount, false);

        // 滚动到最后一条
        chatListView.MovePanelToItemIndex(
            msgCount - 1,
            0
        );

        // 发送消息到AI
        SendMessageToAI();
    }

    /// <summary>
    /// 发送消息到AI
    /// </summary>
    private void SendMessageToAI()
    {
        if (dialogSystem == null) return;
        Debug.Log("发送消息到AI");
        // 显示加载中提示
        ShowLoadingIndicator();

        // 调用DeepSeek API
        dialogSystem.SendMessageToAI(
            (aiResponse) =>
                {
                    // 隐藏加载中提示
                    HideLoadingIndicator();

                    // 添加AI回复到数据层
                    List<string> options = dialogSystem.GetCurrentDivinerOptions();
                    dialogSystem.AddAIMessage(aiResponse, options);

                    // 更新列表 - 移除 RefreshAllShownItem
                    if (chatListView != null)
                    {
                        int msgCount = dialogSystem.GetMessageCount();
                        Debug.Log($"接收AI消息后，当前消息数量：{msgCount}");
                        chatListView.SetListItemCount(msgCount, false);

                        chatListView.MovePanelToItemIndex(
                         msgCount - 1,
                         0
                         );
                    }
                },
            (error) =>
            {
                // 隐藏加载中提示
                HideLoadingIndicator();

                Debug.LogError("AI响应错误: " + error);
                ToastManager.ShowToast("AI响应失败，请稍后重试。");
            }
        );
    }


    #endregion

    #region 加载指示器

    private bool mIsLoading = false;

    /// <summary>
    /// 显示加载中指示器
    /// </summary>
    private void ShowLoadingIndicator()
    {
        mIsLoading = true;
        // 可以在这里实现加载动画或提示
        Debug.Log("AI思考中...");
    }

    /// <summary>
    /// 隐藏加载中指示器
    /// </summary>
    private void HideLoadingIndicator()
    {
        mIsLoading = false;
    }

    #endregion

    #region API Function

    #endregion

    #region UI组件事件

    /// <summary>
    /// 切换占卜师按钮点击
    /// </summary>
    public void OnswitchDivinerButtonClick()
    {
        Debug.Log("switchDivinerButton is clicked");

        if (dialogSystem == null) return;

        // 切换占卜师类型
        dialogSystem.SwitchDivinerType();

        // 更新UI显示
        UpdateDivinerInfo();

        // 显示切换提示
        string divinerName = dialogSystem.GetCurrentDivinerName();
        ToastManager.ShowToast("已切换为" + divinerName);
    }

    /// <summary>
    /// 问题按钮点击（快捷问题）
    /// </summary>
    public void OnquestionButtonClick()
    {
        Debug.Log("questionButton is clicked");
        // 可以在这里实现快捷问题的功能
    }

    /// <summary>
    /// 发送按钮点击
    /// </summary>
    public void OnsendButtonClick()
    {
        if (uiComponent.questionInputField == null) return;

        string inputText = uiComponent.questionInputField.text;
        if (string.IsNullOrEmpty(inputText))
        {
            ToastManager.ShowToast("请写下你想问的问题。");
            return;
        }

        if (mIsLoading)
        {
            ToastManager.ShowToast("请等待AI回复完成。");
            return;
        }
        Debug.Log($"发送信息：{inputText}");
        // 发送消息
        SendUserMessage(inputText);

        // 清空输入框
        uiComponent.questionInputField.text = "";
    }

    /// <summary>
    /// 输入框内容变化
    /// </summary>
    public void OnquestionInputChange(string text)
    {
    }

    /// <summary>
    /// 输入框结束编辑
    /// </summary>
    public void OnquestionInputEnd(string text)
    {

    }

    #endregion

    #region AI消息回调

    /// <summary>
    /// AI选项按钮点击回调
    /// </summary>
    private void OnAIOptionClick(int optionIndex)
    {
        Debug.Log("AI选项按钮点击: " + optionIndex);

        if (dialogSystem == null) return;

        List<string> options = dialogSystem.GetCurrentDivinerOptions();
        if (options == null || optionIndex < 0 || optionIndex >= options.Count) return;

        string selectedOption = options[optionIndex];
        Debug.Log("用户选择了: " + selectedOption);

        // 根据选项执行不同操作
        switch (selectedOption)
        {
            case "为这个问题选牌阵":
                ToastManager.ShowToast("正在为您选牌阵...");
                // TODO: 实现选牌阵功能
                break;
            case "继续追问":
                // 聚焦输入框，让用户继续输入
                if (uiComponent.questionInputField != null)
                {
                    uiComponent.questionInputField.ActivateInputField();
                }
                break;
            case "明天再看这条线索":
                ToastManager.ShowToast("已保存线索，明天见！");
                // TODO: 实现保存线索功能
                break;
            case "看这段关系的周期":
                ToastManager.ShowToast("正在分析关系周期...");
                // TODO: 实现关系周期分析
                break;
            case "分析今日星象":
                ToastManager.ShowToast("正在分析今日星象...");
                // TODO: 实现今日星象分析
                break;
            case "看下一周趋势":
                ToastManager.ShowToast("正在预测下周趋势...");
                // TODO: 实现下周趋势预测
                break;
            case "保存明日回看":
                ToastManager.ShowToast("已保存，明日可回看！");
                // TODO: 实现保存功能
                break;
            default:
                // 将选项作为问题发送
                SendUserMessage(selectedOption);
                break;
        }
    }

    /// <summary>
    /// 重新生成声音按钮点击回调
    /// </summary>
    private void OnRegenerateVoiceClick()
    {
        Debug.Log("重新生成声音按钮点击");
        ToastManager.ShowToast("正在重新生成语音...");
        // TODO: 实现语音合成功能
    }

    #endregion
}
