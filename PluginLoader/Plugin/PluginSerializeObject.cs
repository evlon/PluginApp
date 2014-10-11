using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace DSS.Platform.Plugin
{
    //public interface IPluginSerializeObject
    //{
    //}

    [Serializable]
    public class SerializableMarshalByRefObject<T> : MarshalByRefObject, ISerializable where T : class
    {
        protected T target;
        protected RemotePluginLoader<T> loader;

        public RemotePluginLoader<T> Loader { get { return loader; } }

        public SerializableMarshalByRefObject(T target)
        {
            this.target = target;
        }

        public SerializableMarshalByRefObject(T target, RemotePluginLoader<T> loader)
        {
            this.target = target;
            this.loader= loader;
        }

        protected SerializableMarshalByRefObject(SerializationInfo info, StreamingContext context)
        {
            string typeName = info.GetString("targetType");
            target = (T)info.GetValue("target", Type.GetType(typeName));
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            string typeName = target.GetType().AssemblyQualifiedName;
            info.AddValue("targetType", typeName);
            info.AddValue("target", target, target.GetType());
        }
    }

    [Serializable]
    public class SerializableObject<T> : ISerializable where T :class
    {
        protected T target;

        protected RemotePluginLoader<T> loader;

        public RemotePluginLoader<T> Loader { get { return loader; } }

        public SerializableObject(T target)
        {
            this.target = target;
        }

        public SerializableObject(T target, RemotePluginLoader<T> loader)
        {
            this.target = target;
            this.loader = loader;
        }

        protected SerializableObject(SerializationInfo info, StreamingContext context)
        {
            byte[] bytes = (byte[])info.GetValue("bytes", typeof(byte[]));
            target =  Loader.Deserialize(bytes) as T;
           
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            
            var bytes = Loader.Serialize((MarshalByRefObject)(object)target);
            info.AddValue("bytes", bytes, typeof(byte[]));
        }
    }

    public class LoaderHost<T>  where T : class
    {
        protected T target;

        protected RemotePluginLoader<T> loader;

        public RemotePluginLoader<T> Loader { get { return loader; }}

        public LoaderHost(T target)
        {
            this.target = target;
        }

        public LoaderHost(T target, RemotePluginLoader<T> loader)
        {
            this.target = target;
            this.loader = loader;
        }
    }

    //[Serializable]
    //public class SerializableMarshalByRefObjectProxy<T> : ISerializable where T : class
    //{
    //    RemotePluginLoader<T> loader;
    //    protected T target;

    //    public SerializableMarshalByRefObjectProxy(RemotePluginLoader<T> loader, T target)
    //    {
    //        this.loader = loader;
    //        this.target = target;
    //    }

    //    protected SerializableMarshalByRefObjectProxy(SerializationInfo info, StreamingContext context)
    //    {
            
    //    }

    //    public void GetObjectData(SerializationInfo info, StreamingContext context)
    //    {
            
    //    }
    //}

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Delegate | AttributeTargets.Interface, Inherited = true)]
    public sealed class DomainSerializableAttribute : Attribute
    {
    }


}
