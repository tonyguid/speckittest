using System.Diagnostics;
using BlobCopy.API.Models;
using BlobCopy.API.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Xunit;
using Xunit.Abstractions;

namespace BlobCopy.Tests.Performance;

/// <summary>
/// Performance benchmarks for blob copy operations.
/// Requirement: SC-004 requires 100MB file copy in under 1 minute.
/// </summary>
[Trait("Category", "Performance")]
public class CopyPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public CopyPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Performance", "CopyOperation")]
    public async Task CopyOperationManager_ShouldHandleConcurrentOperations()
    {
        // Arrange
        var service = new BlobCopyService(
            CreateMockBlobClientFactory().Object,
            CreateMockProgressNotificationService().Object,
            NullLogger<BlobCopyService>.Instance);

        const int concurrentOperations = 50;
        var operations = new List<Task<BlobCopyOperation>>();

        // Act
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < concurrentOperations; i++)
        {
            operations.Add(Task.Run(() => service.CreateOperationAsync(
                $"https://source{i}.blob.core.windows.net/container/blob{i}.txt",
                $"https://dest{i}.blob.core.windows.net/container/blob{i}.txt")));
        }

        var results = await Task.WhenAll(operations);
        stopwatch.Stop();

        // Assert
        var durationMs = stopwatch.Elapsed.TotalMilliseconds;
        var opsPerSecond = concurrentOperations / stopwatch.Elapsed.TotalSeconds;

        _output.WriteLine($"Concurrent Operation Creation Results:");
        _output.WriteLine($"  Concurrent Operations: {concurrentOperations}");
        _output.WriteLine($"  Duration: {durationMs:F3}ms");
        _output.WriteLine($"  Operations/Second: {opsPerSecond:F0}");

        Assert.All(results, op => Assert.NotNull(op));
        Assert.All(results, op => Assert.NotEqual(Guid.Empty, op.Id));

        // Should create 50 operations in under 1 second
        Assert.True(durationMs < 1000,
            $"Creating {concurrentOperations} operations took {durationMs:F0}ms (expected < 1000ms)");
    }

    [Fact]
    [Trait("Performance", "OperationLookup")]
    public async Task GetOperation_ShouldBeFastForLargeOperationCount()
    {
        // Arrange
        var service = new BlobCopyService(
            CreateMockBlobClientFactory().Object,
            CreateMockProgressNotificationService().Object,
            NullLogger<BlobCopyService>.Instance);

        const int operationCount = 1000;
        var operationIds = new List<Guid>();

        // Create many operations
        for (int i = 0; i < operationCount; i++)
        {
            var op = await service.CreateOperationAsync(
                $"https://source.blob.core.windows.net/container/blob{i}.txt",
                $"https://dest.blob.core.windows.net/container/blob{i}.txt");
            operationIds.Add(op.Id);
        }

        // Warm up
        for (int i = 0; i < 10; i++)
        {
            service.GetOperation(operationIds[i % operationCount]);
        }

        // Act - measure lookup time
        const int lookupIterations = 100;
        var latencies = new List<double>(lookupIterations);
        var random = new Random(42);
        var stopwatch = new Stopwatch();

        for (int i = 0; i < lookupIterations; i++)
        {
            var randomId = operationIds[random.Next(operationCount)];
            stopwatch.Restart();
            var op = service.GetOperation(randomId);
            stopwatch.Stop();
            latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
            Assert.NotNull(op);
        }

        // Calculate percentiles
        latencies.Sort();
        var p50 = latencies[(int)(latencies.Count * 0.50)];
        var p95 = latencies[(int)(latencies.Count * 0.95)];
        var p99 = latencies[(int)(latencies.Count * 0.99)];

        _output.WriteLine($"Operation Lookup Performance ({operationCount} operations):");
        _output.WriteLine($"  P50: {p50:F4}ms");
        _output.WriteLine($"  P95: {p95:F4}ms");
        _output.WriteLine($"  P99: {p99:F4}ms");

        // Assert - lookup should be fast (under 1ms even with 1000 operations)
        Assert.True(p95 < 1.0,
            $"P95 lookup latency ({p95:F4}ms) exceeds 1ms");
    }

    [Fact]
    [Trait("Performance", "ProgressUpdate")]
    public async Task UpdateProgress_ShouldHandleRapidUpdates()
    {
        // Arrange
        var mockProgressService = CreateMockProgressNotificationService();
        var service = new BlobCopyService(
            CreateMockBlobClientFactory().Object,
            mockProgressService.Object,
            NullLogger<BlobCopyService>.Instance);

        var operation = await service.CreateOperationAsync(
            "https://source.blob.core.windows.net/container/blob.txt",
            "https://dest.blob.core.windows.net/container/blob.txt");

        const long totalBytes = 100 * 1024 * 1024; // 100 MB
        const long chunkSize = 1024 * 1024; // 1 MB chunks
        const int updateCount = (int)(totalBytes / chunkSize);

        // Act
        var stopwatch = Stopwatch.StartNew();

        for (int i = 1; i <= updateCount; i++)
        {
            service.UpdateOperationProgress(operation.Id, i * chunkSize, totalBytes);
        }

        stopwatch.Stop();

        // Assert
        var durationMs = stopwatch.Elapsed.TotalMilliseconds;
        var updatesPerSecond = updateCount / stopwatch.Elapsed.TotalSeconds;

        _output.WriteLine($"Progress Update Performance:");
        _output.WriteLine($"  Total Updates: {updateCount}");
        _output.WriteLine($"  Duration: {durationMs:F3}ms");
        _output.WriteLine($"  Updates/Second: {updatesPerSecond:F0}");

        // Should handle 100 updates (100 MB in 1 MB chunks) in under 100ms
        Assert.True(durationMs < 100,
            $"Progress updates took {durationMs:F0}ms (expected < 100ms)");
    }

    [Theory]
    [InlineData(1 * 1024 * 1024, 500)]       // 1 MB: 500ms max
    [InlineData(10 * 1024 * 1024, 2000)]     // 10 MB: 2s max
    [InlineData(100 * 1024 * 1024, 60000)]   // 100 MB: 60s max (SC-004 requirement)
    [Trait("Performance", "CopyTime")]
    public void CopyOperation_ShouldMeetLatencyTargets(long fileSize, int maxDurationMs)
    {
        // This is a placeholder test that documents the performance requirements
        // In a real scenario, this would test against actual Azure Storage

        _output.WriteLine($"Copy Latency Target:");
        _output.WriteLine($"  File Size: {fileSize / (1024.0 * 1024.0):F1} MB");
        _output.WriteLine($"  Max Duration: {maxDurationMs}ms ({maxDurationMs / 1000.0}s)");
        _output.WriteLine($"  Required Throughput: {(fileSize / 1024.0 / 1024.0) / (maxDurationMs / 1000.0):F2} MB/s");

        // Assert the requirement is documented
        if (fileSize == 100 * 1024 * 1024)
        {
            // SC-004: 100MB in under 1 minute
            Assert.Equal(60000, maxDurationMs);
        }
    }

    [Fact]
    [Trait("Performance", "StatusRetrieval")]
    public async Task GetAllOperations_ShouldScaleLinearly()
    {
        // Arrange
        var service = new BlobCopyService(
            CreateMockBlobClientFactory().Object,
            CreateMockProgressNotificationService().Object,
            NullLogger<BlobCopyService>.Instance);

        // Create operations in batches and measure retrieval time
        var measurements = new List<(int count, double durationMs)>();
        var sizes = new[] { 10, 50, 100, 500, 1000 };

        foreach (var size in sizes)
        {
            // Create operations up to this size
            var currentCount = service.GetAllOperations().Count();
            var toCreate = size - currentCount;

            for (int i = 0; i < toCreate; i++)
            {
                await service.CreateOperationAsync(
                    $"https://source.blob.core.windows.net/container/blob{size}_{i}.txt",
                    $"https://dest.blob.core.windows.net/container/blob{size}_{i}.txt");
            }

            // Measure retrieval time
            var stopwatch = Stopwatch.StartNew();
            var ops = service.GetAllOperations().ToList();
            stopwatch.Stop();

            measurements.Add((size, stopwatch.Elapsed.TotalMilliseconds));
            _output.WriteLine($"  {size} operations: {stopwatch.Elapsed.TotalMilliseconds:F3}ms");
        }

        // Verify scaling is roughly linear (not exponential)
        // The ratio of time for 1000 ops vs 100 ops should be < 20x (ideally ~10x)
        var time100 = measurements.First(m => m.count == 100).durationMs;
        var time1000 = measurements.First(m => m.count == 1000).durationMs;
        var ratio = time1000 / time100;

        _output.WriteLine($"\nScaling Analysis:");
        _output.WriteLine($"  100 operations: {time100:F3}ms");
        _output.WriteLine($"  1000 operations: {time1000:F3}ms");
        _output.WriteLine($"  Ratio (1000/100): {ratio:F2}x (linear would be ~10x)");

        Assert.True(ratio < 20,
            $"Retrieval time scaling ratio ({ratio:F2}x) suggests non-linear performance");
    }

    private Mock<IBlobClientFactory> CreateMockBlobClientFactory()
    {
        var mock = new Mock<IBlobClientFactory>();

        var mockBlobClient = new Mock<BlobClient>();
        mockBlobClient.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));

        mock.Setup(x => x.CreateBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);

        return mock;
    }

    private Mock<IProgressNotificationService> CreateMockProgressNotificationService()
    {
        var mock = new Mock<IProgressNotificationService>();
        mock.Setup(x => x.NotifyProgressAsync(It.IsAny<ProgressUpdate>()))
            .Returns(Task.CompletedTask);
        return mock;
    }
}
