using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;

namespace SalternPID
{
    // a controller that we want to test: a name and a way to make a new one for every run
    public class Candidate
    {
        public string Name;
        public Func<IController> Make;

        public Candidate(string name, Func<IController> make)
        {
            Name = name;
            Make = make;
        }
    }

    // the results of one experiment: a table with the metrics, and the IAE of every run (we need it for the paired comparison)
    public class ExperimentResult
    {
        public DataTable Table = new DataTable();

        // key = situation + "|" + controller name, value = the IAE of each run (run i uses the seed i + 1)
        public Dictionary<string, double[]> IaeOfRuns = new Dictionary<string, double[]>();
    }

    // the experiments E1 (comparison), E6 (turning parts off) and E7 (changing the weights)
    public static class Experiments
    {
        // one run of a controller in one situation; it is the same order of steps as in the main form
        public static RunMetrics RunOne(IController controller, Scenario s, int seed)
        {
            TankSimulator tank = new TankSimulator(seed);
            RunLog log = new RunLog();

            for (int k = 0; k < s.Steps; k++)
            {
                tank.NoiseSigma = s.NoiseSigma;
                tank.NullPosition = s.NullPosition(k);
                tank.Disturbance = s.Disturbance(k);
                double setPoint = s.SetPoint(k);

                double wanted = controller.Decide(tank.ReadLevel(), setPoint, tank.Valve);
                tank.MoveValve(wanted);

                log.Add(tank.Level, setPoint, tank.Valve, tank.NullPosition, tank.Disturbance);
                tank.Step();
            }

            int cancels = 0;
            ExtendedPid ours = controller as ExtendedPid;
            if (ours != null)
                cancels = ours.CancelCount;

            return RunMetrics.Calculate(log, s.EventStep, cancels);
        }

        // runs every controller in every situation, "runs" times (seeds 1, 2, 3 ...), and makes the table
        // onRunDone is called after each run (for the progress bar)
        public static ExperimentResult Run(List<Scenario> scenarios, List<Candidate> candidates, int runs, Action onRunDone)
        {
            ExperimentResult result = new ExperimentResult();

            DataTable t = result.Table;
            t.Columns.Add("Situation");
            t.Columns.Add("Controller");
            t.Columns.Add("IAE mean");
            t.Columns.Add("IAE std");
            t.Columns.Add("Max error");
            t.Columns.Add("Settled %");
            t.Columns.Add("Settling steps");
            t.Columns.Add("Overshoot");
            t.Columns.Add("Valve movement");
            t.Columns.Add("Direction changes");
            t.Columns.Add("Final error");
            t.Columns.Add("Cancels");

            foreach (Scenario s in scenarios)
            {
                foreach (Candidate c in candidates)
                {
                    double[] iae = new double[runs];
                    double sumMax = 0, sumValve = 0, sumDirection = 0, sumFinal = 0, sumCancels = 0;
                    double sumSettling = 0, sumOvershoot = 0;
                    int settledCount = 0, overshootCount = 0;

                    for (int i = 0; i < runs; i++)
                    {
                        RunMetrics m = RunOne(c.Make(), s, i + 1);

                        iae[i] = m.IAE;
                        sumMax += m.MaxError;
                        sumValve += m.ValveMovement;
                        sumDirection += m.DirectionChanges;
                        sumFinal += m.FinalError;
                        sumCancels += m.Cancels;

                        if (m.Settled)
                        {
                            settledCount++;
                            sumSettling += m.SettlingSteps;
                        }
                        if (m.HasOvershoot)
                        {
                            overshootCount++;
                            sumOvershoot += m.Overshoot;
                        }

                        if (onRunDone != null)
                            onRunDone();
                    }

                    result.IaeOfRuns[s.Name + "|" + c.Name] = iae;

                    string settling = settledCount > 0 ? (sumSettling / settledCount).ToString("F0") : "-";
                    string overshoot = overshootCount > 0 ? (sumOvershoot / overshootCount).ToString("F1") : "-";

                    t.Rows.Add(s.Name, c.Name,
                        Mean(iae).ToString("F0"), Std(iae).ToString("F0"),
                        (sumMax / runs).ToString("F1"),
                        (100.0 * settledCount / runs).ToString("F0"),
                        settling, overshoot,
                        (sumValve / runs).ToString("F0"),
                        (sumDirection / runs).ToString("F0"),
                        (sumFinal / runs).ToString("F2"),
                        (sumCancels / runs).ToString("F1"));
                }
            }

            return result;
        }

        // for each situation: in how many runs is our controller better than the others
        // the runs are paired: the same seed gives the same random numbers for both controllers
        public static DataTable PairedTable(ExperimentResult comparison, List<Scenario> scenarios, string oursName, List<string> otherNames, int runs)
        {
            DataTable t = new DataTable();
            t.Columns.Add("Situation");
            t.Columns.Add("Compared with");
            t.Columns.Add("Ours is better (smaller IAE)");
            t.Columns.Add("Mean IAE difference (other - ours)");

            foreach (Scenario s in scenarios)
            {
                double[] ours = comparison.IaeOfRuns[s.Name + "|" + oursName];

                foreach (string other in otherNames)
                {
                    double[] others = comparison.IaeOfRuns[s.Name + "|" + other];

                    int wins = 0;
                    double difference = 0;
                    for (int i = 0; i < runs; i++)
                    {
                        if (ours[i] < others[i]) wins++;
                        difference += others[i] - ours[i];
                    }

                    t.Rows.Add(s.Name, other, wins + " of " + runs, (difference / runs).ToString("F0"));
                }
            }

            return t;
        }

        // E1: our controller and the four controllers for the comparison
        public static List<Candidate> ComparisonCandidates()
        {
            List<Candidate> list = new List<Candidate>();
            list.Add(new Candidate("P", () => BasicPid.CreateP()));
            list.Add(new Candidate("PI", () => BasicPid.CreatePI()));
            list.Add(new Candidate("PID", () => BasicPid.CreatePID()));
            list.Add(new Candidate("PID+", () => BasicPid.CreatePIDPlus()));
            list.Add(new Candidate("Extended PID (ours)", () => new ExtendedPid(new PidSettings())));
            return list;
        }

        // E6: our controller with one part turned off
        public static List<Candidate> AblationCandidates()
        {
            List<Candidate> list = new List<Candidate>();
            list.Add(Variant("Full controller", s => { }));
            list.Add(Variant("No second derivative", s => s.UseSecondDerivative = false));
            list.Add(Variant("No smoothing", s => s.UseSmoothing = false));
            list.Add(Variant("No dead-band", s => s.UseDeadBand = false));
            list.Add(Variant("No integral reset", s => s.UseIntegralReset = false));
            list.Add(Variant("No cancel of the derivative", s => s.UseCancelDerivative = false));
            list.Add(Variant("Linear fp", s => s.UseNonlinearP = false));
            list.Add(Variant("Linear fi", s => s.UseNonlinearI = false));
            list.Add(Variant("Linear fp and fi", s => { s.UseNonlinearP = false; s.UseNonlinearI = false; }));
            return list;
        }

        // E7: our controller with one weight changed (0.5, 0.8, 1.25 and 2 times)
        public static List<Candidate> WeightCandidates()
        {
            List<Candidate> list = new List<Candidate>();
            list.Add(Variant("Original weights", s => { }));

            double[] factors = { 0.5, 0.8, 1.25, 2 };
            foreach (double f in factors)
            {
                double factor = f;
                string text = factor.ToString(CultureInfo.InvariantCulture);
                list.Add(Variant("Kp x" + text, s => s.Kp = s.Kp * factor));
                list.Add(Variant("Ki x" + text, s => s.Ki = s.Ki * factor));
                list.Add(Variant("Kd x" + text, s => s.Kd = s.Kd * factor));
            }
            return list;
        }

        private static Candidate Variant(string name, Action<PidSettings> change)
        {
            return new Candidate(name, () =>
            {
                PidSettings settings = new PidSettings();
                change(settings);
                return new ExtendedPid(settings);
            });
        }

        // takes only some situations from the list, by name
        public static List<Scenario> Pick(List<Scenario> all, params string[] names)
        {
            List<Scenario> list = new List<Scenario>();
            foreach (string name in names)
                foreach (Scenario s in all)
                    if (s.Name == name)
                        list.Add(s);
            return list;
        }

        private static double Mean(double[] values)
        {
            double sum = 0;
            foreach (double v in values) sum += v;
            return sum / values.Length;
        }

        private static double Std(double[] values)
        {
            double mean = Mean(values);
            double sum = 0;
            foreach (double v in values) sum += (v - mean) * (v - mean);
            return Math.Sqrt(sum / values.Length);
        }

        // saves a table in a CSV file
        public static void SaveTable(DataTable table, string path)
        {
            string sep = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
            StringBuilder text = new StringBuilder();

            List<string> names = new List<string>();
            foreach (DataColumn column in table.Columns)
                names.Add(column.ColumnName);
            text.AppendLine(string.Join(sep, names));

            foreach (DataRow row in table.Rows)
            {
                List<string> cells = new List<string>();
                foreach (object cell in row.ItemArray)
                    cells.Add(cell.ToString());
                text.AppendLine(string.Join(sep, cells));
            }

            File.WriteAllText(path, text.ToString());
        }
    }
}