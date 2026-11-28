// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Playgrounds;

/// <summary>Built-in stylesheet and progressive-enhancement script for HTTP and code playgrounds.</summary>
internal static class PlaygroundsAssets
{
    /// <summary>Gets the UTF-8 CSS written to <c>assets/stylesheets/nstd-playgrounds.css</c>.</summary>
    internal static ReadOnlySpan<byte> CssBytes => """
        .nd-http-playground, .nd-code-playground {
          border: 1px solid rgba(128, 128, 128, 0.28);
          border-radius: 0.5rem;
          margin: 1rem 0;
          overflow: hidden;
        }
        .nd-http-bar {
          display: flex;
          align-items: center;
          gap: 0.6rem;
          padding: 0.55rem 0.85rem;
          background: rgba(128, 128, 128, 0.08);
        }
        .nd-http-method {
          font-weight: 700;
          font-size: 0.78rem;
          padding: 0.15rem 0.45rem;
          border-radius: 0.25rem;
          background: #2563eb;
          color: #ffffff;
        }
        .nd-http-tabs { display: flex; flex-wrap: wrap; }
        .nd-http-tab-input { position: absolute; opacity: 0; pointer-events: none; }
        .nd-http-tab-label {
          padding: 0.4rem 0.8rem;
          cursor: pointer;
          font-size: 0.82rem;
          font-weight: 600;
          border-bottom: 2px solid transparent;
        }
        .nd-http-panel { display: none; order: 99; width: 100%; padding: 0.5rem 0.85rem; }
        .nd-http-tab-input:checked + .nd-http-tab-label { border-bottom-color: #2563eb; }
        .nd-http-tab-input:checked + .nd-http-tab-label + .nd-http-panel { display: block; }
        .nd-http-tryit { border-top: 1px solid rgba(128, 128, 128, 0.2); padding: 0.5rem 0.85rem; }
        .nd-code-playground-frame { width: 100%; min-height: 22rem; border: 0; display: block; }
        .nd-code-playground-caption { padding: 0.4rem 0.75rem; font-size: 0.8rem; opacity: 0.8; }
        """u8;

    /// <summary>Gets the UTF-8 JavaScript written to <c>assets/javascripts/nstd-playgrounds.js</c>.</summary>
    internal static ReadOnlySpan<byte> JsBytes => """
        document.addEventListener("click", async (ev) => {
          const btn = ev.target.closest(".nd-http-send");
          if (!btn) return;
          const root = btn.closest(".nd-http-playground");
          const out = root.querySelector(".nd-http-response");
          const bodyInput = root.querySelector(".nd-http-body-input");
          const method = root.getAttribute("data-method") || "GET";
          const url = root.getAttribute("data-url") || "/";
          out.textContent = "Sending...";
          try {
            const init = { method };
            if (bodyInput && method !== "GET" && method !== "HEAD") init.body = bodyInput.value;
            const res = await fetch(url, init);
            const text = await res.text();
            out.textContent = `HTTP ${res.status}\n\n${text}`;
          } catch (err) {
            out.textContent = String(err);
          }
        });
        """u8;
}
