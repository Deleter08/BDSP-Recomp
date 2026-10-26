#if !SWITCH
// PC platform layer (s33c): nn::ngc, the NG-word (profanity) filter. NOT shipped code.
// On the console nn::ngc::ProfanityFilter is SDK code that reads the NgWord system archive; the game reaches it offline when the
// player names a Pokemon / the trainer / a box (Dpr.UI.SoftwareKeyboard.CheckNgc). The behaviour reference is Ryujinx's Ngct
// service (Services/Ngct/NgctServer.cs): no word table is shipped, nothing matches, the text comes back unchanged, Success.
// So every word is clean here, the call never fails, and the [Out] result array keeps the zeros its marshaller wrote.
using System;
using System.Collections.Generic;

namespace PcPlatform
{
    public static class PcNgc
    {
        class Filter
        {
            public nn.ngc.ProfanityFilter.MaskMode MaskMode = nn.ngc.ProfanityFilter.MaskMode.OverWrite;
            public nn.ngc.ProfanityFilter.SkipMode SkipMode = nn.ngc.ProfanityFilter.SkipMode.NotSkip;
            public bool CheckDesiredLanguage;
        }

        static readonly Dictionary<long, Filter> filters = new Dictionary<long, Filter>();
        static long nextHandle = 0x1000;

        // nn::ngc::ProfanityFilter() + Initialize(workBuffer 0x20000 aligned 0x1000, checkDesiredLanguage): two non-null native
        // pointers (the 0x30-byte filter object and the work buffer) and Success.
        public static nn.Result Initialize(ref IntPtr profanityFilter, ref IntPtr ngcWorkBuffer, bool checkDesiredLanguage)
        {
            long handle;
            lock (filters)
            {
                handle = nextHandle;
                nextHandle += 0x10;
                filters[handle] = new Filter { CheckDesiredLanguage = checkDesiredLanguage };
            }
            profanityFilter = new IntPtr(handle);
            ngcWorkBuffer = new IntPtr(handle + 8);
            return new nn.Result();
        }

        // The native calls the filter's virtual destructor (when non-null) and frees the work buffer.
        public static void Destroy(IntPtr profanityFilter, IntPtr ngcWorkBuffer)
        {
            lock (filters)
                filters.Remove(profanityFilter.ToInt64());
        }

        static Filter Get(IntPtr profanityFilter)
        {
            Filter f;
            lock (filters)
                filters.TryGetValue(profanityFilter.ToInt64(), out f);
            return f;
        }

        // The NgWord archive's content version. No archive on PC.
        public static uint GetContentVersion(IntPtr profanityFilter)
        {
            return 0; // TODO(pc): the console returns the installed NgWord system archive's version (firmware-dependent)
        }

        // checkResults[i] = the PatternList bits under which words[i] is an NG word; the marshaller zeroed the array before the
        // native call and copies it back after. Nothing matches (Ryujinx Ngct Match = false).
        public static nn.Result CheckProfanityWords(IntPtr profanityFilter, nn.ngc.ProfanityFilter.PatternList[] checkResults, nn.ngc.ProfanityFilter.PatternList patterns, string[] words, long wordCount)
        {
            if (checkResults != null)
            {
                long n = Math.Min(wordCount, checkResults.Length);
                for (long i = 0; i < n; i++)
                    checkResults[i] = 0;
            }
            return new nn.Result();
        }

        // In-place masking of the UTF-16 text; no word matches, so the text is left as is and the count is 0.
        public static nn.Result MaskProfanityWordsInText(IntPtr profanityFilter, ref int profanityWordCount, string text, nn.ngc.ProfanityFilter.PatternList patterns)
        {
            profanityWordCount = 0;
            return new nn.Result();
        }

        public static void SetMaskMode(IntPtr profanityFilter, nn.ngc.ProfanityFilter.MaskMode mode)
        {
            Filter f = Get(profanityFilter);
            if (f != null)
                f.MaskMode = mode;
        }

        public static void SkipAtSignCheck(IntPtr profanityFilter, nn.ngc.ProfanityFilter.SkipMode skipMode)
        {
            Filter f = Get(profanityFilter);
            if (f != null)
                f.SkipMode = skipMode;
        }

        // nn::ngc::CountNumbers(const char*): the number of numeral characters in the (UTF-8 marshalled) string.
        public static int CountNumbers(string str)
        {
            if (str == null)
                return 0;
            int count = 0;
            for (int i = 0; i < str.Length; i++)
            {
                if (char.IsDigit(str[i])) // TODO(pc): the SDK's exact numeral set (ASCII + full-width digits counted; other numeral forms unknown)
                    count++;
            }
            return count;
        }

        // nn::ngc::*ForPlatformRegionChina: the ngct service (Ryujinx Ngct Match / Filter): no match, text unchanged, Success.
        public static nn.Result CheckProfanityWordsForPlatformRegionChina(ref bool checkResult, string text)
        {
            checkResult = false;
            return new nn.Result();
        }

        public static nn.Result MaskProfanityWordsForPlatformRegionChina(System.Text.StringBuilder text)
        {
            return new nn.Result();
        }
    }
}
#endif
