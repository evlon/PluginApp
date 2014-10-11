using DSS.Platform.Plugin.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace DSS.Platform.Plugin.ImplObjects
{
    public class PluginCallerProxy<T> where T : class
    {
        private T _targetPlugin;
        private T _proxyPlugin;
        private PluginLoader<T> _pluginLoader;
        private System.Threading.ReaderWriterLockSlim locker = new ReaderWriterLockSlim();

        /// <summary>
        /// 构造一个代理类，保存在 ProxyPlugin 属性中
        /// </summary>
        /// <param name="loader"></param>
        public PluginCallerProxy(PluginLoader<T> loader)
        {
            this.PluginLoader = loader;
            this.TargetPlugin = loader.Plugin;

            //从函数生成代理类
            this._proxyPlugin = InterfaceProxyBuilder<T>.CreateFuncProxy(() => this.TargetPlugin, typeof(object));
        }

        /// <summary>
        /// 供使用的代理
        /// </summary>
        public T ProxyPlugin
        {
            get { return _proxyPlugin; }
        }

        /// <summary>
        /// 插件物理文件
        /// </summary>
        public string PluginAssemblyPath
        {
            get { return _pluginLoader.TargetAssemblyPath; }
        }

        #region 实现


        internal PluginLoader<T> PluginLoader
        {
            get
            {
                return _pluginLoader;
            }
            set
            {
                _pluginLoader = value;
                this.TargetPlugin = _pluginLoader == null ? null : _pluginLoader.Plugin;
            }
        }

        internal T TargetPlugin
        {
            get
            {
                locker.EnterReadLock();
                try
                {
                    if (_targetPlugin == null)
                    {
                        throw new DssPluginException("插件已经卸载");
                    }
                    return _targetPlugin;
                }
                finally
                {
                    locker.ExitReadLock();
                }

            }
            set
            {
                locker.EnterWriteLock();
                try
                {
                    _targetPlugin = value;
                }
                finally
                {
                    locker.ExitWriteLock();
                }
            }
        } 
        #endregion
    }
}
