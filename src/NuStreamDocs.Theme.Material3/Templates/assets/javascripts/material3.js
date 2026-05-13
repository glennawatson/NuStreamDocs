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

    /* Search-overlay shell \u2014 pure UX, engine-agnostic. The actual search behavior
       (fetching the index, scoring, rendering result rows) is contributed by the
       selected search-engine plugin (Pagefind / Lunr) via its own glue script,
       which finds the [data-md-component="search-query|search-list|search-status"]
       hooks the overlay markup exposes. This function only handles:
         - opening / closing via the __search checkbox
         - focusing the query field when the overlay opens
         - dismissing on Escape and the close button
         - "Enter / form submit navigates to the first result" \u2014 works regardless
           of which engine populated the list (we just look for the first <a>).
       With no search engine plugin registered, the shell still hides cleanly. */
    function initializeSearchShell() {
        var searchToggle = document.getElementById("__search");
        var query = byComponent("search-query");
        var results = byComponent("search-list");
        if (!searchToggle || !query) {
            return;
        }

        var closeButton = byComponent("search-close");
        var form = query.closest("form");

        if (form && results) {
            form.addEventListener("submit", function (event) {
                var first = results.querySelector("a");
                event.preventDefault();
                if (first) {
                    window.location.href = first.href;
                }
            });
        }

        if (results) {
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
        }

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
                // Plain <input>: a single rAF tick is enough to outlast the
                // :checked-driven display flip; no shadow-DOM hydration race.
                window.requestAnimationFrame(function () {
                    query.focus();
                });
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
            headers: {"Accept": "application/vnd.github+json"}
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
        } catch (e) { /* storage may be blocked */
        }
        if (window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches) {
            return "slate";
        }
        return "default";
    }

    apply(preferred());

    document.addEventListener("DOMContentLoaded", function () {
        apply(preferred());
        initializeSearchShell();
        initializeRepoStats();

        /* Hamburger is a <label for="__drawer">. We hijack the click and toggle
           the checkbox in JS — iOS Safari sometimes drops the label-for association
           when the input is visually-hidden via clip-path, so taking the toggle
           path explicitly is the most portable option. preventDefault stops the
           native label action so we don't double-toggle.
           The keydown handler covers Space/Enter (label doesn't activate on those
           natively); change-listener mirrors checked state into aria-expanded. */
        var drawerBtn = document.querySelector("[data-md-component=\"hamburger\"]");
        var drawerCheckbox = document.getElementById("__drawer");
        if (drawerBtn && drawerCheckbox) {
            function syncExpanded() {
                drawerBtn.setAttribute("aria-expanded", drawerCheckbox.checked ? "true" : "false");
            }

            function toggleDrawer(event) {
                event.preventDefault();
                drawerCheckbox.checked = !drawerCheckbox.checked;
                drawerCheckbox.dispatchEvent(new Event("change"));
            }

            syncExpanded();
            drawerCheckbox.addEventListener("change", syncExpanded);
            drawerBtn.addEventListener("click", toggleDrawer);
            drawerBtn.addEventListener("keydown", function (event) {
                if (event.key !== " " && event.key !== "Enter") {
                    return;
                }
                toggleDrawer(event);
            });
        }

        var btn = document.querySelector("[data-md-component=\"palette-toggle\"]");
        if (!btn) {
            return;
        }
        btn.addEventListener("click", function () {
            var current = root.getAttribute("data-md-color-scheme") === "slate" ? "default" : "slate";
            apply(current);
            try {
                localStorage.setItem(STORAGE_KEY, current);
            } catch (e) { /* ignore */
            }
        });
    });
})();

/* Scroll the primary sidebar's active nav link into view on first load.
   The sidebar's __inner element scrolls independently (sticky position +
   max-height + overflow-y: auto), so the page-level scroll behavior won't
   reach it; we have to scroll the sidebar itself. Centered so readers can
   see siblings above + below their current page in the tree. */
(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        var sidebar = document.querySelector(".md-sidebar--primary .md-sidebar__inner");
        if (!sidebar) {
            return;
        }

        var active = sidebar.querySelector(".md-nav__link--active");
        if (!active) {
            return;
        }

        var sidebarRect = sidebar.getBoundingClientRect();
        var activeRect = active.getBoundingClientRect();
        var offset = activeRect.top - sidebarRect.top - sidebar.clientHeight / 2 + active.clientHeight / 2;
        sidebar.scrollTop = sidebar.scrollTop + offset;
    });
})();

/* Primary-sidebar live filter. Hides nav items whose label and
   descendants don't match the query; auto-expands matching ancestors
   so hits are visible without manual click-through. */
(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        var holder = document.querySelector(".md-sidebar--primary [data-md-component=\"nav-filter\"]");
        if (!holder) {
            return;
        }

        var input = holder.querySelector("input");
        var nav = document.querySelector(".md-sidebar--primary .md-nav--primary");
        if (!input || !nav) {
            return;
        }

        var items = nav.querySelectorAll(".md-nav__item");
        var toggleSnapshot = [];
        var toggles = nav.querySelectorAll("input.md-nav__toggle");
        for (var t = 0; t < toggles.length; t++) {
            toggleSnapshot.push(toggles[t].checked);
        }

        function labelText(item) {
            var direct = item.querySelector(":scope > .md-nav__link, :scope > .md-nav__container > .md-nav__link");
            return direct ? (direct.textContent || "").toLowerCase() : "";
        }

        function clearItem(item) {
            item.classList.remove("md-nav__item--filter-hidden");
            var link = item.querySelector(":scope > .md-nav__link, :scope > .md-nav__container > .md-nav__link");
            if (link) {
                link.classList.remove("md-nav__link--filter-match");
            }
        }

        function reset() {
            for (var i = 0; i < items.length; i++) {
                clearItem(items[i]);
            }
            var t = nav.querySelectorAll("input.md-nav__toggle");
            for (var j = 0; j < t.length && j < toggleSnapshot.length; j++) {
                t[j].checked = toggleSnapshot[j];
            }
        }

        /* Returns true if this item or any descendant matched. */
        function walk(item, query) {
            var children = item.querySelectorAll(":scope > nav > ul > li.md-nav__item, :scope > .md-nav__container > nav > ul > li.md-nav__item");
            var anyChildMatch = false;
            for (var i = 0; i < children.length; i++) {
                if (walk(children[i], query)) {
                    anyChildMatch = true;
                }
            }

            var selfText = labelText(item);
            var selfMatch = selfText.indexOf(query) >= 0;
            var link = item.querySelector(":scope > .md-nav__link, :scope > .md-nav__container > .md-nav__link");
            if (link) {
                link.classList.toggle("md-nav__link--filter-match", selfMatch && query.length > 0);
            }

            var visible = selfMatch || anyChildMatch;
            item.classList.toggle("md-nav__item--filter-hidden", !visible);

            if (anyChildMatch) {
                var toggle = item.querySelector(":scope > input.md-nav__toggle");
                if (toggle) {
                    toggle.checked = true;
                }
            }

            return visible;
        }

        var debounceTimer = 0;
        input.addEventListener("input", function () {
            window.clearTimeout(debounceTimer);
            debounceTimer = window.setTimeout(function () {
                var query = input.value.trim().toLowerCase();
                if (!query) {
                    reset();
                    return;
                }

                var roots = nav.querySelectorAll(":scope > ul > li.md-nav__item");
                for (var i = 0; i < roots.length; i++) {
                    walk(roots[i], query);
                }
            }, 80);
        });
    });
})();

/* Clipboard button — wires the <button class="md-clipboard"> emitted by
   HighlightPlugin to copy the surrounding <pre><code> body via the
   navigator.clipboard API. Adds a transient .md-clipboard--copied class so
   the CSS can flash a "Copied" pill, then removes it after a short delay. */
(function () {
    document.addEventListener("click", function (event) {
        var button = event.target.closest(".md-clipboard");
        if (!button) return;

        var code = button.parentElement && button.parentElement.querySelector("pre > code");
        if (!code) return;

        var text = code.innerText;
        if (!navigator.clipboard) {
            // Older browser without async clipboard support — fall back to legacy execCommand.
            var area = document.createElement("textarea");
            area.value = text;
            area.style.position = "fixed";
            area.style.opacity = "0";
            document.body.appendChild(area);
            area.select();
            try {
                document.execCommand("copy");
            } catch (e) { /* swallow */
            }
            document.body.removeChild(area);
            flashCopied(button);
            return;
        }

        navigator.clipboard.writeText(text).then(function () {
            flashCopied(button);
        });
    });

    function flashCopied(button) {
        button.classList.add("md-clipboard--copied");
        window.setTimeout(function () {
            button.classList.remove("md-clipboard--copied");
        }, 1500);
    }
})();
