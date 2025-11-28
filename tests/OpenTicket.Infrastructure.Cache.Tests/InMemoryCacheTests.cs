using Microsoft.Extensions.Options;
using OpenTicket.Infrastructure.Cache.Abstractions;
using OpenTicket.Infrastructure.Cache.InMemory;
using Shouldly;

namespace OpenTicket.Infrastructure.Cache.Tests;

public class InMemoryCacheTests : IDisposable
{
    private readonly InMemoryCache _cache;

    public InMemoryCacheTests()
    {
        var options = Options.Create(new CacheOptions
        {
            DefaultTtl = TimeSpan.FromMinutes(5),
            KeyPrefix = "test"
        });
        _cache = new InMemoryCache(options);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    [Fact]
    public async Task GetAsync_WhenKeyNotExists_ShouldReturnDefault()
    {
        // Act
        var result = await _cache.GetAsync<string>("nonexistent");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_AndGetAsync_ShouldStoreAndRetrieveValue()
    {
        // Arrange
        var key = "test-key";
        var value = new TestData { Id = 1, Name = "Test" };

        // Act
        await _cache.SetAsync(key, value);
        var result = await _cache.GetAsync<TestData>(key);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(1);
        result.Name.ShouldBe("Test");
    }

    [Fact]
    public async Task SetStringAsync_AndGetStringAsync_ShouldStoreAndRetrieveString()
    {
        // Arrange
        var key = "string-key";
        var value = "test-value";

        // Act
        await _cache.SetStringAsync(key, value);
        var result = await _cache.GetStringAsync(key);

        // Assert
        result.ShouldBe("test-value");
    }

    [Fact]
    public async Task RemoveAsync_WhenKeyExists_ShouldRemoveAndReturnTrue()
    {
        // Arrange
        var key = "remove-key";
        await _cache.SetStringAsync(key, "value");

        // Act
        var removed = await _cache.RemoveAsync(key);
        var exists = await _cache.ExistsAsync(key);

        // Assert
        removed.ShouldBeTrue();
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveAsync_WhenKeyNotExists_ShouldReturnFalse()
    {
        // Act
        var removed = await _cache.RemoveAsync("nonexistent");

        // Assert
        removed.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenKeyExists_ShouldReturnTrue()
    {
        // Arrange
        var key = "exists-key";
        await _cache.SetStringAsync(key, "value");

        // Act
        var exists = await _cache.ExistsAsync(key);

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenKeyNotExists_ShouldReturnFalse()
    {
        // Act
        var exists = await _cache.ExistsAsync("nonexistent");

        // Assert
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task SetIfNotExistsAsync_WhenKeyNotExists_ShouldSetAndReturnTrue()
    {
        // Arrange
        var key = "setnx-key";

        // Act
        var result = await _cache.SetIfNotExistsAsync(key, "value", TimeSpan.FromMinutes(1));
        var value = await _cache.GetStringAsync(key);

        // Assert
        result.ShouldBeTrue();
        value.ShouldBe("\"value\""); // JSON serialized
    }

    [Fact]
    public async Task SetIfNotExistsAsync_WhenKeyExists_ShouldNotSetAndReturnFalse()
    {
        // Arrange
        var key = "setnx-exists-key";
        await _cache.SetStringAsync(key, "original");

        // Act
        var result = await _cache.SetIfNotExistsAsync(key, "new-value", TimeSpan.FromMinutes(1));
        var value = await _cache.GetStringAsync(key);

        // Assert
        result.ShouldBeFalse();
        value.ShouldBe("original");
    }

    [Fact]
    public async Task RefreshAsync_WhenKeyExists_ShouldReturnTrue()
    {
        // Arrange
        var key = "refresh-key";
        await _cache.SetStringAsync(key, "value", TimeSpan.FromSeconds(10));

        // Act
        var refreshed = await _cache.RefreshAsync(key, TimeSpan.FromMinutes(5));

        // Assert
        refreshed.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshAsync_WhenKeyNotExists_ShouldReturnFalse()
    {
        // Act
        var refreshed = await _cache.RefreshAsync("nonexistent", TimeSpan.FromMinutes(5));

        // Assert
        refreshed.ShouldBeFalse();
    }

    [Fact]
    public async Task GetManyAsync_ShouldReturnMultipleValues()
    {
        // Arrange
        await _cache.SetAsync("multi-1", new TestData { Id = 1, Name = "One" });
        await _cache.SetAsync("multi-2", new TestData { Id = 2, Name = "Two" });

        // Act
        var results = await _cache.GetManyAsync<TestData>(["multi-1", "multi-2", "multi-3"]);

        // Assert
        results.Count.ShouldBe(3);
        results["multi-1"]!.Id.ShouldBe(1);
        results["multi-2"]!.Id.ShouldBe(2);
        results["multi-3"].ShouldBeNull();
    }

    [Fact]
    public async Task RemoveManyAsync_ShouldRemoveMultipleKeysAndReturnCount()
    {
        // Arrange
        await _cache.SetStringAsync("remove-1", "v1");
        await _cache.SetStringAsync("remove-2", "v2");

        // Act
        var removed = await _cache.RemoveManyAsync(["remove-1", "remove-2", "remove-3"]);

        // Assert
        removed.ShouldBe(2);
    }

    [Fact]
    public async Task GetAsync_WhenExpired_ShouldReturnDefault()
    {
        // Arrange
        var key = "expiring-key";
        await _cache.SetStringAsync(key, "value", TimeSpan.FromMilliseconds(50));

        // Wait for expiration
        await Task.Delay(100);

        // Act
        var result = await _cache.GetStringAsync(key);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExistsAsync_WhenExpired_ShouldReturnFalse()
    {
        // Arrange
        var key = "expiring-exists-key";
        await _cache.SetStringAsync(key, "value", TimeSpan.FromMilliseconds(50));

        // Wait for expiration
        await Task.Delay(100);

        // Act
        var exists = await _cache.ExistsAsync(key);

        // Assert
        exists.ShouldBeFalse();
    }

    private record TestData
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}