using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XamlAnimatedGif;
namespace DragonFrame;
public partial class MainWindow
{
    internal async Task RunTestAsync(string[] args)
    {
        string option(string key,string fallback) { int i=Array.IndexOf(args,key); return i>=0 && i+1<args.Length ? args[i+1] : fallback; }
        string scenario=option("--scenario","verify"), output=option("--output",AppContext.BaseDirectory);
        Directory.CreateDirectory(output);
        var checks=new Dictionary<string,object?>();
        try
        {
            if(scenario=="settings-preview")
            {
                var dialog=new SettingsWindow(Settings) { Owner=this, WindowStartupLocation=WindowStartupLocation.Manual, Left=-10000, Top=-10000, ShowActivated=false };
                dialog.Show();
                await Task.Delay(100);
                string[] tabs={"内容","外观","切换"};
                for(int i=0;i<tabs.Length;i++)
                {
                    dialog.SettingsTabs.SelectedIndex=i;
                    dialog.UpdateLayout();
                    await Task.Delay(80);
                    var bitmap=new RenderTargetBitmap((int)dialog.ActualWidth,(int)dialog.ActualHeight,96,96,PixelFormats.Pbgra32);
                    bitmap.Render(dialog);
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream=File.Create(Path.Combine(output,"设置-"+tabs[i]+".png"));encoder.Save(stream);
                }
                dialog.Close();
                var menu=FrameRoot.ContextMenu;
                menu.PlacementTarget=FrameRoot;
                menu.IsOpen=true;
                await Task.Delay(100);
                menu.UpdateLayout();
                if(menu.ActualWidth>0 && menu.ActualHeight>0)
                {
                    var menuBitmap=new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth),(int)Math.Ceiling(menu.ActualHeight),96,96,PixelFormats.Pbgra32);
                    menuBitmap.Render(menu);
                    var menuEncoder=new PngBitmapEncoder();menuEncoder.Frames.Add(BitmapFrame.Create(menuBitmap));
                    using var menuFile=File.Create(Path.Combine(output,"菜单预览.png"));menuEncoder.Save(menuFile);
                    checks["menuCaptured"]=true;
                }
                menu.IsOpen=false;
                checks["tabsCaptured"]=tabs.Length;
            }
            else if(scenario=="features")
            {
                await VerifyFeaturesAsync(checks,output);
            }
            else if(scenario=="verify")
            {
                slideshow.Stop();
                checks["renderer"]=System.Windows.Media.RenderOptions.ProcessRenderMode.ToString();
                checks["default300x200"]=Width==300 && Height==200;
                checks["transparentTopmostUniform"]=AllowsTransparency && Topmost && Picture.Stretch==Stretch.Uniform;
                foreach(string file in Directory.GetFiles(Settings.Folder).Where(p=>!IsGif(p) && Supported(p)))
                {
                    bool loaded=LoadFile(file); var image=Picture.Source as BitmapSource;
                    checks["static:"+Path.GetFileName(file)]=new { loaded, width=image?.PixelWidth,height=image?.PixelHeight, withinBounds=image!=null && image.PixelWidth<=Math.Ceiling(ActualWidth*VisualTreeHelper.GetDpi(this).DpiScaleX)+1 && image.PixelHeight<=Math.Ceiling(ActualHeight*VisualTreeHelper.GetDpi(this).DpiScaleY)+1 };
                }
                foreach(string file in Directory.GetFiles(Settings.Folder,"*.gif"))
                {
                    LoadFile(file); await Task.Delay(700);
                    var animator=AnimationBehavior.GetAnimator(Picture);
                    var indices=new HashSet<int>(); var hashes=new HashSet<ulong>(); int wraps=0,last=-1;
                    for(int i=0;i<100;i++)
                    {
                        if(animator!=null) { int index=animator.CurrentFrameIndex; indices.Add(index); if(last>index) wraps++; last=index; var frameHash=HashBitmap((BitmapSource)Picture.Source); if(hashes.Add(frameHash) && Path.GetFileName(file)=="disposal.gif") { var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create((BitmapSource)Picture.Source)); using var fs=File.Create(Path.Combine(output,"disposal-"+frameHash+".png")); png.Save(fs); } }
                        await Task.Delay(50);
                    }
                    checks["gif:"+Path.GetFileName(file)]=new { frames=animator?.FrameCount, distinctIndices=indices.Count,distinctPixelHashes=hashes.Count,wraps,animated=indices.Count>1 && hashes.Count>1,looping=wraps>0 };
                    Capture(Path.Combine(output,Path.GetFileName(file)+".png"));
                }
                var gif=Directory.GetFiles(Settings.Folder,"*.gif").First();
                LoadFile(gif); await Task.Delay(300); Hide(); await Task.Delay(300);
                checks["hiddenReleasesAnimator"]=AnimationBehavior.GetAnimator(Picture)==null && Picture.Source==null && !slideshow.IsEnabled;
                Show(); await Task.Delay(400); checks["showRestartsGIF"]=AnimationBehavior.GetAnimator(Picture)!=null;
                for(int i=0;i<40;i++) { Next(); await Task.Delay(80); }
                ReleaseImage(); await Task.Delay(300); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                checks["oldAnimatorReferences"]=OldAnimators.Count;
                checks["oldAnimatorsAliveAfterCollection"]=OldAnimators.Count(w=>w.IsAlive);
                foreach(int interval in new[]{60,180,300,600}) { Settings.IntervalSeconds=interval; StartSlideshow(); checks["interval:"+interval]=slideshow.Interval.TotalSeconds==interval; }
                string scratch=Path.Combine(output,"empty"); Directory.CreateDirectory(scratch);
                var savedFolder=Settings.Folder; Settings.Folder=scratch; RefreshFolder(); Next(); checks["emptyFolderMessage"]=Status.Visibility==Visibility.Visible && Picture.Source==null;
                Settings.Folder=Path.Combine(output,"does-not-exist"); RefreshFolder(); checks["missingFolderHandled"]=files.Length==0;
                Settings.Folder=savedFolder!; RefreshFolder(); Next();
                var settingsWindow=new SettingsWindow(Settings) { Owner=this }; if(headless) { settingsWindow.Opacity=0; settingsWindow.ShowActivated=false; settingsWindow.WindowStartupLocation=WindowStartupLocation.Manual; settingsWindow.Left=-10000; settingsWindow.Top=-10000; } settingsWindow.Show(); await Task.Delay(200); settingsWindow.Close(); checks["settingsWindowOpens"]=true;
                Capture(Path.Combine(output,"frame.png"));
            }
            else if(scenario=="gallery")
            {
                slideshow.Stop();
                var results=new List<object>();
                foreach(string path in files)
                {
                    bool loaded=LoadFile(path);
                    if(IsGif(path))
                    {
                        for(int i=0;i<100 && AnimationBehavior.GetAnimator(Picture)==null && Status.Visibility!=Visibility.Visible;i++) await Task.Delay(20);
                        loaded=loaded && AnimationBehavior.GetAnimator(Picture)!=null;
                    }
                    else await Task.Delay(20);
                    results.Add(new { file=Path.GetFileName(path),loaded,frames=AnimationBehavior.GetAnimator(Picture)?.FrameCount });
                }
                checks["fileResults"]=results;
                checks["enumerated"]=files.Length;
                checks["loadedCount"]=LoadCount;
            }
            else if(scenario=="video")
            {
                string video=option("--video-file",Directory.EnumerateFiles(Settings.VideoFolder,"*.mp4").First());
                checks["videoFile"]=Path.GetFileName(video);
                checks["videoSelected"]=LoadFile(video) && IsVideo(CurrentFile!);
                for(int i=0;i<200 && !VideoIsOpen && Failures.Count==0;i++) await Task.Delay(50);
                checks["mediaOpened"]=VideoIsOpen;
                checks["naturalWidth"]=VideoPlayer.NaturalVideoWidth;
                checks["naturalHeight"]=VideoPlayer.NaturalVideoHeight;
                checks["muted"]=VideoPlayer.IsMuted;
                checks["speedRatio"]=VideoPlayer.SpeedRatio;
                checks["videoWithinFrame"]=VideoPlayer.Width<=Width+0.1 && VideoPlayer.Height<=Height+0.1;
                var videoSettings=new SettingsWindow(Settings);
                videoSettings.MediaModeBox.SelectedIndex=2;
                videoSettings.VideoFolderBox.Text=Settings.VideoFolder;
                videoSettings.VideoSpeedBox.SelectedIndex=2;
                videoSettings.SaveButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                checks["videoSettingsValidate"]=videoSettings.SaveValidated && videoSettings.SelectedMediaMode=="Mixed" && videoSettings.SelectedVideoSpeed==1.5 && videoSettings.SelectedVideoFolder==Settings.VideoFolder;
                if(VideoIsOpen)
                {
                    await Task.Delay(1000);
                    var p1=VideoPlayer.Position;
                    Capture(Path.Combine(output,"video-frame.png"));
                    await Task.Delay(1000);
                    checks["positionAdvances"]=VideoPlayer.Position>p1;
                    checks["durationSeconds"]=VideoPlayer.NaturalDuration.HasTimeSpan ? VideoPlayer.NaturalDuration.TimeSpan.TotalSeconds : 0;
                    if(VideoPlayer.NaturalDuration.HasTimeSpan && VideoPlayer.NaturalDuration.TimeSpan.TotalSeconds>3)
                    {
                        VideoPlayer.Position=VideoPlayer.NaturalDuration.TimeSpan-TimeSpan.FromSeconds(1.5);
                        checks["positionAfterSeekRequest"]=VideoPlayer.Position.TotalSeconds;
                        for(int i=0;i<160 && CurrentFile==video;i++)await Task.Delay(50);
                        checks["positionAfterWait"]=VideoPlayer.Position.TotalSeconds;
                        checks["videoEndedEvents"]=VideoEndedCount;
                        checks["loadCountAfterEnd"]=LoadCount;
                        checks["endAdvancesToNext"]=CurrentFile!=video;
                        for(int i=0;i<100 && !VideoIsOpen && Failures.Count==0;i++)await Task.Delay(50);
                        checks["nextVideoOpens"]=VideoIsOpen;
                    }
                    Hide();
                    checks["hideReleasesVideo"]=VideoPlayer.Source==null && !VideoIsOpen;
                }
            }
            else if(scenario=="video-release")
            {
                string video=option("--video-file",Directory.EnumerateFiles(Settings.VideoFolder,"*.mp4").First());
                LoadFile(video);
                for(int i=0;i<200 && !VideoIsOpen && Failures.Count==0;i++)await Task.Delay(50);
                checks["videoOpened"]=VideoIsOpen;
                if(VideoIsOpen)
                {
                    await Task.Delay(1500);
                    checks["videoWorkingSetMiB"]=Math.Round(System.Diagnostics.Process.GetCurrentProcess().WorkingSet64/1048576.0,1);
                    checks["videoPrivateMiB"]=Math.Round(System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64/1048576.0,1);
                }
                LoadFile(Directory.EnumerateFiles(Settings.Folder).First(Supported));
                await Task.Delay(3000);
                GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
                checks["videoReleased"]=VideoPlayer.Source==null && VideoPlayer.Visibility==Visibility.Collapsed && !VideoIsOpen;
                checks["staticAfterVideoWorkingSetMiB"]=Math.Round(System.Diagnostics.Process.GetCurrentProcess().WorkingSet64/1048576.0,1);
                checks["staticAfterVideoPrivateMiB"]=Math.Round(System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64/1048576.0,1);
            }
            else if(scenario=="video-audio")
            {
                string video=option("--video-file",Directory.EnumerateFiles(Settings.VideoFolder,"*.mp4").First());
                Settings.VideoAudioEnabled=false;Settings.VideoVolume=0.25;
                LoadFile(video);
                for(int i=0;i<200 && !VideoIsOpen && Failures.Count==0;i++)await Task.Delay(50);
                checks["opensMutedByDefault"]=VideoIsOpen && VideoPlayer.IsMuted && Math.Abs(VideoPlayer.Volume-0.25)<0.01;
                // Keep the system silent during automated checks while exercising the unmuted media path.
                ApplySettings(Settings.Folder,Settings.IntervalSeconds,videoAudioEnabled:true,videoVolume:0);
                checks["canEnableAudioSilently"]=!VideoPlayer.IsMuted && VideoPlayer.Volume==0 && trayAudioMenu?.IsChecked==true;
                ToggleVideoAudio();
                checks["trayToggleMutes"]=VideoPlayer.IsMuted && trayAudioMenu?.IsChecked==false;
                var dialog=new SettingsWindow(Settings);
                dialog.VideoAudioBox.IsChecked=true;dialog.VideoVolumeSlider.Value=50;
                dialog.SaveButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                checks["settingsAcceptsAudioAndVolume"]=dialog.SaveValidated && dialog.SelectedVideoAudioEnabled && Math.Abs(dialog.SelectedVideoVolume-0.5)<0.01;
                Hide();checks["hideClosesVideoAndAudio"]=VideoPlayer.Source==null && !VideoIsOpen;
            }
            else if(scenario=="video-watcher")
            {
                string source=option("--video-file",Directory.EnumerateFiles(Settings.VideoFolder,"*.mp4").First());
                string scratch=Path.Combine(output,"视频监视副本");Directory.CreateDirectory(scratch);
                ApplySettings(Settings.Folder,Settings.IntervalSeconds,videoFolder:scratch,mediaMode:"Mixed");
                checks["initiallyEmpty"]=files.Length==0;
                string added=Path.Combine(scratch,"新加入视频.mp4");File.Copy(source,added,true);
                for(int i=0;i<160 && (!files.Contains(added,StringComparer.OrdinalIgnoreCase) || !VideoIsOpen);i++)await Task.Delay(50);
                checks["newVideoAutomaticallyDiscoveredAndPlayed"]=files.Contains(added,StringComparer.OrdinalIgnoreCase) && VideoIsOpen && CurrentFile==added;
                checks["videoWatcherActive"]=string.Equals(watchedVideoFolder,scratch,StringComparison.OrdinalIgnoreCase);
            }
            else if(scenario=="media-modes")
            {
                string images=Settings.Folder,videos=Settings.VideoFolder;
                ApplySettings(images,180,videoFolder:videos,mediaMode:"Images");
                checks["imagesOnly"]=files.Length>0 && files.All(Supported) && videoWatcher==null;
                ApplySettings(Path.Combine(output,"missing-images"),180,videoFolder:videos,mediaMode:"Videos");
                checks["videosOnlyWithoutImageFolder"]=files.Length>0 && files.All(IsVideo) && folderWatcher==null && videoWatcher!=null;
                var dialog=new SettingsWindow(Settings);dialog.FolderBox.Text=Path.Combine(output,"missing-images");dialog.MediaModeBox.SelectedIndex=1;
                dialog.SaveButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                checks["videoOnlySettingsValidateWithoutImageFolder"]=dialog.SaveValidated && dialog.SelectedMediaMode=="Videos";
                ApplySettings(images,180,videoFolder:videos,mediaMode:"Mixed");
                checks["mixedIncludesBoth"]=files.Any(Supported) && files.Any(IsVideo) && folderWatcher!=null && videoWatcher!=null;
            }
            else
            {
                if(scenario=="video-perf") LoadFile(option("--video-file",Directory.EnumerateFiles(Settings.VideoFolder,"*.mp4").First()));
                if(scenario=="slideshow") { Settings.IntervalSeconds=1; StartSlideshow(); }
                else if(scenario=="hidden") { await Task.Delay(1000); Hide(); }
                await Task.Delay(TimeSpan.FromSeconds(int.Parse(option("--seconds","35"))));
                checks["loadedCount"]=LoadCount;
                checks["currentGIFFrameCount"]=AnimationBehavior.GetAnimator(Picture)?.FrameCount;
                checks["currentMediaFile"]=CurrentFile==null ? null : Path.GetFileName(CurrentFile);
                checks["videoOpen"]=VideoIsOpen;
                checks["oldAnimators"]=OldAnimators.Count;
                checks["oldAnimatorsAliveWithoutForcedGC"]=OldAnimators.Count(w=>w.IsAlive);
                checks["actualOpacity"]=Opacity;
                var activeAnimator=AnimationBehavior.GetAnimator(Picture);
                checks["restoreCanvasBytes"]=activeAnimator==null ? 0 : ((byte[])typeof(Animator).GetField("_previousBackBuffer",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(activeAnimator)!).Length;
                checks["frameWidth"]=Width; checks["frameHeight"]=Height;
                checks["workingSetBeforeCapture"]=System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
                checks["privateBytesBeforeCapture"]=System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64;
                checks["collectionCounts"]=new[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
                if(IsVisible) Capture(Path.Combine(output,scenario+".png"));
            }
            checks["nativeWindowVisible"]=IsWindowVisible(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            checks["managedBytes"]=GC.GetTotalMemory(false);
            checks["gcHeapBytes"]=GC.GetGCMemoryInfo().HeapSizeBytes;
            checks["dpiScale"]=VisualTreeHelper.GetDpi(this).DpiScaleX;
            checks["errors"]=Failures.ToArray();
        }
        catch(Exception ex) { checks["testException"]=ex.ToString(); }
        finally
        {
            File.WriteAllText(Path.Combine(output,scenario+"-result.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions {WriteIndented=true}));
            Close();
        }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr handle);
    static ulong HashBitmap(BitmapSource bitmap)
    {
        var scaled=new TransformedBitmap(bitmap,new ScaleTransform(32.0/bitmap.PixelWidth,32.0/bitmap.PixelHeight));
        var data=new byte[32*32*4]; var converted=new FormatConvertedBitmap(scaled,PixelFormats.Bgra32,null,0); converted.CopyPixels(data,128,0);
        ulong hash=14695981039346656037; foreach(byte b in data) hash=unchecked((hash^b)*1099511628211); return hash;
    }
    void Capture(string path)
    {
        var bitmap=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(FrameRoot);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream=File.Create(path); encoder.Save(stream);
    }
}








