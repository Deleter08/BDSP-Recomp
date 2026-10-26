#!/usr/bin/env python3
"""Unattended AssetRipper export of the game dump into a Unity project - the first stage of the automated setup (author, s46:
"I want the players to be able to setup the project with as few steps as possible, so automate everything as much as possible").

Drives AssetRipper's GUI server headless over its local HTTP API (the same calls the browser UI makes): start the server, apply the
export settings (POST /Settings/Update, then read them back and verify), POST /LoadFolder <game root>, POST /Export/UnityProject
<out>, then follow the log until "Finished post-export" and stop the server. Guards every 20 s: free space on the output drive and
the memory Windows can still hand out (RAM + page file; free physical RAM alone is no floor - paging is normal on a 16 GB PC) -
below a floor the server is stopped (memory rules: disk budget before any run, one heavy job at a time).

  python -I setup/stages/ar_export.py [--exe <AssetRipper.GUI.*.exe>] [--game <game root>] [--out <folder>] [--port 29232]
        [--min-disk 2] [--min-commit 3] [--status <file>] [--heap-limit-gb N] [--workstation-gc]
Defaults come from the setup config (setup/lib/paths.py): assetripper, dump, <work>/export. A half-finished export left by an
interrupted run is removed first; a finished one is kept (EXPORT_DONE marker) and the stage succeeds without exporting again.

<game root> holds exefs\\main + romfs\\Data (AssetRipper's Switch detection needs the NSO + metadata; never romfs\\Data alone).
Settings = the ones the s3 export (Premium 1.3.14) used; options the running edition does not offer are reported, not fatal.
"""
import argparse
import ctypes
import os
import re
import shutil
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import assetripper_settings as ars  # noqa: E402
import paths  # noqa: E402

# the s3 export's settings (notes/assetripper_settings_before.html); SaveSettingsToDisk off = nothing written next to the exe
SETTINGS = {
    "BundledAssetsExportMode": "DirectExport", "ScriptContentLevel": "Level3", "AudioExportFormat": "Default",
    "ImageExportFormat": "Png", "LightmapTextureExportFormat": "Yaml", "SpriteExportMode": "Yaml", "ShaderExportMode": "Decompile",
    "TextExportMode": "Parse", "ScriptLanguageVersion": "AutoSafe", "ScriptExportMode": "Decompiled",
    "IgnoreStreamingAssets": False, "EnableStaticMeshSeparation": True, "RemoveNullableAttributes": False,
    "PublicizeAssemblies": False, "EnablePrefabOutlining": False, "EnableAssetDeduplication": False,
    "ScriptTypesFullyQualified": False, "PreferOriginalTextureExtension": True, "ExportUnreadableAssets": False,
    "SaveSettingsToDisk": False, "DefaultVersion": "0.0.0a0", "TargetVersion": "0.0.0a0",
}


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k):
        return None


OPENER = urllib.request.build_opener(NoRedirect)


def http(port, path, form=None, timeout=7200):
    url = "http://localhost:%d%s" % (port, path)
    data = urllib.parse.urlencode(form).encode() if form is not None else None
    try:
        with OPENER.open(urllib.request.Request(url, data=data), timeout=timeout) as r:
            return r.status, r.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:          # 302 = success for the POST endpoints
        return e.code, ""


def memory_gb():
    """(free physical RAM, free commit) in GB. macOS: no page-file counter, report free RAM for both."""
    if sys.platform == "darwin":
        page = os.sysconf("SC_PAGE_SIZE")
        try:
            out = subprocess.check_output(["vm_stat"], text=True)
            pages = {}
            for line in out.splitlines():
                if ":" in line:
                    k, v = line.split(":", 1)
                    try:
                        pages[k.strip()] = int(v.strip().rstrip("."))
                    except ValueError:
                        pass
            avail = pages.get("Pages free", 0) + pages.get("Pages inactive", 0) + pages.get("Pages speculative", 0)
            free = avail * page / 2 ** 30
            return free, free
        except Exception:
            total = os.sysconf("SC_PHYS_PAGES") * page / 2 ** 30
            return total, total
    class M(ctypes.Structure):
        _fields_ = [("l", ctypes.c_ulong), ("ml", ctypes.c_ulong)] + [(n, ctypes.c_ulonglong) for n in
                                                                       ("t", "a", "tp", "ap", "tv", "av", "x")]
    m = M()
    m.l = ctypes.sizeof(M)
    ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(m))
    return m.a / 2 ** 30, m.ap / 2 ** 30


def free_ram_gb():
    return memory_gb()[0]


# AssetRipper's .NET heap cap. Measured on BDSP v1.3.0 (2026-10-15, work/setup/memtest): 10 GB -> OutOfMemory while loading,
# 14 GB -> OutOfMemory while exporting, 20 GB -> complete (all 175,208 asset paths, 0 errors, 8.3 min, peak commit 21.2 GB).
# Without a cap the GC grows to what the machine offers (~28 GB on a 96 GB PC), which pushes a 32 GB PC into paging.
HEAP_GB = 20


# Loading the dump (POST /LoadFolder) answers only when it is done - 1-2 min on the reference PC, much longer on a PC that pages.
# AssetRipper's log tells where it is; these are its lines for BDSP v1.3.0 (measured 2026-10-15), in order.
LOAD_PHASES = [("Attempting to read files", "looking at the dump"),
               ("Collecting game files", "finding the game files"),
               ("Collecting Streaming Assets", "finding the asset bundles"),
               ("scripting backend", "reading the asset bundles and the game's code"),
               ("Finished reading files", "processing the loaded assets")]
BUNDLE_FOUND = "Asset bundle '"           # "Import : Asset bundle 'battle' has been found"
BUNDLES = 14052                           # such lines in a v1.3.0 dump
PROCESSING_STEPS = ["Creating Scene Definitions", "Main Asset Pairing", "Reconstruct AnimatorController Assets",
                    "Reconstruct AudioMixer Assets", "Editor Format Conversion", "Lighting Data Assets",
                    "Processing Scriptable Object Groups"]
EXPORT_PROGRESS = re.compile(r"ExportProgress : \((\d+)/(\d+)\)")
# The bundles are listed in seconds; reading their content logs nothing - AssetRipper's memory is the progress there (live run,
# 2026-10-16: 0.7 GB -> 17.0 GB -> 18.5 GB at the end of loading, 55 s on the reference PC)
LOADED_GB = 18.5
LOAD_REPORT_S = 15                        # progress line cadence while loading / exporting
EXPORT_REPORT_S = 30


class LoadProgress:
    def __init__(self):
        self.phase, self.bundles, self.step = "starting", 0, 0

    def feed(self, text):
        for line in text.splitlines():
            if BUNDLE_FOUND in line and "has been found" in line:
                self.bundles += 1
                continue
            for key, phase in LOAD_PHASES:
                if key in line:
                    self.phase = phase
            for i, s in enumerate(PROCESSING_STEPS):
                if "Processing : " + s in line:
                    self.step = i + 1

    def text(self, mem_gb=None):
        if self.phase == "reading the asset bundles and the game's code" and mem_gb is not None:
            return "reading the asset bundles and the game's code: %.1f GB of about %g GB in memory" % (mem_gb, LOADED_GB)
        if self.phase == "finding the asset bundles":
            return "finding the asset bundles: {:,} of {:,} ({}%)".format(self.bundles, BUNDLES, 100 * self.bundles // BUNDLES)
        if self.phase == "processing the loaded assets" and self.step:
            return "processing the loaded assets: step %d of %d (%s)" % (self.step, len(PROCESSING_STEPS),
                                                                          PROCESSING_STEPS[self.step - 1])
        return self.phase


def proc_mem_gb(pid):
    """(peak private commit, peak working set, current working set) of one process in GB, or None. macOS: RSS via ps(1) as 'now'."""
    if sys.platform == "darwin":
        try:
            out = subprocess.check_output(["ps", "-o", "rss=", "-p", str(pid)], text=True).strip()
            if out:
                return None, None, int(out) / 2 ** 20     # RSS KB -> GB
        except Exception:
            pass
        return None
    class PMC(ctypes.Structure):
        _fields_ = [("cb", ctypes.c_ulong), ("faults", ctypes.c_ulong)] + [(n, ctypes.c_size_t) for n in
                                                                            ("pws", "ws", "qppp", "qpp", "qpnp", "qnp", "pf", "ppf")]
    h = ctypes.windll.kernel32.OpenProcess(0x1000, False, pid)        # PROCESS_QUERY_LIMITED_INFORMATION
    if not h:
        return None
    try:
        c = PMC()
        c.cb = ctypes.sizeof(PMC)
        if not ctypes.windll.kernel32.K32GetProcessMemoryInfo(h, ctypes.byref(c), c.cb):
            return None
        return c.ppf / 2 ** 30, c.pws / 2 ** 30, c.ws / 2 ** 30
    finally:
        ctypes.windll.kernel32.CloseHandle(h)


def mem_text(pid):
    m = proc_mem_gb(pid)
    if not m:
        return "AssetRipper memory n/a"
    if m[0] is None:
        return "AssetRipper now %.1f GB" % m[2]
    return "AssetRipper peak commit %.1f GB, peak working set %.1f GB, now %.1f GB" % m


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--exe", default=paths.CFG.get("assetripper"))
    ap.add_argument("--game", default=paths.DUMP)
    ap.add_argument("--out", default=paths.EXPORT)
    ap.add_argument("--port", type=int, default=29232)
    ap.add_argument("--min-disk", type=float, default=2.0)
    ap.add_argument("--min-commit", type=float, default=3.0, help="GB of RAM + page file that must stay available")
    ap.add_argument("--status", default=None)
    ap.add_argument("--heap-limit-gb", type=float, default=float(paths.CFG.get("assetripper_heap_gb", HEAP_GB)),
                    help="cap AssetRipper's .NET heap (DOTNET_GCHeapHardLimit); 0 = no cap. Default %d (config "
                         "assetripper_heap_gb)" % HEAP_GB)
    ap.add_argument("--workstation-gc", action="store_true", help="DOTNET_gcServer=0 (one GC heap instead of one per CPU)")
    a = ap.parse_args()
    out = os.path.abspath(a.out)
    work = os.path.dirname(out)
    os.makedirs(work, exist_ok=True)
    log = os.path.join(work, os.path.basename(out) + "_assetripper.log")
    status = open(a.status or os.path.join(work, os.path.basename(out) + "_status.txt"), "a", encoding="utf-8")

    def say(msg):
        line = time.strftime("%H:%M:%S ") + msg
        print(line, flush=True)
        status.write(line + "\n")
        status.flush()

    if not a.exe or not a.game:
        sys.exit("AssetRipper exe or game dump not configured (run setup.py with --assetripper / --dump)")
    done_marker = os.path.join(out, "EXPORT_DONE")
    if os.path.exists(done_marker):
        say("export already complete (%s) - kept" % done_marker)
        return
    if os.path.exists(out) and os.listdir(out):
        say("removing the half-finished export of an interrupted run: " + out)
        shutil.rmtree(out)
    os.makedirs(work, exist_ok=True)
    if sys.platform == "darwin":
        drive = os.path.abspath(out)
        while not os.path.isdir(drive):
            drive = os.path.dirname(drive)
    else:
        drive = os.path.splitdrive(out)[0] + "\\"
    say("START disk %s %.1f GB free, RAM %.1f GB free, RAM + page file %.1f GB free; floors disk %.1f, RAM + page file %.1f" % (
        drive, shutil.disk_usage(drive).free / 2 ** 30, *memory_gb(), a.min_disk, a.min_commit))
    env = dict(os.environ, DOTNET_Thread_DefaultStackSize="1000000", COMPlus_Thread_DefaultStackSize="1000000")
    if a.heap_limit_gb:
        env["DOTNET_GCHeapHardLimit"] = "%X" % int(a.heap_limit_gb * 2 ** 30)    # hex bytes; logged with GB below
    if a.workstation_gc:
        env["DOTNET_gcServer"] = "0"
    say("AssetRipper memory: %s" % ("heap capped at %g GB" % a.heap_limit_gb if a.heap_limit_gb else "no cap") +
        (", workstation GC" if a.workstation_gc else ""))
    srv = subprocess.Popen([a.exe, "--headless", "--port", str(a.port), "--log-path", log], cwd=os.path.dirname(a.exe), env=env,
                           stdout=open(os.path.join(work, os.path.basename(out) + "_stdout.log"), "w"), stderr=subprocess.STDOUT)

    def stop(reason):
        say("STOP server (%s)" % reason)
        if sys.platform == "darwin":
            subprocess.run(["pkill", "-9", "-P", str(srv.pid)], capture_output=True)
            try:
                srv.kill()
            except OSError:
                pass
        else:
            subprocess.run(["taskkill", "/PID", str(srv.pid), "/T", "/F"], capture_output=True)

    try:
        for _ in range(90):
            time.sleep(2)
            try:
                http(a.port, "/", timeout=5)
                break
            except OSError:
                continue
        else:
            stop("server did not come up")
            sys.exit(1)
        page = os.path.join(work, os.path.basename(out) + "_settings_default.html")
        open(page, "w", encoding="utf-8").write(http(a.port, "/Settings/Edit")[1])
        offered = ars.parse(page)
        form, missing = {}, []
        for k, v in SETTINGS.items():
            if k not in offered or offered[k].endswith("LOCKED"):
                missing.append(k + ("" if k not in offered else " (locked: %s)" % offered[k].split()[0]))
            elif isinstance(v, bool):
                if v:
                    form[k] = ""             # a checked box posts its value="" as the browser does; unchecked = absent
            else:
                form[k] = v
        say("settings not offered by this edition: %s" % (", ".join(missing) or "none"))
        code, _ = http(a.port, "/Settings/Update", form)
        page2 = os.path.join(work, os.path.basename(out) + "_settings_used.html")
        open(page2, "w", encoding="utf-8").write(http(a.port, "/Settings/Edit")[1])
        now = ars.parse(page2)
        bad = [k for k, v in SETTINGS.items() if k in now and not now[k].endswith("LOCKED") and
               now[k].split()[0] != (v if not isinstance(v, bool) else ("checked" if v else "off"))]
        say("Settings/Update http %s; settings not as wanted after update: %s" % (code, ", ".join(bad) or "none"))
        if bad:
            stop("settings did not apply")
            sys.exit(1)
        t0 = time.time()
        say("LoadFolder %s - AssetRipper reads the whole dump before it writes anything (progress every %d s)" % (
            a.game, LOAD_REPORT_S))
        result = {}

        def load():
            try:
                result["code"] = http(a.port, "/LoadFolder", {"Path": a.game})[0]
            except Exception as e:                 # noqa: BLE001 - reported below
                result["error"] = e
        loader = threading.Thread(target=load, daemon=True)
        log_pos = os.path.getsize(log) if os.path.exists(log) else 0
        loader.start()
        progress, last_report = LoadProgress(), time.time()
        while loader.is_alive():
            loader.join(5)
            if os.path.exists(log):
                with open(log, "rb") as f:
                    f.seek(log_pos)
                    new = f.read()
                log_pos += len(new)
                text = new.decode("utf-8", "replace")
                progress.feed(text)
                if "OutOfMemoryException" in text:
                    stop("AssetRipper ran out of memory while loading the dump")
                    sys.exit("AssetRipper ran out of memory while loading the dump - close other programs and make sure Windows' "
                             "page file can grow (it is on the system drive), then run the setup again; log: %s" % log)
            disk = shutil.disk_usage(drive).free / 2 ** 30
            ram, commit = memory_gb()
            if srv.poll() is not None:
                say("server exited by itself rc=%s while loading the dump" % srv.returncode)
                sys.exit(1)
            if commit < a.min_commit:
                stop("Windows is about to run out of memory: RAM + page file %.1f GB free < %.1f" % (commit, a.min_commit))
                sys.exit(1)
            if loader.is_alive() and time.time() - last_report >= LOAD_REPORT_S:
                last_report = time.time()
                m = proc_mem_gb(srv.pid)
                say("%5.1f min | loading: %s | RAM + page file %.1f GB free" % (
                    (time.time() - t0) / 60, progress.text(m[2] if m else None), commit))
        if "error" in result:
            stop("loading the dump failed")
            sys.exit("AssetRipper did not answer the load request: %s" % result["error"])
        code = result["code"]
        say("LoadFolder http %s after %.0f s (%d asset bundles)" % (code, time.time() - t0, progress.bundles))
        if code not in (200, 302):
            oom = os.path.exists(log) and "OutOfMemoryException" in open(log, encoding="utf-8", errors="replace").read()
            stop("loading the dump failed" + (" - AssetRipper ran out of memory" if oom else ""))
            sys.exit("AssetRipper could not load the dump (http %s)%s - see %s" % (
                code, "; it ran out of memory: close other programs and make sure Windows' page file can grow" if oom else "", log))
        os.makedirs(out, exist_ok=True)
        say("Export/UnityProject -> %s" % out)
        exp = subprocess.Popen([sys.executable, "-I", "-c",
                                "import urllib.request,urllib.parse,sys\n"
                                "try: urllib.request.urlopen(urllib.request.Request(sys.argv[1], data=urllib.parse.urlencode("
                                "{'Path': sys.argv[2]}).encode()), timeout=36000)\n"
                                "except Exception as e: print(e)\n",
                                "http://localhost:%d/Export/UnityProject" % a.port, out], stdin=subprocess.DEVNULL)
        last = 0
        read_to = log_pos                       # the load lines were checked above
        while True:
            time.sleep(20)
            disk = shutil.disk_usage(drive).free / 2 ** 30
            ram, commit = memory_gb()
            tail = ""
            if os.path.exists(log):
                with open(log, "rb") as f:
                    f.seek(read_to)
                    new = f.read()
                    read_to += len(new)
                if b"OutOfMemoryException" in new:
                    # AssetRipper carries on after it (e.g. "Unable to read MonoBehaviour Structure ... (OutOfMemoryException)")
                    # and would write an incomplete export
                    stop("AssetRipper ran out of memory")
                    sys.exit("AssetRipper ran out of memory during the export - close other programs and make sure Windows' page "
                             "file can grow (it is on the system drive), then run the setup again; log: %s" % log)
                with open(log, "rb") as f:
                    f.seek(max(0, os.path.getsize(log) - 4096))
                    lines = f.read().decode("utf-8", "replace").strip().splitlines()
                tail = lines[-1][:120] if lines else ""
                if any("Finished post-export" in l for l in lines):
                    say("DONE after %.1f min; disk %.1f GB free, RAM %.1f GB free; %s" % ((time.time() - t0) / 60, disk, ram,
                                                                                         mem_text(srv.pid)))
                    break
            if srv.poll() is not None:
                say("server exited by itself rc=%s; log tail: %s" % (srv.returncode, tail))
                sys.exit(1)
            if disk < a.min_disk:
                stop("disk %.1f GB < %.1f" % (disk, a.min_disk))
                sys.exit(1)
            if commit < a.min_commit:
                stop("Windows is about to run out of memory: RAM + page file %.1f GB free < %.1f" % (commit, a.min_commit))
                sys.exit(1)
            if time.time() - last >= EXPORT_REPORT_S:
                last = time.time()
                prog = None
                if os.path.exists(log):
                    with open(log, "rb") as f:
                        f.seek(max(0, os.path.getsize(log) - 65536))
                        found = EXPORT_PROGRESS.findall(f.read().decode("utf-8", "replace"))
                    if found:
                        n, total = (int(x) for x in found[-1])
                        prog = "exporting {:,} of {:,} assets ({}%)".format(n, total, 100 * n // total)
                say("%5.1f min | %s | disk %.1f GB free | RAM + page file %.1f GB free | %s" % (
                    (time.time() - t0) / 60, prog or tail, disk, commit, mem_text(srv.pid)))
        exp.wait(timeout=60)
        with open(done_marker, "w", encoding="utf-8") as f:
            f.write(time.strftime("%Y-%m-%d %H:%M:%S") + "\n")
    finally:
        if srv.poll() is None:
            stop("finished")


if __name__ == "__main__":
    main()
