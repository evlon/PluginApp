using DSS.Platform.IO;
using DSS.Platform.Plugin.ImplObjects;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace DSS.Platform.Plugin
{
    /// <summary>
    /// 监视插件目录
    /// 输出插件DLL的变更，修改，删除，增加
    /// </summary>
    public class PluginManager<T> where T : class
    {
        class PluginFileWatcher : FileWatcher
        {
            private static ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            private PluginManager<T> manager;
            public PluginFileWatcher(PluginManager<T> manager)
            {
                this.manager = manager;
            }

            protected override void ProcessChangedFile(FileSystemEventArgs e)
            {
                Debug.Print(e.Name + "=>" + e.ChangeType);
                switch (e.ChangeType)
                {
                    case WatcherChangeTypes.Created:
                        {
                            //加载
                            try
                            {
                                var loader = new PluginLoader<T>(e.Name, manager.pluginPath);
                                var proxy = new PluginCallerProxy<T>(loader);
                                manager.plugins.TryAdd(e.Name, proxy);

                                manager.OnPluginChange(new PluginManagerEventArgs<T>(e.Name, PluginChangeType.Created, proxy.ProxyPlugin));
                            }
                            catch (DssPluginException ex)
                            {
                                log.ErrorFormat("加载插件发生异常:{0}", ex.Message);
                            }
                        }
                        break;
                    case WatcherChangeTypes.Deleted:
                        {
                            PluginCallerProxy<T> proxy;
                            if (manager.plugins.TryRemove(e.Name, out proxy))
                            {
                                manager.OnPluginChange(new PluginManagerEventArgs<T>(e.Name, PluginChangeType.Deleted, proxy.ProxyPlugin));

                                var loader = proxy.PluginLoader;
                                proxy.PluginLoader = null;
                                loader.Unload();
                            }
                        }
                        break;
                    case WatcherChangeTypes.Changed:
                        {
                            PluginCallerProxy<T> proxy;
                            if (manager.plugins.TryGetValue(e.Name, out proxy))
                            {
                                manager.OnPluginChange(new PluginManagerEventArgs<T>(e.Name, PluginChangeType.Deleted, proxy.ProxyPlugin));

                                var loader = proxy.PluginLoader;
                                loader.Unload();

                                try
                                {
                                    loader = new PluginLoader<T>(e.Name, manager.pluginPath);
                                    proxy.PluginLoader = loader;

                                    manager.OnPluginChange(new PluginManagerEventArgs<T>(e.Name, PluginChangeType.Created, proxy.ProxyPlugin));
                                }
                                catch (DssPluginException ex)
                                {
                                    log.ErrorFormat("加载插件发生异常:{0}", ex.Message);
                                }
                            }
                        }
                        break;
                }

            }
        }

        private PluginFileWatcher watcher;
        #region 实现
        #region 字段
        private string pluginPath;
        private ConcurrentDictionary<string, PluginCallerProxy<T>> plugins = new ConcurrentDictionary<string, PluginCallerProxy<T>>();


        #endregion


        /// <summary>
        /// 创建一个插件管理器
        /// </summary>
        public PluginManager()
        {
            watcher = new PluginFileWatcher(this);
           
        }

        /// <summary>
        /// 开始监控插件目录
        /// </summary>
        /// <param name="pluginPath">本地插件目录</param>
        /// <returns></returns>
        public PluginManager<T> Start(string pluginPath = "plugins")
        {
            this.pluginPath = pluginPath;
            watcher.StartWatcher(pluginPath, "*.dll");
            return this;
        }

        public void Stop()
        {
            watcher.StopWatcher();
        }

        //protected override void ProcessChangedFile(FileSystemEventArgs e)
        //{
        //    Debug.Print(e.Name + "=>" + e.ChangeType);
        //    switch (e.ChangeType)
        //    {
        //        case WatcherChangeTypes.Created:
        //            {
        //                //加载
        //                var loader = new PluginLoader<T>(e.Name, pluginPath);
        //                var proxy = new PluginCallerProxy<T>(loader);
        //                plugins.TryAdd(e.Name, proxy);

        //                OnPluginChange(new PluginManagerEventArgs<T>(e.Name, PluginChangeType.Created, proxy.ProxyPlugin));
        //            }
        //            break;
        //        case WatcherChangeTypes.Deleted:
        //            {
        //                PluginCallerProxy<T> proxy;
        //                if (plugins.TryRemove(e.Name, out proxy))
        //                {
        //                    OnPluginChange(new PluginManagerEventArgs<T>(e.Name, PluginChangeType.Deleted, proxy.ProxyPlugin));

        //                    var loader = proxy.PluginLoader;
        //                    proxy.PluginLoader = null;
        //                    loader.Unload();
        //                }
        //            }
        //            break;
        //        case WatcherChangeTypes.Changed:
        //            {
        //                PluginCallerProxy<T> proxy;
        //                if (plugins.TryGetValue(e.Name, out proxy))
        //                {
        //                    OnPluginChange(new PluginManagerEventArgs<T>(e.Name, PluginChangeType.Deleted, proxy.ProxyPlugin));

        //                    var loader = proxy.PluginLoader;
        //                    loader.Unload();


        //                    loader = new PluginLoader<T>(e.Name, pluginPath);
        //                    proxy.PluginLoader = loader;

        //                    OnPluginChange(new PluginManagerEventArgs<T>(e.Name, PluginChangeType.Created, proxy.ProxyPlugin));
        //                }
        //            }
        //            break;
        //    }

        //}
       
        protected virtual void OnPluginChange(PluginManagerEventArgs<T> e)
        {
            if (PluginChanged != null)
            {
                PluginChanged(this, e);
            }
        }
        #endregion

        #region 公共


        public event PluginChangeHandle<T> PluginChanged;
        #endregion

        /// <summary>
        /// 获取加载的所有插件
        /// </summary>
        /// <returns></returns>
        public PluginCallerProxy<T>[] GetPlugins()
        {
            return plugins.Values.ToArray();
        }

        /// <summary>
        /// 把二进制内容写到文件中，并加载插件
        /// </summary>
        /// <param name="pluginContent"></param>
        /// <param name="namePrefix">fileName = string.Concat(namePrefix , DateTime.Now.ToString("yyyyMMddHHmmss.fff"), ".dll")</param>
        public void AddPlugin(byte[] pluginContent, string namePrefix)
        {
            string fileName = Path.Combine(this.pluginPath, string.Concat(namePrefix , DateTime.Now.ToString("yyyyMMddHHmmss.fff"), ".dll"));
            File.WriteAllBytes(fileName,pluginContent);
        }

        /// <summary>
        /// 删除插件
        /// </summary>
        /// <param name="namePrefix"></param>
        public void RemovePlugin(string namePrefix)
        {
            var files = Directory.GetFiles(string.Concat(namePrefix,"*"));
            Array.ForEach(files, f => File.Delete(f));
        }




        
    }

    public delegate void PluginChangeHandle<T>(object sender, PluginManagerEventArgs<T> e) where T : class;


    public class PluginManagerEventArgs<T> : EventArgs where T : class
    {
        public string FileName { get; private set; }
        public PluginChangeType ChangeType { get; private set; }
        public T PluginInstance { get; private set; }

        public PluginManagerEventArgs(string fileName, PluginChangeType changeType, T pluginInstance)
        {
            this.FileName = fileName;
            this.ChangeType = changeType;
            this.PluginInstance = pluginInstance;
        }
    }


    public enum PluginChangeType
    {
        Created,
        Deleted
    }


}
