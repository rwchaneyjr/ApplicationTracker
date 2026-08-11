using System.Diagnostics;
using System.Globalization;
using JobApplicationTracker.Data;
using JobApplicationTracker.Models;

namespace JobApplicationTracker.Forms;

/// <summary>
/// Main application window: dashboard, reminders, search/filters, and application grid.
/// </summary>
public class MainForm : Form
{
    private readonly Label _totalValueLabel = new();
    private readonly Label _underReviewValueLabel = new();
    private readonly Label _interviewsValueLabel = new();
    private readonly Label _offersValueLabel = new();
    private readonly Label _rejectionsValueLabel = new();

    private readonly Panel _reminderPanel = new();
    private readonly Label _reminderTitleLabel = new();
    private readonly ListBox _reminderListBox = new();

    private readonly TextBox _searchTextBox = new();
    private readonly ComboBox _statusFilterComboBox = new();
    private readonly DateTimePicker _dateFromPicker = new();
    private readonly DateTimePicker _dateToPicker = new();
    private readonly CheckBox _dateFilterEnabledCheckBox = new();

    private readonly DataGridView _grid = new();

    private readonly Button _finishButton = new();
    private readonly Button _addButton = new();
    private readonly Button _editButton = new();
    private readonly Button _deleteButton = new();
    private readonly Button _refreshButton = new();
    private readonly Button _openLinkButton = new();
    private readonly Button _exportButton = new();
    private readonly Button _backupButton = new();
    private readonly Button _clearFiltersButton = new();
    private readonly Label _bridgeStatusLabel = new();

    private readonly LocalBridgeServer _bridgeServer = new();
    private List<JobApplication> _currentRows = new();
    private bool _suppressFilterEvents;

    public MainForm()
    {
        Text = "Job Application Tracker";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 720);
        Size = new Size(1280, 840);
        Font = new Font("Segoe UI", 11F);
        BackColor = Color.FromArgb(245, 247, 250);
        KeyPreview = true;

        BuildLayout();
        WireEvents();
        RefreshAll(showStartupReminders: true);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        StartBrowserBridge();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _bridgeServer.ApplicationReceived -= BridgeServer_ApplicationReceived;
        _bridgeServer.StatusChanged -= BridgeServer_StatusChanged;
        _bridgeServer.Dispose();
        base.OnFormClosed(e);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // dashboard
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); // reminders
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));  // filters
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // grid
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));  // buttons
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));  // bridge status

        root.Controls.Add(BuildDashboard(), 0, 0);
        root.Controls.Add(BuildReminderSection(), 0, 1);
        root.Controls.Add(BuildFilterSection(), 0, 2);
        root.Controls.Add(BuildGridSection(), 0, 3);
        root.Controls.Add(BuildButtonBar(), 0, 4);
        root.Controls.Add(BuildBridgeStatusBar(), 0, 5);

        Controls.Add(root);
    }

    private Control BuildBridgeStatusBar()
    {
        _bridgeStatusLabel.Dock = DockStyle.Fill;
        _bridgeStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _bridgeStatusLabel.Font = new Font("Segoe UI", 9.5F);
        _bridgeStatusLabel.ForeColor = Color.FromArgb(70, 80, 95);
        _bridgeStatusLabel.Text = "Browser link: starting…";
        return _bridgeStatusLabel;
    }

    private Control BuildDashboard()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8)
        };

        for (var i = 0; i < 5; i++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        }

        panel.Controls.Add(CreateStatCard("Total Applications", _totalValueLabel, Color.FromArgb(26, 86, 138)), 0, 0);
        panel.Controls.Add(CreateStatCard("Under Review", _underReviewValueLabel, Color.FromArgb(140, 100, 20)), 1, 0);
        panel.Controls.Add(CreateStatCard("Interviews", _interviewsValueLabel, Color.FromArgb(20, 110, 90)), 2, 0);
        panel.Controls.Add(CreateStatCard("Offers", _offersValueLabel, Color.FromArgb(30, 120, 50)), 3, 0);
        panel.Controls.Add(CreateStatCard("Rejections", _rejectionsValueLabel, Color.FromArgb(140, 45, 45)), 4, 0);

        return panel;
    }

    private static Control CreateStatCard(string title, Label valueLabel, Color accent)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            BackColor = Color.White,
            Padding = new Padding(14, 12, 14, 12)
        };

        var accentBar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 5,
            BackColor = accent
        };

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = Color.FromArgb(90, 96, 105),
            Font = new Font("Segoe UI", 10F)
        };

        valueLabel.Text = "0";
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Font = new Font("Segoe UI Semibold", 28F, FontStyle.Bold);
        valueLabel.ForeColor = Color.FromArgb(28, 32, 38);
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;

        card.Controls.Add(valueLabel);
        card.Controls.Add(titleLabel);
        card.Controls.Add(accentBar);
        return card;
    }

    private Control BuildReminderSection()
    {
        _reminderPanel.Dock = DockStyle.Fill;
        _reminderPanel.BackColor = Color.FromArgb(255, 248, 230);
        _reminderPanel.Padding = new Padding(14);
        _reminderPanel.Margin = new Padding(0, 0, 0, 8);

        _reminderTitleLabel.Text = "Reminders — Follow Up Needed";
        _reminderTitleLabel.Dock = DockStyle.Top;
        _reminderTitleLabel.Height = 28;
        _reminderTitleLabel.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold);
        _reminderTitleLabel.ForeColor = Color.FromArgb(120, 70, 10);

        _reminderListBox.Dock = DockStyle.Fill;
        _reminderListBox.BorderStyle = BorderStyle.None;
        _reminderListBox.BackColor = Color.FromArgb(255, 248, 230);
        _reminderListBox.Font = new Font("Segoe UI", 11F);
        _reminderListBox.IntegralHeight = false;
        _reminderListBox.DoubleClick += ReminderListBox_DoubleClick;

        var hint = new Label
        {
            Text = "Double-click a reminder to open that application.",
            Dock = DockStyle.Bottom,
            Height = 22,
            ForeColor = Color.FromArgb(120, 90, 40),
            Font = new Font("Segoe UI", 9.5F)
        };

        _reminderPanel.Controls.Add(_reminderListBox);
        _reminderPanel.Controls.Add(hint);
        _reminderPanel.Controls.Add(_reminderTitleLabel);
        return _reminderPanel;
    }

    private Control BuildFilterSection()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoSize = false,
            Padding = new Padding(0, 8, 0, 8)
        };

        panel.Controls.Add(CreateInlineLabel("Search"));
        _searchTextBox.Width = 220;
        _searchTextBox.Height = 32;
        _searchTextBox.PlaceholderText = "Company or job title";
        panel.Controls.Add(_searchTextBox);

        panel.Controls.Add(CreateInlineLabel("Status"));
        _statusFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _statusFilterComboBox.Width = 150;
        _statusFilterComboBox.Items.Add("All");
        _statusFilterComboBox.Items.AddRange(ApplicationStatus.All);
        _statusFilterComboBox.SelectedIndex = 0;
        panel.Controls.Add(_statusFilterComboBox);

        _dateFilterEnabledCheckBox.Text = "Date applied";
        _dateFilterEnabledCheckBox.AutoSize = true;
        _dateFilterEnabledCheckBox.Margin = new Padding(16, 8, 8, 0);
        panel.Controls.Add(_dateFilterEnabledCheckBox);

        _dateFromPicker.Format = DateTimePickerFormat.Short;
        _dateFromPicker.Width = 120;
        _dateFromPicker.Enabled = false;
        panel.Controls.Add(_dateFromPicker);

        panel.Controls.Add(CreateInlineLabel("to"));
        _dateToPicker.Format = DateTimePickerFormat.Short;
        _dateToPicker.Width = 120;
        _dateToPicker.Enabled = false;
        panel.Controls.Add(_dateToPicker);

        StyleSecondaryButton(_clearFiltersButton, "Clear Filters");
        _clearFiltersButton.Width = 130;
        _clearFiltersButton.Margin = new Padding(16, 0, 0, 0);
        panel.Controls.Add(_clearFiltersButton);

        return panel;
    }

    private Control BuildGridSection()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            BackColor = Color.White
        };

        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.ReadOnly = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowHeadersVisible = false;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(232, 236, 241);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(40, 45, 52);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        _grid.ColumnHeadersHeight = 38;
        _grid.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(210, 228, 245);
        _grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        _grid.RowTemplate.Height = 34;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        _grid.CellDoubleClick += (_, _) => EditSelected();

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "Id", Visible = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Company", HeaderText = "Company", FillWeight = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Job Title", FillWeight = 140 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DateApplied", HeaderText = "Date Applied", FillWeight = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Salary", HeaderText = "Salary / Pay", FillWeight = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Contact", HeaderText = "Contact", FillWeight = 100 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Interview", HeaderText = "Interview", FillWeight = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "FollowUp", HeaderText = "Follow-Up", FillWeight = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Url", HeaderText = "Job Link", FillWeight = 120 });

        panel.Controls.Add(_grid);
        return panel;
    }

    private Control BuildButtonBar()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 8, 0, 0)
        };

        StylePrimaryButton(_finishButton, "Application URL");
        StyleSecondaryButton(_addButton, "Add Application");
        StyleSecondaryButton(_editButton, "Edit");
        StyleSecondaryButton(_deleteButton, "Delete");
        StyleSecondaryButton(_refreshButton, "Refresh");
        StyleSecondaryButton(_openLinkButton, "Open Job Link");
        StyleSecondaryButton(_exportButton, "Export CSV");
        StyleSecondaryButton(_backupButton, "Backup Database");

        _finishButton.Width = 160;
        _addButton.Width = 150;
        _openLinkButton.Width = 150;
        _exportButton.Width = 130;
        _backupButton.Width = 160;

        panel.Controls.AddRange(new Control[]
        {
            _finishButton, _addButton, _editButton, _deleteButton, _refreshButton,
            _openLinkButton, _exportButton, _backupButton
        });

        return panel;
    }

    private void WireEvents()
    {
        _finishButton.Click += (_, _) => FinishApplication();
        _addButton.Click += (_, _) => AddApplication();
        _editButton.Click += (_, _) => EditSelected();
        _deleteButton.Click += (_, _) => DeleteSelected();
        _refreshButton.Click += (_, _) => RefreshAll();
        _openLinkButton.Click += (_, _) => OpenSelectedJobLink();
        _exportButton.Click += (_, _) => ExportCsv();
        _backupButton.Click += (_, _) => BackupDatabase();
        _clearFiltersButton.Click += (_, _) => ClearFilters();

        _searchTextBox.TextChanged += (_, _) => ApplyFilters();
        _statusFilterComboBox.SelectedIndexChanged += (_, _) => ApplyFilters();
        _dateFromPicker.ValueChanged += (_, _) => ApplyFilters();
        _dateToPicker.ValueChanged += (_, _) => ApplyFilters();
        _dateFilterEnabledCheckBox.CheckedChanged += (_, _) =>
        {
            _dateFromPicker.Enabled = _dateFilterEnabledCheckBox.Checked;
            _dateToPicker.Enabled = _dateFilterEnabledCheckBox.Checked;
            ApplyFilters();
        };

        KeyDown += MainForm_KeyDown;
    }

    private void StartBrowserBridge()
    {
        _bridgeServer.ApplicationReceived += BridgeServer_ApplicationReceived;
        _bridgeServer.StatusChanged += BridgeServer_StatusChanged;

        try
        {
            _bridgeServer.Start();
        }
        catch (Exception ex)
        {
            _bridgeStatusLabel.ForeColor = Color.FromArgb(140, 45, 45);
            _bridgeStatusLabel.Text =
                $"Browser link failed to start ({ex.Message}). Keep the app open and retry, or use Application URL manually.";
        }
    }

    private void BridgeServer_StatusChanged(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        void update()
        {
            _bridgeStatusLabel.ForeColor = Color.FromArgb(30, 90, 55);
            _bridgeStatusLabel.Text =
                $"{message}  |  Keep this app open while applying online. Extension sends jobs here automatically.";
        }

        if (!IsHandleCreated)
        {
            HandleCreated += (_, _) => BeginInvoke(update);
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(update);
        }
        else
        {
            update();
        }
    }

    private void BridgeServer_ApplicationReceived(JobApplication application)
    {
        if (IsDisposed)
        {
            return;
        }

        void update()
        {
            RefreshAll();
            _bridgeStatusLabel.ForeColor = Color.FromArgb(26, 86, 138);
            _bridgeStatusLabel.Text =
                $"Saved from browser: {application.CompanyName} — {application.JobTitle}";

            NotifyIconBalloon(
                "Application saved",
                $"{application.CompanyName} — {application.JobTitle}");
        }

        if (!IsHandleCreated)
        {
            HandleCreated += (_, _) => BeginInvoke(update);
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(update);
        }
        else
        {
            update();
        }
    }

    private void NotifyIconBalloon(string title, string text)
    {
        // Lightweight notice without requiring a tray icon lifetime.
        try
        {
            using var notify = new NotifyIcon
            {
                Visible = true,
                Icon = SystemIcons.Information,
                BalloonTipTitle = title,
                BalloonTipText = text,
                BalloonTipIcon = ToolTipIcon.Info
            };
            notify.ShowBalloonTip(3000);
        }
        catch
        {
            // Optional UI only.
        }
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.N)
        {
            FinishApplication();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F5)
        {
            RefreshAll();
            e.Handled = true;
        }
    }

    private void RefreshAll(bool showStartupReminders = false)
    {
        ApplyFilters();
        RefreshDashboard();
        RefreshReminders(showStartupReminders);
    }

    private void ApplyFilters()
    {
        if (_suppressFilterEvents)
        {
            return;
        }

        try
        {
            DateTime? from = null;
            DateTime? to = null;

            if (_dateFilterEnabledCheckBox.Checked)
            {
                from = _dateFromPicker.Value.Date;
                to = _dateToPicker.Value.Date;
                if (from > to)
                {
                    (from, to) = (to, from);
                }
            }

            var status = _statusFilterComboBox.SelectedItem?.ToString();
            _currentRows = DatabaseHelper.SearchApplications(
                _searchTextBox.Text,
                status,
                from,
                to);

            BindGrid(_currentRows);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to load applications.\n\n{ex.Message}",
                "Database Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void BindGrid(IEnumerable<JobApplication> applications)
    {
        _grid.Rows.Clear();

        foreach (var app in applications)
        {
            var rowIndex = _grid.Rows.Add(
                app.Id,
                app.CompanyName,
                app.JobTitle,
                FormatDate(app.DateApplied),
                app.Status,
                app.SalaryOrPayRate,
                app.ContactName,
                FormatDate(app.InterviewDate),
                FormatDate(app.FollowUpDate),
                app.JobPostingUrl);

            if (app.NeedsFollowUp &&
                app.Status is not ApplicationStatus.Hired
                    and not ApplicationStatus.Rejected
                    and not ApplicationStatus.Withdrawn)
            {
                _grid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 243, 205);
            }
        }
    }

    private void RefreshDashboard()
    {
        try
        {
            var stats = DatabaseHelper.GetDashboardStats();
            _totalValueLabel.Text = stats.Total.ToString(CultureInfo.CurrentCulture);
            _underReviewValueLabel.Text = stats.UnderReview.ToString(CultureInfo.CurrentCulture);
            _interviewsValueLabel.Text = stats.Interviews.ToString(CultureInfo.CurrentCulture);
            _offersValueLabel.Text = stats.Offers.ToString(CultureInfo.CurrentCulture);
            _rejectionsValueLabel.Text = stats.Rejections.ToString(CultureInfo.CurrentCulture);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to refresh dashboard.\n\n{ex.Message}",
                "Database Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RefreshReminders(bool showStartupDialog)
    {
        try
        {
            var due = DatabaseHelper.GetFollowUpsDue();
            _reminderListBox.Items.Clear();

            if (due.Count == 0)
            {
                _reminderTitleLabel.Text = "Reminders — No follow-ups due";
                _reminderPanel.BackColor = Color.FromArgb(236, 245, 239);
                _reminderListBox.BackColor = Color.FromArgb(236, 245, 239);
                _reminderTitleLabel.ForeColor = Color.FromArgb(30, 90, 55);
                _reminderListBox.Items.Add("You're all caught up. Set a follow-up date on any application to get a reminder here.");
                return;
            }

            _reminderTitleLabel.Text = $"Reminders — Follow Up Needed ({due.Count})";
            _reminderPanel.BackColor = Color.FromArgb(255, 248, 230);
            _reminderListBox.BackColor = Color.FromArgb(255, 248, 230);
            _reminderTitleLabel.ForeColor = Color.FromArgb(120, 70, 10);

            foreach (var app in due)
            {
                var when = app.FollowUpDate!.Value.Date == DateTime.Today
                    ? "Today"
                    : app.FollowUpDate.Value.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);

                var overdue = app.FollowUpDate.Value.Date < DateTime.Today ? " (overdue)" : string.Empty;
                _reminderListBox.Items.Add(
                    new ReminderItem(
                        app.Id,
                        $"Contact {app.CompanyName} — {app.JobTitle} · {when}{overdue}"));
            }

            if (showStartupDialog)
            {
                ShowStartupReminderDialog(due);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to load reminders.\n\n{ex.Message}",
                "Database Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ShowStartupReminderDialog(List<JobApplication> due)
    {
        var lines = due
            .Take(8)
            .Select(app =>
            {
                var dateText = app.FollowUpDate!.Value.Date == DateTime.Today
                    ? "today"
                    : app.FollowUpDate.Value.ToString("MMMM d", CultureInfo.CurrentCulture);
                return $"• Contact {app.CompanyName} ({app.JobTitle}) — {dateText}";
            });

        var extra = due.Count > 8 ? $"\n…and {due.Count - 8} more." : string.Empty;

        MessageBox.Show(
            $"You have {due.Count} follow-up reminder{(due.Count == 1 ? string.Empty : "s")}:\n\n" +
            string.Join("\n", lines) + extra +
            "\n\nThey are also listed in the Reminders section at the top of the window.",
            "Follow-Up Reminders",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void FinishApplication()
    {
        using var dialog = new ApplicationEditForm(existing: null, finishMode: true);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            DatabaseHelper.Insert(dialog.ApplicationData);
            RefreshAll();

            var app = dialog.ApplicationData;
            MessageBox.Show(
                $"Saved to your tracker:\n\n{app.CompanyName} — {app.JobTitle}\nStatus: {app.Status}\n\n" +
                "The dashboard, reminders, and application list have been updated.",
                "Application Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to save the application.\n\n{ex.Message}",
                "Save Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void AddApplication()
    {
        using var dialog = new ApplicationEditForm(existing: null, finishMode: false);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            DatabaseHelper.Insert(dialog.ApplicationData);
            RefreshAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to save the application.\n\n{ex.Message}",
                "Save Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void EditSelected()
    {
        var selected = GetSelectedApplication();
        if (selected is null)
        {
            MessageBox.Show(
                "Select an application to edit.",
                "Edit Application",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var dialog = new ApplicationEditForm(selected);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            DatabaseHelper.Update(dialog.ApplicationData);
            RefreshAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to update the application.\n\n{ex.Message}",
                "Save Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void DeleteSelected()
    {
        var selected = GetSelectedApplication();
        if (selected is null)
        {
            MessageBox.Show(
                "Select an application to delete.",
                "Delete Application",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Delete the application for {selected.CompanyName} — {selected.JobTitle}?",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        try
        {
            DatabaseHelper.Delete(selected.Id);
            RefreshAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to delete the application.\n\n{ex.Message}",
                "Delete Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OpenSelectedJobLink()
    {
        var selected = GetSelectedApplication();
        if (selected is null)
        {
            MessageBox.Show(
                "Select an application first.",
                "Open Job Link",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(selected.JobPostingUrl))
        {
            MessageBox.Show(
                "This application does not have a job posting URL.",
                "Open Job Link",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            var url = selected.JobPostingUrl.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to open the link.\n\n{ex.Message}",
                "Open Job Link",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ExportCsv()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Export Applications to CSV",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"job-applications-{DateTime.Now:yyyyMMdd}.csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            DatabaseHelper.ExportToCsv(dialog.FileName, _currentRows);
            MessageBox.Show(
                $"Exported {_currentRows.Count} application(s) to:\n{dialog.FileName}",
                "Export Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to export CSV.\n\n{ex.Message}",
                "Export Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void BackupDatabase()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose a folder for the database backup",
            UseDescriptionForTitle = true
        };

        // Prefer a simple SaveFileDialog-like flow: copy into chosen folder.
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var path = DatabaseHelper.CreateBackup(dialog.SelectedPath);
            MessageBox.Show(
                $"Backup created:\n{path}",
                "Backup Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to create backup.\n\n{ex.Message}",
                "Backup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ClearFilters()
    {
        _suppressFilterEvents = true;
        _searchTextBox.Text = string.Empty;
        _statusFilterComboBox.SelectedIndex = 0;
        _dateFilterEnabledCheckBox.Checked = false;
        _dateFromPicker.Value = DateTime.Today.AddMonths(-3);
        _dateToPicker.Value = DateTime.Today;
        _suppressFilterEvents = false;
        ApplyFilters();
    }

    private void ReminderListBox_DoubleClick(object? sender, EventArgs e)
    {
        if (_reminderListBox.SelectedItem is not ReminderItem item)
        {
            return;
        }

        var application = DatabaseHelper.GetById(item.ApplicationId);
        if (application is null)
        {
            return;
        }

        using var dialog = new ApplicationEditForm(application);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            DatabaseHelper.Update(dialog.ApplicationData);
            RefreshAll();
        }
    }

    private JobApplication? GetSelectedApplication()
    {
        if (_grid.CurrentRow is null || _grid.CurrentRow.IsNewRow)
        {
            return null;
        }

        var rawId = _grid.CurrentRow.Cells["Id"].Value;
        if (rawId is null)
        {
            return null;
        }

        var idValue = Convert.ToInt32(rawId, CultureInfo.InvariantCulture);
        return DatabaseHelper.GetById(idValue);
    }

    private static string FormatDate(DateTime? value)
    {
        return value?.ToString("MMM d, yyyy", CultureInfo.CurrentCulture) ?? string.Empty;
    }

    private static Label CreateInlineLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(8, 10, 4, 0),
            Font = new Font("Segoe UI", 10.5F)
        };
    }

    private static void StylePrimaryButton(Button button, string text)
    {
        button.Text = text;
        button.Height = 42;
        button.Width = 120;
        button.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        button.BackColor = Color.FromArgb(26, 86, 138);
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Margin = new Padding(0, 0, 10, 0);
        button.Cursor = Cursors.Hand;
    }

    private static void StyleSecondaryButton(Button button, string text)
    {
        button.Text = text;
        button.Height = 42;
        button.Width = 110;
        button.Font = new Font("Segoe UI", 11F);
        button.BackColor = Color.White;
        button.ForeColor = Color.FromArgb(35, 40, 48);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(190, 198, 208);
        button.FlatAppearance.BorderSize = 1;
        button.Margin = new Padding(0, 0, 10, 0);
        button.Cursor = Cursors.Hand;
    }

    private sealed class ReminderItem
    {
        public ReminderItem(int applicationId, string displayText)
        {
            ApplicationId = applicationId;
            DisplayText = displayText;
        }

        public int ApplicationId { get; }
        public string DisplayText { get; }

        public override string ToString() => DisplayText;
    }
}
