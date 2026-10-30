using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
namespace DragonFrame;
// Shell tray icon without loading Windows Forms / System.Drawing into the steady-state player.
internal sealed class TrayIcon : IDisposable
{
    const int Callback=0x8000+17;
    readonly HwndSource source;
    readonly Action toggle;
    readonly ContextMenu menu;
    readonly uint taskbarCreated;
    readonly IntPtr ownedIcon;
    NotifyIconData data;
    bool disposed;
    internal TrayIcon(Window owner,ContextMenu menu,Action toggle)
    {
        this.menu=menu; this.toggle=toggle;
        source=HwndSource.FromHwnd(new WindowInteropHelper(owner).Handle)!;
        taskbarCreated=RegisterWindowMessage("TaskbarCreated");
        string iconPath=Path.Combine(AppContext.BaseDirectory,"dragon.ico");
        if(File.Exists(iconPath)) ownedIcon=LoadImage(IntPtr.Zero,iconPath,1,16,16,0x10);
        data=new NotifyIconData { Size=(uint)Marshal.SizeOf<NotifyIconData>(),Window=source.Handle,Id=1,Flags=1|2|4,CallbackMessage=Callback,Icon=ownedIcon!=IntPtr.Zero ? ownedIcon : LoadIcon(IntPtr.Zero,new IntPtr(32512)),Tip="小龙娘相框",Info="",InfoTitle="" };
        source.AddHook(Hook); Add();
    }
    internal void SetTip(string text) { data.Tip=text; Shell_NotifyIcon(1,ref data); }
    void Add() { if(!Shell_NotifyIcon(0,ref data)) App.Log("托盘图标添加失败"); }
    IntPtr Hook(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if((uint)msg==taskbarCreated) Add();
        if(msg==Callback)
        {
            int mouse=(int)lParam;
            if(mouse==0x203) { toggle(); handled=true; }
            else if(mouse==0x205) { SetForegroundWindow(source.Handle); menu.Placement=PlacementMode.MousePoint; menu.IsOpen=true; handled=true; }
        }
        return IntPtr.Zero;
    }
    public void Dispose() { if(disposed) return; disposed=true; Shell_NotifyIcon(2,ref data); source.RemoveHook(Hook); if(ownedIcon!=IntPtr.Zero) DestroyIcon(ownedIcon); }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct NotifyIconData
    {
        public uint Size; public IntPtr Window; public uint Id,Flags; public int CallbackMessage; public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Tip;
        public uint State,StateMask;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)] public string Info;
        public uint Timeout;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool Shell_NotifyIcon(uint message,ref NotifyIconData data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr LoadIcon(IntPtr instance,IntPtr name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr LoadImage(IntPtr instance,string path,uint type,int width,int height,uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
}

