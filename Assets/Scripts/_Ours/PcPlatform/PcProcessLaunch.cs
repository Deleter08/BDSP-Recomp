#if !SWITCH && !UNITY_EDITOR && UNITY_STANDALONE_WIN
// PC platform layer (Windows PLAYER only): start a process through kernel32 CreateProcessW (HANDOFF "s38b"). NOT shipped code.
// System.Diagnostics.Process.Start is NOT implemented by the IL2CPP of Unity 2019.4.27f1 (libil2cpp icalls/System/System.Diagnostics/
// Process.cpp: CreateProcess_internal / ShellExecuteEx_internal = IL2CPP_NOT_IMPLEMENTED_ICALL), so PcProgram.Restart (the console's
// nn::oe::RestartProgram = relaunch the title) uses this. No UnityEngine dependency (exercised by tools/winbuild/il2cpp_smoke).
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PcPlatform
{
    public static class PcProcessLaunch
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct STARTUPINFO
        {
            public int cb;
            public string lpReserved, lpDesktop, lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION
        {
            public IntPtr hProcess, hThread;
            public int dwProcessId, dwThreadId;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateProcessW(string applicationName, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes,
            bool inheritHandles, uint creationFlags, IntPtr environment, string currentDirectory, ref STARTUPINFO startupInfo,
            out PROCESS_INFORMATION processInformation);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCommandLineW();

        // Starts commandLine (executable + arguments, Windows quoting) in workingDirectory; returns the new process id, 0 on failure.
        public static int Launch(string commandLine, string workingDirectory, out int win32Error)
        {
            var si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
            PROCESS_INFORMATION pi;
            var buffer = new StringBuilder(commandLine, commandLine.Length + 1); // CreateProcessW may write into the command line
            bool ok = CreateProcessW(null, buffer, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, workingDirectory, ref si, out pi);
            win32Error = ok ? 0 : Marshal.GetLastWin32Error();
            if (!ok)
                return 0;
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            return pi.dwProcessId;
        }

        // The running player again, with its own command line (executable + arguments as it was started).
        public static int Relaunch(out int win32Error)
        {
            return Launch(Marshal.PtrToStringUni(GetCommandLineW()), Directory.GetCurrentDirectory(), out win32Error);
        }
    }
}
#endif
