// BDSP play-test campaign (s33): editor-only check of the save-data marshal layout - not game code.
// BdspMarshalCheck.Run (-executeMethod): Marshal.SizeOf(PlayerWork.SaveData) against the shipped BINARY_SIZE of CURRENT_VERSION,
// OffsetOf the fields the game itself pins (GMS_WORK_ADDR = gmsdata, SAVEDATA_HASH_ADDR = saveDataHash), every top-level field offset,
// and a StructureToPtr / PtrToStructure round trip of an empty SaveData. Output ../work/s33/marshal_check.txt, then quits.
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BdspMarshalCheck
{
    [MenuItem("BDSP/Check save-data marshal layout")]
    public static void Run()
    {
        var sb = new StringBuilder();
        try
        {
            var t = typeof(PlayerWork.SaveData);
            int size = Marshal.SizeOf(t);
            int expect = PlayerWork.SaveData.BINARY_SIZE[PlayerWork.SaveData.CURRENT_VERSION - PlayerWork.SaveData.LAUNCH_VERSION];
            sb.AppendLine(string.Format("SizeOf(SaveData) = {0}  (shipped BINARY_SIZE[CURRENT] = {1}) {2}", size, expect, size == expect ? "OK" : "MISMATCH"));
            int gms = (int)Marshal.OffsetOf(t, "gmsdata");
            int hash = (int)Marshal.OffsetOf(t, "saveDataHash");
            sb.AppendLine(string.Format("OffsetOf(gmsdata) = {0} (GMS_WORK_ADDR {1}) {2}", gms, PlayerWork.SaveData.GMS_WORK_ADDR, gms == PlayerWork.SaveData.GMS_WORK_ADDR ? "OK" : "MISMATCH"));
            sb.AppendLine(string.Format("OffsetOf(saveDataHash) = {0} (SAVEDATA_HASH_ADDR {1}) {2}", hash, PlayerWork.SaveData.SAVEDATA_HASH_ADDR, hash == PlayerWork.SaveData.SAVEDATA_HASH_ADDR ? "OK" : "MISMATCH"));
            sb.AppendLine("fields:");
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                string fs;
                try { fs = f.FieldType.IsValueType && !f.FieldType.IsEnum && !f.FieldType.IsPrimitive ? Marshal.SizeOf(f.FieldType).ToString() : "-"; }
                catch (Exception e) { fs = "ERR " + e.Message; }
                sb.AppendLine(string.Format("  {0,8} {1} : {2} (sizeof {3})", (int)Marshal.OffsetOf(t, f.Name), f.Name, f.FieldType.Name, fs));
            }
            var empty = new PlayerWork.SaveData();
            IntPtr p = Marshal.AllocHGlobal(size);
            var zero = new byte[size];
            Marshal.Copy(zero, 0, p, size);
            Marshal.StructureToPtr(empty, p, false);
            var back = (PlayerWork.SaveData)Marshal.PtrToStructure(p, t);
            Marshal.FreeHGlobal(p);
            sb.AppendLine(string.Format("round trip of an empty SaveData: ok (intValues {0}, boolValues {1})", back.intValues != null ? back.intValues.Length : -1, back.boolValues != null ? back.boolValues.Length : -1));
        }
        catch (Exception e)
        {
            sb.AppendLine("EXCEPTION " + e);
        }
        sb.AppendLine("BDSP_MARSHAL_DONE");
        var dir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "work", "s33"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "marshal_check.txt"), sb.ToString());
        UnityEngine.Debug.Log("BDSP_MARSHAL_DONE");
        EditorApplication.Exit(0);
    }
}
