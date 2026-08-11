const statusEl = document.getElementById("status");

function setStatus(text, ok) {
  statusEl.textContent = text;
  statusEl.className = "status " + (ok ? "ok" : "bad");
}

function extractFromTab(markApplied) {
  function textOf(el) {
    return (el?.textContent || "").replace(/\s+/g, " ").trim();
  }

  function firstText(selectors) {
    for (const selector of selectors) {
      const el = document.querySelector(selector);
      const value = textOf(el);
      if (value) return value;
    }
    return "";
  }

  const host = location.hostname.toLowerCase();
  let source = host.replace(/^www\./, "");
  if (host.includes("linkedin")) source = "LinkedIn";
  else if (host.includes("indeed")) source = "Indeed";
  else if (host.includes("ziprecruiter")) source = "ZipRecruiter";
  else if (host.includes("glassdoor")) source = "Glassdoor";
  else if (host.includes("monster")) source = "Monster";
  else if (host.includes("dice.com")) source = "Dice";
  else if (host.includes("greenhouse")) source = "Greenhouse";
  else if (host.includes("lever.co")) source = "Lever";
  else if (host.includes("workday")) source = "Workday";

  const jobTitle = firstText([
    ".job-details-jobs-unified-top-card__job-title h1",
    "h1[data-testid='jobsearch-JobInfoHeader-title']",
    ".jobsearch-JobInfoHeader-title",
    "h1.job_title",
    "[data-test='job-title']",
    "h1",
    "[itemprop='title']"
  ]) || document.title.replace(/\s*[\|\-–—]\s*(LinkedIn|Indeed|ZipRecruiter|Glassdoor).*$/i, "").trim();

  let companyName = firstText([
    ".job-details-jobs-unified-top-card__company-name a",
    ".job-details-jobs-unified-top-card__company-name",
    "[data-company-name='true']",
    "[data-testid='inlineHeader-companyName']",
    "a.company_name",
    "[data-test='employer-name']",
    "[itemprop='hiringOrganization']",
    "[class*='company-name']",
    "[class*='companyName']"
  ]);

  if (!companyName && / at /i.test(jobTitle)) {
    companyName = jobTitle.split(/\sat\s/i).pop().trim();
  }

  return {
    companyName: companyName || "Unknown company",
    jobTitle: jobTitle || "Unknown job title",
    jobPostingUrl: location.href.split("?")[0],
    source,
    markApplied,
    status: markApplied ? "Applied" : "Interested",
    notes: `Captured from ${source} on ${new Date().toISOString()}`
  };
}

async function refreshHealth() {
  chrome.runtime.sendMessage({ type: "HEALTH_CHECK" }, (response) => {
    if (chrome.runtime.lastError || !response?.ok) {
      setStatus("Desktop app not running. Open Job Application Tracker first.", false);
      return;
    }
    setStatus("Desktop app connected. Works with LinkedIn, Indeed, ZipRecruiter, and more.", true);
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
