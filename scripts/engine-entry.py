"""The packaged process keeps stdout exclusively for the JSONL protocol."""
from semojum_braille import sidecar
from editor_composition import compose
from editor_translation import PageTranslationCache, add_formula_rules, translate_editor

upstream_handle = sidecar.handle
page_cache = PageTranslationCache(sidecar)


def handle(request):
    op = request.get("op")
    if op == "compose_editor":
        return compose(request.get("job") or {})
    if op == "translate_page":
        result = page_cache.handle(request, upstream_handle)
        for element, output in zip(request.get("elements") or [], result["elements"], strict=True):
            add_formula_rules(element, output, sidecar)
        return result
    if op == "translate_editor":
        result = translate_editor({**request, "op": "translate"}, sidecar, upstream_handle)
    else:
        result = upstream_handle(request)
    if op in ("translate", "translate_editor"):
        add_formula_rules(request, result, sidecar)
    if op == "ping":
        result["editor_protocol"] = 1
    return result


sidecar.handle = handle

if __name__ == "__main__":
    sidecar.main()
