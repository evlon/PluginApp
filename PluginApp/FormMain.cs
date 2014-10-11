using DSS.Platform.Plugin;
using DSS.Platform.Plugin.Emit;
using DSS.Platform.Plugin.Server;
using PluginBase;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Windows.Forms;

namespace PluginApp
{
    public partial class FormMain : Form
    {
        private PluginLoader<IPlugin> loader;
        public FormMain()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.loader = new PluginLoader<IPlugin>("Plugins.HelloWorld.dll", "plugins");
         
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (loader == null)
                return;

            MessageBox.Show(loader.Plugin.Run("111").ToString());
            MessageBox.Show(loader.Plugin.Run("111", new Action(() => { MessageBox.Show(AppDomain.CurrentDomain.FriendlyName); }).CreateRemoteAppDomainProxy()));
            MessageBox.Show(loader.Plugin.Run("111", new Func<string>(() => { MessageBox.Show(AppDomain.CurrentDomain.FriendlyName); return "fun ret"; }).CreateRemoteAppDomainProxy()));
        }
        private void button6_Click(object sender, EventArgs e)
        {
            if (loader == null)
                return;

            MessageBox.Show(loader.Plugin.Run("111").ToString());
            MessageBox.Show(loader.Plugin.Run("111", new Action(() => { MessageBox.Show(AppDomain.CurrentDomain.FriendlyName); })));
            MessageBox.Show(loader.Plugin.Run("111", new Func<string>(() => { MessageBox.Show(AppDomain.CurrentDomain.FriendlyName); return "fun ret"; })));
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (loader == null)
                return;
            this.loader.Unload();
            
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (loader == null)
                return;
            var watch = Stopwatch.StartNew();
            for(int i = 0; i < 10000; ++i)
            {
                loader.Plugin.Run("1");
            }
            watch.Stop();
            var ms1 = watch.Elapsed.TotalMilliseconds;

            watch.Restart();
            Action a = new Action(() => { });
            for (int i = 0; i < 10000; ++i)
            {
                a();
            }
            watch.Stop();
            var ms2 = watch.Elapsed.TotalMilliseconds;

            Debug.Print("跨域:{0},   本地:{1}", ms1, ms2);

            //跨域:1032.3765,   本地:0.2803


            watch.Restart();
            for (int i = 0; i < 10000; ++i)
            {
                loader.Plugin.Run("1", a);
            }
            watch.Stop();
            ms1 = watch.Elapsed.TotalMilliseconds;

            watch.Restart();

            for (int i = 0; i < 10000; ++i)
            {
                loader.Plugin.Run("1", a.CreateRemoteAppDomainProxy());
            }
            watch.Stop();
            ms2 = watch.Elapsed.TotalMilliseconds;

            Debug.Print("跨域回调:插件域{0},   本域:{1}", ms1, ms2);

            //跨域回调:插件域4069.8274,   本域:4305.1899
            /*
                    [LoaderOptimization( LoaderOptimization.MultiDomainHost)]

            跨域:984.0413,   本地:0.2713
            跨域回调:插件域3890.3369,   本域:4165.8899 
             * 
             */
            /*
                   [LoaderOptimization( LoaderOptimization.MultiDomain)]

            跨域:988.6496,   本地:0.2739
            跨域回调:插件域3922.3287,   本域:4143.324             * 
            */
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
           
          //这里加上了泛型参数 IPlugin表示要求自动生成这个接口的代理类
            var inst = new PluginManager<IPlugin>().Start();
            inst.PluginChanged += OnPluginChanged;

        }

        private void button8_Click(object sender, EventArgs e)
        {
            this.server = new TcpPluginServer<IPlugin>(801);
            server.Start();
        }

        IPlugin lastLoadedPlugin = null;
        private TcpPluginServer<IPlugin> server;
        void OnPluginChanged(object sender, PluginManagerEventArgs<IPlugin> e)
        {
            if (e.ChangeType == PluginChangeType.Created)
            {
                lastLoadedPlugin = e.PluginInstance;
                // 这里初始化插件，提供服务
                e.PluginInstance.Run(DateTime.Now.ToString(), new Action(() => { Debug.Print(AppDomain.CurrentDomain.FriendlyName); MessageBox.Show(DateTime.Now.ToString()); }));
            }
        }


        private void button7_Click(object sender, EventArgs e)
        {
            if (lastLoadedPlugin != null)
            {
                Debug.Print("Return => {0}",lastLoadedPlugin.Run(DateTime.Now.ToString()));
                Debug.Print("Return => {0}",lastLoadedPlugin.Run(DateTime.Now.ToString(), new Action(() => { Debug.Print(AppDomain.CurrentDomain.FriendlyName); })));
                Debug.Print("Return => {0}", lastLoadedPlugin.Run(DateTime.Now.ToString(), new Func<string>(() => { Debug.Print(AppDomain.CurrentDomain.FriendlyName); return "2233"; })));

                int idleTimeout = 30;
                object userContext;
                byte? flagByte = 0;
                int needReceiveCount;
                byte[] bytesReturn;

                lastLoadedPlugin.ProcessAccept(ref idleTimeout, out userContext, ref flagByte, out needReceiveCount, out bytesReturn);
                Debug.Print("needReceiveCount=" + needReceiveCount.ToString());

                //尝试序列化
                BinaryFormatter bf = new BinaryFormatter();
                using (MemoryStream ms = new MemoryStream())
                {
                    var obj = lastLoadedPlugin.Run("");
                    Debug.Print("Return => {0}", obj.Id);
                    bf.Serialize(ms, obj);

                    ms.Position = 0;

                    var obj2 = bf.Deserialize(ms) as IMessage;
                    Debug.Print("Return => {0}", obj2.Id);
                }

                //IMessage obj = lastLoadedPlugin.Run("");
                //Debug.Print("Return => {0}", obj.ToString());
                //byte[] bytes = lastLoadedPlugin.Serialize((MarshalByRefObject)obj);
                //IMessage obj2 = lastLoadedPlugin.Deserialize(bytes) as IMessage;
                //Debug.Print("Return => {0}", obj2.Id);

            }
        }


        private void button5_Click(object sender, EventArgs e)
        {
            var proxy = InterfaceProxyBuilder<IPlugin>.CreateProxy(new TestPlugin(), typeof(object));
            proxy.Run(DateTime.Now.ToString());
            proxy.Run(DateTime.Now.ToString(), () => { Debug.Print("OK"); });
            proxy.Run(DateTime.Now.ToString(), () => { Debug.Print("OK"); return "OK"; });

            var proxy2 = InterfaceProxyBuilder<IPlugin>.CreateFuncProxy(() => new TestPlugin(), typeof(object));
            proxy2.Run(DateTime.Now.ToString());
            proxy2.Run(DateTime.Now.ToString(), () => { Debug.Print("OK"); });
            proxy2.Run(DateTime.Now.ToString(), () => { Debug.Print("OK"); return "OK"; });
        }

 

    }

    public class TestPlugin : IPlugin
    {
        public Guid Guid
        {
            get { return Guid.NewGuid(); }
        }

        public IMessage Run(string args)
        {
            Debug.Print(args);
            return new MessageObject() { Id = Guid.NewGuid().ToString("N") };
        }

        public string Run(string args, Action action)
        {
            action();
            return args;
        }

        public string Run(string args, Func<string> func)
        {
            func();
            return args;
        }


        public void ProcessAccept(ref int idleTimeout, out object userContext, ref byte? flagByte, out int needReceiveCount, out byte[] bytesReturn)
        {
            userContext = new object();
            needReceiveCount = 9;
            bytesReturn = null;
        }

        public byte[] Serialize(MarshalByRefObject obj)
        {
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);

                ms.Position = 0;

                return ms.ToArray();
            }
        }

        public MarshalByRefObject Deserialize(byte[] bytes)
        {
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                var obj2 = bf.Deserialize(ms) as MarshalByRefObject;
                return obj2;
            }
        }
    }
}
