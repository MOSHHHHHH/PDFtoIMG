/**
 * Fetches the latest GitHub Release for this repo and fills in:
 *  - the version label
 *  - the direct download link for the .exe asset
 *  - the file size and publish date
 * Falls back gracefully (keeps the static "all releases" link) if the
 * GitHub API call fails for any reason (rate limiting, offline, etc.).
 */
(function () {
  const OWNER = "MOSHHHHHH";
  const REPO = "PDFtoIMG";
  const API_URL = `https://api.github.com/repos/${OWNER}/${REPO}/releases/latest`;

  const versionEl = document.getElementById("download-version");
  const buttonEl = document.getElementById("download-button");
  const sizeEl = document.getElementById("download-size");
  const dateEl = document.getElementById("download-date");

  function formatBytes(bytes) {
    if (!bytes) return "";
    const mb = bytes / (1024 * 1024);
    return `${mb.toFixed(1)} MB`;
  }

  function formatDate(iso) {
    try {
      const d = new Date(iso);
      return `עודכן לאחרונה: ${d.toLocaleDateString("he-IL")}`;
    } catch {
      return "";
    }
  }

  fetch(API_URL, { headers: { Accept: "application/vnd.github+json" } })
    .then((res) => {
      if (!res.ok) throw new Error(`GitHub API responded ${res.status}`);
      return res.json();
    })
    .then((data) => {
      const version = data.tag_name || data.name;
      if (version && versionEl) {
        versionEl.textContent = `גרסה נוכחית: ${version}`;
      }

      const exeAsset = (data.assets || []).find((a) => a.name.toLowerCase().endsWith(".exe"));
      if (exeAsset && buttonEl) {
        // Point straight at the .exe asset and force a download instead of a
        // page navigation - until this resolves the button still safely falls
        // back to the general Releases page set in the HTML.
        buttonEl.href = exeAsset.browser_download_url;
        buttonEl.setAttribute("download", exeAsset.name);
        buttonEl.removeAttribute("target");
        if (sizeEl) sizeEl.textContent = `לא נדרשת התקנה · ${formatBytes(exeAsset.size)}`;
      }

      if (dateEl && data.published_at) {
        dateEl.textContent = formatDate(data.published_at);
      }
    })
    .catch(() => {
      if (versionEl) versionEl.textContent = "לצפייה בגרסה העדכנית, ראו את עמוד ה-Releases";
      // buttonEl keeps its static fallback href pointing at /releases/latest
    });
})();
