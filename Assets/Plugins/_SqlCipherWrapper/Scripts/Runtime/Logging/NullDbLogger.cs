#region USINGS

using System;

#endregion USINGS

namespace SqlCipherWrapper.Runtime
{
    /// <summary>
    /// Логгер-заглушка. Ничего не делает.
    /// Используется в тестах и когда логи не нужны.
    /// </summary>
    public sealed class NullDbLogger : IDbLogger
    {
        public static readonly NullDbLogger Instance = new NullDbLogger();

        private NullDbLogger() { }

        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, Exception ex = null) { }
    }
}