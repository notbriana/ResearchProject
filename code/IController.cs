namespace SalternPID
{
    // every controller should get what the sensor reads, the set point and the valve position now, and give back the valve position it wants (from 0 to 100)
    public interface IController
    {
        string Name { get; }
        double Decide(double measuredLevel, double setPoint, double currentValve);
    }
}