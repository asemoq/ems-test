using Gs1Marking.Server.Application;
using Gs1Marking.Server.Options;
using Gs1Marking.Contract.Gs1;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gs1Marking.Tests;

/// <summary>
/// Юнит-тесты broadcaster'а, доказывающие, что бизнес-логика тестируется без сети.
/// </summary>
public class MarkingCodeBroadcasterTests
{
    [Fact]
    public async Task Broadcaster_PublishesGeneratedCode_ToSubscriber()
    {
        var broadcaster = new MarkingCodeBroadcaster(
            new FakeGenerator("CODE-1"),
            Options.Create(new MarkingOptions { GenerationIntervalMs = 20 }),
            NullLogger<MarkingCodeBroadcaster>.Instance);

        var subscriber = new FakeSubscriber();
        broadcaster.Subscribe(subscriber);

        await broadcaster.StartAsync(CancellationToken.None);
        try
        {
            var code = await subscriber.Received.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("CODE-1", code);
        }
        finally
        {
            await broadcaster.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Broadcaster_FailureOfOneSubscriber_DoesNotStopOthers()
    {
        var broadcaster = new MarkingCodeBroadcaster(
            new FakeGenerator("CODE-2"),
            Options.Create(new MarkingOptions { GenerationIntervalMs = 20 }),
            NullLogger<MarkingCodeBroadcaster>.Instance);

        var healthy = new FakeSubscriber();
        var broken = new ThrowingSubscriber();
        broadcaster.Subscribe(broken);
        broadcaster.Subscribe(healthy);

        await broadcaster.StartAsync(CancellationToken.None);
        try
        {
            var code = await healthy.Received.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("CODE-2", code);
        }
        finally
        {
            await broadcaster.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Broadcaster_DoesNotPublishToUnsubscribedSubscriber()
    {
        var broadcaster = new MarkingCodeBroadcaster(
            new FakeGenerator("CODE-3"),
            Options.Create(new MarkingOptions { GenerationIntervalMs = 20 }),
            NullLogger<MarkingCodeBroadcaster>.Instance);

        var subscriber = new FakeSubscriber();
        broadcaster.Subscribe(subscriber);
        broadcaster.Unsubscribe(subscriber.Id);

        // Запускаем и даём таймеру сработать; подписчик должен остаться пустым.
        await broadcaster.StartAsync(CancellationToken.None);
        try
        {
            await Task.Delay(100);
            Assert.False(subscriber.Received.IsCompleted);
        }
        finally
        {
            await broadcaster.StopAsync(CancellationToken.None);
        }
    }

    private sealed class FakeGenerator(string code) : IGs1CodeGenerator
    {
        public string Generate() => code;
    }

    private sealed class FakeSubscriber : ICodeSubscriber
    {
        private readonly TaskCompletionSource<string> _received = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> Received => _received.Task;

        public Guid Id { get; } = Guid.NewGuid();

        public Task Completion { get; } = Task.CompletedTask;

        public ValueTask PublishAsync(string code, CancellationToken cancellationToken = default)
        {
            _received.TrySetResult(code);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ThrowingSubscriber : ICodeSubscriber
    {
        public Guid Id { get; } = Guid.NewGuid();

        public Task Completion { get; } = Task.CompletedTask;

        public ValueTask PublishAsync(string code, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Boom");

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
