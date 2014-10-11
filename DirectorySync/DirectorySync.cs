using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Evlon.Platform.IO
{
    public class DirectorySync
    {
        private string pathRoot;
        public DirectorySync(string pathRoot, bool includeSubdir)
        {
            this.pathRoot = pathRoot;
            DirectoryModifyUpdater updater = new DirectoryModifyUpdater(pathRoot, includeSubdir);
            updater.Start();

            this.SnapLastWriteTime();
        }

        private void SnapLastWriteTime()
        {
            //起一个线程慢慢干吧
            ThreadPool.QueueUserWorkItem(new WaitCallback(e => {
                DirectoryInfo di = new DirectoryInfo(pathRoot);

                this.RunSnap(di);
            }));
        }

        private void RunSnap(DirectoryInfo di)
        {
            
        }
    }
}
