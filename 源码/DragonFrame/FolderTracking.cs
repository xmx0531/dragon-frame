using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
namespace DragonFrame;
public partial class MainWindow
{
    int watchEventQueued;
    int watcherNeedsRestart;
    FileSystemWatcher? videoWatcher;
    string? watchedVideoFolder;
    void EnsureFolderWatcher()
    {
        string full=Path.GetFullPath(Settings.Folder);
        if(folderWatcher!=null && string.Equals(watchedFolder,full,StringComparison.OrdinalIgnoreCase)) return;
        StopFolderWatcher();
        var watcher=new FileSystemWatcher(full) { IncludeSubdirectories=false,NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite|NotifyFilters.Size,InternalBufferSize=8192 };
        watcher.Created+=FolderChanged; watcher.Changed+=FolderChanged; watcher.Deleted+=FolderChanged; watcher.Renamed+=FolderRenamed; watcher.Error+=FolderWatchError;
        folderWatcher=watcher; watchedFolder=full; watcher.EnableRaisingEvents=true;
    }
    void StopFolderWatcher()
    {
        if(folderWatcher==null) return;
        folderWatcher.EnableRaisingEvents=false;
        folderWatcher.Created-=FolderChanged; folderWatcher.Changed-=FolderChanged; folderWatcher.Deleted-=FolderChanged; folderWatcher.Renamed-=FolderRenamed; folderWatcher.Error-=FolderWatchError;
        folderWatcher.Dispose(); folderWatcher=null; watchedFolder=null;
    }
    void EnsureVideoWatcher()
    {
        if(Settings.MediaMode=="Images" || !Directory.Exists(Settings.VideoFolder)) {StopVideoWatcher();return;}
        string full=Path.GetFullPath(Settings.VideoFolder);
        if(videoWatcher!=null && string.Equals(watchedVideoFolder,full,StringComparison.OrdinalIgnoreCase)) return;
        StopVideoWatcher();
        var watcher=new FileSystemWatcher(full) {IncludeSubdirectories=false,NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite|NotifyFilters.Size,InternalBufferSize=8192};
        watcher.Created+=FolderChanged;watcher.Changed+=FolderChanged;watcher.Deleted+=FolderChanged;watcher.Renamed+=FolderRenamed;watcher.Error+=FolderWatchError;
        videoWatcher=watcher;watchedVideoFolder=full;watcher.EnableRaisingEvents=true;
    }
    void StopVideoWatcher()
    {
        if(videoWatcher==null)return;
        videoWatcher.EnableRaisingEvents=false;
        videoWatcher.Created-=FolderChanged;videoWatcher.Changed-=FolderChanged;videoWatcher.Deleted-=FolderChanged;videoWatcher.Renamed-=FolderRenamed;videoWatcher.Error-=FolderWatchError;
        videoWatcher.Dispose();videoWatcher=null;watchedVideoFolder=null;
    }
    void FolderChanged(object sender,FileSystemEventArgs e) { if(SupportedMedia(e.FullPath)) QueueFolderRefresh(); }
    void FolderRenamed(object sender,RenamedEventArgs e) { if(SupportedMedia(e.FullPath) || SupportedMedia(e.OldFullPath)) QueueFolderRefresh(); }
    void FolderWatchError(object sender,ErrorEventArgs e) { QueueFolderRefresh(true); }
    void QueueFolderRefresh(bool restart=false)
    {
        if(restart) Interlocked.Exchange(ref watcherNeedsRestart,1);
        if(Interlocked.Exchange(ref watchEventQueued,1)!=0) return;
        Dispatcher.BeginInvoke(new Action(()=>
        {
            Interlocked.Exchange(ref watchEventQueued,0);
            if(closed) return;

            folderDebounce.Stop(); folderDebounce.Start();
        }));
    }
    void FolderDebounceTick(object? sender,EventArgs e)
    {
        folderDebounce.Stop(); if(closed) return;
        if(Interlocked.Exchange(ref watcherNeedsRestart,0)!=0) {StopFolderWatcher();StopVideoWatcher();}
        RefreshFolder();
        if(CurrentFile==null || !files.Contains(CurrentFile,StringComparer.OrdinalIgnoreCase) || (Picture.Source==null && VideoPlayer.Source==null && Status.Visibility==Visibility.Visible)) Next();
        // New images enter the filename list only. Do not change the current image or restart its countdown.
    }
}


