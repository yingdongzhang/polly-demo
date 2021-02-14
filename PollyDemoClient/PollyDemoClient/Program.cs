using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;
using Polly.Registry;
using Polly.Caching;
using Polly.Caching.Memory;

namespace PollyDemoClient
{
    enum Option
    {
        Retry,
        CircuitBreaker,
        Fallback,
        Timeout,
        Cache,
        Bulkhead
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            using IHost host = CreateHostBuilder(args).Build();
            var client = new Client(host.Services);

            var choice = -1;
            var demos = Enum.GetValues(typeof(Option));

            Console.WriteLine("Choose demo:");

            var options = new List<int>();
            for (int i = 0; i < demos.Length; i++)
            {
                Console.WriteLine($"{i}. " + demos.GetValue(i).ToString());
                options.Add(i);
            }

            while(!options.Contains(choice))
            {
                string input = Console.ReadLine();
                choice = int.Parse(input);
            }

            switch (choice)
            {
                case (int)Option.Retry:
                    await client.Demo1Retry();
                    break;
                case (int)Option.CircuitBreaker:
                    await client.Demo2CircuitBreaker();
                    break;
                case (int)Option.Fallback:
                    await client.AlwaysFail();
                    break;
                case (int)Option.Timeout:
                    await client.Timeout();
                    break;
                case (int)Option.Cache:
                    await client.Cache();
                    break;
                case (int)Option.Bulkhead:
                    await client.Bulkhead();
                    break;
                default:
                    break;
            };
        }

        static IHostBuilder CreateHostBuilder(string[] args)
        {
            var retryPolicy = HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .WaitAndRetryAsync(new[]
                    {
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(3)
                    },
                    onRetryAsync: (_, _, _) =>
                    {
                        Console.WriteLine($"Retrying...");
                        return Task.CompletedTask;
                    });

            var circuitBreakerPolicy = Policy.HandleResult<HttpResponseMessage>(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                    .CircuitBreakerAsync(
                        3,
                        TimeSpan.FromSeconds(2),
                        onBreak: (_, _) => { Console.WriteLine("Circuit open"); },
                        onReset: () => { Console.WriteLine("Circuit close"); },
                        onHalfOpen: () => { Console.WriteLine("Circuit half-open"); }
                    );

            var fallbackPolicy = Policy.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                    .Or<Exception>()
                    .FallbackAsync(
                        new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.NotFound,
                            Content = new StringContent("Not found")
                        },
                        onFallbackAsync: (_, _) => 
                        {
                            Console.WriteLine("Falling back to default response");
                            return Task.CompletedTask; 
                        }
                    )
                    .WrapAsync(retryPolicy)
                    .WrapAsync(circuitBreakerPolicy);

            var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(
                3,
                onTimeoutAsync: (_, _, _, _) => {
                    Console.WriteLine("Timeout");
                    return Task.CompletedTask;
                });

            var memoryCacheProvider = new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions()));
            var cachePolicy = Policy.CacheAsync<HttpResponseMessage>(memoryCacheProvider, TimeSpan.FromSeconds(2));

            var bulkheadPolicy = Policy.BulkheadAsync<HttpResponseMessage>(
                10,
                20,
                onBulkheadRejectedAsync: (_) =>
                {
                    Console.WriteLine("New requests are rejected");
                    return Task.CompletedTask;
                });

            var pollyRegistry = new PolicyRegistry
            {
                { "WaitAndRetryThreeTimesPolicy", retryPolicy },
                { "CircuitBreakerPolicy", circuitBreakerPolicy },
                { "FallbackPolicy", fallbackPolicy },
                { "TimeoutPolicy", timeoutPolicy },
                { "CachePolicy", cachePolicy },
                { "BulkheadPolicy", bulkheadPolicy }
            };

            return Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                {
                    services.AddPolicyRegistry(pollyRegistry);
                    services
                    .AddMemoryCache()
                    .AddLogging()
                    .AddHttpClient("Demo", client => {
                        client.BaseAddress = new Uri("https://localhost:5001/");
                        client.DefaultRequestHeaders.Add("Accept", "application/json");
                    })
                    .AddPolicyHandlerFromRegistry("FallbackPolicy")
                    .AddPolicyHandlerFromRegistry("TimeoutPolicy")
                    .AddPolicyHandlerFromRegistry("CachePolicy")
                    .AddPolicyHandlerFromRegistry("BulkheadPolicy");

                    services.AddSingleton<IAsyncCacheProvider>(memoryCacheProvider);
                    services.RemoveAll<IHttpMessageHandlerBuilderFilter>();
                });
        }
    }
}
