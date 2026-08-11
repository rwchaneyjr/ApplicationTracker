# Browser Extension — Job sites → Job Application Tracker

Send jobs from **LinkedIn, Indeed, ZipRecruiter, Glassdoor**, and other sites into the desktop app.

The job site’s apply form stays on that website. The extension reads company / title / URL from the page and sends them to:

`http://127.0.0.1:17871` (local only)

## Supported sites

| Site | Floating **Save to Tracker** button | Extension popup Save |
|------|-------------------------------------|----------------------|
| LinkedIn | Yes | Yes |
| Indeed | Yes | Yes |
| ZipRecruiter | Yes | Yes |
| Glassdoor | Yes | Yes |
| Monster / Dice | Yes | Yes |
| Greenhouse / Lever / Workday | Yes | Yes |
| Other job pages | Use popup on the open tab | Yes |

Auto-save after “application submitted” works best on LinkedIn Easy Apply; on other sites use **Save to Tracker** if auto-detect misses.

## Setup

1. Open **Job Application Tracker** (status bar: Browser link ready)
2. Chrome/Edge → Extensions → Developer mode → **Load unpacked** → select `BrowserExtension`
3. Apply on any supported site → click **Save to Tracker**
4. Confirm the job appears in the desktop app
