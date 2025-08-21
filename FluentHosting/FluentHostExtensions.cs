using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace FluentHosting
{
	public static class FluentHostExtensions
    {

        public static string PathToContentType(this string path)
        {
            var extension = Path.GetExtension(path);
            switch (extension)
            {
                case ".html": return "text/html";
                case ".js": return "text/javascript";
                case ".css": return "text/css";
                case ".json": return "application/json";
                default: return "text/plain";
            }
        }

        public static FluentHost Handles(this FluentHost host, string route, Verb verb, Func<HttpListenerContext, IHandlerResponse> handler, CorsConfig corsConfig = null)
        {
            host.Handlers.Add(new RouteHandler(route, verb, handler, corsConfig));

            if (corsConfig == null) return host;

            // TODO: Tidy this up.
            var preflightHandler = new RouteHandler(route, 
                Verb.Options, 
                context =>
                {
                    var origin = context.Request.Headers["Origin"].ToLowerInvariant();
                    var allow = corsConfig.Origins.Any(p => p.ToLowerInvariant() == origin) || corsConfig.Origins.Any(p => p == "*");
                    return new HandlerResponse
                    {
                        Code = allow ? 200 : 400,
                        ContentLength = 0,
                        ContentType = "text/plain", Encoding = Encoding.UTF8,
                        Stream = new MemoryStream(new byte[] { })
                    };
                }, corsConfig);
            host.Handlers.Add(preflightHandler);

            return host;
        }

        public static FluentHost AddHandler(this FluentHost host, IRouteHandler handler)
        {
            host.Handlers.Add(handler);
            return host;
        }

        public static Verb ToVerb(this string method)
        {
            switch (method)
            {
                case "GET": return Verb.Get;
                case "PUT": return Verb.Put;
                case "POST": return Verb.Post;
                case "DELETE": return Verb.Delete;
                case "OPTIONS": return Verb.Options;
                default: return Verb.Get;
            }
        }

        public static string GetFlagsString(this Verb verb)
        {
            var flags = verb.GetFlags().Where(p => !p.Equals(Verb.All) && !p.Equals(Verb.None));
            var list = string.Join(", ", flags.Select(p => Enum.GetName(typeof(Verb), p)?.ToUpper()));
            return list;
        }

        public static T ParseFlags<T>(this string value) where T : struct
        {
            if (Enum.TryParse(value, true, out T result))
            {
                return result;
            }
            throw new ArgumentException("Value could not be parsed.");
        }

        public static IEnumerable<Enum> GetFlags(this Enum input)
        {
            return Enum.GetValues(input.GetType()).Cast<Enum>().Where(input.HasFlag);
        }

    }
}
