#!/usr/bin/env python3
"""AutoForce generator.

Writes mods/autoforce/ into an OpenRA source checkout (release 20250330 or compatible):
  mod.yaml            derived from mods/ra/mod.yaml so it always matches the engine version
  rules/autoforce.yaml, weapons/autoforce.yaml, fluent/autoforce.ftl

Usage: generate.py <openra-root> [--check]
"""
import os
import re
import shutil
import sys

TICKS_PER_SECOND = 25
MAJOR_TIERS = (3, 6, 9)

# ----------------------------------------------------------------------------
# Node helpers. Every strand is a list of 9 nodes (tiers 1..9, majors at 3/6/9).
# ----------------------------------------------------------------------------


def S(name, desc, v):
    return dict(effect="Speed", value=v, name=name, desc=desc)


def R(name, desc, v):
    return dict(effect="FireRate", value=v, name=name, desc=desc)


def D(name, desc, v):
    return dict(effect="Damage", value=v, name=name, desc=desc)


def A(unit, count, name, desc):
    return dict(effect="AddUnit", unit=unit, count=count, name=name, desc=desc)


def E(count, name, desc):
    return dict(effect="AddElite", count=count, name=name, desc=desc)


def P(frm, to, count, name, desc):
    return dict(effect="Replace", frm=frm, unit=to, count=count, name=name, desc=desc)


def AR(v, name, desc):
    return dict(effect="Armor", value=v, name=name, desc=desc)


def ST(v, name, desc):
    return dict(effect="Storage", value=v, name=name, desc=desc)


def C(cond, name, desc):
    return dict(effect="Condition", cond=cond, name=name, desc=desc)


def spd(v):
    return "Squad moves %d%% faster." % v


def rate(v):
    return "Squad fires %d%% faster." % v


def dmg(v):
    return "Squad deals %d%% more damage." % v


# ----------------------------------------------------------------------------
# Buildings
# ----------------------------------------------------------------------------

BUILDINGS = [
    dict(
        actor="AFINF", parent="TENT", image="tent", name="Infantry Barracks",
        desc="Trains a rifle squad on its own and keeps it topped up. Open the helix menu to upgrade.",
        cost=500, order=21, prereq="anypower, ~techlevel.infonly", ptype="AutoInf",
        interval=150, vehicles=False, roster=[("E1", 8)],
        quality=[
            S("Forced March", spd(10), 10), D("Hardened Rounds", dmg(10), 10),
            P("E1", "SNPR", 2, "Marksman Team", "MAJOR: 2 riflemen become snipers (long range, kills infantry fast)."),
            R("Drilled Reloads", rate(10), 10), S("Light Kit", spd(10), 10),
            P("E1", "SHOK", 2, "Shock Detachment", "MAJOR: 2 riflemen become shock troopers."),
            D("Armor Piercing", dmg(15), 15), R("Rapid Fire Doctrine", rate(15), 15),
            P("E1", "E7", 1, "Special Forces", "MAJOR: 1 rifleman becomes a commando."),
        ],
        quantity=[
            A("MEDI", 1, "Field Medic", "Adds a medic to the squad."), A("E1", 2, "Conscripts", "Adds 2 riflemen."),
            A("MGUN", 3, "Machine Gun Section", "MAJOR: adds 3 machine gunners."),
            E(2, "Reinforce Elites", "Adds 2 more of your newest special unit."), A("E1", 2, "Second Wave", "Adds 2 riflemen."),
            A("E2", 3, "Grenadier Platoon", "MAJOR: adds 3 grenadiers."),
            E(2, "Veteran Cadre", "Adds 2 more of your newest special unit."), A("MEDI", 1, "Second Medic", "Adds another medic."),
            A("E1", 6, "Mass Levy", "MAJOR: adds 6 riflemen."),
        ]),
    dict(
        actor="AFAT", parent="BARR", image="barr", name="Anti-Armor Barracks",
        desc="Rocket troops that train themselves. Built to kill tanks.",
        cost=600, order=22, prereq="anypower, barracks, ~techlevel.low", ptype="AutoInf",
        interval=170, vehicles=False, roster=[("E3", 6), ("E1", 2)],
        quality=[
            D("Shaped Charges", dmg(10), 10), R("Rapid Loaders", rate(10), 10),
            P("E3", "HUNT", 3, "Tank Hunters", "MAJOR: 3 rocket soldiers become tank hunters (heavier missiles)."),
            S("Light Packs", spd(10), 10), D("Tungsten Warheads", dmg(15), 15),
            P("E1", "HUNT", 2, "Armor Specialists", "MAJOR: 2 riflemen become tank hunters."),
            R("Auto Loaders", rate(15), 15), D("Depleted Core", dmg(20), 20),
            P("E3", "HUNT", 3, "Siege Breakers", "MAJOR: 3 more rocket soldiers become tank hunters."),
        ],
        quantity=[
            A("E3", 1, "Extra Launcher", "Adds a rocket soldier."), A("E3", 1, "Extra Launcher II", "Adds another rocket soldier."),
            A("HUNT", 2, "Heavy Rocket Section", "MAJOR: adds 2 tank hunters."),
            E(2, "Reinforce Elites", "Adds 2 more of your newest special unit."), A("MEDI", 1, "Field Medic", "Adds a medic."),
            A("E3", 4, "Mass Rocket Troops", "MAJOR: adds 4 rocket soldiers."),
            E(2, "Veteran Cadre", "Adds 2 more of your newest special unit."), A("E6", 1, "Combat Engineer", "Adds an engineer."),
            A("HUNT", 4, "Armor Doctrine", "MAJOR: adds 4 tank hunters."),
        ]),
    dict(
        actor="AFDEMO", parent="TENT", image="tent", name="Demolition Barracks",
        desc="Flamers, grenadiers and assault specialists that train themselves. Close-range wrecking crew.",
        cost=600, order=23, prereq="anypower, barracks, ~techlevel.low", ptype="AutoInf",
        interval=170, vehicles=False, roster=[("E4", 4), ("E2", 4)],
        quality=[
            D("Napalm Mix", dmg(10), 10), S("Sprint Training", spd(10), 10),
            P("E4", "SHOK", 2, "Shock Assault", "MAJOR: 2 flamers become shock troopers."),
            R("Quick Hands", rate(10), 10), D("Thermite Charges", dmg(15), 15),
            P("E2", "SHOK", 2, "Tesla Vanguard", "MAJOR: 2 grenadiers become shock troopers."),
            S("Assault Kit", spd(10), 10), D("Overpressure", dmg(20), 20),
            P("E4", "E7", 1, "Demolition Expert", "MAJOR: 1 flamer becomes a commando."),
        ],
        quantity=[
            A("E4", 1, "Extra Flamer", "Adds a flamethrower."), A("E2", 1, "Extra Grenadier", "Adds a grenadier."),
            A("DOG", 3, "Attack Dogs", "MAJOR: adds 3 attack dogs."),
            E(2, "More Hounds", "Adds 2 more of your newest special unit."), A("E4", 2, "Burner Crew", "Adds 2 flamers."),
            A("E4", 4, "Flame Battalion", "MAJOR: adds 4 flamers."),
            E(2, "Veteran Cadre", "Adds 2 more of your newest special unit."), A("E2", 2, "Grenade Crew", "Adds 2 grenadiers."),
            A("E2", 4, "Blitz Levy", "MAJOR: adds 4 grenadiers."),
        ]),
    dict(
        actor="AFMAIN", parent="WEAP", image="weap", name="Main Factory",
        desc="Builds your tank line by itself. Upgrade toward heavy armor or a swarm of light tanks.",
        cost=2000, order=81, prereq="proc, ~techlevel.low", ptype="AutoVeh",
        interval=450, vehicles=True, roster=[("2TNK", 4)], extra_prereq="weap",
        quality=[
            D("Depleted Shells", dmg(10), 10), S("Tuned Engines", spd(10), 10),
            P("2TNK", "3TNK", 2, "Heavy Armor", "MAJOR: 2 medium tanks become heavy tanks."),
            R("Autoloaders", rate(10), 10), D("Reinforced Barrels", dmg(10), 10),
            P("2TNK", "4TNK", 1, "Mammoth Guard", "MAJOR: 1 medium tank becomes a mammoth tank."),
            S("Overdrive", spd(10), 10), R("Rapid Cycling", rate(15), 15),
            P("3TNK", "4TNK", 2, "Super Heavy Doctrine", "MAJOR: 2 heavy tanks become mammoth tanks."),
        ],
        quantity=[
            A("2TNK", 1, "Extra Chassis", "Adds a medium tank."), A("1TNK", 2, "Light Tank Screen", "Adds 2 light tanks."),
            A("1TNK", 3, "Light Tank Wing", "MAJOR: adds 3 light tanks."),
            E(2, "Reinforce Elites", "Adds 2 more of your newest special unit."), A("2TNK", 1, "Second Chassis", "Adds a medium tank."),
            A("3TNK", 2, "Armored Reserve", "MAJOR: adds 2 heavy tanks."),
            E(1, "Veteran Cadre", "Adds 1 more of your newest special unit."), A("1TNK", 2, "Scout Tanks", "Adds 2 light tanks."),
            A("2TNK", 3, "Tank Army", "MAJOR: adds 3 medium tanks."),
        ]),
    dict(
        actor="AFUTIL", parent="WEAP", image="weap", name="Utility Factory",
        desc="Scout vehicles and transports that build themselves. Upgrade for stealth and electronic specialists.",
        cost=1200, order=82, prereq="proc, ~techlevel.low", ptype="AutoVeh",
        interval=380, vehicles=True, roster=[("JEEP", 2), ("APC", 2)],
        quality=[
            S("Racing Tires", spd(10), 10), D("Better Guns", dmg(10), 10),
            P("JEEP", "STNK", 2, "Recon Specialists", "MAJOR: 2 scouts become stealth tanks."),
            R("Fast Loaders", rate(10), 10), S("Turbo Engines", spd(10), 10),
            P("APC", "CTNK", 1, "Chrono Detachment", "MAJOR: 1 transport becomes a chrono tank."),
            D("Hot Loads", dmg(15), 15), R("Rapid Fire Mounts", rate(15), 15),
            P("APC", "TTNK", 2, "Tesla Escorts", "MAJOR: 2 transports become tesla tanks."),
        ],
        quantity=[
            A("JEEP", 1, "Extra Scout", "Adds a scout vehicle."), A("APC", 1, "Extra Transport", "Adds a transport."),
            A("JEEP", 3, "Motor Pool", "MAJOR: adds 3 scout vehicles."),
            E(2, "Reinforce Elites", "Adds 2 more of your newest special unit."), A("MNLY", 1, "Minelayer", "Adds a minelayer."),
            A("APC", 3, "Troop Convoy", "MAJOR: adds 3 transports."),
            E(1, "Veteran Cadre", "Adds 1 more of your newest special unit."), A("MGG", 1, "Gap Generator", "Adds a gap generator."),
            A("APC", 4, "Logistics Fleet", "MAJOR: adds 4 transports."),
        ]),
    dict(
        actor="AFSIEGE", parent="WEAP", image="weap", name="Siege Factory",
        desc="Artillery and rocket launchers that build themselves. Slow, long-range and expensive.",
        cost=1800, order=83, prereq="weap, dome, ~techlevel.low", ptype="AutoVeh",
        interval=480, vehicles=True, roster=[("ARTY", 2), ("V2RL", 2)],
        quality=[
            D("High Explosive", dmg(10), 10), R("Crew Drill", rate(10), 10),
            P("ARTY", "V2RL", 2, "Rocket Doctrine", "MAJOR: 2 artillery become V2 rocket launchers."),
            D("Heavy Charges", dmg(15), 15), S("Tracked Prime Movers", spd(10), 10),
            P("V2RL", "QTNK", 1, "Shock Wave Unit", "MAJOR: 1 V2 launcher becomes a MAD tank."),
            R("Auto Feed", rate(10), 10), D("Deep Strike", dmg(20), 20),
            P("ARTY", "QTNK", 1, "Doomsday Battery", "MAJOR: 1 artillery becomes a MAD tank."),
        ],
        quantity=[
            A("ARTY", 1, "Extra Gun", "Adds an artillery piece."), A("V2RL", 1, "Extra Launcher", "Adds a V2 launcher."),
            A("ARTY", 2, "Battery Expansion", "MAJOR: adds 2 artillery."),
            E(1, "Reinforce Elites", "Adds 1 more of your newest special unit."), A("2TNK", 1, "Escort Tank", "Adds a medium tank escort."),
            A("V2RL", 2, "Rocket Regiment", "MAJOR: adds 2 V2 launchers."),
            E(1, "Veteran Cadre", "Adds 1 more of your newest special unit."), A("JEEP", 1, "Spotter", "Adds a scout vehicle."),
            A("ARTY", 3, "Grand Battery", "MAJOR: adds 3 artillery."),
        ]),
]


# ----------------------------------------------------------------------------
# Upgrades for existing Red Alert buildings (no new actor: the building itself gets the trees).
# Short 6-tier trees (majors at tiers 3 and 6).
# ----------------------------------------------------------------------------

MCV_SECONDS = 120

_AF = "AutoAir"
INPLACE = [
    dict(
        actor="HPAD", ptypes=["AutoAir"], ptype="AutoAir", interval=300, initial=150, vehicles=True,
        costscale=1.2, timescale=1.2, roster=[("HELI", 2)],
        quality=[
            S("Light Rotors", spd(10), 10), D("Hardened Rockets", dmg(10), 10),
            P("HELI", "HIND", 1, "Hind Conversion", "MAJOR: 1 Longbow becomes a Hind gunship."),
            R("Auto Loaders", rate(10), 10), D("Armor-Piercing Rockets", dmg(15), 15),
            P("HELI", "HIND", 1, "Gunship Wing", "MAJOR: another Longbow becomes a Hind gunship."),
        ],
        quantity=[
            A("TRAN", 1, "Transport Chopper", "Adds a Chinook."), A("HELI", 1, "Extra Longbow", "Adds a Longbow."),
            A("HIND", 1, "Gunship Section", "MAJOR: adds a Hind gunship."),
            E(1, "Reinforce Elites", "Adds 1 more of your newest special aircraft."), A("HELI", 1, "Third Longbow", "Adds another Longbow."),
            A("HIND", 2, "Gunship Squadron", "MAJOR: adds 2 Hind gunships."),
        ]),
    dict(
        actor="AFLD", ptypes=["AutoAir"], ptype="AutoAir", interval=300, initial=150, vehicles=True,
        costscale=1.2, timescale=1.2, roster=[("YAK", 2)],
        quality=[
            S("Afterburner Tuning", spd(10), 10), D("Hardened Rounds", dmg(10), 10),
            P("YAK", "MIG", 1, "MiG Conversion", "MAJOR: 1 Yak becomes a MiG."),
            R("Auto Loaders", rate(10), 10), D("Armor-Piercing Rounds", dmg(15), 15),
            P("YAK", "MIG", 1, "MiG Wing", "MAJOR: another Yak becomes a MiG."),
        ],
        quantity=[
            A("YAK", 1, "Extra Yak", "Adds a Yak."), A("YAK", 1, "Extra Yak II", "Adds another Yak."),
            A("MIG", 1, "MiG Flight", "MAJOR: adds a MiG."),
            E(1, "Reinforce Elites", "Adds 1 more of your newest special aircraft."), A("YAK", 1, "Third Yak", "Adds another Yak."),
            A("MIG", 2, "MiG Squadron", "MAJOR: adds 2 MiGs."),
        ]),
    dict(
        actor="SYRD", ptypes=["AutoShip"], ptype="AutoShip", interval=400, initial=200, vehicles=True,
        costscale=1.2, timescale=1.2, roster=[("DD", 1), ("PT", 2)],
        exits=[("Exit@1", "AutoShip"), ("Exit@2", "AutoShip"), ("Exit@3", "AutoShip"), ("Exit@4", "AutoShip")],
        quality=[
            S("Clean Hulls", spd(10), 10), D("Hardened Shells", dmg(10), 10),
            P("DD", "CA", 1, "Cruiser Refit", "MAJOR: 1 destroyer becomes a cruiser."),
            R("Auto Loaders", rate(10), 10), D("Armor-Piercing Shells", dmg(15), 15),
            P("PT", "DD", 2, "Destroyer Squadron", "MAJOR: 2 gunboats become destroyers."),
        ],
        quantity=[
            A("PT", 1, "Extra Gunboat", "Adds a gunboat."), A("PT", 1, "Extra Gunboat II", "Adds another gunboat."),
            A("DD", 1, "Destroyer Division", "MAJOR: adds a destroyer."),
            E(1, "Reinforce Elites", "Adds 1 more of your newest special ship."), A("DD", 1, "Extra Destroyer", "Adds another destroyer."),
            A("CA", 1, "Cruiser Detachment", "MAJOR: adds a cruiser."),
        ]),
    dict(
        actor="SPEN", ptypes=["AutoShip", "AutoSub"], ptype="AutoShip", interval=400, initial=200, vehicles=True,
        costscale=1.2, timescale=1.2, roster=[("SS", 2)],
        exits=[("Exit@1", "AutoSub"), ("Exit@2", "AutoSub"), ("Exit@3", "AutoSub"), ("Exit@4", "AutoSub"),
               ("Exit@b1", "AutoShip"), ("Exit@b2", "AutoShip"), ("Exit@b3", "AutoShip"), ("Exit@b4", "AutoShip")],
        overrides=[("SS", "AutoSub"), ("MSUB", "AutoSub")],
        quality=[
            S("Quiet Props", spd(10), 10), D("Hardened Torpedoes", dmg(10), 10),
            P("SS", "MSUB", 1, "Missile Submarine", "MAJOR: 1 submarine becomes a missile submarine."),
            R("Auto Loaders", rate(10), 10), D("Heavy Warheads", dmg(15), 15),
            P("SS", "MSUB", 1, "Missile Wolfpack", "MAJOR: another submarine becomes a missile submarine."),
        ],
        quantity=[
            A("SS", 1, "Extra Submarine", "Adds a submarine."), A("SS", 1, "Extra Submarine II", "Adds another submarine."),
            A("MSUB", 1, "Missile Section", "MAJOR: adds a missile submarine."),
            E(1, "Reinforce Elites", "Adds 1 more of your newest special ship."), A("SS", 1, "Third Submarine", "Adds another submarine."),
            A("MSUB", 2, "Missile Flotilla", "MAJOR: adds 2 missile submarines."),
        ]),
    dict(
        actor="PROC", ptypes=["AutoHarv"], ptype="AutoHarv", interval=500, initial=375, vehicles=False,
        costscale=0.8, timescale=1.0, harvesters=True, roster=[("HARV", 1)], nobar=True,
        raw=["\tExit@1:", "\t\tSpawnOffset: 0,0,0", "\t\tExitCell: 1,2", "\t\tFacing: 256"],
        quality=[
            S("Tuned Engines", "Every harvester you own drives 3% faster.", 3),
            AR(4, "Armor Plating", "Every harvester takes 4% less damage."),
            AR(5, "Reinforced Hulls", "MAJOR: harvesters take 5% less damage (12% in total with the other armor upgrades)."),
            S("Efficient Gearing", "Harvesters drive 3% faster.", 3),
            AR(3, "Hardened Plating", "Harvesters take 3% less damage."),
            S("Racing Tune", "MAJOR: harvesters drive 4% faster (10% faster in total).", 4),
        ],
        quantity=[
            ST(500, "Extra Silo", "Your resource storage grows by 500."),
            A("HARV", 1, "Second Shift", "A refinery crew builds one more harvester for free."),
            ST(1000, "Silo Complex", "MAJOR: your resource storage grows by 1000."),
            ST(500, "Extra Silo II", "Your resource storage grows by 500."),
            A("HARV", 1, "Third Shift", "One more free harvester."),
            ST(1000, "Grand Storage Depot", "MAJOR: your resource storage grows by 1000."),
        ]),
    dict(
        actor="FACT", interval=100, initial=100, vehicles=False, costscale=0.8, timescale=1.0, roster=[],
        conditional=[],  # filled in below
        quality=[
            C("afb1", "Prefab Panels", "Buildings and defenses are constructed 4% faster."),
            C("afb2", "Crane Teams", "Buildings and defenses are constructed another 4% faster."),
            C("afb3", "Assembly Line", "MAJOR: construction is another 6% faster."),
            C("afb4", "Better Logistics", "Construction is another 3% faster."),
            C("afb5", "Night Shifts", "Construction is another 3% faster."),
            C("afb6", "Modular Construction", "MAJOR: construction is another 6% faster (about 24% in total)."),
        ],
        quantity=[
            C("afp1", "Auxiliary Generator", "The Construction Yard itself generates 15 power."),
            C("afp2", "Generator Tuning", "The Construction Yard generates 15 more power."),
            C("afp3", "Reactor Core", "MAJOR: the Construction Yard generates 30 more power."),
            C("afp4", "Coolant Loop", "The Construction Yard generates 20 more power."),
            C("afp5", "Power Bus", "The Construction Yard generates 20 more power."),
            C("afp6", "Fusion Cell", "MAJOR: the Construction Yard generates 40 more power (140 in total)."),
        ]),
]

YARD_CONDITIONALS = [("afb1", "time", 4), ("afb2", "time", 4), ("afb3", "time", 6),
                     ("afb4", "time", 3), ("afb5", "time", 3), ("afb6", "time", 6),
                     ("afp1", "power", 15), ("afp2", "power", 15), ("afp3", "power", 30),
                     ("afp4", "power", 20), ("afp5", "power", 20), ("afp6", "power", 40)]
for _b in INPLACE:
    if _b["actor"] == "FACT":
        _b["conditional"] = [c for c in YARD_CONDITIONALS if c[1] == "power"] + [c for c in YARD_CONDITIONALS if c[1] == "time"]


# Badge text and colour drawn on each building in the world, plus (optionally) a different existing
# structure icon for the build sidebar: actor -> (badge, colour, (icon sequence, icon file, image that hosts it)).
BADGES = {'AFINF': ('INF', '5CD65C', None), 'AFAT': ('AT', 'E8604C', None), 'AFDEMO': ('DEMO', 'F0A030', ('icon-demo', 'kennicon.shp', 'tent')), 'AFMAIN': ('MAIN', '5AA0F0', None), 'AFUTIL': ('UTIL', '4CD6D6', ('icon-util', 'fixicon.shp', 'weap')), 'AFSIEGE': ('SIEGE', 'C070E0', ('icon-siege', 'stekicon.shp', 'weap'))}

# New infantry built on top of existing RA actors: (name, parent, cost, display, desc, extra yaml lines)
CUSTOM_UNITS = [
    ("SNPR", "E1", 450, "Sniper", "Long-range rifleman. Deadly to infantry, weak against armor.",
     ["\tHealth:", "\t\tHP: 4000", "\tRevealsShroud:", "\t\tRange: 8c0",
      "\tArmament@PRIMARY:", "\t\tWeapon: SniperRifle"]),
    ("MGUN", "E1", 250, "Machine Gunner", "Sprays short bursts. Good against infantry.",
     ["\tArmament@PRIMARY:", "\t\tWeapon: MGunBurst"]),
    ("HUNT", "E3", 450, "Tank Hunter", "Rocket soldier with heavy anti-armor missiles.",
     ["\tArmament@SECONDARY:", "\t\tWeapon: HuntDragon"]),
]

WEAPONS = """\
SniperRifle:
	Inherits: ^LightMG
	ReloadDelay: 70
	Range: 9c0
	Report: gun11.aud
	Warhead@1Dam: SpreadDamage
		Damage: 6000
		Versus:
			None: 150
			Wood: 5
			Light: 10
			Heavy: 5
			Concrete: 2

MGunBurst:
	Inherits: M60mg
	Range: 5c0
	ReloadDelay: 24
	Burst: 6

HuntDragon:
	Inherits: Dragon
	ReloadDelay: 40
	Range: 5c768
	Warhead@1Dam: SpreadDamage
		Damage: 7000
"""

# Minor tiers cost/time; majors cost/time. Vehicles are scaled.
MINOR_COST = {1: 300, 2: 400, 4: 550, 5: 650, 7: 800, 8: 900}
MAJOR_COST = {3: 900, 6: 1500, 9: 2200}
MINOR_SECS = {1: 18, 2: 24, 4: 30, 5: 36, 7: 42, 8: 48}
MAJOR_SECS = {3: 40, 6: 55, 9: 70}


def node_yaml(b, side, idx, n):
    tier = idx + 1
    major = tier in MAJOR_TIERS
    major = n["desc"].startswith("MAJOR")
    assert tier in MAJOR_TIERS or not major, (b["actor"], side, tier)
    assert (tier in MAJOR_TIERS) == major, (b["actor"], side, tier)
    scale = b.get("costscale", 1.5 if b["vehicles"] else 1.0)
    cost = (MAJOR_COST if major else MINOR_COST)[tier]
    secs = (MAJOR_SECS if major else MINOR_SECS)[tier]
    cost = int(round(cost * scale / 50.0) * 50)
    secs = int(round(secs * b.get("timescale", 1.3 if b["vehicles"] else 1.0)))
    nid = ("ql" if side == "Quality" else "qn") + str(tier)
    out = ["\tHelixNode@%s:" % nid,
           "\t\tId: %s" % nid,
           "\t\tSide: %s" % side,
           "\t\tTier: %d" % tier]
    if major:
        out.append("\t\tMajor: true")
    out += ["\t\tName: %s" % n["name"],
            "\t\tDescription: %s" % n["desc"],
            "\t\tCost: %d" % cost,
            "\t\tDuration: %d" % (secs * TICKS_PER_SECOND),
            "\t\tEffect: %s" % n["effect"]]
    if "value" in n:
        out.append("\t\tValue: %d" % n["value"])
    if "unit" in n:
        out.append("\t\tUnit: %s" % n["unit"])
    if "frm" in n:
        out.append("\t\tFrom: %s" % n["frm"])
    if "cond" in n:
        out.append("\t\tCondition: %s" % n["cond"])
    if "count" in n:
        out.append("\t\tCount: %d" % n["count"])
    return out


def building_yaml(b):
    L = []
    L.append("%s:" % b["actor"])
    L.append("\tInherits: %s" % b["parent"])
    L += ["\tRenderSprites:", "\t\tImage: %s" % b["image"]]
    L += ["\tBuildable:",
          "\t\tBuildPaletteOrder: %d" % b["order"],
          "\t\tPrerequisites: %s" % b["prereq"],
          "\t\tDescription: actor-%s.description" % b["actor"].lower()]
    badge, colour, icon = BADGES[b["actor"]]
    if icon:
        L.append("\t\tIcon: %s" % icon[0])
    L += ["\tWithTextDecoration@badge:", "\t\tText: %s" % badge, "\t\tFont: TinyBold",
          "\t\tColor: %s" % colour, "\t\tPosition: Bottom"]
    L += ["\tValued:", "\t\tCost: %d" % b["cost"]]
    L += ["\tTooltip:", "\t\tName: actor-%s.name" % b["actor"].lower()]
    L.append("\t-ProductionBar:")
    L += ["\tProduction:", "\t\tProduces: %s" % b["ptype"]]
    if not b["vehicles"]:
        L += ["\tExit@1:", "\t\tProductionTypes: %s" % b["ptype"],
              "\tExit@2:", "\t\tProductionTypes: %s" % b["ptype"]]
    if b.get("extra_prereq"):
        L += ["\tProvidesPrerequisite@%s:" % b["extra_prereq"], "\t\tPrerequisite: %s" % b["extra_prereq"]]
    if b["parent"] in ("TENT", "BARR"):
        # Defenses (pillbox, flame tower, ...) need the classic "tent"/"barr" prerequisite names.
        L += ["\tProvidesPrerequisite@tent:", "\t\tPrerequisite: tent",
              "\tProvidesPrerequisite@barr:", "\t\tPrerequisite: barr"]
    L += ["\tAutoSquad:",
          "\t\tProductionType: %s" % b["ptype"],
          "\t\tSpawnInterval: %d" % b["interval"],
          "\t\tInitialDelay: %d" % (75 if not b["vehicles"] else 150),
          "\t\tRoster:"]
    for unit, count in b["roster"]:
        L.append("\t\t\t%s: %d" % (unit, count))
    L += nodes_yaml(b)
    return L


def nodes_yaml(b):
    L = []
    for idx in range(len(b["quality"])):
        L += node_yaml(b, "Quality", idx, b["quality"][idx])
        L += node_yaml(b, "Quantity", idx, b["quantity"][idx])
    return L


def inplace_yaml(b):
    L = ["%s:" % b["actor"]]
    L += b.get("raw", [])
    if b.get("ptypes"):
        L += ["\tProduction:", "\t\tProduces: %s" % ", ".join(b["ptypes"])]
        if not b.get("nobar"):
            L.append("\t-ProductionBar:")
    for exit_name, ptype in b.get("exits", []):
        L += ["\t%s:" % exit_name, "\t\tProductionTypes: %s" % ptype]
    L += ["\tAutoSquad:"]
    if b.get("ptype"):
        L.append("\t\tProductionType: %s" % b["ptype"])
    L += ["\t\tSpawnInterval: %d" % b["interval"], "\t\tInitialDelay: %d" % b["initial"]]
    if b.get("harvesters"):
        L.append("\t\tAffectsAllHarvesters: true")
    if b.get("overrides"):
        L.append("\t\tProductionTypeOverrides:")
        for u, t in b["overrides"]:
            L.append("\t\t\t%s: %s" % (u, t))
    if b["roster"]:
        L.append("\t\tRoster:")
        for unit, count in b["roster"]:
            L.append("\t\t\t%s: %d" % (unit, count))
    for cond, kind, amount in b.get("conditional", []):
        if kind == "power":
            L += ["\tPower@%s:" % cond, "\t\tAmount: %d" % amount, "\t\tRequiresCondition: %s" % cond]
        else:
            L += ["\tProvidesPrerequisite@%s:" % cond, "\t\tPrerequisite: %s" % cond,
                  "\t\tRequiresCondition: %s" % cond]
    L += nodes_yaml(b)
    return L


def yard_global_yaml():
    # Every placed building / defense reads the construction-speed prerequisites the Construction Yard grants.
    L = ["^BasicBuilding:"]
    for cond, kind, amount in YARD_CONDITIONALS:
        if kind == "time":
            L += ["\tProductionTimeMultiplier@%s:" % cond, "\t\tMultiplier: %d" % (100 - amount),
                  "\t\tPrerequisites: %s" % cond, "\t\tQueue: Building, Defense"]
    L.append("")
    return L


def rules_yaml():
    L = ["# Generated by tools/generate.py - edit the generator, not this file.", ""]
    L += ["^Infantry:", "\tSquadMember:", "",
          "^Vehicle:", "\tSquadMember:", "",
          "^Ship:", "\tSquadMember:", "",
          "^Plane:", "\tSquadMember:", "",
          "World:", "\tAutoForceUI:", ""]
    L += yard_global_yaml()
    # The classic buildings are replaced by the auto-training variants.
    for old in ("TENT", "BARR", "WEAP"):
        L += ["%s:" % old, "\tBuildable:", "\t\tPrerequisites: ~disabled", ""]
    # Refinery: keeps its free harvester, then builds one more over time (replaced if it dies).
    for ip in INPLACE:
        L += inplace_yaml(ip)
        L.append("")
    L += ["FIX:",
          "\tProduction:", "\t\tProduces: AutoMcv",
          "\tExit@1:", "\t\tProductionTypes: AutoMcv", "\t\tExitCell: 0,2",
          "\tExit@2:", "\t\tProductionTypes: AutoMcv", "\t\tExitCell: 2,2",
          "\tOnDemandBuild:", "\t\tUnit: MCV", "\t\tProductionType: AutoMcv",
          "\t\tDuration: %d" % (MCV_SECONDS * TICKS_PER_SECOND), "\t\tPayCost: true", ""]
    for b in BUILDINGS:
        L += building_yaml(b)
        L.append("")
    for name, parent, cost, _disp, _desc, extra in CUSTOM_UNITS:
        L += ["%s:" % name, "\tInherits: %s" % parent, "\t-Buildable:",
              "\tRenderSprites:", "\t\tImage: %s" % parent.lower(),
              "\tTooltip:", "\t\tName: actor-%s.name" % name.lower(),
              "\tSelectable:", "\t\tClass: %s" % name,
              "\tValued:", "\t\tCost: %d" % cost]
        L += extra
        L.append("")
    return "\n".join(L)


def sequences_yaml():
    by_image = {}
    for _actor, (_b, _c, icon) in BADGES.items():
        if icon:
            by_image.setdefault(icon[2], []).append(icon)
    L = ["# Generated by tools/generate.py. Extra sidebar icons borrowed from other Red Alert structures.", ""]
    for image in sorted(by_image):
        L.append("%s:" % image)
        for seq, filename, _img in by_image[image]:
            L += ["\t%s:" % seq, "\t\tFilename: %s" % filename, "\t\tTilesetFilenames:"]
        L.append("")
    return "\n".join(L)


def fluent():
    L = ["## AutoForce", ""]
    for b in BUILDINGS:
        k = b["actor"].lower()
        L += ["actor-%s =" % k, "    .name = %s" % b["name"], "    .description = %s" % b["desc"], ""]
    for name, _p, _c, disp, desc, _x in CUSTOM_UNITS:
        L += ["actor-%s =" % name.lower(), "    .name = %s" % disp, "    .description = %s" % desc, ""]
    return "\n".join(L)


def derive_mod_yaml(ra_text):
    t = ra_text
    t = t.replace("Title: mod-title", "Title: AutoForce (Red Alert)")
    t = t.replace("WindowTitle: mod-windowtitle", "WindowTitle: AutoForce")
    t = t.replace("\t\t$ra: ra\n", "\t\t$ra: ra\n\t\t$autoforce: autoforce\n", 1)
    t = t.replace("\tra|rules/fakes.yaml\n", "\tra|rules/fakes.yaml\n\tautoforce|rules/autoforce.yaml\n", 1)
    t = t.replace("\tra|sequences/misc.yaml\n", "\tra|sequences/misc.yaml\n\tautoforce|sequences/autoforce.yaml\n", 1)
    t = t.replace("\tra|weapons/superweapons.yaml\n", "\tra|weapons/superweapons.yaml\n\tautoforce|weapons/autoforce.yaml\n", 1)
    t = t.replace("\tra|fluent/rules.ftl\n", "\tra|fluent/rules.ftl\n\tautoforce|fluent/autoforce.ftl\n", 1)
    t = t.replace("Assemblies: OpenRA.Mods.Common.dll, OpenRA.Mods.Cnc.dll",
                  "Assemblies: OpenRA.Mods.Common.dll, OpenRA.Mods.Cnc.dll, OpenRA.Mods.AutoForce.dll", 1)
    for needle in ("autoforce|rules/autoforce.yaml", "autoforce|weapons/autoforce.yaml",
                   "autoforce|fluent/autoforce.ftl", "autoforce|sequences/autoforce.yaml",
                   "OpenRA.Mods.AutoForce.dll", "$autoforce: autoforce"):
        if needle not in t:
            raise SystemExit("mod.yaml derivation failed: could not apply %r (engine version differs?)" % needle)
    return t


def ra_actor_names(root):
    names = set()
    d = os.path.join(root, "mods", "ra", "rules")
    for f in os.listdir(d):
        if f.endswith(".yaml"):
            for line in open(os.path.join(d, f), encoding="utf-8"):
                m = re.match(r"^([A-Za-z0-9_\^]+):\s*$", line)
                if m:
                    names.add(m.group(1))
    return names


def validate(root):
    known = ra_actor_names(root) | {c[0] for c in CUSTOM_UNITS}
    problems = []
    for b in BUILDINGS:
        if b["parent"] not in known:
            problems.append("%s: parent %s unknown" % (b["actor"], b["parent"]))
        used = [u for u, _ in b["roster"]]
        for side in ("quality", "quantity"):
            if len(b[side]) != 9:
                problems.append("%s %s has %d nodes" % (b["actor"], side, len(b[side])))
            for n in b[side]:
                used += [n.get("unit"), n.get("frm")]
        for u in used:
            if u and u not in known:
                problems.append("%s: unit %s is not an RA actor or custom unit" % (b["actor"], u))
    for ip in INPLACE:
        used = [u for u, _ in ip["roster"]]
        if len(ip["quality"]) != len(ip["quantity"]):
            problems.append("%s: strands differ in length" % ip["actor"])
        for n in ip["quality"] + ip["quantity"]:
            used += [n.get("unit"), n.get("frm")]
        for u in used:
            if u and u not in known:
                problems.append("%s: unit %s unknown" % (ip["actor"], u))
        if ip["actor"] not in known:
            problems.append("in-place building %s unknown" % ip["actor"])
    for name, parent, *_ in CUSTOM_UNITS:
        if parent not in known:
            problems.append("custom unit %s: parent %s unknown" % (name, parent))
    return problems


def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    root = sys.argv[1]
    ra_mod = os.path.join(root, "mods", "ra", "mod.yaml")
    if not os.path.isfile(ra_mod):
        raise SystemExit("%s not found: pass the root of an OpenRA source checkout" % ra_mod)

    problems = validate(root)
    if problems:
        raise SystemExit("validation failed:\n  " + "\n  ".join(problems))
    if "--check" in sys.argv:
        print("validation OK (%d buildings, %d custom units)" % (len(BUILDINGS), len(CUSTOM_UNITS)))
        return

    out = os.path.join(root, "mods", "autoforce")
    for sub in ("rules", "weapons", "fluent", "sequences"):
        os.makedirs(os.path.join(out, sub), exist_ok=True)

    with open(ra_mod, encoding="utf-8") as f:
        mod_text = derive_mod_yaml(f.read())
    files = {
        "mod.yaml": mod_text,
        os.path.join("rules", "autoforce.yaml"): rules_yaml(),
        os.path.join("weapons", "autoforce.yaml"): WEAPONS,
        os.path.join("fluent", "autoforce.ftl"): fluent(),
        os.path.join("sequences", "autoforce.yaml"): sequences_yaml(),
    }
    for rel, text in files.items():
        with open(os.path.join(out, rel), "w", encoding="utf-8", newline="\n") as f:
            f.write(text if text.endswith("\n") else text + "\n")

    for icon in ("icon.png", "icon-2x.png", "icon-3x.png"):
        src = os.path.join(root, "mods", "ra", icon)
        if os.path.isfile(src):
            shutil.copy(src, os.path.join(out, icon))

    print("wrote", out)


if __name__ == "__main__":
    main()
