using PluginBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PluginApp
{
    [Serializable]
    public class MessageObject : MarshalByRefObject, IMessage
    {
        public string Id { get; set; }

        public override string ToString()
        {
            return Id;
        }
    }
}
