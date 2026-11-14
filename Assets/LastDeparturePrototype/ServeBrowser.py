"""Serve the local Unity WebGL build with compression and WebAssembly headers."""
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import tempfile
import argparse

ROOT = Path(tempfile.gettempdir()) / "YardSurvival-WebGL"


class UnityHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(ROOT), **kwargs)

    def guess_type(self, path):
        original = path.removesuffix(".gz").removesuffix(".br")
        if original.endswith(".wasm"):
            return "application/wasm"
        if original.endswith(".js"):
            return "application/javascript"
        if original.endswith(".data"):
            return "application/octet-stream"
        return super().guess_type(original)

    def end_headers(self):
        if self.path.split("?", 1)[0].endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        elif self.path.split("?", 1)[0].endswith(".br"):
            self.send_header("Content-Encoding", "br")
        self.send_header("Cache-Control", "no-cache")
        super().end_headers()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Local Unity WebGL preview server")
    parser.add_argument("--directory", type=Path, default=ROOT)
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args()
    ROOT = args.directory.resolve()
    if not (ROOT / "index.html").is_file():
        raise SystemExit("Build fehlt: in Unity Tools > Yard Survival > Build browser prototype ausführen.")
    print(f"Yard Survival: http://127.0.0.1:{args.port} — Ctrl+C beendet den Server.", flush=True)
    ThreadingHTTPServer(("127.0.0.1", args.port), UnityHandler).serve_forever()
