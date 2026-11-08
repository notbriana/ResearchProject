using System;
using System.Globalization;
using System.IO;

namespace SalternPID
{
    // the metrics of one run 
    public class RunMetrics
    {
        public double IAE;               // the sum of |e(k)| over all steps (the total error)
        public double MaxError;          // the biggest |e(k)| after the event
        public bool Settled;             // does |e| stay under 1 percent until the end?
        public int SettlingSteps;        // steps from the event until it settles
        public bool HasOvershoot;        // only when there is a big error at the event
        public double Overshoot;         // how much the level goes to the other side of the set point
        public double ValveMovement;     // the sum of |u(k) - u(k-1)|
        public int DirectionChanges;     // how many times the valve changes direction
        public double FinalError;        // the mean of |e| over the last 50 steps
        public int Cancels;              // how many times the derivative was cancelled

        public static RunMetrics Calculate(RunLog log, int eventStep, int cancels)
        {
            RunMetrics m = new RunMetrics();
            int n = log.Count;

            double[] e = new double[n];
            for (int k = 0; k < n; k++)
                e[k] = log.Level[k] - log.SetPoint[k];

            // the total error
            for (int k = 0; k < n; k++)
                m.IAE += Math.Abs(e[k]);

            // the biggest error after the event
            for (int k = eventStep; k < n; k++)
                m.MaxError = Math.Max(m.MaxError, Math.Abs(e[k]));

            // the first step after which |e| stays <= 1 until the end
            int settleStep = n;
            for (int k = n - 1; k >= eventStep; k--)
            {
                if (Math.Abs(e[k]) <= 1.0) settleStep = k;
                else break;
            }
            m.Settled = settleStep < n;
            m.SettlingSteps = settleStep - eventStep;

            // how much the level goes to the other side of the set point
            if (Math.Abs(e[eventStep]) >= 5)
            {
                m.HasOvershoot = true;
                double side = Math.Sign(e[eventStep]);
                for (int k = eventStep; k < n; k++)
                    m.Overshoot = Math.Max(m.Overshoot, -side * e[k]);
            }

            // the valve movement and the changes of direction
            double lastDirection = 0;
            for (int k = 6; k < n; k++)
            {
                double move = log.Valve[k] - log.Valve[k - 1];
                m.ValveMovement += Math.Abs(move);

                if (Math.Abs(move) > 0.000000001)
                {
                    double direction = Math.Sign(move);
                    if (lastDirection != 0 && direction != lastDirection)
                        m.DirectionChanges++;
                    lastDirection = direction;
                }
            }

            // the final error (the mean of |e| over the last 50 steps)
            double sum = 0;
            for (int k = n - 50; k < n; k++)
                sum += Math.Abs(e[k]);
            m.FinalError = sum / 50.0;

            m.Cancels = cancels;
            return m;
        }

        // the text for the screen
        public string ToText(string situation, string controller)
        {
            string settling = Settled ? SettlingSteps + " steps" : "not settled";
            string overshoot = HasOvershoot ? Overshoot.ToString("F1") : "-";

            return
                "Results of the run: " + situation + ", " + controller + Environment.NewLine +
                "IAE (total error): " + IAE.ToString("F0") + Environment.NewLine +
                "Max error after event: " + MaxError.ToString("F1") + Environment.NewLine +
                "Settling time: " + settling + Environment.NewLine +
                "Overshoot: " + overshoot + Environment.NewLine +
                "Valve movement: " + ValveMovement.ToString("F0") + Environment.NewLine +
                "Direction changes: " + DirectionChanges + Environment.NewLine +
                "Final error: " + FinalError.ToString("F2") + Environment.NewLine +
                "Derivative cancels: " + Cancels;
        }

        // one line with the metrics, in a CSV file
        public void SaveCsv(string path, string situation, string controller, int seed)
        {
            string sep = CultureInfo.CurrentCulture.TextInfo.ListSeparator;

            string header = "situation" + sep + "controller" + sep + "seed" + sep + "IAE" + sep + "maxError" + sep
                + "settled" + sep + "settlingSteps" + sep + "overshoot" + sep + "valveMovement" + sep
                + "directionChanges" + sep + "finalError" + sep + "cancels";

            string line = situation + sep + controller + sep + seed + sep + IAE.ToString("F1") + sep + MaxError.ToString("F2") + sep
                + (Settled ? "yes" : "no") + sep + SettlingSteps + sep + (HasOvershoot ? Overshoot.ToString("F2") : "") + sep
                + ValveMovement.ToString("F1") + sep + DirectionChanges + sep + FinalError.ToString("F3") + sep + Cancels;

            File.WriteAllText(path, header + Environment.NewLine + line + Environment.NewLine);
        }
    }
}