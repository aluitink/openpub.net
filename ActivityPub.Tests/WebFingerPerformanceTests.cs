using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using System.Text.Json;
using Xunit;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using ActivityPub.Core.Models;
using ActivityPub.Core;

namespace ActivityPub.Tests;

public class WebFingerPerformanceTests
{
    [Fact]
    public async Task WebFinger_Cache_Hit_Rate_Benchmark_Test()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        
        // Warm up the endpoint
        await client.GetAsync("/.well-known/webfinger?resource=acct:warmup@localhost");
        
        // Act - Run multiple requests to measure cache hit rate
        var stopwatch = Stopwatch.StartNew();
        const int requestCount = 1000;
        
        // Make requests to the same resource repeatedly to test caching
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < requestCount; i++)
        {
            var task = client.GetAsync($"/.well-known/webfinger?resource=acct:test@localhost");
            tasks.Add(task);
        }
        
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        // Simple validation: check that all responses are successful
        foreach (var response in results)
        {
            Assert.True(response.IsSuccessStatusCode);
        }
        
        // Assert
        var averageTime = stopwatch.ElapsedMilliseconds / requestCount;
        Assert.True(averageTime < 100, $"Average response time should be under 100ms, but was {averageTime}ms");
    }
    
    [Fact]
    public async Task WebFinger_Serialization_Performance_Benchmark()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        
        // Warm up
        await client.GetAsync("/.well-known/webfinger?resource=acct:warmup@localhost");
        
        // Act - Measure serialization performance
        var stopwatch = Stopwatch.StartNew();
        const int requestCount = 100;
        
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < requestCount; i++)
        {
            var task = client.GetAsync($"/.well-known/webfinger?resource=acct:serializetest{i}@localhost");
            tasks.Add(task);
        }
        
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        // Assert
        var averageTime = stopwatch.ElapsedMilliseconds / requestCount;
        Assert.True(averageTime < 50, $"Average serialization response time should be under 50ms, but was {averageTime}ms");
        
        // Validate response format
        foreach (var response in results)
        {
            Assert.True(response.IsSuccessStatusCode);
            var content = await response.Content.ReadAsStringAsync();
            
            // Parse the JSON to validate structure
            var jrd = JsonSerializer.Deserialize<WebFingerJrd>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            Assert.NotNull(jrd);
            Assert.NotEmpty(jrd.Links);
            Assert.Equal("self", jrd.Links[0].Rel);
        }
    }
    
    [Fact]
    public async Task WebFinger_Comprehensive_Benchmark()
    {
        // Arrange
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        
        // Warm up the endpoint
        await client.GetAsync("/.well-known/webfinger?resource=acct:warmup@localhost");
        
        // Act - Run comprehensive performance test
        var stopwatch = Stopwatch.StartNew();
        const int requestCount = 1000;
        
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < requestCount; i++)
        {
            var task = client.GetAsync($"/.well-known/webfinger?resource=acct:benchmark{i}@localhost");
            tasks.Add(task);
        }
        
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        // Assert
        var totalTime = stopwatch.ElapsedMilliseconds;
        var averageTime = totalTime / requestCount;
        
        // These thresholds should be achieved with our optimizations
        Assert.True(totalTime < 10000, $"Total time should be under 10 seconds, but was {totalTime}ms");
        Assert.True(averageTime < 10, $"Average response time should be under 10ms, but was {averageTime}ms");
        
        // Validate all responses are successful
        foreach (var response in results)
        {
            Assert.True(response.IsSuccessStatusCode);
        }
    }
}