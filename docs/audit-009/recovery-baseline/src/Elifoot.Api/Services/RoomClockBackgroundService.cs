using System.Collections.Concurrent;
using Elifoot.Application;
using Elifoot.Contracts;

namespace Elifoot.Api.Services;

/// <summary>
/// Single background orchestrator loop for all active rooms.
/// Avoids creating thousands of separate per-match timers; advances each room's shared clock
/// according to its configured TickIntervalMs when in a running state, and enforces pause window timeouts.
/// </summary>
public sealed class RoomClockBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<Guid, DateTime> _lastTickByWorld = new();

    public RoomClockBackgroundService(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool disableAutoClock = _configuration.GetValue<bool>("Elifoot:DisableBackgroundClock");
        if (disableAutoClock) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IWorldRepository>();
                var orchestrator = scope.ServiceProvider.GetRequiredService<MultiplayerRoundOrchestrator>();

                var worlds = await repo.ListWorldsAsync(stoppingToken);
                var now = DateTime.UtcNow;

                foreach (var world in worlds)
                {
                    if (world.RoundState is RoundState.RUNNING_FIRST_HALF or RoundState.RUNNING_SECOND_HALF or RoundState.RUNNING_EXTRA_TIME)
                    {
                        var lastTick = _lastTickByWorld.GetValueOrDefault(world.Id, DateTime.MinValue);
                        if ((now - lastTick).TotalMilliseconds >= Math.Max(40, world.TickIntervalMs))
                        {
                            _lastTickByWorld[world.Id] = now;
                            await orchestrator.AdvanceClockTickAsync(world.Id, stoppingToken);
                        }
                    }
                    else if (world.ActivePauseWindowId != null)
                    {
                        await orchestrator.ForceExpirePauseWindowIfTimedOutAsync(world.Id, ignoreClockForTest: false, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Keep background loop alive
            }

            await Task.Delay(50, stoppingToken);
        }
    }
}
