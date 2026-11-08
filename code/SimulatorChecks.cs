using System;
using System.Collections.Generic;

namespace SalternPID
{
    // before the experiments we check that the simulator does what the model says
    public static class SimulatorChecks
    {
        public static List<string> RunAll()
        {
            List<string> lines = new List<string>();

            Add(lines, "The valve at u0: the level does not change", CheckValveAtNull());
            Add(lines, "The valve at u0 + 10: the level goes up by about 2.2 per step", CheckValveAboveNull(lines));
            Add(lines, "The valve at u0 - 10: the level goes down by about 2.2 per step", CheckValveBelowNull(lines));
            Add(lines, "The level stays between 0 and 100", CheckLevelBounds());
            Add(lines, "The valve moves at most 3 percent per step", CheckValveSpeed());
            Add(lines, "The same seed gives exactly the same run", CheckSameSeed());
            Add(lines, "The noise of the sensor has the mean 0 and the standard deviation 0.5", CheckNoise(lines));

            // for the check with the main window
            lines.Add("");
            lines.Add("Compare with the main window: seed 1, controller 'Extended PID (ours)'.");
            lines.Add("Run each situation in the window and look at 'IAE' and 'Valve movement':");
            foreach (Scenario s in Scenarios.CreateAll())
            {
                RunMetrics m = Experiments.RunOne(new ExtendedPid(new PidSettings()), s, 1);
                lines.Add("  " + s.Name + ": IAE " + m.IAE.ToString("F0") + ", valve movement " + m.ValveMovement.ToString("F0")
                    + ", cancels " + m.Cancels);
            }

            return lines;
        }

        // puts "OK" or "FAIL" in front of the line
        private static void Add(List<string> lines, string text, bool passed)
        {
            lines.Add((passed ? "OK    " : "FAIL  ") + text);
        }

        private static bool CheckValveAtNull()
        {
            TankSimulator tank = new TankSimulator(1);
            tank.NullPosition = 50;

            double start = tank.Level;
            for (int i = 0; i < 100; i++)
            {
                tank.MoveValve(50);
                tank.Step();
            }

            return Math.Abs(tank.Level - start) < 0.000000001;
        }

        private static bool CheckValveAboveNull(List<string> lines)
        {
            double sum = 0;
            int count = 0;

            for (int seed = 1; seed <= 500; seed++)
            {
                TankSimulator tank = new TankSimulator(seed);
                tank.NullPosition = 40;   // the valve is at 50, so it is u0 + 10
                for (int i = 0; i < 20; i++)
                {
                    double before = tank.Level;
                    tank.Step();
                    sum += tank.Level - before;
                    count++;
                }
            }

            double mean = sum / count;
            lines.Add("      mean change per step: " + mean.ToString("F3"));
            return Math.Abs(mean - 2.2) < 0.05;
        }

        private static bool CheckValveBelowNull(List<string> lines)
        {
            double sum = 0;
            int count = 0;

            for (int seed = 1; seed <= 500; seed++)
            {
                TankSimulator tank = new TankSimulator(seed);

                // first we raise the level, so it has room to go down
                tank.NullPosition = 0;
                while (tank.Level < 70)
                {
                    tank.MoveValve(50);
                    tank.Step();
                }

                tank.NullPosition = 60;   // the valve is at 50, so it is u0 - 10
                for (int i = 0; i < 20; i++)
                {
                    double before = tank.Level;
                    tank.Step();
                    sum += tank.Level - before;
                    count++;
                }
            }

            double mean = sum / count;
            lines.Add("      mean change per step: " + mean.ToString("F3"));
            return Math.Abs(mean + 2.2) < 0.05;
        }

        private static bool CheckLevelBounds()
        {
            TankSimulator tank = new TankSimulator(7);
            tank.NullPosition = 20;
            Random random = new Random(3);

            for (int i = 0; i < 5000; i++)
            {
                tank.MoveValve(random.Next(0, 101));
                tank.Step();
                if (tank.Level < 0 || tank.Level > 100)
                    return false;
            }
            return true;
        }

        private static bool CheckValveSpeed()
        {
            TankSimulator tank = new TankSimulator(7);
            Random random = new Random(3);
            double last = tank.Valve;

            for (int i = 0; i < 5000; i++)
            {
                tank.MoveValve(random.Next(0, 101));
                if (Math.Abs(tank.Valve - last) > 3 + 0.000000001)
                    return false;
                last = tank.Valve;
            }
            return true;
        }

        private static bool CheckSameSeed()
        {
            TankSimulator a = new TankSimulator(5);
            TankSimulator b = new TankSimulator(5);

            for (int i = 0; i < 300; i++)
            {
                a.MoveValve(80);
                b.MoveValve(80);
                a.Step();
                b.Step();
                if (a.Level != b.Level)
                    return false;
            }
            return true;
        }

        private static bool CheckNoise(List<string> lines)
        {
            TankSimulator tank = new TankSimulator(9);
            tank.NoiseSigma = 0.5;

            int n = 20000;
            double sum = 0, sumSquares = 0;
            for (int i = 0; i < n; i++)
            {
                double noise = tank.ReadLevel() - tank.Level;
                sum += noise;
                sumSquares += noise * noise;
            }

            double mean = sum / n;
            double sigma = Math.Sqrt(sumSquares / n - mean * mean);
            lines.Add("      noise mean " + mean.ToString("F3") + ", standard deviation " + sigma.ToString("F3"));
            return Math.Abs(mean) < 0.02 && Math.Abs(sigma - 0.5) < 0.02;
        }
    }
}