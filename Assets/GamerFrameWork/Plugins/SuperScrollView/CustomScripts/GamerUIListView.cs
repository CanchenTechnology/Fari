using SuperScrollView;
using UnityEngine;
namespace GamerFrameWork.UIFrameWork
{
    /// <summary>
    /// 按某一行或某一列排序
    /// </summary>
    public class GamerUIListView : MonoBehaviour
    {
        public LoopListView2 loopListView;

        private int m_ViewDataCount = 99; //数据列表长度,建议在当前接口触发时向数据层索取

        private GetItemDataDelegate m_GetItemDataCallBack = null;

        private void Awake()
        {
            if (loopListView == null)
            {
                loopListView = GetComponent<LoopListView2>();
            }
        }

        /// <summary>
        /// 刷新列表显示
        /// </summary>
        public void RefreshListView(bool resetPos, int viewDataCount, GetItemDataDelegate getItemDataCallBack)
        {
            m_ViewDataCount = viewDataCount;
            m_GetItemDataCallBack = getItemDataCallBack;
            if (!loopListView.ListViewInited)
            {
                //初始化滚动列表 切记不可在Awake或Start中初始化Count=0的列表。SupperView会有BUG
                loopListView.InitListView(m_ViewDataCount,OnShowItemByIndex);
            }
            else
            {
                //数据发生变化,重新设置最新的数据,数据增删必须调用此接口,否则会出现item索引与数据不一致和一切其他显示的Bug
                loopListView.SetListItemCount(m_ViewDataCount,false);
                //为什么不直接SetListItemCount的参数设置为True呢？
                if (resetPos)
                {
                    loopListView.MovePanelToItemIndex(0, 0);
                }
                loopListView.RefreshAllShownItem();
            }
        }
        /// <summary>
        /// Item元素显示回调
        /// </summary>
        /// <param name="listView">滚动列表</param>
        /// <param name="index">item索引</param>
        /// <returns></returns>
        private LoopListViewItem2 OnShowItemByIndex(LoopListView2 listView,int index)
        { 
            if(index<0||index>=m_ViewDataCount) return null;
            //获取item显示的数据
            object itemData = m_GetItemDataCallBack(index);
            if (itemData==null) return null;

            if (loopListView.ItemPrefabDataList.Count == 0)
            {
                Debug.LogError("ItemPrefabDataList is null!");
                return null;
            }
            //创建对应item预制体
            LoopListViewItem2 item = listView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            if (item == null)
            {
                Debug.LogError($"item is null");
            }
            //获取item上的脚本组件
            IGamerUIViewListItem itemScript = item.GetComponent<IGamerUIViewListItem>();

            if (!item.IsInitHandlerCalled)
            { 
                item.IsInitHandlerCalled = true;
                //item脚本初始化接口
                itemScript.InitListItem();
            }
            //设置item脚本数据
            itemScript.SetItemListData(index, itemData);
            return item;
        }

        public void OnRelease()
        {
            var itemScriptArr = loopListView.ContainerTrans.GetComponentsInChildren<IGamerUIViewListItem>(true);
            foreach (var item in itemScriptArr)
            {
                item.OnRelease();
            }
        }
    }
}

