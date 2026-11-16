"""The packaged process keeps stdout exclusively for the JSONL protocol."""
from semojum_braille import sidecar
from editor_composition import compose
from editor_translation import PageTranslationCache, translate_editor

upstream_handle = sidecar.handle
page_cache = PageTranslationCache(sidecar)


def handle(request):
    if request.get("op") == "compose_editor":
        return compose(request.get("job") or {})
    if request.get("op") == "translate_editor":
        return translate_editor({**request, "op": "translate"}, sidecar, upstream_handle)
    result = page_cache.handle(request, upstream_handle) if request.get("op") == "translate_page" else upstream_handle(request)
    if request.get("op") == "ping":
        result["editor_protocol"] = 1
    return result


sidecar.handle = handle

if __name__ == "__main__":
    sidecar.main()
