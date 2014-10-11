using DSS.Platform.Plugin.ImplObjects;
using DSS.Platform.Plugin;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Text;

namespace DSS.Platform.Plugin.Emit
{
    /// <summary>
    /// 从接口 TPlugin 生成代理类
    /// </summary>
    /// <typeparam name="TPlugin">要求是一个接口</typeparam>
    public static class InterfaceProxyBuilder<TPlugin> where TPlugin : class
    {
        private static ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
#if EMITSAVE
        private static System.Threading.Mutex locker = new System.Threading.Mutex(false, typeof(InterfaceProxyBuilder<TPlugin>).GUID.ToString("N"));
#endif
        private static MethodInfo miActionConverter = typeof(RemoteActionProxy).GetMethod("CreateProxyAction");
        private static ConcurrentDictionary<Type, MethodInfo> miFuncConverter = new ConcurrentDictionary<Type, MethodInfo>();
        private static ConcurrentDictionary<Type, Type> proxies = new ConcurrentDictionary<Type, Type>();
        private static ConcurrentDictionary<Type, Object> proxyLockers = new ConcurrentDictionary<Type, Object>();
        private static ConcurrentDictionary<string, Assembly> dynamicAssembly = new ConcurrentDictionary<string, Assembly>();
        private static ConcurrentDictionary<string, Type> dynamicTypes = new ConcurrentDictionary<string, Type>();
        static InterfaceProxyBuilder()
        {
            //AssemblyName assemblyName = new AssemblyName(string.Concat(AppDomain.CurrentDomain.FriendlyName,"_DynamicAssembly"));
            //assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            ////string typeName = string.Concat(targetType.FullName, "_Proxy");
            //module = assemblyBuilder.DefineDynamicModule("DynamicProxy", "DynamicAssembly.dll");

            AppDomain.CurrentDomain.TypeResolve+=CurrentDomain_TypeResolve;
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

        }

        private static Assembly CurrentDomain_TypeResolve(object sender, ResolveEventArgs args)
        {
            Debug.Print("TypeResolve :" + args.Name);

            Type type;
            if (dynamicTypes.TryGetValue(args.Name, out type))
                return type.Assembly;

            return null; 
        }

        static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            Debug.Print("AssemblyResolve :" + args.Name);
            Assembly ret;
            if (dynamicAssembly.TryGetValue(args.Name, out ret))
                return ret;
            return null;

        }

        private static void CheckTargetType(Type targetType)
        {
            if (!targetType.IsInterface)
            {
                throw new NotImplementedException("T 必须为接口");
            }

        }

        /// <summary>
        /// 生成接口实例对像的代理
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <param name="target"></param>
        /// <returns></returns>
        public static TPlugin CreateProxy<TObject>(TObject target, Type baseType,              
            bool igoreCrossDomainAttribute = false, 
            Type retOutBaseType = null,
            Func<Type, TPlugin> instanceCreator = null,
            Action<TypeBuilder> onCreateType = null
            ) where TObject : TPlugin
        {
            Type targetType =typeof(TPlugin);
            CheckTargetType(targetType);

            var proxyType = proxies.GetOrAdd(targetType, t =>
            {
                var locker = proxyLockers.GetOrAdd(t, new object());

                lock (locker)
                {
                    Type val;
                    if (proxies.TryGetValue(t, out val))
                        return val;

                    return CreateProxyType(t, baseType, igoreCrossDomainAttribute, retOutBaseType, onCreateType);
                } 
            });

            if (instanceCreator == null)
            {
                return proxyType.GetConstructor(new Type[] { targetType }).Invoke(new object[] { target }) as TPlugin;
            }
            else
            {
                return instanceCreator(proxyType);
            }

            //return System.Activator.CreateInstance(proxyType, (TObject)target) as TPlugin;
        }

        /// <summary>
        /// 生成一个接口的代理类，并从baseType继承
        /// </summary>
        /// <param name="targetType">接口类型</param>
        /// <param name="baseType">基类</param>
        /// <returns>代理类</returns>
        public static Type CreateProxyType(Type targetType, Type baseType, bool igoreCrossDomainAttribute = false,
            Type retOutTypeBase = null, Action<TypeBuilder> onCreateType = null) 
        {
            Debug.Assert(targetType.IsInterface,"必须是接口");

            AssemblyName assemblyName = new AssemblyName(string.Concat("ProxyType_", string.Concat(targetType.GUID.ToString("N"), baseType.GUID.ToString("N"), retOutTypeBase.GUID.ToString("N")).ComputeMd5()));
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);

            var module = assemblyBuilder.DefineDynamicModule(assemblyName.Name, string.Concat(assemblyName.Name, ".dll"));
            string typeName = string.Concat(targetType.FullName,"_", baseType.Name, "_Proxy");
            //声明类
            
            var typeBuilder = module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, baseType,  new Type[] { targetType }); //

            //实现类

            //字段
            FieldInfo filedTarget = baseType.GetField("target", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (filedTarget == null)
            {
                var fieldBuilder = typeBuilder.DefineField("target", targetType, FieldAttributes.Private);
                fieldBuilder.SetConstant(null);
                filedTarget = fieldBuilder;



                //构造函数
                var cnstBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { targetType });
                {
                    var il = cnstBuilder.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_1);

                    il.Emit(OpCodes.Stfld, filedTarget);
                    il.Emit(OpCodes.Ret);
                }
            }
            else
            {
                foreach (var cnst in baseType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var parmInfos = cnst.GetParameters();
                    var parmTypes = parmInfos.ToList().ConvertAll(pi => pi.ParameterType).ToArray();

                    var cnstBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, parmTypes);
                    {
                        var il = cnstBuilder.GetILGenerator();
                        il.Emit(OpCodes.Ldarg_0);
                        for (int i = 1; i <= parmTypes.Length; ++i)
                        {
                            switch (i)
                            {
                                case 0:
                                    il.Emit(OpCodes.Ldarg_0);
                                    break;
                                case 1:
                                    il.Emit(OpCodes.Ldarg_1);
                                    break;
                                case 2:
                                    il.Emit(OpCodes.Ldarg_2);
                                    break;
                                case 3:
                                    il.Emit(OpCodes.Ldarg_3);
                                    break;
                                default:
                                    il.Emit(OpCodes.Ldarg_S, (byte)i);
                                    break;
                            }
                        }


                        //il.Emit(OpCodes.Stfld, fieldBuilder);
                        il.Emit(OpCodes.Call, cnst);
                        il.Emit(OpCodes.Ret);
                    }
                }
            }

            if (onCreateType != null)
            {
                onCreateType(typeBuilder);
            }

            

            //属性
            Dictionary<MethodInfo, MethodBuilder> map = new Dictionary<MethodInfo, MethodBuilder>();


            //方法代理
            List<MethodInfo> methods = new List<MethodInfo>();

            methods.AddRange(targetType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            foreach (var tt in targetType.GetInterfaces())
            {
                methods.AddRange(tt.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            }

            methods.ForEach(mi =>
            {
                var retType = mi.ReturnType;

                var parmInfos = mi.GetParameters();
                var parmTypes = parmInfos.ToList().ConvertAll(pi => pi.ParameterType).ToArray();


                MethodAttributes methodAttrs = mi.Attributes & (~MethodAttributes.Abstract);

                var methodBuilder = typeBuilder.DefineMethod(mi.Name, methodAttrs, CallingConventions.Standard, mi.ReturnType, parmTypes);
                parmInfos.Where(pi => pi.IsOut).ToList().ForEach(pi => {
                    methodBuilder.DefineParameter(pi.Position + 1, ParameterAttributes.Out, string.Concat("arg", pi.Position));
                });
                //方法体
                {
                    var il = methodBuilder.GetILGenerator();

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, filedTarget);
                    if (parmTypes.Length > 0)
                    {
                        for (int i = 1; i <= parmTypes.Length; ++i)
                        {
                            switch (i)
                            {
                                case 0:
                                    il.Emit(OpCodes.Ldarg_0);
                                    break;
                                case 1:
                                    il.Emit(OpCodes.Ldarg_1);
                                    break;
                                case 2:
                                    il.Emit(OpCodes.Ldarg_2);
                                    break;
                                case 3:
                                    il.Emit(OpCodes.Ldarg_3);
                                    break;
                                default:
                                    il.Emit(OpCodes.Ldarg_S, (byte)i);
                                    break;
                            }

                            //检查参数，如果是 CrossDomain ，并且是Action 或者是 Func<T> 进行跨域替换
                            if (!igoreCrossDomainAttribute)
                            {
                                Type prmType = parmTypes[i - 1];
                                var prmInfo = parmInfos[i - 1];
                                if (prmInfo.GetCustomAttributes(typeof(CallerDomainRun), true).Length > 0)
                                {
                                    if (prmType.Equals(typeof(Action)))
                                    {
                                        il.Emit(OpCodes.Call, miActionConverter);
                                    }
                                    else if (prmType.IsGenericType && (prmType.BaseType == typeof(Func<>).BaseType))
                                    {
                                        var miProxy = miFuncConverter.GetOrAdd(prmType, rt =>
                                        {

                                            var typeProxy = typeof(RemoteFuncProxy<>).MakeGenericType(rt.GetGenericArguments());
                                            return typeProxy.GetMethod("CreateProxyFunc");
                                        });

                                        il.Emit(OpCodes.Call, miProxy);
                                    }
                                }
                            }
                        }
                    }

                    il.Emit(OpCodes.Callvirt, mi);

                    //检查返回参数类型，是不是具有序列化标志， 如果有，每次返回对象时，生成代理返回
                    if (retOutTypeBase != null && mi.ReturnType != typeof(void))
                    {
                        //var cas = mi.ReturnType.GetCustomAttributes(typeof(DomainSerializableAttribute), true);
                        //if (cas.Length > 0)
                        //{
                        //    Type retWraperType = CreateSerializeableProxyType(mi.ReturnType,retOutTypeBase.GetGenericTypeDefinition().MakeGenericType(mi.ReturnType), true);
                        //    var constructorInfo = retWraperType.GetConstructor(new[] { mi.ReturnType });

                        //    il.Emit(OpCodes.Newobj, constructorInfo);
                        //}

                        var cas = mi.ReturnType.GetCustomAttributes(typeof(DomainSerializableAttribute), true);
                        if (cas.Length > 0)
                        {
                            FieldInfo fieldLoader = baseType.GetField("loader", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (fieldLoader != null)
                            {
                                Type loadType = typeof(RemotePluginLoader<>).MakeGenericType(mi.ReturnType);
                                Type retWraperType = CreateSerializeableProxyType(mi.ReturnType, retOutTypeBase.GetGenericTypeDefinition().MakeGenericType(mi.ReturnType), true);
                                var constructorInfo = retWraperType.GetConstructor(new[] { mi.ReturnType, loadType });

                                il.Emit(OpCodes.Ldarg_0);
                                il.Emit(OpCodes.Ldfld, fieldLoader);
                                il.Emit(OpCodes.Newobj, constructorInfo);
                            }

                        }
                    }

                    //if (mi.ReturnType == typeof(void))
                    //{
                    //    il.Emit(OpCodes.Pop);
                    //}
                    il.Emit(OpCodes.Ret);

                }

                map.Add(mi, methodBuilder);

            });

            List<PropertyInfo> props = new List<PropertyInfo>();

            props.AddRange(targetType.GetProperties());
            foreach (var tt in targetType.GetInterfaces())
            {
                props.AddRange(tt.GetProperties());
            }
            //var props = targetType.GetProperties();
            props.ToList().ForEach(pi =>
            {
                var propBuilder = typeBuilder.DefineProperty(pi.Name, PropertyAttributes.HasDefault, pi.ReflectedType, null);

                if (pi.CanRead)
                {
                    var mi = pi.GetGetMethod();
                    Debug.Assert(map.ContainsKey(mi));

                    var builder = map[mi];

                    propBuilder.SetGetMethod(builder);

                }

                if (pi.CanWrite)
                {
                    var mi = pi.GetSetMethod();
                    Debug.Assert(map.ContainsKey(mi));

                    var builder = map[mi];

                    propBuilder.SetSetMethod(builder);

                }


            });

            

            var ret = typeBuilder.CreateType();

#if EMITSAVE
            locker.WaitOne(5000);
            try
            {
                assemblyBuilder.Save(string.Concat(typeName, "_", DateTime.Now.ToString("yyyyMMddHHmmss"), Guid.NewGuid().ToString("N"), ".dll"));
            }
            catch (Exception ex)
            {
                log.Error(" assemblyBuilder.Save Error", ex);
            }
            finally
            {
                locker.ReleaseMutex();
            }
#endif

            dynamicAssembly.TryAdd(ret.Assembly.FullName, ret.Assembly);
            dynamicTypes.TryAdd(ret.Assembly.FullName, ret);
           return ret;

        }


        public static TPlugin CreateFuncProxy(Func<TPlugin> targetFunc, Type baseType, bool igoreCrossDomainAttribute = false)
        {
            Type targetType = typeof(TPlugin);
            CheckTargetType(targetType);

            var proxyType = proxies.GetOrAdd(typeof(Func<TPlugin>), t =>
            {
                var locker = proxyLockers.GetOrAdd(t, new object());

                lock (locker)
                {
                    Type val;
                    if (proxies.TryGetValue(t, out val))
                        return val;

                    return CreateFuncProxyType(t, baseType, igoreCrossDomainAttribute);
                }
            });

            return System.Activator.CreateInstance(proxyType, targetFunc) as TPlugin;
        }

        /// <summary>
        /// 生成一个接口工厂的代理类，并从baseType继承
        /// </summary>
        /// <param name="targetType">接口工厂类型 要求必须为 Func&lt;TPlugin&gt;</param>
        /// <param name="baseType">基类</param>
        /// <returns>代理类</returns>
        public static Type CreateFuncProxyType(Type targetType, Type baseType, bool igoreCrossDomainAttribute = false) 
        {
            Debug.Assert(typeof(Delegate).IsAssignableFrom(targetType), "必须是一个委托");
            var miInvoke = targetType.GetMethod("Invoke");

            Type interfaceType = miInvoke.ReturnType;

            AssemblyName assemblyName = new AssemblyName(string.Concat("FuncProxyType_", string.Concat(targetType.GUID.ToString("N"), baseType.GUID.ToString("N")).ComputeMd5()));
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            var module = assemblyBuilder.DefineDynamicModule(assemblyName.Name, string.Concat(assemblyName.Name, ".dll"));

            string typeName = string.Concat(interfaceType.FullName, "_FuncProxy"); 
            //声明类

            var typeBuilder = module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, baseType, new Type[] { interfaceType }); //

            //实现类

            //字段
            var fieldBuilder = typeBuilder.DefineField("targetFunc", targetType, FieldAttributes.Private);
            fieldBuilder.SetConstant(null);

            //构造函数
            var cnstBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { targetType });
            {
                var il = cnstBuilder.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);

                il.Emit(OpCodes.Stfld, fieldBuilder);
                il.Emit(OpCodes.Ret);
            }

            //属性
            Dictionary<MethodInfo, MethodBuilder> map = new Dictionary<MethodInfo, MethodBuilder>();



            //方法代理
            List<MethodInfo> methods = new List<MethodInfo>();

            methods.AddRange(interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            foreach (var tt in interfaceType.GetInterfaces())
            {
                methods.AddRange(tt.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            }

            //var methods = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Default).ToList();

            methods.ForEach(mi =>
            {
                var retType = mi.ReturnType;
                var parmInfos = mi.GetParameters();
                var parmTypes = parmInfos.ToList().ConvertAll(pi => pi.ParameterType).ToArray();


                MethodAttributes methodAttrs = mi.Attributes & (~MethodAttributes.Abstract);

                var methodBuilder = typeBuilder.DefineMethod(mi.Name, methodAttrs, CallingConventions.Standard, mi.ReturnType, parmTypes);
                parmInfos.Where(pi => pi.IsOut).ToList().ForEach(pi =>
                {
                    methodBuilder.DefineParameter(pi.Position + 1, ParameterAttributes.Out, string.Concat("arg", pi.Position));
                });

                //方法体
                {
                    var il = methodBuilder.GetILGenerator();

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, fieldBuilder);
                    il.Emit(OpCodes.Callvirt, miInvoke);
                    if (parmTypes.Length > 0)
                    {
                        for (int i = 1; i <= parmTypes.Length; ++i)
                        {
                            switch (i)
                            {
                                case 0:
                                    il.Emit(OpCodes.Ldarg_0);
                                    break;
                                case 1:
                                    il.Emit(OpCodes.Ldarg_1);
                                    break;
                                case 2:
                                    il.Emit(OpCodes.Ldarg_2);
                                    break;
                                case 3:
                                    il.Emit(OpCodes.Ldarg_3);
                                    break;
                                default:
                                    il.Emit(OpCodes.Ldarg_S, (byte)i);
                                    break;
                            }
                            if (!igoreCrossDomainAttribute)
                            {
                                //检查参数，如果是 CrossDomain ，并且是Action 或者是 Func<T> 进行跨域替换
                                Type prmType = parmTypes[i - 1];
                                var prmInfo = parmInfos[i - 1];
                                if (prmInfo.GetCustomAttributes(typeof(CallerDomainRun), true).Length > 0)
                                {
                                    if (prmType.Equals(typeof(Action)))
                                    {
                                        il.Emit(OpCodes.Call, miActionConverter);
                                    }
                                    else if (prmType.IsGenericType && (prmType.BaseType == typeof(Func<>).BaseType))
                                    {
                                        var miProxy = miFuncConverter.GetOrAdd(prmType, rt =>
                                        {

                                            var typeProxy = typeof(RemoteFuncProxy<>).MakeGenericType(rt.GetGenericArguments());
                                            return typeProxy.GetMethod("CreateProxyFunc");
                                        });

                                        il.Emit(OpCodes.Call, miProxy);
                                    }
                                }
                            }
                        }
                    }

                    il.Emit(OpCodes.Callvirt, mi);

                    //if (mi.ReturnType == typeof(void))
                    //{
                    //    il.Emit(OpCodes.Pop);
                    //}
                    il.Emit(OpCodes.Ret);

                }

                map.Add(mi, methodBuilder);

            });

            List<PropertyInfo> props = new List<PropertyInfo>();

            props.AddRange(interfaceType.GetProperties());
            foreach (var tt in interfaceType.GetInterfaces())
            {
                props.AddRange(tt.GetProperties());
            } 
            
            //var props = interfaceType.GetProperties();
            props.ToList().ForEach(pi =>
            {
                var propBuilder = typeBuilder.DefineProperty(pi.Name, PropertyAttributes.HasDefault, pi.ReflectedType, null);

                if (pi.CanRead)
                {
                    var mi = pi.GetGetMethod();
                    Debug.Assert(map.ContainsKey(mi));

                    var builder = map[mi];

                    propBuilder.SetGetMethod(builder);

                }

                if (pi.CanWrite)
                {
                    var mi = pi.GetSetMethod();
                    Debug.Assert(map.ContainsKey(mi));

                    var builder = map[mi];

                    propBuilder.SetSetMethod(builder);

                }


            });



            var ret = typeBuilder.CreateType();

#if EMITSAVE
            locker.WaitOne(5000);
            try
            {
                assemblyBuilder.Save(string.Concat( typeName, "_Func_", DateTime.Now.ToString("yyyyMMddHHmmss"),Guid.NewGuid().ToString("N"), ".dll"));
            }
            catch (Exception ex)
            {
                log.Error(" assemblyBuilder.Save Error", ex);
            }
            finally
            {
                locker.ReleaseMutex();
            }
#endif

            dynamicAssembly.TryAdd(ret.Assembly.FullName, ret.Assembly);
            dynamicTypes.TryAdd(ret.Assembly.FullName, ret);
            return ret;            
        }


        /// <summary>
        /// 生成一个接口的代理类，并从baseType继承
        /// </summary>
        /// <param name="targetType">接口类型</param>
        /// <returns>代理类</returns>
        public static Type CreateSerializeableProxyType(Type targetType, Type baseType,
            bool igoreCrossDomainAttribute = false,
            Action<TypeBuilder> onTypeCreator = null
            )
        {
            if (!targetType.IsInterface)
                throw new ArgumentException("必须是接口", "targetType");

            if (!typeof(ISerializable).IsAssignableFrom(baseType))
                throw new ArgumentException("基类必须实现 ISerializable");


            Debug.Assert(targetType.IsInterface, "必须是接口");
            //Type baseType =  typeof(SerializableMarshalByRefObject<>).MakeGenericType(targetType);

            AssemblyName assemblyName = new AssemblyName(string.Concat("SerializeableProxyType_", string.Concat(targetType.GUID.ToString("N"), baseType.GUID.ToString("N")).ComputeMd5()));
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            var module = assemblyBuilder.DefineDynamicModule(assemblyName.Name, string.Concat(assemblyName.Name, ".dll"));
            //声明类
            string typeName = string.Concat(targetType.FullName,"_", baseType.Name, "_SerializeableProxy");

            var typeBuilder = module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed, baseType, new Type[] { targetType }); //

            //添加 SerializableAttribute
            ConstructorInfo classCtorInfo = typeof(SerializableAttribute).GetConstructor(new Type[0]);
            CustomAttributeBuilder caBuilder = new CustomAttributeBuilder( classCtorInfo,new object[0]);
            typeBuilder.SetCustomAttribute(caBuilder);
            //实现类

            //字段
            var fieldTarget = baseType.GetField("target", BindingFlags.Instance | BindingFlags.NonPublic);
            //fieldBuilder.SetConstant(null);

            foreach (var cnst in baseType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var parmInfos = cnst.GetParameters();
                var parmTypes = parmInfos.ToList().ConvertAll(pi => pi.ParameterType).ToArray();

                var cnstBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, parmTypes);
                {
                    var il = cnstBuilder.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0);
                    for (int i = 1; i <= parmTypes.Length; ++i)
                    {
                        switch (i)
                        {
                            case 0:
                                il.Emit(OpCodes.Ldarg_0);
                                break;
                            case 1:
                                il.Emit(OpCodes.Ldarg_1);
                                break;
                            case 2:
                                il.Emit(OpCodes.Ldarg_2);
                                break;
                            case 3:
                                il.Emit(OpCodes.Ldarg_3);
                                break;
                            default:
                                il.Emit(OpCodes.Ldarg_S, (byte)i);
                                break;
                        }
                    }


                    //il.Emit(OpCodes.Stfld, fieldBuilder);
                    il.Emit(OpCodes.Call, cnst);
                    il.Emit(OpCodes.Ret);
                }
            }

            ////构造函数
            //var cnstBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { targetType });
            //{
            //    var il = cnstBuilder.GetILGenerator();
            //    il.Emit(OpCodes.Ldarg_0);
            //    il.Emit(OpCodes.Ldarg_1);

            //    //il.Emit(OpCodes.Stfld, fieldBuilder);
            //    il.Emit(OpCodes.Call, baseType.GetConstructor(new[] { targetType }));
            //    il.Emit(OpCodes.Ret);
            //}

            //var cnstSeriaBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { typeof(SerializationInfo), typeof(StreamingContext ) });
            //{
            //    var il = cnstSeriaBuilder.GetILGenerator();
            //    il.Emit(OpCodes.Ldarg_0);
            //    il.Emit(OpCodes.Ldarg_1);
            //    il.Emit(OpCodes.Ldarg_2);

            //    il.Emit(OpCodes.Call, baseType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(SerializationInfo), typeof(StreamingContext) }, null));
            //    il.Emit(OpCodes.Ret);
            //}



            //属性
            Dictionary<MethodInfo, MethodBuilder> map = new Dictionary<MethodInfo, MethodBuilder>();


            //方法代理
            List<MethodInfo> methods = new List<MethodInfo>();

            methods.AddRange(targetType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            foreach (var tt in targetType.GetInterfaces())
            {
                methods.AddRange(tt.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            }

            methods.ForEach(mi =>
            {
                var retType = mi.ReturnType;

                var parmInfos = mi.GetParameters();
                var parmTypes = parmInfos.ToList().ConvertAll(pi => pi.ParameterType).ToArray();


                MethodAttributes methodAttrs = mi.Attributes & (~MethodAttributes.Abstract);

                var methodBuilder = typeBuilder.DefineMethod(mi.Name, methodAttrs, CallingConventions.Standard, mi.ReturnType, parmTypes);
                parmInfos.Where(pi => pi.IsOut).ToList().ForEach(pi =>
                {
                    methodBuilder.DefineParameter(pi.Position + 1, ParameterAttributes.Out, string.Concat("arg", pi.Position));
                });
                //方法体
                {
                    var il = methodBuilder.GetILGenerator();

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, fieldTarget);
                    if (parmTypes.Length > 0)
                    {
                        for (int i = 1; i <= parmTypes.Length; ++i)
                        {
                            switch (i)
                            {
                                case 0:
                                    il.Emit(OpCodes.Ldarg_0);
                                    break;
                                case 1:
                                    il.Emit(OpCodes.Ldarg_1);
                                    break;
                                case 2:
                                    il.Emit(OpCodes.Ldarg_2);
                                    break;
                                case 3:
                                    il.Emit(OpCodes.Ldarg_3);
                                    break;
                                default:
                                    il.Emit(OpCodes.Ldarg_S, (byte)i);
                                    break;
                            }

                            //检查参数，如果是 CrossDomain ，并且是Action 或者是 Func<T> 进行跨域替换
                            if (!igoreCrossDomainAttribute)
                            {
                                Type prmType = parmTypes[i - 1];
                                var prmInfo = parmInfos[i - 1];
                                if (prmInfo.GetCustomAttributes(typeof(CallerDomainRun), true).Length > 0)
                                {
                                    if (prmType.Equals(typeof(Action)))
                                    {
                                        il.Emit(OpCodes.Call, miActionConverter);
                                    }
                                    else if (prmType.IsGenericType && (prmType.BaseType == typeof(Func<>).BaseType))
                                    {
                                        var miProxy = miFuncConverter.GetOrAdd(prmType, rt =>
                                        {

                                            var typeProxy = typeof(RemoteFuncProxy<>).MakeGenericType(rt.GetGenericArguments());
                                            return typeProxy.GetMethod("CreateProxyFunc");
                                        });

                                        il.Emit(OpCodes.Call, miProxy);
                                    }
                                }
                            }
                        }
                    }

                    il.Emit(OpCodes.Callvirt, mi);

                    //检查返回参数类型，是不是具有序列化标志， 如果有，每次返回对象时，生成代理返回
                    if (mi.ReturnType != typeof(void))
                    {
                        var cas = mi.ReturnType.GetCustomAttributes(typeof(DomainSerializableAttribute), true);
                        if (cas.Length > 0)
                        {
                            FieldInfo fieldLoader = baseType.GetField("loader", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (fieldLoader != null)
                            {

                                Type loadType = typeof(RemotePluginLoader<>).MakeGenericType(mi.ReturnType);
                                Type retWraperType = CreateSerializeableProxyType(mi.ReturnType, baseType.GetGenericTypeDefinition().MakeGenericType(targetType), true);
                                var constructorInfo = retWraperType.GetConstructor(new[] { mi.ReturnType, loadType });

                                il.Emit(OpCodes.Ldarg_0);
                                il.Emit(OpCodes.Ldfld, fieldLoader);
                                il.Emit(OpCodes.Newobj, constructorInfo);
                            }
                        }
                    }

                    //if (mi.ReturnType == typeof(void))
                    //{
                    //    il.Emit(OpCodes.Pop);
                    //}

                    
                    il.Emit(OpCodes.Ret);

                }

                map.Add(mi, methodBuilder);

            });

            List<PropertyInfo> props = new List<PropertyInfo>();

            props.AddRange(targetType.GetProperties());
            foreach (var tt in targetType.GetInterfaces())
            {
                props.AddRange(tt.GetProperties());
            }
            //var props = targetType.GetProperties();
            props.ToList().ForEach(pi =>
            {
                var propBuilder = typeBuilder.DefineProperty(pi.Name, PropertyAttributes.HasDefault, pi.ReflectedType, null);

                if (pi.CanRead)
                {
                    var mi = pi.GetGetMethod();
                    Debug.Assert(map.ContainsKey(mi));

                    var builder = map[mi];

                    propBuilder.SetGetMethod(builder);

                }

                if (pi.CanWrite)
                {
                    var mi = pi.GetSetMethod();
                    Debug.Assert(map.ContainsKey(mi));

                    var builder = map[mi];

                    propBuilder.SetSetMethod(builder);

                }


            });



            var ret = typeBuilder.CreateType();


#if EMITSAVE
            locker.WaitOne(5000);
            try
            {
                assemblyBuilder.Save(string.Concat( typeName, "_", DateTime.Now.ToString("yyyyMMddHHmmss"),Guid.NewGuid().ToString("N"), ".dll"));
            }
            catch (Exception ex)
            {
                log.Error(" assemblyBuilder.Save Error", ex);
            }
            finally
            {
                locker.ReleaseMutex();
            }
#endif        

            dynamicAssembly.TryAdd(ret.Assembly.FullName, ret.Assembly);
            dynamicTypes.TryAdd(ret.Assembly.FullName, ret);
            return ret;

        }



    }

    /// <summary>
    /// 表示此参数，需要调用RemoteActionProxy OR RemoteFuncProxy
    /// </summary>
    public class CallerDomainRun : Attribute
    {

    }
}
