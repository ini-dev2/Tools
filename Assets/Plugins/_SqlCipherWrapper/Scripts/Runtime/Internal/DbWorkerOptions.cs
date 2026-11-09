#region USINGS

#endregion USINGS

using System;
using System.Threading;

namespace SqlCipherWrapper.Runtime
{
    /// <summary>
    /// Настройки воркера. Позволяют тонко контролировать поведение
    /// под конкретную платформу (Quest, Pico, PC VR, Editor).
    /// </summary>
    public sealed class DbWorkerOptions
    {
        /// <summary>
        /// Имя потока. Видно в отладчике и crash-репортах.
        /// </summary>
        public string ThreadName { get; set; } = "SqlCipher.DbWorker";

        /// <summary>
        /// Приоритет потока. BelowNormal — не конкурирует с рендером VR.
        /// </summary>
        public ThreadPriority ThreadPriority { get; set; }
            = ThreadPriority.BelowNormal;

        /// <summary>
        /// Максимум элементов в очереди. 0 = без лимита (опасно для памяти).
        /// При переполнении EnqueueAsync будет ждать.
        /// </summary>
        public int QueueCapacity { get; set; } = 1024;

        /// <summary>
        /// Таймаут graceful shutdown при Dispose.
        /// По истечении поток будет брошен (может привести к порче БД).
        /// </summary>
        public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Таймаут одной операции. 0 = без таймаута.
        /// ВНИМАНИЕ: SQLite не поддерживает прерывание из C#,
        /// поэтому таймаут только логирует проблему. Задел на будущее.
        /// </summary>
        public TimeSpan OperationTimeout { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Логировать каждую операцию. В релизе — false (overhead + мусор).
        /// </summary>
        public bool VerboseLogging { get; set; } = false;

        /// <summary>
        /// Автоматически перезапускать воркер при фатальной ошибке.
        /// ОПАСНО: если БД повреждена — зациклится.
        /// В текущей реализации НЕ реализовано.
        /// </summary>
        public bool AutoRestartOnFatalError { get; set; } = false;

        /// <summary>
        /// Максимум попыток перезапуска. В текущей реализации НЕ используется.
        /// </summary>
        public int MaxRestartAttempts { get; set; } = 3;

        public static DbWorkerOptions Default => new DbWorkerOptions();

        /// <summary>
        /// Настройки для VR-шлемов: минимальное влияние на рендер.
        /// </summary>
        public static DbWorkerOptions VrDefault => new DbWorkerOptions
        {
            ThreadPriority = ThreadPriority.BelowNormal,
            QueueCapacity = 512,
            ShutdownTimeout = TimeSpan.FromSeconds(3),
            VerboseLogging = false
        };

        /// <summary>
        /// Настройки для редактора: максимум информации.
        /// </summary>
        public static DbWorkerOptions Editor => new DbWorkerOptions
        {
            ThreadPriority = ThreadPriority.Normal,
            QueueCapacity = 4096,
            ShutdownTimeout = TimeSpan.FromSeconds(1),
            VerboseLogging = true
        };
    }
}