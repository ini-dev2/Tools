#region USINGS

using SqlCipher4Unity3D;
using System;

#endregion USINGS

namespace SqlCipherWrapper.Runtime
{
    /// <summary>
    /// Не-генерик база. Нужна для единообразной работы с очередью
    /// (BlockingCollection требует один тип).
    /// </summary>
    internal abstract class WorkItemBase
    {
        public string Description { get; protected set; }

        /// <summary>Выполнить операцию на соединении.</summary>
        public abstract void Execute(SQLiteConnection connection);

        /// <summary>Завершить операцию с ошибкой (воркер упал).</summary>
        public abstract void Fail(Exception ex);
    }
}