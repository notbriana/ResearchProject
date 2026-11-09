using System;
using System.Collections.Generic;

namespace SalternPID
{
    // we try many values on other random sequences (seeds 1001 to 1020) than the ones used for the results (seeds 1, 2, 3 ...)
    
    public static class Tuner
    {
        public const int FirstSeed = 1001;
        public const int Runs = 20;

        // how many times we compute a cost (for the progress bar): P 10, PI 80, PID 560, PID+ 560, and our controller 2 rounds x 34
        public const int TotalEvaluations = 10 + 80 + 560 + 560 + 68;

        private static readonly double[] Kps = { 0.5, 1, 1.5, 2, 3, 4, 6, 8, 10, 14 };
        private static readonly double[] Kis = { 0.02, 0.05, 0.1, 0.2, 0.4, 0.8, 1.6, 3.2 };
        private static readonly double[] Kds = { 0, 10, 25, 50, 100, 200, 350 };

        // the cost of a controller: the mean of IAE + 0.3 * valve movement
        private static double Cost(Func<IController> make, List<Scenario> scenarios)
        {
            double sum = 0;
            int count = 0;

            foreach (Scenario s in scenarios)
            {
                for (int i = 0; i < Runs; i++)
                {
                    RunMetrics m = Experiments.RunOne(make(), s, FirstSeed + i);
                    sum += m.IAE + 0.3 * m.ValveMovement;
                    count++;
                }
            }

            return sum / count;
        }

        // tries all the combinations of Kp, Ki and Kd (a grid search) for P, PI, PID or PID+
        private static string TuneBasic(string name, bool useKi, bool useKd, bool filter, bool antiWindup, List<Scenario> scenarios, Action onEvaluation)
        {
            double[] kis = useKi ? Kis : new double[] { 0 };
            double[] kds = useKd ? Kds : new double[] { 0 };

            double bestCost = double.MaxValue;
            double bestKp = 0, bestKi = 0, bestKd = 0;

            foreach (double kp in Kps)
            {
                foreach (double ki in kis)
                {
                    foreach (double kd in kds)
                    {
                        double a = kp, b = ki, c = kd;
                        double cost = Cost(() => new BasicPid(name, a, b, c, filter, antiWindup), scenarios);
                        if (onEvaluation != null) onEvaluation();

                        if (cost < bestCost)
                        {
                            bestCost = cost;
                            bestKp = kp;
                            bestKi = ki;
                            bestKd = kd;
                        }
                    }
                }
            }

            string text = name.PadRight(5) + "Kp = " + bestKp;
            if (useKi) text = text + ", Ki = " + bestKi;
            if (useKd) text = text + ", Kd = " + bestKd;
            return text + "   (cost " + bestCost.ToString("F0") + ")";
        }

        // the start point for our controller; we change one value at a time, and we keep it if the cost is smaller
        private static PidSettings StartSettings()
        {
            PidSettings s = new PidSettings();
            s.Kp = 2;
            s.Ki = 0.2;
            s.Kd = 50;
            s.ShapeP = 0.05;
            s.ShapeI = 0.005;
            s.ShapeD = 0.05;
            s.DeadBand = 0.5;
            return s;
        }

        private static double Evaluate(PidSettings settings, List<Scenario> scenarios)
        {
            return Cost(() => new ExtendedPid(settings.Copy()), scenarios);
        }

        // tries some values for one setting, and it keeps the best one
        private static PidSettings TryValues(PidSettings best, List<Scenario> scenarios, double[] values,
            Action<PidSettings, double> set, ref double bestCost, Action onEvaluation)
        {
            foreach (double value in values)
            {
                PidSettings p = best.Copy();
                set(p, value);

                double cost = Evaluate(p, scenarios);
                if (onEvaluation != null) onEvaluation();

                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = p;
                }
            }
            return best;
        }

        private static string TuneOurs(List<Scenario> scenarios, Action onEvaluation)
        {
            PidSettings best = StartSettings();
            double bestCost = Evaluate(best, scenarios);

            for (int round = 0; round < 2; round++)
            {
                best = TryValues(best, scenarios, new double[] { 2, 3, 4, 6, 8, 10, 14 }, (p, v) => p.Kp = v, ref bestCost, onEvaluation);
                best = TryValues(best, scenarios, new double[] { 0.05, 0.1, 0.2, 0.4, 0.8, 1.6 }, (p, v) => p.Ki = v, ref bestCost, onEvaluation);
                best = TryValues(best, scenarios, new double[] { 0, 10, 25, 50, 100, 200 }, (p, v) => p.Kd = v, ref bestCost, onEvaluation);
                best = TryValues(best, scenarios, new double[] { 0, 0.02, 0.05, 0.1 }, (p, v) => p.ShapeP = v, ref bestCost, onEvaluation);
                best = TryValues(best, scenarios, new double[] { 0, 0.002, 0.005, 0.01, 0.02 }, (p, v) => p.ShapeI = v, ref bestCost, onEvaluation);
                best = TryValues(best, scenarios, new double[] { 0, 0.05, 0.2 }, (p, v) => p.ShapeD = v, ref bestCost, onEvaluation);
                best = TryValues(best, scenarios, new double[] { 0.2, 0.5, 1.0 }, (p, v) => p.DeadBand = v, ref bestCost, onEvaluation);
            }

            return "Ours Kp = " + best.Kp + ", Ki = " + best.Ki + ", Kd = " + best.Kd
                + ", shapes of fp, fi, fd = " + best.ShapeP + " / " + best.ShapeI + " / " + best.ShapeD
                + ", dead-band = " + best.DeadBand + "   (cost " + bestCost.ToString("F0") + ")";
        }

        // tunes everything and gives back the text with the results.
        public static string TuneAll(Action onEvaluation)
        {
            List<Scenario> scenarios = Scenarios.CreateAll();

            List<string> lines = new List<string>();
            lines.Add("Tuned weights (seeds " + FirstSeed + " to " + (FirstSeed + Runs - 1)
                + ", situations S1 to S6, cost = IAE + 0.3 x valve movement)");
            lines.Add(TuneBasic("P", false, false, false, false, scenarios, onEvaluation));
            lines.Add(TuneBasic("PI", true, false, false, false, scenarios, onEvaluation));
            lines.Add(TuneBasic("PID", true, true, false, false, scenarios, onEvaluation));
            lines.Add(TuneBasic("PID+", true, true, true, true, scenarios, onEvaluation));
            lines.Add(TuneOurs(scenarios, onEvaluation));

            return string.Join(Environment.NewLine, lines);
        }
    }
}