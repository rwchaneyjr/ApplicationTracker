namespace JobApplicationTracker.Models;

/// <summary>
/// Represents a single job application tracked by the user.
/// </summary>
public class JobApplication
{
    public int Id { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    public string JobTitle { get; set; } = string.Empty;

    public DateTime? DateApplied { get; set; }

    public string Status { get; set; } = ApplicationStatus.Interested;

    public string SalaryOrPayRate { get; set; } = string.Empty;

    public string JobPostingUrl { get; set; } = string.Empty;

    public string ContactName { get; set; } = string.Empty;

    public string ContactEmail { get; set; } = string.Empty;

    public DateTime? InterviewDate { get; set; }

    public DateTime? FollowUpDate { get; set; }

    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// True when a follow-up date is set and is today or earlier.
    /// </summary>
    public bool NeedsFollowUp
    {
        get
        {
            if (!FollowUpDate.HasValue)
            {
                return false;
            }

            return FollowUpDate.Value.Date <= DateTime.Today;
        }
    }

    /// <summary>
    /// Validates required fields. Returns an error message, or null when valid.
    /// </summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            return "Company name is required.";
        }

        if (string.IsNullOrWhiteSpace(JobTitle))
        {
            return "Job title is required.";
        }

        if (string.IsNullOrWhiteSpace(Status))
        {
            return "Application status is required.";
        }

        return null;
    }
}
