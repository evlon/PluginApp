using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PluginAdmin
{
    class Program
    {
        static void Main(string[] args)
        {
            using (WebReferences.PluginServiceImpl ps = new WebReferences.PluginServiceImpl())
            {
                var list = ps.List();

                foreach (var i in list)
                {
                    Console.Write("{0}, {1}", i.AssemblyName, i.UpdateTime);
                }

                Console.WriteLine("开始删除");
                Console.ReadLine();
                if (list.Length > 0)
                {
                    ps.Remove(list[0].AssemblyName);

                    list = ps.List();

                    foreach (var i in list)
                    {
                        Console.Write("{0}, {1}", i.AssemblyName, i.UpdateTime);
                    }
                }
                Console.WriteLine("开始部署");
                Console.ReadLine();


                System.IO.Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(),"plugins"), "Plugins.*.dll").ToList().ForEach(d =>
                    {
                        //http://localhost/plugins/Plugins.HelloWorld.dll
                        ps.Add(Path.GetFileNameWithoutExtension(d), "http://localhost/plugins/" + Path.GetFileName(d));
                    });


                list = ps.List();

                foreach (var i in list)
                {
                    Console.Write("{0}, {1}", i.AssemblyName, i.UpdateTime);
                }

            }




            Console.ReadLine();
            
        }
    }
}
