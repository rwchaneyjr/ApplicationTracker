# Browser Extension — LinkedIn → Job Application Tracker

This companion extension lets the **desktop app know when you apply on LinkedIn** (or manually save a job from a LinkedIn job page).

LinkedIn’s application form stays on LinkedIn. The extension reads the job title/company/URL from the page and sends them to the desktop app over a **local-only** link:

`http://127.0.0.1:17871`

Nothing is sent to the cloud.

## Setup (one time)

### 1. Keep the desktop app running
Open **Job Application Tracker**.  
At the bottom you should see: **Browser link ready…**

### 2. Install the extension in Chrome or Edge

1. Open `chrome://extensions` (Chrome) or `edge://extensions` (Edge)
2. Turn on **Developer mode**
3. Click **Load unpacked**
4. Select this folder:

`BrowserExtension`

### 3. Apply on LinkedIn
1. Open a LinkedIn job posting
2. Either:
   - Click the floating **Save to Tracker** button, or
   - Finish **Easy Apply** (the extension tries to auto-save after submit), or
   - Click the extension icon → **Save current job as Applied**
3. Look in the desktop app — the job should appear in the table and dashboard

## Notes
- The desktop app must be open for LinkedIn saves to work
- Duplicate job links are not saved twice
- LinkedIn page layouts change; if auto-detect misses, use **Save to Tracker**
