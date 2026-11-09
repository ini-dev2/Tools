#region USINGS

using SqlCipher4Unity3D;

#endregion USINGS

namespace SqlCipherWrapper.Runtime
{
    /// <summary>
    /// Стандартная фабрика. Создаёт SQLCipher-соединение.
    /// </summary>
    public sealed class DefaultConnectionFactory : IDbConnectionFactory
    {
        public SQLiteConnection Create(string path, string password)
        {
            return new SQLiteConnection(path, password: password);
        }
    }
}