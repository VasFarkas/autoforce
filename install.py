#!/usr/bin/env python3
"""Install AutoForce into an OpenRA source checkout and build it.

  python3 install.py <path-to-OpenRA-checkout> [--no-build]

Tested against the OpenRA release-20250330 source tree. Needs the .NET 6+ SDK (same as building OpenRA).
"""
import os
import shutil
import subprocess
import sys

here = os.path.dirname(os.path.abspath(__file__))


def run(cmd, cwd):
    print("+", " ".join(cmd))
    return subprocess.call(cmd, cwd=cwd)


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    if len(args) != 1:
        raise SystemExit(__doc__)
    root = os.path.abspath(args[0])
    if not os.path.isfile(os.path.join(root, "OpenRA.sln")) or not os.path.isfile(os.path.join(root, "mods", "ra", "mod.yaml")):
        raise SystemExit("%s does not look like an OpenRA source checkout (OpenRA.sln / mods/ra missing)" % root)

    dst = os.path.join(root, "OpenRA.Mods.AutoForce")
    if os.path.isdir(dst):
        shutil.rmtree(dst)
    shutil.copytree(os.path.join(here, "OpenRA.Mods.AutoForce"), dst,
                    ignore=shutil.ignore_patterns("bin", "obj"))
    print("copied C# project to", dst)

    if subprocess.call([sys.executable, "-I", os.path.join(here, "tools", "generate.py"), root]) != 0:
        raise SystemExit("rule generation failed")
    if subprocess.call([sys.executable, "-I", os.path.join(here, "tools", "check.py"), root]) != 0:
        raise SystemExit("static checks failed")

    sln_text = open(os.path.join(root, "OpenRA.sln"), encoding="utf-8-sig").read()
    if "OpenRA.Mods.AutoForce" not in sln_text:
        if shutil.which("dotnet") is None:
            raise SystemExit("dotnet SDK not found. Install the .NET SDK, then run:\n"
                             "  dotnet sln OpenRA.sln add OpenRA.Mods.AutoForce/OpenRA.Mods.AutoForce.csproj\n"
                             "and build OpenRA as usual (make / make.cmd).")
        if run(["dotnet", "sln", "OpenRA.sln", "add", "OpenRA.Mods.AutoForce/OpenRA.Mods.AutoForce.csproj"], root) != 0:
            raise SystemExit("could not add the project to OpenRA.sln")

    if "--no-build" not in sys.argv:
        windows = os.name == "nt"
        cmd = ["cmd", "/c", "make.cmd", "all"] if windows else ["make"]
        if run(cmd, root) != 0:
            raise SystemExit("build failed - see the compiler output above")

    launcher = "launch-game.cmd" if os.name == "nt" else "./launch-game.sh"
    print("\nDone. Start it with:\n  %s Game.Mod=autoforce" % launcher)
    print("(Red Alert content must already be installed, e.g. by launching the normal Red Alert mod once.)")


if __name__ == "__main__":
    main()
