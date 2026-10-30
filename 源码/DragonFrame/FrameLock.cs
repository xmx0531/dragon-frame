using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace DragonFrame;
public partial class MainWindow
{
    const int ExStyleIndex=-20;
    const long TransparentStyle=0x20;
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW",SetLastError=true)] static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)] static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
    internal bool NativeClickThrough => (GetWindowLongPtr(new WindowInteropHelper(this).Handle,ExStyleIndex).ToInt64()&TransparentStyle)!=0;
    void ApplyNativeLock(bool locked)
    {
        var handle=new WindowInteropHelper(this).Handle; if(handle==IntPtr.Zero) return;
        long style=GetWindowLongPtr(handle,ExStyleIndex).ToInt64();
        SetWindowLongPtr(handle,ExStyleIndex,new IntPtr(locked ? style|TransparentStyle : style&~TransparentStyle));
        if(NativeClickThrough!=locked) { Settings.IsLocked=false; ShowError("无法切换锁定状态："+new Win32Exception(Marshal.GetLastWin32Error()).Message); }
    }
    internal void SetFrameLocked(bool locked)
    {
        Settings.IsLocked=locked; ApplyNativeLock(locked); UpdateLockLabels(); SetChromeVisible(IsMouseOver); SaveSettings();
    }
    void UpdateLockLabels()
    {
        string label=Settings.IsLocked ? "解锁相框（恢复鼠标操作）" : "锁定相框（鼠标穿透）";
        if(lockMenu!=null) lockMenu.Header=label;
        if(trayLockMenu!=null) trayLockMenu.Header=label;
        tray?.SetTip(Settings.IsLocked ? "小龙娘相框 · 已锁定，双击解锁" : "小龙娘相框 · 双击显示 / 隐藏");
    }
    internal void TrayDoubleClick()
    {
        if(Settings.IsLocked) { SetFrameLocked(false); if(!IsVisible) Show(); }
        else ToggleVisible();
    }
}
