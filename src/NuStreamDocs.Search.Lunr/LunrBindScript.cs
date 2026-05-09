// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Search.Lunr;

/// <summary>Lunr glue script — embedded as a UTF-8 byte literal.</summary>
internal static class LunrBindScript
{
    /// <summary>Gets the UTF-8 bytes of the glue script.</summary>
    public static ReadOnlySpan<byte> Bytes => """
/* NuStreamDocs Lunr glue — binds the vendored Lunr.js runtime to the theme's
   search shell. The theme contributes only the markup (data-md-component
   hooks); this script supplies the search behavior. Loaded by the search
   plugin's head-extras regardless of which theme is active, so M2 + M3 share
   the exact same wiring. */
(function () {
  "use strict";

  /* Maximum results displayed in the dropdown. Lunr returns scored hits in
     descending order; we cap to keep the panel scannable on small screens. */
  var MAX_RESULTS = 20;

  /* Minimum window of context around the first matched term in the rendered
     snippet, in characters. Mirrors the Pagefind side. */
  var EXCERPT_WINDOW = 160;

  function byComponent(name) {
    return document.querySelector("[data-md-component=\"" + name + "\"]");
  }

  function debounce(fn, delay) {
    var timer = 0;
    return function () {
      var args = arguments;
      clearTimeout(timer);
      timer = window.setTimeout(function () { fn.apply(null, args); }, delay);
    };
  }

  function escapeHtml(value) {
    return value
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function escapeRegex(value) {
    return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  }

  function parseSectionPriorities() {
    var meta = document.querySelector("meta[name=\"nustreamdocs:search-section-priorities\"]");
    if (!meta || !meta.content) { return []; }
    var out = [];
    var pairs = meta.content.split(",");
    for (var i = 0; i < pairs.length; i++) {
      var raw = pairs[i].trim();
      if (!raw) { continue; }
      var colon = raw.lastIndexOf(":");
      if (colon < 1) { continue; }
      var weight = parseInt(raw.substring(colon + 1), 10);
      if (!isFinite(weight)) { continue; }
      out.push({ prefix: raw.substring(0, colon).toLowerCase(), weight: weight });
    }
    return out;
  }

  function applySectionWeight(url, priorities) {
    for (var i = 0; i < priorities.length; i++) {
      if (url.indexOf(priorities[i].prefix) >= 0) {
        return priorities[i].weight;
      }
    }
    return 0;
  }

  function buildExcerptHtml(text, terms) {
    var clean = (text || "").replace(/\s+/g, " ").trim();
    if (!clean) { return ""; }

    var lower = clean.toLowerCase();
    var firstHit = -1;
    for (var i = 0; i < terms.length; i++) {
      var idx = lower.indexOf(terms[i]);
      if (idx >= 0 && (firstHit < 0 || idx < firstHit)) { firstHit = idx; }
    }

    var sliceStart = 0;
    var sliceEnd = clean.length;
    if (firstHit >= 0 && clean.length > EXCERPT_WINDOW) {
      sliceStart = Math.max(0, firstHit - Math.floor(EXCERPT_WINDOW / 3));
      if (sliceStart > 0) {
        var space = clean.lastIndexOf(" ", sliceStart);
        sliceStart = space >= 0 ? space + 1 : sliceStart;
      }
      sliceEnd = Math.min(clean.length, sliceStart + EXCERPT_WINDOW);
    } else if (clean.length > EXCERPT_WINDOW) {
      sliceEnd = EXCERPT_WINDOW;
    }

    var slice = clean.substring(sliceStart, sliceEnd);
    var prefix = sliceStart > 0 ? "… " : "";
    var suffix = sliceEnd < clean.length ? " …" : "";

    var escaped = escapeHtml(slice);
    var sortedTerms = terms.slice().sort(function (a, b) { return b.length - a.length; });
    var pattern = sortedTerms.map(escapeRegex).filter(Boolean).join("|");
    if (pattern) {
      var re = new RegExp("(" + pattern + ")", "gi");
      escaped = escaped.replace(re, "<mark class=\"md-search__highlight\">$1</mark>");
    }
    return escapeHtml(prefix) + escaped + escapeHtml(suffix);
  }

  function bind() {
    var query = byComponent("search-query");
    var results = byComponent("search-list");
    var status = byComponent("search-status");
    if (!query || !results || !status) { return; }
    if (typeof window.lunr !== "function") {
      status.textContent = "Lunr runtime did not load.";
      return;
    }

    var indexMeta = document.querySelector("meta[name=\"nustreamdocs:search-index\"]");
    if (!indexMeta || !indexMeta.content) { return; }

    var sectionPriorities = parseSectionPriorities();

    var indexPromise;
    function loadIndex() {
      if (!indexPromise) {
        indexPromise = fetch(indexMeta.content, { credentials: "same-origin" })
          .then(function (res) {
            if (!res.ok) { throw new Error("Search index request failed: " + res.status); }
            return res.json();
          })
          .then(function (raw) {
            /* mkdocs-material-shaped doc list: each entry has {location, title, text}. */
            var docs = Array.isArray(raw.docs) ? raw.docs : [];
            var byRef = Object.create(null);
            var lang = (raw.config && raw.config.lang) || "en";
            var index = window.lunr(function () {
              this.ref("location");
              this.field("title", { boost: 10 });
              this.field("text");
              for (var i = 0; i < docs.length; i++) {
                var doc = docs[i];
                this.add(doc);
                byRef[doc.location] = doc;
              }
            });
            return { index: index, byRef: byRef, lang: lang };
          })
          .catch(function (err) {
            indexPromise = null;
            throw err;
          });
      }
      return indexPromise;
    }

    function clearResults() { results.textContent = ""; }
    function setStatus(text) { status.textContent = text; }

    function renderHit(doc, terms) {
      var item = document.createElement("li");
      item.className = "md-search__item";

      var link = document.createElement("a");
      link.className = "md-search__link";
      link.href = doc.location;

      var title = document.createElement("span");
      title.className = "md-search__title";
      title.textContent = doc.title || doc.location;
      link.appendChild(title);

      var path = document.createElement("span");
      path.className = "md-search__path";
      path.textContent = doc.location;
      link.appendChild(path);

      var excerptHtml = buildExcerptHtml(doc.text, terms);
      if (excerptHtml) {
        var excerpt = document.createElement("span");
        excerpt.className = "md-search__excerpt";
        excerpt.innerHTML = excerptHtml;
        link.appendChild(excerpt);
      }

      item.appendChild(link);
      results.appendChild(item);
    }

    var runSearch = debounce(function (value) {
      var trimmed = (value || "").trim();
      if (trimmed.length < 2) {
        clearResults();
        setStatus("Type at least 2 characters to search.");
        return;
      }

      loadIndex().then(function (state) {
        /* Lunr's default query: AND-of-terms, prefix-match per token. */
        var terms = trimmed.toLowerCase().split(/\s+/).filter(Boolean);
        var lunrQuery = terms.map(function (t) { return t + "*"; }).join(" ");
        var hits;
        try {
          hits = state.index.search(lunrQuery);
        } catch (e) {
          /* User typed a query that's not a valid Lunr expression — fall back to literal. */
          hits = state.index.search(trimmed);
        }

        if (sectionPriorities.length > 0) {
          for (var i = 0; i < hits.length; i++) {
            hits[i]._boost = applySectionWeight((hits[i].ref || "").toLowerCase(), sectionPriorities);
          }
          hits.sort(function (a, b) {
            if (b._boost !== a._boost) { return b._boost - a._boost; }
            return b.score - a.score;
          });
        }

        clearResults();
        if (hits.length === 0) {
          setStatus("No results found.");
          return;
        }

        var capped = hits.slice(0, MAX_RESULTS);
        setStatus(capped.length === 1 ? "1 result" : capped.length + " results");
        for (var k = 0; k < capped.length; k++) {
          var doc = state.byRef[capped[k].ref];
          if (doc) { renderHit(doc, terms); }
        }
      }).catch(function (err) {
        clearResults();
        setStatus("Search is unavailable right now.");
        if (window.console && console.error) { console.error(err); }
      });
    }, 100);

    query.addEventListener("input", function () {
      runSearch(query.value || "");
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", bind);
  } else {
    bind();
  }
})();
"""u8;
}
