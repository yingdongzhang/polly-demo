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

            var options = new List<int>();
            for (int i = 0; i < demos.Length; i++)
            {
                options.Add(i);
            }

            while(!options.Contains(choice))
            {
                for (int i = 0; i < demos.Length; i++)
                {
                    Console.WriteLine($"{i}. " + demos.GetValue(i).ToString());
                }

                Console.WriteLine("Choose demo:");
                string input = Console.ReadLine();
                choice = int.Parse(input);

                switch (choice)
                {
                    case (int)Option.Retry:
                        await client.Retry();
                        break;
                    case (int)Option.CircuitBreaker:
                        await client.CircuitBreaker();
                        break;
                    case (int)Option.Fallback:
                        await client.Fallback();
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

                choice = -1;
            }
        }

        static void Log(string message)
        {
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + message);
        }

        static IHostBuilder CreateHostBuilder(string[] args)
        {
            // Retry Policy
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
                        Console.WriteLine("[Policy - retryPolicy] Retrying...");
                        return Task.CompletedTask;
                    });

            // Circuit Breaker Policy
            var circuitBreakerPolicy = Policy.HandleResult<HttpResponseMessage>(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                    .CircuitBreakerAsync(
                        3,
                        TimeSpan.FromSeconds(2),
                        onBreak: (_, _) => { Log("[Policy - circuitBreakerPolicy] Circuit open"); },
                        onReset: () => { Log("[Policy - circuitBreakerPolicy] Circuit close"); },
                        onHalfOpen: () => { Log("[Policy - circuitBreakerPolicy] Circuit half-open"); }
                    );

            // Fallback Policy
            var fallbackPolicy = Policy.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                    .Or<Exception>()
                    .FallbackAsync(
                        new HttpResponseMessage
                        {
                            Content = new StringContent("Default response")
                        },
                        onFallbackAsync: (_, _) => 
                        {
                            Log("[Policy - fallbackPolicy] Falling back to default response");
                            return Task.CompletedTask; 
                        }
                    )
                    .WrapAsync(retryPolicy)
                    .WrapAsync(circuitBreakerPolicy);

            // Timeout Policy
            var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(
                3,
                onTimeoutAsync: (_, _, _, _) => {
                    Log("[Policy - timeoutPolicy] Timeout");
                    return Task.CompletedTask;
                });

            // Cache Policy
            var memoryCacheProvider = new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions()));
            var cachePolicy = Policy.CacheAsync<HttpResponseMessage>(
                memoryCacheProvider,
                ttl: TimeSpan.FromSeconds(2),
                onCacheGet: (_, value) => {
                    Log($"[Policy - cachePolicy] Returning from cache {value}");
                },
                onCacheMiss: (_, _) => {},
                onCachePut: (_, _) => {},
                onCacheGetError: (_, _, _) => {},
                onCachePutError: (_, _, _) => {});

            // Bulkhead Policy
            var bulkheadPolicy = Policy.BulkheadAsync<HttpResponseMessage>(
                3,
                6,
                onBulkheadRejectedAsync: (_) =>
                {
                    Log("[Policy - bulkheadPolicy] New requests are rejected");
                    return Task.CompletedTask;
                });

            // Policy Registry
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
                    .AddLogging();

                    services.AddSingleton<IAsyncCacheProvider>(memoryCacheProvider);

                    services.AddHttpClient("Demo", client => {
                        client.BaseAddress = new Uri("https://localhost:5001/");
                        client.DefaultRequestHeaders.Add("Accept", "application/json");
                    })
                    .AddPolicyHandlerFromRegistry("FallbackPolicy")
                    .AddPolicyHandlerFromRegistry("TimeoutPolicy")
                    .AddPolicyHandlerFromRegistry("CachePolicy")
                    .AddPolicyHandlerFromRegistry("BulkheadPolicy");

                    services.RemoveAll<IHttpMessageHandlerBuilderFilter>();
                });
        }
    }
}
