using System;

namespace SalternPID
{
   
    // level(k+1) = level(k) + r * (valve - nullPosition) + disturbance
    // r is a random number between 0.05 and 0.39 (new at every step).
    // The valve can move maximum 3 percent in one step.
    // The level stays between 0 and 100.
    public class TankSimulator
    {
        private Random _gainRandom;   // gives r
        private Random _noiseRandom;  // gives the noise of the sensor

        // the real level of the water, from 0 to 100
        public double Level { get; private set; }

        // the real position of the valve, from 0 to 100
        public double Valve { get; private set; }

        // the valve position where the level does not change (the controller does not know it)
        public double NullPosition { get; set; }

        // rain (positive) or evaporation (negative), added to the level at every step
        public double Disturbance { get; set; }

        // noise of the level sensor (standard deviation), 0 = no noise
        public double NoiseSigma { get; set; }

        // maximum valve movement in one step
        public double MaxValveStep { get; set; }

        public TankSimulator(int seed)
        {
            Reset(seed);
        }

        // starts again with the start values of the program: level 10, valve 50, null position 50
        public void Reset(int seed)
        {
            _gainRandom = new Random(seed);
            _noiseRandom = new Random(seed * 7919 + 13);

            Level = 10;
            Valve = 50;
            NullPosition = 50;
            Disturbance = 0;
            NoiseSigma = 0;
            MaxValveStep = 3;
        }

        // what the controller sees: the level plus the noise of the sensor
        public double ReadLevel()
        {
            if (NoiseSigma <= 0)
                return Level;

            return Level + NoiseSigma * NextGaussian();
        }

        // the controller asks for a valve position. the valve goes there, but it can move only MaxValveStep percent in one step
        public void MoveValve(double wantedPosition)
        {
            if (wantedPosition < 0) 
                wantedPosition = 0;
            if (wantedPosition > 100) 
                wantedPosition = 100;

            double difference = wantedPosition - Valve;

            if (Math.Abs(difference) <= MaxValveStep)
            {
                Valve = wantedPosition;
            }
            else if (difference > 0)
            {
                Valve = Valve + MaxValveStep;
            }
            else
            {
                Valve = Valve - MaxValveStep;
            }
        }

        // this is one step of the process
        public void Step()
        {
            // r is a whole number from 5 to 39, divided by 100
            double r = _gainRandom.Next(5, 40) / 100.0;

            double newLevel = Level + r * (Valve - NullPosition) + Disturbance;

            // if the new level is outside [0, 100] we keep the old level
            if (newLevel >= 0 && newLevel <= 100)
            {
                Level = newLevel;
            }
        }

        // a random number with mean 0 and standard deviation 1
        private double NextGaussian()
        {
            double u1 = 1.0 - _noiseRandom.NextDouble();
            double u2 = _noiseRandom.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}