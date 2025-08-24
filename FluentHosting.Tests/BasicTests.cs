using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FluentHosting.Tests
{
    public class BasicTests
    {
        public const string ApiUrl = "http://localhost";
        public const int ApiPort = 1337;

        public string BaseUrl => $"{ApiUrl}:{ApiPort}/";

        [Fact]
        public async Task ComposingAnApi_With_OneHandler_ReturningHelloWorld_ShouldReturn_HelloWorld()
        {
            var host = new FluentHost(ApiUrl, ApiPort)
                .Handles("/", Verb.Get, context => new StringResponse("Hello World!"))
                .Start();

            using var client = new HttpClient();
            var data = await client.GetStringAsync(BaseUrl);
            Assert.Equal("Hello World!", data);
            await Task.Delay(500);
            data = await client.GetStringAsync(BaseUrl);
            Assert.Equal("Hello World!", data);

            host.Stop();
        }

        [Fact]
        public async Task ComposingAnApi_With_OneHandler_AcceptingDelete_ShouldReturn_204_And_Empty_Body()
        {
            const string endpoint = "/items/1";
            var host = new FluentHost(ApiUrl, ApiPort)
                .Handles(endpoint, Verb.Delete, context => new StringResponse(string.Empty, 204))
                .Start();

            using (var client = new HttpClient())
            {
                var response = await client.DeleteAsync($"{BaseUrl}{endpoint}");
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            }
            host.Stop();
        }


    }
}
