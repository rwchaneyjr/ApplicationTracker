using System.Globalization;
using System.IO;
using System.Text;
using JobApplicationTracker.Models;
using Microsoft.Data.Sqlite;

namespace JobApplicationTracker.Data;

/// <summary>
/// Handles all SQLite database operations for job applications.
/// The database file is stored in the user's local AppData folder.
/// </summary>
public static class DatabaseHelper
{
    private static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JobApplicationTracker");

    public static string DatabasePath { get; } = Path.Combine(DataFolder, "applications.db");

    private static string ConnectionString => $"Data Source={DatabasePath}";

    /// <summary>
    /// Creates the data folder and applications table if they do not already exist.
    /// </summary>
    public static void InitializeDatabase()
    {
        Directory.CreateDirectory(DataFolder);

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS Applications (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyName TEXT NOT NULL,
                JobTitle TEXT NOT NULL,
                DateApplied TEXT NULL,
                Status TEXT NOT NULL,
                SalaryOrPayRate TEXT NULL,
                JobPostingUrl TEXT NULL,
                ContactName TEXT NULL,
                ContactEmail TEXT NULL,
                InterviewDate TEXT NULL,
                FollowUpDate TEXT NULL,
                Notes TEXT NULL
            );
            """;

        using var command = connection.CreateCommand();
        command.CommandText = createTableSql;
        command.ExecuteNonQuery();
    }

    public static List<JobApplication> GetAllApplications()
    {
        var results = new List<JobApplication>();

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, CompanyName, JobTitle, DateApplied, Status, SalaryOrPayRate,
                   JobPostingUrl, ContactName, ContactEmail, InterviewDate, FollowUpDate, Notes
            FROM Applications
            ORDER BY
                CASE WHEN FollowUpDate IS NOT NULL AND date(FollowUpDate) <= date('now', 'localtime') THEN 0 ELSE 1 END,
                COALESCE(DateApplied, '0001-01-01') DESC,
                CompanyName COLLATE NOCASE;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadApplication(reader));
        }

        return results;
    }

    public static List<JobApplication> SearchApplications(
        string? searchText,
        string? statusFilter,
        DateTime? dateFrom,
        DateTime? dateTo)
    {
        var results = new List<JobApplication>();
        var whereParts = new List<string>();

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            whereParts.Add("(CompanyName LIKE $search OR JobTitle LIKE $search)");
            command.Parameters.AddWithValue("$search", $"%{searchText.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            !string.Equals(statusFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            whereParts.Add("Status = $status");
            command.Parameters.AddWithValue("$status", statusFilter);
        }

        if (dateFrom.HasValue)
        {
            whereParts.Add("DateApplied IS NOT NULL AND date(DateApplied) >= date($dateFrom)");
            command.Parameters.AddWithValue("$dateFrom", ToDbDate(dateFrom.Value));
        }

        if (dateTo.HasValue)
        {
            whereParts.Add("DateApplied IS NOT NULL AND date(DateApplied) <= date($dateTo)");
            command.Parameters.AddWithValue("$dateTo", ToDbDate(dateTo.Value));
        }

        var whereClause = whereParts.Count > 0
            ? "WHERE " + string.Join(" AND ", whereParts)
            : string.Empty;

        command.CommandText = $"""
            SELECT Id, CompanyName, JobTitle, DateApplied, Status, SalaryOrPayRate,
                   JobPostingUrl, ContactName, ContactEmail, InterviewDate, FollowUpDate, Notes
            FROM Applications
            {whereClause}
            ORDER BY
                CASE WHEN FollowUpDate IS NOT NULL AND date(FollowUpDate) <= date('now', 'localtime') THEN 0 ELSE 1 END,
                COALESCE(DateApplied, '0001-01-01') DESC,
                CompanyName COLLATE NOCASE;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadApplication(reader));
        }

        return results;
    }

    public static JobApplication? GetById(int id)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, CompanyName, JobTitle, DateApplied, Status, SalaryOrPayRate,
                   JobPostingUrl, ContactName, ContactEmail, InterviewDate, FollowUpDate, Notes
            FROM Applications
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return ReadApplication(reader);
        }

        return null;
    }

    public static int Insert(JobApplication application)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Applications (
                CompanyName, JobTitle, DateApplied, Status, SalaryOrPayRate,
                JobPostingUrl, ContactName, ContactEmail, InterviewDate, FollowUpDate, Notes)
            VALUES (
                $company, $title, $dateApplied, $status, $salary,
                $url, $contact, $email, $interview, $followUp, $notes);
            SELECT last_insert_rowid();
            """;

        BindApplication(command, application);
        var result = command.ExecuteScalar();
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    public static void Update(JobApplication application)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Applications SET
                CompanyName = $company,
                JobTitle = $title,
                DateApplied = $dateApplied,
                Status = $status,
                SalaryOrPayRate = $salary,
                JobPostingUrl = $url,
                ContactName = $contact,
                ContactEmail = $email,
                InterviewDate = $interview,
                FollowUpDate = $followUp,
                Notes = $notes
            WHERE Id = $id;
            """;

        BindApplication(command, application);
        command.Parameters.AddWithValue("$id", application.Id);
        command.ExecuteNonQuery();
    }

    public static void Delete(int id)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Applications WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public static DashboardStats GetDashboardStats()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                COUNT(*) AS Total,
                SUM(CASE WHEN Status = 'Under Review' THEN 1 ELSE 0 END) AS UnderReview,
                SUM(CASE WHEN Status = 'Interview' THEN 1 ELSE 0 END) AS Interviews,
                SUM(CASE WHEN Status = 'Offer' THEN 1 ELSE 0 END) AS Offers,
                SUM(CASE WHEN Status = 'Rejected' THEN 1 ELSE 0 END) AS Rejections
            FROM Applications;
            """;

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new DashboardStats
            {
                Total = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                UnderReview = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                Interviews = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                Offers = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                Rejections = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
            };
        }

        return new DashboardStats();
    }

    public static List<JobApplication> GetFollowUpsDue()
    {
        var results = new List<JobApplication>();

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, CompanyName, JobTitle, DateApplied, Status, SalaryOrPayRate,
                   JobPostingUrl, ContactName, ContactEmail, InterviewDate, FollowUpDate, Notes
            FROM Applications
            WHERE FollowUpDate IS NOT NULL
              AND date(FollowUpDate) <= date('now', 'localtime')
              AND Status NOT IN ('Hired', 'Rejected', 'Withdrawn')
            ORDER BY date(FollowUpDate) ASC, CompanyName COLLATE NOCASE;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadApplication(reader));
        }

        return results;
    }

    public static JobApplication? FindDuplicate(JobApplication application)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        if (!string.IsNullOrWhiteSpace(application.JobPostingUrl))
        {
            command.CommandText = """
                SELECT Id, CompanyName, JobTitle, DateApplied, Status, SalaryOrPayRate,
                       JobPostingUrl, ContactName, ContactEmail, InterviewDate, FollowUpDate, Notes
                FROM Applications
                WHERE JobPostingUrl IS NOT NULL
                  AND lower(trim(JobPostingUrl)) = lower(trim($url))
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$url", application.JobPostingUrl.Trim());
        }
        else
        {
            command.CommandText = """
                SELECT Id, CompanyName, JobTitle, DateApplied, Status, SalaryOrPayRate,
                       JobPostingUrl, ContactName, ContactEmail, InterviewDate, FollowUpDate, Notes
                FROM Applications
                WHERE lower(trim(CompanyName)) = lower(trim($company))
                  AND lower(trim(JobTitle)) = lower(trim($title))
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$company", application.CompanyName.Trim());
            command.Parameters.AddWithValue("$title", application.JobTitle.Trim());
        }

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return ReadApplication(reader);
        }

        return null;
    }

    public static void ExportToCsv(string filePath, IEnumerable<JobApplication> applications)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "Company Name,Job Title,Date Applied,Status,Salary/Pay Rate,Job Posting URL," +
            "Contact Name,Contact Email,Interview Date,Follow-Up Date,Notes");

        foreach (var app in applications)
        {
            builder.AppendLine(string.Join(",",
                Csv(app.CompanyName),
                Csv(app.JobTitle),
                Csv(FormatDate(app.DateApplied)),
                Csv(app.Status),
                Csv(app.SalaryOrPayRate),
                Csv(app.JobPostingUrl),
                Csv(app.ContactName),
                Csv(app.ContactEmail),
                Csv(FormatDate(app.InterviewDate)),
                Csv(FormatDate(app.FollowUpDate)),
                Csv(app.Notes)));
        }

        File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
    }

    public static string CreateBackup(string? destinationFolder = null)
    {
        if (!File.Exists(DatabasePath))
        {
            throw new FileNotFoundException("Database file was not found.", DatabasePath);
        }

        var folder = string.IsNullOrWhiteSpace(destinationFolder)
            ? Path.Combine(DataFolder, "Backups")
            : destinationFolder;

        Directory.CreateDirectory(folder);

        var fileName = $"applications-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db";
        var destinationPath = Path.Combine(folder, fileName);
        File.Copy(DatabasePath, destinationPath, overwrite: true);
        return destinationPath;
    }

    private static void BindApplication(SqliteCommand command, JobApplication application)
    {
        command.Parameters.AddWithValue("$company", application.CompanyName.Trim());
        command.Parameters.AddWithValue("$title", application.JobTitle.Trim());
        command.Parameters.AddWithValue("$dateApplied", ToDbDateOrDbNull(application.DateApplied));
        command.Parameters.AddWithValue("$status", application.Status.Trim());
        command.Parameters.AddWithValue("$salary", NullIfEmpty(application.SalaryOrPayRate));
        command.Parameters.AddWithValue("$url", NullIfEmpty(application.JobPostingUrl));
        command.Parameters.AddWithValue("$contact", NullIfEmpty(application.ContactName));
        command.Parameters.AddWithValue("$email", NullIfEmpty(application.ContactEmail));
        command.Parameters.AddWithValue("$interview", ToDbDateOrDbNull(application.InterviewDate));
        command.Parameters.AddWithValue("$followUp", ToDbDateOrDbNull(application.FollowUpDate));
        command.Parameters.AddWithValue("$notes", NullIfEmpty(application.Notes));
    }

    private static JobApplication ReadApplication(SqliteDataReader reader)
    {
        return new JobApplication
        {
            Id = reader.GetInt32(0),
            CompanyName = reader.GetString(1),
            JobTitle = reader.GetString(2),
            DateApplied = ParseDate(reader.IsDBNull(3) ? null : reader.GetString(3)),
            Status = reader.GetString(4),
            SalaryOrPayRate = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            JobPostingUrl = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            ContactName = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            ContactEmail = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
            InterviewDate = ParseDate(reader.IsDBNull(9) ? null : reader.GetString(9)),
            FollowUpDate = ParseDate(reader.IsDBNull(10) ? null : reader.GetString(10)),
            Notes = reader.IsDBNull(11) ? string.Empty : reader.GetString(11)
        };
    }

    private static object ToDbDateOrDbNull(DateTime? value)
    {
        return value.HasValue ? ToDbDate(value.Value) : DBNull.Value;
    }

    private static string ToDbDate(DateTime value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var exact))
        {
            return exact.Date;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed.Date;
        }

        return null;
    }

    private static object NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    private static string FormatDate(DateTime? value)
    {
        return value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

/// <summary>
/// Summary counts shown on the main dashboard.
/// </summary>
public class DashboardStats
{
    public int Total { get; set; }
    public int UnderReview { get; set; }
    public int Interviews { get; set; }
    public int Offers { get; set; }
    public int Rejections { get; set; }
}
