// BDSP iOS textures (2026-10-24): the ripped textures are NativeFormatImporter .asset files holding the shipped desktop-compressed data
// (DXT1 / DXT5 / BC4 / BC5 / BC7 Texture2D, BC6H Cubemap). Unity loads them as-is - nothing re-encodes them per platform - and
// BuildPipeline refuses them for iOS ("DXT1|BC1 compressed textures are not supported when publishing to iPhone"; the Mac decodes them,
// so the Mac build works). Editor only. NOT shipped code.
//
// IosConvertTextures (build_ios.py step `textures`) writes an ASTC copy of every such .asset into <work>/build/ios_textures/files/<the
// same Assets/... path>. Assets/ is only READ here. Each copy is the original YAML with four fields replaced (m_TextureFormat,
// m_CompleteImageSize, image data, _typelessdata): same object, fileID, GUID (.meta untouched), name, size, mip count, sampler and
// colour space. The data: every shipped mip level decoded by Unity's CPU decoder, then Unity's ASTC encoder - the shipped mips are
// kept, not regenerated (they are what the Mac shows).
//   DXT1 (4 bpp)                     -> ASTC 6x6  (3.56 bpp: a little smaller than the source); a texture whose 6x6 mip 0 is below
//                                       38 dB PSNR against its source goes to ASTC 4x4 instead (measured: 654 of 11,542 - fine
//                                       detail, and the normal maps the game stores as DXT1, were 26-38 dB at 6x6)
//   DXT5 / BC7 / BC5 / BC4 (8/4 bpp) -> ASTC 4x4  (8 bpp: alpha, normal maps and masks keep their quality; DXT5 / BC7 same size)
//   BC6H cubemaps (HDR)              -> ASTC HDR 4x4 (8 bpp as BC6H; Apple GPU family 6 = A13 and later)
// Channels are not swizzled: a DXT5nm / BC5 normal map keeps its layout, so the shaders read the same channels as the Mac's.
// Resumable: <work>/build/ios_textures/index.tsv has one line per converted file (source size + write time); an unchanged source
// with its copy present is skipped. The decode of each copy is compared with the decode of its source (mip 0 PSNR, in the index).
//
// build_ios.py puts the copies into Assets/ only around the iOS bundle / player steps (rename + APFS clone, setup/stages/ios_textures.py)
// and puts the originals back after the editor has exited; IosRequireNoDesktopTextures stops those steps in seconds when it did not.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace BdspWinBuild
{
    public static partial class WinBuild
    {
        static string IosTexRoot { get { return Path.Combine(Work, "ios_textures"); } }

        // source format -> iOS format (anything else in a native texture is already iOS-legal: RGBA32, RGB24, Alpha8, R8, RGBA64)
        static readonly Dictionary<int, TextureFormat> IosTexTarget = new Dictionary<int, TextureFormat>
        {
            { (int)TextureFormat.DXT1, TextureFormat.ASTC_6x6 },
            { (int)TextureFormat.DXT5, TextureFormat.ASTC_4x4 },
            { (int)TextureFormat.BC7, TextureFormat.ASTC_4x4 },
            { (int)TextureFormat.BC5, TextureFormat.ASTC_4x4 },
            { (int)TextureFormat.BC4, TextureFormat.ASTC_4x4 },
            { (int)TextureFormat.BC6H, TextureFormat.ASTC_HDR_4x4 },
        };
        const TextureCompressionQuality IosTexQuality = TextureCompressionQuality.Normal;
        const double IosTexFallbackPsnr = 38.0;     // ASTC 6x6 below this -> ASTC 4x4
        const double IosTexWarnPsnr = 30.0;

        [MenuItem("BDSP/iOS Build/1b Convert the DXT/BC textures to ASTC (into SetupWork; Assets is not changed)", false, 3)]
        public static void IosConvertTextures() { RunIos("textures", r => IosTextTextures(r) && ConvertIosTextures(r, IosTexRoot, null)); }

        // setup's d7 stage writes the native textures as BINARY serialized files (UnityPy; YAML hex would double the texel bytes). Unity
        // reads them in any mode but this converter edits YAML fields, and the reference project holds them as text (its editor upgrade
        // re-serialized them under ForceText). Before converting: let Unity re-serialize every binary Texture2D / Cubemap .asset as text
        // (same object, GUID, fileID and texels; only the representation changes) and check that each one now is text.
        static bool IosTextTextures(Report r)
        {
            var t0 = DateTime.Now;
            string assets = Path.Combine(Project, "Assets");
            var binary = new List<string>();
            var head = new byte[5];
            foreach (var f in Directory.EnumerateFiles(assets, "*.asset", SearchOption.AllDirectories))
            {
                string rel = "Assets" + f.Substring(assets.Length).Replace('\\', '/');
                if (rel.StartsWith("Assets/StreamingAssets/", StringComparison.Ordinal))
                    continue;
                int n;
                using (var s = File.OpenRead(f))
                    n = s.Read(head, 0, head.Length);
                if (n == 5 && Encoding.ASCII.GetString(head) == "%YAML")
                    continue;
                var type = AssetDatabase.GetMainAssetTypeAtPath(rel);
                if (type == typeof(Texture2D) || type == typeof(Cubemap))
                    binary.Add(rel);
            }
            if (binary.Count == 0)
            {
                r.Line(string.Format("binary native textures: 0 (all text; {0:F1} s scan)", (DateTime.Now - t0).TotalSeconds));
                return true;
            }
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                r.Error("binary native textures need ForceText serialization to become text; EditorSettings.serializationMode is " + EditorSettings.serializationMode);
                return false;
            }
            AssetDatabase.ForceReserializeAssets(binary, ForceReserializeAssetsOptions.ReserializeAssets);
            var still = binary.Where(rel =>
            {
                using (var s = File.OpenRead(Path.Combine(Project, rel)))
                    return s.Read(head, 0, head.Length) != 5 || Encoding.ASCII.GetString(head) != "%YAML";
            }).ToList();
            r.Line(string.Format("binary native textures re-serialized as text: {0} ({1} still binary) in {2:F1} min", binary.Count, still.Count,
                (DateTime.Now - t0).TotalMinutes));
            if (still.Count > 0)
            {
                r.Error(still.Count + " native textures stayed binary after ForceReserializeAssets; first: " + string.Join(", ", still.Take(5).ToArray()));
                return false;
            }
            return true;
        }

        // diagnostics: BDSP_IOS_TEX_PROBE = "Assets/a.asset|Assets/b.asset" -> only those, into <work>/build/ios_textures_probe
        public static void IosConvertTexturesProbe()
        {
            var only = (Environment.GetEnvironmentVariable("BDSP_IOS_TEX_PROBE") ?? "").Split('|').Where(x => x.Length > 0).ToArray();
            RunIos("textures_probe", r => ConvertIosTextures(r, Path.Combine(Work, "ios_textures_probe"), only));
        }

        // ---- which files ---------------------------------------------------------------------------------------------------------
        static readonly Regex IosTexHeader = new Regex(@"^--- !u!(28|89) &\d+\n(?:.*\n)*?  m_TextureFormat: (\d+)\n", RegexOptions.Compiled);

        // every native Texture2D / Cubemap .asset under Assets (not StreamingAssets) whose format iOS refuses: Assets-relative path -> format
        static SortedDictionary<string, int> IosDesktopTextures()
        {
            var found = new SortedDictionary<string, int>(StringComparer.Ordinal);
            string assets = Path.Combine(Project, "Assets");
            var buf = new byte[2048];
            foreach (var f in Directory.EnumerateFiles(assets, "*.asset", SearchOption.AllDirectories))
            {
                string rel = "Assets" + f.Substring(assets.Length).Replace('\\', '/');
                if (rel.StartsWith("Assets/StreamingAssets/", StringComparison.Ordinal))
                    continue;
                int n;
                using (var s = File.OpenRead(f))
                    n = s.Read(buf, 0, buf.Length);
                string head = Encoding.ASCII.GetString(buf, 0, n);
                int doc = head.IndexOf("\n--- !u!", StringComparison.Ordinal);
                if (doc < 0)
                    continue;
                var m = IosTexHeader.Match(head.Substring(doc + 1));
                int fmt;
                if (m.Success && int.TryParse(m.Groups[2].Value, out fmt) && IosTexTarget.ContainsKey(fmt))
                    found[rel] = fmt;
            }
            return found;
        }

        // the bundle / player steps: nothing iOS refuses may be left in Assets (build_ios.py swapped the ASTC copies in)
        static bool IosRequireNoDesktopTextures(Report r)
        {
            var t0 = DateTime.Now;
            var left = IosDesktopTextures();
            r.Line(string.Format("desktop-only (DXT/BC) native textures in Assets: {0} ({1:F1} s scan)", left.Count, (DateTime.Now - t0).TotalSeconds));
            if (left.Count == 0)
                return true;
            r.Error(left.Count + " DXT/BC textures are in Assets - iOS refuses them. Run this step through setup/build_ios.sh (it puts the ASTC copies " +
                    "from " + IosTexRoot + " in place and the originals back afterwards), or by hand: python setup/stages/ios_textures.py --swap-in ... --restore. First: " +
                    string.Join(", ", left.Keys.Take(5).ToArray()));
            return false;
        }

        // ---- the conversion --------------------------------------------------------------------------------------------------------
        sealed class IosTexEntry { public long Size, Ticks; public string Line; }

        static Dictionary<string, IosTexEntry> ReadIosTexIndex(string index)
        {
            var d = new Dictionary<string, IosTexEntry>(StringComparer.Ordinal);
            if (!File.Exists(index))
                return d;
            foreach (var line in File.ReadAllLines(index))
            {
                if (line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                var c = line.Split('\t');
                long size, ticks;
                if (c.Length >= 10 && long.TryParse(c[1], out size) && long.TryParse(c[2], out ticks))
                    d[c[0]] = new IosTexEntry { Size = size, Ticks = ticks, Line = line };    // the last line of a path wins
            }
            return d;
        }

        static bool ConvertIosTextures(Report r, string root, string[] only)
        {
            var t0 = DateTime.Now;
            string files = Path.Combine(root, "files"), index = Path.Combine(root, "index.tsv");
            Directory.CreateDirectory(files);
            var todo = IosDesktopTextures();
            if (only != null && only.Length > 0)
                todo = new SortedDictionary<string, int>(todo.Where(kv => only.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value), StringComparer.Ordinal);
            var byFmt = todo.GroupBy(kv => kv.Value).OrderBy(g => g.Key).Select(g => (TextureFormat)g.Key + " " + g.Count() + " -> " + IosTexTarget[g.Key]);
            r.Line(string.Format("DXT/BC native textures: {0} ({1}); scan {2:F1} s; output {3}", todo.Count, string.Join(", ", byFmt.ToArray()),
                (DateTime.Now - t0).TotalSeconds, files));

            var done = ReadIosTexIndex(index);
            int skipped = 0, converted = 0, failed = 0;
            long inBytes = 0, outBytes = 0;
            var worst = new List<KeyValuePair<double, string>>();
            var tStart = DateTime.Now;
            var lastProgress = DateTime.MinValue;
            using (var log = new StreamWriter(index, true, new UTF8Encoding(false)))
            {
                if (new FileInfo(index).Length == 0)
                    log.WriteLine("#bdsp-ios-textures\t1\tpath\tsrc_size\tsrc_mtime_ticks\tsrc_format\tios_format\twidth\theight\tmips\tios_bytes\tpsnr_mip0");
                int i = 0;
                foreach (var kv in todo)
                {
                    i++;
                    string src = Path.Combine(Project, kv.Key), dst = Path.Combine(files, kv.Key);
                    var fi = new FileInfo(src);
                    long ticks = fi.LastWriteTimeUtc.Ticks;
                    IosTexEntry e;
                    if (done.TryGetValue(kv.Key, out e) && e.Size == fi.Length && e.Ticks == ticks && File.Exists(dst) && !IosTexNeedsFallback(e.Line))
                    {
                        skipped++;
                        continue;
                    }
                    try
                    {
                        var c0 = DateTime.Now;
                        string line = ConvertIosTexture(kv.Key, src, dst, fi.Length, ticks);
                        log.WriteLine(line);
                        log.Flush();
                        converted++;
                        var cols = line.Split('\t');
                        inBytes += fi.Length;
                        outBytes += new FileInfo(dst).Length;
                        double psnr = double.Parse(cols[9], System.Globalization.CultureInfo.InvariantCulture);
                        worst.Add(new KeyValuePair<double, string>(psnr, kv.Key + " " + cols[3] + "->" + cols[4] + " " + cols[5] + "x" + cols[6]));
                        if (only != null && only.Length > 0)
                            r.Line(string.Format("  {0}: {1:F2} s, {2}", kv.Key, (DateTime.Now - c0).TotalSeconds, line));
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        r.Error("convert " + kv.Key + ": " + ex.Message);
                        if (failed >= 50)
                        {
                            r.Error("50 failures - stopping");
                            break;
                        }
                    }
                    if ((DateTime.Now - lastProgress).TotalSeconds >= 30)
                    {
                        lastProgress = DateTime.Now;
                        double el = (DateTime.Now - tStart).TotalSeconds;
                        double rate = converted / Math.Max(1.0, el);
                        File.WriteAllText(Path.Combine(root, "progress.txt"), string.Format(
                            "{0:yyyy-MM-dd HH:mm:ss} {1}/{2} (converted {3}, skipped {4}, failed {5}); {6:F2} files/s; ETA {7:F0} min\n", DateTime.Now, i, todo.Count,
                            converted, skipped, failed, rate, rate > 0 ? (todo.Count - i) / rate / 60 : 0));
                    }
                    if (converted % 200 == 199)
                        GC.Collect();
                }
            }
            r.Line(string.Format("converted {0}, up to date {1}, failed {2} in {3:F1} min; this run {4:F2} GB YAML in -> {5:F2} GB out",
                converted, skipped, failed, (DateTime.Now - t0).TotalMinutes, inBytes / 1e9, outBytes / 1e9));
            foreach (var w in worst.OrderBy(x => x.Key).Take(25))
                r.Line(string.Format("  lowest PSNR {0:F1} dB  {1}", w.Key, w.Value));
            int low = worst.Count(x => x.Key < IosTexWarnPsnr);
            if (low > 0)
                r.Warn(low + " textures below " + IosTexWarnPsnr + " dB mip-0 PSNR (listed above, lowest first)");
            // the whole set must be covered: every DXT/BC source has a fresh copy
            var fresh = ReadIosTexIndex(index);
            int missing = todo.Keys.Count(k => { IosTexEntry x; var f = new FileInfo(Path.Combine(Project, k)); return !fresh.TryGetValue(k, out x) || x.Size != f.Length || x.Ticks != f.LastWriteTimeUtc.Ticks || !File.Exists(Path.Combine(files, k)) || IosTexNeedsFallback(x.Line); });
            r.Line("index " + index + ": " + fresh.Count + " entries; sources without a fresh ASTC copy: " + missing);
            if (missing > 0)
                r.Error(missing + " DXT/BC textures have no up-to-date ASTC copy");
            return r.Errors == 0;
        }

        // an index line made before the 6x6 -> 4x4 fallback existed (or with another threshold) is converted again
        static bool IosTexNeedsFallback(string line)
        {
            var c = line.Split('\t');
            double psnr;
            return c[4] == TextureFormat.ASTC_6x6.ToString() && double.TryParse(c[9], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out psnr) && psnr < IosTexFallbackPsnr;
        }

        static readonly Regex IosTexField = new Regex(@"^  (m_Width|m_Height|m_CompleteImageSize|m_TextureFormat|m_MipCount|m_ImageCount|m_ColorSpace|image data): (\d+)$", RegexOptions.Multiline);

        // one file: parse, decode every level, encode, check, write <dst> (atomically); returns the index line
        static string ConvertIosTexture(string rel, string src, string dst, long srcSize, long srcTicks)
        {
            string text = File.ReadAllText(src, Encoding.UTF8);
            if (text.IndexOf("\n--- !u!", StringComparison.Ordinal) != text.LastIndexOf("\n--- !u!", StringComparison.Ordinal))
                throw new Exception("more than one object in the file");
            const string hexKey = "\n  _typelessdata: ";
            int hx = text.IndexOf(hexKey, StringComparison.Ordinal);
            if (hx < 0)
                throw new Exception("no _typelessdata");
            int h0 = hx + hexKey.Length, h1 = text.IndexOf('\n', h0);
            string head = text.Substring(0, hx + 1), tail = text.Substring(h1);
            var v = new Dictionary<string, int>();
            foreach (Match m in IosTexField.Matches(head))
            {
                if (v.ContainsKey(m.Groups[1].Value))
                    throw new Exception("field twice: " + m.Groups[1].Value);
                v[m.Groups[1].Value] = int.Parse(m.Groups[2].Value);
            }
            foreach (var k in new[] { "m_Width", "m_Height", "m_CompleteImageSize", "m_TextureFormat", "m_MipCount", "m_ImageCount", "m_ColorSpace", "image data" })
                if (!v.ContainsKey(k))
                    throw new Exception("no " + k);
            // (the d7 cubemaps were written in the 2019.4 layout: no serializedVersion line in m_StreamData)
            if (!Regex.IsMatch(tail, @"^  m_StreamData:\n(?:    serializedVersion: \d+\n)?    offset: 0\n    size: 0\n", RegexOptions.Multiline))
                throw new Exception("the data is not inline (m_StreamData size != 0)");
            bool cube = text.Contains("\n--- !u!89 ");
            int w = v["m_Width"], h = v["m_Height"], fmt = v["m_TextureFormat"], mips = v["m_MipCount"], images = v["m_ImageCount"];
            bool linear = v["m_ColorSpace"] == 0;
            if ((h1 - h0) != v["image data"] * 2 || v["image data"] != v["m_CompleteImageSize"] * images || images != (cube ? 6 : 1))
                throw new Exception(string.Format("sizes do not add up: hex {0}, image data {1}, complete {2} x {3}", h1 - h0, v["image data"], v["m_CompleteImageSize"], images));
            byte[] raw = HexToBytes(text, h0, h1 - h0);
            var target = IosTexTarget[fmt];

            byte[] ios;
            double psnr;
            if (cube)
                ios = EncodeIosCube(raw, w, (TextureFormat)fmt, mips, v["m_CompleteImageSize"], target, out psnr);
            else
            {
                ios = EncodeIosTexture2D(raw, w, h, (TextureFormat)fmt, mips, linear, target, out psnr);
                if (target == TextureFormat.ASTC_6x6 && psnr < IosTexFallbackPsnr)
                {
                    target = TextureFormat.ASTC_4x4;
                    ios = EncodeIosTexture2D(raw, w, h, (TextureFormat)fmt, mips, linear, target, out psnr);
                }
            }

            long perImage = IosAstcBytes(target, w, h, mips);
            if (ios.Length != perImage * images)
                throw new Exception(string.Format("encoder gave {0} bytes, {1} x {2} expected for {3} {4}x{5} {6} mips", ios.Length, perImage, images, target, w, h, mips));

            string outHead = SetIosTexField(SetIosTexField(SetIosTexField(head, "m_TextureFormat", ((int)target).ToString()),
                "m_CompleteImageSize", perImage.ToString()), "image data", ios.Length.ToString());
            var sb = new StringBuilder(outHead.Length + ios.Length * 2 + tail.Length + 32);
            sb.Append(outHead).Append("  _typelessdata: ");
            AppendHex(sb, ios);
            sb.Append(tail);
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            string tmp = dst + ".tmp";
            File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
            if (File.Exists(dst))
                File.Delete(dst);
            File.Move(tmp, dst);
            return string.Join("\t", new[] { rel, srcSize.ToString(), srcTicks.ToString(), ((TextureFormat)fmt).ToString(), target.ToString(), w.ToString(), h.ToString(),
                mips.ToString(), ios.Length.ToString(), psnr.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) });
        }

        static byte[] EncodeIosTexture2D(byte[] raw, int w, int h, TextureFormat fmt, int mips, bool linear, TextureFormat target, out double psnr)
        {
            Texture2D src = null, dst = null, back = null;
            try
            {
                src = new Texture2D(w, h, fmt, mips, linear);
                src.LoadRawTextureData(raw);
                dst = new Texture2D(w, h, TextureFormat.RGBA32, mips, linear);
                Color32[] mip0 = null;
                for (int m = 0; m < mips; m++)
                {
                    var px = IosMipTexels(src.GetPixels32(m), fmt, w, h, m);  // Unity's CPU decoder: the texels the Mac's GPU samples
                    if (m == 0)
                        mip0 = px;
                    dst.SetPixels32(px, m);
                }
                dst.Apply(false, false);
                EditorUtility.CompressTexture(dst, target, IosTexQuality);
                if (dst.format != target)
                    throw new Exception("CompressTexture left the texture " + dst.format + " (wanted " + target + ")");
                byte[] ios = dst.GetRawTextureData();
                back = new Texture2D(w, h, target, mips, linear);
                back.LoadRawTextureData(ios);
                psnr = IosPsnr(mip0, IosMipTexels(back.GetPixels32(0), target, w, h, 0));
                return ios;
            }
            finally
            {
                if (src != null) UnityEngine.Object.DestroyImmediate(src);
                if (dst != null) UnityEngine.Object.DestroyImmediate(dst);
                if (back != null) UnityEngine.Object.DestroyImmediate(back);
            }
        }

        // the serialized layout is face-major: face 0 mips 0..n, face 1 mips 0..n, ... (m_CompleteImageSize = one face)
        static byte[] EncodeIosCube(byte[] raw, int w, TextureFormat fmt, int mips, int perFace, TextureFormat target, out double psnr)
        {
            Cubemap src = null, dst = null, back = null;
            try
            {
                src = new Cubemap(w, fmt, mips);
                dst = new Cubemap(w, TextureFormat.RGBAHalf, mips);
                var faceMip0 = new Color[6][];
                for (int f = 0; f < 6; f++)
                {
                    int off = f * perFace;
                    for (int m = 0; m < mips; m++)
                    {
                        int mw = Math.Max(1, w >> m);
                        int len = (int)IosBlockBytes(fmt, mw, mw);
                        src.SetPixelData(raw, m, (CubemapFace)f, off);
                        off += len;
                    }
                }
                for (int f = 0; f < 6; f++)
                    for (int m = 0; m < mips; m++)
                    {
                        var px = src.GetPixels((CubemapFace)f, m);
                        if (m == 0)
                            faceMip0[f] = px;
                        dst.SetPixels(px, (CubemapFace)f, m);
                    }
                dst.Apply(false, false);
                EditorUtility.CompressCubemapTexture(dst, target, IosTexQuality);
                if (dst.format != target)
                    throw new Exception("CompressCubemapTexture left the cubemap " + dst.format + " (wanted " + target + ")");
                var outBytes = new List<byte>();
                for (int f = 0; f < 6; f++)
                    for (int m = 0; m < mips; m++)
                        outBytes.AddRange(dst.GetPixelData<byte>(m, (CubemapFace)f).ToArray());
                byte[] ios = outBytes.ToArray();
                back = new Cubemap(w, target, mips);
                int o = 0;
                for (int f = 0; f < 6; f++)
                    for (int m = 0; m < mips; m++)
                    {
                        back.SetPixelData(ios, m, (CubemapFace)f, o);
                        o += (int)IosBlockBytes(target, Math.Max(1, w >> m), Math.Max(1, w >> m));
                    }
                // HDR: compared on the 0..1 range (clamped), all six faces
                double sum = 0;
                long n = 0;
                for (int f = 0; f < 6; f++)
                {
                    var a = faceMip0[f];
                    var b = back.GetPixels((CubemapFace)f, 0);
                    for (int i = 0; i < a.Length; i++)
                    {
                        sum += Sq(Mathf.Clamp01(a[i].r) - Mathf.Clamp01(b[i].r)) + Sq(Mathf.Clamp01(a[i].g) - Mathf.Clamp01(b[i].g)) + Sq(Mathf.Clamp01(a[i].b) - Mathf.Clamp01(b[i].b));
                        n += 3;
                    }
                }
                psnr = sum <= 0 ? 99.0 : 10 * Math.Log10(1.0 / (sum / n));
                return ios;
            }
            finally
            {
                if (src != null) UnityEngine.Object.DestroyImmediate(src);
                if (dst != null) UnityEngine.Object.DestroyImmediate(dst);
                if (back != null) UnityEngine.Object.DestroyImmediate(back);
            }
        }

        // GetPixels32 of a block-compressed mip smaller than a block (or not a multiple of it) returns the whole decoded blocks: keep the
        // mip's own w x h texels (rows bottom-up from the start of the data, as the blocks are)
        static Color32[] IosMipTexels(Color32[] px, TextureFormat fmt, int w, int h, int m)
        {
            int mw = Math.Max(1, w >> m), mh = Math.Max(1, h >> m);
            if (px.Length == mw * mh)
                return px;
            int bw, bh, bytes;
            IosBlock(fmt, out bw, out bh, out bytes);
            int aw = (mw + bw - 1) / bw * bw, ah = (mh + bh - 1) / bh * bh;
            if (px.Length != aw * ah)
                throw new Exception(string.Format("GetPixels32 mip {0} ({1}x{2}) gave {3} texels, neither {4} nor the block-padded {5}", m, mw, mh, px.Length, mw * mh, aw * ah));
            var o = new Color32[mw * mh];
            for (int y = 0; y < mh; y++)
                Array.Copy(px, y * aw, o, y * mw, mw);
            return o;
        }

        static double Sq(double x) { return x * x; }

        static double IosPsnr(Color32[] a, Color32[] b)
        {
            if (a.Length != b.Length)
                throw new Exception("PSNR: " + a.Length + " vs " + b.Length + " texels");
            double sum = 0;
            for (int i = 0; i < a.Length; i++)
                sum += Sq(a[i].r - b[i].r) + Sq(a[i].g - b[i].g) + Sq(a[i].b - b[i].b) + Sq(a[i].a - b[i].a);
            double mse = sum / (4.0 * a.Length);
            return mse <= 0 ? 99.0 : 10 * Math.Log10(255.0 * 255.0 / mse);
        }

        static void IosBlock(TextureFormat f, out int bw, out int bh, out int bytes)
        {
            switch (f)
            {
                case TextureFormat.DXT1: case TextureFormat.BC4: bw = bh = 4; bytes = 8; return;
                case TextureFormat.DXT5: case TextureFormat.BC5: case TextureFormat.BC6H: case TextureFormat.BC7:
                case TextureFormat.ASTC_4x4: case TextureFormat.ASTC_HDR_4x4: bw = bh = 4; bytes = 16; return;
                case TextureFormat.ASTC_5x5: bw = bh = 5; bytes = 16; return;
                case TextureFormat.ASTC_6x6: bw = bh = 6; bytes = 16; return;
                case TextureFormat.ASTC_8x8: bw = bh = 8; bytes = 16; return;
            }
            throw new Exception("no block size for " + f);
        }

        static long IosBlockBytes(TextureFormat f, int w, int h)
        {
            int bw, bh, bytes;
            IosBlock(f, out bw, out bh, out bytes);
            return (long)((w + bw - 1) / bw) * ((h + bh - 1) / bh) * bytes;
        }

        static long IosAstcBytes(TextureFormat f, int w, int h, int mips)
        {
            long n = 0;
            for (int m = 0; m < mips; m++)
                n += IosBlockBytes(f, Math.Max(1, w >> m), Math.Max(1, h >> m));
            return n;
        }

        static string SetIosTexField(string head, string key, string value)
        {
            var re = new Regex("^  " + Regex.Escape(key) + @": \d+$", RegexOptions.Multiline);
            if (re.Matches(head).Count != 1)
                throw new Exception("not exactly one " + key);
            return re.Replace(head, "  " + key + ": " + value);
        }

        static byte[] HexToBytes(string s, int start, int len)
        {
            if ((len & 1) != 0)
                throw new Exception("odd hex length");
            var b = new byte[len / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = (byte)((HexNibble(s[start + 2 * i]) << 4) | HexNibble(s[start + 2 * i + 1]));
            return b;
        }

        static int HexNibble(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            throw new Exception("not hex: " + c);
        }

        static void AppendHex(StringBuilder sb, byte[] b)
        {
            const string digits = "0123456789abcdef";
            foreach (var x in b)
                sb.Append(digits[x >> 4]).Append(digits[x & 15]);
        }
    }
}
