#region USINGS

using System;

#endregion USINGS

namespace Tickable.Runtime
{
    public interface ITickManager<T> : IDisposable where T : ITick
    {
        void CallHook();

        void PlayHook();
        void StopHook();

        void Register(TickType type, T tick);
        void Register(TickType type, T tick, float intervalSeconds);

        void Remove(TickType type, T tick);
        void Remove(TickType type, T tick, float intervalSeconds);
    }
}