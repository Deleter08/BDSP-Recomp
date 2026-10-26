#if !SWITCH
// PC platform layer: nn::time ClockSnapshot (208 bytes) for Dpr.NX.ClockSnapshot's game natives. NOT shipped code.
//   GetClockSnapshot     = nn::time::ClockSnapshot::CreateWithStandardSystemClock (native @002aaf40)
//   CompareClockSnapshot = nn::time::CalculateStandardUserSystemClockDifferenceByUser(old, new) != 0 (native @002aaf70)
// The difference is computed as Ryujinx does (IStaticServiceForPsc): new.UserContext.Offset - old.UserContext.Offset, forced to 0
// when the two snapshots carry different steady-clock source ids or both have automatic correction on. On PC the steady clock is
// a per-Play-session Stopwatch with a fresh source id (Ryujinx also draws a new id each launch), and the user-clock offset only
// moves when the Windows clock jumps against it by more than 2 s - the PC equivalent of the user setting the console clock.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PcPlatform
{
    public static class PcTime
    {
        public const int SnapshotSize = 0xD0;

        static readonly Stopwatch steady = Stopwatch.StartNew();
        static readonly Guid sourceId = Guid.NewGuid();
        static readonly long steadyBase = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // steady seconds = steadyBase + elapsed
        static long userOffset;   // user clock = steady + userOffset (seconds)
        static bool offsetSet;
        static readonly object gate = new object();

        static long SteadySeconds()
        {
            return steadyBase + (long)steady.Elapsed.TotalSeconds;
        }

        static long UserOffset(long steadyNow, long wallNow)
        {
            lock (gate)
            {
                long measured = wallNow - steadyNow;
                if (!offsetSet || Math.Abs(measured - userOffset) > 2)
                {
                    userOffset = measured;
                    offsetSet = true;
                }
                return userOffset;
            }
        }

        // nn::time::ClockSnapshot layout: +0x00 user SystemClockContext {i64 offset, SteadyClockTimePoint {i64 value, u128 source}},
        // +0x20 network SystemClockContext, +0x40 i64 user time, +0x48 i64 network time, +0x50 user CalendarTime (8),
        // +0x58 network CalendarTime (8), +0x60 user CalendarAdditionalInfo (0x18), +0x78 network CalendarAdditionalInfo (0x18),
        // +0x90 SteadyClockTimePoint (0x18), +0xA8 LocationName (0x24), +0xCC bool automatic correction, +0xCD u8 type.
        public static void Create(IntPtr buf)
        {
            var b = new byte[SnapshotSize];
            long steadyNow = SteadySeconds();
            long wallNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long offset = UserOffset(steadyNow, wallNow);
            long userTime = steadyNow + offset;
            byte[] source = sourceId.ToByteArray();

            WriteContext(b, 0x00, offset, steadyNow, source);
            WriteContext(b, 0x20, offset, steadyNow, source); // no network clock on PC: mirrors the user clock
            BitConverter.GetBytes(userTime).CopyTo(b, 0x40);
            BitConverter.GetBytes(userTime).CopyTo(b, 0x48);

            // The console's time zone = the host's: the shipped game also reads System.DateTime.Now directly (GameManager.nowTime,
            // PlayTimeManager, DailyEventWork, ...), which Mono answers in the Windows zone, so the snapshot calendar is host-local too
            // and the reference emulator runs with --system-timezone PcConfig.TimeZoneLocation (HANDOFF §11.15; s33 labelled this
            // calendar "UTC" while Ryubing's default device zone IS UTC - a 7-hour time-of-day difference on the author's machine).
            DateTime local = DateTimeOffset.FromUnixTimeSeconds(userTime).ToLocalTime().DateTime;
            WriteCalendar(b, 0x50, local);
            WriteCalendar(b, 0x58, local);
            TimeSpan utcOffset = TimeZoneInfo.Local.GetUtcOffset(local);
            WriteAdditional(b, 0x60, local, utcOffset);
            WriteAdditional(b, 0x78, local, utcOffset);

            BitConverter.GetBytes(steadyNow).CopyTo(b, 0x90);
            Array.Copy(source, 0, b, 0x98, 16);
            byte[] location = System.Text.Encoding.ASCII.GetBytes(PcConfig.TimeZoneLocation);
            Array.Copy(location, 0, b, 0xA8, location.Length);
            b[0xCC] = 0;
            b[0xCD] = 0;
            Marshal.Copy(b, 0, buf, SnapshotSize);
        }

        public static bool Compare(IntPtr oldSnapshot, IntPtr newSnapshot)
        {
            var a = new byte[SnapshotSize];
            var c = new byte[SnapshotSize];
            Marshal.Copy(oldSnapshot, a, 0, SnapshotSize);
            Marshal.Copy(newSnapshot, c, 0, SnapshotSize);
            long difference = BitConverter.ToInt64(c, 0x00) - BitConverter.ToInt64(a, 0x00);
            bool sameSource = true;
            for (int i = 0; i < 16; i++)
                sameSource &= a[0x10 + i] == c[0x10 + i];
            if (!sameSource || (a[0xCC] != 0 && c[0xCC] != 0))
                difference = 0;
            return difference != 0;
        }

        static void WriteContext(byte[] b, int at, long offset, long steadyNow, byte[] source)
        {
            BitConverter.GetBytes(offset).CopyTo(b, at);
            BitConverter.GetBytes(steadyNow).CopyTo(b, at + 8);
            Array.Copy(source, 0, b, at + 0x10, 16);
        }

        static void WriteCalendar(byte[] b, int at, DateTime t)
        {
            BitConverter.GetBytes((short)t.Year).CopyTo(b, at);
            b[at + 2] = (byte)t.Month;
            b[at + 3] = (byte)t.Day;
            b[at + 4] = (byte)t.Hour;
            b[at + 5] = (byte)t.Minute;
            b[at + 6] = (byte)t.Second;
        }

        static void WriteAdditional(byte[] b, int at, DateTime t, TimeSpan utcOffset)
        {
            BitConverter.GetBytes((int)t.DayOfWeek).CopyTo(b, at);
            BitConverter.GetBytes(t.DayOfYear - 1).CopyTo(b, at + 4);
            byte[] name = System.Text.Encoding.ASCII.GetBytes(PcConfig.TimeZoneAbbreviation);
            Array.Copy(name, 0, b, at + 8, name.Length);
            BitConverter.GetBytes((int)utcOffset.TotalSeconds).CopyTo(b, at + 0x14);
        }
    }
}
#endif
