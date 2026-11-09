namespace RestBuilder.Runtime
{
    internal static class HttpMethodExtensions
    {
        public static string ToVerb(this HttpMethod method) => method switch
        {
            HttpMethod.Get => "GET",
            HttpMethod.Post => "POST",
            HttpMethod.Put => "PUT",
            HttpMethod.Patch => "PATCH",
            HttpMethod.Delete => "DELETE",
            HttpMethod.Head => "HEAD",
            _ => "GET"
        };
    }
}