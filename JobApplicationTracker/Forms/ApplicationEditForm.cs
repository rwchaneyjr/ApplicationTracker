using System.Diagnostics;
using System.Globalization;
using JobApplicationTracker.Data;
using JobApplicationTracker.Models;

namespace JobApplicationTracker.Forms;

/// <summary>
/// Dialog used to add or edit a single job application.
/// </summary>
public class ApplicationEditForm : Form
{
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

    public ApplicationEditForm(JobApplication? existing = null)
    {
        ApplicationData = existing is null
            ? new JobApplication
            {
                DateApplied = DateTime.Today,
                Status = ApplicationStatus.Interested
            }
            : Clone(existing);

        Text = existing is null ? "Add Application" : "Edit Application";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 11F);
        ClientSize = new Size(640, 620);
        BackColor = Color.White;
        Padding = new Padding(20);

        BuildLayout();
        LoadValues();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 13,
            Padding = new Padding(4)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (var i = 0; i < 12; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
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
        layout.RowStyles[10] = new RowStyle(SizeType.Percent, 100);
        layout.RowStyles[11] = new RowStyle(SizeType.Absolute, 8);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 54,
            Padding = new Padding(0, 8, 0, 0)
        };

        StylePrimaryButton(_saveButton, "Save");
        StyleSecondaryButton(_cancelButton, "Cancel");
        _saveButton.Click += SaveButton_Click;
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        buttonPanel.Controls.Add(_saveButton);
        buttonPanel.Controls.Add(_cancelButton);

        Controls.Add(layout);
        Controls.Add(buttonPanel);

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;
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
    }
}
