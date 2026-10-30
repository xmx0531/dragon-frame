using System;
using System.Collections.Generic;
using System.Linq;
namespace DragonFrame;
// Filename metadata only: no decoded pictures or animation frames are retained here.
internal sealed class ShufflePlaylist
{
    readonly List<string> pending = new();
    readonly HashSet<string> played = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> known = new(StringComparer.OrdinalIgnoreCase);
    string? folder;
    internal void Synchronize(string folderKey,IEnumerable<string> files)
    {
        if(!string.Equals(folder,folderKey,StringComparison.OrdinalIgnoreCase))
        { folder=folderKey; pending.Clear(); played.Clear(); known.Clear(); }
        var available=new HashSet<string>(files,StringComparer.OrdinalIgnoreCase);
        pending.RemoveAll(p=>!available.Contains(p));
        played.RemoveWhere(p=>!available.Contains(p));
        known.RemoveWhere(p=>!available.Contains(p));
        foreach(string file in available)
            if(known.Add(file)) pending.Insert(Random.Shared.Next(pending.Count+1),file);
    }
    internal string? TakeNext(string? current)
    {
        if(known.Count==0) return null;
        if(pending.Count==0)
        {
            played.Clear(); pending.AddRange(known);
            for(int i=pending.Count-1;i>0;i--) { int j=Random.Shared.Next(i+1); (pending[i],pending[j])=(pending[j],pending[i]); }
        }
        // At a round boundary, avoid immediately replaying the last filename.
        int index=pending.Count-1;
        if(pending.Count>1 && string.Equals(pending[index],current,StringComparison.OrdinalIgnoreCase))
        {
            int other=Random.Shared.Next(index); (pending[index],pending[other])=(pending[other],pending[index]);
        }
        string next=pending[index]; pending.RemoveAt(index); played.Add(next); return next;
    }
}
