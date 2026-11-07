namespace SalternPID
{
    // one test situation; it tells us, at every step, what the set point is, where the null position u0 is, and how much rain or evaporation there is
    public class Scenario
    {
        public string Name;
        public string Description;

        public int Steps = 400;        // how long a run is
        public int EventStep = 150;    // the step when the event happens

        public double NoiseSigma = 0;  // noise of the level sensor

        // the set point before and after the event
        public double SetPointBefore = 74;
        public double SetPointAfter = 74;

        // the null position u0 of the valve before and after the event
        public double NullBefore = 50;
        public double NullAfter = 50;

        // rain: this value is added to the level at every step from "RainFrom" to "RainTo" (not included)
        public double RainValue = 0;
        public int RainFrom = 0;
        public int RainTo = 0;

        // evaporation: the same, but with a negative value
        public double EvaporationValue = 0;
        public int EvaporationFrom = 0;
        public int EvaporationTo = 0;

        public double SetPoint(int step)
        {
            if (step < EventStep) return SetPointBefore;
            return SetPointAfter;
        }

        public double NullPosition(int step)
        {
            if (step < EventStep) return NullBefore;
            return NullAfter;
        }

        public double Disturbance(int step)
        {
            if (step >= RainFrom && step < RainTo) return RainValue;
            if (step >= EvaporationFrom && step < EvaporationTo) return EvaporationValue;
            return 0;
        }
    }
}