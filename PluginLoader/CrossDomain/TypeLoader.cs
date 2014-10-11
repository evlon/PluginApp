using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSS.Platform.CrossDomain
{
    public class TypeLoader 
    {
        protected RemoteTypeLoader remoteTypeLoader;
        private string pluginPath;

        public string TargetAssemblyPath { get; private set; }

        public AppDomain RemoteDomain { get; private set; }

       

        public RemoteTypeLoader CreateRemoteTypeLoader(Type remoteLoaderType, string targetDomainName = null)
        {
            AppDomainSetup setup = new AppDomainSetup();
            setup.ApplicationName = "AppLoader";
            setup.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
            setup.PrivateBinPath = pluginPath;
            setup.CachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CachePath");;
            setup.ShadowCopyFiles = "true";
            setup.ShadowCopyDirectories = string.Concat(setup.ApplicationBase, ";", setup.PrivateBinPath);

            this.RemoteDomain = AppDomain.CreateDomain(targetDomainName ?? string.Concat("AppLoaderDomain_", Guid.NewGuid().ToString()), null, setup);

            Type typeName = remoteLoaderType;

            RemoteTypeLoader ret = (RemoteTypeLoader)RemoteDomain.CreateInstanceAndUnwrap(typeName.Assembly.FullName, typeName.FullName,false, System.Reflection.BindingFlags.Default,
                null, new object[]{pluginPath},null,null);

            return ret;
        }
        protected TypeLoader()
        { }

        public TypeLoader(string targetAssembly, string pluginPath)
        {
            this.TargetAssemblyPath = targetAssembly;
            this.pluginPath = pluginPath;
            this.remoteTypeLoader = CreateRemoteTypeLoader();
            this.remoteTypeLoader.InitTypeLoader(targetAssembly);
        }

        protected virtual RemoteTypeLoader CreateRemoteTypeLoader()
        {
            return CreateRemoteTypeLoader(typeof(RemoteTypeLoader));
        }

        public void Unload()
        {
            AppDomain.Unload(this.RemoteDomain);
        }

    }
}
