namespace RestBuilder.Runtime
{
    /// <summary>
    /// Optional global logger bridge, so you can route logs from RestClient-created
    /// requests and RestRequest directly. Set once at bootstrap.
    /// </summary>
    public static class SharedLogger
    {
        public static IRestLogger Default { get; set; }
    }
}