using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SalternPID
{
    // this draws the tank with the water, the set point line and the valve
    public class TankView : Control
    {
        private double _level = 0;
        private double _setPoint = 0;
        private double _valve = 50;

        public double Level
        {
            get { return _level; }
            set { _level = value; 
                Invalidate(); }
        }

        public double SetPoint
        {
            get { return _setPoint; }
            set { _setPoint = value; 
                Invalidate(); }
        }

        public double Valve
        {
            get { return _valve; }
            set { _valve = value; 
                Invalidate(); }
        }

        public TankView()
        {
            DoubleBuffered = true;   // no flicker
            ResizeRedraw = true;
            BackColor = Color.White;
        }

        // this function converts a value from 0 to 100 into a y position on the screen
        private int ToY(double value, int top, int bottom)
        {
            return bottom - (int)((bottom - top) * value / 100.0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int top = 40;
            int bottom = Height - 40;
            int left = 60;
            int right = Width - 110;

            if (bottom - top < 20 || right - left < 20) return;

            using (Font smallFont = new Font("Segoe UI", 9))
            using (Font boldFont = new Font("Segoe UI", 10, FontStyle.Bold))
            using (Pen blackPen = new Pen(Color.Black, 2))
            using (Pen thinPen = new Pen(Color.Black, 1))
            using (Pen setPointPen = new Pen(Color.Red, 2))
            {
                // the water
                int waterTop = ToY(_level, top, bottom);
                using (Brush waterBrush = new SolidBrush(Color.FromArgb(130, 175, 225)))
                {
                    g.FillRectangle(waterBrush, left, waterTop, right - left, bottom - waterTop);
                }

                // the tank
                g.DrawRectangle(blackPen, left, top, right - left, bottom - top);

                // the scale
                for (int value = 0; value <= 100; value += 25)
                {
                    int y = ToY(value, top, bottom);
                    g.DrawLine(thinPen, left - 6, y, left, y);
                    g.DrawString(value.ToString(), smallFont, Brushes.Black, 15, y - 8);
                }

                // the set point
                int spY = ToY(_setPoint, top, bottom);
                setPointPen.DashStyle = DashStyle.Dash;
                g.DrawLine(setPointPen, left - 10, spY, right, spY);
                g.DrawString("SP " + _setPoint.ToString("F0"), boldFont, Brushes.Red, right - 70, spY - 22);

                // the valve
                int barX = right + 40;
                int barWidth = 26;
                int valveTop = ToY(_valve, top, bottom);
                g.FillRectangle(Brushes.Orange, barX, valveTop, barWidth, bottom - valveTop);
                g.DrawRectangle(blackPen, barX, top, barWidth, bottom - top);
                g.DrawString("Valve " + _valve.ToString("F0"), smallFont, Brushes.Black, barX - 15, bottom + 8);

                // the level text
                g.DrawString("Level " + _level.ToString("F1"), boldFont, Brushes.Black, left, 10);
            }
        }
    }
}