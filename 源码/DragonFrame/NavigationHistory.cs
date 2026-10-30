using System;
using System.Collections.Generic;
using System.Linq;
namespace DragonFrame;
internal sealed class NavigationHistory
{
    readonly List<string> entries=new(); int cursor=-1;
    internal bool CanBack=>cursor>0;
    internal bool CanForward=>cursor<entries.Count-1;
    internal int Count=>entries.Count;
    internal void Clear(){entries.Clear();cursor=-1;}
    internal void Record(string path)
    {
        if(cursor>=0 && string.Equals(entries[cursor],path,StringComparison.OrdinalIgnoreCase)) return;
        if(CanForward) entries.RemoveRange(cursor+1,entries.Count-cursor-1);
        entries.Add(path); if(entries.Count>30) entries.RemoveAt(0); cursor=entries.Count-1;
    }
    internal string? Back()=>CanBack ? entries[--cursor] : null;
    internal string? Forward()=>CanForward ? entries[++cursor] : null;
    internal void SelectCurrent(string path)
    {int i=entries.FindLastIndex(p=>string.Equals(p,path,StringComparison.OrdinalIgnoreCase));if(i>=0)cursor=i;else Record(path);}
    internal void Prune(HashSet<string> allowed)
    {
        int kept=entries.Take(cursor+1).Count(allowed.Contains);
        entries.RemoveAll(p=>!allowed.Contains(p));cursor=Math.Min(entries.Count-1,kept-1);
    }
}
