"""tools/winbuild/shader_variants_shipped.py - the SHIPPED shader variant list of the shipped `shaders` bundle (s39).

Why: BuildAssetBundles keeps a shader_feature variant only when a material or a ShaderVariantCollection IN THE SHADER'S OWN BUNDLE
asks for it (measured s39: the full Windows build gave Delphis/Map/Standard 6 sub-programs vs 5,140 shipped, the `shaders` bundle
163 KB vs 6.7 MB, although 9,849 project materials use its keywords; fxparticle - whose bundle carries the developers' own
.shadervariants assets - came out EXACTLY as shipped, 7,976 = 7,976). The shipped bundle holds 63 shaders + 1 material, no
collection, so the Windows build gets one: every (shader, pass LightMode, keyword set) compiled into the shipped bundle, read from
each pass's sub-programs (global + local keyword indices -> the pass's m_NameIndices names; vertex and fragment sets united).
The editor step BdspWinBuild writes it as a ShaderVariantCollection into the Windows `shaders` bundle.

usage: py -3.13 -I tools/winbuild/shader_variants_shipped.py [--bundle romfs/.../Dpr/shaders]
       -> work/winbuild/shipped_shader_variants.tsv  (#bdsp-shipped-variants\t1\t<rows>; shader \t lightmode \t keywords space-separated)
"""
import collections, os, sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402  (setup: dump / project / work folder from the setup config)


def arg(name, default):
    return sys.argv[sys.argv.index(name) + 1] if name in sys.argv else default


def main():
    import UnityPy
    bundle = arg("--bundle", os.path.join(paths.need(paths.AA, "dump"), "Dpr", "shaders"))
    out = paths.work("build", "shipped_shader_variants.tsv")
    env = UnityPy.load(bundle)
    rows, per_shader = [], collections.Counter()
    subprograms = collections.Counter()
    for o in env.objects:
        if o.type.name != "Shader":
            continue
        pf = o.read_typetree()["m_ParsedForm"]
        name = pf["m_Name"]
        for si, ss in enumerate(pf["m_SubShaders"]):
            for p in ss["m_Passes"]:
                names = {v: k for k, v in p["m_NameIndices"]}
                tags = dict(p["m_State"]["m_Tags"]["tags"]) if p.get("m_State") else {}
                tags.update({k.upper(): v for k, v in dict((p.get("m_Tags") or {}).get("tags", [])).items()})
                light = (tags.get("LIGHTMODE") or "").upper()
                sets = set()
                for prog in ("progVertex", "progFragment", "progGeometry", "progHull", "progDomain"):
                    for s in (p.get(prog) or {}).get("m_SubPrograms", []):
                        subprograms[name] += 1
                        kws = [names.get(i, "") for i in s["m_GlobalKeywordIndices"] + s["m_LocalKeywordIndices"]]
                        if any(k == "" for k in kws):
                            print("WARN %s: keyword index without a name" % name)
                        sets.add(" ".join(sorted(k for k in kws if k)))
                for kw in sorted(sets):
                    rows.append((name, light, kw))
                    per_shader[name] += 1
    rows = sorted(set(rows))
    with open(out, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("#bdsp-shipped-variants\t1\t%d\n" % len(rows))
        for r in rows:
            fh.write("\t".join(r) + "\n")
    print("shaders %d, variant rows %d (sub-programs %d) -> %s" % (len(per_shader), len(rows), sum(subprograms.values()), out))
    for n, c in per_shader.most_common(8):
        print("  %-44s %5d variants, %5d sub-programs" % (n, c, subprograms[n]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
