using DSS.Platform.CrossDomain;
using DSS.Platform.Plugin.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace DSS.Platform.Plugin
{
    public class PluginLoader<T> : TypeLoader where T : class
    {
        public T Plugin { get; private set; }
        public PluginLoader(string targetAssembly, string pluginPath)
            :base(targetAssembly, pluginPath)
        {
            var pluginImpl = ((RemotePluginLoader<T>)remoteTypeLoader).Plugin;
            //if (false) // 没有发现需要序列化的对象
            //{
                //枚举每个函数， 检查返回值类型定义， 如果标记 DomainSerializableAttribute ，
                ///生成代理类型，里面含有序列化反序列化相对应的信息
                Type targetType = typeof(T);



                T proxyProxy = InterfaceProxyBuilder<T>.CreateProxy(pluginImpl, typeof(LoaderHost<>).MakeGenericType(targetType), true, typeof(SerializableObject<>),
                    proxyType =>
                    {
                        return proxyType.GetConstructor(new Type[] { targetType, typeof(RemotePluginLoader<T>) }).Invoke(new object[] { pluginImpl, remoteTypeLoader }) as T;
                    });
                
                this.Plugin = proxyProxy;

            //}
            //else
            //{
            //    this.Plugin = pluginImpl;
            //}
        }

        protected override RemoteTypeLoader CreateRemoteTypeLoader()
        {
            return CreateRemoteTypeLoader(typeof(RemotePluginLoader<T>));
        }

        //public Guid PluginId
        //{
        //    get { return RemotePlugin.PluginId; }
        //}

        //public string Run(string args)
        //{
        //    return this.RemotePlugin.Run(args);
        //}


        //public string Run(string args, Action action)
        //{
        //    //本地域执行
        //    return this.RemotePlugin.Run(args, action.CreateRemoteAppDomainProxy());
        //}


        //public string Run(string args, RemoteAction action)
        //{
        //    //远程域执行，直接传递
        //    return this.RemotePlugin.Run(args, ()=>action());
        //}


        //public string Run(string args, Func<string> func)
        //{
        //    //本地域执行
        //    return this.RemotePlugin.Run(args, func.CreateRemoteAppDomainProxy());
        //}

        //public string Run(string args, RemoteFunc<string> func)
        //{
        //    //远程域执行，直接传递
        //    return this.RemotePlugin.Run(args, ()=>func());
        //}
    }

    
}
