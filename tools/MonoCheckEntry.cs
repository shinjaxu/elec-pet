using System;
using System.IO;
using System.Reflection;
using System.Threading;

// 此入口由 Mono 执行，确保线程 apartment 由 Mono 建立，而非继承 CLR 宿主的主线程。
public static class MonoCheckEntry
{
    public static int Main(string[] args)
    {
        int result = 1;
        try
        {
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var entry = assembly.EntryPoint;
            if (entry == null) throw new InvalidOperationException("检查程序没有 Main 入口。");
            string[] forwarded = new string[args.Length - 1];
            Array.Copy(args, 1, forwarded, 0, forwarded.Length);
            var apartment = entry.IsDefined(typeof(STAThreadAttribute), false) ? ApartmentState.STA : ApartmentState.MTA;
            var thread = new Thread(() =>
            {
                try
                {
                    Console.WriteLine("MONO pointerSize={0}; apartment={1}", IntPtr.Size, Thread.CurrentThread.GetApartmentState());
                    object returned = entry.Invoke(null, entry.GetParameters().Length == 0 ? null : new object[] { forwarded });
                    result = returned is int ? (int)returned : 0;
                }
                catch (Exception exception)
                { Console.Error.WriteLine(exception is TargetInvocationException ? exception.InnerException : exception); }
            });
            thread.SetApartmentState(apartment);
            thread.Start(); thread.Join();
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); }
        return result;
    }
}
