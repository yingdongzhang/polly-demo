using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.CircuitBreaker;

namespace PollyDemoClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using IHost host = CreateHostBuilder(args).Build();
            var client = new Client(host.Services);

            var choice = 0;
            var demos = new List<int> { 1, 2, 3 };

            while (!demos.Contains((int)choice))
            {
                Console.WriteLine("Choose demo:");
                var choiceStr = Console.ReadLine();
                choice = int.Parse(choiceStr);
            }

            switch (choice)
            {
                case 1:
                    await client.Demo1Retry();
                    break;
                case 2:
                    await client.Demo2CircuitBreaker();
                    break;
                default:
                    break;
            };
        }

        static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) => services
                    .AddLogging()
                    .AddHttpClient("Demo", client => {
                        client.BaseAddress = new Uri("https://localhost:5001/");
                        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                    })
                    .AddTransientHttpErrorPolicy(builder => builder.WaitAndRetryAsync(new[]
                    {
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(3)
                    }))
                    .AddPolicyHandler(Policy
                        .HandleResult<HttpResponseMessage>(response => response.StatusCode == HttpStatusCode.TooManyRequests)
                        .CircuitBreakerAsync(3, durationOfBreak: TimeSpan.FromSeconds(3)))
                );
    }
}
