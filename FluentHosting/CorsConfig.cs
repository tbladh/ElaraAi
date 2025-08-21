using System;
using System.Collections.Generic;
using System.Linq;

namespace FluentHosting
{
    public class CorsConfig 
    {
        
        public static CorsConfig AllowAll = new CorsConfig(new []{"*"}, Verb.All, new []{"*"} );
        
        public string[] Origins { get; }
        public Verb Methods { get; set; }
        public string[] Headers { get; set; }

        public int MaxAge { get; }

        public CorsConfig(IEnumerable<string> origins, Verb methods, IEnumerable<string> headers, int maxAge = 86400)
        {
            Origins = origins.ToArray();
            Methods = methods;
            Headers = headers.ToArray();
            MaxAge = maxAge;
        }

        public IEnumerable<string> ToHeaders(string requestedOrigin)
        {
            var origin = Origins.FirstOrDefault(p => p == "*") ?? 
                         (Origins.FirstOrDefault(p => string.Equals(p, requestedOrigin, StringComparison.InvariantCultureIgnoreCase)) ?? 
                          Origins.FirstOrDefault());
            if (origin == "*") origin = requestedOrigin;
            if (origin != null)
            {
                yield return $"Access-Control-Allow-Origin: {origin}";
            }
            yield return $"Access-Control-Allow-Methods: {Methods.GetFlagsString()}";
            var headers = string.Join(", ", Headers);
            yield return $"Access-Control-Allow-Headers: {headers}";
            yield return $"Access-Control-Max-Age: {MaxAge}";
        }

    };
}
