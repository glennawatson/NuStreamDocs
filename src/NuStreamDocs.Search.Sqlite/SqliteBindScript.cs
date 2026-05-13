// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Search.Sqlite;

/// <summary>SQLite glue script — embedded as a UTF-8 byte literal.</summary>
internal static class SqliteBindScript
{
    /// <summary>Gets the UTF-8 bytes of the glue script.</summary>
    public static ReadOnlySpan<byte> Bytes => """
                                              /* NuStreamDocs SQLite glue — binds the vendored sql.js-httpvfs runtime to the
                                                 theme's search shell. The theme contributes only the markup (data-md-component
                                                 hooks); this script supplies the search behavior. Loaded by the search plugin's
                                                 head-extras regardless of which theme is active, so M2 + M3 share the same
                                                 wiring. The SQLite database (search.db) is queried in-browser over HTTP range
                                                 requests — only the index pages a query touches are fetched. */
                                              (function () {
                                                "use strict";

                                                /* Maximum results displayed in the dropdown. */
                                                var MAX_RESULTS = 20;

                                                /* SQLite page size the search.db was built with; must match requestChunkSize. */
                                                var REQUEST_CHUNK_SIZE = 4096;

                                                /* Served paths of the vendored runtime files (see SqliteSearchPlugin.StaticAssets). */
                                                var LOADER_URL = "/assets/javascripts/sql.js-httpvfs.js";
                                                var WORKER_URL = "/assets/javascripts/sqlite.worker.js";
                                                var WASM_URL = "/assets/javascripts/sql-wasm.wasm";

                                                var QUERY_SQL =
                                                  "SELECT url, title, " +
                                                  "snippet(pages, 2, '<mark class=\"md-search__highlight\">', '</mark>', '…', 12) AS excerpt, " +
                                                  "bm25(pages, 10.0, 1.0) AS rank " +
                                                  "FROM pages WHERE pages MATCH ? ORDER BY rank LIMIT ?";

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

                                                function loadLoaderScript() {
                                                  return new Promise(function (resolve, reject) {
                                                    if (typeof window.createDbWorker === "function") { resolve(); return; }
                                                    var existing = document.querySelector("script[data-sqlite-loader]");
                                                    if (existing) {
                                                      existing.addEventListener("load", function () { resolve(); });
                                                      existing.addEventListener("error", function () { reject(new Error("sql.js-httpvfs loader failed to load")); });
                                                      return;
                                                    }
                                                    var s = document.createElement("script");
                                                    s.src = LOADER_URL;
                                                    s.async = true;
                                                    s.setAttribute("data-sqlite-loader", "");
                                                    s.addEventListener("load", function () { resolve(); });
                                                    s.addEventListener("error", function () { reject(new Error("sql.js-httpvfs loader failed to load")); });
                                                    document.head.appendChild(s);
                                                  });
                                                }

                                                function buildMatchExpression(trimmed) {
                                                  var terms = trimmed.toLowerCase().split(/\s+/).filter(Boolean);
                                                  return terms
                                                    .map(function (t) { return t.replace(/["*]/g, "") + "*"; })
                                                    .filter(function (t) { return t.length > 1; })
                                                    .join(" ");
                                                }

                                                function bind() {
                                                  var query = byComponent("search-query");
                                                  var results = byComponent("search-list");
                                                  var status = byComponent("search-status");
                                                  if (!query || !results || !status) { return; }

                                                  var indexMeta = document.querySelector("meta[name=\"nustreamdocs:search-index\"]");
                                                  if (!indexMeta || !indexMeta.content) { return; }
                                                  var dbUrl = indexMeta.content;

                                                  var sectionPriorities = parseSectionPriorities();

                                                  var workerPromise = null;
                                                  function getWorker() {
                                                    if (!workerPromise) {
                                                      workerPromise = loadLoaderScript()
                                                        .then(function () {
                                                          return window.createDbWorker(
                                                            [{ from: "inline", config: { serverMode: "full", requestChunkSize: REQUEST_CHUNK_SIZE, url: dbUrl } }],
                                                            WORKER_URL,
                                                            WASM_URL
                                                          );
                                                        })
                                                        .catch(function (err) { workerPromise = null; throw err; });
                                                    }
                                                    return workerPromise;
                                                  }

                                                  function clearResults() { results.textContent = ""; }
                                                  function setStatus(text) { status.textContent = text; }

                                                  function renderHit(row) {
                                                    var item = document.createElement("li");
                                                    item.className = "md-search__item";

                                                    var link = document.createElement("a");
                                                    link.className = "md-search__link";
                                                    link.href = row.url;

                                                    var title = document.createElement("span");
                                                    title.className = "md-search__title";
                                                    title.textContent = row.title || row.url;
                                                    link.appendChild(title);

                                                    var path = document.createElement("span");
                                                    path.className = "md-search__path";
                                                    path.textContent = row.url;
                                                    link.appendChild(path);

                                                    if (row.excerpt) {
                                                      var excerpt = document.createElement("span");
                                                      excerpt.className = "md-search__excerpt";
                                                      /* snippet() returns trusted HTML built from our own column text, wrapping matched terms in <mark>. */
                                                      excerpt.innerHTML = row.excerpt;
                                                      link.appendChild(excerpt);
                                                    }

                                                    item.appendChild(link);
                                                    results.appendChild(item);
                                                  }

                                                  function runQuery(db, matchExpr, rawInput) {
                                                    return db.query(QUERY_SQL, matchExpr, MAX_RESULTS).catch(function () {
                                                      /* The user typed something that isn't a valid FTS5 expression — retry as a quoted phrase. */
                                                      var phrase = '"' + String(rawInput).replace(/"/g, '""') + '"';
                                                      return db.query(QUERY_SQL, phrase, MAX_RESULTS);
                                                    });
                                                  }

                                                  var runSearch = debounce(function (value) {
                                                    var trimmed = (value || "").trim();
                                                    if (trimmed.length < 2) {
                                                      clearResults();
                                                      setStatus("Type at least 2 characters to search.");
                                                      return;
                                                    }

                                                    var matchExpr = buildMatchExpression(trimmed);
                                                    if (!matchExpr) {
                                                      clearResults();
                                                      setStatus("Type at least 2 characters to search.");
                                                      return;
                                                    }

                                                    getWorker().then(function (worker) {
                                                      return runQuery(worker.db, matchExpr, trimmed);
                                                    }).then(function (rows) {
                                                      rows = rows || [];
                                                      if (sectionPriorities.length > 0) {
                                                        for (var i = 0; i < rows.length; i++) {
                                                          rows[i]._boost = applySectionWeight(String(rows[i].url || "").toLowerCase(), sectionPriorities);
                                                        }
                                                        rows.sort(function (a, b) {
                                                          if (b._boost !== a._boost) { return b._boost - a._boost; }
                                                          return a.rank - b.rank;
                                                        });
                                                      }

                                                      clearResults();
                                                      if (rows.length === 0) {
                                                        setStatus("No results found.");
                                                        return;
                                                      }
                                                      var capped = rows.slice(0, MAX_RESULTS);
                                                      setStatus(capped.length === 1 ? "1 result" : capped.length + " results");
                                                      for (var k = 0; k < capped.length; k++) { renderHit(capped[k]); }
                                                    }).catch(function (err) {
                                                      clearResults();
                                                      setStatus("Search is unavailable right now.");
                                                      if (window.console && console.error) { console.error(err); }
                                                    });
                                                  }, 100);

                                                  /* Warm the worker on focus so the WASM + first range requests overlap the user's typing. */
                                                  query.addEventListener("focus", function () {
                                                    getWorker().catch(function () { /* the failure is surfaced again on the first query */ });
                                                  }, { once: true });
                                                  query.addEventListener("input", function () { runSearch(query.value || ""); });
                                                }

                                                if (document.readyState === "loading") {
                                                  document.addEventListener("DOMContentLoaded", bind);
                                                } else {
                                                  bind();
                                                }
                                              })();
                                              """u8;
}
