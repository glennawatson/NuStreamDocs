# Vendored runtime

- Package: `sql.js-httpvfs`
- Version: 0.8.12
- Source: https://www.npmjs.com/package/sql.js-httpvfs (https://github.com/phiresky/sql.js-httpvfs)
- License: Apache-2.0 (the bundled SQLite, via sql.js / Emscripten, is public domain)
- Vendored: 2026-05-12
- Files:
    - `sql.js-httpvfs.js` — UMD bundle of `dist/index.js` (exposes `window.createDbWorker`; `comlink` is bundled in).
    - `sqlite.worker.js` — UMD bundle of `dist/sqlite.worker.js` (the Web Worker that runs SQLite + the HTTP-range VFS).
    - `sql-wasm.wasm` — the sql.js (Emscripten-compiled SQLite, FTS5 enabled) WebAssembly binary, copied verbatim from
      `dist/sql-wasm.wasm`.
- Refresh: re-run `npm pack sql.js-httpvfs`, copy `dist/index.js` → `sql.js-httpvfs.js`, `dist/sqlite.worker.js` and
  `dist/sql-wasm.wasm` verbatim, then bump `SqliteAssets.PinnedRuntimeVersion`.
