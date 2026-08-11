using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using JobApplicationTracker.Models;

namespace JobApplicationTracker.Data;

/// <summary>
/// Local-only HTTP bridge so a browser extension can send job applications
/// from LinkedIn (or other sites) into this desktop app.
/// Listens on http://127.0.0.1:17871 only — nothing is exposed to the internet.
/// </summary>
public sealed class LocalBridgeServer : IDisposable
{
    public const int Port = 17871;
    public static string BaseUrl => $"http://127.0.0.1:{Port}/";

    private readonly HttpListener _listener = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _disposed;

    public event Action<JobApplication>? ApplicationReceived;
    public event Action<string>? StatusChanged;

    public bool IsRunning { get; private set; }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _listener.Prefixes.Clear();
        _listener.Prefixes.Add(BaseUrl);

        try
        {
            _listener.Start();
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Browser link offline: {ex.Message}");
            throw;
        }

        _cts = new CancellationTokenSource();
        IsRunning = true;
        StatusChanged?.Invoke($"Browser link ready on {BaseUrl}");
        _loopTask = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        try
        {
            _cts?.Cancel();
            _listener.Stop();
        }
        catch
        {
            // Ignore shutdown races.
        }

        IsRunning = false;
        StatusChanged?.Invoke("Browser link stopped.");
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext? context = null;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Browser link error: {ex.Message}");
                continue;
            }

            if (context is not null)
            {
                _ = Task.Run(() => HandleRequestAsync(context), token);
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            AddCors(response);

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;

            if (request.HttpMethod == "GET" && path is "/api/health" or "/api/status")
            {
                await WriteJsonAsync(response, 200, new
                {
                    ok = true,
                    app = "Job Application Tracker",
                    message = "Ready to receive applications from the browser extension."
                });
                return;
            }

            if (request.HttpMethod == "POST" && path == "/api/applications")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var body = await reader.ReadToEndAsync();
                var incoming = JsonSerializer.Deserialize<IncomingApplicationDto>(
                    body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (incoming is null)
                {
                    await WriteJsonAsync(response, 400, new { ok = false, error = "Invalid JSON body." });
                    return;
                }

                var application = incoming.ToJobApplication();
                var validationError = application.Validate();
                if (validationError is not null)
                {
                    await WriteJsonAsync(response, 400, new { ok = false, error = validationError });
                    return;
                }

                // Avoid duplicates for the same posting URL or same company+title today.
                if (DatabaseHelper.FindDuplicate(application) is { } existing)
                {
                    await WriteJsonAsync(response, 200, new
                    {
                        ok = true,
                        duplicate = true,
                        id = existing.Id,
                        message = "Already saved in Job Application Tracker."
                    });
                    return;
                }

                var id = DatabaseHelper.Insert(application);
                application.Id = id;
                ApplicationReceived?.Invoke(application);

                await WriteJsonAsync(response, 201, new
                {
                    ok = true,
                    duplicate = false,
                    id,
                    message = $"Saved: {application.CompanyName} — {application.JobTitle}"
                });
                return;
            }

            await WriteJsonAsync(response, 404, new { ok = false, error = "Not found." });
        }
        catch (Exception ex)
        {
            try
            {
                await WriteJsonAsync(response, 500, new { ok = false, error = ex.Message });
            }
            catch
            {
                // Response may already be closed.
            }
        }
    }

    private static void AddCors(HttpListenerResponse response)
    {
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, int statusCode, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _cts?.Dispose();
        try
        {
            _listener.Close();
        }
        catch
        {
            // Ignore.
        }
    }
}

/// <summary>
/// JSON payload sent by the browser extension.
/// </summary>
public class IncomingApplicationDto
{
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string? Status { get; set; }
    public string? SalaryOrPayRate { get; set; }
    public string? JobPostingUrl { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? Notes { get; set; }
    public string? Source { get; set; }
    public bool MarkApplied { get; set; } = true;

    public JobApplication ToJobApplication()
    {
        var status = string.IsNullOrWhiteSpace(Status)
            ? (MarkApplied ? ApplicationStatus.Applied : ApplicationStatus.Interested)
            : Status.Trim();

        var notes = string.IsNullOrWhiteSpace(Notes) ? string.Empty : Notes.Trim();
        if (!string.IsNullOrWhiteSpace(Source))
        {
            var sourceLine = $"Captured from {Source.Trim()} on {DateTime.Now:yyyy-MM-dd HH:mm}.";
            notes = string.IsNullOrWhiteSpace(notes) ? sourceLine : sourceLine + Environment.NewLine + notes;
        }

        return new JobApplication
        {
            CompanyName = CompanyName?.Trim() ?? string.Empty,
            JobTitle = JobTitle?.Trim() ?? string.Empty,
            DateApplied = MarkApplied ? DateTime.Today : null,
            Status = status,
            SalaryOrPayRate = SalaryOrPayRate?.Trim() ?? string.Empty,
            JobPostingUrl = JobPostingUrl?.Trim() ?? string.Empty,
            ContactName = ContactName?.Trim() ?? string.Empty,
            ContactEmail = ContactEmail?.Trim() ?? string.Empty,
            Notes = notes
        };
    }
}
