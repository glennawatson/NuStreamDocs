/* Light/dark palette toggle. Reads + writes localStorage so the
   choice survives navigation. Picks up `prefers-color-scheme` on
   first load. */
(function () {
  "use strict";

  var STORAGE_KEY = "md-color-scheme";
  var root = document.documentElement;

  function applyTo(element, scheme) {
    if (!element) {
      return;
    }

    element.setAttribute("data-md-color-scheme", scheme);
    element.setAttribute("data-md-color-primary", "custom");
    element.setAttribute("data-md-color-accent", "custom");
  }

  function apply(scheme) {
    applyTo(root, scheme);
    applyTo(document.body, scheme);
  }

  function byComponent(name) {
    return document.querySelector("[data-md-component=\"" + name + "\"]");
  }

  function formatCompactCount(value) {
    if (typeof value !== "number" || !isFinite(value)) {
      return "";
    }

    if (value >= 1000000) {
      return (value / 1000000).toFixed(value >= 10000000 ? 0 : 1).replace(/\.0$/, "") + "m";
    }

    if (value >= 1000) {
      return (value / 1000).toFixed(value >= 10000 ? 0 : 1).replace(/\.0$/, "") + "k";
    }

    return String(value);
  }

  function cleanSearchText(value) {
    return (value || "").replace(/\u00b6/g, "").trim();
  }

  function debounce(fn, delay) {
    var timer = 0;
    return function () {
      var args = arguments;
      clearTimeout(timer);
      timer = window.setTimeout(function () { fn.apply(null, args); }, delay);
    };
  }

  function getFieldValue(element) {
    return element && typeof element.value === "string" ? element.value : "";
  }

  function initializeSearch() {
    var formatMeta = document.querySelector("meta[name=\"nustreamdocs:search-format\"]");
    var indexMeta = document.querySelector("meta[name=\"nustreamdocs:search-index\"]");
    var searchToggle = document.getElementById("__search");
    var query = byComponent("search-query");
    var results = byComponent("search-list");
    var status = byComponent("search-status");
    var closeButton = byComponent("search-close");
    var form = query ? query.closest("form") : null;
    if (!formatMeta || !indexMeta || !searchToggle || !query || !results || !status) {
      return;
    }

    var manifestPromise;
    function loadManifest() {
      if (!manifestPromise) {
        manifestPromise = fetch(indexMeta.content, { credentials: "same-origin" })
          .then(function (response) {
            if (!response.ok) {
              throw new Error("Search index request failed.");
            }

            return response.json();
          });
      }

      return manifestPromise;
    }

    function setStatus(text) {
      status.textContent = text;
    }

    function clearResults() {
      results.textContent = "";
    }

    function renderMatches(matches) {
      clearResults();
      if (!matches.length) {
        setStatus("No results found.");
        return;
      }

      setStatus(matches.length === 1 ? "1 result" : matches.length + " results");
      for (var i = 0; i < matches.length; i++) {
        var match = matches[i];
        var item = document.createElement("li");
        item.className = "md-search__item";

        var link = document.createElement("a");
        link.className = "md-search__link";
        link.href = match.url;

        var title = document.createElement("span");
        title.className = "md-search__title";
        title.textContent = cleanSearchText(match.title) || match.url;
        link.appendChild(title);

        var path = document.createElement("span");
        path.className = "md-search__path";
        path.textContent = match.url;
        link.appendChild(path);

        item.appendChild(link);
        results.appendChild(item);
      }
    }

    var runSearch = debounce(function (value) {
      var normalized = value.trim().toLowerCase();
      if (normalized.length < 2) {
        clearResults();
        setStatus("Type at least 2 characters to search.");
        return;
      }

      if (formatMeta.content !== "pagefind") {
        clearResults();
        setStatus("This theme currently supports Pagefind search indexes.");
        return;
      }

      loadManifest()
        .then(function (manifest) {
          var terms = normalized.split(/\s+/).filter(Boolean);
          var records = Array.isArray(manifest.records) ? manifest.records : [];
          var matches = [];
          for (var i = 0; i < records.length && matches.length < 20; i++) {
            var record = records[i];
            var target = (cleanSearchText(record.title) + " " + record.url).toLowerCase();
            var matched = true;
            for (var j = 0; j < terms.length; j++) {
              if (target.indexOf(terms[j]) < 0) {
                matched = false;
                break;
              }
            }

            if (matched) {
              matches.push(record);
            }
          }

          renderMatches(matches);
        })
        .catch(function () {
          clearResults();
          setStatus("Search is unavailable right now.");
        });
    }, 100);

    query.addEventListener("input", function () {
      runSearch(getFieldValue(query));
    });

    if (form) {
      form.addEventListener("submit", function (event) {
        var first = results.querySelector("a");
        if (!first) {
          event.preventDefault();
          return;
        }

        event.preventDefault();
        window.location.href = first.href;
      });
    }

    query.addEventListener("keydown", function (event) {
      if (event.key !== "Enter") {
        return;
      }

      var first = results.querySelector("a");
      if (!first) {
        return;
      }

      event.preventDefault();
      window.location.href = first.href;
    });

    if (closeButton) {
      closeButton.addEventListener("click", function () {
        searchToggle.checked = false;
      });
    }

    document.addEventListener("keydown", function (event) {
      if (event.key === "Escape" && searchToggle.checked) {
        searchToggle.checked = false;
      }
    });

    searchToggle.addEventListener("change", function () {
      if (searchToggle.checked) {
        window.setTimeout(function () { query.focus(); }, 0);
      }
    });
  }

  function initializeRepoStats() {
    var source = document.querySelector(".md-source[data-md-component=\"source\"]");
    if (!source) {
      return;
    }

    var facts = byComponent("source-facts");
    var stars = byComponent("source-stars");
    var forks = byComponent("source-forks");
    if (!facts || !stars || !forks) {
      return;
    }

    var match = source.href.match(/^https:\/\/github\.com\/([^/]+)\/([^/#?]+)/i);
    if (!match) {
      return;
    }

    fetch("https://api.github.com/repos/" + match[1] + "/" + match[2], {
      headers: { "Accept": "application/vnd.github+json" }
    }).then(function (response) {
      if (!response.ok) {
        throw new Error("GitHub API request failed.");
      }

      return response.json();
    }).then(function (repo) {
      stars.textContent = formatCompactCount(repo.stargazers_count) + " stars";
      forks.textContent = formatCompactCount(repo.forks_count) + " forks";
      stars.hidden = false;
      forks.hidden = false;
      facts.hidden = false;
    }).catch(function () {
      facts.hidden = true;
    });
  }

  function preferred() {
    try {
      var saved = localStorage.getItem(STORAGE_KEY);
      if (saved === "default" || saved === "slate") {
        return saved;
      }
    } catch (e) { /* storage may be blocked */ }
    if (window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches) {
      return "slate";
    }
    return "default";
  }

  apply(preferred());

  document.addEventListener("DOMContentLoaded", function () {
    apply(preferred());
    initializeSearch();
    initializeRepoStats();

    var btn = document.querySelector("[data-md-component=\"palette-toggle\"]");
    if (!btn) { return; }
    btn.addEventListener("click", function () {
      var current = root.getAttribute("data-md-color-scheme") === "slate" ? "default" : "slate";
      apply(current);
      try { localStorage.setItem(STORAGE_KEY, current); } catch (e) { /* ignore */ }
    });
  });
})();
