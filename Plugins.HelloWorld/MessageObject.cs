using DSS.Platform.Plugin;
using PluginBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Plugins.HelloWorld
{
    [Serializable]
    public class MessageObject : IMessage
    {
        public string Id { get; set; }
        public override string ToString()
        {
            return Id;
        }

        public MessageObject()
        { }

        //protected MessageObject(SerializationInfo info, StreamingContext context)
        //{
        //    this.Id = info.GetString("id");
           
        //}

        //public void GetObjectData(SerializationInfo info, StreamingContext context)
        //{
        //    info.AddValue("id", this.Id);
        //}
    }

    [Serializable]
    public class SerializableMessageObjectProxy : MarshalByRefObject, ISerializable, IMessage
    {
        //public SerializableMessageObjectProxy(IMessage target)
        //    //: base(target)
        //{
        //}


        //protected SerializableMessageObjectProxy(SerializationInfo info, StreamingContext context)
        //    :base(info, context)
        //{

        //}



        public string Id
        {
            get { return target.Id; }
        }

        protected IMessage target;

        public SerializableMessageObjectProxy(IMessage target)
        {
            this.target = target;
        }
        public SerializableMessageObjectProxy(IMessage target, RemotePluginLoader<IPlugin> loader)
        {
            this.target = target;
        }

        protected SerializableMessageObjectProxy(SerializationInfo info, StreamingContext context)
        {
            //string typeName = info.GetString("targetType");
            //target = (IMessage)info.GetValue("target", Type.GetType(typeName));
            var s = info.GetString("key");
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("key", "123");
            //string typeName = target.GetType().AssemblyQualifiedName;
            //info.AddValue("targetType", typeName);
            //info.AddValue("target", target, target.GetType());
        }
    }

    //[Serializable]
    //public class SerializableMarshalByRefObject<T> : MarshalByRefObject, ISerializable where T : class
    //{
    //    protected T target;

    //    public SerializableMarshalByRefObject(T target)
    //    {
    //        this.target = target;
    //    }

    //    protected SerializableMarshalByRefObject(SerializationInfo info, StreamingContext context)
    //    {
    //        string typeName = info.GetString("targetType");
    //        target = (T)info.GetValue("target", Type.GetType(typeName));
    //    }

    //    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
    //    {
    //        string typeName = target.GetType().AssemblyQualifiedName;
    //        info.AddValue("targetType", typeName);
    //        info.AddValue("target", target, target.GetType());
    //    }
    //}

}
