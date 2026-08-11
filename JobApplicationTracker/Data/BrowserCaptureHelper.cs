using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using JobApplicationTracker.Models;

namespace JobApplicationTracker.Data;

/// <summary>
/// Captures a job-site browser window: copies the page URL, reads the window
/// title, takes a screenshot, and builds field suggestions for Fill Blanks.
/// </summary>
public static class BrowserCaptureHelper
{
    private static readonly string CaptureFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JobApplicationTracker",
        "Captures");

    private static readonly string[] BrowserProcessNames =
    {
        "chrome", "msedge", "firefox", "brave", "opera"
    };

    private static readonly string[] JobSiteHints =
    {
        "linkedin", "indeed", "ziprecruiter", "glassdoor", "monster",
        "dice", "greenhouse", "lever", "workday", "jobs", "careers", "hiring"
    };

    public static BrowserCaptureResult CaptureFromBrowser()
    {
        var window = FindBestBrowserWindow();
        if (window is null || window.Value.Handle == IntPtr.Zero)
        {
            return BrowserCaptureResult.Fail(
                "No browser job page found. Open a job on LinkedIn/Indeed/ZipRecruiter, click that browser window, then press Get URL again.");
        }

        var handle = window.Value.Handle;
        var title = window.Value.Title;
        ForceForeground(handle);
        Thread.Sleep(300);

        var url = TryGetAddressBarUrl(handle)
                  ?? TryGetUrlFromClipboardIfBrowserLink()
                  ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(url))
        {
            try
            {
                Clipboard.SetText(url);
            }
            catch
            {
                // Clipboard may be busy.
            }
        }

        string screenshotPath = string.Empty;
        try
        {
            using var bitmap = CaptureWindow(handle);
            Directory.CreateDirectory(CaptureFolder);
            screenshotPath = Path.Combine(CaptureFolder, $"capture-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            bitmap.Save(screenshotPath, ImageFormat.Png);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Screenshot failed: " + ex.Message);
        }

        // Use title + URL (+ any visible page text hints in the title) to fill blanks.
        var combinedText = BuildParseText(title, url);
        var parsed = JobPostingParser.Parse(combinedText);
        if (!string.IsNullOrWhiteSpace(url))
        {
            parsed.JobPostingUrl = url.Trim();
        }

        ApplyTitleHints(title, parsed);

        if (string.IsNullOrWhiteSpace(parsed.JobTitle) && !string.IsNullOrWhiteSpace(title))
        {
            parsed.JobTitle = CleanWindowTitle(title);
        }

        var noteBits = new List<string>
        {
            $"Captured from browser on {DateTime.Now:yyyy-MM-dd HH:mm}."
        };
        if (!string.IsNullOrWhiteSpace(title))
        {
            noteBits.Add("Browser title: " + title.Trim());
        }

        if (!string.IsNullOrWhiteSpace(screenshotPath))
        {
            noteBits.Add("Screenshot saved: " + screenshotPath);
        }

        parsed.NotesExcerpt = string.IsNullOrWhiteSpace(parsed.NotesExcerpt)
            ? string.Join(Environment.NewLine, noteBits)
            : string.Join(Environment.NewLine, noteBits) + Environment.NewLine + parsed.NotesExcerpt;

        return new BrowserCaptureResult
        {
            Success = true,
            WindowTitle = title,
            Url = parsed.JobPostingUrl ?? url,
            ScreenshotPath = screenshotPath,
            Parsed = parsed,
            Message = string.IsNullOrWhiteSpace(url)
                ? "Captured title and screenshot. Could not read URL automatically — paste it if needed."
                : "Captured URL, title, and screenshot."
        };
    }

    private static void ApplyTitleHints(string title, ParsedJobPosting parsed)
    {
        var cleaned = CleanWindowTitle(title);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return;
        }

        var parts = Regex.Split(cleaned, @"\s+[\|\-–—]\s+|\sat\s", RegexOptions.IgnoreCase)
            .Select(p => p.Trim())
            .Where(p => p.Length > 1)
            .ToArray();

        if (parts.Length >= 1 && string.IsNullOrWhiteSpace(parsed.JobTitle))
        {
            parsed.JobTitle = parts[0];
        }

        if (parts.Length >= 2 && string.IsNullOrWhiteSpace(parsed.CompanyName))
        {
            var company = parts[1];
            if (!JobSiteHints.Any(h => company.Contains(h, StringComparison.OrdinalIgnoreCase)))
            {
                parsed.CompanyName = company;
            }
            else if (parts.Length >= 3)
            {
                parsed.CompanyName = parts[^2];
            }
        }
    }

    private static string CleanWindowTitle(string title)
    {
        var cleaned = Regex.Replace(
            title,
            @"\s*[\|\-–—]\s*(LinkedIn|Indeed|ZipRecruiter|Glassdoor|Monster|Dice|Google Chrome|Microsoft Edge|Brave|Firefox|Opera).*$",
            "",
            RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }

    private static string BuildParseText(string title, string url)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CleanWindowTitle(title));
        if (!string.IsNullOrWhiteSpace(url))
        {
            sb.AppendLine(url);
            sb.AppendLine("Job posting URL: " + url);
        }

        return sb.ToString();
    }

    private static string? TryGetUrlFromClipboardIfBrowserLink()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText().Trim();
                if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    return text;
                }
            }
        }
        catch
        {
            // Clipboard can be locked.
        }

        return null;
    }

    private static (IntPtr Handle, string Title)? FindBestBrowserWindow()
    {
        var candidates = new List<(IntPtr Handle, string Title, int Score)>();

        EnumWindows((hWnd, lParam) =>
        {
            if (!NativeIsWindowVisible(hWnd))
            {
                return true;
            }

            uint processId;
            NativeGetWindowThreadProcessId(hWnd, out processId);
            if (processId == 0)
            {
                return true;
            }

            try
            {
                using var process = Process.GetProcessById(unchecked((int)processId));
                var processName = process.ProcessName.ToLowerInvariant();
                if (!BrowserProcessNames.Contains(processName))
                {
                    return true;
                }

                var title = GetWindowTitle(hWnd);
                if (string.IsNullOrWhiteSpace(title) || title.Length < 3)
                {
                    return true;
                }

                var score = 1;
                var lower = title.ToLowerInvariant();
                if (JobSiteHints.Any(h => lower.Contains(h)))
                {
                    score += 5;
                }

                if (lower.Contains("job") || lower.Contains("hiring") || lower.Contains("career"))
                {
                    score += 2;
                }

                if (NativeGetForegroundWindow() == hWnd)
                {
                    score += 4;
                }

                candidates.Add((hWnd, title, score));
            }
            catch
            {
                // Process may have exited.
            }

            return true;
        }, IntPtr.Zero);

        if (candidates.Count == 0)
        {
            return null;
        }

        var best = candidates.OrderByDescending(c => c.Score).First();
        return (best.Handle, best.Title);
    }

    private static string? TryGetAddressBarUrl(IntPtr mainWindow)
    {
        try
        {
            var root = AutomationElement.FromHandle(mainWindow);
            if (root is null)
            {
                return null;
            }

            var editCondition = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.IsKeyboardFocusableProperty, true));

            var edits = root.FindAll(TreeScope.Descendants, editCondition);
            foreach (AutomationElement edit in edits)
            {
                string? value = null;
                try
                {
                    if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObj) &&
                        patternObj is ValuePattern valuePattern)
                    {
                        value = valuePattern.Current.Value;
                    }
                }
                catch
                {
                    // Some edits deny access.
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    value = edit.Current.Name;
                }

                value = value?.Trim();
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (!value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    if (value.Contains('.') && !value.Contains(' ') && value.Contains('/'))
                    {
                        value = "https://" + value;
                    }
                    else
                    {
                        continue;
                    }
                }

                if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                    uri.Host.Contains('.'))
                {
                    return uri.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Address bar read failed: " + ex.Message);
        }

        return null;
    }

    private static Bitmap CaptureWindow(IntPtr hWnd)
    {
        NativeGetWindowRect(hWnd, out var rect);
        var width = Math.Max(1, rect.Right - rect.Left);
        var height = Math.Max(1, rect.Bottom - rect.Top);
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        using var g = Graphics.FromImage(bmp);
        var hdc = g.GetHdc();
        var printed = false;
        try
        {
            printed = NativePrintWindow(hWnd, hdc, 2 /* PW_RENDERFULLCONTENT */);
        }
        finally
        {
            g.ReleaseHdc(hdc);
        }

        if (!printed)
        {
            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
        }

        return bmp;
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        var length = NativeGetWindowTextLength(hWnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(length + 1);
        _ = NativeGetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static void ForceForeground(IntPtr hWnd)
    {
        try
        {
            NativeShowWindow(hWnd, 9 /* SW_RESTORE */);
            NativeSetForegroundWindow(hWnd);
        }
        catch
        {
            // Best effort.
        }
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "EnumWindows")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "IsWindowVisible")]
    private static extern bool NativeIsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId", SetLastError = true)]
    private static extern uint NativeGetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", EntryPoint = "GetWindowText", CharSet = CharSet.Unicode)]
    private static extern int NativeGetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextLength", CharSet = CharSet.Unicode)]
    private static extern int NativeGetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    private static extern IntPtr NativeGetForegroundWindow();

    [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
    private static extern bool NativeSetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "ShowWindow")]
    private static extern bool NativeShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", EntryPoint = "GetWindowRect")]
    private static extern bool NativeGetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll", EntryPoint = "PrintWindow")]
    private static extern bool NativePrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

public sealed class BrowserCaptureResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string WindowTitle { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string ScreenshotPath { get; init; } = string.Empty;
    public ParsedJobPosting? Parsed { get; init; }

    public static BrowserCaptureResult Fail(string message) =>
        new() { Success = false, Message = message };
}
