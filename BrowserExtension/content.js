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

  function hostSource() {
    const host = location.hostname.replace(/^www\./, "").toLowerCase();
    if (host.includes("linkedin")) return "LinkedIn";
    if (host.includes("indeed")) return "Indeed";
    if (host.includes("ziprecruiter")) return "ZipRecruiter";
    if (host.includes("glassdoor")) return "Glassdoor";
    if (host.includes("monster")) return "Monster";
    if (host.includes("simplyhired")) return "SimplyHired";
    if (host.includes("careerbuilder")) return "CareerBuilder";
    if (host.includes("dice.com")) return "Dice";
    if (host.includes("greenhouse.io")) return "Greenhouse";
    if (host.includes("lever.co")) return "Lever";
    if (host.includes("myworkdayjobs") || host.includes("workday")) return "Workday";
    return host;
  }

  function cleanTitle(title) {
    return (title || "")
      .replace(/\s*[\|\-–—]\s*(LinkedIn|Indeed|ZipRecruiter|Glassdoor|Monster).*$/i, "")
      .replace(/\s+/g, " ")
      .trim();
  }

  function extractLinkedIn() {
    return {
      jobTitle: firstText([
        ".job-details-jobs-unified-top-card__job-title h1",
        ".jobs-unified-top-card__job-title h1",
        "h1.t-24",
        "h1"
      ]),
      companyName: firstText([
        ".job-details-jobs-unified-top-card__company-name a",
        ".job-details-jobs-unified-top-card__company-name",
        ".jobs-unified-top-card__company-name a",
        ".jobs-unified-top-card__company-name"
      ]),
      salary: firstText([
        ".salary-main-rail__data-amount",
        ".job-details-jobs-unified-top-card__job-insight--highlight"
      ]),
      description: firstText(["#job-details", ".jobs-description__content"])
    };
  }

  function extractIndeed() {
    return {
      jobTitle: firstText([
        "h1[data-testid='jobsearch-JobInfoHeader-title']",
        ".jobsearch-JobInfoHeader-title",
        "h2.jobTitle",
        "h1.jobsearch-JobInfoHeader-title span",
        "h1"
      ]),
      companyName: firstText([
        "[data-company-name='true']",
        "[data-testid='inlineHeader-companyName'] a",
        "[data-testid='inlineHeader-companyName']",
        ".jobsearch-InlineCompanyRating a",
        ".jobsearch-CompanyInfoContainer a"
      ]),
      salary: firstText([
        "#salaryInfoAndJobType",
        "[data-testid='attribute_snippet_testid']",
        ".jobsearch-JobMetadataHeader-item"
      ]),
      description: firstText(["#jobDescriptionText", ".jobsearch-jobDescriptionText"])
    };
  }

  function extractZipRecruiter() {
    return {
      jobTitle: firstText([
        "h1.job_title",
        "h1[class*='job_title']",
        "h1.hiring_job_name",
        "h1"
      ]),
      companyName: firstText([
        "a.company_name",
        "a[class*='company_name']",
        ".hiring_company_text a",
        ".company_name",
        "[class*='companyName']"
      ]),
      salary: firstText([
        ".job_salary",
        "[class*='salary']",
        ".hiring_salary"
      ]),
      description: firstText([
        ".job_description",
        "#job_description",
        "[class*='jobDescription']"
      ])
    };
  }

  function extractGlassdoor() {
    return {
      jobTitle: firstText([
        "[data-test='job-title']",
        "h1[class*='JobDetails_jobTitle']",
        "h1"
      ]),
      companyName: firstText([
        "[data-test='employer-name']",
        "h4[class*='heading'] a",
        "[class*='EmployerProfile'] a"
      ]),
      salary: firstText([
        "[data-test='detailSalary']",
        "[class*='SalaryEstimate']"
      ]),
      description: firstText([
        "[class*='JobDetails_jobDescription']",
        "#JobDescriptionContainer"
      ])
    };
  }

  function extractGeneric() {
    const jobTitle = firstText([
      "h1",
      "h1 span",
      "[itemprop='title']",
      "[data-testid*='job-title']",
      "[class*='job-title']",
      "[class*='jobTitle']"
    ]);

    let companyName = firstText([
      "[itemprop='hiringOrganization']",
      "[itemprop='name']",
      "[data-testid*='company']",
      "[class*='company-name']",
      "[class*='companyName']",
      "a[href*='/company']"
    ]);

    if (!companyName && / at /i.test(jobTitle)) {
      companyName = jobTitle.split(/\sat\s/i).pop().trim();
    }

    // Open Graph / meta fallbacks
    if (!companyName) {
      companyName =
        document.querySelector("meta[property='og:site_name']")?.content?.trim() || "";
    }

    const salary = firstText([
      "[class*='salary']",
      "[data-testid*='salary']",
      "[itemprop='baseSalary']"
    ]);

    const description = firstText([
      "[itemprop='description']",
      "[class*='job-description']",
      "[class*='jobDescription']",
      "article",
      "main"
    ]);

    return { jobTitle, companyName, salary, description };
  }

  function extractJob() {
    const host = location.hostname.toLowerCase();
    let extracted;

    if (host.includes("linkedin")) {
      extracted = extractLinkedIn();
    } else if (host.includes("indeed")) {
      extracted = extractIndeed();
    } else if (host.includes("ziprecruiter")) {
      extracted = extractZipRecruiter();
    } else if (host.includes("glassdoor")) {
      extracted = extractGlassdoor();
    } else {
      extracted = extractGeneric();
    }

    // Fill gaps with generic selectors.
    const fallback = extractGeneric();
    const jobTitle = cleanTitle(extracted.jobTitle || fallback.jobTitle || document.title);
    let companyName = (extracted.companyName || fallback.companyName || "").trim();

    if (!companyName && / at /i.test(jobTitle)) {
      companyName = jobTitle.split(/\sat\s/i).pop().trim();
    }

    const salary = extracted.salary || fallback.salary || "";
    const description = (extracted.description || fallback.description || "").slice(0, 500);
    const source = hostSource();

    return {
      companyName: companyName || "Unknown company",
      jobTitle: jobTitle || "Unknown job title",
      jobPostingUrl: location.href.split("?")[0],
      salaryOrPayRate: /\$|salary|pay|\/yr|\/hr|hour|year/i.test(salary) ? salary : "",
      notes: description ? `${source} posting excerpt:\n${description}` : "",
      source,
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

    if (!payload.jobTitle || payload.jobTitle === "Unknown job title") {
      showToast("Could not read the job title on this page. Try the extension popup.", true);
      return false;
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
    if (!text || text.length > 400) {
      return false;
    }
    return (
      text.includes("application submitted") ||
      text.includes("your application was sent") ||
      text.includes("application has been submitted") ||
      text.includes("successfully applied") ||
      text.includes("thanks for applying") ||
      text.includes("thank you for applying") ||
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

  document.addEventListener(
    "click",
    (event) => {
      const target = event.target;
      if (!(target instanceof Element)) {
        return;
      }
      const button = target.closest("button, [role='button'], input[type='submit']");
      if (!button) {
        return;
      }
      const label = textOf(button).toLowerCase() || (button.getAttribute("value") || "").toLowerCase();
      if (
        label === "submit application" ||
        label.includes("submit application") ||
        label.includes("submit your application") ||
        (label.includes("submit") && label.includes("apply")) ||
        label === "apply now" && location.hostname.includes("ziprecruiter")
      ) {
        setTimeout(() => saveToTracker(true), 1600);
      }
    },
    true
  );
})();
