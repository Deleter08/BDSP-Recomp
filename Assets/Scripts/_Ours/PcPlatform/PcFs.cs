#if !SWITCH
// PC platform layer: nn::fs on the host file system (HANDOFF §11.7 "Saves"). NOT shipped code.
//
// Mount names map to host directories. A save mount holds the game's files byte-for-byte (SaveData.bin / Backup.bin as
// Dpr.NX.SaveSystem writes them), so moving a save to Ryujinx is a plain copy into its
// bis/user/save/<saveDataId>/0/ folder, and back.
//   save data   : <persistentDataPath>/nn/save/<application id X16>/<uid0 X16><uid1 X16>/
// Result values follow LibHac (Ryujinx's fs implementation = the play-test reference); the ErrorRange getters in nn.fs that the
// shipped nn.dll defines agree with them (PathNotFound 2-1, PathAlreadyExists 2-2, MountNameAlreadyExists 2-60, TargetNotFound 2-1002).
// Writes go straight to the host file; Commit only flushes (the console's save journal is not modelled).
using System;
using System.Collections.Generic;
using System.IO;

namespace PcPlatform
{
    public static class PcFs
    {
        const int Module = 2;
        public static readonly nn.Result Success = new nn.Result();
        public static readonly nn.Result PathNotFound = new nn.Result(Module, 1);
        public static readonly nn.Result PathAlreadyExists = new nn.Result(Module, 2);
        public static readonly nn.Result DirectoryNotEmpty = new nn.Result(Module, 8);
        public static readonly nn.Result MountNameAlreadyExists = new nn.Result(Module, 60);
        public static readonly nn.Result TargetNotFound = new nn.Result(Module, 1002);
        public static readonly nn.Result OutOfRange = new nn.Result(Module, 3005);
        public static readonly nn.Result FileExtensionWithoutOpenModeAllowAppend = new nn.Result(Module, 6201);
        public static readonly nn.Result ReadUnpermitted = new nn.Result(Module, 6202);
        public static readonly nn.Result WriteUnpermitted = new nn.Result(Module, 6203);
        public static readonly nn.Result InvalidHandle = new nn.Result(Module, 6063);
        public static readonly nn.Result InvalidPath = new nn.Result(Module, 6001);
        public static readonly nn.Result NotMounted = new nn.Result(Module, 6905);
        public static readonly nn.Result UnexpectedHostError = new nn.Result(Module, 6099);

        class Mount
        {
            public string root;
            public bool readOnly;
            public bool save;   // a save-data mount: its commits become play-test checkpoints (PcSaveCheckpoint)
        }

        class FileEntry
        {
            public FileStream stream;
            public nn.fs.OpenFileMode mode;
        }

        class OpenDir
        {
            public string path;
            public nn.fs.OpenDirectoryMode mode;
            public string[] entries;
            public int next;
        }

        static readonly object gate = new object();
        static readonly Dictionary<string, Mount> mounts = new Dictionary<string, Mount>(StringComparer.Ordinal);
        static readonly Dictionary<long, FileEntry> files = new Dictionary<long, FileEntry>();
        static readonly Dictionary<long, OpenDir> dirs = new Dictionary<long, OpenDir>();
        static long nextHandle = 1;

        // ---- save data -------------------------------------------------------------------------------------------------

        public static string SaveRoot(ulong applicationId, nn.account.Uid user)
        {
            return Path.Combine(PcConfig.Root, "save", applicationId.ToString("X16"), user._data0.ToString("X16") + user._data1.ToString("X16"));
        }

        public static nn.Result MountSave(string name, ulong applicationId, nn.account.Uid user, bool readOnly, bool create)
        {
            string root = SaveRoot(applicationId, user);
            if (!Directory.Exists(root))
            {
                if (!create)
                    return TargetNotFound;
                Directory.CreateDirectory(root);
            }
            var result = AddMount(name, root, readOnly);
            lock (gate)
            {
                Mount m;
                if (mounts.TryGetValue(name, out m) && m.root == root)
                    m.save = true;
            }
            return result;
        }

        public static bool SaveExists(ulong applicationId, nn.account.Uid user)
        {
            return Directory.Exists(SaveRoot(applicationId, user));
        }

        public static nn.Result EnsureSave(ulong applicationId, nn.account.Uid user)
        {
            Directory.CreateDirectory(SaveRoot(applicationId, user));
            return Success;
        }

        public static nn.Result AddMount(string name, string root, bool readOnly)
        {
            lock (gate)
            {
                if (mounts.ContainsKey(name))
                    return MountNameAlreadyExists;
                mounts[name] = new Mount { root = root, readOnly = readOnly };
            }
            PcConfig.Log("fs mount " + name + ": -> " + root + (readOnly ? " (read-only)" : ""));
            return Success;
        }

        public static void Unmount(string name)
        {
            lock (gate)
                mounts.Remove(name);
        }

        public static nn.Result Commit(string name)
        {
            Mount m;
            lock (gate)
            {
                if (!mounts.TryGetValue(name, out m))
                    return NotMounted;
                foreach (var f in files.Values)
                    f.stream.Flush(true);
            }
            // play-test checkpoints (s34): a committed save mount is copied aside - SaveSystem.Save commits once, after both files closed
            if (m.save && !m.readOnly)
                PcSaveCheckpoint.Snapshot(m.root);
            return Success;
        }

        // ---- paths ---------------------------------------------------------------------------------------------------

        // "SaveData:/SaveData.bin" -> host path. Fails with NotMounted / InvalidPath.
        static nn.Result Resolve(string path, out string host, out bool readOnly)
        {
            host = null;
            readOnly = false;
            if (path == null)
                return InvalidPath;
            int colon = path.IndexOf(':');
            if (colon <= 0)
                return InvalidPath;
            Mount m;
            lock (gate)
            {
                if (!mounts.TryGetValue(path.Substring(0, colon), out m))
                    return NotMounted;
            }
            string rel = path.Substring(colon + 1).TrimStart('/');
            if (rel.Contains(".."))
                return InvalidPath;
            host = rel.Length == 0 ? m.root : Path.Combine(m.root, rel.Replace('/', Path.DirectorySeparatorChar));
            readOnly = m.readOnly;
            return Success;
        }

        public static nn.Result GetEntryType(ref nn.fs.EntryType type, string path)
        {
            var r = Resolve(path, out string host, out _);
            if (!r.IsSuccess())
                return r;
            if (Directory.Exists(host))
            {
                type = nn.fs.EntryType.Directory;
                return Success;
            }
            if (File.Exists(host))
            {
                type = nn.fs.EntryType.File;
                return Success;
            }
            return PathNotFound;
        }

        public static nn.Result GetFreeSpaceSize(ref long size, string path)
        {
            var r = Resolve(path, out string host, out _);
            if (!r.IsSuccess())
                return r;
            try
            {
                size = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(host))).AvailableFreeSpace;
                return Success;
            }
            catch (Exception)
            {
                return UnexpectedHostError;
            }
        }

        // ---- files ---------------------------------------------------------------------------------------------------

        public static nn.Result CreateFile(string path, long size)
        {
            var r = Resolve(path, out string host, out bool ro);
            if (!r.IsSuccess())
                return r;
            if (ro)
                return WriteUnpermitted;
            if (File.Exists(host) || Directory.Exists(host))
                return PathAlreadyExists;
            if (!Directory.Exists(Path.GetDirectoryName(host)))
                return PathNotFound;
            using (var fs = new FileStream(host, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite))
                fs.SetLength(size);
            return Success;
        }

        public static nn.Result DeleteFile(string path)
        {
            var r = Resolve(path, out string host, out bool ro);
            if (!r.IsSuccess())
                return r;
            if (ro)
                return WriteUnpermitted;
            if (!File.Exists(host))
                return PathNotFound;
            File.Delete(host);
            return Success;
        }

        public static nn.Result RenameFile(string from, string to)
        {
            var r = Resolve(from, out string a, out bool ro);
            if (!r.IsSuccess())
                return r;
            r = Resolve(to, out string b, out _);
            if (!r.IsSuccess())
                return r;
            if (ro)
                return WriteUnpermitted;
            if (!File.Exists(a))
                return PathNotFound;
            if (File.Exists(b) || Directory.Exists(b))
                return PathAlreadyExists;
            File.Move(a, b);
            return Success;
        }

        public static nn.Result OpenFile(ref nn.fs.FileHandle handle, string path, nn.fs.OpenFileMode mode)
        {
            var r = Resolve(path, out string host, out bool ro);
            if (!r.IsSuccess())
                return r;
            if (ro && (mode & nn.fs.OpenFileMode.Write) != 0)
                return WriteUnpermitted;
            if (!File.Exists(host))
                return PathNotFound;
            var access = (mode & nn.fs.OpenFileMode.Write) != 0 ? FileAccess.ReadWrite : FileAccess.Read;
            var stream = new FileStream(host, FileMode.Open, access, FileShare.ReadWrite);
            lock (gate)
            {
                long id = nextHandle++;
                files[id] = new FileEntry { stream = stream, mode = mode };
                handle.handle = new IntPtr(id);
            }
            return Success;
        }

        static FileEntry Get(nn.fs.FileHandle handle)
        {
            lock (gate)
            {
                files.TryGetValue(handle.handle.ToInt64(), out FileEntry f);
                return f;
            }
        }

        public static nn.Result Read(ref long read, nn.fs.FileHandle handle, long offset, byte[] buffer, long size)
        {
            var f = Get(handle);
            if (f == null)
                return InvalidHandle;
            if ((f.mode & nn.fs.OpenFileMode.Read) == 0)
                return ReadUnpermitted;
            if (offset < 0 || size < 0 || size > buffer.Length)
                return OutOfRange;
            lock (f)
            {
                long length = f.stream.Length;
                if (offset > length)
                    return OutOfRange;
                long n = Math.Min(size, length - offset);
                f.stream.Position = offset;
                long done = 0;
                while (done < n)
                {
                    int got = f.stream.Read(buffer, (int)done, (int)(n - done));
                    if (got <= 0)
                        break;
                    done += got;
                }
                read = done;
            }
            return Success;
        }

        // The overload without a read count fails unless the whole range was read (LibHac FileSystemClient.ReadFile).
        public static nn.Result ReadExact(nn.fs.FileHandle handle, long offset, byte[] buffer, long size)
        {
            long read = 0;
            var r = Read(ref read, handle, offset, buffer, size);
            if (!r.IsSuccess())
                return r;
            return read == size ? Success : OutOfRange;
        }

        public static nn.Result Write(nn.fs.FileHandle handle, long offset, byte[] buffer, long size, nn.fs.WriteOption option)
        {
            var f = Get(handle);
            if (f == null)
                return InvalidHandle;
            if ((f.mode & nn.fs.OpenFileMode.Write) == 0)
                return WriteUnpermitted;
            if (offset < 0 || size < 0 || size > buffer.Length)
                return OutOfRange;
            lock (f)
            {
                if (offset + size > f.stream.Length && (f.mode & nn.fs.OpenFileMode.AllowAppend) == 0)
                    return FileExtensionWithoutOpenModeAllowAppend;
                f.stream.Position = offset;
                f.stream.Write(buffer, 0, (int)size);
                if ((option & nn.fs.WriteOption.Flush) != 0)
                    f.stream.Flush(true);
            }
            return Success;
        }

        public static nn.Result Flush(nn.fs.FileHandle handle)
        {
            var f = Get(handle);
            if (f == null)
                return InvalidHandle;
            lock (f)
                f.stream.Flush(true);
            return Success;
        }

        public static nn.Result SetSize(nn.fs.FileHandle handle, long size)
        {
            var f = Get(handle);
            if (f == null)
                return InvalidHandle;
            if ((f.mode & nn.fs.OpenFileMode.Write) == 0)
                return WriteUnpermitted;
            lock (f)
                f.stream.SetLength(size);
            return Success;
        }

        public static nn.Result GetSize(ref long size, nn.fs.FileHandle handle)
        {
            var f = Get(handle);
            if (f == null)
                return InvalidHandle;
            lock (f)
                size = f.stream.Length;
            return Success;
        }

        public static nn.fs.OpenFileMode GetOpenMode(nn.fs.FileHandle handle)
        {
            var f = Get(handle);
            return f == null ? 0 : f.mode;
        }

        public static void Close(nn.fs.FileHandle handle)
        {
            FileEntry f;
            lock (gate)
            {
                long id = handle.handle.ToInt64();
                if (!files.TryGetValue(id, out f))
                    return;
                files.Remove(id);
            }
            lock (f)
                f.stream.Dispose();
        }

        // ---- directories ---------------------------------------------------------------------------------------------

        public static nn.Result CreateDirectory(string path)
        {
            var r = Resolve(path, out string host, out bool ro);
            if (!r.IsSuccess())
                return r;
            if (ro)
                return WriteUnpermitted;
            if (File.Exists(host) || Directory.Exists(host))
                return PathAlreadyExists;
            if (!Directory.Exists(Path.GetDirectoryName(host)))
                return PathNotFound;
            Directory.CreateDirectory(host);
            return Success;
        }

        public static nn.Result DeleteDirectory(string path, bool recursive, bool cleanOnly)
        {
            var r = Resolve(path, out string host, out bool ro);
            if (!r.IsSuccess())
                return r;
            if (ro)
                return WriteUnpermitted;
            if (!Directory.Exists(host))
                return PathNotFound;
            if (cleanOnly)
            {
                foreach (var d in Directory.GetDirectories(host))
                    Directory.Delete(d, true);
                foreach (var f in Directory.GetFiles(host))
                    File.Delete(f);
                return Success;
            }
            if (!recursive && Directory.EnumerateFileSystemEntries(host).GetEnumerator().MoveNext())
                return DirectoryNotEmpty;
            Directory.Delete(host, recursive);
            return Success;
        }

        public static nn.Result RenameDirectory(string from, string to)
        {
            var r = Resolve(from, out string a, out bool ro);
            if (!r.IsSuccess())
                return r;
            r = Resolve(to, out string b, out _);
            if (!r.IsSuccess())
                return r;
            if (ro)
                return WriteUnpermitted;
            if (!Directory.Exists(a))
                return PathNotFound;
            if (File.Exists(b) || Directory.Exists(b))
                return PathAlreadyExists;
            Directory.Move(a, b);
            return Success;
        }

        public static nn.Result OpenDirectory(ref nn.fs.DirectoryHandle handle, string path, nn.fs.OpenDirectoryMode mode)
        {
            var r = Resolve(path, out string host, out _);
            if (!r.IsSuccess())
                return r;
            if (!Directory.Exists(host))
                return PathNotFound;
            var list = new List<string>();
            if ((mode & nn.fs.OpenDirectoryMode.Directory) != 0)
                list.AddRange(Directory.GetDirectories(host));
            if ((mode & nn.fs.OpenDirectoryMode.File) != 0)
                list.AddRange(Directory.GetFiles(host));
            lock (gate)
            {
                long id = nextHandle++;
                dirs[id] = new OpenDir { path = host, mode = mode, entries = list.ToArray() };
                handle.handle = new IntPtr(id);
            }
            return Success;
        }

        public static nn.Result ReadDirectory(ref long count, nn.fs.DirectoryEntry[] buffer, nn.fs.DirectoryHandle handle, long max)
        {
            OpenDir d;
            lock (gate)
                dirs.TryGetValue(handle.handle.ToInt64(), out d);
            if (d == null)
                return InvalidHandle;
            long n = 0;
            while (n < max && n < buffer.Length && d.next < d.entries.Length)
            {
                string e = d.entries[d.next++];
                bool isDir = Directory.Exists(e);
                buffer[n++] = nn.fs.DirectoryEntry.PcMake(Path.GetFileName(e), isDir ? nn.fs.EntryType.Directory : nn.fs.EntryType.File,
                    isDir ? 0 : new FileInfo(e).Length);
            }
            count = n;
            return Success;
        }

        public static nn.Result GetDirectoryEntryCount(ref long count, nn.fs.DirectoryHandle handle)
        {
            OpenDir d;
            lock (gate)
                dirs.TryGetValue(handle.handle.ToInt64(), out d);
            if (d == null)
                return InvalidHandle;
            count = d.entries.Length;
            return Success;
        }

        public static void CloseDirectory(nn.fs.DirectoryHandle handle)
        {
            lock (gate)
                dirs.Remove(handle.handle.ToInt64());
        }
    }
}
#endif
