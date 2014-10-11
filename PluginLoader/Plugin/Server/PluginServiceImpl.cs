using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.IO;
using System.Net;

namespace DSS.Platform.Plugin.Server
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Single, InstanceContextMode = InstanceContextMode.Single, UseSynchronizationContext = false)]
    public class PluginServiceImpl : IPluginServiceContract
    {
        private DirectoryInfo pluginPath;

        public PluginServiceImpl(string pluginPath)
        {
            this.pluginPath =  new DirectoryInfo(pluginPath);
        }

        /// <summary>
        /// 枚举当前所有插件
        /// </summary>
        /// <returns></returns>
        public PluginItem[] List()
        {
            var ret = this.pluginPath.GetFiles("*.dll").ToList().ConvertAll(d =>
                new PluginItem() { AssemblyName = Path.GetFileNameWithoutExtension(d.Name), UpdateTime = d.LastWriteTime });

            return ret.ToArray();
        }

        /// <summary>
        /// 添加或者更新一个插件
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="pluginUri">下载最新插件程序集的URI地址 eg:http://remote.com/plugins/plugins.hello.dll </param>
        public void Add(string assemblyName, string pluginUri)
        {
            byte[] pluginContent = new System.Net.WebClient().DownloadData(pluginUri);
            if(!assemblyName.ToLower().EndsWith(".dll"))
            {
                assemblyName = string.Concat(assemblyName, ".dll");
            }
            File.WriteAllBytes(Path.Combine(pluginPath.FullName, assemblyName), pluginContent);
        }

        /// <summary>
        /// 依程序集删除一个插件
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        public void Remove(string assemblyName)
        {
            if (!assemblyName.ToLower().EndsWith(".dll"))
            {
                assemblyName = string.Concat(assemblyName, ".dll");
            }

            File.Delete(Path.Combine(pluginPath.FullName, assemblyName));
        }
    }
}
