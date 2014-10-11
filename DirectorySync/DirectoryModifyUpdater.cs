using Evlon.Platform.IO.ImplObjects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Evlon.Platform.IO
{
    /// <summary>
    /// 监视目录以及子目录，把修改向上层父目录传递
    /// 
    /// 如果添加、修改、删除了新文件，则父目录的修改时间变更
    /// </summary>
    public class DirectoryModifyUpdater
    {
        private bool includeSubdir;
        private string path;
        private FileSystemWatcher fileMonitor;
        private ConcurrentDictionary<string, LazyAction> changedFiles = new ConcurrentDictionary<string, LazyAction>();
        public DirectoryModifyUpdater(string path, bool includeSubdir)
        {
            this.path = path;
            this.includeSubdir = includeSubdir;
        }

        public DirectoryModifyUpdater Start()
        {
            this.fileMonitor = new FileSystemWatcher(path, "*.*");
            this.fileMonitor.IncludeSubdirectories = includeSubdir;

            this.fileMonitor.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            this.fileMonitor.Changed += OnPluginChanged;
            this.fileMonitor.Created += OnPluginChanged;
            this.fileMonitor.Deleted += OnPluginChanged;
            this.fileMonitor.Renamed += OnPluginRenamed;


            this.fileMonitor.EnableRaisingEvents = true;
            return this;
            
        }

        public DirectoryModifyUpdater Stop()
        {
            this.fileMonitor.EnableRaisingEvents = false;
            return this;
        }

        private void CreateOrReplaceUpdateTask(DirectoryInfo dir)
        {
            this.changedFiles.GetOrAdd(dir.FullName, path => new LazyAction(() => dir.LastWriteTime = DateTime.Now)).LazyRun(1000);
        }

        void OnPluginRenamed(object sender, RenamedEventArgs e)
        {
            //重命名，理解为去掉原来的，增加新命名的
            FileInfo old = new FileInfo(e.OldFullPath);
            CreateOrReplaceUpdateTask(old.Directory);

            FileInfo n = new FileInfo(e.FullPath);
            CreateOrReplaceUpdateTask(n.Directory);

        }

        void OnPluginChanged(object sender, FileSystemEventArgs e)
        {
            Debug.Print(e.Name + e.ChangeType);
            FileInfo n = new FileInfo(e.FullPath);
            CreateOrReplaceUpdateTask(n.Directory);

        }
    }
}
