using DSS.Platform.Plugin.ImplObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSS.Platform.Plugin
{
    public static class RemoteDomainHelper
    {
        public static Action CreateRemoteAppDomainProxy(this Action action)
        {
            return RemoteActionProxy.CreateProxyAction(action);
        }
        public static Func<T> CreateRemoteAppDomainProxy<T>(this Func<T> func)
        {
            return RemoteFuncProxy<T>.CreateProxyFunc(func);
        }

    }
}
