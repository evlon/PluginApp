using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;

namespace DSS.Platform.Plugin.Server
{
    /// <summary>
    /// 插件WCF启动、停止类
    /// </summary>
    public class PluginService
    {
        private ServiceHost host;

        /// <summary>
        /// 开始服务
        /// </summary>
        /// <param name="hostName">主机名</param>
        /// <param name="port">端口</param>
        /// <param name="pluginPath">插件目录，一定要和插件加载类配置成一样的目录</param>
        public void Start(string hostName, int port, string pluginPath = "plugins")
        {
            IPluginServiceContract serviceContract = new PluginServiceImpl(pluginPath);


            this.host = new ServiceHost(serviceContract, new UriBuilder("http", hostName, port, "pluginservice").Uri);
            
            var smb = host.Description.Behaviors.Find<ServiceMetadataBehavior>();
            if (smb == null)
            {
                smb = new ServiceMetadataBehavior();
                host.Description.Behaviors.Add(smb);
            }
            smb.HttpGetEnabled = true;
            smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy12;


            host.AddServiceEndpoint(ServiceMetadataBehavior.MexContractName, MetadataExchangeBindings.CreateMexHttpBinding()
                , "mex");

              host.AddServiceEndpoint(typeof(IPluginServiceContract), new BasicHttpBinding()
                , "");
            host.Open();

        }

        /// <summary>
        /// 停止服务
        /// </summary>
        public void Stop()
        {
            this.host.Close();
        }
    }
}
