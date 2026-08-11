using System.Text.RegularExpressions;
using JobApplicationTracker.Models;

namespace JobApplicationTracker.Data;

/// <summary>
/// Local (offline) parser that reads pasted job-posting text and extracts
/// common fields so the add/edit form can fill blank boxes automatically.
/// </summary>
public static class JobPostingParser
{
    private static readonly Regex UrlRegex = new(
        @"https?://[^\s<>""']+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EmailRegex = new(
        @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SalaryRegex = new(
        @"(?:\$|USD\s*)?\d{2,3}(?:,\d{3})+(?:\s*[-–—to]+\s*(?:\$|USD\s*)?\d{2,3}(?:,\d{3})+)?(?:\s*(?:per\s+year|/yr|a year|annually))?" +
        @"|(?:\$|USD\s*)?\d{2,3}\s*[kK](?:\s*[-–—to]+\s*(?:\$|USD\s*)?\d{2,3}\s*[kK])?" +
        @"|(?:\$|USD\s*)?\d{2,3}(?:\.\d{2})?\s*(?:/\s*hr|per\s+hour|an\s+hour|hourly)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LabeledCompanyRegex = new(
        @"(?im)^(?:company|employer|organization|organisation)\s*[:\-]\s*(.+)$");

    private static readonly Regex LabeledTitleRegex = new(
        @"(?im)^(?:job\s*title|title|position|role)\s*[:\-]\s*(.+)$");

    private static readonly Regex AtCompanyRegex = new(
        @"(?i)\b(?:at|@)\s+([A-Z][A-Za-z0-9&.'\-\s]{1,60})",
        RegexOptions.Compiled);

    private static readonly Regex ContactNameRegex = new(
        @"(?im)^(?:contact|recruiter|hiring\s+manager|recruiter\s+name)\s*[:\-]\s*(.+)$");

    private static readonly string[] TitleKeywords =
    {
        "engineer", "developer", "manager", "analyst", "designer", "specialist",
        "coordinator", "director", "administrator", "technician", "consultant",
        "associate", "assistant", "intern", "lead", "architect", "scientist",
        "representative", "officer", "supervisor", "clerk", "nurse", "teacher"
    };

    /// <summary>
    /// Parse pasted posting text into field suggestions.
    /// </summary>
    public static ParsedJobPosting Parse(string? text)
    {
        var result = new ParsedJobPosting();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        var cleaned = text.Replace("\r\n", "\n").Trim();
        var lines = cleaned
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 0)
            .ToArray();

        result.JobPostingUrl = FirstMatch(UrlRegex, cleaned);
        result.ContactEmail = FirstMatch(EmailRegex, cleaned);
        result.SalaryOrPayRate = NormalizeWhitespace(FirstMatch(SalaryRegex, cleaned));

        result.CompanyName = FirstGroup(LabeledCompanyRegex, cleaned);
        result.JobTitle = FirstGroup(LabeledTitleRegex, cleaned);
        result.ContactName = FirstGroup(ContactNameRegex, cleaned);

        if (string.IsNullOrWhiteSpace(result.JobTitle) && lines.Length > 0)
        {
            result.JobTitle = PickLikelyTitle(lines);
        }

        if (string.IsNullOrWhiteSpace(result.CompanyName))
        {
            result.CompanyName = InferCompany(cleaned, lines, result.JobTitle);
        }

        // Keep a short excerpt in notes for reference.
        result.NotesExcerpt = BuildNotesExcerpt(cleaned);

        return result;
    }

    /// <summary>
    /// Fill only blank fields on the target application from parsed values.
    /// Returns how many blank fields were filled.
    /// </summary>
    public static int FillBlanks(JobApplication target, ParsedJobPosting parsed)
    {
        var filled = 0;

        filled += FillIfBlank(target.CompanyName, parsed.CompanyName, v => target.CompanyName = v);
        filled += FillIfBlank(target.JobTitle, parsed.JobTitle, v => target.JobTitle = v);
        filled += FillIfBlank(target.SalaryOrPayRate, parsed.SalaryOrPayRate, v => target.SalaryOrPayRate = v);
        filled += FillIfBlank(target.JobPostingUrl, parsed.JobPostingUrl, v => target.JobPostingUrl = v);
        filled += FillIfBlank(target.ContactName, parsed.ContactName, v => target.ContactName = v);
        filled += FillIfBlank(target.ContactEmail, parsed.ContactEmail, v => target.ContactEmail = v);

        if (string.IsNullOrWhiteSpace(target.Notes) && !string.IsNullOrWhiteSpace(parsed.NotesExcerpt))
        {
            target.Notes = parsed.NotesExcerpt;
            filled++;
        }

        return filled;
    }

    private static int FillIfBlank(string current, string? suggestion, Action<string> assign)
    {
        if (!string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(suggestion))
        {
            return 0;
        }

        assign(suggestion.Trim());
        return 1;
    }

    private static string? PickLikelyTitle(string[] lines)
    {
        foreach (var line in lines.Take(8))
        {
            if (LooksLikeTitle(line))
            {
                return TrimTitle(line);
            }
        }

        // Fall back to the first short non-URL line.
        foreach (var line in lines.Take(5))
        {
            if (!UrlRegex.IsMatch(line) && line.Length is >= 4 and <= 90)
            {
                return TrimTitle(line);
            }
        }

        return null;
    }

    private static bool LooksLikeTitle(string line)
    {
        if (line.Length > 100 || UrlRegex.IsMatch(line) || EmailRegex.IsMatch(line))
        {
            return false;
        }

        var lower = line.ToLowerInvariant();
        return TitleKeywords.Any(keyword => lower.Contains(keyword, StringComparison.Ordinal));
    }

    private static string? InferCompany(string fullText, string[] lines, string? jobTitle)
    {
        var atMatch = AtCompanyRegex.Match(fullText);
        if (atMatch.Success)
        {
            var company = CleanCompany(atMatch.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(company))
            {
                return company;
            }
        }

        // Pattern: "Title - Company" or "Title | Company"
        if (!string.IsNullOrWhiteSpace(jobTitle))
        {
            foreach (var line in lines.Take(6))
            {
                if (!line.Contains(jobTitle, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parts = Regex.Split(line, @"\s[-–—|•]\s");
                if (parts.Length >= 2)
                {
                    var maybeCompany = CleanCompany(parts[^1]);
                    if (!string.Equals(maybeCompany, jobTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        return maybeCompany;
                    }
                }
            }
        }

        // Second line sometimes holds the company on job boards.
        if (lines.Length >= 2)
        {
            var candidate = CleanCompany(lines[1]);
            if (candidate.Length is >= 2 and <= 60 &&
                !LooksLikeTitle(candidate) &&
                !UrlRegex.IsMatch(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string BuildNotesExcerpt(string text)
    {
        var excerpt = Regex.Replace(text, @"\s+", " ").Trim();
        if (excerpt.Length > 400)
        {
            excerpt = excerpt[..400].Trim() + "…";
        }

        return "Pasted from job posting:\n" + excerpt;
    }

    private static string? FirstMatch(Regex regex, string text)
    {
        var match = regex.Match(text);
        return match.Success ? match.Value.Trim().TrimEnd('.', ',', ';', ')') : null;
    }

    private static string? FirstGroup(Regex regex, string text)
    {
        var match = regex.Match(text);
        return match.Success ? CleanCompany(match.Groups[1].Value) : null;
    }

    private static string TrimTitle(string value)
    {
        return NormalizeWhitespace(value.Trim().Trim('-', '|', '•', ':'));
    }

    private static string CleanCompany(string value)
    {
        value = value.Trim().Trim('-', '|', '•', ':', ',', '.');
        value = Regex.Replace(value, @"\s+", " ");
        // Stop at common trailing location separators.
        var locationSplit = Regex.Split(value, @"\s+[·•]\s+|,\s+(?=[A-Z][a-z]+,\s*[A-Z]{2}\b)");
        return locationSplit[0].Trim();
    }

    private static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value.Trim(), @"\s+", " ");
    }
}

/// <summary>
/// Field suggestions extracted from pasted job-posting text.
/// </summary>
public class ParsedJobPosting
{
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string? SalaryOrPayRate { get; set; }
    public string? JobPostingUrl { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? NotesExcerpt { get; set; }
}
