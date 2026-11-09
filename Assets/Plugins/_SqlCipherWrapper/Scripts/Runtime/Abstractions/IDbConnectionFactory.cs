#region USINGS

using SqlCipher4Unity3D;

#endregion USINGS

namespace SqlCipherWrapper.Runtime
{
    /// <summary>
    /// Фабрика SQLite-соединений.
    /// Нужна для тестируемости: в тестах подставляем in-memory SQLite.
    /// </summary>
    public interface IDbConnectionFactory
    {
        SQLiteConnection Create(string path, string password);
    }
}