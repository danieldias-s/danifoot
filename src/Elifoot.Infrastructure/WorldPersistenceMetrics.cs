using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Elifoot.Infrastructure;

public sealed class WorldPersistenceMetrics : IDisposable
{
    private readonly Meter _meter = new("Elifoot.Multiplayer.Persistence");
    internal readonly Phase Serialization;
    internal readonly Phase Hydration;
    internal readonly Phase Transaction;
    internal readonly Phase History;
    internal readonly Phase Save;
    internal readonly Phase Commit;

    public WorldPersistenceMetrics()
    {
        Serialization = new(_meter, "serialization");
        Hydration = new(_meter, "hydration");
        Transaction = new(_meter, "transaction");
        History = new(_meter, "history");
        Save = new(_meter, "save");
        Commit = new(_meter, "commit");
    }

    public object Snapshot() => new
    {
        serialization = Serialization.Snapshot(), hydration = Hydration.Snapshot(),
        transaction = Transaction.Snapshot(), history = History.Snapshot(),
        save = Save.Snapshot(), commit = Commit.Snapshot()
    };

    public void Dispose() => _meter.Dispose();

    internal sealed class Phase(Meter meter, string name)
    {
        private readonly Histogram<double> _duration = meter.CreateHistogram<double>($"elifoot.world.{name}.duration", "ms");
        private long _count, _totalUs, _characters;

        internal void Record(long started, int characters = 0)
        {
            var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Interlocked.Increment(ref _count);
            Interlocked.Add(ref _totalUs, (long)(ms * 1000));
            Interlocked.Add(ref _characters, characters);
            _duration.Record(ms);
        }

        internal object Snapshot() => new
        {
            count = Interlocked.Read(ref _count), ms = Interlocked.Read(ref _totalUs) / 1000d,
            characters = Interlocked.Read(ref _characters)
        };
    }
}
