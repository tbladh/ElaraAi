using System;
using System.Net;

namespace FluentHosting
{
	public class RouteHandler : IRouteHandler
    {
        public string Route { get; }
        public Verb Verb { get; }
        public Func<HttpListenerContext, IHandlerResponse> Handler { get; }

        public CorsConfig CorsConfig { get; }

        public RouteHandler(string route, Verb verb, Func<HttpListenerContext, IHandlerResponse> handler, CorsConfig corsConfig = null)
        {
            Route = route;
            Verb = verb;
            Handler = handler;
            CorsConfig = corsConfig;
        }

    }
}
