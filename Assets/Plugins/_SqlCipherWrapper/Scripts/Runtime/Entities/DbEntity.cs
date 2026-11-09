#region USINGS

using SQLite.Attributes;

#endregion USINGS

namespace SqlCipherWrapper.Runtime
{
    /// <summary>
    /// Базовый класс для всех сущностей БД.
    /// Содержит первичный ключ с автоинкрементом.
    ///
    /// Все таблицы в БД должны наследоваться от этого класса,
    /// чтобы использовать обобщённые CRUD-операции.
    /// </summary>
    public abstract class DbEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
    }
}