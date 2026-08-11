# Job Application Tracker

A simple Windows desktop application for tracking every job you apply for — company, status, interviews, follow-ups, contacts, and notes — all stored locally with SQLite. No internet connection required.

**Repository:** [rwchaneyjr/ApplicationTracker](https://github.com/rwchaneyjr/ApplicationTracker)

## Features

- **LinkedIn companion extension:** when you apply on LinkedIn, save (or auto-capture) the company/title/URL into the desktop app over a local-only bridge — LinkedIn’s form stays on LinkedIn
- **Finish Application:** after you apply, press this button, paste the job posting, click **Fill Blanks** to auto-fill empty fields, then **Save to Tracker** — it stores the record and refreshes the dashboard, reminders, and table
- Track applications with company, title, date applied, status, salary, posting URL, recruiter contact, interview date, follow-up date, and notes
- Status options: Interested, Applied, Under Review, Assessment, Interview, Offer, Hired, Rejected, Withdrawn
- Dashboard totals for applications, under review, interviews, offers, and rejections
- Search by company or job title; filter by status and date applied
- **Reminders:** follow-up dates that are today or overdue appear in a prominent Reminders section and as a startup notice (for example, “Contact Acme Corp — Software Engineer · Today”)
- Add, Edit, Delete, Refresh, and Open Job Link
- Export the current list to CSV
- Create a backup copy of the SQLite database
- Validation prevents saving blank company names or job titles
- Database is created automatically on first launch
- **Fill Blanks** works offline — it reads pasted posting text and only fills empty fields (company, title, salary, URL, contact, email)

## Requirements

- Windows 10 or later
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (to build)
- .NET 8 Desktop Runtime (to run a published build without the SDK)

## Project layout

```
JobApplicationTracker/
  Program.cs                 # Entry point; initializes the database
  Models/
    JobApplication.cs        # Application data model + validation
    ApplicationStatus.cs     # Allowed status values
  Data/
    DatabaseHelper.cs        # SQLite create/read/update/delete, CSV export, backup
    JobPostingParser.cs      # Offline paste → fill blanks helper
  Forms/
    MainForm.cs              # Main window (dashboard, reminders, grid, filters)
    ApplicationEditForm.cs   # Finish/Add/Edit dialog with Fill Blanks
```

## Build and run (development)

From a Developer PowerShell / Command Prompt on Windows:

```bat
cd JobApplicationTracker
dotnet restore
dotnet build
dotnet run
```

Or open `JobApplicationTracker/JobApplicationTracker.csproj` in Visual Studio 2022 and press F5.

## Publish a standalone .exe

Create a self-contained Windows executable (no separate .NET install needed on the target PC):

```bat
cd JobApplicationTracker
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
```

The runnable file will be:

```text
JobApplicationTracker\publish\JobApplicationTracker.exe
```

### Optional: package as an installer / pinnable app

1. Publish the `.exe` (command above).
2. Double-click **`MakePinnable.bat`** in the repo root.
3. That installs the app under `%LocalAppData%\JobApplicationTracker\App` and creates **Start Menu** + **Desktop** shortcuts.
4. Right-click **Job Application Tracker** → **Pin to Start** / **Pin to taskbar**.

You can also use [Inno Setup](https://jrsoftware.org/isinfo.php) later to wrap it into a Setup.exe / MSI for sale.

## Where data is stored

| Item | Location |
|------|----------|
| SQLite database | `%LocalAppData%\JobApplicationTracker\applications.db` |
| Default backups | `%LocalAppData%\JobApplicationTracker\Backups\` (or a folder you choose) |

## Capture jobs from LinkedIn, Indeed, ZipRecruiter, and more

The desktop app cannot see browser tabs by itself. Use the companion extension:

1. Keep **Job Application Tracker** open (status bar shows “Browser link ready”).
2. In Chrome/Edge → Extensions → Developer mode → **Load unpacked** → choose the `BrowserExtension` folder.
3. Apply on **LinkedIn, Indeed, ZipRecruiter, Glassdoor**, etc.
4. Click **Save to Tracker** on the job page (or use the extension popup).
5. The job appears in the desktop app immediately.

Details: see `BrowserExtension/README.md`.

## How to use

1. Click **Finish Application** when you have just applied (status defaults to Applied, date = today).
2. Optional: paste the job posting text and press **Fill Blanks** to auto-fill empty company, title, salary, URL, and contact fields.
3. Review the fields, set a **Follow-up date** if you want a reminder, then press **Save to Tracker**.
4. The dashboard, reminders, and application table update immediately.
5. Use search and status/date filters to narrow the list.
6. Select a row and use **Edit**, **Delete**, or **Open Job Link**.
7. Use **Export CSV** or **Backup Database** before major changes or when moving PCs.

## Future idea (not in v1)

A later version could add cloud AI helpers for even smarter extraction from messy postings. The current **Fill Blanks** feature already works offline with local text parsing.

## License

All rights reserved unless otherwise noted by the repository owner.
