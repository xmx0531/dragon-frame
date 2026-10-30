using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace DragonFrame;
internal sealed class DetailWindow:Window
{
    internal readonly Image LargePicture=new(){Stretch=Stretch.Uniform};
    internal readonly ScrollViewer Viewer=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    internal readonly Button FitButton=new(){Content="完整显示",Margin=new Thickness(4),Padding=new Thickness(10,4,10,4)};
    internal readonly Button ReadButton=new(){Content="按宽阅读（可滚动）",Margin=new Thickness(4),Padding=new Thickness(10,4,10,4)};
    internal bool Reading {get;private set;}
    internal DetailWindow(MainWindow parent,string path,bool gif)
    {
        Title=Path.GetFileName(path)+" · Esc 或关闭返回相框";Topmost=true;ShowInTaskbar=false;ShowActivated=false;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Width=Math.Min(1000,Math.Max(320,SystemParameters.WorkArea.Width-60));Height=Math.Min(800,Math.Max(240,SystemParameters.WorkArea.Height-60));MinWidth=320;MinHeight=240;
        Background=new SolidColorBrush(Color.FromRgb(35,35,35));
        var root=new DockPanel();var buttons=new StackPanel{Orientation=Orientation.Horizontal};buttons.Children.Add(FitButton);buttons.Children.Add(ReadButton);DockPanel.SetDock(buttons,Dock.Top);root.Children.Add(buttons);Viewer.Content=LargePicture;root.Children.Add(Viewer);Content=root;
        FitButton.Click+=(_,_)=>{Reading=false;UpdateMode();};ReadButton.Click+=(_,_)=>{Reading=true;UpdateMode();};Viewer.SizeChanged+=(_,_)=>UpdateMode();
        if(gif)LargePicture.SetBinding(Image.SourceProperty,new Binding("Source"){Source=parent.Picture,Mode=BindingMode.OneWay});
        else
        {
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);
            int w=decoder.Frames[0].PixelWidth,h=decoder.Frames[0].PixelHeight;
            var dpi=VisualTreeHelper.GetDpi(parent);
            // Retain readable width for tall comics, with a bounded temporary decode (~4 MP).
            double factor=Math.Min(1,Math.Min(Width*dpi.DpiScaleX/w,Math.Sqrt(3900000.0/((long)w*h))));
            stream.Position=0;var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.StreamSource=stream;bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=Math.Max(1,(int)Math.Floor(w*factor));bitmap.EndInit();bitmap.Freeze();LargePicture.Source=bitmap;
        }
        UpdateMode();PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){e.Handled=true;Close();}};
        Closed+=(_,_)=>{BindingOperations.ClearBinding(LargePicture,Image.SourceProperty);LargePicture.Source=null;};
    }
    void UpdateMode()
    {
        double w=Viewer.ActualWidth>0?Viewer.ActualWidth:Width-30,h=Viewer.ActualHeight>0?Viewer.ActualHeight:Height-90;
        Viewer.VerticalScrollBarVisibility=Reading?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled;
        LargePicture.Width=Math.Max(1,w-(Reading?20:0));LargePicture.Height=Reading?double.NaN:Math.Max(1,h);
        LargePicture.VerticalAlignment=Reading?VerticalAlignment.Top:VerticalAlignment.Center;
        FitButton.IsEnabled=Reading;ReadButton.IsEnabled=!Reading;
    }
}
