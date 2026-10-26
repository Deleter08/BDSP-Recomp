#if !SWITCH
// PC platform layer: integer division with the Switch's results (author s46: "add the check at all 73"). NOT shipped code.
//
// The shipped game divides integers with raw AArch64 SDIV / UDIV (IL2CPP, divide-by-zero checks off). There a zero divisor never
// traps: x / 0 = 0 and x % 0 = x (the remainder is x - 0 * 0), and int.MinValue / -1 wraps to int.MinValue (remainder 0). The PC
// player is IL2CPP on x64, where IDIV faults on both, and the editor (Mono) throws. Game sites whose divisor is neither a constant
// nor tested for zero call these in their `#if !SWITCH` arm; the shipped statement stays in `#else`.
// Census of the sites: work/clean/c5/div_census.tsv; the C# lines and what was done with each: work/clean/c5/div_fix/.
//
// Overloads follow C#'s own operand promotion (uint op int -> long), so a call has the type the shipped expression had.
namespace PcPlatform
{
    public static class PcIntDiv
    {
        public static int Div(int a, int b) { return b == 0 ? 0 : b == -1 ? unchecked(-a) : a / b; }
        public static long Div(long a, long b) { return b == 0 ? 0L : b == -1L ? unchecked(-a) : a / b; }
        public static uint Div(uint a, uint b) { return b == 0u ? 0u : a / b; }
        public static ulong Div(ulong a, ulong b) { return b == 0ul ? 0ul : a / b; }

        public static int Rem(int a, int b) { return b == 0 ? a : b == -1 ? 0 : a % b; }
        public static long Rem(long a, long b) { return b == 0L ? a : b == -1L ? 0L : a % b; }
        public static uint Rem(uint a, uint b) { return b == 0u ? a : a % b; }
        public static ulong Rem(ulong a, ulong b) { return b == 0ul ? a : a % b; }
    }
}
#endif
