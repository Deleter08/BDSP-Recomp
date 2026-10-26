// tools/winbuild/DprManifestDump.cs - read-only dump of an AssetAssistant manifest (Dpr.bin = .NET BinaryFormatter of
// SmartPoint.AssetAssistant.AssetBundleDownloadManifest, shipped assembly name "AssetAssistant, Version=0.0.0.0").
// Mirror types carry the shipped field names; the binder admits ONLY these three types (anything else in the stream = refusal).
// Build:  C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /out:tools\winbuild\bin\DprManifestDump.exe tools\winbuild\DprManifestDump.cs
// Run:    tools\winbuild\bin\DprManifestDump.exe <Dpr.bin> <out.tsv>
//   out.tsv: header lines starting with '#', then one row per record:
//   projectName \t assetBundleName \t isStreamingSceneAssetBundle \t size \t hash(32 hex) \t lastWriteTimeUtc(ticks) \t deps(';') \t assetPaths(';')
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace SmartPoint.AssetAssistant
{
    [Serializable]
    public struct RecordedHash { public uint u0, u1, u2, u3; }

    [Serializable]
    public class AssetBundleRecord
    {
        public string projectName;
        public string assetBundleName;
        public RecordedHash hash;
        public DateTime lastWriteTime;
        public bool isStreamingSceneAssetBundle;
        public string[] allDependencies;
        public string[] assetPaths;
        public long size;
    }

    [Serializable]
    public class AssetBundleDownloadManifest
    {
        public int _version;
        public string _projectName;
        public AssetBundleRecord[] _records;
        public string[] _assetBundleNamesWithVariant;
    }
}

namespace BdspWinBuild
{
    using SmartPoint.AssetAssistant;

    sealed class StrictBinder : SerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            switch (typeName)
            {
                case "SmartPoint.AssetAssistant.AssetBundleDownloadManifest": return typeof(AssetBundleDownloadManifest);
                case "SmartPoint.AssetAssistant.AssetBundleRecord": return typeof(AssetBundleRecord);
                case "SmartPoint.AssetAssistant.RecordedHash": return typeof(RecordedHash);
            }
            if (assemblyName.StartsWith("mscorlib,", StringComparison.Ordinal) &&
                (typeName == "System.String" || typeName == "System.DateTime"))
                return Type.GetType(typeName, true);
            throw new SerializationException("refused type: " + typeName + ", " + assemblyName);
        }
    }

    static class Program
    {
        static string Hex(RecordedHash h)
        {
            return h.u0.ToString("x8") + h.u1.ToString("x8") + h.u2.ToString("x8") + h.u3.ToString("x8");
        }

        static string Join(string[] a)
        {
            return a == null ? "<null>" : string.Join(";", a);
        }

        static int Main(string[] args)
        {
            if (args.Length != 2) { Console.Error.WriteLine("usage: DprManifestDump <Dpr.bin> <out.tsv>"); return 2; }
            AssetBundleDownloadManifest m;
            using (var fs = File.OpenRead(args[0]))
            {
                var bf = new BinaryFormatter();
                bf.Binder = new StrictBinder();
                m = (AssetBundleDownloadManifest)bf.Deserialize(fs);
            }
            var sb = new StringBuilder();
            sb.Append("#version\t").Append(m._version).Append('\n');
            sb.Append("#projectName\t").Append(m._projectName).Append('\n');
            sb.Append("#records\t").Append(m._records == null ? -1 : m._records.Length).Append('\n');
            sb.Append("#variants\t").Append(Join(m._assetBundleNamesWithVariant)).Append('\n');
            foreach (var r in m._records)
            {
                sb.Append(r.projectName).Append('\t').Append(r.assetBundleName).Append('\t')
                  .Append(r.isStreamingSceneAssetBundle ? 1 : 0).Append('\t').Append(r.size).Append('\t')
                  .Append(Hex(r.hash)).Append('\t').Append(r.lastWriteTime.Ticks).Append('\t')
                  .Append(Join(r.allDependencies)).Append('\t').Append(Join(r.assetPaths)).Append('\n');
            }
            File.WriteAllText(args[1], sb.ToString(), new UTF8Encoding(false));
            Console.WriteLine("version {0} project '{1}' records {2} variants {3}", m._version, m._projectName,
                m._records.Length, m._assetBundleNamesWithVariant == null ? -1 : m._assetBundleNamesWithVariant.Length);
            return 0;
        }
    }
}
