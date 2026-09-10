#!/usr/bin/env python3
"""
Zune Package Downloader & Iris Desktop Player Disassembler
Downloads ZunePackage.exe, unpacks the installer payloads, decompiles
ZuneShell.dll and extracts authentic Iris UIX markup, styles, and assets.
"""

import os
import sys
import glob
import shutil
import subprocess
import urllib.request

DOWNLOAD_URL = "https://files1.majorgeeks.com/0b93caee71a9d214d0bbbc5622ea29507e3b8a7a/internet/ZunePackage.exe"
# The decompiled corpus is deliberately kept OUTSIDE this repository (Microsoft
# IP is not redistributed). Default: a sibling `zune-disassembly/` directory next
# to the repo root; override with the ZUNE_DISASSEMBLY_DIR environment variable.
BASE_DIR = os.environ.get(
    "ZUNE_DISASSEMBLY_DIR",
    os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "zune-disassembly")),
)
PKG_PATH = os.path.join(BASE_DIR, "ZunePackage.exe")
PKG_DIR = os.path.join(BASE_DIR, "package")
MSI_DIR = os.path.join(BASE_DIR, "msi")
DECOMPILED_DIR = os.path.join(BASE_DIR, "zuneshell")
UIX_DIR = os.path.join(BASE_DIR, "uix")
ASSETS_DIR = os.path.join(BASE_DIR, "assets")

def run(cmd, cwd=None):
    print(f"[*] Running: {' '.join(cmd) if isinstance(cmd, list) else cmd}")
    res = subprocess.run(cmd, shell=isinstance(cmd, str), cwd=cwd, capture_output=True, text=True)
    if res.returncode != 0:
        print(f"[!] Warning (exit {res.returncode}): {res.stderr[:300]}")
    return res

def download_package():
    os.makedirs(BASE_DIR, exist_ok=True)
    if os.path.exists(PKG_PATH) and os.path.getsize(PKG_PATH) > 100 * 1024 * 1024:
        print(f"[+] ZunePackage.exe already downloaded ({os.path.getsize(PKG_PATH)} bytes).")
        return

    print(f"[+] Downloading ZunePackage.exe from {DOWNLOAD_URL}...")
    curl_cmd = [
        "curl", "-L", "-C", "-",
        "--retry", "3",
        "-A", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
        "-o", PKG_PATH,
        DOWNLOAD_URL
    ]
    res = run(curl_cmd)
    if not os.path.exists(PKG_PATH) or os.path.getsize(PKG_PATH) < 100 * 1024 * 1024:
        # Fallback to python urllib with stream
        print("[*] Curl failed or returned small file, trying urllib stream...")
        req = urllib.request.Request(DOWNLOAD_URL, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req) as resp, open(PKG_PATH, "wb") as out_file:
            shutil.copyfileobj(resp, out_file)

    print(f"[+] Download complete: {os.path.getsize(PKG_PATH)} bytes.")

def unpack_package():
    os.makedirs(PKG_DIR, exist_ok=True)
    print(f"[+] Unpacking ZunePackage.exe into {PKG_DIR}...")
    run(["7z", "x", "-y", f"-o{PKG_DIR}", PKG_PATH])

def unpack_msi():
    os.makedirs(MSI_DIR, exist_ok=True)
    msi_files = glob.glob(os.path.join(PKG_DIR, "**", "*.msi"), recursive=True)
    print(f"[+] Found {len(msi_files)} MSI installers: {msi_files}")

    # Prioritize Zune-x86.msi or Zune-x64.msi
    zune_msi = None
    for msi in msi_files:
        if "Zune-x86.msi" in msi or "Zune-x64.msi" in msi or "zune.msi" in msi.lower():
            zune_msi = msi
            break
    if not zune_msi and msi_files:
        zune_msi = msi_files[0]

    if zune_msi:
        print(f"[+] Extracting primary MSI: {zune_msi}...")
        run(["7z", "x", "-y", f"-o{MSI_DIR}", zune_msi])
    else:
        print("[!] No MSI found directly, checking for CAB archives...")
        cabs = glob.glob(os.path.join(PKG_DIR, "**", "*.cab"), recursive=True)
        for cab in cabs:
            run(["7z", "x", "-y", f"-o{MSI_DIR}", cab])

def find_binary(name):
    for root, _, files in os.walk(BASE_DIR):
        for f in files:
            if f.lower() == name.lower():
                return os.path.join(root, f)
    return None

def decompile_zune_shell():
    os.makedirs(DECOMPILED_DIR, exist_ok=True)
    os.makedirs(UIX_DIR, exist_ok=True)
    os.makedirs(ASSETS_DIR, exist_ok=True)

    zune_shell = find_binary("ZuneShell.dll")
    if not zune_shell:
        print("[!] Could not locate ZuneShell.dll in extracted files!")
        return

    print(f"[+] Found ZuneShell.dll: {zune_shell} ({os.path.getsize(zune_shell)} bytes)")

    # 1. Use 7z to directly extract resources from PE dll if possible
    pe_res_dir = os.path.join(BASE_DIR, "zuneshell_resources")
    os.makedirs(pe_res_dir, exist_ok=True)
    run(["7z", "x", "-y", f"-o{pe_res_dir}", zune_shell])

    # 2. Use ilspycmd to decompile project and dump all resources
    ilspy = os.path.expanduser("~/.dotnet/tools/ilspycmd")
    if os.path.exists(ilspy):
        print(f"[+] Decompiling ZuneShell.dll with ilspycmd into {DECOMPILED_DIR}...")
        run([ilspy, "-p", "-o", DECOMPILED_DIR, zune_shell])
        # Also list resources
        res_list = run([ilspy, "--list-resources", zune_shell])
        with open(os.path.join(BASE_DIR, "resources_list.txt"), "w") as f:
            f.write(res_list.stdout)
    else:
        print("[!] ilspycmd not found, relying on 7z PE extraction.")

    # 3. Collect all .uix files and graphics
    uix_count = 0
    asset_count = 0
    for root, _, files in os.walk(BASE_DIR):
        if root.startswith(UIX_DIR) or root.startswith(ASSETS_DIR):
            continue
        for f in files:
            ext = os.path.splitext(f)[1].lower()
            src = os.path.join(root, f)
            if ext == ".uix":
                dst = os.path.join(UIX_DIR, f)
                if not os.path.exists(dst):
                    shutil.copy2(src, dst)
                uix_count += 1
            elif ext in [".png", ".jpg", ".jpeg", ".ico", ".cur", ".bmp"]:
                dst = os.path.join(ASSETS_DIR, f)
                if not os.path.exists(dst):
                    shutil.copy2(src, dst)
                asset_count += 1

    print(f"[+] Extraction complete!")
    print(f"    Total .uix markup files extracted: {len(os.listdir(UIX_DIR))}")
    print(f"    Total graphic assets extracted: {len(os.listdir(ASSETS_DIR))}")

if __name__ == "__main__":
    download_package()
    unpack_package()
    unpack_msi()
    decompile_zune_shell()
