

//**********************************************
//创建人：玖一
//功能说明：
//**********************************************
namespace XFGameFrameWork.MVC
{
    public interface IModel
    {
        void Init();
    }
    public interface IView
    {
        void Init();
        void Refresh();
    }
    public interface IController
    {
        void Init();
        void Execute(string eventName, object data = null);
    }


}



