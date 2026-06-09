
using System;

namespace XFGameFrameWork
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ClassAPIAttribute : Attribute
    {
        public string DisplayMenuName { get; private set; }
        public string GroupName { get; private set; }
        
        public int RenderOrder { get;private set; }
        
        public string DisplayClassName { get; private set; }

        public ClassAPIAttribute(string groupName, string displayMenuName,int renderOrder,string displayClassName = null)
        {
            GroupName = groupName;
            DisplayMenuName = displayMenuName;
            RenderOrder = renderOrder;
            DisplayClassName = displayClassName;
        }
    }
}