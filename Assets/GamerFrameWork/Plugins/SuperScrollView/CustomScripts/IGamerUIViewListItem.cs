using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace GamerFrameWork.UIFrameWork
{
    public interface IGamerUIViewListItem 
    {
        /// <summary>
        /// 初始化列表item
        /// </summary>
        public void InitListItem();

    /// <summary>
    ///  设置Item显示数据
    /// </summary>
    /// <param name="index">数据索引</param>
    /// <param name="data">Item数据</param>
        public void SetItemListData(int index, params object[] data);

        /// <summary>
        /// 资源释放接口
        /// </summary>
        public void OnRelease();
        
        
    }
}

