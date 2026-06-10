using SuperScrollView;

namespace GamerFrameWork.UIFrameWork
{
    /// <summary>
    /// 需要 ListView 上下文的 Item 实现此接口（如展开/收起动画需要通知列表刷新布局）
    /// </summary>
    public interface IGamerUIListViewItemContext
    {
        /// <summary>
        /// 设置 ListView 上下文
        /// </summary>
        /// <param name="listView">LoopListView2 引用</param>
        /// <param name="index">当前 Item 在列表中的索引</param>
        void SetListViewContext(LoopListView2 listView, int index);
    }
}
