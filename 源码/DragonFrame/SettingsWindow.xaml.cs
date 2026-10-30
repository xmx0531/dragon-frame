using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DragonFrame.Models;
namespace DragonFrame;
public partial class SettingsWindow : Window
{
    public bool RestoreExcludedRequested {get;private set;}
    public string SelectedFolder=>FolderBox.Text.Trim();
    public string SelectedVideoFolder=>VideoFolderBox.Text.Trim();
    public string SelectedMediaMode=>((ComboBoxItem)MediaModeBox.SelectedItem).Tag.ToString()!;
    public double SelectedVideoSpeed=>double.Parse(((ComboBoxItem)VideoSpeedBox.SelectedItem).Tag.ToString()!,CultureInfo.InvariantCulture);
    public bool SelectedVideoAudioEnabled=>VideoAudioBox.IsChecked==true;
    public double SelectedVideoVolume=>VideoVolumeSlider.Value/100;
    public int SelectedInterval { get; private set; }
    internal bool SaveValidated { get; private set; }
    public double SelectedWidth { get; private set; }
    public double SelectedHeight { get; private set; }
    public bool SelectedEnlarge=>EnlargeBox.IsChecked==true;
    public string SelectedAnchor=>((ComboBoxItem)AnchorBox.SelectedItem).Tag.ToString()!;
    internal event Action<double,double>? PreviewSizeRequested;
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent(); MaxHeight=Math.Max(300,SystemParameters.WorkArea.Height-40); FolderBox.Text=settings.Folder;
        VideoFolderBox.Text=settings.VideoFolder;
        foreach(ComboBoxItem item in MediaModeBox.Items)if(item.Tag.ToString()==settings.MediaMode)MediaModeBox.SelectedItem=item;
        foreach(ComboBoxItem item in VideoSpeedBox.Items) if(item.Tag.ToString()==settings.VideoSpeed.ToString("0.###",CultureInfo.InvariantCulture)) VideoSpeedBox.SelectedItem=item;
        if(VideoSpeedBox.SelectedItem==null)VideoSpeedBox.SelectedIndex=1;
        VideoAudioBox.IsChecked=settings.VideoAudioEnabled;
        VideoVolumeSlider.Value=Math.Round(settings.VideoVolume*100/5)*5;
        VideoVolumeText.Text=$"{VideoVolumeSlider.Value:0}%";
        WidthBox.Text=settings.Width.ToString("0",CultureInfo.InvariantCulture); HeightBox.Text=settings.Height.ToString("0",CultureInfo.InvariantCulture); EnlargeBox.IsChecked=settings.EnlargeSmallImages;
        foreach(ComboBoxItem item in AnchorBox.Items) if(item.Tag.ToString()==settings.ImageAnchor) AnchorBox.SelectedItem=item;
        DisplayInterval(settings.IntervalSeconds);
        RestoreExcludedButton.Content=$"恢复全部不参与轮播的图片（{settings.ExcludedFiles.Count}张）";RestoreExcludedButton.IsEnabled=settings.ExcludedFiles.Count>0;
    }
    void DisplayInterval(int seconds)
    {
        int unit=seconds%3600==0 ? 3600 : seconds%60==0 ? 60 : 1;
        IntervalValueBox.Text=((decimal)seconds/unit).ToString("0.###",CultureInfo.InvariantCulture);
        foreach(ComboBoxItem item in IntervalUnitBox.Items) if(item.Tag.ToString()==unit.ToString()) IntervalUnitBox.SelectedItem=item;
    }
    void RestoreExcluded(object sender,RoutedEventArgs e){RestoreExcludedRequested=true;RestoreExcludedButton.Content="保存后恢复全部图片；取消则保持原样";RestoreExcludedButton.IsEnabled=false;}
    void VideoVolumeChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
    {
        if(VideoVolumeText!=null)VideoVolumeText.Text=$"{e.NewValue:0}%";
    }
    void Preset(object sender,RoutedEventArgs e)
    {
        string[] sizes=((string)((Button)sender).Tag).Split(','); WidthBox.Text=sizes[0]; HeightBox.Text=sizes[1];
    }
    void TimePreset(object sender,RoutedEventArgs e)=>DisplayInterval(int.Parse((string)((Button)sender).Tag));
    void Browse(object sender,RoutedEventArgs e)
    {
        var picker=new Microsoft.Win32.OpenFolderDialog { Title="选择图片文件夹", InitialDirectory=SelectedFolder };
        if(picker.ShowDialog(this)==true) FolderBox.Text=picker.FolderName;
    }
    void BrowseVideo(object sender,RoutedEventArgs e)
    {
        var picker=new Microsoft.Win32.OpenFolderDialog {Title="选择视频文件夹",InitialDirectory=SelectedVideoFolder};
        if(picker.ShowDialog(this)==true)VideoFolderBox.Text=picker.FolderName;
    }
    bool ReadSize(out int width,out int height)
    {
        width=height=0;
        if(!int.TryParse(WidthBox.Text,out width) || width<100 || width>800 || !int.TryParse(HeightBox.Text,out height) || height<80 || height>600) { ErrorText.Text="宽度可以填100–800，高度可以填80–600。"; return false; }
        return true;
    }
    void Preview(object sender,RoutedEventArgs e)
    {
        if(!ReadSize(out int width,out int height)) return;
        ErrorText.Text="这是预览；点保存才会记住。";
        PreviewSizeRequested?.Invoke(width,height);
    }
    internal static bool TryParseInterval(string text,int unit,out int seconds)
    {
        seconds=0;
        var style=NumberStyles.AllowLeadingWhite|NumberStyles.AllowTrailingWhite|NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint;
        if(unit!=1 && unit!=60 && unit!=3600) return false;
        if(!decimal.TryParse(text,style,CultureInfo.InvariantCulture,out decimal amount) || amount<=0 || amount>86400) return false;
        decimal total=amount*unit;
        if(total<1 || total>86400 || decimal.Truncate(total)!=total) return false;
        seconds=(int)total; return true;
    }
    void Save(object sender,RoutedEventArgs e)
    {
        SaveValidated=false;
        if(SelectedMediaMode!="Videos" && !Directory.Exists(SelectedFolder)) { ErrorText.Text="找不到图片文件夹，请重新选择。"; return; }
        if(SelectedMediaMode!="Images" && !Directory.Exists(SelectedVideoFolder)) {ErrorText.Text="找不到视频文件夹，请重新选择。";return;}
        if(!ReadSize(out int width,out int height)) return;
        int unit=int.Parse(((ComboBoxItem)IntervalUnitBox.SelectedItem).Tag.ToString()!);
        if(!TryParseInterval(IntervalValueBox.Text,unit,out int seconds)) { ErrorText.Text="时间可以填1秒到24小时，比如45秒、2.5分钟。"; return; }
        try { if(SelectedMediaMode!="Videos") {using var files=Directory.EnumerateFiles(SelectedFolder).GetEnumerator(); files.MoveNext();} if(SelectedMediaMode!="Images") {using var videos=Directory.EnumerateFiles(SelectedVideoFolder).GetEnumerator(); videos.MoveNext();} }
        catch(Exception) { ErrorText.Text="这个文件夹暂时无法读取，请重新选择。"; return; }
        SelectedWidth=width; SelectedHeight=height; SelectedInterval=seconds; SaveValidated=true; if(IsVisible) DialogResult=true;
    }
}
