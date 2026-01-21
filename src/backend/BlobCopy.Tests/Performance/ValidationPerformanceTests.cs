using System.Diagnostics;
using BlobCopy.API.Services;
using Xunit;
using Xunit.Abstractions;

namespace BlobCopy.Tests.Performance;

/// <summary>
/// Performance benchmarks for the /validate endpoint.
/// Requirement: SC-001 requires p95 latency under 200ms.
/// </summary>
[Trait("Category", "Performance")]
public class ValidationPerformanceTests
{
    private readonly ITestOutputHelper _output;
    private readonly BlobValidationService _validationService;

    public ValidationPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _validationService = new BlobValidationService();
    }

    [Fact]
    [Trait("Performance", "P95Latency")]
    public async Task ValidateUri_ShouldCompleteUnder200ms_P95()
    {
        // Arrange
        const int iterations = 100;
        const double targetP95Ms = 200.0;
        var latencies = new List<double>(iterations);

        var testUris = new[]
        {
            "https://account.blob.core.windows.net/container/blob.txt",
            "https://storageaccount123.blob.core.windows.net/my-container/path/to/blob.vhd",
            "https://prod.blob.core.windows.net/data/exports/2024/01/file.csv",
            "", // Invalid - should still be fast
            "http://invalid.com/blob", // Invalid - should still be fast
        };

        // Warm up
        for (int i = 0; i < 10; i++)
        {
            foreach (var uri in testUris)
            {
                await _validationService.ValidateUriFormatAsync(uri);
            }
        }

        // Act
        var stopwatch = new Stopwatch();
        for (int i = 0; i < iterations; i++)
        {
            var uri = testUris[i % testUris.Length];
            stopwatch.Restart();
            await _validationService.ValidateUriFormatAsync(uri);
            stopwatch.Stop();
            latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        // Calculate percentiles
        latencies.Sort();
        var p50 = latencies[(int)(latencies.Count * 0.50)];
        var p95 = latencies[(int)(latencies.Count * 0.95)];
        var p99 = latencies[(int)(latencies.Count * 0.99)];
        var avg = latencies.Average();
        var min = latencies.Min();
        var max = latencies.Max();

        // Output results
        _output.WriteLine($"URI Validation Performance Results ({iterations} iterations):");
        _output.WriteLine($"  Min: {min:F3}ms");
        _output.WriteLine($"  Avg: {avg:F3}ms");
        _output.WriteLine($"  P50: {p50:F3}ms");
        _output.WriteLine($"  P95: {p95:F3}ms");
        _output.WriteLine($"  P99: {p99:F3}ms");
        _output.WriteLine($"  Max: {max:F3}ms");
        _output.WriteLine($"  Target P95: {targetP95Ms}ms");

        // Assert
        Assert.True(p95 < targetP95Ms,
            $"P95 latency ({p95:F3}ms) exceeds target ({targetP95Ms}ms)");
    }

    [Fact]
    [Trait("Performance", "Throughput")]
    public async Task ValidateUri_ShouldHandleHighThroughput()
    {
        // Arrange
        const int totalRequests = 1000;
        const int concurrencyLevel = 10;
        var uri = "https://account.blob.core.windows.net/container/blob.txt";

        // Act
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task>();

        for (int i = 0; i < concurrencyLevel; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < totalRequests / concurrencyLevel; j++)
                {
                    await _validationService.ValidateUriFormatAsync(uri);
                }
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Calculate throughput
        var durationSeconds = stopwatch.Elapsed.TotalSeconds;
        var requestsPerSecond = totalRequests / durationSeconds;

        // Output results
        _output.WriteLine($"Throughput Test Results:");
        _output.WriteLine($"  Total Requests: {totalRequests}");
        _output.WriteLine($"  Concurrency: {concurrencyLevel}");
        _output.WriteLine($"  Duration: {durationSeconds:F3}s");
        _output.WriteLine($"  Throughput: {requestsPerSecond:F0} req/s");

        // Assert - should handle at least 1000 req/s
        Assert.True(requestsPerSecond > 1000,
            $"Throughput ({requestsPerSecond:F0} req/s) is below minimum (1000 req/s)");
    }

    [Fact]
    [Trait("Performance", "Memory")]
    public async Task ValidateUri_ShouldNotLeakMemory()
    {
        // Arrange
        const int iterations = 10000;
        var uri = "https://account.blob.core.windows.net/container/blob.txt";

        // Get baseline memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var baselineMemory = GC.GetTotalMemory(true);

        // Act
        for (int i = 0; i < iterations; i++)
        {
            await _validationService.ValidateUriFormatAsync(uri);
        }

        // Get final memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);

        var memoryIncrease = finalMemory - baselineMemory;
        var memoryIncreaseKb = memoryIncrease / 1024.0;

        // Output results
        _output.WriteLine($"Memory Test Results:");
        _output.WriteLine($"  Iterations: {iterations}");
        _output.WriteLine($"  Baseline Memory: {baselineMemory / 1024.0:F2} KB");
        _output.WriteLine($"  Final Memory: {finalMemory / 1024.0:F2} KB");
        _output.WriteLine($"  Memory Increase: {memoryIncreaseKb:F2} KB");
        _output.WriteLine($"  Per-Request Overhead: {memoryIncrease / (double)iterations:F2} bytes");

        // Assert - memory increase should be reasonable (less than 10MB for 10K requests)
        const long maxAllowedIncrease = 10 * 1024 * 1024; // 10 MB
        Assert.True(memoryIncrease < maxAllowedIncrease,
            $"Memory increase ({memoryIncreaseKb / 1024.0:F2} MB) exceeds maximum ({maxAllowedIncrease / 1024.0 / 1024.0} MB)");
    }
}
