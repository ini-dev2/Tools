#region USINGS

using System;

#endregion USINGS

namespace SqlCipherWrapper.Runtime
{
    /// <summary>
    /// Абстракция логгера. Позволяет:
    /// - использовать Unity Debug в рантайме;
    /// - использовать NullLogger в тестах;
    /// - подменять на кастомный логгер (Sentry, файлы).
    /// </summary>
    public interface IDbLogger
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message, Exception ex = null);
    }
}