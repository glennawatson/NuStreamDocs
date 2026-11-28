// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Transitions;

/// <summary>The client-side router script, embedded as a UTF-8 byte literal.</summary>
internal static class RouterScript
{
    /// <summary>Gets the UTF-8 bytes of the router script.</summary>
    public static ReadOnlySpan<byte> Bytes => """
                                              /* NuStreamDocs client router - intercepts same-origin link navigation, swaps the
                                                 page's content, navigation, header, and footer regions in place (animated via
                                                 the View Transitions API where available), preserves search state and sidebar
                                                 scroll position, and pre-fetches links so navigation feels instant. Degrades
                                                 to plain full-page navigation when JavaScript, View Transitions, or per-page
                                                 CSP hashes require it. Configuration comes from:
                                                 <meta name="nstd:router" content="content=...;nav=...;prefetch=hover;delay=80;animation=fade;ignore=...">. */
                                              (function () {
                                                "use strict";

                                                function unescapeConfigValue(raw) {
                                                  return raw.replace(/%3B/gi, ";").replace(/%25/g, "%");
                                                }

                                                function readConfig() {
                                                  var cfg = { content: "[data-md-component=\"content\"]", nav: "", prefetch: "hover", delay: 80, animation: "fade", ignore: "" };
                                                  var meta = document.querySelector("meta[name=\"nstd:router\"]");
                                                  if (!meta || !meta.content) { return cfg; }
                                                  meta.content.split(";").forEach(function (pair) {
                                                    var eq = pair.indexOf("=");
                                                    if (eq < 1) { return; }
                                                    var key = pair.slice(0, eq).trim();
                                                    var value = unescapeConfigValue(pair.slice(eq + 1).trim());
                                                    if (key === "delay") { var n = parseInt(value, 10); if (isFinite(n)) { cfg.delay = n; } }
                                                    else if (key in cfg) { cfg[key] = value; }
                                                  });
                                                  return cfg;
                                                }

                                                var cfg = readConfig();
                                                var reducedMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
                                                var animate = cfg.animation !== "none" && !reducedMotion && typeof document.startViewTransition === "function";
                                                var prefetchCache = new Map(); // canonicalUrl (no hash) -> Promise<string | null>
                                                var PREFETCH_CACHE_MAX = 20;
                                                var inFlight = null; // AbortController of the fetch currently underway
                                                var activeTransition = null; // ViewTransition currently awaiting swap/completion
                                                var navSeq = 0;
                                                var currentDocKey = "";

                                                function dispatch(name, detail) {
                                                  document.dispatchEvent(new CustomEvent(name, { detail: detail || {} }));
                                                }

                                                function canonicalDocUrl(url) {
                                                  try {
                                                    var u = new URL(url, location.href);
                                                    u.hash = "";
                                                    return u.href;
                                                  } catch (e) {
                                                    return url;
                                                  }
                                                }

                                                function sameOrigin(url) {
                                                  try { return new URL(url, location.href).origin === location.origin; } catch (e) { return false; }
                                                }

                                                function isHtmlResponse(res) {
                                                  var ct = res.headers.get("content-type") || "";
                                                  return ct.indexOf("text/html") >= 0;
                                                }

                                                function candidateLink(el) {
                                                  var a = el && el.closest ? el.closest("a[href]") : null;
                                                  if (!a || a.hasAttribute("download") || a.target || !sameOrigin(a.href)) { return null; }
                                                  if (cfg.ignore && a.matches(cfg.ignore)) { return null; }
                                                  return a;
                                                }

                                                function isInPageAnchor(url) {
                                                  var target = new URL(url, location.href);
                                                  return target.pathname === location.pathname && target.search === location.search && target.hash.length > 0;
                                                }

                                                /* ---- fetching ---- */
                                                function fetchPage(url, signal) {
                                                  return fetch(url, { credentials: "same-origin", headers: { "X-NStd-Router": "1" }, signal: signal })
                                                    .then(function (res) {
                                                      if (!res.ok || !isHtmlResponse(res)) { return null; }
                                                      return res.text();
                                                    })
                                                    .catch(function () { return null; });
                                                }

                                                function prefetch(url) {
                                                  var key = canonicalDocUrl(url);
                                                  if (prefetchCache.has(key)) { return; }
                                                  if (prefetchCache.size >= PREFETCH_CACHE_MAX) { prefetchCache.delete(prefetchCache.keys().next().value); }
                                                  var p = fetchPage(key, undefined).then(function (html) {
                                                    if (!html) { prefetchCache.delete(key); }
                                                    return html;
                                                  });
                                                  prefetchCache.set(key, p);
                                                }

                                                /* ---- CSP compatibility ---- */
                                                function cspCompatible(newDoc) {
                                                  var curCsp = document.head.querySelector("meta[http-equiv=\"Content-Security-Policy\"]");
                                                  if (!curCsp || !curCsp.content) { return true; }
                                                  var nextCsp = newDoc.head.querySelector("meta[http-equiv=\"Content-Security-Policy\"]");
                                                  if (!nextCsp || !nextCsp.content) { return false; }
                                                  if (curCsp.content === nextCsp.content) { return true; }
                                                  var hashes = nextCsp.content.match(/'sha256-[A-Za-z0-9+/=]+'/g);
                                                  if (!hashes) { return curCsp.content === nextCsp.content; }
                                                  for (var i = 0; i < hashes.length; i++) {
                                                    if (curCsp.content.indexOf(hashes[i]) < 0) { return false; }
                                                  }
                                                  return true;
                                                }

                                                /* ---- DOM & head syncing ---- */
                                                function syncAttributes(target, source) {
                                                  if (!target || !source) { return; }
                                                  var names = [];
                                                  for (var i = 0; i < target.attributes.length; i++) { names.push(target.attributes[i].name); }
                                                  for (var j = 0; j < names.length; j++) {
                                                    if (!source.hasAttribute(names[j])) { target.removeAttribute(names[j]); }
                                                  }
                                                  for (var k = 0; k < source.attributes.length; k++) {
                                                    var attr = source.attributes[k];
                                                    if (target.getAttribute(attr.name) !== attr.value) { target.setAttribute(attr.name, attr.value); }
                                                  }
                                                }

                                                function reexecuteScripts(root) {
                                                  var scripts = root.querySelectorAll("script");
                                                  for (var i = 0; i < scripts.length; i++) {
                                                    var old = scripts[i];
                                                    var fresh = document.createElement("script");
                                                    for (var j = 0; j < old.attributes.length; j++) { fresh.setAttribute(old.attributes[j].name, old.attributes[j].value); }
                                                    fresh.textContent = old.textContent;
                                                    old.parentNode.replaceChild(fresh, old);
                                                  }
                                                }

                                                function mergeHead(newDoc) {
                                                  document.title = newDoc.title || "";
                                                  var managedSelector = [
                                                    "meta[name=\"description\"]",
                                                    "meta[name=\"author\"]",
                                                    "meta[property^=\"og:\"]",
                                                    "meta[name^=\"twitter:\"]",
                                                    "link[rel=\"canonical\"]",
                                                    "link[rel=\"prev\"]",
                                                    "link[rel=\"next\"]",
                                                    "link[rel=\"alternate\"][hreflang]",
                                                    "script[type=\"application/ld+json\"]"
                                                  ].join(",");
                                                  document.head.querySelectorAll(managedSelector).forEach(function (el) { el.remove(); });
                                                  newDoc.head.querySelectorAll(managedSelector).forEach(function (el) {
                                                    document.head.appendChild(el.cloneNode(true));
                                                  });

                                                  var existingStyles = {};
                                                  document.head.querySelectorAll("link[rel=\"stylesheet\"][href]").forEach(function (l) { existingStyles[l.href] = true; });
                                                  newDoc.head.querySelectorAll("link[rel=\"stylesheet\"][href]").forEach(function (l) {
                                                    if (!existingStyles[l.href]) { document.head.appendChild(l.cloneNode(true)); }
                                                  });

                                                  var existingScripts = {};
                                                  document.querySelectorAll("script[src]").forEach(function (s) { existingScripts[s.src] = true; });
                                                  newDoc.head.querySelectorAll("script[src]").forEach(function (s) {
                                                    if (existingScripts[s.src]) { return; }
                                                    var fresh = document.createElement("script");
                                                    for (var i = 0; i < s.attributes.length; i++) { fresh.setAttribute(s.attributes[i].name, s.attributes[i].value); }
                                                    document.head.appendChild(fresh);
                                                  });
                                                }

                                                function swapRegion(selector, newDoc) {
                                                  if (!selector) { return true; }
                                                  var current = document.querySelector(selector);
                                                  var incoming = newDoc.querySelector(selector);
                                                  if (!current && !incoming) { return true; }
                                                  if (!current || !incoming) { return false; }
                                                  syncAttributes(current, incoming);
                                                  current.innerHTML = incoming.innerHTML;
                                                  reexecuteScripts(current);
                                                  return true;
                                                }

                                                function syncChrome(newDoc) {
                                                  // 1. Sync root <html> lang and dir attributes (e.g. NuStreamDocs.I18n).
                                                  if (newDoc.documentElement) {
                                                    ["lang", "dir"].forEach(function (attr) {
                                                      if (newDoc.documentElement.hasAttribute(attr)) {
                                                        document.documentElement.setAttribute(attr, newDoc.documentElement.getAttribute(attr));
                                                      } else {
                                                        document.documentElement.removeAttribute(attr);
                                                      }
                                                    });
                                                  }

                                                  // 2. Sync header title and per-page "Edit this page" button without touching search or repo stats.
                                                  var curTitle = document.querySelector(".md-header .md-header__title");
                                                  var nextTitle = newDoc.querySelector(".md-header .md-header__title");
                                                  if (curTitle && nextTitle) {
                                                    syncAttributes(curTitle, nextTitle);
                                                    curTitle.innerHTML = nextTitle.innerHTML;
                                                  }

                                                  var curEdit = document.querySelector(".md-header a[title=\"Edit this page\"]");
                                                  var nextEdit = newDoc.querySelector(".md-header a[title=\"Edit this page\"]");
                                                  if (curEdit && nextEdit) {
                                                    syncAttributes(curEdit, nextEdit);
                                                    curEdit.innerHTML = nextEdit.innerHTML;
                                                  } else if (curEdit && !nextEdit) {
                                                    curEdit.remove();
                                                  } else if (!curEdit && nextEdit) {
                                                    var headerNav = document.querySelector(".md-header .md-header__inner");
                                                    if (headerNav) {
                                                      var anchorBefore = headerNav.querySelector(".md-header__source, [data-md-component=\"palette-toggle\"], a[title=\"Source repository\"]");
                                                      headerNav.insertBefore(nextEdit.cloneNode(true), anchorBefore || null);
                                                    }
                                                  }

                                                  // 3. Sync I18n language switcher (<nav class="nd-lang-switcher">).
                                                  var curLang = document.querySelector("nav.nd-lang-switcher");
                                                  var nextLang = newDoc.querySelector("nav.nd-lang-switcher");
                                                  if (curLang && nextLang) {
                                                    curLang.replaceWith(nextLang.cloneNode(true));
                                                  } else if (curLang && !nextLang) {
                                                    curLang.remove();
                                                  } else if (!curLang && nextLang) {
                                                    var hdr = document.querySelector("header.md-header");
                                                    if (hdr) { hdr.appendChild(nextLang.cloneNode(true)); }
                                                    else if (document.body) { document.body.insertBefore(nextLang.cloneNode(true), document.body.firstChild); }
                                                  }

                                                  // 4. Sync top-level navigation tabs (<nav class="md-tabs">).
                                                  var curTabs = document.querySelector("nav.md-tabs");
                                                  var nextTabs = newDoc.querySelector("nav.md-tabs");
                                                  if (curTabs && nextTabs) {
                                                    curTabs.replaceWith(nextTabs.cloneNode(true));
                                                  } else if (curTabs && !nextTabs) {
                                                    curTabs.remove();
                                                  } else if (!curTabs && nextTabs) {
                                                    var headerEl = document.querySelector("header.md-header");
                                                    if (headerEl && headerEl.parentNode) {
                                                      headerEl.parentNode.insertBefore(nextTabs.cloneNode(true), headerEl.nextSibling);
                                                    }
                                                  }

                                                  // 5. Sync primary left sidebar visibility + <nav class="md-nav--primary">, preserving sidebar scroll.
                                                  var curPrimary = document.querySelector(".md-sidebar--primary");
                                                  var nextPrimary = newDoc.querySelector(".md-sidebar--primary");
                                                  if (curPrimary && nextPrimary && cfg.nav !== ".md-sidebar--primary") {
                                                    var scrollBox = curPrimary.querySelector(".md-sidebar__inner");
                                                    var savedSidebarScroll = scrollBox ? scrollBox.scrollTop : 0;
                                                    syncAttributes(curPrimary, nextPrimary);
                                                    var curNavTree = curPrimary.querySelector(".md-nav--primary");
                                                    var nextNavTree = nextPrimary.querySelector(".md-nav--primary");
                                                    if (curNavTree && nextNavTree) {
                                                      syncAttributes(curNavTree, nextNavTree);
                                                      curNavTree.innerHTML = nextNavTree.innerHTML;
                                                    }
                                                    var filterInput = curPrimary.querySelector(".md-nav__filter-input");
                                                    if (filterInput && filterInput.value) { filterInput.value = ""; }
                                                    if (scrollBox) { scrollBox.scrollTop = savedSidebarScroll; }
                                                  }

                                                  // 6. Sync footer (Previous / Next page links and per-page footer metadata).
                                                  var curFooter = document.querySelector("footer.md-footer");
                                                  var nextFooter = newDoc.querySelector("footer.md-footer");
                                                  if (curFooter && nextFooter) {
                                                    syncAttributes(curFooter, nextFooter);
                                                    curFooter.innerHTML = nextFooter.innerHTML;
                                                    reexecuteScripts(curFooter);
                                                  }
                                                }

                                                function closeOverlays() {
                                                  ["__search", "__drawer"].forEach(function (id) {
                                                    var toggle = document.getElementById(id);
                                                    if (toggle && toggle.checked) {
                                                      toggle.checked = false;
                                                      toggle.dispatchEvent(new Event("change"));
                                                    }
                                                  });
                                                }

                                                function scrollToHash(url) {
                                                  var hash = new URL(url, location.href).hash;
                                                  if (!hash || hash.length <= 1) { return false; }
                                                  var id = decodeURIComponent(hash.slice(1));
                                                  var target = document.getElementById(id) || document.getElementsByName(id)[0];
                                                  if (!target) { return false; }
                                                  target.scrollIntoView();
                                                  return true;
                                                }

                                                function applyScroll(url, isPop, savedScroll) {
                                                  if (isPop && typeof savedScroll === "number") { window.scrollTo(0, savedScroll); return; }
                                                  if (scrollToHash(url)) { return; }
                                                  window.scrollTo(0, 0);
                                                }

                                                function performSwap(html, url, isPop, savedScroll, seq) {
                                                  var newDoc = new DOMParser().parseFromString(html, "text/html");
                                                  if (!newDoc.querySelector(cfg.content) || !cspCompatible(newDoc)) { return false; }
                                                  dispatch("nstd:before-swap", { to: url, newDocument: newDoc });
                                                  function doSwap() {
                                                    if (seq !== navSeq) { return; }
                                                    closeOverlays();
                                                    swapRegion(cfg.content, newDoc);
                                                    if (cfg.nav) { swapRegion(cfg.nav, newDoc); }
                                                    syncChrome(newDoc);
                                                    mergeHead(newDoc);
                                                    if (!isPop) { history.pushState({ nstdScroll: 0 }, "", url); }
                                                    currentDocKey = canonicalDocUrl(location.href);
                                                    applyScroll(url, isPop, savedScroll);
                                                    dispatch("nstd:page-load", { url: location.href });
                                                  }
                                                  if (animate) {
                                                    var vt = document.startViewTransition(doSwap);
                                                    activeTransition = vt;
                                                    if (vt && vt.finished && typeof vt.finished.finally === "function") {
                                                      vt.finished.finally(function () { if (activeTransition === vt) { activeTransition = null; } });
                                                    }
                                                  } else {
                                                    doSwap();
                                                  }
                                                  return true;
                                                }

                                                function hardNav(url) { location.assign(url); }

                                                function cancelPending() {
                                                  if (inFlight) { inFlight.abort(); inFlight = null; }
                                                  if (activeTransition && typeof activeTransition.skipTransition === "function") {
                                                    try { activeTransition.skipTransition(); } catch (e) { /* ignore */ }
                                                    activeTransition = null;
                                                  }
                                                }

                                                function navigate(url, isPop, savedScroll) {
                                                  var seq = ++navSeq;
                                                  cancelPending();
                                                  if (!isPop) {
                                                    history.replaceState(Object.assign({}, history.state, { nstdScroll: window.scrollY }), "");
                                                  }
                                                  var controller = new AbortController();
                                                  inFlight = controller;
                                                  var key = canonicalDocUrl(url);
                                                  var cached = prefetchCache.get(key);
                                                  var htmlPromise = cached ? cached : fetchPage(key, controller.signal);
                                                  htmlPromise.then(function (html) {
                                                    if (seq !== navSeq) { return; }
                                                    inFlight = null;
                                                    if (!html || !performSwap(html, url, isPop, savedScroll, seq)) { hardNav(url); }
                                                  }).catch(function () { if (seq === navSeq) { inFlight = null; hardNav(url); } });
                                                }

                                                /* ---- wiring ---- */
                                                function onClick(event) {
                                                  if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) { return; }
                                                  var a = candidateLink(event.target);
                                                  if (!a) { return; }
                                                  var url = a.href;
                                                  if (isInPageAnchor(url)) {
                                                    closeOverlays();
                                                    if (new URL(url, location.href).href === location.href) {
                                                      event.preventDefault();
                                                      scrollToHash(url);
                                                    }
                                                    return;
                                                  }
                                                  if (new URL(url, location.href).href === location.href) {
                                                    event.preventDefault();
                                                    closeOverlays();
                                                    window.scrollTo(0, 0);
                                                    return;
                                                  }
                                                  event.preventDefault();
                                                  navigate(url, false, undefined);
                                                }

                                                function onPopState(event) {
                                                  var saved = event.state && typeof event.state.nstdScroll === "number" ? event.state.nstdScroll : undefined;
                                                  if (canonicalDocUrl(location.href) === currentDocKey) {
                                                    cancelPending();
                                                    closeOverlays();
                                                    applyScroll(location.href, true, saved);
                                                    return;
                                                  }
                                                  navigate(location.href, true, saved);
                                                }

                                                /* ---- prefetch wiring ---- */
                                                function setupPrefetch() {
                                                  if (cfg.prefetch === "off") { return; }
                                                  if (cfg.prefetch === "viewport") {
                                                    if (!("IntersectionObserver" in window)) { return; }
                                                    var io = new IntersectionObserver(function (entries) {
                                                      entries.forEach(function (entry) {
                                                        if (!entry.isIntersecting) { return; }
                                                        var a = candidateLink(entry.target);
                                                        io.unobserve(entry.target);
                                                        if (a && !isInPageAnchor(a.href)) { prefetch(a.href); }
                                                      });
                                                    });
                                                    var observeAll = function () { document.querySelectorAll("a[href]").forEach(function (a) { io.observe(a); }); };
                                                    observeAll();
                                                    document.addEventListener("nstd:page-load", observeAll);
                                                    return;
                                                  }
                                                  // hover (default): debounced mouseover + touchstart
                                                  var timer = 0;
                                                  document.addEventListener("mouseover", function (event) {
                                                    var a = candidateLink(event.target);
                                                    if (!a || isInPageAnchor(a.href) || prefetchCache.has(canonicalDocUrl(a.href))) { return; }
                                                    clearTimeout(timer);
                                                    var href = a.href;
                                                    timer = window.setTimeout(function () { prefetch(href); }, cfg.delay);
                                                  });
                                                  document.addEventListener("mouseout", function () { clearTimeout(timer); });
                                                  document.addEventListener("touchstart", function (event) {
                                                    var a = candidateLink(event.target);
                                                    if (a && !isInPageAnchor(a.href)) { prefetch(a.href); }
                                                  }, { passive: true });
                                                }

                                                function start() {
                                                  if (!("AbortController" in window) || !window.DOMParser || !history.pushState) { return; }
                                                  if (history.scrollRestoration) { history.scrollRestoration = "manual"; }
                                                  currentDocKey = canonicalDocUrl(location.href);
                                                  history.replaceState(Object.assign({}, history.state, { nstdScroll: window.scrollY }), "");
                                                  document.addEventListener("click", onClick);
                                                  window.addEventListener("popstate", onPopState);
                                                  setupPrefetch();
                                                  dispatch("nstd:page-load", { url: location.href });
                                                }

                                                if (document.readyState === "loading") { document.addEventListener("DOMContentLoaded", start); } else { start(); }
                                              })();
                                              """u8;
}
