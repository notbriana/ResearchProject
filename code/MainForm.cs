using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SalternPID
{
    public class MainForm : Form
    {
        private TankSimulator _tank;
        private PidSettings _settings;
        private IController _controller;
        private int _step = 0;

        // the test situation (null = free mode, we move the sliders by hand)
        private List<Scenario> _scenarios;
        private Scenario _scenario = null;

        // what happens at every step, and the metrics of the finished run
        private RunLog _log = new RunLog();
        private RunMetrics _metrics = null;

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
        private ComboBox _scenarioBox;
        private Control[] _scenarioDrivenControls;
        private Label _resultLabel;

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
        private ComboBox _controllerBox;
        private Control[] _ourOnlyControls;
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
            _scenarios = Scenarios.CreateAll();

            BuildInterface();

            // one step every 150 ms
            _timer = new Timer();
            _timer.Interval = 150;
            _timer.Tick += Timer_Tick;

            UpdateScreen();
        }

        private void BuildInterface()
        {
            // the panel on the right is added first, the tank on the left after it
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
            AddTitle(column, "Process");

            // the test situations S1 to S6
            FlowLayoutPanel situationRow = new FlowLayoutPanel();
            situationRow.AutoSize = true;
            Label situationLabel = new Label();
            situationLabel.Text = "Situation:";
            situationLabel.AutoSize = true;
            situationLabel.Margin = new Padding(3, 6, 3, 0);
            situationRow.Controls.Add(situationLabel);

            _scenarioBox = new ComboBox();
            _scenarioBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _scenarioBox.Items.Add("Free (we move the sliders by hand)");
            foreach (Scenario scenario in _scenarios)
                _scenarioBox.Items.Add(scenario.Name + ": " + scenario.Description);
            _scenarioBox.Width = 340;
            _scenarioBox.SelectedIndex = 0;
            _scenarioBox.SelectedIndexChanged += ScenarioBox_Changed;
            situationRow.Controls.Add(_scenarioBox);
            column.Controls.Add(situationRow);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.Controls.Add(MakeButton("Start", StartButton_Click));
            buttons.Controls.Add(MakeButton("Stop", StopButton_Click));
            buttons.Controls.Add(MakeButton("Reset", ResetButton_Click));
            buttons.Controls.Add(MakeButton("Save CSV", SaveButton_Click));
            column.Controls.Add(buttons);

            // with this box we stop only the process, the controller goes on
            _processCheck = AddCheckBox(column, "Process runs", true, null);

            _seedBox = AddNumberRow(column, "Seed:", 1, 100000, 1, 0, 1);
            _noiseBox = AddNumberRow(column, "Noise of the level sensor:", 0, 5, 0.1M, 1, 0);

            _setPointBar = AddTrackBar(column, "Set point (SP)", 0, 100, 74);
            _valveBar = AddTrackBar(column, "Valve u", 0, 100, 50);
            _nullBar = AddTrackBar(column, "Null position u0", 0, 100, 50);
            _disturbanceBar = AddTrackBar(column, "Disturbance d (rain +, evaporation -)", -5, 5, 0);

            // the metrics of the run, shown when a situation is finished
            _resultLabel = new Label();
            _resultLabel.Font = new Font("Consolas", 10);
            _resultLabel.AutoSize = true;
            _resultLabel.Margin = new Padding(3, 15, 3, 0);
            column.Controls.Add(_resultLabel);

            // when a situation is chosen, it moves these controls, not us
            _scenarioDrivenControls = new Control[] { _setPointBar, _nullBar, _disturbanceBar, _noiseBox };
        }

        private void BuildControllerColumn(FlowLayoutPanel column)
        {
            AddTitle(column, "Controller");

            // which controller: ours or one of the normal ones for the comparison
            FlowLayoutPanel chooseRow = new FlowLayoutPanel();
            chooseRow.AutoSize = true;
            Label chooseLabel = new Label();
            chooseLabel.Text = "Controller:";
            chooseLabel.AutoSize = true;
            chooseLabel.Margin = new Padding(3, 6, 3, 0);
            chooseRow.Controls.Add(chooseLabel);

            _controllerBox = new ComboBox();
            _controllerBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _controllerBox.Items.Add("Extended PID (ours)");
            _controllerBox.Items.Add("P");
            _controllerBox.Items.Add("PI");
            _controllerBox.Items.Add("PID");
            _controllerBox.Items.Add("PID+ (filtered derivative, anti-windup)");
            _controllerBox.Width = 280;
            _controllerBox.SelectedIndex = 0;
            _controllerBox.SelectedIndexChanged += ControllerBox_Changed;
            chooseRow.Controls.Add(_controllerBox);
            column.Controls.Add(chooseRow);

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

            // the switches and the weights are only for our controller
            _ourOnlyControls = new Control[]
            {
                _secondDerivativeCheck, _smoothingCheck, _deadBandCheck, _resetCheck,
                _cancelCheck, _nonlinearPCheck, _nonlinearICheck, _kpBox, _kiBox, _kdBox
            };

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
            DoReset();
        }

        // the tank, the controller, the chart and the step counter
        private void DoReset()
        {
            _timer.Stop();
            _tank.Reset((int)_seedBox.Value);
            _controller = MakeController(_controllerBox.SelectedIndex);
            _step = 0;
            _chartView.Clear();
            _log.Clear();
            _metrics = null;
            _resultLabel.Text = "";
            ApplyScenario();
            CopySettingsToTank();
            UpdateScreen();
        }

        // a situation was chosen (or the free mode)
        private void ScenarioBox_Changed(object sender, EventArgs e)
        {
            int index = _scenarioBox.SelectedIndex;
            if (index <= 0)
                _scenario = null;
            else
                _scenario = _scenarios[index - 1];

            // in a situation, the sliders are moved by the situation, so we cannot move them
            foreach (Control control in _scenarioDrivenControls)
                control.Enabled = (_scenario == null);

            DoReset();
        }

        // the situation sets the set point, u0, the disturbance and the noise for the current step
        private void ApplyScenario()
        {
            if (_scenario == null) return;

            SetBar(_setPointBar, (int)_scenario.SetPoint(_step));
            SetBar(_nullBar, (int)_scenario.NullPosition(_step));
            SetBar(_disturbanceBar, (int)_scenario.Disturbance(_step));
            _noiseBox.Value = (decimal)_scenario.NoiseSigma;
        }

        private void SetBar(TrackBar bar, int value)
        {
            if (bar.Value != value)
                bar.Value = value;
        }

        // the run is finished: we compute the metrics and we show them
        private void ShowResults()
        {
            if (_scenario == null) return;
            if (_log.Count != _scenario.Steps) return;

            _metrics = RunMetrics.Calculate(_log, _scenario.EventStep, GetCancelCount());
            _resultLabel.Text = _metrics.ToText(_scenario.Name, _controller.Name);
        }

        private int GetCancelCount()
        {
            ExtendedPid ours = _controller as ExtendedPid;
            if (ours == null) return 0;
            return ours.CancelCount;
        }

        // saves the steps in a CSV file, and the metrics in a second CSV file (only for a finished situation)
        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (_log.Count == 0)
            {
                MessageBox.Show("There is nothing to save yet. Run something first.");
                return;
            }

            string situation = (_scenario == null) ? "free" : _scenario.Name;
            string controllerName = _controller.Name.Replace(" ", "").Replace("+", "Plus");

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "CSV files (*.csv)|*.csv";
            dialog.FileName = "run_" + situation + "_" + controllerName + "_seed" + _seedBox.Value + ".csv";
            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            _log.SaveCsv(dialog.FileName);
            string message = "Saved the steps in:" + Environment.NewLine + dialog.FileName;

            if (_metrics != null)
            {
                string folder = Path.GetDirectoryName(dialog.FileName);
                string metricsName = Path.GetFileNameWithoutExtension(dialog.FileName) + "_metrics.csv";
                string metricsPath = Path.Combine(folder, metricsName);

                _metrics.SaveCsv(metricsPath, situation, _controller.Name, (int)_seedBox.Value);
                message = message + Environment.NewLine + Environment.NewLine + "Saved the metrics in:" + Environment.NewLine + metricsPath;
            }

            MessageBox.Show(message);
        }

        // makes the controller that is chosen in the list
        private IController MakeController(int index)
        {
            if (index == 1) return BasicPid.CreateP();
            if (index == 2) return BasicPid.CreatePI();
            if (index == 3) return BasicPid.CreatePID();
            if (index == 4) return BasicPid.CreatePIDPlus();
            return new ExtendedPid(_settings);
        }

        private void ControllerBox_Changed(object sender, EventArgs e)
        {
            _controller = MakeController(_controllerBox.SelectedIndex);

            // the switches and the weights work only for our controller
            bool isOurs = (_controllerBox.SelectedIndex == 0);
            foreach (Control control in _ourOnlyControls)
                control.Enabled = isOurs;

            // in a situation, a new controller means a new run
            if (_scenario != null)
                DoReset();
            else
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
            // a situation has a fixed number of steps
            if (_scenario != null && _step >= _scenario.Steps)
            {
                _timer.Stop();
                ShowResults();
                UpdateScreen();
                return;
            }

            // the situation sets the unknown things (or the sliders, in the free mode)
            ApplyScenario();
            CopySettingsToTank();

            // what the sensor reads (with noise)
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

            // we save this step, for the metrics and for the CSV file
            _log.Add(_tank.Level, _setPointBar.Value, _tank.Valve, _tank.NullPosition, _tank.Disturbance);

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

            string text =
                "Step: " + _step + Environment.NewLine +
                "Level PV: " + _tank.Level.ToString("F2") + Environment.NewLine +
                "Error e = PV - SP: " + error.ToString("F2") + Environment.NewLine +
                "Valve u: " + _tank.Valve.ToString("F1") + Environment.NewLine +
                "Null position u0: " + _tank.NullPosition.ToString("F0") + Environment.NewLine +
                "Controller: " + _controller.Name + Environment.NewLine +
                "Situation: " + SituationText();

            // the inside numbers are only for our controller
            ExtendedPid ours = _controller as ExtendedPid;
            if (ours != null)
            {
                text = text + Environment.NewLine +
                    "Smooth error es: " + ours.SmoothError.ToString("F2") + Environment.NewLine +
                    "Rate d1s: " + ours.Rate.ToString("F2") + Environment.NewLine +
                    "Acceleration d2s: " + ours.Acceleration.ToString("F2") + Environment.NewLine +
                    "Integral S: " + ours.Sum.ToString("F1") + Environment.NewLine +
                    "Derivative cancels: " + ours.CancelCount;
            }

            _infoLabel.Text = text;
        }

        private string SituationText()
        {
            if (_scenario == null) return "free";

            if (_step >= _scenario.Steps)
                return _scenario.Name + " finished (" + _scenario.Steps + " steps)";

            return _scenario.Name + " (step " + _step + " of " + _scenario.Steps + ")";
        }
    }
}