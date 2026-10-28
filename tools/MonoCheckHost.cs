using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

// 仅供独立验证使用：CLR 宿主只通过原生导出进入构建包的 x64 Mono，不加载 UnityPlayer。
public static class MonoCheckHost
{
    public static int Main(string[] args)
    {
        if (args.Length < 4 || IntPtr.Size != 8)
        {
            Console.Error.WriteLine("用法：MonoCheckHost <x64运行库> <Mono库根目录> <Mono配置目录> <检查exe> [检查参数]");
            return 2;
        }
        try
        {
            string runtime = Path.GetFullPath(args[0]);
            if (!SetDllDirectory(Path.GetDirectoryName(runtime))) throw new Win32Exception();
            IntPtr library = LoadLibrary(runtime);
            if (library == IntPtr.Zero) throw new Win32Exception();
            var setDirs = (SetDirs)Export(library, "mono_set_dirs", typeof(SetDirs));
            var parseConfig = (ParseConfig)Export(library, "mono_config_parse", typeof(ParseConfig));
            var initialize = (Initialize)Export(library, "mono_jit_init_version", typeof(Initialize));
            var openAssembly = (OpenAssembly)Export(library, "mono_domain_assembly_open", typeof(OpenAssembly));
            var execute = (Execute)Export(library, "mono_jit_exec", typeof(Execute));
            var cleanup = (Cleanup)Export(library, "mono_jit_cleanup", typeof(Cleanup));
            Console.WriteLine("HOST pointerSize={0}; runtime={1}", IntPtr.Size, runtime);
            setDirs(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
            parseConfig(IntPtr.Zero);
            IntPtr domain = initialize("ElecPet.X64Checks", "v4.0.30319");
            if (domain == IntPtr.Zero) throw new InvalidOperationException("x64 Mono 初始化失败。");
            try
            {
                string assemblyPath = Path.GetFullPath(args[3]);
                IntPtr assembly = openAssembly(domain, assemblyPath);
                if (assembly == IntPtr.Zero) throw new InvalidOperationException("无法加载检查程序：" + assemblyPath);
                // mono_jit_exec 的 argv[0] 必须是检查程序自身，后续参数原样交给其 Main。
                string[] arguments = new string[args.Length - 3];
                Array.Copy(args, 3, arguments, 0, arguments.Length);
                arguments[0] = assemblyPath;
                return execute(domain, assembly, arguments.Length, arguments);
            }
            finally { cleanup(domain); }
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }

    private static Delegate Export(IntPtr library, string name, Type type)
    {
        IntPtr address = GetProcAddress(library, name);
        if (address == IntPtr.Zero) throw new EntryPointNotFoundException(name);
        return Marshal.GetDelegateForFunctionPointer(address, type);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate void SetDirs(string assemblies, string configuration);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ParseConfig(IntPtr configuration);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate IntPtr Initialize(string domain, string version);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate IntPtr OpenAssembly(IntPtr domain, string assembly);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate int Execute(IntPtr domain, IntPtr assembly, int count,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr, SizeParamIndex = 2)] string[] arguments);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Cleanup(IntPtr domain);
    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string path);
    [DllImport("kernel32.dll", EntryPoint = "SetDllDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string path);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr library, string name);
}
