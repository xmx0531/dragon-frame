using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
namespace DragonFrame;
public partial class MainWindow
{
    readonly NavigationHistory history=new();
    readonly Dictionary<string,(long length,long ticks)> failedFiles=new(StringComparer.OrdinalIgnoreCase);
    string? playlistFolder;
    bool detailOpen;
    readonly List<MenuItem> previousMenus=new(),undoExcludeMenus=new(),detailMenus=new();
    static (long length,long ticks) FileStamp(string path)
    { try {var info=new FileInfo(path);return (info.Length,info.LastWriteTimeUtc.Ticks);}catch{return (-1,0);} }
    bool FailedUnchanged(string path)
    {
        if(!failedFiles.TryGetValue(path,out var stamp)) return false;
        if(stamp==FileStamp(path)) return true;
        failedFiles.Remove(path);return false;
    }
    void MarkFailed(string path)=>failedFiles[path]=FileStamp(path);
    internal void Previous()
    {
        string? original=CurrentFile;RefreshFolder();
        while(history.CanBack)
        {
            string path=history.Back()!;
            if(LoadFile(path)){StartSlideshow();return;}
        }
        if(original!=null && files.Contains(original,StringComparer.OrdinalIgnoreCase) && !FailedUnchanged(original))
        {LoadFile(original);history.SelectCurrent(original);StartSlideshow();}
        UpdateTimingText();
    }
    void PreviousClicked(object sender,RoutedEventArgs e){e.Handled=true;Previous();}
    internal void ExcludeCurrent()
    {
        if(CurrentFile==null) return;
        if(!Settings.ExcludedFiles.Contains(CurrentFile,StringComparer.OrdinalIgnoreCase)) Settings.ExcludedFiles.Add(CurrentFile);
        SaveSettings();Next();
    }
    internal void UndoLastExclude()
    {
        if(Settings.ExcludedFiles.Count==0)return;
        Settings.ExcludedFiles.RemoveAt(Settings.ExcludedFiles.Count-1);SaveSettings();RefreshFolder();
        if(Picture.Source==null && VideoPlayer.Source==null && IsVisible)Next();
    }
    internal void RestoreAllExcluded()
    {Settings.ExcludedFiles.Clear();SaveSettings();RefreshFolder();if(Picture.Source==null && VideoPlayer.Source==null && IsVisible)Next();}
    void RecoverAnimationError()
    {
        string? failed=CurrentFile;if(failed==null)return;
        MarkFailed(failed);ReleaseImage();
        Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>
        {if(!closed && IsVisible && !settingsOpen && !detailOpen && CurrentFile==failed)Next();}));
    }
    internal DetailWindow CreateDetailWindow()=>new(this,CurrentFile!,IsGif(CurrentFile!));
    internal void OpenDetail()
    {
        if(CurrentFile==null || !File.Exists(CurrentFile) || detailOpen || IsVideo(CurrentFile))return;
        detailOpen=true;StartSlideshow();
        try
        {
            var dialog=CreateDetailWindow();dialog.Owner=this;
            if(headless){dialog.Opacity=0;dialog.ShowActivated=false;dialog.WindowStartupLocation=WindowStartupLocation.Manual;dialog.Left=-10000;dialog.Top=-10000;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};timer.Tick+=(_,_)=>{timer.Stop();dialog.Close();};timer.Start();}
            dialog.ShowDialog();
        }
        catch(Exception ex){ShowError("无法打开大图："+ex.Message);}
        finally{detailOpen=false;if(CurrentFile!=null && FailedUnchanged(CurrentFile))Next();else StartSlideshow();}
    }
}
