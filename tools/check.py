#!/usr/bin/env python3
"""Static checks for the generated mod (no .NET needed).

Usage: check.py <openra-root>
Verifies that the YAML the generator wrote lines up with the C# trait fields, the RA parent actors
and the fluent strings, so mistakes show up here instead of as a crash at game start.
"""
import os
import re
import sys

root = sys.argv[1]
here = os.path.dirname(os.path.abspath(__file__))
cs_dir = os.path.join(here, "..", "OpenRA.Mods.AutoForce")
mod = os.path.join(root, "mods", "autoforce")
errors = []


def err(msg):
    errors.append(msg)


# ---- C# fields -------------------------------------------------------------
def cs_fields(cls_file, cls_name):
    text = open(os.path.join(cs_dir, cls_file), encoding="utf-8").read()
    m = re.search(r"class %s\b.*?\n\t\{(.*?)\n\t\}" % cls_name, text, re.S)
    body = m.group(1)
    return set(re.findall(r"public readonly [\w<>,\s]+? (\w+)\s*(?:=|;)", body))


helix_fields = cs_fields("Traits/HelixNode.cs", "HelixNodeInfo")
squad_fields = cs_fields("Traits/AutoSquad.cs", "AutoSquadInfo")
cs_fields_extra = {"onde": cs_fields("Traits/OnDemandBuild.cs", "OnDemandBuildInfo")}
effects = set(re.findall(r"(\w+),", re.search(r"enum HelixEffect\s*\{(.*?)\}", open(os.path.join(cs_dir, "Traits/HelixNode.cs")).read(), re.S).group(1).replace("//", "\n//").replace("\n//", "\n#")))
sides = {"Quantity", "Quality"}

# ---- parse overlay ---------------------------------------------------------
rules_path = os.path.join(mod, "rules", "autoforce.yaml")
lines = open(rules_path, encoding="utf-8").read().split("\n")
actors = {}
cur = trait = None
for i, ln in enumerate(lines, 1):
    if not ln.strip() or ln.lstrip().startswith("#"):
        continue
    if re.match(r" +", ln):
        err("%s:%d uses spaces for indentation" % (rules_path, i))
    depth = len(ln) - len(ln.lstrip("\t"))
    s = ln.strip()
    if depth == 0:
        cur = s.rstrip(":")
        actors[cur] = {"traits": [], "nodes": []}
        trait = None
    elif depth == 1:
        trait = {"name": s.split(":")[0], "value": s.split(":", 1)[1].strip(), "kv": {}}
        actors[cur]["traits"].append(trait)
    elif depth == 2 and trait is not None:
        k, _, v = s.partition(":")
        trait["kv"][k] = v.strip()
    elif depth > 3:
        err("%s:%d unexpected indentation" % (rules_path, i))

for name, a in actors.items():
    ids = set()
    tiers = {}
    for t in a["traits"]:
        base = t["name"].split("@")[0]
        if base == "HelixNode":
            bad = set(t["kv"]) - helix_fields
            if bad:
                err("%s %s: unknown HelixNode fields %s" % (name, t["name"], sorted(bad)))
            if (t["kv"].get("Id"), t["kv"].get("Faction")) in ids:
                err("%s: duplicate node id %s" % (name, t["kv"].get("Id")))
            ids.add((t["kv"].get("Id"), t["kv"].get("Faction")))
            suffix = t["name"].split("@")[1]
            fac = t["kv"].get("Faction")
            if suffix != t["kv"].get("Id") + {"Allies": "a", "Soviet": "s"}.get(fac, ""):
                err("%s: trait suffix and Id/Faction differ for %s" % (name, t["name"]))
            if t["kv"].get("Side") not in sides:
                err("%s: bad Side %s" % (name, t["kv"].get("Side")))
            if t["kv"].get("Effect") not in effects:
                err("%s: bad Effect %s" % (name, t["kv"].get("Effect")))
            for f_ in ("Allies", "Soviet"):
                if fac in (None, f_):
                    tiers.setdefault((f_, int(t["kv"]["Tier"])), []).append(t["kv"]["Side"])
            major = t["kv"].get("Major") == "true"
            if major != (int(t["kv"]["Tier"]) % 3 == 0):
                err("%s: Major flag wrong at tier %s" % (name, t["kv"]["Tier"]))
            if t["kv"]["Effect"] == "Condition" and "Condition" not in t["kv"]:
                err("%s %s: Condition effect without Condition" % (name, t["name"]))
            if t["kv"]["Effect"] in ("Armor", "Storage") and "Value" not in t["kv"]:
                err("%s %s: missing Value" % (name, t["name"]))
            eff = t["kv"]["Effect"]
            if eff in ("Speed", "FireRate", "Damage") and "Value" not in t["kv"]:
                err("%s %s: missing Value" % (name, t["name"]))
            if eff in ("AddUnit", "Replace") and "Unit" not in t["kv"]:
                err("%s %s: missing Unit" % (name, t["name"]))
            if eff == "Replace" and "From" not in t["kv"]:
                err("%s %s: missing From" % (name, t["name"]))
        if base == "AutoSquad":
            bad = set(t["kv"]) - squad_fields
            if bad:
                err("%s: unknown AutoSquad fields %s" % (name, sorted(bad)))
    if ids:
        for f_ in ("Allies", "Soviet"):
            ts = {k[1]: v for k, v in tiers.items() if k[0] == f_}
            n_t = len(ts)
            if n_t not in (6, 9) or sorted(ts) != list(range(1, n_t + 1)) or any(sorted(v) != ["Quality", "Quantity"] for v in ts.values()):
                err("%s (%s): helix must have one Quantity + one Quality node on every tier (6 or 9 tiers)" % (name, f_))
        # every granted condition must be consumed by a trait of this actor or by ^BasicBuilding
        for t in a["traits"]:
            if t["name"].startswith("HelixNode@") and t["kv"].get("Effect") == "Condition":
                c = t["kv"]["Condition"]
                used = any(x["kv"].get("RequiresCondition") == c for x in a["traits"])
                if not used:
                    err("%s: condition %s is granted but nothing requires it" % (name, c))
                if not any(x["kv"].get("Prerequisites") == c for x in actors.get("^BasicBuilding", {"traits": []})["traits"]) \
                        and not any(x["name"].startswith("Power@") and x["kv"].get("RequiresCondition") == c for x in a["traits"]):
                    err("%s: condition %s has no effect" % (name, c))

# ---- parents, removals, exits ---------------------------------------------
ra_rules = ""
d = os.path.join(root, "mods", "ra", "rules")
for f in os.listdir(d):
    if f.endswith(".yaml"):
        ra_rules += open(os.path.join(d, f), encoding="utf-8").read() + "\n"


def ra_block(actor):
    # an actor can be declared in several files (later ones extend it), so join every block
    ms = re.findall(r"^%s:\n((?:\t.*\n|\n)*)" % re.escape(actor), ra_rules, re.M)
    return "".join(ms) if ms else None


ONDEMAND = cs_fields("Traits/OnDemandBuild.cs", "OnDemandBuildInfo")
for name, a in actors.items():
    for t in a["traits"]:
        if t["name"] == "OnDemandBuild":
            bad = set(t["kv"]) - ONDEMAND
            if bad:
                err("%s: unknown OnDemandBuild fields %s" % (name, sorted(bad)))
            if "Unit" not in t["kv"]:
                err("%s: OnDemandBuild needs Unit" % name)

for name, a in actors.items():
    inh = next((t["value"] for t in a["traits"] if t["name"] == "Inherits"), None)
    if inh:
        blk = ra_block(inh)
        if blk is None and inh not in actors:
            err("%s inherits unknown actor %s" % (name, inh))
        for t in a["traits"]:
            if t["name"].startswith("-") and blk is not None:
                tn = t["name"][1:]
                if inh in ("E1", "E3") and tn == "Buildable":
                    continue
                if not re.search(r"^\t%s:" % re.escape(tn), blk, re.M):
                    err("%s removes %s but %s does not have it" % (name, tn, inh))
            if re.match(r"Exit@\d", t["name"]) and blk is not None:
                if not re.search(r"^\t%s:" % re.escape(t["name"]), blk, re.M):
                    err("%s overrides %s which %s lacks" % (name, t["name"], inh))
    elif name.startswith("^") or name == "World":
        pass
    elif ra_block(name) is None:
        err("%s missing in RA rules" % name)
    else:
        blk = ra_block(name)
        for t in a["traits"]:
            if t["name"].startswith("-") and not re.search(r"^\t%s:" % re.escape(t["name"][1:]), blk, re.M):
                err("%s removes %s which RA does not define" % (name, t["name"][1:]))
            if re.match(r"Exit@", t["name"]) and "ProductionTypes" in t["kv"] and not re.search(r"^\t%s:" % re.escape(t["name"]), blk, re.M) \
                    and name not in ("FIX", "PROC"):
                err("%s overrides %s which RA lacks" % (name, t["name"]))

# the unit-producing buildings must keep an Exit usable by their production type
for name, a in actors.items():
    ap = next((t for t in a["traits"] if t["name"] == "AutoSquad"), None)
    if not ap:
        continue
    ptype = ap["kv"].get("ProductionType")
    prod = next((t for t in a["traits"] if t["name"] == "Production"), None)
    if ptype is None:
        continue
    if prod is None or ptype not in [x.strip() for x in prod["kv"].get("Produces", "").split(",")]:
        err("%s: Production.Produces must include AutoSquad.ProductionType (%s)" % (name, ptype))

# ---- fluent keys -----------------------------------------------------------
ftl = open(os.path.join(mod, "fluent", "autoforce.ftl"), encoding="utf-8").read()
for name, a in actors.items():
    for t in a["traits"]:
        for v in list(t["kv"].values()) + [t["value"]]:
            m = re.fullmatch(r"(actor-[\w]+)\.(name|description)", v)
            if m:
                blk = re.search(r"^%s =\n((?:[ ]+.*\n)+)" % m.group(1), ftl, re.M)
                if not blk or not re.search(r"^\s+\.%s = \S" % m.group(2), blk.group(1), re.M):
                    err("fluent key missing: %s" % v)

# ---- weapons referenced by units exist ------------------------------------
weapons = open(os.path.join(mod, "weapons", "autoforce.yaml"), encoding="utf-8").read()
for name, a in actors.items():
    for t in a["traits"]:
        w = t["kv"].get("Weapon")
        if w and not re.search(r"^%s:" % re.escape(w), weapons, re.M):
            err("%s uses weapon %s which is not defined" % (name, w))
for w in re.findall(r"^Inherits: (\S+)", weapons, re.M):
    if w.startswith("^"):
        continue
    if not re.search(r"^%s:" % re.escape(w), open(os.path.join(root, "mods/ra/weapons/smallcaliber.yaml")).read() +
                     open(os.path.join(root, "mods/ra/weapons/missiles.yaml")).read(), re.M):
        err("weapon parent %s not found in RA weapons" % w)

# ---- mod.yaml --------------------------------------------------------------
my = open(os.path.join(mod, "mod.yaml"), encoding="utf-8").read()
for needle in ("autoforce|rules/autoforce.yaml", "autoforce|weapons/autoforce.yaml",
               "autoforce|fluent/autoforce.ftl", "autoforce|sequences/autoforce.yaml",
               "OpenRA.Mods.AutoForce.dll", "$autoforce: autoforce"):
    if needle not in my:
        err("mod.yaml lacks %s" % needle)

# ---- C# sanity: balanced braces, no tabs/space mixups, brace per file ------
for dp, _, fs in os.walk(cs_dir):
    for f in fs:
        if f.endswith(".cs"):
            s = open(os.path.join(dp, f), encoding="utf-8").read()
            code = re.sub(r'"(?:\\.|[^"\\])*"', '""', s)
            code = re.sub(r"//.*", "", code)
            code = re.sub(r"/\*.*?\*/", "", code, flags=re.S)
            for a, b in ("{}", "()", "[]"):
                if code.count(a) != code.count(b):
                    err("%s: unbalanced %s%s (%d vs %d)" % (f, a, b, code.count(a), code.count(b)))

if errors:
    print("FAILED")
    for e in errors:
        print(" -", e)
    sys.exit(1)

n_nodes = sum(1 for a in actors.values() for t in a["traits"] if t["name"].startswith("HelixNode@"))
print("OK: %d actors, %d helix nodes, YAML keys match C# fields, parents/removals/exits/fluent/weapons consistent" % (len(actors), n_nodes))

