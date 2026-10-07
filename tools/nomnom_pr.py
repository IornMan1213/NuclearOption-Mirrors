"""Push nomnom/NOMirrors.json to the open NOMNOM pull request (fork IornMan1213/NOMNOM), or report its state.

python tools/nomnom_pr.py [--update]
"""
import base64, json, os, subprocess, sys, urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PR = "https://api.github.com/repos/KopterBuzz/NOMNOM/pulls/421"


def token():
    out = subprocess.run(["git", "credential", "fill"], input="protocol=https\nhost=github.com\n\n", capture_output=True, text=True, cwd=ROOT).stdout
    return next(l.split("=", 1)[1] for l in out.splitlines() if l.startswith("password="))


def api(method, url, tok, body=None):
    req = urllib.request.Request(url, data=json.dumps(body).encode() if body is not None else None, method=method,
                                 headers={"Authorization": f"Bearer {tok}", "Accept": "application/vnd.github+json", "Content-Type": "application/json"})
    with urllib.request.urlopen(req) as r:
        return json.load(r)


def main():
    path = os.path.join(ROOT, "nomnom", "NOMirrors.json")
    data = json.load(open(path, encoding="utf-8-sig"))
    text = json.dumps(data, indent=2, ensure_ascii=False) + "\n"
    open(path, "w", encoding="utf-8", newline="\n").write(text)          # normalised: no BOM, 2-space indent
    tok = token()
    pr = api("GET", PR, tok)
    print("PR", pr["state"], "merged" if pr.get("merged") else "not merged", pr["head"]["repo"]["full_name"], pr["head"]["ref"])
    if "--update" not in sys.argv or pr["state"] != "open":
        return
    repo, ref = pr["head"]["repo"]["full_name"], pr["head"]["ref"]
    url = f"https://api.github.com/repos/{repo}/contents/modManifests/NOMirrors.json"
    cur = api("GET", url + f"?ref={ref}", tok)
    api("PUT", url, tok, {"message": "NOMirrors: add 0.0.2", "content": base64.b64encode(text.encode()).decode(), "sha": cur["sha"], "branch": ref})
    print("updated", url, "on", ref)


if __name__ == "__main__":
    main()
