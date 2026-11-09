#region USINGS

#endregion USINGS

namespace SqlCipherWrapper.Runtime
{
    /// <summary>
    /// Состояние воркера. Используется для диагностики и защиты от
    /// некорректных вызовов (например, Enqueue до Start).
    /// </summary>
    public enum WorkerState
    {
        /// <summary>Создан, StartAsync не вызван.</summary>
        Created,

        /// <summary>Поток запущен, соединение открывается.</summary>
        Starting,

        /// <summary>Готов принимать операции.</summary>
        Running,

        /// <summary>Dispose вызван, очередь дренируется.</summary>
        Stopping,

        /// <summary>Поток завершён, соединение закрыто.</summary>
        Stopped,

        /// <summary>Критическая ошибка, воркер неработоспособен.</summary>
        FatalError
    }
}