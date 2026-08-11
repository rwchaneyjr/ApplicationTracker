using JobApplicationTracker.Data;
using JobApplicationTracker.Models;

namespace JobApplicationTracker.Forms;

/// <summary>
/// Dialog used to finish, add, or edit a job application.
/// Includes a paste box that fills blank fields from a job posting.
/// </summary>
public class ApplicationEditForm : Form
{
    private readonly bool _isNew;
    private readonly bool _finishMode;

    private readonly TextBox _pasteTextBox = new();
    private readonly Button _getUrlButton = new();
    private readonly Button _fillBlanksButton = new();
    private readonly Label _fillStatusLabel = new();

    private readonly TextBox _companyTextBox = new();
    private readonly TextBox _jobTitleTextBox = new();
    private readonly DateTimePicker _dateAppliedPicker = new();
    private readonly CheckBox _dateAppliedClearCheckBox = new();
    private readonly ComboBox _statusComboBox = new();
    private readonly TextBox _salaryTextBox = new();
    private readonly TextBox _urlTextBox = new();
    private readonly TextBox _contactNameTextBox = new();
    private readonly TextBox _contactEmailTextBox = new();
    private readonly DateTimePicker _interviewPicker = new();
    private readonly CheckBox _interviewEnabledCheckBox = new();
    private readonly DateTimePicker _followUpPicker = new();
    private readonly CheckBox _followUpEnabledCheckBox = new();
    private readonly TextBox _notesTextBox = new();
    private readonly Button _saveButton = new();
    private readonly Button _cancelButton = new();

    public JobApplication ApplicationData { get; private set; }

    /// <param name="existing">Existing application when editing; null when adding.</param>
    /// <param name="finishMode">
    /// When true (new application), defaults to Applied + today's date and uses
    /// "Finish Application" wording for the save button.
    /// </param>
    public ApplicationEditForm(JobApplication? existing = null, bool finishMode = false)
    {
        _isNew = existing is null;
        _finishMode = finishMode && _isNew;

        ApplicationData = existing is null
            ? new JobApplication
            {
                DateApplied = DateTime.Today,
                Status = _finishMode ? ApplicationStatus.Applied : ApplicationStatus.Interested
            }
            : Clone(existing);

        Text = _finishMode
            ? "Finish Application"
            : _isNew ? "Add Application" : "Edit Application";

        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 11F);
        ClientSize = new Size(720, 800);
        BackColor = Color.White;
        Padding = new Padding(16);

        BuildLayout();
        LoadValues();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(4)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 175));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        root.Controls.Add(BuildPastePanel(), 0, 0);
        root.Controls.Add(BuildFieldsPanel(), 0, 1);
        root.Controls.Add(BuildButtonPanel(), 0, 2);

        Controls.Add(root);
        AcceptButton = _saveButton;
        CancelButton = _cancelButton;
    }

    private Control BuildPastePanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(240, 246, 252),
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 8)
        };

        var title = new Label
        {
            Text = "Get Apply URL from browser, or paste a job posting",
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(26, 86, 138)
        };

        var hint = new Label
        {
            Text = "Open the job page in Chrome/Edge, then click Get Apply URL — copies the link, screenshots the page, and fills blank fields.",
            Dock = DockStyle.Top,
            Height = 34,
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(70, 80, 95)
        };

        _pasteTextBox.Multiline = true;
        _pasteTextBox.ScrollBars = ScrollBars.Vertical;
        _pasteTextBox.Dock = DockStyle.Fill;
        _pasteTextBox.Font = new Font("Segoe UI", 10F);
        _pasteTextBox.PlaceholderText = "Paste the job posting text here, or use Get Apply URL…";

        var actionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };

        StylePrimaryButton(_getUrlButton, "Get Apply URL");
        _getUrlButton.Width = 150;
        _getUrlButton.Click += GetUrlButton_Click;

        StyleSecondaryButton(_fillBlanksButton, "Fill Blanks");
        _fillBlanksButton.Width = 130;
        _fillBlanksButton.Click += FillBlanksButton_Click;

        _fillStatusLabel.AutoSize = true;
        _fillStatusLabel.Margin = new Padding(12, 10, 0, 0);
        _fillStatusLabel.ForeColor = Color.FromArgb(40, 90, 55);
        _fillStatusLabel.Font = new Font("Segoe UI", 10F);

        actionRow.Controls.Add(_getUrlButton);
        actionRow.Controls.Add(_fillBlanksButton);
        actionRow.Controls.Add(_fillStatusLabel);

        panel.Controls.Add(_pasteTextBox);
        panel.Controls.Add(actionRow);
        panel.Controls.Add(hint);
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildFieldsPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 12,
            Padding = new Padding(0, 4, 0, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (var i = 0; i < 11; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        AddLabeledControl(layout, 0, "Company name *", _companyTextBox);
        AddLabeledControl(layout, 1, "Job title *", _jobTitleTextBox);

        ConfigureDatePicker(_dateAppliedPicker);
        _dateAppliedClearCheckBox.Text = "Clear date";
        _dateAppliedClearCheckBox.AutoSize = true;
        _dateAppliedClearCheckBox.CheckedChanged += (_, _) =>
        {
            _dateAppliedPicker.Enabled = !_dateAppliedClearCheckBox.Checked;
        };

        var dateAppliedPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoSize = true
        };
        dateAppliedPanel.Controls.Add(_dateAppliedPicker);
        dateAppliedPanel.Controls.Add(_dateAppliedClearCheckBox);
        AddLabeledControl(layout, 2, "Date applied", dateAppliedPanel);

        _statusComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _statusComboBox.Dock = DockStyle.Fill;
        _statusComboBox.Items.AddRange(ApplicationStatus.All);
        AddLabeledControl(layout, 3, "Status", _statusComboBox);

        AddLabeledControl(layout, 4, "Salary / pay rate", _salaryTextBox);
        AddLabeledControl(layout, 5, "Job posting URL", _urlTextBox);
        AddLabeledControl(layout, 6, "Contact / recruiter", _contactNameTextBox);
        AddLabeledControl(layout, 7, "Contact email", _contactEmailTextBox);

        ConfigureOptionalDate(layout, 8, "Interview date", _interviewPicker, _interviewEnabledCheckBox);
        ConfigureOptionalDate(layout, 9, "Follow-up date", _followUpPicker, _followUpEnabledCheckBox);

        _notesTextBox.Multiline = true;
        _notesTextBox.ScrollBars = ScrollBars.Vertical;
        _notesTextBox.Dock = DockStyle.Fill;
        _notesTextBox.AcceptsReturn = true;
        AddLabeledControl(layout, 10, "Notes", _notesTextBox);
        layout.SetRowSpan(_notesTextBox, 2);

        return layout;
    }

    private Control BuildButtonPanel()
    {
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0)
        };

        var saveText = _finishMode ? "Save to Tracker" : "Save";
        StylePrimaryButton(_saveButton, saveText);
        _saveButton.Width = _finishMode ? 160 : 120;
        StyleSecondaryButton(_cancelButton, "Cancel");

        _saveButton.Click += SaveButton_Click;
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        buttonPanel.Controls.Add(_saveButton);
        buttonPanel.Controls.Add(_cancelButton);
        return buttonPanel;
    }

    private void GetUrlButton_Click(object? sender, EventArgs e)
    {
        _getUrlButton.Enabled = false;
        _fillStatusLabel.ForeColor = Color.FromArgb(70, 80, 95);
        _fillStatusLabel.Text = "Capturing browser page…";
        Application.DoEvents();

        try
        {
            var result = BrowserCaptureHelper.CaptureFromBrowser();
            if (!result.Success || result.Parsed is null)
            {
                _fillStatusLabel.ForeColor = Color.FromArgb(140, 80, 20);
                _fillStatusLabel.Text = result.Message;
                MessageBox.Show(
                    result.Message,
                    "Get Apply URL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // Put captured text into the paste box so the user can see / edit it.
            var pasteBuilder = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(result.Parsed.JobTitle))
            {
                pasteBuilder.AppendLine(result.Parsed.JobTitle);
            }

            if (!string.IsNullOrWhiteSpace(result.Parsed.CompanyName))
            {
                pasteBuilder.AppendLine("Company: " + result.Parsed.CompanyName);
            }

            if (!string.IsNullOrWhiteSpace(result.Url))
            {
                pasteBuilder.AppendLine(result.Url);
            }

            if (!string.IsNullOrWhiteSpace(result.WindowTitle))
            {
                pasteBuilder.AppendLine(result.WindowTitle);
            }

            _pasteTextBox.Text = pasteBuilder.ToString().Trim();

            var draft = CaptureCurrentValues();
            var filledCount = JobPostingParser.FillBlanks(draft, result.Parsed);

            _companyTextBox.Text = draft.CompanyName;
            _jobTitleTextBox.Text = draft.JobTitle;
            _salaryTextBox.Text = draft.SalaryOrPayRate;
            _urlTextBox.Text = draft.JobPostingUrl;
            _contactNameTextBox.Text = draft.ContactName;
            _contactEmailTextBox.Text = draft.ContactEmail;
            _notesTextBox.Text = draft.Notes;

            _fillStatusLabel.ForeColor = Color.FromArgb(40, 90, 55);
            _fillStatusLabel.Text = $"{result.Message} Filled {filledCount} blank field(s).";
        }
        catch (Exception ex)
        {
            _fillStatusLabel.ForeColor = Color.FromArgb(140, 45, 45);
            _fillStatusLabel.Text = "Capture failed.";
            MessageBox.Show(
                $"Unable to capture from the browser.\n\n{ex.Message}",
                "Get Apply URL",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _getUrlButton.Enabled = true;
            try
            {
                // Return focus to this dialog after touching the browser window.
                Activate();
            }
            catch
            {
                // Ignore.
            }
        }
    }

    private void FillBlanksButton_Click(object? sender, EventArgs e)
    {
        var pasted = _pasteTextBox.Text;
        if (string.IsNullOrWhiteSpace(pasted))
        {
            MessageBox.Show(
                "Paste a job posting into the box first, then press Fill Blanks.",
                "Nothing to Fill",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var draft = CaptureCurrentValues();
        var parsed = JobPostingParser.Parse(pasted);
        var filledCount = JobPostingParser.FillBlanks(draft, parsed);

        // Push filled values back into the form controls.
        _companyTextBox.Text = draft.CompanyName;
        _jobTitleTextBox.Text = draft.JobTitle;
        _salaryTextBox.Text = draft.SalaryOrPayRate;
        _urlTextBox.Text = draft.JobPostingUrl;
        _contactNameTextBox.Text = draft.ContactName;
        _contactEmailTextBox.Text = draft.ContactEmail;
        _notesTextBox.Text = draft.Notes;

        if (filledCount == 0)
        {
            _fillStatusLabel.ForeColor = Color.FromArgb(140, 80, 20);
            _fillStatusLabel.Text = "No blank fields could be filled from that text.";
        }
        else
        {
            _fillStatusLabel.ForeColor = Color.FromArgb(40, 90, 55);
            _fillStatusLabel.Text = $"Filled {filledCount} blank field{(filledCount == 1 ? string.Empty : "s")}.";
        }
    }

    private JobApplication CaptureCurrentValues()
    {
        return new JobApplication
        {
            Id = ApplicationData.Id,
            CompanyName = _companyTextBox.Text.Trim(),
            JobTitle = _jobTitleTextBox.Text.Trim(),
            DateApplied = _dateAppliedClearCheckBox.Checked ? null : _dateAppliedPicker.Value.Date,
            Status = _statusComboBox.SelectedItem?.ToString() ?? ApplicationStatus.Interested,
            SalaryOrPayRate = _salaryTextBox.Text.Trim(),
            JobPostingUrl = _urlTextBox.Text.Trim(),
            ContactName = _contactNameTextBox.Text.Trim(),
            ContactEmail = _contactEmailTextBox.Text.Trim(),
            InterviewDate = _interviewEnabledCheckBox.Checked ? _interviewPicker.Value.Date : null,
            FollowUpDate = _followUpEnabledCheckBox.Checked ? _followUpPicker.Value.Date : null,
            Notes = _notesTextBox.Text.Trim()
        };
    }

    private static void AddLabeledControl(TableLayoutPanel layout, int row, string labelText, Control control)
    {
        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 11F, FontStyle.Regular)
        };

        control.Dock = DockStyle.Fill;
        if (control is TextBox textBox && !textBox.Multiline)
        {
            textBox.Height = 30;
        }

        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private static void ConfigureDatePicker(DateTimePicker picker)
    {
        picker.Format = DateTimePickerFormat.Short;
        picker.Width = 160;
        picker.ShowCheckBox = false;
    }

    private void ConfigureOptionalDate(
        TableLayoutPanel layout,
        int row,
        string label,
        DateTimePicker picker,
        CheckBox enabledCheckBox)
    {
        ConfigureDatePicker(picker);
        enabledCheckBox.Text = "Set date";
        enabledCheckBox.AutoSize = true;
        enabledCheckBox.CheckedChanged += (_, _) =>
        {
            picker.Enabled = enabledCheckBox.Checked;
        };
        picker.Enabled = false;

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoSize = true
        };
        panel.Controls.Add(enabledCheckBox);
        panel.Controls.Add(picker);
        AddLabeledControl(layout, row, label, panel);
    }

    private void LoadValues()
    {
        _companyTextBox.Text = ApplicationData.CompanyName;
        _jobTitleTextBox.Text = ApplicationData.JobTitle;

        if (ApplicationData.DateApplied.HasValue)
        {
            _dateAppliedPicker.Value = ApplicationData.DateApplied.Value;
            _dateAppliedClearCheckBox.Checked = false;
            _dateAppliedPicker.Enabled = true;
        }
        else
        {
            _dateAppliedPicker.Value = DateTime.Today;
            _dateAppliedClearCheckBox.Checked = true;
            _dateAppliedPicker.Enabled = false;
        }

        _statusComboBox.SelectedItem = ApplicationStatus.All.Contains(ApplicationData.Status)
            ? ApplicationData.Status
            : ApplicationStatus.Interested;

        _salaryTextBox.Text = ApplicationData.SalaryOrPayRate;
        _urlTextBox.Text = ApplicationData.JobPostingUrl;
        _contactNameTextBox.Text = ApplicationData.ContactName;
        _contactEmailTextBox.Text = ApplicationData.ContactEmail;

        if (ApplicationData.InterviewDate.HasValue)
        {
            _interviewEnabledCheckBox.Checked = true;
            _interviewPicker.Value = ApplicationData.InterviewDate.Value;
            _interviewPicker.Enabled = true;
        }

        if (ApplicationData.FollowUpDate.HasValue)
        {
            _followUpEnabledCheckBox.Checked = true;
            _followUpPicker.Value = ApplicationData.FollowUpDate.Value;
            _followUpPicker.Enabled = true;
        }

        _notesTextBox.Text = ApplicationData.Notes;
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        ApplicationData.CompanyName = _companyTextBox.Text.Trim();
        ApplicationData.JobTitle = _jobTitleTextBox.Text.Trim();
        ApplicationData.DateApplied = _dateAppliedClearCheckBox.Checked
            ? null
            : _dateAppliedPicker.Value.Date;
        ApplicationData.Status = _statusComboBox.SelectedItem?.ToString() ?? ApplicationStatus.Interested;
        ApplicationData.SalaryOrPayRate = _salaryTextBox.Text.Trim();
        ApplicationData.JobPostingUrl = _urlTextBox.Text.Trim();
        ApplicationData.ContactName = _contactNameTextBox.Text.Trim();
        ApplicationData.ContactEmail = _contactEmailTextBox.Text.Trim();
        ApplicationData.InterviewDate = _interviewEnabledCheckBox.Checked
            ? _interviewPicker.Value.Date
            : null;
        ApplicationData.FollowUpDate = _followUpEnabledCheckBox.Checked
            ? _followUpPicker.Value.Date
            : null;
        ApplicationData.Notes = _notesTextBox.Text.Trim();

        var validationError = ApplicationData.Validate();
        if (validationError is not null)
        {
            MessageBox.Show(
                validationError,
                "Missing Information",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static JobApplication Clone(JobApplication source)
    {
        return new JobApplication
        {
            Id = source.Id,
            CompanyName = source.CompanyName,
            JobTitle = source.JobTitle,
            DateApplied = source.DateApplied,
            Status = source.Status,
            SalaryOrPayRate = source.SalaryOrPayRate,
            JobPostingUrl = source.JobPostingUrl,
            ContactName = source.ContactName,
            ContactEmail = source.ContactEmail,
            InterviewDate = source.InterviewDate,
            FollowUpDate = source.FollowUpDate,
            Notes = source.Notes
        };
    }

    private static void StylePrimaryButton(Button button, string text)
    {
        button.Text = text;
        button.Width = 120;
        button.Height = 40;
        button.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        button.BackColor = Color.FromArgb(26, 86, 138);
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Margin = new Padding(8, 0, 0, 0);
        button.Cursor = Cursors.Hand;
    }

    private static void StyleSecondaryButton(Button button, string text)
    {
        button.Text = text;
        button.Width = 120;
        button.Height = 40;
        button.Font = new Font("Segoe UI", 11F);
        button.BackColor = Color.FromArgb(235, 238, 242);
        button.ForeColor = Color.FromArgb(40, 45, 52);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Margin = new Padding(8, 0, 0, 0);
        button.Cursor = Cursors.Hand;
    }
}
