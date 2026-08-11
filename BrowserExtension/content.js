(() => {
  if (window.__jobTrackerCompanionLoaded) {
    return;
  }
  window.__jobTrackerCompanionLoaded = true;

  const BUTTON_ID = "job-tracker-save-button";
  const TOAST_ID = "job-tracker-toast";

  function textOf(el) {
    return (el?.textContent || "").replace(/\s+/g, " ").trim();
  }

  function firstText(selectors) {
    for (const selector of selectors) {
      const el = document.querySelector(selector);
      const value = textOf(el);
      if (value) {
        return value;
      }
    }
    return "";
  }

  function extractJob() {
    const jobTitle = firstText([
      ".job-details-jobs-unified-top-card__job-title h1",
      ".jobs-unified-top-card__job-title h1",
      "h1.t-24",
      "h1"
    ]);

    let companyName = firstText([
      ".job-details-jobs-unified-top-card__company-name a",
      ".job-details-jobs-unified-top-card__company-name",
      ".jobs-unified-top-card__company-name a",
      ".jobs-unified-top-card__company-name",
      "a.job-card-container__company-name"
    ]);

    // Fallback: "Title at Company" style headings.
    if (!companyName && jobTitle.toLowerCase().includes(" at ")) {
      const parts = jobTitle.split(/\sat\s/i);
      if (parts.length >= 2) {
        companyName = parts[parts.length - 1].trim();
      }
    }

    const salary = firstText([
      ".salary-main-rail__data-amount",
      ".job-details-jobs-unified-top-card__job-insight--highlight",
      "span.job-details-jobs-unified-top-card__job-insight"
    ]);

    const description = firstText([
      "#job-details",
      ".jobs-description__content",
      ".jobs-box__html-content"
    ]).slice(0, 500);

    return {
      companyName: companyName || "Unknown company",
      jobTitle: jobTitle || document.title.replace(/\s*\|\s*LinkedIn.*$/i, "").trim(),
      jobPostingUrl: location.href.split("?")[0],
      salaryOrPayRate: /\$|salary|\/yr|\/hr|hour/i.test(salary) ? salary : "",
      notes: description ? `LinkedIn posting excerpt:\n${description}` : "",
      source: "LinkedIn",
      markApplied: true,
      status: "Applied"
    };
  }

  function showToast(message, isError = false) {
    let toast = document.getElementById(TOAST_ID);
    if (!toast) {
      toast = document.createElement("div");
      toast.id = TOAST_ID;
      document.documentElement.appendChild(toast);
    }
    toast.textContent = message;
    toast.className = isError ? "job-tracker-toast error" : "job-tracker-toast";
    toast.style.display = "block";
    clearTimeout(showToast._timer);
    showToast._timer = setTimeout(() => {
      toast.style.display = "none";
    }, 4000);
  }

  async function saveToTracker(markApplied) {
    const payload = extractJob();
    payload.markApplied = markApplied;
    payload.status = markApplied ? "Applied" : "Interested";

    if (!payload.jobTitle || payload.jobTitle.length < 2) {
      showToast("Could not read the job title on this page.", true);
      return;
    }

    return new Promise((resolve) => {
      chrome.runtime.sendMessage({ type: "SAVE_APPLICATION", payload }, (response) => {
        if (chrome.runtime.lastError) {
          showToast("Extension error: " + chrome.runtime.lastError.message, true);
          resolve(false);
          return;
        }

        if (!response?.ok) {
          showToast(
            response?.error ||
              "Desktop app not reachable. Open Job Application Tracker and keep it running.",
            true
          );
          resolve(false);
          return;
        }

        if (response.duplicate) {
          showToast("Already in Job Application Tracker.");
        } else {
          showToast(`Saved: ${payload.companyName} — ${payload.jobTitle}`);
        }
        resolve(true);
      });
    });
  }

  function ensureButton() {
    if (document.getElementById(BUTTON_ID)) {
      return;
    }

    const wrap = document.createElement("div");
    wrap.id = BUTTON_ID;
    wrap.innerHTML = `
      <button type="button" class="job-tracker-main-btn" data-action="applied">Save to Tracker</button>
      <button type="button" class="job-tracker-secondary-btn" data-action="interested">Interested</button>
    `;
    document.documentElement.appendChild(wrap);

    wrap.addEventListener("click", async (event) => {
      const target = event.target;
      if (!(target instanceof HTMLElement)) {
        return;
      }
      const action = target.getAttribute("data-action");
      if (!action) {
        return;
      }
      target.setAttribute("disabled", "true");
      await saveToTracker(action === "applied");
      target.removeAttribute("disabled");
    });
  }

  function looksLikeApplicationSubmitted(node) {
    const text = textOf(node).toLowerCase();
    if (!text) {
      return false;
    }
    return (
      text.includes("application submitted") ||
      text.includes("your application was sent") ||
      (text.includes("applied") && text.includes("view application"))
    );
  }

  let lastAutoSaveKey = "";
  async function maybeAutoSaveFromDom(node) {
    if (!looksLikeApplicationSubmitted(node)) {
      return;
    }

    const job = extractJob();
    const key = `${job.companyName}|${job.jobTitle}|${job.jobPostingUrl}`;
    if (key === lastAutoSaveKey) {
      return;
    }
    lastAutoSaveKey = key;
    await saveToTracker(true);
  }

  const observer = new MutationObserver((mutations) => {
    ensureButton();
    for (const mutation of mutations) {
      for (const node of mutation.addedNodes) {
        if (node.nodeType === Node.ELEMENT_NODE) {
          maybeAutoSaveFromDom(node);
        }
      }
    }
  });

  ensureButton();
  observer.observe(document.documentElement, { childList: true, subtree: true });

  // Also watch Easy Apply submit buttons being clicked.
  document.addEventListener(
    "click",
    (event) => {
      const target = event.target;
      if (!(target instanceof Element)) {
        return;
      }
      const button = target.closest("button, [role='button']");
      if (!button) {
        return;
      }
      const label = textOf(button).toLowerCase();
      if (
        label === "submit application" ||
        label.includes("submit application") ||
        (label.includes("submit") && label.includes("apply"))
      ) {
        setTimeout(() => saveToTracker(true), 1500);
      }
    },
    true
  );
})();
