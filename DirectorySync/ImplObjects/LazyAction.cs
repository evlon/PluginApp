using System;
using System.Threading;


namespace Evlon.Platform.IO.ImplObjects
{
    public class LazyAction
    {
        private Timer timeLazy = null;
        public Action Action { get; private set; }

        public LazyAction(Action action)
        {
            this.Action = action;
            this.timeLazy = new Timer(new TimerCallback(e =>
            {
                this.Action();

            }));
        }

        /// <summary>
        /// 设置延迟调用作业
        /// </summary>
        /// <param name="ms"></param>
        public LazyAction LazyRun(int ms)
        {
            this.timeLazy.Change(ms, -1);
            return this;
        }
    }
}
