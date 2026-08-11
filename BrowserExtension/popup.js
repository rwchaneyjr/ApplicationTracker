const statusEl = document.getElementById("status");

function setStatus(text, ok) {
  statusEl.textContent = text;
  statusEl.className = "status " + (ok ? "ok" : "bad");
}

function extractFromTab(markApplied) {
  const jobTitle =
    document.querySelector(".job-details-jobs-unified-top-card__job-title h1, .jobs-unified-top-card__job-title h1, h1")?.textContent?.replace(/\s+/g, " ").trim() ||
    document.title.replace(/\s*\|\s*LinkedIn.*$/i, "").trim();

  const companyName =
    document.querySelector(".job-details-jobs-unified-top-card__company-name a, .job-details-jobs-unified-top-card__company-name, .jobs-unified-top-card__company-name a, .jobs-unified-top-card__company-name")?.textContent?.replace(/\s+/g, " ").trim() ||
    "Unknown company";

  return {
    companyName,
    jobTitle,
    jobPostingUrl: location.href.split("?")[0],
    source: location.hostname.includes("linkedin") ? "LinkedIn" : location.hostname,
    markApplied,
    status: markApplied ? "Applied" : "Interested",
    notes: `Captured from browser tab on ${new Date().toISOString()}`
  };
}

async function refreshHealth() {
  chrome.runtime.sendMessage({ type: "HEALTH_CHECK" }, (response) => {
    if (chrome.runtime.lastError || !response?.ok) {
      setStatus("Desktop app not running. Open Job Application Tracker first.", false);
      return;
    }
    setStatus("Desktop app connected. Ready to save jobs.", true);
  });
}

async function saveFromActiveTab(markApplied) {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (!tab?.id) {
    setStatus("No active tab found.", false);
    return;
  }

  try {
    const [{ result }] = await chrome.scripting.executeScript({
      target: { tabId: tab.id },
      func: extractFromTab,
      args: [markApplied]
    });

    chrome.runtime.sendMessage({ type: "SAVE_APPLICATION", payload: result }, (response) => {
      if (!response?.ok) {
        setStatus(response?.error || "Save failed. Is the desktop app open?", false);
        return;
      }
      if (response.duplicate) {
        setStatus("Already saved in the tracker.", true);
      } else {
        setStatus(response.message || "Saved to Job Application Tracker.", true);
      }
    });
  } catch (error) {
    setStatus(String(error), false);
  }
}

document.getElementById("saveApplied").addEventListener("click", () => saveFromActiveTab(true));
document.getElementById("saveInterested").addEventListener("click", () => saveFromActiveTab(false));
refreshHealth();
