using System;

namespace XFGameFrameWork
{
    public class APIExampleCodeAttribute : Attribute
    {
        public string Code { get; private set; }

        public APIExampleCodeAttribute(string code)
        {
            Code = code;
        }
    }
}