using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DragonFrame.Models;
using XamlAnimatedGif;
namespace DragonFrame;
public partial class MainWindow : Window
{
    internal readonly AppSettings Settings;
    readonly DispatcherTimer slideshow = new();
    readonly DispatcherTimer resizeDebounce = new() { Interval = TimeSpan.FromMilliseconds(250) };
    readonly DispatcherTimer folderDebounce = new() { Interval = TimeSpan.FromMilliseconds(700) };
    TrayIcon? tray;
    FileSystemWatcher? folderWatcher;
    string? watchedFolder;

    bool settingsOpen;
    SettingsWindow? settingsDialog;
    DateTimeOffset? nextChangeAt;
    MenuItem? lockMenu, trayLockMenu, audioMenu, trayAudioMenu;
    readonly bool testMode;
    readonly bool headless;
    string[] files = Array.Empty<string>();
    readonly ShufflePlaylist playlist = new();
    internal string? CurrentFile;
    bool closed;
    internal bool IsClosing=>closed;
    int sourcePixelWidth,sourcePixelHeight;
    internal int LoadCount;
    internal readonly List<string> Failures = new();
    internal readonly List<WeakReference> OldAnimators = new();
    internal MainWindow(string[] args)
    {
        testMode = args.Contains("--test"); headless=testMode && args.Contains("--headless");
        Settings = testMode ? new AppSettings() : AppSettings.Load();
        if(testMode) ShowActivated=false;
        string? option(string key) { int i = Array.IndexOf(args,key); return i >= 0 && i+1 < args.Length ? args[i+1] : null; }
        if (testMode && option("--folder") is string folder) Settings.Folder = folder;
        if (testMode) { Settings.MediaMode=option("--video-folder")!=null ? (option("--folder")!=null ? "Mixed" : "Videos") : "Images"; Settings.VideoFolder=option("--video-folder")??""; }
        if (testMode && option("--media-mode") is string testMediaMode && Array.IndexOf(AppSettings.MediaModes,testMediaMode)>=0)Settings.MediaMode=testMediaMode;
        if (testMode && double.TryParse(option("--video-speed"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double testSpeed)) Settings.VideoSpeed=testSpeed;
        if(testMode && args.Contains("--audio-on"))Settings.VideoAudioEnabled=true;
        if(testMode && double.TryParse(option("--audio-volume"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double testVolume))Settings.VideoVolume=Math.Clamp(testVolume,0,1);
        InitializeComponent();
        if(args.Contains("--ui")) ShowInTaskbar=true;
        Width=Settings.Width; Height=Settings.Height;
        var area=SystemParameters.WorkArea;
        Left=Settings.Left is double x && double.IsFinite(x) ? Math.Clamp(x,SystemParameters.VirtualScreenLeft,Math.Max(SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth-Width)) : area.Right-Width-20;
        Top=Settings.Top is double y && double.IsFinite(y) ? Math.Clamp(y,SystemParameters.VirtualScreenTop,Math.Max(SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight-Height)) : area.Top+100;
        if(testMode && (args.Contains("--monitor") || args.Contains("--monitor-small")))
        {
            Width=args.Contains("--monitor-small")?200:450; Height=args.Contains("--monitor-small")?250:300; Settings.Width=Width; Settings.Height=Height;
            Left=area.Left+12; Top=Math.Max(area.Top,area.Bottom-Height-12);
            Settings.IsLocked=true;
        }
        if(headless) { Opacity=0; ShowActivated=false; Left=-10000; Top=-10000; }
        var menu = new ContextMenu { Style=(Style)FrameRoot.Resources["FrameContextMenu"] };
        MenuItem add(ContextMenu target,string text, Action action) { var item=new MenuItem { Header=text, Style=(Style)FrameRoot.Resources["FrameMenuItem"] }; item.Click+=(_,_)=>action(); target.Items.Add(item); return item; }
        previousMenus.Add(add(menu,"上一张",Previous)); add(menu,"下一张",Next); detailMenus.Add(add(menu,"看大图（双击）",OpenDetail)); add(menu,"这张不再轮播（保留文件）",ExcludeCurrent); undoExcludeMenus.Add(add(menu,"撤销上次不再轮播",UndoLastExclude)); audioMenu=add(menu,"播放视频声音",ToggleVideoAudio);audioMenu.IsCheckable=true; add(menu,"设置…",OpenSettings); lockMenu=add(menu,"锁定（鼠标穿透）",()=>SetFrameLocked(!Settings.IsLocked)); add(menu,"隐藏（托盘双击恢复）",ToggleVisible); add(menu,"退出",Close);
        FrameRoot.ContextMenu=menu;
        var trayMenu=new ContextMenu { Style=(Style)FrameRoot.Resources["FrameContextMenu"] };
        add(trayMenu,"显示 / 隐藏",ToggleVisible); previousMenus.Add(add(trayMenu,"上一张",Previous)); add(trayMenu,"下一张",Next); detailMenus.Add(add(trayMenu,"看大图",OpenDetail)); add(trayMenu,"这张不再轮播（保留文件）",ExcludeCurrent); undoExcludeMenus.Add(add(trayMenu,"撤销上次不再轮播",UndoLastExclude)); trayAudioMenu=add(trayMenu,"播放视频声音",ToggleVideoAudio);trayAudioMenu.IsCheckable=true; add(trayMenu,"设置…",OpenSettings);
        trayLockMenu=add(trayMenu,"锁定（鼠标穿透）",()=>SetFrameLocked(!Settings.IsLocked)); add(trayMenu,"退出",Close);
        SourceInitialized+=(_,_)=> { if(!headless) tray=new TrayIcon(this,trayMenu,TrayDoubleClick); ApplyNativeLock(Settings.IsLocked); };
        slideshow.Tick+=SlideshowTick;
        resizeDebounce.Tick+=(_,_)=> { resizeDebounce.Stop(); if(IsVisible && CurrentFile!=null && !IsGif(CurrentFile) && !IsVideo(CurrentFile)) LoadFile(CurrentFile); SaveSettings(); };
        folderDebounce.Tick+=FolderDebounceTick;
        AnimationBehavior.SetRepeatBehavior(Picture,RepeatBehavior.Forever);
        AnimationBehavior.AddErrorHandler(Picture,AnimationError);
        AnimationBehavior.AddLoadedHandler(Picture,GifLoaded);
        UpdateAudioControls();
        Loaded+=async (_,_)=> { RefreshFolder(); Next(); UpdateLockLabels(); if(testMode) await RunTestAsync(args); };
        IsVisibleChanged+=(_,_)=>
        {
            if(!IsVisible) { slideshow.Stop(); nextChangeAt=null; resizeDebounce.Stop(); ReleaseImage(); }
            else if(IsLoaded && !closed) { RefreshFolder(); if(CurrentFile!=null && files.Contains(CurrentFile,StringComparer.OrdinalIgnoreCase)) LoadFile(CurrentFile); else Next(); StartSlideshow(); }
        };
    }
    static bool IsGif(string path) => Path.GetExtension(path).Equals(".gif",StringComparison.OrdinalIgnoreCase);
    static bool IsVideo(string path) => Path.GetExtension(path).Equals(".mp4",StringComparison.OrdinalIgnoreCase);
    static bool Supported(string path) => new[]{".jpg",".jpeg",".png",".gif"}.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase);
    static bool SupportedMedia(string path) => Supported(path) || IsVideo(path);
    internal void RefreshFolder()
    {
        string folderKey=Settings.MediaMode+"|"+Settings.Folder+"|"+(Settings.MediaMode!="Images"?Settings.VideoFolder:"");
        if(!string.Equals(playlistFolder,folderKey,StringComparison.OrdinalIgnoreCase))
        {playlistFolder=folderKey;history.Clear();failedFiles.Clear();}
        try
        {
            var excluded=new HashSet<string>(Settings.ExcludedFiles,StringComparer.OrdinalIgnoreCase);
            var images=Settings.MediaMode!="Videos" ? Directory.EnumerateFiles(Settings.Folder).Where(Supported) : Enumerable.Empty<string>();
            var videos=Settings.MediaMode!="Images" && Directory.Exists(Settings.VideoFolder) ? Directory.EnumerateFiles(Settings.VideoFolder).Where(IsVideo) : Enumerable.Empty<string>();
            var present=images.Concat(videos).ToArray();
            var presentSet=new HashSet<string>(present,StringComparer.OrdinalIgnoreCase);
            foreach(string stale in failedFiles.Keys.Where(p=>!presentSet.Contains(p)).ToArray())failedFiles.Remove(stale);
            files=present.Where(p=>!excluded.Contains(p) && !FailedUnchanged(p)).ToArray();if(Settings.MediaMode!="Videos")EnsureFolderWatcher();else StopFolderWatcher();EnsureVideoWatcher();
        }
        catch(Exception ex) { files=Array.Empty<string>(); StopFolderWatcher(); StopVideoWatcher(); ShowError("无法读取媒体目录："+ex.Message); }
        playlist.Synchronize(Settings.Folder,files);
        history.Prune(new HashSet<string>(files,StringComparer.OrdinalIgnoreCase));
        StartSlideshow(false); UpdateTimingText();
    }
    void StartSlideshow(bool restart=true)
    {
        var desiredInterval=TimeSpan.FromSeconds(Settings.IntervalSeconds);
        if(slideshow.Interval!=desiredInterval) slideshow.Interval=desiredInterval;
        if(!IsVisible || files.Length<=1 || closed || settingsOpen || detailOpen || (CurrentFile!=null && IsVideo(CurrentFile))) { slideshow.Stop(); nextChangeAt=null; }
        else if(restart || !slideshow.IsEnabled)
        {
            slideshow.Stop(); slideshow.Interval=TimeSpan.FromSeconds(Settings.IntervalSeconds);
            nextChangeAt=DateTimeOffset.Now.AddSeconds(Settings.IntervalSeconds); slideshow.Start();
        }
        UpdateTimingText();
    }
    void SlideshowTick(object? sender,EventArgs e)=>Next();
    internal void Next()
    {
        // Also refresh at an actual change to recover missed watcher notifications; no extra polling timer.
        RefreshFolder();
        if(files.Length==0) { ReleaseImage(); CurrentFile=null; Status.Text="当前没有可播放的图片或视频。\n放入新文件会自动读取；鼠标移入后点 ⚙ 设置。"; Status.Visibility=Visibility.Visible; StartSlideshow(); return; }
        while(history.CanForward)
        {
            string path=history.Forward()!;if(LoadFile(path)){StartSlideshow();return;}
        }
        for(int i=0;i<files.Length;i++)
        {
            string? candidate=playlist.TakeNext(CurrentFile);
            if(candidate!=null && files.Length>1 && string.Equals(candidate,CurrentFile,StringComparison.OrdinalIgnoreCase)) continue;
            if(candidate!=null && LoadFile(candidate)) { history.Record(candidate);StartSlideshow(); return; }
        }
        StartSlideshow();
    }
    internal bool LoadFile(string path)
    {
        ReleaseImage(); CurrentFile=path;
        if(closed) return false;
        if(!IsVisible) return true; // Queue the selected filename until shown; never decode while hidden.
        try
        {
            if(IsVideo(path)) StartVideo(path);
            else if(IsGif(path)) AnimationBehavior.SetSourceUri(Picture,new Uri(Path.GetFullPath(path)));
            else
            {
                using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
                var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);
                int sourceW=decoder.Frames[0].PixelWidth, sourceH=decoder.Frames[0].PixelHeight;
                sourcePixelWidth=sourceW; sourcePixelHeight=sourceH; UpdatePictureLayout();
                var dpi=VisualTreeHelper.GetDpi(this);
                double scale=Math.Min(1,Math.Min(Picture.Width*dpi.DpiScaleX/sourceW,Picture.Height*dpi.DpiScaleY/sourceH));
                stream.Position=0;
                var image=new BitmapImage(); image.BeginInit(); image.StreamSource=stream; image.CacheOption=BitmapCacheOption.OnLoad;
                image.DecodePixelWidth=Math.Max(1,(int)Math.Ceiling(sourceW*scale)); image.EndInit(); image.Freeze(); Picture.Source=image;
            }
            Status.Visibility=Visibility.Collapsed; LoadCount++; UpdateTimingText(); return true;
        }
        catch(Exception ex) { MarkFailed(path);ShowError(Path.GetFileName(path)+"："+ex.Message); return false; }
    }
    void GifLoaded(object sender,RoutedEventArgs e)
    {
        if(Picture.Source is BitmapSource bitmap) { sourcePixelWidth=bitmap.PixelWidth; sourcePixelHeight=bitmap.PixelHeight; UpdatePictureLayout(); }
    }
    void UpdatePictureLayout()
    {
        if(Picture==null || sourcePixelWidth<=0 || sourcePixelHeight<=0) return;
        var dpi=VisualTreeHelper.GetDpi(this);
        var fit=FitPicture(sourcePixelWidth/dpi.DpiScaleX,sourcePixelHeight/dpi.DpiScaleY,Width,Height,Settings.EnlargeSmallImages);
        Picture.Width=fit.width; Picture.Height=fit.height;
        Picture.HorizontalAlignment=Settings.ImageAnchor.EndsWith("Right",StringComparison.Ordinal) ? HorizontalAlignment.Right : Settings.ImageAnchor.EndsWith("Left",StringComparison.Ordinal) ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        Picture.VerticalAlignment=Settings.ImageAnchor.StartsWith("Top",StringComparison.Ordinal) ? VerticalAlignment.Top : Settings.ImageAnchor.StartsWith("Bottom",StringComparison.Ordinal) ? VerticalAlignment.Bottom : VerticalAlignment.Center;
    }
    void UpdateVideoLayout()
    {
        if(VideoPlayer.NaturalVideoWidth<=0 || VideoPlayer.NaturalVideoHeight<=0) return;
        var fit=FitPicture(VideoPlayer.NaturalVideoWidth,VideoPlayer.NaturalVideoHeight,Width,Height,true);
        VideoPlayer.Width=fit.width; VideoPlayer.Height=fit.height;
        VideoPlayer.HorizontalAlignment=Settings.ImageAnchor.EndsWith("Right",StringComparison.Ordinal) ? HorizontalAlignment.Right : Settings.ImageAnchor.EndsWith("Left",StringComparison.Ordinal) ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        VideoPlayer.VerticalAlignment=Settings.ImageAnchor.StartsWith("Top",StringComparison.Ordinal) ? VerticalAlignment.Top : Settings.ImageAnchor.StartsWith("Bottom",StringComparison.Ordinal) ? VerticalAlignment.Bottom : VerticalAlignment.Center;
    }
    internal static (double width,double height) FitPicture(double sourceWidth,double sourceHeight,double frameWidth,double frameHeight,bool enlarge)
    {
        double factor=Math.Min(frameWidth/sourceWidth,frameHeight/sourceHeight);
        if(!enlarge) factor=Math.Min(1,factor);
        return (Math.Max(1,sourceWidth*factor),Math.Max(1,sourceHeight*factor));
    }
    void FrameSizeChanged(object sender,SizeChangedEventArgs e)
    {
        UpdatePictureLayout(); UpdateVideoLayout(); if(TimingText!=null) UpdateTimingText();
    }
    internal void ReleaseImage()
    {
        ReleaseVideo();
        var animator=AnimationBehavior.GetAnimator(Picture);
        if(testMode && animator!=null) OldAnimators.Add(new WeakReference(animator));
        AnimationBehavior.SetSourceUri(Picture,null!); Picture.Source=null; sourcePixelWidth=sourcePixelHeight=0;
    }
    void AnimationError(DependencyObject sender,AnimationErrorEventArgs e) {ShowError("GIF 无法播放："+Path.GetFileName(CurrentFile)+" "+e.Exception.Message);RecoverAnimationError();}
    void ShowError(string text) { if(Failures.Count==100) Failures.RemoveAt(0); Failures.Add(text); App.Log(text); Status.Text=text; Status.Visibility=Visibility.Visible; }
    void DragFrame(object sender,MouseButtonEventArgs e)
    {
        if(Settings.IsLocked) return;
        // Never begin moving the frame from toolbar buttons, text boxes or a resize handle.
        for(DependencyObject? node=e.OriginalSource as DependencyObject;node!=null;node=VisualTreeHelper.GetParent(node)) if(node is ButtonBase || node is Thumb) return;
        if(e.ClickCount==2){e.Handled=true;OpenDetail();return;}
        if(e.LeftButton==MouseButtonState.Pressed) { DragMove(); SaveSettings(); }
    }
    void ResizeFrame(object sender,DragDeltaEventArgs e)
    {
        if(Settings.IsLocked) return;
        Width=Math.Clamp(Width+e.HorizontalChange,MinWidth,MaxWidth); Height=Math.Clamp(Height+e.VerticalChange,MinHeight,MaxHeight); UpdateTimingText();
    }
    void ResizeFinished(object sender,DragCompletedEventArgs e) { resizeDebounce.Stop(); resizeDebounce.Start(); }
    void FrameEnter(object sender,MouseEventArgs e)=>SetChromeVisible(true);
    void FrameLeave(object sender,MouseEventArgs e)=>SetChromeVisible(false);
    internal void SetChromeVisible(bool hovering)
    {
        var visibility=(hovering || settingsOpen) && !Settings.IsLocked ? Visibility.Visible : Visibility.Collapsed;
        HoverToolbar.Visibility=visibility; HoverInfo.Visibility=visibility; ResizeGrip.Visibility=visibility; FrameOutline.Visibility=visibility; UpdateTimingText();
    }
    void UpdateTimingText()
    {
        string interval=Settings.IntervalSeconds%60==0 ? (Settings.IntervalSeconds/60)+"分钟" : Settings.IntervalSeconds+"秒";
        TimingText.Text=$"{Width:0}×{Height:0} · "+(settingsOpen ? "设置时暂停轮播" : CurrentFile!=null && IsVideo(CurrentFile) ? "视频播完切换 · "+files.Length+"项" : files.Length<=1 ? files.Length+"项 · 新文件自动加入" : interval+"/张 · 下张 "+(nextChangeAt?.ToString("HH:mm:ss")??"暂停")+" · "+files.Length+"项");
        TimingText.ToolTip=CurrentFile==null ? null : Path.GetFileName(CurrentFile);
        PreviousButton.IsEnabled=history.CanBack;foreach(var item in previousMenus)item.IsEnabled=history.CanBack;foreach(var item in undoExcludeMenus)item.IsEnabled=Settings.ExcludedFiles.Count>0;
        foreach(var item in detailMenus)item.IsEnabled=CurrentFile!=null && !IsVideo(CurrentFile);
        NextButton.ToolTip="下一张（重新计时）\n"+TimingText.Text;
    }
    void NextClicked(object sender,RoutedEventArgs e) { e.Handled=true; Next(); }
    void SettingsClicked(object sender,RoutedEventArgs e) { e.Handled=true; OpenSettings(); }
    void LockClicked(object sender,RoutedEventArgs e) { e.Handled=true; SetFrameLocked(true); }
    void HideClicked(object sender,RoutedEventArgs e) { e.Handled=true; ToggleVisible(); }
    internal void ToggleVisible() { if(IsVisible) Hide(); else Show(); }
    internal void ApplySettings(string folder,int interval,double? width=null,double? height=null,bool? enlarge=null,string? anchor=null,string? videoFolder=null,string? mediaMode=null,double? videoSpeed=null,bool? videoAudioEnabled=null,double? videoVolume=null)
    {
        bool folderChanged=!string.Equals(Settings.Folder,folder,StringComparison.OrdinalIgnoreCase);
        bool sizingChanged=(width.HasValue && width!=Settings.Width)||(height.HasValue && height!=Settings.Height)||(enlarge.HasValue && enlarge!=Settings.EnlargeSmallImages);
        Settings.Folder=folder; Settings.IntervalSeconds=Math.Clamp(interval,1,86400);
        if(videoFolder!=null) Settings.VideoFolder=videoFolder;
        if(mediaMode!=null && Array.IndexOf(AppSettings.MediaModes,mediaMode)>=0) Settings.MediaMode=mediaMode;
        if(videoSpeed.HasValue) Settings.VideoSpeed=videoSpeed.Value;
        if(videoAudioEnabled.HasValue) Settings.VideoAudioEnabled=videoAudioEnabled.Value;
        if(videoVolume.HasValue) Settings.VideoVolume=Math.Clamp(videoVolume.Value,0,1);
        if(width.HasValue) Settings.Width=Math.Clamp(width.Value,100,800);
        if(height.HasValue) Settings.Height=Math.Clamp(height.Value,80,600);
        if(enlarge.HasValue) Settings.EnlargeSmallImages=enlarge.Value;
        if(anchor!=null && Array.IndexOf(AppSettings.Anchors,anchor)>=0) Settings.ImageAnchor=anchor;
        // The frame's position never changes on image load, alignment changes, or settings save.
        Width=Settings.Width; Height=Settings.Height; UpdatePictureLayout(); UpdateVideoLayout(); RefreshFolder();
        if(folderChanged || CurrentFile==null || !files.Contains(CurrentFile,StringComparer.OrdinalIgnoreCase)) Next();
        else if(sizingChanged && !IsGif(CurrentFile) && !IsVideo(CurrentFile)) LoadFile(CurrentFile);
        if(CurrentFile!=null && IsVideo(CurrentFile)) VideoPlayer.SpeedRatio=Settings.VideoSpeed;
        ApplyVideoAudio();UpdateAudioControls();
        StartSlideshow(); SaveSettings();
    }
    internal void OpenSettings()
    {
        if(settingsDialog!=null) { settingsDialog.Activate(); return; }
        double previousWidth=Width, previousHeight=Height;
        settingsOpen=true; StartSlideshow(); SetChromeVisible(true);
        var dialog=new SettingsWindow(Settings) { Owner=this }; settingsDialog=dialog;
        dialog.PreviewSizeRequested+=PreviewFrameSize;
        if(headless) { dialog.Opacity=0; dialog.ShowActivated=false; dialog.WindowStartupLocation=WindowStartupLocation.Manual; dialog.Left=-10000; dialog.Top=-10000; }
        bool saved=false;
        try
        {
            saved=dialog.ShowDialog()==true; settingsOpen=false;
            if(saved) {if(dialog.RestoreExcludedRequested)Settings.ExcludedFiles.Clear(); ApplySettings(dialog.SelectedFolder,dialog.SelectedInterval,dialog.SelectedWidth,dialog.SelectedHeight,dialog.SelectedEnlarge,dialog.SelectedAnchor,dialog.SelectedVideoFolder,dialog.SelectedMediaMode,dialog.SelectedVideoSpeed,dialog.SelectedVideoAudioEnabled,dialog.SelectedVideoVolume);}
            else { RestoreFrameSize(previousWidth,previousHeight); StartSlideshow(); }
        }
        finally { settingsOpen=false; settingsDialog=null; SetChromeVisible(IsMouseOver); if(CurrentFile!=null && FailedUnchanged(CurrentFile) && IsVisible)Next();else StartSlideshow(false); }
    }
    internal void PreviewFrameSize(double width,double height)
    {
        Width=width; Height=height; UpdatePictureLayout(); SetChromeVisible(true);
    }
    internal void RestoreFrameSize(double width,double height)
    {
        Width=width; Height=height; UpdatePictureLayout();
    }
    void SaveSettings()
    {
        if(testMode) return;
        if(!settingsOpen){Settings.Width=Width;Settings.Height=Height;}
        Settings.Left=Left; Settings.Top=Top;
        try { Settings.Save(); } catch(Exception ex) { ShowError("设置保存失败，请将程序放在可写目录："+ex.Message); }
    }
    protected override void OnClosed(EventArgs e)
    {
        closed=true; slideshow.Stop(); resizeDebounce.Stop(); folderDebounce.Stop(); slideshow.Tick-=SlideshowTick; StopFolderWatcher();
        ReleaseImage(); StopVideoWatcher(); AnimationBehavior.RemoveErrorHandler(Picture,AnimationError); AnimationBehavior.RemoveLoadedHandler(Picture,GifLoaded);
        tray?.Dispose(); SaveSettings(); base.OnClosed(e);
    }
}



