/* Light/dark palette toggle. Reads + writes localStorage so the
   choice survives navigation. Picks up `prefers-color-scheme` on
   first load. */
(function () {
  "use strict";

  var STORAGE_KEY = "md-color-scheme";
  var root = document.documentElement;

  function apply(scheme) {
    root.setAttribute("data-md-color-scheme", scheme);
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
    var btn = document.querySelector("[data-md-component=\"palette-toggle\"]");
    if (!btn) { return; }
    btn.addEventListener("click", function () {
      var current = root.getAttribute("data-md-color-scheme") === "slate" ? "default" : "slate";
      apply(current);
      try { localStorage.setItem(STORAGE_KEY, current); } catch (e) { /* ignore */ }
    });
  });
})();
