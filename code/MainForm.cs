using System;
using System.Drawing;
using System.Windows.Forms;

namespace SalternPID
{
    public class MainForm : Form
    {
        private TankSimulator _tank;
        private int _step = 0;

        private TankView _tankView;
        private Timer _timer;

        private NumericUpDown _seedBox;
        private TrackBar _setPointBar;
        private TrackBar _valveBar;
        private TrackBar _nullBar;
        private TrackBar _disturbanceBar;
        private Label _infoLabel;

        public MainForm()
        {
            Text = "Saltern PID";
            ClientSize = new Size(920, 650);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10);
            BackColor = Color.White;

            _tank = new TankSimulator(1);

            BuildInterface();

            // one step of the process every 150 ms
            _timer = new Timer();
            _timer.Interval = 150;
            _timer.Tick += Timer_Tick;

            UpdateScreen();
        }

        private void BuildInterface()
        {
            // first we add the panel on the right, then the tank on the left after it
            Panel rightPanel = new Panel();
            rightPanel.Dock = DockStyle.Fill;
            rightPanel.Padding = new Padding(15);
            Controls.Add(rightPanel);

            _tankView = new TankView();
            _tankView.Dock = DockStyle.Left;
            _tankView.Width = 340;
            Controls.Add(_tankView);

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.AutoScroll = true;
            rightPanel.Controls.Add(flow);

            Label title = new Label();
            title.Text = "Saltern PID";
            title.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            title.AutoSize = true;
            flow.Controls.Add(title);

            Label help = new Label();
            help.Text = "We move the valve by hand. The level goes up when the valve is above "
                      + "the null position u0 and it goes down when the valve is below it";
            help.AutoSize = true;
            help.MaximumSize = new Size(500, 0);
            help.Margin = new Padding(3, 5, 3, 10);
            flow.Controls.Add(help);

            // the buttons
            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.Controls.Add(MakeButton("Start", StartButton_Click));
            buttons.Controls.Add(MakeButton("Stop", StopButton_Click));
            buttons.Controls.Add(MakeButton("Reset", ResetButton_Click));
            flow.Controls.Add(buttons);

            // the seed of the random numbers
            FlowLayoutPanel seedRow = new FlowLayoutPanel();
            seedRow.AutoSize = true;
            seedRow.Margin = new Padding(3, 10, 3, 0);

            Label seedLabel = new Label();
            seedLabel.Text = "Seed (random sequence):";
            seedLabel.AutoSize = true;
            seedLabel.Margin = new Padding(3, 6, 3, 0);
            seedRow.Controls.Add(seedLabel);

            _seedBox = new NumericUpDown();
            _seedBox.Minimum = 1;
            _seedBox.Maximum = 100000;
            _seedBox.Value = 1;
            _seedBox.Width = 80;
            seedRow.Controls.Add(_seedBox);
            flow.Controls.Add(seedRow);

            // the sliders
            _setPointBar = AddTrackBar(flow, "Set point (SP)", 0, 100, 74);
            _valveBar = AddTrackBar(flow, "Valve u (by hand)", 0, 100, 50);
            _nullBar = AddTrackBar(flow, "Null position u0 (the controller does not know it)", 0, 100, 50);
            _disturbanceBar = AddTrackBar(flow, "Disturbance d (rain +, evaporation -)", -5, 5, 0);

            // the numbers
            _infoLabel = new Label();
            _infoLabel.Font = new Font("Consolas", 11);
            _infoLabel.AutoSize = true;
            _infoLabel.Margin = new Padding(3, 20, 3, 0);
            flow.Controls.Add(_infoLabel);
        }

        private Button MakeButton(string text, EventHandler onClick)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = 110;
            button.Height = 36;
            button.Click += onClick;
            return button;
        }

        // a title, a slider, and the label shows the value of the slider
        private TrackBar AddTrackBar(Control parent, string title, int min, int max, int value)
        {
            Label label = new Label();
            label.AutoSize = true;
            label.Text = title + ": " + value;
            label.Margin = new Padding(3, 12, 3, 0);
            parent.Controls.Add(label);

            TrackBar bar = new TrackBar();
            bar.Minimum = min;
            bar.Maximum = max;
            bar.Value = value;
            bar.TickFrequency = (max - min) / 10;
            bar.Width = 480;
            bar.ValueChanged += (sender, e) =>
            {
                label.Text = title + ": " + bar.Value;
                UpdateScreen();
            };
            parent.Controls.Add(bar);

            return bar;
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            _timer.Start();
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            _timer.Stop();
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            _timer.Stop();
            _tank.Reset((int)_seedBox.Value);
            CopySettingsToTank();
            _step = 0;
            UpdateScreen();
        }

        private void CopySettingsToTank()
        {
            _tank.NullPosition = _nullBar.Value;
            _tank.Disturbance = _disturbanceBar.Value;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // these arethe unknown things of the process
            CopySettingsToTank();

            // the valve goes to the position of the slider (maximum 3 percent per step)
            _tank.MoveValve(_valveBar.Value);

            // the process makes one step
            _tank.Step();
            _step++;

            UpdateScreen();
        }

        private void UpdateScreen()
        {
            _tankView.Level = _tank.Level;
            _tankView.SetPoint = _setPointBar.Value;
            _tankView.Valve = _tank.Valve;

            double error = _tank.Level - _setPointBar.Value;

            _infoLabel.Text =
                "Step: " + _step + Environment.NewLine +
                "Level PV: " + _tank.Level.ToString("F2") + Environment.NewLine +
                "Set point SP: " + _setPointBar.Value + Environment.NewLine +
                "Error e = PV - SP: " + error.ToString("F2") + Environment.NewLine +
                "Valve u: " + _tank.Valve.ToString("F1") + Environment.NewLine +
                "Null position u0: " + _tank.NullPosition.ToString("F0");
        }
    }
}