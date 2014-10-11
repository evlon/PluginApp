using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSS.Platform.Plugin.ImplObjects
{
    /// <summary>
    /// 方法域间代理类
    /// </summary>
    public class RemoteFuncProxy<T> : MarshalByRefObject
    {
        private Func<T> func;
        private RemoteFuncProxy()
        {
        }
        private RemoteFuncProxy(Func<T> func)
        {
            this.func = func;

        }

        public T Execute()
        {
            return this.func();
        }

        public static Func<T> CreateProxyFunc(Func<T> func)
        {
            var proxy = new RemoteFuncProxy<T>(func);
            return proxy.Execute;
        }
    }
}
