// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Transitions;

/// <summary>The client-side router script — embedded as a UTF-8 byte literal.</summary>
internal static class RouterScript
{
    /// <summary>Gets the UTF-8 bytes of the router script.</summary>
    public static ReadOnlySpan<byte> Bytes => """
/* NuStreamDocs client router — intercepts same-origin link navigation, swaps the
   page's content region in place (animated via the View Transitions API where
   available), preserves the chrome (sidebar / search / scroll), and pre-fetches
   links so navigation feels instant. Degrades to plain full-page navigation when
   JavaScript or the View Transitions API is unavailable. Configuration comes from
   <meta name="nstd:router" content="content=...;nav=...;prefetch=hover;delay=80;animation=fade;ignore=...">. */
(function () {
  "use strict";

  function readConfig() {
    var cfg = { content: "[data-md-component=\"content\"]", nav: "", prefetch: "hover", delay: 80, animation: "fade", ignore: "" };
    var meta = document.querySelector("meta[name=\"nstd:router\"]");
    if (!meta || !meta.content) { return cfg; }
    meta.content.split(";").forEach(function (pair) {
      var eq = pair.indexOf("=");
      if (eq < 1) { return; }
      var key = pair.slice(0, eq).trim();
      var value = pair.slice(eq + 1).trim();
      if (key === "delay") { var n = parseInt(value, 10); if (isFinite(n)) { cfg.delay = n; } }
      else if (key in cfg) { cfg[key] = value; }
    });
    return cfg;
  }

  var cfg = readConfig();
  var reducedMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  var animate = cfg.animation !== "none" && !reducedMotion && typeof document.startViewTransition === "function";
  var prefetchCache = new Map(); // url -> Promise<string | null>
  var PREFETCH_CACHE_MAX = 20;
  var inFlight = null; // AbortController of the navigation currently underway

  function dispatch(name, detail) {
    document.dispatchEvent(new CustomEvent(name, { detail: detail || {} }));
  }

  function sameOrigin(url) {
    try { return new URL(url, location.href).origin === location.origin; } catch (e) { return false; }
  }

  function isHtmlResponse(res) {
    var ct = res.headers.get("content-type") || "";
    return ct.indexOf("text/html") >= 0;
  }

  function candidateLink(el) {
    var a = el.closest ? el.closest("a[href]") : null;
    if (!a || a.hasAttribute("download") || a.target || !sameOrigin(a.href)) { return null; }
    if (cfg.ignore && a.matches(cfg.ignore)) { return null; }
    return a;
  }

  function isInPageAnchor(url) {
    var target = new URL(url, location.href);
    return target.pathname === location.pathname && target.search === location.search && target.hash;
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
    if (prefetchCache.has(url)) { return; }
    if (prefetchCache.size >= PREFETCH_CACHE_MAX) { prefetchCache.delete(prefetchCache.keys().next().value); }
    prefetchCache.set(url, fetchPage(url, undefined));
  }

  /* ---- swapping ---- */
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
    document.title = newDoc.title || document.title;
    ["meta[name=\"description\"]", "link[rel=\"canonical\"]"].forEach(function (sel) {
      var incoming = newDoc.head.querySelector(sel);
      if (!incoming) { return; }
      var current = document.head.querySelector(sel);
      if (current) { current.replaceWith(incoming.cloneNode(true)); } else { document.head.appendChild(incoming.cloneNode(true)); }
    });
    var existing = {};
    document.head.querySelectorAll("link[rel=\"stylesheet\"][href]").forEach(function (l) { existing[l.href] = true; });
    newDoc.head.querySelectorAll("link[rel=\"stylesheet\"][href]").forEach(function (l) {
      if (!existing[l.href]) { document.head.appendChild(l.cloneNode(true)); }
    });
  }

  function swapRegion(selector, newDoc) {
    if (!selector) { return true; }
    var current = document.querySelector(selector);
    var incoming = newDoc.querySelector(selector);
    if (!current || !incoming) { return false; }
    current.replaceChildren();
    var frag = document.createRange().createContextualFragment(incoming.innerHTML);
    current.appendChild(frag);
    reexecuteScripts(current);
    return true;
  }

  function applyScroll(url, isPop, savedScroll) {
    if (isPop && typeof savedScroll === "number") { window.scrollTo(0, savedScroll); return; }
    var hash = new URL(url, location.href).hash;
    if (hash && hash.length > 1) {
      var t = document.getElementById(decodeURIComponent(hash.slice(1)));
      if (t) { t.scrollIntoView(); return; }
    }
    window.scrollTo(0, 0);
  }

  function performSwap(html, url, isPop, savedScroll) {
    var newDoc = new DOMParser().parseFromString(html, "text/html");
    if (!newDoc.querySelector(cfg.content)) { return false; } // selector mismatch — caller falls back to a hard nav
    dispatch("nstd:before-swap", { to: url, newDocument: newDoc });
    function doSwap() {
      swapRegion(cfg.content, newDoc);
      if (cfg.nav) { swapRegion(cfg.nav, newDoc); }
      mergeHead(newDoc);
      if (!isPop) { history.pushState({ nstdScroll: 0 }, "", url); }
      applyScroll(url, isPop, savedScroll);
    }
    if (animate) { document.startViewTransition(doSwap); } else { doSwap(); }
    dispatch("nstd:page-load", { url: location.href });
    return true;
  }

  function hardNav(url) { location.assign(url); }

  function navigate(url, isPop, savedScroll) {
    if (inFlight) { inFlight.abort(); }
    history.replaceState(Object.assign({}, history.state, { nstdScroll: window.scrollY }), "");
    var controller = new AbortController();
    inFlight = controller;
    var cached = prefetchCache.get(url);
    var htmlPromise = cached ? cached : fetchPage(url, controller.signal);
    htmlPromise.then(function (html) {
      if (controller !== inFlight) { return; } // superseded by a newer navigation
      inFlight = null;
      if (!html || !performSwap(html, url, isPop, savedScroll)) { hardNav(url); }
    }).catch(function () { if (controller === inFlight) { inFlight = null; hardNav(url); } });
  }

  /* ---- wiring ---- */
  function onClick(event) {
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) { return; }
    var a = candidateLink(event.target);
    if (!a) { return; }
    var url = a.href;
    if (isInPageAnchor(url)) { return; } // let the browser handle in-page anchors
    if (new URL(url, location.href).href === location.href) { event.preventDefault(); return; }
    event.preventDefault();
    navigate(url, false, undefined);
  }

  function onPopState(event) {
    var saved = event.state && typeof event.state.nstdScroll === "number" ? event.state.nstdScroll : undefined;
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
      if (!a || isInPageAnchor(a.href) || prefetchCache.has(a.href)) { return; }
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
    if (!("AbortController" in window) || !window.DOMParser || !history.pushState) { return; } // ancient browser — stay MPA
    if (history.scrollRestoration) { history.scrollRestoration = "manual"; }
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
