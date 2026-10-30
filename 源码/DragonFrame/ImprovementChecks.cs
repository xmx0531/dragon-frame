using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using XamlAnimatedGif;
namespace DragonFrame;
public partial class MainWindow
{
    internal async Task VerifyImprovementsAsync(Dictionary<string,object?> checks,string output)
    {
        async Task<bool> until(Func<bool> condition){for(int i=0;i<100;i++){if(condition())return true;await Task.Delay(40);}return condition();}
        string original=Settings.Folder;string jpg=Directory.GetFiles(original).First(p=>Supported(p)&&!IsGif(p));
        string gif=Directory.GetFiles(original,"*.gif").First();
        var nav=new NavigationHistory();for(int i=0;i<50;i++)nav.Record("image"+i);
        checks["historyBoundedTo30Filenames"]=nav.Count==30;
        string? back=nav.Back();checks["historyBackAndForward"]=back=="image48" && nav.Forward()=="image49";
        string live=Path.Combine(output,"navigation");Directory.CreateDirectory(live);
        for(int i=0;i<4;i++)File.Copy(jpg,Path.Combine(live,$"photo{i}.jpg"),true);
        ApplySettings(live,180);string a=CurrentFile!;Next();string b=CurrentFile!;Previous();
        checks["previousReturnsActualLastPicture"]=CurrentFile==a && a!=b;Next();checks["nextAfterPreviousFollowsHistory"]=CurrentFile==b;
        string excluded=CurrentFile!;long size=new FileInfo(excluded).Length;ExcludeCurrent();
        checks["excludePreservesOriginalAndRemovesFromPlayback"]=File.Exists(excluded)&&new FileInfo(excluded).Length==size&&!files.Contains(excluded)&&CurrentFile!=excluded;
        var restoreDialog=new SettingsWindow(Settings);restoreDialog.RestoreExcludedButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        checks["restoreExclusionsWaitsForSave"]=restoreDialog.RestoreExcludedRequested&&Settings.ExcludedFiles.Contains(excluded);
        UndoLastExclude();checks["undoRestoresExcludedFilename"]=files.Contains(excluded)&&!Settings.ExcludedFiles.Contains(excluded);
        Settings.ExcludedFiles.AddRange(files);RefreshFolder();Next();
        checks["allExcludedStopsTimerAndShowsMessage"]=files.Length==0&&!slideshow.IsEnabled&&Picture.Source==null&&Status.Visibility==Visibility.Visible;
        UndoLastExclude();checks["undoFromEmptyPlaybackRestoresPicture"]=files.Length==1&&Picture.Source!=null;
        RestoreAllExcluded();checks["restoreAllExcluded"]=files.Length==4&&Settings.ExcludedFiles.Count==0;
        string brokenFolder=Path.Combine(output,"broken");Directory.CreateDirectory(brokenFolder);
        string broken=Path.Combine(brokenFolder,"broken-async.gif");File.WriteAllText(broken,"not a GIF");File.Copy(jpg,Path.Combine(brokenFolder,"good.jpg"),true);
        ApplySettings(brokenFolder,180);LoadFile(broken);
        checks["brokenAsyncGifAutomaticallySkipped"]=await until(()=>CurrentFile!=broken&&Picture.Source!=null&&failedFiles.ContainsKey(broken));
        File.Copy(gif,broken,true);File.SetLastWriteTimeUtc(broken,DateTime.UtcNow.AddSeconds(2));
        checks["fixedFileAutomaticallyEligibleAgain"]=await until(()=>files.Contains(broken)&&!FailedUnchanged(broken));
        LoadFile(broken);checks["repairedGifActuallyAnimates"]=await until(()=>AnimationBehavior.GetAnimator(Picture)?.FrameCount>1);
        string allBad=Path.Combine(output,"all-broken");Directory.CreateDirectory(allBad);File.WriteAllText(Path.Combine(allBad,"broken1.gif"),"bad");File.WriteAllText(Path.Combine(allBad,"broken2.gif"),"bad");
        ApplySettings(allBad,180);checks["allBrokenStopsWithoutRetryLoop"]=await until(()=>files.Length==0&&!slideshow.IsEnabled&&failedFiles.Count==2);
        int failures=Failures.Count;await Task.Delay(350);checks["allBrokenDoesNotSpamErrors"]=Failures.Count==failures;
        ApplySettings(original,180);LoadFile(gif);await until(()=>AnimationBehavior.GetAnimator(Picture)!=null);
        var animator=AnimationBehavior.GetAnimator(Picture);double w=Width,h=Height,x=Left,y=Top;
        var detail=CreateDetailWindow();detail.Opacity=0;detail.ShowActivated=false;detail.WindowStartupLocation=WindowStartupLocation.Manual;detail.Left=-10000;detail.Top=-10000;detail.Show();await Task.Delay(80);
        checks["largeGifSharesExistingDecoderAndBitmap"]=ReferenceEquals(animator,AnimationBehavior.GetAnimator(Picture))&&ReferenceEquals(detail.LargePicture.Source,Picture.Source);
        detail.Close();checks["largeViewReleasesImageBinding"]=detail.LargePicture.Source==null;
        OpenDetail();checks["largeViewRestoresTimerAndFrameGeometry"]=!detailOpen&&slideshow.IsEnabled&&Width==w&&Height==h&&Left==x&&Top==y;
        LoadFile(jpg);var small=Picture.Source;detail=CreateDetailWindow();
        checks["largeStaticViewDecodesWithinBounds"]=detail.LargePicture.Source is BitmapSource bitmap&&bitmap.PixelWidth<=1300&&(long)bitmap.PixelWidth*bitmap.PixelHeight<=4000000;
        detail.ReadButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        checks["longComicReadModeScrollsWithoutDistortion"]=detail.Reading&&double.IsNaN(detail.LargePicture.Height)&&detail.Viewer.VerticalScrollBarVisibility==ScrollBarVisibility.Auto;
        detail.FitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));checks["completeViewCanBeRestored"]=!detail.Reading&&!double.IsNaN(detail.LargePicture.Height);
        detail.Close();checks["largeStaticViewKeepsFrameImage"]=ReferenceEquals(small,Picture.Source)&&detail.LargePicture.Source==null;
        string fixtures=Path.GetFullPath(Path.Combine(output,"..","allocator"));
        var restoreField=typeof(Animator).GetField("_previousBackBuffer",BindingFlags.NonPublic|BindingFlags.Instance)!;
        foreach(string name in new[]{"no-restore.gif","restore-previous.gif"})
        {
            LoadFile(Path.Combine(fixtures,name));await until(()=>AnimationBehavior.GetAnimator(Picture)!=null);
            var active=AnimationBehavior.GetAnimator(Picture)!;int bytes=((byte[])restoreField.GetValue(active)!).Length;
            checks[name+":restoreBufferBytes"]=bytes;
            checks[name+":allocationCorrect"]=name.StartsWith("no-")?bytes==0:bytes==64*64*4;
        }
        int notifications=0;string key="test-"+Guid.NewGuid().ToString("N");
        using(var first=new InstanceGate(key,()=>Interlocked.Increment(ref notifications)))
        using(var second=new InstanceGate(key,()=>{}))
        {checks["singleInstanceGateRejectsDuplicate"]=first.IsOwner&&!second.IsOwner;checks["duplicateLaunchSignalsExistingWindow"]=await until(()=>Volatile.Read(ref notifications)>0);}
        ApplySettings(original,180);slideshow.Stop();
    }
}
