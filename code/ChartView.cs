using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SalternPID
{
    // this is a simple chart: the level, the set point and the valve over the last steps
    public class ChartView : Control
    {
        private const int MaxPoints = 300;

        private List<double> _level = new List<double>();
        private List<double> _setPoint = new List<double>();
        private List<double> _valve = new List<double>();

        public ChartView()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.White;
        }

        public void Clear()
        {
            _level.Clear();
            _setPoint.Clear();
            _valve.Clear();
            Invalidate();
        }

        public void AddPoint(double level, double setPoint, double valve)
        {
            _level.Add(level);
            _setPoint.Add(setPoint);
            _valve.Add(valve);

            // we show only the last steps
            if (_level.Count > MaxPoints)
            {
                _level.RemoveAt(0);
                _setPoint.RemoveAt(0);
                _valve.RemoveAt(0);
            }

            Invalidate();
        }

        private void DrawLine(Graphics g, List<double> values, Pen pen, int left, int top, int width, int height)
        {
            if (values.Count < 2) return;

            PointF[] points = new PointF[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                float x = left + width * (float)i / (MaxPoints - 1);
                float y = top + height - (float)(height * values[i] / 100.0);
                points[i] = new PointF(x, y);
            }
            g.DrawLines(pen, points);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int left = 45;
            int top = 30;
            int width = Width - left - 15;
            int height = Height - top - 20;

            if (width < 50 || height < 50) return;

            using (Font smallFont = new Font("Segoe UI", 9))
            using (Pen gridPen = new Pen(Color.LightGray, 1))
            using (Pen borderPen = new Pen(Color.Black, 1))
            using (Pen levelPen = new Pen(Color.RoyalBlue, 2))
            using (Pen setPointPen = new Pen(Color.Red, 2))
            using (Pen valvePen = new Pen(Color.Orange, 2))
            {
                // the grid and the numbers on the left
                for (int value = 0; value <= 100; value += 25)
                {
                    int y = top + height - (int)(height * value / 100.0);
                    g.DrawLine(gridPen, left, y, left + width, y);
                    g.DrawString(value.ToString(), smallFont, Brushes.Black, 8, y - 8);
                }
                g.DrawRectangle(borderPen, left, top, width, height);

                // the lines
                setPointPen.DashStyle = DashStyle.Dash;
                DrawLine(g, _setPoint, setPointPen, left, top, width, height);
                DrawLine(g, _valve, valvePen, left, top, width, height);
                DrawLine(g, _level, levelPen, left, top, width, height);

                // the names of the lines
                g.DrawString("Level PV", smallFont, Brushes.RoyalBlue, left, 6);
                g.DrawString("Set point SP", smallFont, Brushes.Red, left + 90, 6);
                g.DrawString("Valve u", smallFont, Brushes.DarkOrange, left + 200, 6);
                g.DrawString("last " + MaxPoints + " steps", smallFont, Brushes.Gray, left + width - 90, 6);
            }
        }
    }
}