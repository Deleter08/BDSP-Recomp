// D7 (HANDOFF §11.15): unpack a Unity-crunched texture (.crn data as stored in a Texture2D of format DXT1Crunched / DXT5Crunched)
// into the raw DXT data of EVERY mip level, concatenated level 0..n-1 - the layout a native Unity Texture2D (DXT1 / DXT5) stores.
// The decoder is Unity's own fork of crunch (Unity-Technologies/crunch, branch "unity", commit 8708900e, inc/crn_decomp.h), the
// transcoder Unity's runtime uses to turn crunched data into DXT at load, so the result equals what the Switch player uploads.
// Build: g++ -O2 -o crn_levels.exe crn_levels.cpp       Use: crn_levels <in.crn> <out.dxt>   (prints "levels W H fmt bytes")
#include <cstddef>
#include <cstdlib>
#include <cstring>
#include "crn_decomp.h"
#include <cstdio>
#include <vector>

int main(int argc, char** argv)
{
    if (argc != 3)
    {
        std::fprintf(stderr, "usage: crn_levels <in.crn> <out.dxt>\n");
        return 2;
    }
    std::FILE* f = std::fopen(argv[1], "rb");
    if (!f)
        return 3;
    std::vector<unsigned char> crn;
    unsigned char buf[65536];
    size_t n;
    while ((n = std::fread(buf, 1, sizeof(buf), f)) > 0)
        crn.insert(crn.end(), buf, buf + n);
    std::fclose(f);

    crnd::crn_texture_info info;
    if (!crnd::crnd_get_texture_info(crn.data(), (crnd::uint32)crn.size(), &info))
    {
        std::fprintf(stderr, "not a crn file\n");
        return 4;
    }
    crnd::crnd_unpack_context ctx = crnd::crnd_unpack_begin(crn.data(), (crnd::uint32)crn.size());
    if (!ctx)
        return 5;
    const crnd::uint32 block_bytes = crnd::crnd_get_bytes_per_dxt_block(info.m_format);
    std::vector<unsigned char> out;
    for (crnd::uint32 level = 0; level < info.m_levels; level++)
    {
        const crnd::uint32 w = (info.m_width >> level) > 1 ? (info.m_width >> level) : 1;
        const crnd::uint32 h = (info.m_height >> level) > 1 ? (info.m_height >> level) : 1;
        const crnd::uint32 bx = (w + 3) / 4, by = (h + 3) / 4;
        const crnd::uint32 row_pitch = bx * block_bytes;
        const crnd::uint32 size = row_pitch * by;
        std::vector<unsigned char> level_data(size);
        void* faces[1] = {level_data.data()};
        if (!crnd::crnd_unpack_level(ctx, faces, size, row_pitch, level))
        {
            std::fprintf(stderr, "unpack failed at level %u\n", level);
            crnd::crnd_unpack_end(ctx);
            return 6;
        }
        out.insert(out.end(), level_data.begin(), level_data.end());
    }
    crnd::crnd_unpack_end(ctx);
    std::FILE* o = std::fopen(argv[2], "wb");
    if (!o)
        return 7;
    std::fwrite(out.data(), 1, out.size(), o);
    std::fclose(o);
    std::printf("%u %u %u %u %zu\n", info.m_levels, info.m_width, info.m_height, (unsigned)info.m_format, out.size());
    return 0;
}
