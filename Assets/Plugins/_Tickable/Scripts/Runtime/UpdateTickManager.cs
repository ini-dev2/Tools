#region USINGS

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

#endregion USINGS

namespace Tickable.Runtime
{
    public class UpdateTickManager : ITickManager<IUpdateTick>, IDisposable
    {
        private sealed class SecondsTickGroup<T> where T : ITick
        {
            public TimeSpan Interval { get; }
            public HashSet<T> Ticks { get; } = new();

            public double Accumulator { get; set; }

            public SecondsTickGroup(TimeSpan interval)
            {
                Interval = interval;
            }
        }

        /// <summary>
        /// Ticks in every frame
        /// </summary>
        private readonly HashSet<IUpdateTick> _frameTicks = new();

        /// <summary>
        /// Ticks in game rate
        /// </summary>
        private readonly HashSet<IUpdateTick> _rateTicks = new();

        /// <summary>
        /// Time ticks
        /// </summary>
        private readonly Dictionary<TimeSpan, SecondsTickGroup<IUpdateTick>> _secondsTicks = new();

        private SecondsTickGroup<IUpdateTick>[] _secondsTickCache =
    Array.Empty<SecondsTickGroup<IUpdateTick>>();

        //TODO: Buffer system

        private bool _secondsTickCacheDirty;

        private readonly float _tickInterval;

        private float tickAccumulator;

        private bool disposed;

        private bool isPlaying = false;

        public UpdateTickManager() : this(64)
        {
        }

        public UpdateTickManager(int tickRate)
        {
            if (tickRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRate));
            }

            _tickInterval = 1f / tickRate;
        }

        #region REGISTER

        public void Register(TickType type, IUpdateTick tick)
        {
            ThrowIfDisposed();

            if (tick == null)
            {
                Debug.LogError($"Tick is null for type: {type}");
                return;
            }

            switch (type)
            {
                case TickType.NONE:
                    AddTick(_frameTicks, tick);
                    break;

                case TickType.ON_TICKRATE:
                    AddTick(_rateTicks, tick);
                    break;

                case TickType.ON_SECONDS:
                    throw new ArgumentException(
                        "ON_SECONDS requires an interval.",
                        nameof(type));

                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public void Register(TickType type, IUpdateTick tick, float intervalSeconds)
        {
            ThrowIfDisposed();

            if (tick == null)
            {
                Debug.LogError($"Tick is null for type: {type}");
                return;
            }

            if (type != TickType.ON_SECONDS)
            {
                throw new ArgumentException(
                    "Interval can only be used with ON_SECONDS.",
                    nameof(type));
            }

            if (intervalSeconds <= 0f ||
                float.IsNaN(intervalSeconds) ||
                float.IsInfinity(intervalSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            }

            TimeSpan interval = TimeSpan.FromSeconds(intervalSeconds);

            if (!_secondsTicks.TryGetValue(
                    interval,
                    out SecondsTickGroup<IUpdateTick> group))
            {
                group = new SecondsTickGroup<IUpdateTick>(interval);

                _secondsTicks.Add(interval, group);

                _secondsTickCacheDirty = true;
            }

            AddTick(group.Ticks, tick);
        }

        private void AddTick(HashSet<IUpdateTick> collection, IUpdateTick tick)
        {
            if (!collection.Add(tick))
            {
                Debug.LogWarning($"Tick already registered: {tick}");
            }
        }

        private void RebuildSecondsTickCache()
        {
            if (!_secondsTickCacheDirty)
            {
                return;
            }

            _secondsTickCache = new SecondsTickGroup<IUpdateTick>[_secondsTicks.Count];

            _secondsTicks.Values.CopyTo(_secondsTickCache, 0);

            _secondsTickCacheDirty = false;
        }

        #endregion

        #region REMOVE

        public void Remove(TickType type, IUpdateTick tick)
        {
            ThrowIfDisposed();

            if (tick == null)
            {
                Debug.LogError($"Tick is null for type: {type}");
                return;
            }

            switch (type)
            {
                case TickType.NONE:
                    RemoveTick(_frameTicks, tick);
                    break;

                case TickType.ON_TICKRATE:
                    RemoveTick(_rateTicks, tick);
                    break;

                case TickType.ON_SECONDS:
                    throw new ArgumentException(
                        "ON_SECONDS requires an interval.",
                        nameof(type));

                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public void Remove(TickType type, IUpdateTick tick, float intervalSeconds)
        {
            ThrowIfDisposed();

            if (tick == null)
            {
                Debug.LogError($"Tick is null for type: {type}");
                return;
            }

            if (type != TickType.ON_SECONDS)
            {
                throw new ArgumentException(
                    "Interval can only be used with ON_SECONDS.",
                    nameof(type));
            }

            if (intervalSeconds <= 0f ||
                float.IsNaN(intervalSeconds) ||
                float.IsInfinity(intervalSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            }

            TimeSpan interval = TimeSpan.FromSeconds(intervalSeconds);

            if (!_secondsTicks.TryGetValue(
                    interval,
                    out SecondsTickGroup<IUpdateTick> group))
            {
                Debug.LogWarning($"No ticks registered for interval: {intervalSeconds}");
                return;
            }

            RemoveTick(group.Ticks, tick);

            if (group.Ticks.Count == 0)
            {
                _secondsTicks.Remove(interval);

                _secondsTickCacheDirty = true;
            }
        }

        private void RemoveTick(HashSet<IUpdateTick> collection, IUpdateTick tick)
        {
            if (!collection.Remove(tick))
            {
                Debug.LogWarning($"Tick not registered: {tick}");
            }
        }

        #endregion

        #region UPDATE

        /// <summary>
        /// Called by the Unity Update hook.
        /// </summary>
        private void Update(float dTime)
        {
            if (disposed || !isPlaying)
            {
                return;
            }

            if (dTime < 0f)
            {
                return;
            }

            // Every frame.
            ExecuteTicks(_frameTicks, dTime);

            // Fixed tickrate.
            tickAccumulator += dTime;

            while (tickAccumulator >= _tickInterval)
            {
                tickAccumulator -= _tickInterval;

                ExecuteTicks(_rateTicks, _tickInterval);
            }

            // Custom intervals.
            RebuildSecondsTickCache();

            for (int i = 0; i < _secondsTickCache.Length; i++)
            {
                SecondsTickGroup<IUpdateTick> group = _secondsTickCache[i];

                group.Accumulator += dTime;

                double interval = group.Interval.TotalSeconds;

                while (group.Accumulator >= interval)
                {
                    group.Accumulator -= interval;

                    ExecuteTicks(group.Ticks, (float)interval);
                }
            }
        }

        private void ExecuteTicks(HashSet<IUpdateTick> ticks, float dTime)
        {
            if (ticks.Count == 0)
            {
                return;
            }

            IUpdateTick[] snapshot = new IUpdateTick[ticks.Count];

            ticks.CopyTo(snapshot);

            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i].Tick(dTime);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        #endregion

        #region LIFECYCLE

        private void OnPlayerLoopUpdate()
        {
            if (disposed)
                return;

            Update(Time.deltaTime);
        }

        public void CallHook()
        {
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();

            PlayerLoopSystem customSystem = new PlayerLoopSystem
            {
                type = typeof(UpdateTickManager),
                updateDelegate = OnPlayerLoopUpdate
            };

            if (!AppendToSystem(
                    ref loop,
                    typeof(Update),
                    customSystem))
            {
                throw new InvalidOperationException(
                    "Unity Update system was not found in PlayerLoop.");
            }

            PlayerLoop.SetPlayerLoop(loop);

            isPlaying = true;
        }

        public void DisposeHook()
        {
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();

            RemoveSystem(
                ref loop,
                typeof(UpdateTickManager));

            PlayerLoop.SetPlayerLoop(loop);
        }

        private bool AppendToSystem(ref PlayerLoopSystem parent, Type parentType, PlayerLoopSystem child)
        {
            if (parent.type == parentType)
            {
                PlayerLoopSystem[] systems =
                    parent.subSystemList ?? Array.Empty<PlayerLoopSystem>();

                Array.Resize(ref systems, systems.Length + 1);

                systems[^1] = child;
                parent.subSystemList = systems;

                return true;
            }

            if (parent.subSystemList == null)
            {
                return false;
            }

            for (int i = 0; i < parent.subSystemList.Length; i++)
            {
                if (AppendToSystem(
                        ref parent.subSystemList[i],
                        parentType,
                        child))
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveSystem(ref PlayerLoopSystem parent, Type systemType)
        {
            if (parent.subSystemList == null)
            {
                return;
            }

            List<PlayerLoopSystem> systems = new();

            foreach (PlayerLoopSystem system in parent.subSystemList)
            {
                if (system.type == systemType)
                {
                    continue;
                }

                PlayerLoopSystem child = system;

                RemoveSystem(ref child, systemType);

                systems.Add(child);
            }

            parent.subSystemList = systems.ToArray();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            isPlaying = false;
            disposed = true;

            DisposeHook();

            _frameTicks.Clear();
            _rateTicks.Clear();
            _secondsTicks.Clear();

            _secondsTickCache = Array.Empty<SecondsTickGroup<IUpdateTick>>();
            _secondsTickCacheDirty = false;

            tickAccumulator = 0f;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(UpdateTickManager));
            }
        }

        public void PlayHook()
        {
            ThrowIfDisposed();
            isPlaying = true;
        }

        public void StopHook()
        {
            ThrowIfDisposed();
            isPlaying = false;
        }

        #endregion
    }
}
