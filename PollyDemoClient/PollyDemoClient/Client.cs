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

        public async Task Retry()
        {
            var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "demo/retry"));
            LogResponse(await response.Content.ReadAsStringAsync());
        }

        public async Task CircuitBreaker()
        {
            Task[] tasks = new Task[6];
            
            for (int i = 0; i < 6; i++)
            {
                var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "demo/circuit-breaker"));
                LogResponse(await response.Content.ReadAsStringAsync());
                Thread.Sleep(1000 + i*500);
            }
        }

        public async Task Fallback()
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
                var response = await _cachePolicy.ExecuteAsync(
                    (_) => _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "demo/cache")),
                    context);
                LogResponse(await response.Content.ReadAsStringAsync());
                Thread.Sleep(300);
            }
        }

        public async Task Bulkhead()
        {
            Task[] tasks = new Task[12];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(async () => {
                    var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "demo/bulkhead"));
                    LogResponse(await response.Content.ReadAsStringAsync());
                });
            }
            await Task.WhenAll(tasks);
        }

        private static void LogResponse(string message)
        {
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " [Response] " + message);
        }
    }
}
