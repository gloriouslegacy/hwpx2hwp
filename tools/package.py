#!/usr/bin/env python3
"""win-x64 publish 후 artifacts/hwpx2hwp-<버전>-win-x64.zip (exe + README.txt) 을 만든다 (F8).

실행: 레포 루트에서 `python3 tools/package.py`. 기존 zip 은 덮어쓰지 않는다.
"""
import hashlib
import os
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
CSPROJ = REPO / "src" / "Hwpx2Hwp.App" / "Hwpx2Hwp.App.csproj"
README_SRC = REPO / "docs" / "release" / "README.txt"
ARTIFACTS = REPO / "artifacts"
PUBLISH_DIR = ARTIFACTS / "publish"
EXE_NAME = "hwpx2hwp.exe"
VERSION_RE = re.compile(r"[0-9]+(\.[0-9]+){1,3}([-+][0-9A-Za-z.-]+)?")


def log(msg):
    print(f"package: {msg}", flush=True)


def fail(msg, code=1):
    print(f"package: error={msg}", file=sys.stderr, flush=True)
    sys.exit(code)


def read_version():
    root = ET.parse(CSPROJ).getroot()
    for el in root.iter("Version"):
        if el.text and el.text.strip():
            version = el.text.strip()
            # zip 파일 이름에 들어가므로 경로 문자(../ 등)를 막는다: 쓰기는 artifacts/ 로만
            if not VERSION_RE.fullmatch(version):
                fail(f"csproj <Version> 형식이 잘못됐다 version={version!r} path={CSPROJ}")
            return version
    fail(f"csproj 에 <Version> 이 없다 path={CSPROJ}")


def find_dotnet():
    """(dotnet 경로, 자식 환경) 을 돌려준다."""
    env = os.environ.copy()
    found = shutil.which("dotnet")
    if found:
        return found, env
    home_dotnet = Path.home() / ".dotnet" / "dotnet"
    if home_dotnet.is_file():
        env["DOTNET_ROOT"] = str(home_dotnet.parent)
        return str(home_dotnet), env
    fail("dotnet 을 찾지 못했다 (PATH, ~/.dotnet/dotnet)")


def publish():
    dotnet, env = find_dotnet()
    args = [dotnet, "publish", str(CSPROJ.relative_to(REPO)), "-c", "Release",
            "-r", "win-x64", "-o", str(PUBLISH_DIR.relative_to(REPO))]
    log(f"step=publish dotnet={dotnet}")
    code = subprocess.run(args, cwd=REPO, env=env).returncode
    if code != 0:
        fail(f"dotnet publish 실패 exit={code}", code)
    exe = PUBLISH_DIR / EXE_NAME
    if not exe.is_file():
        fail(f"publish 결과에 {EXE_NAME} 이 없다 dir={PUBLISH_DIR}")
    return exe


def readme_bytes():
    text = README_SRC.read_text(encoding="utf-8-sig")
    text = text.replace("\r\n", "\n").replace("\r", "\n").replace("\n", "\r\n")
    return b"\xef\xbb\xbf" + text.encode("utf-8")


def write_zip(zip_path, exe):
    tmp = zip_path.with_name(zip_path.name + f".tmp{os.getpid()}")
    try:
        with zipfile.ZipFile(tmp, "w", compression=zipfile.ZIP_DEFLATED) as zf:
            zf.write(exe, EXE_NAME)
            zf.writestr(README_SRC.name, readme_bytes())
        try:
            os.link(tmp, zip_path)  # 대상이 있으면 FileExistsError: 덮어쓰지 않는다
        except FileExistsError:
            fail(f"zip 이 이미 있다, 덮어쓰지 않는다 path={zip_path}")
        except OSError as e:
            fail(f"zip 을 만들지 못했다 path={zip_path} reason={e}")
    finally:
        tmp.unlink(missing_ok=True)


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    version = read_version()
    zip_path = ARTIFACTS / f"hwpx2hwp-{version}-win-x64.zip"
    if zip_path.exists():
        fail(f"zip 이 이미 있다, 덮어쓰지 않는다 path={zip_path}")
    if not README_SRC.is_file():
        fail(f"README 원본이 없다 path={README_SRC}")
    log(f"step=start version={version}")
    exe = publish()
    write_zip(zip_path, exe)
    log(f"step=done zip={zip_path} size={zip_path.stat().st_size} sha256={sha256(zip_path)}")


if __name__ == "__main__":
    main()
