using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace PollyDemoServer.Controllers
{
    [ApiController]
    [Route("demo")]
    public class DemoController : ControllerBase
    {
        private readonly ILogger<DemoController> _logger;
        private readonly IMemoryCache _memoryCache;

        public DemoController(ILogger<DemoController> logger, IMemoryCache memoryCache)
        {
            _logger = logger;
            _memoryCache = memoryCache;
        }

        [HttpGet("retry")]
        public IActionResult Retry()
        {
            var requestCountKey = "retry-request-count";
            var requestCount =_memoryCache.Get<int>(requestCountKey);
            
            _logger.LogInformation($"Processing request #{requestCount}");

            if (requestCount < 3)
            {
                _memoryCache.Set<int>(requestCountKey, requestCount + 1);
                throw new Exception("Internal Server Error");
            }

            _logger.LogInformation("Success");
            _memoryCache.Set<int>(requestCountKey, 0);
            return Ok(new List<string>{ "Hello", "World" });
        }

        [HttpGet("circuit-breaker")]
        public IActionResult CircuitBreaker()
        {
            var requestCountKey = "circuit-breaker-request-count";
            var requestCount = _memoryCache.Get<int>(requestCountKey);

            _logger.LogInformation($"Processing request #{requestCount}");

            if (requestCount < 6)
            {
                _logger.LogInformation("Receiving too many requests...");
                _memoryCache.Set<int>(requestCountKey, requestCount + 1);
                return new StatusCodeResult((int)HttpStatusCode.TooManyRequests);
            }

            _logger.LogInformation("Success");
            _memoryCache.Set<int>(requestCountKey, requestCount + 1);
            return Ok("OK");
        }

        [HttpGet("always-fail")]
        public IActionResult AlwaysFail()
        {
            throw new Exception("Internal Server Error");
        }

        [HttpGet("timeout")]
        public IActionResult Timeout()
        {
            Thread.Sleep(5000);
            return Ok("OK");
        }

        [HttpGet("cache")]
        public IActionResult Cache()
        {
            var requestCountKey = "cache-request-count";
            var requestCount = _memoryCache.Get<int>(requestCountKey);

            _logger.LogInformation($"Processing request #{requestCount}");

            if (requestCount < 1)
            {
                _memoryCache.Set<int>(requestCountKey, requestCount + 1);
                return Ok("OK");
            }

            _memoryCache.Set<int>(requestCountKey, requestCount + 1);
            return Ok("Good");
        }

        [HttpGet("bulkhead")]
        public IActionResult BulkHead()
        {
            var requestCountKey = "bulkhead-request-count";
            var requestCount = _memoryCache.Get<int>(requestCountKey);
            _logger.LogInformation($"Processing request #{requestCount}");
            _memoryCache.Set<int>(requestCountKey, requestCount + 1);
            Thread.Sleep(2500);
            return Ok("OK");
        }
    }
}
