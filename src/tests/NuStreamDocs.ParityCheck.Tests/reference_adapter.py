"""Thin adapter: reads JSON from stdin, renders each fragment with a reference engine, writes JSON to stdout.

Request:  {"engine": "mkdocs" | "zensical", "fragments": [{"id": "...", "markdown": "..."}]}
Response: {"engine": "...", "version": "...", "results": {"<id>": {"html": "..."} | {"error": "..."}}}
"""

import json
import os
import sys
import tempfile
from importlib.metadata import version


def mkdocs_renderer():
    import markdown
    from mkdocs.config.defaults import MkDocsConfig

    config = MkDocsConfig()
    config.load_dict({"site_name": "parity", "docs_dir": tempfile.mkdtemp(prefix="parity-mkdocs-")})
    errors, _ = config.validate()
    if errors:
        raise RuntimeError("mkdocs config invalid: " + repr(errors))

    def render(text):
        md = markdown.Markdown(
            extensions=config["markdown_extensions"],
            extension_configs=config["mdx_configs"] or {},
        )
        return md.convert(text)

    return render, "mkdocs " + version("mkdocs") + " / markdown " + version("markdown")


def zensical_renderer():
    from zensical.config import parse_config
    from zensical.markdown.render import render as zensical_render

    directory = tempfile.mkdtemp(prefix="parity-zensical-")
    os.makedirs(os.path.join(directory, "docs"))
    path = os.path.join(directory, "zensical.toml")
    with open(path, "w", encoding="utf-8") as handle:
        handle.write('[project]\nsite_name = "parity"\n')
    parse_config(path)

    def render(text):
        return zensical_render(text, "index.md", "index/")["content"]

    return render, "zensical " + version("zensical") + " / markdown " + version("markdown")


ENGINES = {"mkdocs": mkdocs_renderer, "zensical": zensical_renderer}


def main():
    request = json.load(sys.stdin)
    render, label = ENGINES[request["engine"]]()
    results = {}
    for fragment in request["fragments"]:
        try:
            results[fragment["id"]] = {"html": render(fragment["markdown"])}
        except Exception as error:  # noqa: BLE001
            results[fragment["id"]] = {"error": type(error).__name__ + ": " + str(error)}
    sys.stdout.write(json.dumps({"engine": request["engine"], "version": label, "results": results}))


if __name__ == "__main__":
    main()
