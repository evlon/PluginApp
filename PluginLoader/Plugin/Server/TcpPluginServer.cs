using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace DSS.Platform.Plugin.Server
{
    public class TcpPluginServer<T> where T :class
    {
        protected ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private System.Net.Sockets.TcpListener tcpListener;
        private PluginManager<T> pluginManager = null;
        private bool exiting = false;

        public PluginManager<T> Manager { get; private set; }

        public TcpPluginServer(int port)
        {
            this.tcpListener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
            this.pluginManager = new PluginManager<T>();


        }

        public void Start(string pluginPath = "plugins")
        {
            exiting = false;
            tcpListener.Start();
            tcpListener.BeginAcceptSocket(new AsyncCallback(ListenerCallback), tcpListener);
            this.pluginManager.Start(pluginPath);
            
        }

        public void Stop()
        {
            exiting = true;
            this.tcpListener.Stop();
            this.pluginManager.Stop();

        }


        public void ListenerCallback(IAsyncResult result)
        {
            TcpListener listener = (TcpListener)result.AsyncState;
            // Call EndGetContext to complete the asynchronous operation.
            Socket client = null;
            
            try
            {
                client = listener.EndAcceptSocket(result);
            }
            catch (SocketException exp)
            {
                log.Error(exp);
            }
            catch (Exception exp)
            {
                log.Error(exp);
                if (!exiting)
                    throw;
            }
            finally
            {
                if(!exiting)
                    listener.BeginAcceptTcpClient(new AsyncCallback(ListenerCallback), listener);
            }

            if (client != null)
            {
                CreateProcessor(client);
            }
        }

        private void CreateProcessor(Socket client)
        {
            var processor = new WorkProcessor(this, client);
            processor.Run(ProcessCmdLine);
        }

        private void ProcessCmdLine(WorkProcessor processor, LineCmd cmdline)
        {
            string cmd = cmdline.CmdLine;
            Dictionary<string,string> args = null;

            int pos = cmdline.CmdLine.IndexOf(' ');
            if(pos != -1)
            {
                cmd = cmdline.CmdLine.Substring(0, pos);
                args = cmdline.CmdLine.Substring(pos + 1).Split(' ').ToList()
                    .ConvertAll(s=>{ var p = s.IndexOf('='); return p == -1 ? new {Name = s, Value = ""} : new {Name = s.Substring(0, p), Value = s.Substring(p + 1)};})
                    .ToDictionary(s=>s.Name, s=>s.Value);
            }

            switch (cmd)
            {
                case "list":
                    {
                        var list = this.pluginManager.GetPlugins();
                        cmdline.Resp = string.Join("|", list.ToList().ConvertAll(d => d.PluginAssemblyPath).ToArray());
                    }
                    break;
                case "add":
                    {
                        this.pluginManager.AddPlugin(cmdline.Bytes, args["file"]);
                    }
                    break;
                case "remove":
                    {
                        this.pluginManager.RemovePlugin(args["file"]);
                    }
                    break;
                case "q":
                    {
                        cmdline.DisconnectSocket = true;
                    }
                    break;

                default:
                    {
                        
                    }
                    break;
            }
        }


        class LineCmd
        {
            public LineCmd()
            {
                DisconnectSocket = false;
            }
            public string CmdLine { get; set; }
            public string Resp { get; set; }
            public bool DisconnectSocket { get; set; }

            public FileStream FileStream { get; set; }

            public byte[] Bytes { get; set; }
        }


        class WorkProcessor
        {
            /// <summary>
            /// 行最大字节长度
            /// </summary>
            public const int MaxTextLineByteLength = 1024;
            protected ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            private static ConcurrentDictionary<Socket, WorkProcessor> onlineClient = new ConcurrentDictionary<Socket, WorkProcessor>();

            /// <summary>
            /// \n
            /// </summary>
            public readonly byte LineEnd = Encoding.UTF8.GetBytes(new[] { '\n' })[0];
            private TcpPluginServer<T> pluginServer;
            private Socket client;
            private byte[] buffer = new byte[(MaxTextLineByteLength + 1) * 4]; //UTF8 如果是汉字， 每个字符4字节
            private int receivedBytesCount = 0;
            private Action<WorkProcessor, LineCmd> cmdlineProcessor;

            public WorkProcessor(TcpPluginServer<T> pluginServer, Socket client)
            {
                this.pluginServer = pluginServer;
                this.client = client;
                onlineClient.TryAdd(client, this);
            }


            internal void Run(Action<WorkProcessor, LineCmd> cmdlineProcessor)
            {
                this.cmdlineProcessor = cmdlineProcessor;
                receivedBytesCount = 0;
                BeginReceive(client);
               
            }

            static void  CloseSocket(Socket socket)
            {
                WorkProcessor removed;
                onlineClient.TryRemove(socket, out removed);
                socket.Close();
            }

            private void BeginReceive(Socket c)
            {
                try
                {
                    c.BeginReceive(buffer, receivedBytesCount, buffer.Length - receivedBytesCount, SocketFlags.None, new AsyncCallback(OnReceived), c);
                }
                catch (SocketException ex)
                {
                    log.ErrorFormat("BeginReceive", ex);
                    WorkProcessor removed;
                    onlineClient.TryRemove(client, out removed);
                }
                catch (ObjectDisposedException ex)
                {
                    log.ErrorFormat("BeginReceive", ex.Message);
                    WorkProcessor removed;
                    onlineClient.TryRemove(client, out removed);
                }
            }

            

            private void OnReceived(IAsyncResult ar)
            {
                var c = (Socket)ar.AsyncState;
                int l = c.EndReceive(ar);
                if (l > 0)
                {
                   
                        receivedBytesCount += l;
                        var pos = Array.IndexOf(buffer, LineEnd, receivedBytesCount - l, l);
                        byte[] bytes = null;
                        if (pos != -1)
                        {
                            string cmdLinePrefix = Encoding.UTF8.GetString(buffer, 0, pos).Trim("\r\n ".ToArray());

                            if (cmdLinePrefix.EndsWith("#")//表示后面有数据
                                && (receivedBytesCount - 1 - 9 > pos)  // 后面是8个字节， 首字节为0的7Byte长度
                                && (buffer[pos + 1] == 0)) //后面还有数据 第一个字节是0， 后面7Byte为接收的长度
                            {
                                if (l < sizeof(Int64))
                                {
                                    log.Debug("发送文件格式不正确");
                                    CloseSocket(c);
                                }

                                long bytesLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt64(buffer, pos + 1));

                                bytes = new byte[bytesLength];
                                for (long len = 0; len < bytesLength; )
                                {
                                    try
                                    {
                                        var rl = c.Receive(bytes, (int)len, (int)(bytes.LongLength - len), SocketFlags.None);
                                        if (rl <= 0)
                                        {
                                            CloseSocket(c);
                                            return;
                                        }

                                        len += rl;

                                    }
                                    catch (Exception ex)
                                    {
                                        log.ErrorFormat("Receive Bytes", ex.Message);
                                        CloseSocket(client);
                                        return;
                                    }

                                }

                                receivedBytesCount = 0;


                                ///TODO:这里添加接收文件
                                // buffer.Skip(1).Take(4).ToArray()
                            }

                            ProcessCmdline(cmdLinePrefix,bytes);
                        }
                        else
                        {
                            BeginReceive(c);
                        }
                   
                }
                else
                {
                    CloseSocket(c);
                }

            }

            private void ProcessCmdline(string line, byte[] bytes)
            {
                Debug.Assert(cmdlineProcessor != null, "必须有处理器");
                var cmdLine = new LineCmd() { CmdLine = line, Bytes = bytes }; 
                
                try
                {
                   
                    cmdlineProcessor(this, cmdLine );
                    if (cmdLine.DisconnectSocket)
                    {
                        this.client.Close();
                        return;
                    }
                    
                }
                catch (Exception ex)
                {
                    log.ErrorFormat("cmdlineProcessor", ex.Message);
                    CloseSocket(client);
                    return;
                }
                if (string.IsNullOrEmpty(cmdLine.Resp))
                {
                    receivedBytesCount = 0;
                    BeginReceive(client);
                }
                else
                {
                    try
                    {
                        var bytesSend = Encoding.UTF8.GetBytes(cmdLine.Resp);
                        client.BeginSend(bytesSend, 0, bytesSend.Length, SocketFlags.None, new AsyncCallback(OnSent), client);
                    }
                    catch (SocketException ex)
                    {
                        log.ErrorFormat("BeginReceive", ex);
                        CloseSocket(client);
                    }
                    catch (ObjectDisposedException ex)
                    {
                        log.ErrorFormat("BeginReceive", ex.Message);
                        CloseSocket(client);
                    }
                }
            }

            private void OnSent(IAsyncResult ar)
            {
                var c = (Socket)ar.AsyncState;
                c.EndSend(ar);
                receivedBytesCount = 0;

                BeginReceive(c);
               
            }



        }

    }



}
