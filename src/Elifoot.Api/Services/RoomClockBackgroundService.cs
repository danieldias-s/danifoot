using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Infrastructure;

namespace Elifoot.Api.Services;

public sealed class RoomClockBackgroundService(
    IServiceProvider services, IConfiguration configuration, RoomClockMetrics metrics,
    ILogger<RoomClockBackgroundService> logger) : BackgroundService
{
    private sealed record Work(Guid WorldId, long Enqueued, bool Recovery, bool Pause);
    private readonly ConcurrentDictionary<Guid, long> _nextByWorld = new();
    private readonly ConcurrentDictionary<Guid, byte> _scheduled = new();
    private long _logAfter;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (configuration.GetValue<bool>("Elifoot:DisableBackgroundClock")) return;
        int concurrency = configuration.GetValue<int>("Elifoot:ClockConcurrency", 1);
        int capacity = configuration.GetValue<int>("Elifoot:ClockQueueCapacity", 64);
        if (concurrency is < 1 or > 10 || capacity is < 1 or > 4096)
            throw new InvalidOperationException("Configure ClockConcurrency (1–10) e ClockQueueCapacity (1–4096).");
        var queue = Channel.CreateBounded<Work>(new BoundedChannelOptions(capacity) { SingleWriter = true });
        var authority = services.GetService<WorldAuthority>();
        var workers = Enumerable.Range(0, concurrency)
            .Select(_ => Consume(queue.Reader, authority, stoppingToken)).ToArray();
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
            do
            {
                if (authority is { IsActive: false }) break;
                try
                {
                    var repo = services.GetRequiredService<IWorldRepository>();
                    var worlds = await repo.ListWorldsForClockAsync(stoppingToken);
                    var now = Environment.TickCount64;
                    var active = worlds.Select(w => w.Id).ToHashSet();
                    metrics.SetActive(active.Count);
                    foreach (var id in _nextByWorld.Keys)
                        if (!active.Contains(id) && !_scheduled.ContainsKey(id)) _nextByWorld.TryRemove(id, out _);
                    foreach (var world in worlds.OrderBy(w => _nextByWorld.GetValueOrDefault(w.Id)))
                    {
                        if (_nextByWorld.GetValueOrDefault(world.Id) > now) break;
                        bool recovery = world.RoundCheckpointNeedsSave || world.PendingWorldDataChange != null;
                        bool running = world.RoundState is RoundState.RUNNING_FIRST_HALF
                            or RoundState.RUNNING_SECOND_HALF or RoundState.RUNNING_EXTRA_TIME;
                        bool pause = !running && !recovery && world.ActivePauseWindowId != null;
                        if ((!running && !recovery && !pause) || !_scheduled.TryAdd(world.Id, 0)) continue;
                        var work = new Work(world.Id, Stopwatch.GetTimestamp(), recovery, pause);
                        long due = _nextByWorld.GetValueOrDefault(world.Id);
                        int interval = recovery ? 2000 : pause ? 50 : Math.Max(40, world.TickIntervalMs);
                        _nextByWorld[world.Id] = now + interval;
                        metrics.Enqueue();
                        if (!queue.Writer.TryWrite(work))
                        {
                            metrics.RemoveWaiting();
                            _nextByWorld[world.Id] = due;
                            _scheduled.TryRemove(world.Id, out _);
                            break;
                        }
                        metrics.Accepted(queue.Reader.Count);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception failure) { Report(failure); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            queue.Writer.TryComplete();
            await Task.WhenAll(workers);
            while (queue.Reader.TryRead(out _)) metrics.RemoveWaiting();
            metrics.SetActive(0);
        }
    }

    private async Task Consume(ChannelReader<Work> queue, WorldAuthority? authority, CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var work in queue.ReadAllAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested || authority is { IsActive: false })
                {
                    metrics.RemoveWaiting();
                    return;
                }
                var start = Stopwatch.GetTimestamp();
                metrics.Start(Stopwatch.GetElapsedTime(work.Enqueued, start).TotalMilliseconds);
                bool succeeded = false;
                try
                {
                    using var scope = services.CreateScope();
                    var orchestrator = scope.ServiceProvider.GetRequiredService<MultiplayerRoundOrchestrator>();
                    if (work.Pause)
                        await orchestrator.ForceExpirePauseWindowIfTimedOutAsync(work.WorldId, false, stoppingToken);
                    else
                        await orchestrator.AdvanceClockTickAsync(work.WorldId, stoppingToken);
                    succeeded = true;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception failure)
                {
                    _nextByWorld[work.WorldId] = Environment.TickCount64 + 2000;
                    Report(failure);
                }
                finally
                {
                    metrics.Finish(Stopwatch.GetElapsedTime(start).TotalMilliseconds, succeeded, succeeded && work.Recovery);
                    _scheduled.TryRemove(work.WorldId, out _);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private void Report(Exception failure)
    {
        metrics.Fail();
        var previous = Interlocked.Read(ref _logAfter);
        var now = Environment.TickCount64;
        if (now >= previous && Interlocked.CompareExchange(ref _logAfter, now + 30000, previous) == previous)
            logger.LogWarning("Falha no relógio de sala ({FailureType}). A recuperação será tentada novamente.",
                failure.GetType().Name);
    }
}
