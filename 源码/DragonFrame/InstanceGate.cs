using System;
using System.Threading;
namespace DragonFrame;
// Kernel notification: no periodic polling, and tests use a separate unique name.
internal sealed class InstanceGate:IDisposable
{
    Mutex? mutex;EventWaitHandle? signal;RegisteredWaitHandle? wait;bool owns;
    internal bool IsOwner=>owns;
    internal InstanceGate(string key,Action wake)
    {
        mutex=new Mutex(true,"Local\\DragonFrame.Mutex."+key,out owns);
        string eventName="Local\\DragonFrame.Wake."+key;
        if(!owns)
        {
            try{using var existing=EventWaitHandle.OpenExisting(eventName);existing.Set();}catch(WaitHandleCannotBeOpenedException){}
            mutex.Dispose();mutex=null;return;
        }
        signal=new EventWaitHandle(false,EventResetMode.AutoReset,eventName);
        wait=ThreadPool.RegisterWaitForSingleObject(signal,(_,_)=>{try{wake();}catch{}},null,Timeout.Infinite,false);
    }
    public void Dispose()
    {
        wait?.Unregister(null);wait=null;signal?.Dispose();signal=null;
        if(owns){mutex?.ReleaseMutex();owns=false;}mutex?.Dispose();mutex=null;
    }
}
