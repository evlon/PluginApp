using DSS.Platform.CrossDomain;
using DSS.Platform.Plugin.Emit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace DSS.Platform.Plugin
{
    public class RemotePluginLoader<T> : RemoteTypeLoader where T :class
    {
        protected Type pluginType;
        public T Plugin { get; private set; }

        public RemotePluginLoader(string pluginPath)
            : base(pluginPath)
        {
        }
        
        protected override System.Reflection.Assembly LoadAssembly(string assemblyPath)
        {
            var ass =  base.LoadAssembly(assemblyPath);

            //查找插件
            Type typePlugin = typeof(T);
            this.pluginType = ass.GetTypes().Where(t => typePlugin.IsAssignableFrom(t)).FirstOrDefault();

            if (this.pluginType == null)
                throw new DssPluginException("找不到实现" + typePlugin.FullName + "的类型");

            //生成代理插件类，并从MarshalByRefObject继承以支持AppDomain通信
            this.Plugin = InterfaceProxyBuilder<T>.CreateProxy((T)System.Activator.CreateInstance(pluginType),
                typeof(MarshalByRefObject), true, typeof(SerializableMarshalByRefObject<>));
            return ass;
        }

        /// <summary>
        /// 直接在远程Domain中，执行方法。 要求支持序列化
        /// </summary>
        /// <param name="action">执行的匿名方法</param>
        public void Execute(Action action)
        {
            action();
        }

        /// <summary>
        /// 直接在远程Domain中，执行函数。 要求支持序列化
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fun"></param>
        /// <returns></returns>
        public TRet Execute<TRet>(Func<TRet> fun)
        {
            return fun();
        }

        /// <summary>
        /// 序列化
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public byte[] Serialize(MarshalByRefObject obj)
        {
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);

                ms.Position = 0;

                return ms.ToArray();
            }
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public MarshalByRefObject Deserialize(byte[] bytes)
        {
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                var obj2 = bf.Deserialize(ms) as MarshalByRefObject;
                //在返回之前， 要加上代理类


                return obj2;
            }
        }

    }
}
