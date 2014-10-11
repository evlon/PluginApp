using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DSS.Platform.Plugin.ImplObjects
{
    /// <summary>
    /// 方法域间代理类
    /// </summary>
    public class RemoteActionProxy : MarshalByRefObject
    {
        private Action action;
        private RemoteActionProxy()
        {
        }

        private RemoteActionProxy(Action action)
        {
            this.action = action;

        }

        public void Execute()
        {
            this.action();
        }

        public static Action CreateProxyAction(Action action)
        {
            var proxy = new RemoteActionProxy(action);
            return proxy.Execute;
        }
    }
}
