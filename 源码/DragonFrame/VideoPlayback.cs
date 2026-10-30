using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DragonFrame;

public partial class MainWindow
{
    bool releasingVideo;
    internal bool VideoIsOpen { get; private set; }
    internal int VideoEndedCount { get; private set; }

    void StartVideo(string path)
    {
        VideoIsOpen=false;
        VideoPlayer.Visibility=Visibility.Visible;
        ApplyVideoAudio();
        VideoPlayer.SpeedRatio=Settings.VideoSpeed;
        VideoPlayer.Source=new Uri(Path.GetFullPath(path));
        VideoPlayer.Play();
        UpdateAudioControls();
    }

    void ReleaseVideo()
    {
        releasingVideo=true;
        VideoIsOpen=false;
        VideoPlayer.Close();
        VideoPlayer.Source=null;
        VideoPlayer.Visibility=Visibility.Collapsed;
        VideoPlayer.Width=VideoPlayer.Height=double.NaN;
        releasingVideo=false;
        UpdateAudioControls();
    }

    void ApplyVideoAudio()
    {
        VideoPlayer.Volume=Settings.VideoVolume;
        VideoPlayer.IsMuted=!Settings.VideoAudioEnabled;
    }

    internal void ToggleVideoAudio()
    {
        Settings.VideoAudioEnabled=!Settings.VideoAudioEnabled;
        ApplyVideoAudio();
        UpdateAudioControls();
        SaveSettings();
    }

    void VideoAudioClicked(object sender,RoutedEventArgs e)
    {
        e.Handled=true;
        ToggleVideoAudio();
    }

    void UpdateAudioControls()
    {
        bool playingVideo=CurrentFile!=null && IsVideo(CurrentFile) && VideoPlayer.Source!=null;
        VideoAudioButton.Visibility=playingVideo ? Visibility.Visible : Visibility.Collapsed;
        VideoAudioButton.Content=Settings.VideoAudioEnabled ? "♫" : "♪";
        VideoAudioButton.ToolTip=Settings.VideoAudioEnabled ? $"视频声音已开（{Settings.VideoVolume:P0}），点击静音" : "视频已静音，点击开启声音";
        if(audioMenu!=null)audioMenu.IsChecked=Settings.VideoAudioEnabled;
        if(trayAudioMenu!=null)trayAudioMenu.IsChecked=Settings.VideoAudioEnabled;
    }

    void VideoOpened(object sender,RoutedEventArgs e)
    {
        if(releasingVideo || VideoPlayer.Source==null || CurrentFile==null || !IsVideo(CurrentFile)) return;
        VideoIsOpen=true;
        UpdateVideoLayout();
        UpdateTimingText();
    }

    void VideoEnded(object sender,RoutedEventArgs e)
    {
        VideoEndedCount++;
        string? finished=CurrentFile;
        if(!releasingVideo && !closed && IsVisible && VideoPlayer.Source!=null && finished!=null && IsVideo(finished))
            Dispatcher.BeginInvoke(new Action(()=> {if(!closed && IsVisible && CurrentFile==finished && VideoPlayer.Source!=null) Next();}));
    }

    void VideoFailed(object sender,ExceptionRoutedEventArgs e)
    {
        if(releasingVideo || VideoPlayer.Source==null || CurrentFile==null || !IsVideo(CurrentFile)) return;
        string failed=CurrentFile;
        MarkFailed(failed);
        ShowError("视频无法播放："+Path.GetFileName(failed)+" "+e.ErrorException.Message);
        Dispatcher.BeginInvoke(new Action(()=> {if(!closed && IsVisible && CurrentFile==failed) Next();}));
    }
}
