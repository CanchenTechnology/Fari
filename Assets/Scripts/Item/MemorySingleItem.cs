using System.Collections;
using System.Collections.Generic;
using GamerFrameWork.UIFrameWork;
using SuperScrollView;
using UnityEngine;
using UnityEngine.UI;

public class MemorySingleItem : MonoBehaviour, IGamerUIViewListItem, IGamerUIListViewItemContext
{
    public Text contentText;

    public Text timeText;

    public Button infoBtn;

    private MemoryUiItem _data;
    private LoopListView2 _listView;
    private int _index = -1;

    public void InitListItem()
    {
        if (infoBtn != null)
        {
            infoBtn.onClick.RemoveListener(OnInfoButtonClick);
            infoBtn.onClick.AddListener(OnInfoButtonClick);
        }
    }

    public void SetItemListData(int index, params object[] data)
    {
        _index = index;
        _data = data != null && data.Length > 0 ? data[0] as MemoryUiItem : null;
        Refresh();
    }

    public void SetListViewContext(LoopListView2 listView, int index)
    {
        _listView = listView;
        _index = index;
    }

    public void OnRelease()
    {
        if (infoBtn != null)
            infoBtn.onClick.RemoveListener(OnInfoButtonClick);
        _data = null;
        _listView = null;
        _index = -1;
    }

    private void Refresh()
    {
        if (_data == null)
        {
            if (contentText != null) contentText.text = "";
            if (timeText != null) timeText.text = "";
            return;
        }

        if (contentText != null)
        {
            string prefix = _data.Important ? "★ " : "";
            string suffix = _data.PendingConfirm ? "（待确认）" : _data.Enabled ? "" : "（已关闭）";
            contentText.text = $"{prefix}{_data.Text}{suffix}";
            contentText.color = _data.Enabled || _data.PendingConfirm
                ? new Color(0.12f, 0.12f, 0.12f, 1f)
                : new Color(0.46f, 0.42f, 0.50f, 1f);
        }

        if (timeText != null)
        {
            timeText.text = string.IsNullOrEmpty(_data.DateText) ? "长期" : _data.DateText;
            timeText.color = _data.Enabled || _data.PendingConfirm
                ? new Color(0.16f, 0.16f, 0.18f, 1f)
                : new Color(0.50f, 0.46f, 0.54f, 1f);
        }
    }

    private void OnInfoButtonClick()
    {
        if (_data == null || string.IsNullOrEmpty(_data.Id))
        {
            ToastManager.ShowToast("暂无记忆内容");
            return;
        }

        MemoryDetailUI detail = UIModule.Instance.PopUpWindow<MemoryDetailUI>();
        if (detail != null)
            detail.SetMemoryItem(_data.Id);
    }
}
