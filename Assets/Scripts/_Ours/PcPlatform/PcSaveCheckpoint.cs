#if !SWITCH
// PC platform layer: automatic save checkpoints for play-testing (author request, s34). NOT shipped code.
//
// Every save the game COMMITS is copied aside as a restorable checkpoint: manual saves (the report window), autosaves and event
// saves all end in Dpr.NX.SaveSystem.Save -> nn.fs.FileSystem.Commit, which reaches PcFs.Commit on the save thread after
// SaveData.bin / Backup.bin are written and closed. The save folder's files are copied to
//   <project>/../work/saves/<NNNNN>_<yyyyMMdd-HHmmss>_<kind>_<zone>/   + info.json
// kind = "auto" while PlayerWork's autosave coroutine runs, else "save"; "-scripted" when the play driver loaded an input script.
// While Play mode runs, <checkpoints>/.playing exists (removed when Play mode ends), so tools/save_checkpoints.py refuses to swap
// the save files under a running game. tools/save_checkpoints.py lists, names and loads the checkpoints.
// OPT-IN (2026-10-17): only a work/saves folder holding the file `.checkpoints` turns checkpoints on, and nothing creates that
// folder. Before, the editor created <project>/../work/saves on every Play (the public setup's verify step plays), and a player
// built up to four folders below it then copied every save there (issue #1's second log: C:\Users\<user>\work\saves).
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PcPlatform
{
    public static class PcSaveCheckpoint
    {
        public static bool Enabled = true;

        // Gregorian, whatever the Windows locale: the author's th-TH culture formats DateTime in the Buddhist calendar (2026 -> 2569).
        static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
        static readonly object gate = new object();
        static readonly Regex numbered = new Regex(@"^(\d{5})_");
        const string OptInFile = ".checkpoints";
        static string _dir;

        // <project>/../work/saves - the editor's working directory is the project folder. A player build has no project folder: the
        // first opted-in work/saves found walking up from the folder that holds the executable (<workspace>/build/<name>/Dpr.exe,
        // s38). null = checkpoints off.
        public static string Dir
        {
            get
            {
                if (_dir == null)
                {
#if UNITY_EDITOR
                    string c = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "work", "saves"));
                    _dir = OptedIn(c) ? c : null;
#else
                    _dir = FindWorkspaceSaves();
#endif
                }
                return _dir;
            }
        }

        static bool OptedIn(string saves) { return File.Exists(Path.Combine(saves, OptInFile)); }

#if !UNITY_EDITOR
        static string FindWorkspaceSaves()
        {
            var d = new DirectoryInfo(Path.GetDirectoryName(Application.dataPath));
            for (int i = 0; d != null && i < 4; i++, d = d.Parent)
            {
                string c = Path.Combine(d.FullName, "work", "saves");
                if (OptedIn(c))
                    return c;
            }
            return null;
        }
#endif

        static string PlayingMarker { get { return Path.Combine(Dir, ".playing"); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void MarkPlaying()
        {
            if (Dir == null)
            {
                Enabled = false;
                PcConfig.Log("save checkpoints: off (a development tool: a work/saves folder holding a " + OptInFile + " file turns it on)");
                return;
            }
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(PlayingMarker, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv) + " pid " +
                    System.Diagnostics.Process.GetCurrentProcess().Id);
                Application.quitting += ClearPlaying;
                PcConfig.Log("save checkpoints: every committed save is copied to " + Dir);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[PcPlatform] save checkpoints disabled: " + e.Message);
                Enabled = false;
            }
        }

        static void ClearPlaying()
        {
            try
            {
                File.Delete(PlayingMarker);
            }
            catch (Exception)
            {
            }
        }

        // Called by PcFs.Commit (the game's save thread) once a save mount is flushed. Never throws: a failed copy must not fail the
        // game's save.
        public static void Snapshot(string saveRoot)
        {
            if (!Enabled)
                return;
            try
            {
                lock (gate)
                {
                    string kind = AutoSaveRunning() ? "auto" : "save";
                    if (PcInput.Scripted)
                        kind += "-scripted";
                    int zoneId;
                    string zone = Zone(out zoneId);
                    DateTime now = DateTime.Now;
                    int seq = NextSequence();
                    string name = seq.ToString("00000", Inv) + "_" + now.ToString("yyyyMMdd-HHmmss", Inv) + "_" + kind + "_" + zone;
                    string target = Path.Combine(Dir, name);
                    Directory.CreateDirectory(target);
                    long bytes = 0;
                    foreach (var file in Directory.GetFiles(saveRoot))
                    {
                        File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
                        bytes += new FileInfo(file).Length;
                    }
                    File.WriteAllText(Path.Combine(target, "info.json"), Info(seq, now, kind, zone, zoneId, bytes), new UTF8Encoding(false));
                    PcConfig.Log("save checkpoint " + name);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[PcPlatform] save checkpoint failed: " + e);
            }
        }

        static bool AutoSaveRunning()
        {
            try
            {
                return PlayerWork.IsAutoSaveSystemBusy();
            }
            catch (Exception)
            {
                return false;
            }
        }

        static string Zone(out int zoneId)
        {
            zoneId = -1;
            try
            {
                ZoneID z = PlayerWork.zoneID;
                zoneId = (int)z;
                return Enum.IsDefined(typeof(ZoneID), z) ? z.ToString() : "zone" + zoneId;
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        // Highest number in the folder + 1, re-read every time (tools/save_checkpoints.py `save` numbers its copies the same way).
        static int NextSequence()
        {
            int next = 1;
            if (Directory.Exists(Dir))
            {
                foreach (var d in Directory.GetDirectories(Dir))
                {
                    var m = numbered.Match(Path.GetFileName(d));
                    if (m.Success)
                        next = Math.Max(next, int.Parse(m.Groups[1].Value) + 1);
                }
            }
            return next;
        }

        static string Info(int seq, DateTime now, string kind, string zone, int zoneId, long bytes)
        {
            string player = "", rival = "";
            int badges = -1;
            try
            {
                player = PlayerWork.userName ?? "";
                rival = PlayerWork.rivalName ?? "";
                badges = 0;
                for (int b = PlayerWork.badge; b != 0; b &= b - 1)
                    badges++;
            }
            catch (Exception)
            {
            }
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"number\": ").Append(seq).Append(",\n");
            sb.Append("  \"time\": \"").Append(now.ToString("yyyy-MM-dd HH:mm:ss", Inv)).Append("\",\n");
            sb.Append("  \"kind\": \"").Append(kind).Append("\",\n");
            sb.Append("  \"zone\": \"").Append(zone).Append("\",\n");
            sb.Append("  \"zoneId\": ").Append(zoneId).Append(",\n");
            sb.Append("  \"player\": \"").Append(Escape(player)).Append("\",\n");
            sb.Append("  \"rival\": \"").Append(Escape(rival)).Append("\",\n");
            sb.Append("  \"badges\": ").Append(badges).Append(",\n");
            sb.Append("  \"bytes\": ").Append(bytes).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        static string Escape(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c == '"' || c == '\\')
                    sb.Append('\\').Append(c);
                else if (c < ' ')
                    sb.Append("\\u").Append(((int)c).ToString("x4"));
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
#endif
