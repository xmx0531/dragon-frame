using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace DragonFrame.Models;
public sealed class AppSettings
{
    public string Folder { get; set; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","图库"));
    public string VideoFolder { get; set; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","视频"));
    public string MediaMode { get; set; } = "Images";
    public static readonly string[] MediaModes = { "Images", "Videos", "Mixed" };
    public double VideoSpeed { get; set; } = 1;
    public bool VideoAudioEnabled { get; set; } = false;
    public double VideoVolume { get; set; } = 0.25;
    public List<string> ExcludedFiles { get; set; } = new();
    public bool IsLocked { get; set; } = false;
    public bool EnlargeSmallImages { get; set; } = true;
    public string ImageAnchor { get; set; } = "TopRight";
    public int IntervalSeconds { get; set; } = 180;
    public double Width { get; set; } = 300;
    public double Height { get; set; } = 200;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public static readonly string[] Anchors = { "TopLeft","TopRight","Center","BottomLeft","BottomRight" };
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings.json");
    public static AppSettings Load()
    {
        try
        {
            string json=File.ReadAllText(FilePath);
            var s = JsonSerializer.Deserialize<AppSettings>(json) ?? new();
            using(var legacy=JsonDocument.Parse(json))
                if(!legacy.RootElement.TryGetProperty(nameof(MediaMode),out _) && legacy.RootElement.TryGetProperty("IncludeVideos",out var oldVideo) && oldVideo.ValueKind==JsonValueKind.True)
                    s.MediaMode="Mixed";
            if(Array.IndexOf(MediaModes,s.MediaMode)<0)s.MediaMode="Images";
            if (s.IntervalSeconds<1 || s.IntervalSeconds>86400) s.IntervalSeconds=180;
            if(Array.IndexOf(Anchors,s.ImageAnchor)<0) s.ImageAnchor="TopRight";
            s.Width = double.IsFinite(s.Width) ? Math.Clamp(s.Width,100,800) : 300;
            s.Height = double.IsFinite(s.Height) ? Math.Clamp(s.Height,80,600) : 200;
            s.Folder = s.Folder ?? "";
            s.VideoFolder = string.IsNullOrWhiteSpace(s.VideoFolder) ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","视频")) : s.VideoFolder;
            if (!double.IsFinite(s.VideoSpeed) || s.VideoSpeed < 0.5 || s.VideoSpeed > 2) s.VideoSpeed = 1;
            if (!double.IsFinite(s.VideoVolume) || s.VideoVolume < 0 || s.VideoVolume > 1) s.VideoVolume = 0.25;
            s.ExcludedFiles=(s.ExcludedFiles ?? new()).Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return s;
        }
        catch (Exception ex) { if (File.Exists(FilePath)) App.Log("读取设置失败 " + ex.Message); return new(); }
    }
    public void Save()
    {
        var temp=FilePath+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(this,new JsonSerializerOptions { WriteIndented=true }));
        File.Move(temp,FilePath,true);
    }
}
