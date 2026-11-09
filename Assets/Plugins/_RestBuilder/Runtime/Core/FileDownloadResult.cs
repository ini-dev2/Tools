namespace RestBuilder.Runtime
{
    /// <summary>
    /// Результат DownloadToFileAsync: путь к финальному файлу и размер в байтах.
    /// </summary>
    public readonly struct FileDownloadResult
    {
        public string FilePath { get; }
        public long Bytes { get; }

        public FileDownloadResult(string filePath, long bytes)
        {
            FilePath = filePath;
            Bytes = bytes;
        }

        public override string ToString() => $"{FilePath} ({Bytes} bytes)";
    }
}