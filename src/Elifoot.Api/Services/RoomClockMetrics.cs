using System.Diagnostics.Metrics;
using Elifoot.Application;
using Elifoot.Infrastructure;

namespace Elifoot.Api.Services;

public sealed class RoomClockMetrics : IDisposable, IRoundTickTiming
{
    private readonly Meter _meter = new("Elifoot.Multiplayer.Clock");
    private readonly Histogram<double> _wait;
    private readonly Histogram<double> _duration;
    private readonly Histogram<double> _simulation;
    private readonly Histogram<double> _persistence;
    private readonly Histogram<double> _synchronization;
    private readonly Counter<long> _failures;
    private readonly Counter<long> _recoveries;
    private readonly RoomConnectionRegistry _presence;
    private int _active, _executing, _waiting;
    private int _peakExecuting, _peakWaiting;
    private long _completed, _failed, _recovered, _waitUs, _executionUs;
    private long _timedTicks, _simulationUs, _persistenceUs, _synchronizationUs;

    public RoomClockMetrics(RoomConnectionRegistry presence)
    {
        _presence = presence;
        _wait = _meter.CreateHistogram<double>("elifoot.clock.queue.wait", "ms");
        _duration = _meter.CreateHistogram<double>("elifoot.clock.execution.duration", "ms");
        _simulation = _meter.CreateHistogram<double>("elifoot.clock.tick.simulation", "ms");
        _persistence = _meter.CreateHistogram<double>("elifoot.clock.tick.persistence", "ms");
        _synchronization = _meter.CreateHistogram<double>("elifoot.clock.tick.synchronization", "ms");
        _failures = _meter.CreateCounter<long>("elifoot.clock.failures");
        _recoveries = _meter.CreateCounter<long>("elifoot.clock.recoveries");
        _meter.CreateObservableGauge("elifoot.clock.worlds.active", () => Volatile.Read(ref _active));
        _meter.CreateObservableGauge("elifoot.clock.executing", () => Volatile.Read(ref _executing));
        _meter.CreateObservableGauge("elifoot.clock.waiting", () => Volatile.Read(ref _waiting));
        _meter.CreateObservableGauge("elifoot.world.cache.retained", () => EfWorldRepository.CacheCounts.Retained);
        _meter.CreateObservableGauge("elifoot.signalr.connections", () => _presence.ConnectionCount);
        _meter.CreateObservableGauge("elifoot.process.working_set", () => Environment.WorkingSet, "By");
    }

    internal void SetActive(int value) => Volatile.Write(ref _active, value);
    internal void Enqueue() => Interlocked.Increment(ref _waiting);
    internal void Accepted(int queued) => Max(ref _peakWaiting, queued);
    internal void RemoveWaiting() => Interlocked.Decrement(ref _waiting);
    internal void Start(double ms)
    {
        RemoveWaiting();
        Max(ref _peakExecuting, Interlocked.Increment(ref _executing));
        Interlocked.Add(ref _waitUs, (long)(ms * 1000));
        _wait.Record(ms);
    }

    internal void Finish(double ms, bool success, bool recovery)
    {
        Interlocked.Decrement(ref _executing);
        if (success) Interlocked.Increment(ref _completed);
        Interlocked.Add(ref _executionUs, (long)(ms * 1000));
        _duration.Record(ms);
        if (recovery)
        {
            Interlocked.Increment(ref _recovered);
            _recoveries.Add(1);
        }
    }

    internal void Fail()
    {
        Interlocked.Increment(ref _failed);
        _failures.Add(1);
    }

    public void Record(double simulationMs, double persistenceMs, double synchronizationMs)
    {
        Interlocked.Add(ref _simulationUs, (long)(simulationMs * 1000));
        Interlocked.Add(ref _persistenceUs, (long)(persistenceMs * 1000));
        Interlocked.Add(ref _synchronizationUs, (long)(synchronizationMs * 1000));
        Interlocked.Increment(ref _timedTicks);
        _simulation.Record(simulationMs);
        _persistence.Record(persistenceMs);
        _synchronization.Record(synchronizationMs);
    }

    private static void Max(ref int location, int candidate)
    {
        int previous = Volatile.Read(ref location);
        while (candidate > previous)
        {
            int raced = Interlocked.CompareExchange(ref location, candidate, previous);
            if (raced == previous) return;
            previous = raced;
        }
    }

    public object Snapshot()
    {
        var gc = GC.GetGCMemoryInfo();
        var generationInfo = gc.GenerationInfo;
        return new
        {
            active = Volatile.Read(ref _active), executing = Volatile.Read(ref _executing),
            waiting = Volatile.Read(ref _waiting), completed = Interlocked.Read(ref _completed),
            peakExecuting = Volatile.Read(ref _peakExecuting), peakWaiting = Volatile.Read(ref _peakWaiting),
            failed = Interlocked.Read(ref _failed), recovered = Interlocked.Read(ref _recovered),
            waitMs = Interlocked.Read(ref _waitUs) / 1000d, executionMs = Interlocked.Read(ref _executionUs) / 1000d,
            timedTicks = Interlocked.Read(ref _timedTicks),
            simulationMs = Interlocked.Read(ref _simulationUs) / 1000d,
            persistenceMs = Interlocked.Read(ref _persistenceUs) / 1000d,
            synchronizationMs = Interlocked.Read(ref _synchronizationUs) / 1000d,
            connections = _presence.ConnectionCount, cacheRetained = EfWorldRepository.CacheCounts.Retained,
            cacheIdentities = EfWorldRepository.CacheCounts.Identities, rss = Environment.WorkingSet,
            allocated = GC.GetTotalAllocatedBytes(), managed = GC.GetTotalMemory(false),
            heap = gc.HeapSizeBytes, loh = generationInfo.Length > 3 ? generationInfo[3].SizeAfterBytes : 0,
            gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2)
        };
    }

    public void Dispose() => _meter.Dispose();
}
