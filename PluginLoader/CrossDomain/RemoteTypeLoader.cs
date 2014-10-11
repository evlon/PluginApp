using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace DSS.Platform.CrossDomain
{
    public class RemoteTypeLoader : MarshalByRefObject
    {
        private static ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private string assemblyPath;
        private Assembly assembly;
        private string pluginPath;

        public RemoteTypeLoader(string pluginPath)
        {
            this.pluginPath = pluginPath;
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception)
                log.Error("UnhandledException", (Exception)e.ExceptionObject);
            else
                log.Error("UnhandledException");
        }

        Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if(Path.IsPathRooted(args.Name))
                return null;

            return Assembly.LoadFrom(Path.Combine(pluginPath, args.Name));
        }

        protected virtual Assembly LoadAssembly(string assemblyPath)
        {
            //这样加载，会引发 AssemblyResolve
            return Assembly.Load(assemblyPath);
        }

        public object CreateInstance(string fullTypeName)
        {
            if (assembly == null)
                return null;

            return assembly.CreateInstance(fullTypeName,false);
        }

        public void InitTypeLoader(string assemblyPath)
        {
            this.assemblyPath = assemblyPath;
            this.assembly = this.LoadAssembly(this.assemblyPath);
        }

      
    }


}
