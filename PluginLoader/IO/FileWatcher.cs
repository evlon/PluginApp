using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace DSS.Platform.IO
{
    public class FileWatcher
    {
         #region 实现
        #region 字段
        private string filePath;
        private FileSystemWatcher pluginWatcher;
        private Timer timerProcess = null;
        private ConcurrentDictionary<string, FileSystemEventArgs> changedFiles = new ConcurrentDictionary<string, FileSystemEventArgs>();


        #endregion

     
        /// <summary>
        /// 创建一个插件管理器
        /// </summary>
        public FileWatcher()
        {
        }

        /// <summary>
        /// 开始监控插件目录
        /// </summary>
        /// <param name="filePath">本地插件目录</param>
        /// <returns></returns>
        public FileWatcher StartWatcher(string filePath = "plugins", string filter = "*.dll")
        {

            this.filePath = filePath;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);

            //监控
            this.pluginWatcher = new FileSystemWatcher(path, filter);
            this.pluginWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            this.pluginWatcher.Changed += OnPluginChanged;
            this.pluginWatcher.Created += OnPluginChanged;
            this.pluginWatcher.Deleted += OnPluginChanged;
            this.pluginWatcher.Renamed += OnPluginRenamed;

            pluginWatcher.EnableRaisingEvents = true;

            timerProcess = new Timer(new TimerCallback(e => this.ProcessChangedFiles()));


            //加载所有
            Directory.GetFiles(path, filter).ToList().ForEach(file =>
            {
                FileInfo fi = new FileInfo(file);
                this.changedFiles[fi.Name] = new FileSystemEventArgs(WatcherChangeTypes.Created, fi.DirectoryName, fi.Name);

            });
            this.timerProcess.Change(10, -1);
            return this;
        }

        public FileWatcher StopWatcher()
        {
            this.timerProcess.Change(-1, -1);
            pluginWatcher.Dispose();
            pluginWatcher = null;
            this.changedFiles.Clear();
            return this;
        }

        void OnPluginRenamed(object sender, RenamedEventArgs e)
        {
            //重命名，理解为去掉原来的，增加新命名的
            FileInfo old = new FileInfo(e.OldFullPath);
            this.changedFiles[old.Name] = new FileSystemEventArgs(WatcherChangeTypes.Deleted, old.DirectoryName, old.Name);

            FileInfo n = new FileInfo(e.FullPath);
            this.changedFiles[n.Name] = new FileSystemEventArgs(WatcherChangeTypes.Created, n.DirectoryName, n.Name);

            //1秒后再处理
            this.timerProcess.Change(1000, -1);

        }

        void OnPluginChanged(object sender, FileSystemEventArgs e)
        {
            Debug.Print(e.Name + e.ChangeType);

            //记录变更
            this.changedFiles[e.Name] = e;

            //1秒后再处理
            this.timerProcess.Change(1000, -1);

        }

        protected void ProcessChangedFiles()
        {
            foreach (var kv in this.changedFiles)
            {
                FileSystemEventArgs val;
                if(this.changedFiles.TryRemove(kv.Key, out val))
                    this.ProcessChangedFile(val);

            }
        }

        protected virtual void ProcessChangedFile(FileSystemEventArgs e)
        {
            Debug.Print(e.Name + "=>" + e.ChangeType);
        }

       
        #endregion
    }
}
