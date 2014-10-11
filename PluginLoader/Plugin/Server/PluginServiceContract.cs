using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace DSS.Platform.Plugin.Server
{
    /// <summary>
    /// 插件管理接口
    /// </summary>
    [ServiceContract]
    public interface IPluginServiceContract
    {
        [OperationContract]
        PluginItem[] List();

        [OperationContract]
        void Add(string assemblyName, string pluginUri);

        [OperationContract]
        void Remove(string assemblyName);
    }

    /// <summary>
    /// 数据类
    /// </summary>
    [DataContract]
    public class PluginItem
    {
        /// <summary>
        /// 程序集名称
        /// </summary>
        [DataMember]
        public string AssemblyName { get; set; }

        /// <summary>
        /// 更新日期
        /// </summary>
        [DataMember]
        public DateTime UpdateTime { get; set; }
    }
}
