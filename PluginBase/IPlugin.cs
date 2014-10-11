using DSS.Platform.Plugin.Emit;
using DSS.Platform.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PluginBase
{

    public interface IPluginBaseBase
    {
        Guid Guid { get; }

    }

    public interface IPluginBase : IPluginBaseBase
    {
        IMessage Run(string args);

    }
    public interface IPlugin : IPluginBase
    {
        string Run(string args, [CallerDomainRun] Action action);

        string Run(string args, [CallerDomainRun] Func<string> func);

        void ProcessAccept(ref int idleTimeout, out object userContext, ref byte? flagByte, out int needReceiveCount, out byte[] bytesReturn);
    }

    //public interface IPluginSerialize
    //{
    //    byte[] Serialize(MarshalByRefObject obj);

    //    MarshalByRefObject Deserialize(byte[] bytes);
    //}

    [DomainSerializable]
    public interface IMessage
    {
        string Id { get; }
    }
}
