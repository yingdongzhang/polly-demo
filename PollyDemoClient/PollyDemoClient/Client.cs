using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PollyDemoClient
{
    class Client
    {
        private readonly HttpClient _httpClient;

        public Client(IServiceProvider services)
        {
            using IServiceScope serviceScope = services.CreateScope();
            IServiceProvider provider = serviceScope.ServiceProvider;

            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            _httpClient = httpClientFactory.CreateClient("Demo");            
        }

        public async Task Demo1Retry()
        {
            await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "Demo/retry"));
        }

        public async Task Demo2CircuitBreaker()
        {
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    var result = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "Demo/circuit-breaker"));
                    Console.WriteLine($"Status code: {result.StatusCode}");
                    Console.WriteLine(await result.Content.ReadAsStringAsync());
                }
            }
            catch (BrokenCircuitException)
            {
                Console.WriteLine("Sleeping for 3 seconds...");
                Thread.Sleep(5000);
                var result = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "Demo/circuit-breaker"));
                Console.WriteLine($"Status code: {result.StatusCode}");
            }
        }
    }
}
