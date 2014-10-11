using PluginBase;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using PluginBase;
using DependLib;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using DSS.Platform.Plugin;

namespace Plugins.HelloWorld
{
    public class PluinHelloWorld : Base, IPlugin
    {
        public static Guid guid = Guid.Parse("{79BB6BE6-9A4A-4DCD-A622-5A59F66112EB}");
        public Guid Guid
        {
            get { return guid; }
        }

        public IMessage Run(string args)
        {
            //Debug.Print("Run({0})", args);
            return new SerializableMessageObjectProxy(new MessageObject() { Id = Guid.NewGuid().ToString("N") });
            //return new MessageObject() { Id = Guid.NewGuid().ToString("N") };
        }

        private string RunInternal(string args)
        {
            //Debug.Print("Run({0})", args);
            return  string.Concat(AppDomain.CurrentDomain.FriendlyName, "(", args, ")-- v3");
        }


        public string Run(string args, Action action)
        {
            action();

            return RunInternal(args);
        }




        public string Run(string args, Func<string> func)
        {
            var ret = func();

            return RunInternal(string.Concat(args, "=>", ret));
        }



        public void ProcessAccept(ref int idleTimeout, out object userContext, ref byte? flagByte, out int needReceiveCount, out byte[] bytesReturn)
        {
            userContext = new object();
            needReceiveCount = 9;
            bytesReturn = null;

        }

        //public byte[] Serialize(MarshalByRefObject obj)
        //{
        //    BinaryFormatter bf = new BinaryFormatter();
        //    using (MemoryStream ms = new MemoryStream())
        //    {
        //        ///TODO:不能序列化 MarshalByRefObject
        //        bf.Serialize(ms, obj);

        //        ms.Position = 0;

        //        return ms.ToArray();
        //    }
        //}

        //public MarshalByRefObject Deserialize(byte[] bytes)
        //{
        //    BinaryFormatter bf = new BinaryFormatter();
        //    using (MemoryStream ms = new MemoryStream(bytes))
        //    {
        //        var obj2 = bf.Deserialize(ms) as MarshalByRefObject;
        //        return obj2;
        //    }
        //}
    }

    public class Proxy : LoaderHost<IPlugin>
    {
        public Proxy(IPlugin target)
            :base(target)
        {
            this.target = target;
        }

        public Proxy(IPlugin target, RemotePluginLoader<IPlugin> loader)
            :base(target, loader)
        {
        }



        public Guid Guid
        {
            get { return target.Guid; }
        }

        public IMessage Run(string args)
        {
            return new SerializableMessageObjectProxy(target.Run(args),base.loader);
        }

        public string Run(string args, Action action)
        {
            return target.Run(args, action);
        }

        public string Run(string args, Func<string> func)
        {
            return target.Run(args, func);
        }
        public void ProcessAccept(ref int idleTimeout, out object userContext, ref byte? flagByte, out int needReceiveCount, out byte[] bytesReturn)
        {
            target.ProcessAccept(ref idleTimeout, out userContext, ref flagByte, out needReceiveCount, out bytesReturn);

        }

        //public byte[] Serialize(MarshalByRefObject obj)
        //{
        //    return target.Serialize(obj);
        //}

        //public MarshalByRefObject Deserialize(byte[] bytes)
        //{
        //    return target.Deserialize(bytes);
        //}
    }

    public class FuncProxy : IPlugin
    {
        private Func<IPlugin> target;

        public Guid Guid
        {
            get { return target().Guid; }
        }

        public IMessage Run(string args)
        {
            return target().Run(args);
        }

        public string Run(string args, Action action)
        {
            return target().Run(args, DSS.Platform.Plugin.ImplObjects.RemoteActionProxy.CreateProxyAction(action));
        }

        public string Run(string args, Func<string> func)
        {
            return target().Run(args, DSS.Platform.Plugin.ImplObjects.RemoteFuncProxy<string>.CreateProxyFunc(func));
        }
        public void ProcessAccept(ref int idleTimeout, out object userContext, ref byte? flagByte, out int needReceiveCount, out byte[] bytesReturn)
        {
            target().ProcessAccept(ref idleTimeout, out userContext, ref flagByte, out needReceiveCount, out bytesReturn);


        }

        //public byte[] Serialize(MarshalByRefObject obj)
        //{
        //    return target().Serialize(obj);
        //}

        //public MarshalByRefObject Deserialize(byte[] bytes)
        //{
        //    return target().Deserialize(bytes);
        //}
    }

}
