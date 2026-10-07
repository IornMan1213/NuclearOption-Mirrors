"""Create a GitHub release for a version and attach NOMirrors.dll, using the token from git's credential helper.

python tools/gh_release.py 0.0.2
Notes come from that version's CHANGELOG.md section. The token is never printed.
"""
import hashlib, json, os, re, subprocess, sys, urllib.request

REPO = "IornMan1213/NuclearOption-Mirrors"
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DLL = os.path.join(ROOT, "src", "NOMirrors", "bin", "Release", "NOMirrors.dll")


def token():
    out = subprocess.run(["git", "credential", "fill"], input="protocol=https\nhost=github.com\n\n", capture_output=True, text=True, cwd=ROOT).stdout
    return next(l.split("=", 1)[1] for l in out.splitlines() if l.startswith("password="))


def api(method, url, tok, data=None, ctype="application/json"):
    req = urllib.request.Request(url, data=data, method=method, headers={"Authorization": f"Bearer {tok}", "Accept": "application/vnd.github+json", "Content-Type": ctype})
    with urllib.request.urlopen(req) as r:
        return json.load(r)


def main():
    ver = sys.argv[1]
    log = open(os.path.join(ROOT, "CHANGELOG.md"), encoding="utf-8").read()
    m = re.search(rf"^## {re.escape(ver)}[^\n]*\n(.*?)(?=^## |\Z)", log, re.S | re.M)
    notes = (m.group(1).strip() if m else "") + ("\n\n**Install:** put `NOMirrors.dll` in `BepInEx/plugins/` (any subfolder). "
                                                 "Requires BepInEx 5. See the README for adding mirrors to an aircraft.")
    tok = token()
    sha = subprocess.run(["git", "rev-parse", "HEAD"], capture_output=True, text=True, cwd=ROOT).stdout.strip()
    rel = api("POST", f"https://api.github.com/repos/{REPO}/releases", tok, json.dumps(
        {"tag_name": f"v{ver}", "target_commitish": sha, "name": f"v{ver}", "body": notes, "make_latest": "true"}).encode())
    print("release:", rel["html_url"])
    data = open(DLL, "rb").read()
    a = api("POST", rel["upload_url"].split("{")[0] + "?name=NOMirrors.dll", tok, data, "application/octet-stream")
    print("asset:", a["browser_download_url"], a["size"], "sha256:" + hashlib.sha256(data).hexdigest())


if __name__ == "__main__":
    main()
