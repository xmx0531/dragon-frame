using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
namespace DragonFrame;
public partial class App : System.Windows.Application
{
    InstanceGate? gate;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(Array.IndexOf(e.Args,"--test")<0)
        {
            string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName+"|"+AppContext.BaseDirectory.ToUpperInvariant()))).Substring(0,24);
            gate=new InstanceGate(key,()=>Dispatcher.BeginInvoke(new Action(()=>
            {if(!Dispatcher.HasShutdownStarted && MainWindow is MainWindow window && window.IsLoaded && !window.IsClosing){if(!window.IsVisible)window.Show();window.WindowState=WindowState.Normal;window.Activate();}})));
            if(!gate.IsOwner){Shutdown();return;}
        }
        if (Array.IndexOf(e.Args,"--hardware") < 0) System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var window = new MainWindow(e.Args);
        MainWindow = window;
        window.Show();
    }
    protected override void OnExit(ExitEventArgs e){gate?.Dispose();gate=null;base.OnExit(e);}
    internal static void Log(string text)
    {
        try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "errors.log"), DateTime.Now.ToString("s") + " " + text + Environment.NewLine); } catch { }
    }
}


