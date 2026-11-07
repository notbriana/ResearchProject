using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SalternPID
{
    // this keeps what happens at every step of a run, so we can compute the metrics and save a CSV file
    public class RunLog
    {
        public List<double> Level = new List<double>();
        public List<double> SetPoint = new List<double>();
        public List<double> Valve = new List<double>();
        public List<double> NullPosition = new List<double>();
        public List<double> Disturbance = new List<double>();

        public int Count
        {
            get { return Level.Count; }
        }

        public void Clear()
        {
            Level.Clear();
            SetPoint.Clear();
            Valve.Clear();
            NullPosition.Clear();
            Disturbance.Clear();
        }

        public void Add(double level, double setPoint, double valve, double nullPosition, double disturbance)
        {
            Level.Add(level);
            SetPoint.Add(setPoint);
            Valve.Add(valve);
            NullPosition.Add(nullPosition);
            Disturbance.Add(disturbance);
        }

        // one line for each step
        public void SaveCsv(string path)
        {
            string sep = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
            StringBuilder text = new StringBuilder();

            text.AppendLine("step" + sep + "level" + sep + "setpoint" + sep + "error" + sep + "valve" + sep + "nullPosition" + sep + "disturbance");

            for (int k = 0; k < Count; k++)
            {
                double error = Level[k] - SetPoint[k];
                text.AppendLine(k + sep + Level[k].ToString("F3") + sep + SetPoint[k].ToString("F1") + sep + error.ToString("F3")
                    + sep + Valve[k].ToString("F2") + sep + NullPosition[k].ToString("F1") + sep + Disturbance[k].ToString("F1"));
            }

            File.WriteAllText(path, text.ToString());
        }
    }
}