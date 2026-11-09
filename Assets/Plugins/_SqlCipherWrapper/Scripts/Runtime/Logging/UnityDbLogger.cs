#region USINGS

using System;
using UnityEngine;

#endregion USINGS

namespace SqlCipherWrapper.Runtime
{
    /// <summary>
    /// Логгер, использующий UnityEngine.Debug.
    /// Info логируется только если verbose = true (для производительности).
    /// Warning и Error логируются всегда.
    /// </summary>
    public sealed class UnityDbLogger : IDbLogger
    {
        private readonly bool _verbose;

        public UnityDbLogger(bool verbose = false)
        {
            _verbose = verbose;
        }

        public void Info(string message)
        {
            if (_verbose)
                Debug.Log($"[DB] {message}");
        }

        public void Warning(string message)
        {
            Debug.LogWarning($"[DB] {message}");
        }

        public void Error(string message, Exception ex = null)
        {
            Debug.LogError(ex == null
                ? $"[DB] {message}"
                : $"[DB] {message}\n{ex}");
        }
    }
}