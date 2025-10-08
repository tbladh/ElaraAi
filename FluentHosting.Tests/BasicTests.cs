using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;

namespace FluentHosting.Tests
{
    public class BasicTests
    {
        private static string ApiUrl => "http://127.0.0.1";

        [Fact]
        public async Task ComposingAnApi_With_OneHandler_ReturningHelloWorld_ShouldReturn_HelloWorld()
        {
            var port = GetAvailablePort();
            var host = new FluentHost(ApiUrl, port)
                .Handles("/", Verb.Get, context => new StringResponse("Hello World!"))
                .Start();

            try
            {
                using var client = new HttpClient { BaseAddress = BuildBaseUri(port) };
                var data = await client.GetStringAsync("/");
                Assert.Equal("Hello World!", data);
                await Task.Delay(200);
                data = await client.GetStringAsync("/");
                Assert.Equal("Hello World!", data);
            }
            finally
            {
                host.Stop();
            }
        }

        [Fact]
        public async Task ComposingAnApi_With_OneHandler_AcceptingDelete_ShouldReturn_204_And_Empty_Body()
        {
            const string endpoint = "/items/1";
            var port = GetAvailablePort();
            var host = new FluentHost(ApiUrl, port)
                .Handles(endpoint, Verb.Delete, context => new StringResponse(string.Empty, 204))
                .Start();

            try
            {
                using var client = new HttpClient { BaseAddress = BuildBaseUri(port) };
                var response = await client.DeleteAsync(endpoint);
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            }
            finally
            {
                host.Stop();
            }
        }

        private static Uri BuildBaseUri(int port) => new($"{ApiUrl}:{port}/");

        private static int GetAvailablePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
