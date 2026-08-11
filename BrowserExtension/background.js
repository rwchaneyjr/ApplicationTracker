const TRACKER_URL = "http://127.0.0.1:17871/api/applications";
const HEALTH_URL = "http://127.0.0.1:17871/api/health";

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type === "SAVE_APPLICATION") {
    saveApplication(message.payload)
      .then((result) => sendResponse(result))
      .catch((error) => sendResponse({ ok: false, error: String(error) }));
    return true;
  }

  if (message?.type === "HEALTH_CHECK") {
    fetch(HEALTH_URL)
      .then(async (response) => {
        const data = await response.json();
        sendResponse({ ok: response.ok, data });
      })
      .catch((error) => sendResponse({ ok: false, error: String(error) }));
    return true;
  }

  return false;
});

async function saveApplication(payload) {
  const response = await fetch(TRACKER_URL, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });

  let data = null;
  try {
    data = await response.json();
  } catch {
    data = null;
  }

  if (!response.ok) {
    return {
      ok: false,
      error: data?.error || `Tracker returned HTTP ${response.status}`
    };
  }

  return data;
}
