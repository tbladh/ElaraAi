using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace FluentHosting
{
	public class FluentHost
	{

		public string Name { get; set; }

        public int Port { get; set; }

		private readonly HttpListener _listener;
		public List<IRouteHandler> Handlers { get; }

		public FluentHost(string name, int port)
		{
			Name = name;
			Port = port;
			_listener = new HttpListener();
			Handlers = new List<IRouteHandler>();
		}

		public FluentHost Start()
		{
			_listener.Prefixes.Add($"{Name}:{Port}/");
			_listener.Start();
			_listener.BeginGetContext(GetContextCallback, null);
			return this;
		}

		public void Stop()
		{
            _listener.Stop();
        }

		private void GetContextCallback(IAsyncResult result)
		{
			if (!_listener.IsListening) return;
            var context = _listener.EndGetContext(result);
            var route = context.Request.Url.LocalPath;
			var verb = context.Request.HttpMethod.ToVerb();

			// Find matching handler for route and verb.
			var handler = Handlers.FirstOrDefault(p => IsRouteMatch(p.Route, route) && (p.Verb & verb) != 0);

			if (handler == null)
			{
				// Find matching default handler for verb. Useful for 404 handling.
				var defaultHandler = Handlers.FirstOrDefault(p => p.Route == "*" && (p.Verb & verb) != 0);
				if (defaultHandler != null)
				{
					Handle(defaultHandler, context);
				}
				else
				{
					// Return default 404 response.
					context.Response.StatusCode = 404;
					context.Response.Close();
				}
			}
			else
			{
				Handle(handler, context);
			}
			_listener.BeginGetContext(GetContextCallback, null);
		}

        // Helper method to match routes including wildcard support
        private bool IsRouteMatch(string routePattern, string requestPath)
        {
            // Simple case - direct match
            if (string.Equals(routePattern, requestPath, StringComparison.InvariantCultureIgnoreCase))
                return true;
            
            // Wildcard handling
            if (routePattern.EndsWith("*"))
            {
                // Get the base part of the pattern (everything before the *)
                var basePath = routePattern.Substring(0, routePattern.Length - 1);
            
                // Check if the request path starts with the base path
                return requestPath.StartsWith(basePath, StringComparison.InvariantCultureIgnoreCase);
            }
        
            return false;
        }

        private static void Handle(IRouteHandler handler, HttpListenerContext context)
		{
            var headers = context.Request.Headers;
            var requestOrigin = headers["Origin"];
            if (requestOrigin != null && handler.CorsConfig != null)
            {
                var corsHeaders = handler.CorsConfig.ToHeaders(requestOrigin);
                foreach (var corsHeader in corsHeaders)
                {
                    context.Response.Headers.Add(corsHeader);
                }
            }
            var hostResponse = handler.Handler.Invoke(context);
			context.Response.ContentEncoding = hostResponse.Encoding;
			context.Response.ContentType = hostResponse.ContentType;
			context.Response.ContentLength64 = hostResponse.ContentLength;
            context.Response.StatusCode = hostResponse.Code;
			using (var outputStream = context.Response.OutputStream)
			{
				hostResponse.Stream.CopyTo(outputStream);
			}
		}
    }
}
