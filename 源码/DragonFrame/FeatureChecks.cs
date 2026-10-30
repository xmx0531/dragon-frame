using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using XamlAnimatedGif;
namespace DragonFrame;
public partial class MainWindow
{
    static IEnumerable<Button> LogicalButtons(DependencyObject root)
    {
        if(root is Button button) yield return button;
        foreach(object child in LogicalTreeHelper.GetChildren(root))
            if(child is DependencyObject node) foreach(var result in LogicalButtons(node)) yield return result;
    }
    internal async Task VerifyFeaturesAsync(Dictionary<string,object?> checks,string output)
    {
        async Task<bool> until(Func<bool> condition,int timeout=6000)
        {
            for(int elapsed=0;elapsed<timeout;elapsed+=50) { if(condition()) return true; await Task.Delay(50); } return condition();
        }
        SetChromeVisible(false); checks["chromeHiddenAtRest"]=HoverToolbar.Visibility==Visibility.Collapsed;
        SetChromeVisible(true); checks["hoverExposesControls"]=HoverToolbar.Visibility==Visibility.Visible && ResizeGrip.Visibility==Visibility.Visible && NextButton.IsEnabled && SettingsButton.IsEnabled && LockButton.IsEnabled && HideButton.IsEnabled;
        FrameRoot.UpdateLayout(); Capture(Path.Combine(output,"hover-toolbar.png"));
        string? before=CurrentFile; var beforeDue=nextChangeAt;
        NextButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        checks["nextButtonChangesImageAndResetsCountdown"]=CurrentFile!=before && nextChangeAt>=beforeDue;
        checks["nextTimeShown"]=TimingText.Text.Contains("下张") && TimingText.Text.Contains("3分钟");
        LockButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        checks["lockButtonEnablesNativeMousePassThrough"]=Settings.IsLocked && NativeClickThrough && HoverToolbar.Visibility==Visibility.Collapsed;
        TrayDoubleClick(); checks["trayDoubleClickUnlocks"]=!Settings.IsLocked && !NativeClickThrough;
        HideButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        checks["hideButtonStopsAnimation"]=!IsVisible && AnimationBehavior.GetAnimator(Picture)==null && !slideshow.IsEnabled;
        TrayDoubleClick(); checks["trayDoubleClickShowsWindow"]=IsVisible;
        // Exercise production settings handlers without showing a native dialog or injecting input.
        var dialog=new SettingsWindow(Settings); dialog.FolderBox.Text=Settings.Folder; dialog.WidthBox.Text="450"; dialog.HeightBox.Text="300"; dialog.IntervalValueBox.Text="45"; dialog.IntervalUnitBox.SelectedIndex=0; dialog.EnlargeBox.IsChecked=true; dialog.AnchorBox.SelectedIndex=1;
        dialog.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        checks["settingsSaveButtonValidation"]=dialog.SaveValidated && dialog.SelectedInterval==45 && dialog.SelectedWidth==450 && dialog.SelectedHeight==300 && dialog.SelectedEnlarge && dialog.SelectedAnchor=="TopRight";
        ApplySettings(dialog.SelectedFolder,dialog.SelectedInterval,dialog.SelectedWidth,dialog.SelectedHeight,dialog.SelectedEnlarge,dialog.SelectedAnchor);
        await Task.Delay(100);
        checks["customTimeApplied"]=Settings.IntervalSeconds==45 && slideshow.Interval.TotalSeconds==45;
        checks["settingsCreateFixedFrame"]=Math.Abs(Width-450)<=1 && Math.Abs(Height-300)<=1;
        var invalid=new SettingsWindow(Settings); invalid.WidthBox.Text="801"; invalid.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        checks["sizeOverMaximumRejected"]=!invalid.SaveValidated && invalid.ErrorText.Text.Length>0;
        checks["customDecimalMinutes"]=SettingsWindow.TryParseInterval("2.5",60,out int sec) && sec==150;
        checks["customSeconds"]=SettingsWindow.TryParseInterval("75",1,out sec) && sec==75;
        checks["customHours"]=SettingsWindow.TryParseInterval("1.5",3600,out sec) && sec==5400;
        checks["rejectZeroTime"]=!SettingsWindow.TryParseInterval("0",60,out sec);
        checks["rejectOverOneDay"]=!SettingsWindow.TryParseInterval("25",3600,out sec);
        checks["rejectNonNumericTime"]=!SettingsWindow.TryParseInterval("hello",1,out sec);
        checks["rejectFractionOfSecond"]=!SettingsWindow.TryParseInterval("0.1",1,out sec);
        checks["boundedLandscapeFit"]=FitPicture(4000,2000,450,300,true)==(450,225);
        checks["boundedPortraitFit"]=FitPicture(2000,4000,450,300,true)==(150,300);
        checks["smallImageCanEnlargeWithinFrame"]=FitPicture(150,100,450,300,true)==(450,300);
        checks["smallImageCanKeepOriginalSize"]=FitPicture(150,100,450,300,false)==(150,100);
        double stableLeft=Left,stableTop=Top;
        double stableWidth=Width,stableHeight=Height;
        bool frameStable=true, picturesContained=true;
        var geometry=new List<object>();
        foreach(string file in Directory.GetFiles(Settings.Folder).Where(Supported))
        {
            LoadFile(file);
            if(IsGif(file)) await until(()=>AnimationBehavior.GetAnimator(Picture)!=null);
            await Task.Delay(50); UpdateLayout();
            geometry.Add(new {file=Path.GetFileName(file),Width,Height,Left,Top});
            frameStable &= Width==stableWidth && Height==stableHeight && Left==stableLeft && Top==stableTop;
            picturesContained &= Picture.Width<=Width+0.1 && Picture.Height<=Height+0.1;
        }
        checks["frameGeometryByImage"]=geometry;
        checks["mixedImagesNeverMoveOrResizeFrame"]=frameStable;
        checks["allPicturesStayInsideFrame"]=picturesContained;
        bool anchorsCorrect=true;
        foreach(string anchor in Models.AppSettings.Anchors)
        {
            ApplySettings(Settings.Folder,45,450,300,true,anchor); await Task.Delay(30); UpdateLayout();
            Point origin=Picture.TranslatePoint(new Point(0,0),FrameRoot);
            double expectedX=anchor.EndsWith("Right") ? Width-Picture.ActualWidth : anchor.EndsWith("Left") ? 0 : (Width-Picture.ActualWidth)/2;
            double expectedY=anchor.StartsWith("Bottom") ? Height-Picture.ActualHeight : anchor.StartsWith("Top") ? 0 : (Height-Picture.ActualHeight)/2;
            anchorsCorrect &= Math.Abs(origin.X-expectedX)<1 && Math.Abs(origin.Y-expectedY)<1 && Left==stableLeft && Top==stableTop;
        }
        checks["allFiveAnchorsPositionImageWithoutMovingFrame"]=anchorsCorrect;
        var previewDialog=new SettingsWindow(Settings); previewDialog.WidthBox.Text="600"; previewDialog.HeightBox.Text="400";
        previewDialog.PreviewSizeRequested+=PreviewFrameSize;
        previewDialog.PreviewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        checks["previewShowsRequestedFrameAndOutlineWithoutSaving"]=Width==600 && Height==400 && FrameOutline.Visibility==Visibility.Visible && Settings.Width==450 && Settings.Height==300 && Left==stableLeft && Top==stableTop;
        RestoreFrameSize(450,300);
        checks["cancelPreviewRestoresFrameSize"]=Math.Abs(Width-450)<=1 && Math.Abs(Height-300)<=1 && Left==stableLeft && Top==stableTop;
        SetChromeVisible(false); checks["outlineHiddenAtRest"]=FrameOutline.Visibility==Visibility.Collapsed;
        SetChromeVisible(true); checks["hoverShowsDashedFrameAndDimensions"]=FrameOutline.Visibility==Visibility.Visible && FrameOutline.StrokeDashArray.Count>0 && TimingText.Text.Contains("450×300");
        FrameRoot.UpdateLayout(); Capture(Path.Combine(output,"fixed-frame-preview.png"));
        ResizeFrame(ResizeGrip,new DragDeltaEventArgs(5000,5000));
        checks["dragResizeClamped"]=Width==800 && Height==600;
        ApplySettings(Settings.Folder,180,300,200,true,"TopRight");
        checks["defaultSizeRestored"]=Width==300 && Height==200;
        // Watch only copies in the test output folder. Never mutate the real gallery.
        string originalFolder=Settings.Folder;
        string jpg=Directory.GetFiles(originalFolder).First(p=>!IsGif(p) && Supported(p));
        string live=Path.Combine(output,"watched-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(live); File.Copy(jpg,Path.Combine(live,"one.jpg"),true);
        ApplySettings(live,180); await Task.Delay(150);
        checks["singleImageHasNoIdleSlideshowTimer"]=files.Length==1 && !slideshow.IsEnabled;
        string added=Path.Combine(live,"ADDED.JPEG"); File.Copy(jpg,added,true);
        checks["newImageAutomaticallyDiscovered"]=await until(()=>files.Length==2 && files.Contains(added));
        checks["watcherStartsSlideshowWhenSecondImageArrives"]=slideshow.IsEnabled;
        string? current=CurrentFile; var due=nextChangeAt;
        string third=Path.Combine(live,"third.png"); File.Copy(jpg,third,true);
        checks["newImageDoesNotInterruptCurrentOrResetTimer"]=await until(()=>files.Length==3) && CurrentFile==current && nextChangeAt==due;
        string renamed=Path.Combine(live,"renamed.jpeg"); File.Move(added,renamed,true);
        checks["renameAutomaticallyDiscovered"]=await until(()=>files.Contains(renamed) && !files.Contains(added));
        File.Delete(third); checks["deletionAutomaticallyDiscovered"]=await until(()=>files.Length==2 && !files.Contains(third));
        Hide(); string fourth=Path.Combine(live,"hidden-added.jpg"); File.Copy(jpg,fourth,true);
        checks["hiddenUpdatesListWithoutDecoding"]=await until(()=>files.Length==3) && Picture.Source==null && !slideshow.IsEnabled;
        Show(); checks["shownRestartsTimer"]=slideshow.IsEnabled;
        ApplySettings(originalFolder,180); await Task.Delay(200);
        checks["watcherReplacedOnFolderChange"]=string.Equals(watchedFolder,Path.GetFullPath(originalFolder),StringComparison.OrdinalIgnoreCase);
        var watcherBefore=watchedFolder; File.Copy(jpg,Path.Combine(live,"old-folder-added.jpg"),true); await Task.Delay(1000);
        checks["oldFolderNoLongerChangesList"]=watchedFolder==watcherBefore && files.Length==Directory.GetFiles(originalFolder).Count(Supported);
        folderDebounce.Stop(); checks["noWatcherPollingTimer"]=!folderDebounce.IsEnabled;
        var bag=new ShufflePlaylist(); var names=new[]{"a","b","c","d","e"};
        bag.Synchronize("one",names); string? previous=null; bool uniqueRounds=true,noAdjacent=true;
        for(int round=0;round<30;round++)
        {
            var seen=new HashSet<string>();
            for(int i=0;i<names.Length;i++) { string next=bag.TakeNext(previous)!; uniqueRounds &= seen.Add(next); noAdjacent &= next!=previous; previous=next; bag.Synchronize("one",names); }
        }
        checks["shuffleThirtyCompleteRoundsWithoutRepeats"]=uniqueRounds;
        checks["shuffleNoImmediateRepeatAcrossRounds"]=noAdjacent;
        bag.Synchronize("changed",new[]{"a","b","c"});
        string first=bag.TakeNext(null)!;
        bag.Synchronize("changed",new[]{"a","b","c","new"});
        var rest=new List<string>(); for(int i=0;i<3;i++) rest.Add(bag.TakeNext(null)!);
        checks["shuffleAdditionPreservesAlreadyPlayed"]=!rest.Contains(first) && rest.Distinct().Count()==3 && rest.Contains("new");
        bag.Synchronize("deleted",new[]{"a","b","c"});
        bag.TakeNext(null); bag.Synchronize("deleted",new[]{"only"});
        checks["shuffleRemovedFilesNeverSelected"]=bag.TakeNext(null)=="only" && bag.TakeNext("only")=="only";
        bag.Synchronize("empty",Array.Empty<string>()); checks["shuffleEmptyFolder"]=bag.TakeNext(null)==null;
        string shuffleFolder=Path.Combine(output,"shuffle-copies"); Directory.CreateDirectory(shuffleFolder);
        for(int i=0;i<5;i++) File.Copy(jpg,Path.Combine(shuffleFolder,$"photo-{i}.jpg"),true);
        ApplySettings(shuffleFolder,180); bool integration=true; previous=null;
        for(int round=0;round<3;round++)
        {
            var seen=new HashSet<string>();
            for(int i=0;i<5;i++)
            {
                string selected=CurrentFile!; integration &= seen.Add(selected) && selected!=previous;
                integration &= string.Equals(TimingText.ToolTip as string,Path.GetFileName(selected),StringComparison.Ordinal); previous=selected; Next();
            }
        }
        checks["nextHandlerThreeUniqueRoundsAndFilename"]=integration;
        ApplySettings(originalFolder,180);
        var presetDialog=new SettingsWindow(Settings);
        var buttons=LogicalButtons(presetDialog).Where(b=>b.Tag is string tag && new[]{"240,240","320,320","450,350"}.Contains(tag)).ToArray();
        bool presets=buttons.Length==3;
        foreach(var button in buttons) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); var dims=((string)button.Tag).Split(','); presets &= presetDialog.WidthBox.Text==dims[0] && presetDialog.HeightBox.Text==dims[1]; }
        checks["threeSizePresetsSetExpectedDimensions"]=presets;

        await VerifyImprovementsAsync(checks,output);
        slideshow.Stop();
        SetChromeVisible(true); FrameRoot.UpdateLayout(); Capture(Path.Combine(output,"final-toolbar.png")); SetChromeVisible(false);
    }
}


