namespace JobApplicationTracker.Models;

/// <summary>
/// Allowed status values for a job application.
/// </summary>
public static class ApplicationStatus
{
    public const string Interested = "Interested";
    public const string Applied = "Applied";
    public const string UnderReview = "Under Review";
    public const string Assessment = "Assessment";
    public const string Interview = "Interview";
    public const string Offer = "Offer";
    public const string Hired = "Hired";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";

    /// <summary>
    /// All selectable status values in display order.
    /// </summary>
    public static readonly string[] All =
    {
        Interested,
        Applied,
        UnderReview,
        Assessment,
        Interview,
        Offer,
        Hired,
        Rejected,
        Withdrawn
    };
}
