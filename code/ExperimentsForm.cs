using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SalternPID
{
    // it runs the experiments E1, E6 and E7 and shows the tables with the results; it also has the checks of the simulator and the tuning of the weights
    public class ExperimentsForm : Form
    {
        private NumericUpDown _runsBox;
        private Button _runButton;
        private Button _saveButton;
        private Button _checksButton;
        private Button _tuneButton;
        private ProgressBar _progressBar;
        private Label _statusLabel;
        private ComboBox _tableBox;
        private DataGridView _grid;
        private TextBox _outputBox;

        // the results
        private DataTable _comparisonTable;
        private DataTable _pairedTable;
        private DataTable _ablationTable;
        private DataTable _weightsTable;

        public ExperimentsForm()
        {
            Text = "Experiments E1, E6 and E7, checks and tuning";
            ClientSize = new Size(1200, 720);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10);
            BackColor = Color.White;

            BuildInterface();
        }

        private void BuildInterface()
        {
            // the table is added first, then the text box at the bottom, then the panel with the buttons on top
            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersVisible = false;
            _grid.BackgroundColor = Color.White;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            Controls.Add(_grid);

            // here we write the results of the checks and of the tuning
            _outputBox = new TextBox();
            _outputBox.Dock = DockStyle.Bottom;
            _outputBox.Height = 190;
            _outputBox.Multiline = true;
            _outputBox.ReadOnly = true;
            _outputBox.ScrollBars = ScrollBars.Vertical;
            _outputBox.Font = new Font("Consolas", 10);
            Controls.Add(_outputBox);

            FlowLayoutPanel top = new FlowLayoutPanel();
            top.Dock = DockStyle.Top;
            top.Height = 190;
            top.Padding = new Padding(10);
            Controls.Add(top);

            Label help = new Label();
            help.Text = "Every controller runs in every situation with the seeds 1, 2, 3 ... (the same seeds for all controllers). "
                      + "E1 compares our controller with P, PI, PID and PID+. E6 turns off one part at a time. E7 changes the weights.";
            help.AutoSize = true;
            help.MaximumSize = new Size(1150, 0);
            top.Controls.Add(help);
            top.SetFlowBreak(help, true);

            Label runsLabel = new Label();
            runsLabel.Text = "Runs for each situation:";
            runsLabel.AutoSize = true;
            runsLabel.Margin = new Padding(3, 8, 3, 0);
            top.Controls.Add(runsLabel);

            _runsBox = new NumericUpDown();
            _runsBox.Minimum = 10;
            _runsBox.Maximum = 1000;
            _runsBox.Increment = 10;
            _runsBox.Value = 100;
            _runsBox.Width = 80;
            top.Controls.Add(_runsBox);

            _runButton = new Button();
            _runButton.Text = "Run the experiments";
            _runButton.Width = 180;
            _runButton.Height = 34;
            _runButton.Click += RunButton_Click;
            top.Controls.Add(_runButton);

            _saveButton = new Button();
            _saveButton.Text = "Save CSV files";
            _saveButton.Width = 140;
            _saveButton.Height = 34;
            _saveButton.Enabled = false;
            _saveButton.Click += SaveButton_Click;
            top.Controls.Add(_saveButton);

            _checksButton = new Button();
            _checksButton.Text = "Simulator checks";
            _checksButton.Width = 160;
            _checksButton.Height = 34;
            _checksButton.Click += ChecksButton_Click;
            top.Controls.Add(_checksButton);

            _tuneButton = new Button();
            _tuneButton.Text = "Tune the weights";
            _tuneButton.Width = 160;
            _tuneButton.Height = 34;
            _tuneButton.Click += TuneButton_Click;
            top.Controls.Add(_tuneButton);

            _progressBar = new ProgressBar();
            _progressBar.Width = 250;
            _progressBar.Height = 24;
            _progressBar.Margin = new Padding(10, 8, 3, 0);
            top.Controls.Add(_progressBar);

            _statusLabel = new Label();
            _statusLabel.AutoSize = true;
            _statusLabel.Margin = new Padding(10, 8, 3, 0);
            top.Controls.Add(_statusLabel);
            top.SetFlowBreak(_statusLabel, true);

            Label tableLabel = new Label();
            tableLabel.Text = "Table:";
            tableLabel.AutoSize = true;
            tableLabel.Margin = new Padding(3, 8, 3, 0);
            top.Controls.Add(tableLabel);

            _tableBox = new ComboBox();
            _tableBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _tableBox.Items.Add("E1: comparison with P, PI, PID and PID+");
            _tableBox.Items.Add("E1: our controller against each of the others (paired runs)");
            _tableBox.Items.Add("E6: turning off one part at a time");
            _tableBox.Items.Add("E7: changing the weights");
            _tableBox.Width = 480;
            _tableBox.SelectedIndex = 0;
            _tableBox.SelectedIndexChanged += TableBox_Changed;
            top.Controls.Add(_tableBox);
        }

        // while something runs, we cannot start another thing
        private void SetBusy(bool busy)
        {
            _runButton.Enabled = !busy;
            _checksButton.Enabled = !busy;
            _tuneButton.Enabled = !busy;
            _saveButton.Enabled = !busy && _comparisonTable != null;
        }

        private async void RunButton_Click(object sender, EventArgs e)
        {
            int runs = (int)_runsBox.Value;

            SetBusy(true);
            _progressBar.Value = 0;
            _statusLabel.Text = "Running...";

            // the progress comes from the other thread and it is shown here
            Progress<int> progress = new Progress<int>(percent => _progressBar.Value = percent);
            Stopwatch watch = Stopwatch.StartNew();

            try
            {
                await Task.Run(() => RunAll(runs, progress));

                _statusLabel.Text = "Done in " + watch.Elapsed.TotalSeconds.ToString("F1") + " seconds.";
                ShowTable();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Error: " + ex.Message;
            }

            SetBusy(false);
        }

        // this runs on another thread, so the window does not freeze
        private void RunAll(int runs, IProgress<int> progress)
        {
            List<Scenario> all = Scenarios.CreateAll();
            List<Scenario> forWeights = Experiments.Pick(all, "S2", "S3", "S4");

            List<Candidate> comparison = Experiments.ComparisonCandidates();
            List<Candidate> ablation = Experiments.AblationCandidates();
            List<Candidate> weights = Experiments.WeightCandidates();

            int total = (all.Count * (comparison.Count + ablation.Count) + forWeights.Count * weights.Count) * runs;
            int done = 0;
            Action oneRunDone = () =>
            {
                done++;
                progress.Report(done * 100 / total);
            };

            // E1
            ExperimentResult e1 = Experiments.Run(all, comparison, runs, oneRunDone);
            _comparisonTable = e1.Table;

            List<string> others = new List<string> { "P", "PI", "PID", "PID+" };
            _pairedTable = Experiments.PairedTable(e1, all, "Extended PID (ours)", others, runs);

            // E6 and E7
            _ablationTable = Experiments.Run(all, ablation, runs, oneRunDone).Table;
            _weightsTable = Experiments.Run(forWeights, weights, runs, oneRunDone).Table;
        }

        private void ChecksButton_Click(object sender, EventArgs e)
        {
            List<string> lines = SimulatorChecks.RunAll();
            _outputBox.Text = string.Join(Environment.NewLine, lines);
            _statusLabel.Text = "The checks are done.";
        }

        private async void TuneButton_Click(object sender, EventArgs e)
        {
            SetBusy(true);
            _progressBar.Value = 0;
            _statusLabel.Text = "Tuning the weights (about 10 to 60 seconds)...";
            _outputBox.Text = "";

            Progress<int> progress = new Progress<int>(percent => _progressBar.Value = percent);
            Stopwatch watch = Stopwatch.StartNew();

            try
            {
                int done = 0;
                Action oneEvaluationDone = () =>
                {
                    done++;
                    int percent = done * 100 / Tuner.TotalEvaluations;
                    if (percent > 100) percent = 100;
                    ((IProgress<int>)progress).Report(percent);
                };

                string text = await Task.Run(() => Tuner.TuneAll(oneEvaluationDone));

                _outputBox.Text = text;
                _statusLabel.Text = "Tuning done in " + watch.Elapsed.TotalSeconds.ToString("F1") + " seconds.";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Error: " + ex.Message;
            }

            SetBusy(false);
        }

        private void TableBox_Changed(object sender, EventArgs e)
        {
            ShowTable();
        }

        private void ShowTable()
        {
            DataTable table = null;
            int index = _tableBox.SelectedIndex;

            if (index == 0) table = _comparisonTable;
            if (index == 1) table = _pairedTable;
            if (index == 2) table = _ablationTable;
            if (index == 3) table = _weightsTable;

            _grid.DataSource = table;
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "Choose the folder for the CSV files";
            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            Experiments.SaveTable(_comparisonTable, Path.Combine(dialog.SelectedPath, "E1_comparison.csv"));
            Experiments.SaveTable(_pairedTable, Path.Combine(dialog.SelectedPath, "E1_paired.csv"));
            Experiments.SaveTable(_ablationTable, Path.Combine(dialog.SelectedPath, "E6_parts_off.csv"));
            Experiments.SaveTable(_weightsTable, Path.Combine(dialog.SelectedPath, "E7_weights.csv"));

            MessageBox.Show("Saved 4 CSV files in:" + Environment.NewLine + dialog.SelectedPath);
        }
    }
}