using UniFramework.Event;

public class SceneEventDefine
{
    public class ChangeToAppScene : IEventMessage
    {
        public static void SendEventMessage()
        {
            var msg = new ChangeToAppScene();
            UniEvent.SendMessage(msg);
        }
    }

}