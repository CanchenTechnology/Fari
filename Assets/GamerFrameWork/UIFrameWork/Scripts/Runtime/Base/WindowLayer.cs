namespace GamerFrameWork.UIFrameWork
{
    public enum WindowLayer
    {
        Background = 0, //背景图层:背景图片、地图底图
        MainUI = 100,   //主界面层
        Popup = 200,    //弹窗层:设置面板、角色面板
        Top = 300,      //顶部层:Loading、网络提示、系统消息
        Guide = 400,    //引导层:新手引导，操作指示
        Debug = 500,    //调试层:调试按钮、性能显示等
    }
}
