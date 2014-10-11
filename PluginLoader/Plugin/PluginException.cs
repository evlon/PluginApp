using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace DSS.Platform.Plugin
{
    [Serializable]
    public class DssPluginException : ApplicationException
    {
        protected DssPluginException()
            : base()
        { }

        //
        // 摘要:
        //     Initializes a new instance of the System.ApplicationException class with
        //     serialized data.
        //
        // 参数:
        //   info:
        //     保存序列化对象数据的对象。
        //
        //   context:
        //     有关源或目标的上下文信息。
        protected DssPluginException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }


        public DssPluginException(string message)
            :base(message)
        {
        }
    }
}
