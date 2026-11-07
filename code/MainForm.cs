using System;
using System.Drawing;
using System.Windows.Forms;

namespace SalternPID
{
    public class MainForm : Form
    {
        private TankSimulator _tank;
        private PidSettings _settings;
        private ExtendedPid _controller;
        private int _step = 0;

        private TankView _tankView;
        private ChartView _chartView;
        private Timer _timer;

        // the process side
        private CheckBox _processCheck;
        private NumericUpDown _seedBox;
        private NumericUpDown _noiseBox;
        private TrackBar _setPointBar;
        private TrackBar _valveBar;
        private TrackBar _nullBar;
        private TrackBar _disturbanceBar;

        // the controller side
        private CheckBox _controllerCheck;
        private CheckBox _secondDerivativeCheck;
        private CheckBox _smoothingCheck;
        private CheckBox _deadBandCheck;
        private CheckBox _resetCheck;
        private CheckBox _cancelCheck;
        private CheckBox _nonlinearPCheck;
        private CheckBox _nonlinearICheck;
        private NumericUpDown _kpBox;
        private NumericUpDown _kiBox;
        private NumericUpDown _kdBox;
        private Label _infoLabel;

        public MainForm()
        {
            Text = "Saltern PID";
            ClientSize = new Size(1200, 760);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10);
            BackColor = Color.White;

            _settings = new PidSettings();
            _controller = new ExtendedPid(_settings);
            _tank = new TankSimulator(1);

            BuildInterface();

            // one step every 150 ms
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
            Controls.Add(rightPanel);

            _tankView = new TankView();
            _tankView.Dock = DockStyle.Left;
            _tankView.Width = 300;
            Controls.Add(_tankView);

            // two columns of controls, and the chart on top
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 2;
            table.RowCount = 1;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rightPanel.Controls.Add(table);

            _chartView = new ChartView();
            _chartView.Dock = DockStyle.Top;
            _chartView.Height = 250;
            rightPanel.Controls.Add(_chartView);

            FlowLayoutPanel processColumn = MakeColumn();
            FlowLayoutPanel controllerColumn = MakeColumn();
            table.Controls.Add(processColumn, 0, 0);
            table.Controls.Add(controllerColumn, 1, 0);

            BuildProcessColumn(processColumn);
            BuildControllerColumn(controllerColumn);
        }

        private FlowLayoutPanel MakeColumn()
        {
            FlowLayoutPanel column = new FlowLayoutPanel();
            column.Dock = DockStyle.Fill;
            column.FlowDirection = FlowDirection.TopDown;
            column.WrapContents = false;
            column.AutoScroll = true;
            column.Padding = new Padding(10, 5, 5, 5);
            return column;
        }

        private void BuildProcessColumn(FlowLayoutPanel column)
        {
            AddTitle(column, "Process (the tank)");

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.Controls.Add(MakeButton("Start", StartButton_Click));
            buttons.Controls.Add(MakeButton("Stop", StopButton_Click));
            buttons.Controls.Add(MakeButton("Reset", ResetButton_Click));
            column.Controls.Add(buttons);

            // with this box we stop only the process, the controller goes on
            _processCheck = AddCheckBox(column, "Process runs", true, null);

            _seedBox = AddNumberRow(column, "Seed:", 1, 100000, 1, 0, 1);
            _noiseBox = AddNumberRow(column, "Noise of the level sensor:", 0, 5, 0.1M, 1, 0);

            _setPointBar = AddTrackBar(column, "Set point (SP)", 0, 100, 74);
            _valveBar = AddTrackBar(column, "Valve u", 0, 100, 50);
            _nullBar = AddTrackBar(column, "Null position u0", 0, 100, 50);
            _disturbanceBar = AddTrackBar(column, "Disturbance d (rain +, evaporation -)", -5, 5, 0);
        }

        private void BuildControllerColumn(FlowLayoutPanel column)
        {
            AddTitle(column, "Controller");

            _controllerCheck = AddCheckBox(column, "Controller runs", true, null);

            Label parts = new Label();
            parts.Text = "Parts of the controller:";
            parts.AutoSize = true;
            parts.Margin = new Padding(3, 10, 3, 0);
            column.Controls.Add(parts);

            _secondDerivativeCheck = AddCheckBox(column, "Second derivative (acceleration)", _settings.UseSecondDerivative, SettingsChanged);
            _smoothingCheck = AddCheckBox(column, "Smoothing", _settings.UseSmoothing, SettingsChanged);
            _deadBandCheck = AddCheckBox(column, "Dead-band", _settings.UseDeadBand, SettingsChanged);
            _resetCheck = AddCheckBox(column, "Integral reset when the error changes sign", _settings.UseIntegralReset, SettingsChanged);
            _cancelCheck = AddCheckBox(column, "Cancel the derivative when the process does not converge", _settings.UseCancelDerivative, SettingsChanged);
            _nonlinearPCheck = AddCheckBox(column, "Nonlinear function fp (proportional)", _settings.UseNonlinearP, SettingsChanged);
            _nonlinearICheck = AddCheckBox(column, "Nonlinear function fi (integral)", _settings.UseNonlinearI, SettingsChanged);

            Label weights = new Label();
            weights.Text = "Weights:";
            weights.AutoSize = true;
            weights.Margin = new Padding(3, 10, 3, 0);
            column.Controls.Add(weights);

            _kpBox = AddNumberRow(column, "Kp (proportional):", 0, 50, 0.5M, 2, (decimal)_settings.Kp);
            _kiBox = AddNumberRow(column, "Ki (integral):", 0, 5, 0.05M, 2, (decimal)_settings.Ki);
            _kdBox = AddNumberRow(column, "Kd (derivative):", 0, 500, 5, 1, (decimal)_settings.Kd);
            _kpBox.ValueChanged += SettingsChanged;
            _kiBox.ValueChanged += SettingsChanged;
            _kdBox.ValueChanged += SettingsChanged;

            _infoLabel = new Label();
            _infoLabel.Font = new Font("Consolas", 10);
            _infoLabel.AutoSize = true;
            _infoLabel.Margin = new Padding(3, 15, 3, 0);
            column.Controls.Add(_infoLabel);
        }

        private void AddTitle(Control parent, string text)
        {
            Label title = new Label();
            title.Text = text;
            title.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            title.AutoSize = true;
            parent.Controls.Add(title);
        }

        private Button MakeButton(string text, EventHandler onClick)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = 90;
            button.Height = 34;
            button.Click += onClick;
            return button;
        }

        private CheckBox AddCheckBox(Control parent, string text, bool isChecked, EventHandler onChange)
        {
            CheckBox box = new CheckBox();
            box.Text = text;
            box.Checked = isChecked;
            box.AutoSize = true;
            if (onChange != null)
                box.CheckedChanged += onChange;
            parent.Controls.Add(box);
            return box;
        }

        private NumericUpDown AddNumberRow(Control parent, string title, decimal min, decimal max, decimal step, int decimals, decimal value)
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.AutoSize = true;

            Label label = new Label();
            label.Text = title;
            label.AutoSize = true;
            label.Margin = new Padding(3, 6, 3, 0);
            row.Controls.Add(label);

            NumericUpDown box = new NumericUpDown();
            box.Minimum = min;
            box.Maximum = max;
            box.Increment = step;
            box.DecimalPlaces = decimals;
            box.Value = value;
            box.Width = 80;
            row.Controls.Add(box);

            parent.Controls.Add(row);
            return box;
        }

        // a title, a slider, and the label shows the value of the slider
        private TrackBar AddTrackBar(Control parent, string title, int min, int max, int value)
        {
            Label label = new Label();
            label.AutoSize = true;
            label.Text = title + ": " + value;
            label.Margin = new Padding(3, 10, 3, 0);
            parent.Controls.Add(label);

            TrackBar bar = new TrackBar();
            bar.Minimum = min;
            bar.Maximum = max;
            bar.Value = value;
            bar.TickFrequency = (max - min) / 10;
            bar.Width = 380;
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
            _controller = new ExtendedPid(_settings);
            CopySettingsToTank();
            _step = 0;
            _chartView.Clear();
            UpdateScreen();
        }

        // the switches and the weights go to the settings of the controller
        private void SettingsChanged(object sender, EventArgs e)
        {
            _settings.UseSecondDerivative = _secondDerivativeCheck.Checked;
            _settings.UseSmoothing = _smoothingCheck.Checked;
            _settings.UseDeadBand = _deadBandCheck.Checked;
            _settings.UseIntegralReset = _resetCheck.Checked;
            _settings.UseCancelDerivative = _cancelCheck.Checked;
            _settings.UseNonlinearP = _nonlinearPCheck.Checked;
            _settings.UseNonlinearI = _nonlinearICheck.Checked;

            _settings.Kp = (double)_kpBox.Value;
            _settings.Ki = (double)_kiBox.Value;
            _settings.Kd = (double)_kdBox.Value;
        }

        private void CopySettingsToTank()
        {
            _tank.NullPosition = _nullBar.Value;
            _tank.Disturbance = _disturbanceBar.Value;
            _tank.NoiseSigma = (double)_noiseBox.Value;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // the unknown things of the process
            CopySettingsToTank();

            // what the sensor reads
            double measured = _tank.ReadLevel();

            // the valve: the controller decides, or the valve is moved by hand
            if (_controllerCheck.Checked)
            {
                double wanted = _controller.Decide(measured, _setPointBar.Value, _tank.Valve);
                _tank.MoveValve(wanted);
            }
            else
            {
                _tank.MoveValve(_valveBar.Value);
            }

            // the process makes one step
            if (_processCheck.Checked)
                _tank.Step();

            _step++;
            _chartView.AddPoint(_tank.Level, _setPointBar.Value, _tank.Valve);
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
                "Error e = PV - SP: " + error.ToString("F2") + Environment.NewLine +
                "Valve u: " + _tank.Valve.ToString("F1") + Environment.NewLine +
                "Null position u0: " + _tank.NullPosition.ToString("F0") + Environment.NewLine +
                "Smooth error es: " + _controller.SmoothError.ToString("F2") + Environment.NewLine +
                "Rate d1s: " + _controller.Rate.ToString("F2") + Environment.NewLine +
                "Acceleration d2s: " + _controller.Acceleration.ToString("F2") + Environment.NewLine +
                "Integral S: " + _controller.Sum.ToString("F1") + Environment.NewLine +
                "Derivative cancels: " + _controller.CancelCount;
        }
    }
}