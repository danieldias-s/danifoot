namespace Elifoot.Application;

public interface IRoundTickTiming
{
    void Record(double simulationMs, double persistenceMs, double synchronizationMs);
}
