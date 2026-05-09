// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Search.Pagefind;

/// <summary>Pagefind glue script — embedded as a UTF-8 byte literal.</summary>
internal static class PagefindBindScript
{
    /// <summary>Gets the UTF-8 bytes of the glue script.</summary>
    public static ReadOnlySpan<byte> Bytes => """
/* NuStreamDocs Pagefind glue — binds the bundled Pagefind WASM runtime to the theme's
   search shell. The theme contributes only the markup (data-md-component hooks); this
   script supplies the search behavior. Loaded by the search plugin's head-extras
   regardless of which theme is active, so M2 + M3 share the exact same wiring. */
(function () {
  "use strict";

  var PAGEFIND_LOADER = "/pagefind/pagefind.js";

  /* Maximum results displayed in the dropdown. Pagefind already trims by relevance;
     we cap to keep the panel scannable on small screens. */
  var MAX_RESULTS = 20;

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

  function bind() {
    var query = byComponent("search-query");
    var results = byComponent("search-list");
    var status = byComponent("search-status");
    if (!query || !results || !status) { return; }

    var sectionPriorities = parseSectionPriorities();
    var pagefindPromise;

    function loadPagefind() {
      if (!pagefindPromise) {
        pagefindPromise = import(PAGEFIND_LOADER).catch(function (err) {
          /* Reset on failure so a retry triggers a fresh import. */
          pagefindPromise = null;
          throw err;
        });
      }
      return pagefindPromise;
    }

    function clearResults() {
      results.textContent = "";
    }

    function setStatus(text) {
      status.textContent = text;
    }

    /* Maximum sub-section hits inlined under each page card before the
       "+ N more on this page" disclosure kicks in. mkdocs-material uses 2;
       matches the user's existing muscle memory. */
    var INLINE_SUB_RESULTS = 2;

    function pageTitle(data) {
      return (data.meta && data.meta.title) || data.url;
    }

    /* Builds one anchor (top page link OR a nested sub-result link). */
    function buildLink(entry, kind) {
      var link = document.createElement("a");
      link.className = kind === "primary" ? "md-search__link" : "md-search__link md-search__link--sub";
      link.href = entry.url || "#";

      var title = document.createElement("span");
      title.className = kind === "primary" ? "md-search__title" : "md-search__subtitle";
      title.textContent = (entry.title || entry.url || "").trim() || entry.url || "";
      link.appendChild(title);

      if (kind === "primary") {
        var path = document.createElement("span");
        path.className = "md-search__path";
        path.textContent = entry.url || "";
        link.appendChild(path);
      }

      if (entry.excerpt) {
        var excerpt = document.createElement("span");
        excerpt.className = "md-search__excerpt";
        /* Pagefind already wraps matched terms in <mark>; trust its output. */
        excerpt.innerHTML = entry.excerpt;
        link.appendChild(excerpt);
      }

      return link;
    }

    /* Renders one Pagefind page result as a card containing:
        1. Top link — the page title + URL + page-level excerpt.
        2. Up to INLINE_SUB_RESULTS section hits (heading + excerpt, deeper anchor).
        3. A "+ N more on this page" disclosure when more sub-results exist.
       Sub-results carry the per-section anchor URL, so clicking them jumps
       directly to the matched heading. */
    function renderHit(data) {
      var item = document.createElement("li");
      item.className = "md-search__item";

      var subs = Array.isArray(data.sub_results) ? data.sub_results : [];

      item.appendChild(buildLink({
        url: data.url,
        title: pageTitle(data),
        excerpt: data.excerpt,
      }, "primary"));

      var inline = subs.slice(0, INLINE_SUB_RESULTS);
      for (var i = 0; i < inline.length; i++) {
        item.appendChild(buildLink(inline[i], "sub"));
      }

      var leftover = subs.length - inline.length;
      if (leftover > 0) {
        var more = document.createElement("a");
        more.className = "md-search__more";
        more.href = data.url;
        more.textContent = leftover === 1
          ? "+ 1 more on this page"
          : "+ " + leftover + " more on this page";
        item.appendChild(more);
      }

      results.appendChild(item);
    }

    var runSearch = debounce(async function (value) {
      var trimmed = (value || "").trim();
      if (trimmed.length < 2) {
        clearResults();
        setStatus("Type at least 2 characters to search.");
        return;
      }

      try {
        var pagefind = await loadPagefind();
        var search = await pagefind.search(trimmed);
        var hits = search.results || [];
        if (sectionPriorities.length > 0) {
          var weighted = [];
          for (var i = 0; i < hits.length; i++) {
            weighted.push({ hit: hits[i], rank: i, boost: 0 });
          }
          /* First pass: resolve URLs (we need them for section matching). */
          var resolved = await Promise.all(weighted.map(function (w) {
            return w.hit.data();
          }));
          for (var j = 0; j < weighted.length; j++) {
            weighted[j].url = (resolved[j].url || "").toLowerCase();
            weighted[j].data = resolved[j];
            weighted[j].boost = applySectionWeight(weighted[j].url, sectionPriorities);
          }
          weighted.sort(function (a, b) {
            if (b.boost !== a.boost) { return b.boost - a.boost; }
            return a.rank - b.rank;
          });
          hits = weighted.slice(0, MAX_RESULTS);
          clearResults();
          if (hits.length === 0) {
            setStatus("No results found.");
            return;
          }
          setStatus(hits.length === 1 ? "1 matching page" : hits.length + " matching pages");
          for (var k = 0; k < hits.length; k++) {
            renderHit(hits[k].data);
          }
          return;
        }

        /* Fast path: no section weighting → resolve + render in order. */
        clearResults();
        if (hits.length === 0) {
          setStatus("No results found.");
          return;
        }
        var capped = hits.slice(0, MAX_RESULTS);
        var datas = await Promise.all(capped.map(function (h) { return h.data(); }));
        setStatus(datas.length === 1 ? "1 matching page" : datas.length + " matching pages");
        for (var m = 0; m < datas.length; m++) {
          renderHit(datas[m]);
        }
      } catch (err) {
        clearResults();
        setStatus("Search is unavailable right now.");
        if (window.console && console.error) { console.error(err); }
      }
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
