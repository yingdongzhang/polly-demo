using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly.Registry;
using Polly.Caching;
using Polly;

namespace PollyDemoClient
{
    class Client
    {
        private readonly HttpClient _httpClient;
        private readonly IAsyncPolicy<HttpResponseMessage> _cachePolicy;

        public Client(IServiceProvider services)
        {
            using IServiceScope serviceScope = services.CreateScope();
            IServiceProvider provider = serviceScope.ServiceProvider;

            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            _httpClient = httpClientFactory.CreateClient("Demo");

            var policyRegistry = provider.GetRequiredService<IReadOnlyPolicyRegistry<string>>();
            _cachePolicy = policyRegistry.Get<IAsyncPolicy<HttpResponseMessage>>("CachePolicy");      
        }

        public async Task Demo1Retry()
        {
            await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "demo/retry"));
        }

        public async Task Demo2CircuitBreaker()
        {
            Task[] tasks = new Task[15];
            
            for (int i = 0; i < 15; i++)
            {
                var result = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "demo/circuit-breaker"));
                Thread.Sleep(1000 + i*100);
            }
        }

        public async Task AlwaysFail()
        {
            await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "demo/always-fail"));
        }

        public async Task Timeout()
        {
            await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "demo/timeout"));
        }

        public async Task Cache()
        {
            Context context = new Context($"cache-key");
            for (int i = 0; i < 10; i++)
            {
                var result = await _cachePolicy.ExecuteAsync(
                    (_) => _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "demo/cache")),
                    context);
                Console.WriteLine(await result.Content.ReadAsStringAsync());
                Thread.Sleep(300);
            }
        }

        public async Task Bulkhead()
        {
            Task[] tasks = new Task[35];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "demo/bulkhead"));
            }
            await Task.WhenAll(tasks);
        }
    }
}
