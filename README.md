# AutoForce: an OpenRA Red Alert mod

Buildings train their own units. You never queue a unit. Instead you spend cash on a helix-shaped upgrade tree per building.

## Install (no tools: Windows players)

This repo contains a GitHub Actions workflow (`.github/workflows/build.yml`) that compiles the mod in the cloud. Upload this folder to a new GitHub repository, open the **Actions** tab, wait for the build, download the `AutoForce-for-OpenRA-20250330` artifact and follow `INSTALL-FOR-PLAYERS.txt` inside it. You only copy one DLL and one folder.

## Install (build it yourself)

1. Get the OpenRA source for **release-20250330** (`git clone --branch release-20250330 https://github.com/OpenRA/OpenRA`) and make sure it builds (needs the .NET SDK).
2. Run `python3 install.py <path-to-that-checkout>`. It copies the C# project in, generates the mod data from your checkout's own Red Alert files, runs static checks, registers the project in `OpenRA.sln` and builds.
3. Start with `./launch-game.sh Game.Mod=autoforce` (`launch-game.cmd Game.Mod=autoforce` on Windows). Red Alert content must be installed already (launch the normal Red Alert mod once).

## What you get

| Building | Squad (start) | Notes |
|---|---|---|
| Infantry Barracks (`AFINF`) | 8 rifle infantry | rifles to snipers / machine guns / grenadiers |
| Anti-Armor Barracks (`AFAT`) | 6 rocket + 2 rifle | tank hunters |
| Demolition Barracks (`AFDEMO`) | 4 flamers + 4 grenadiers | shock troops, dogs, commando |
| Main Factory (`AFMAIN`) | 4 medium tanks | heavy / mammoth, or light-tank swarm |
| Utility Factory (`AFUTIL`) | 2 scouts + 2 APCs | stealth, chrono, tesla escorts |
| Siege Factory (`AFSIEGE`) | 2 artillery + 2 V2 | V2 doctrine, MAD tanks |
| Refinery (`PROC`) | free harvester + 1 built over time (free) | replaced if it dies |

* A building keeps its squad at strength: when a unit dies it trains a replacement, one unit per interval, at the building's rally point.
* **Units are free.** The limit is the squad size: a building only trains until its squad is full, then replaces losses. Set `PayForUnits: true` on `AutoSquad` to charge normal unit prices instead.
* The original Barracks / War Factory are no longer buildable. The classic sidebar queues for infantry and vehicles stay empty.
* The new barracks and factories also count as a "tent" / "barracks" / "war factory" for tech purposes, so pillboxes, flame towers, turrets and the rest of the defenses stay buildable.

## Air, naval, refinery, yard and depot

These are the normal Red Alert buildings, upgraded in place (select one and its panel opens):

| Building | What it does |
|---|---|
| Helipad (`HPAD`) | keeps 2 Longbows; trees add Chinooks / Hind gunships. Needs a free pad, status line says so |
| Airfield (`AFLD`) | keeps 2 Yaks; trees convert / add MiGs |
| Naval Yard (`SYRD`) | destroyer + 2 gunboats; trees add destroyers / cruisers |
| Sub Pen (`SPEN`) | 2 submarines; trees add missile subs |
| Service Depot (`FIX`) | repairs as before, and a **Build MCV** button in its panel: 2 minute build, normal MCV price ($2000) paid when you press it |
| Ore Refinery (`PROC`) | 6-tier tree. Quality: harvester speed (+10% in total) and armor (-12% damage in total). Quantity: storage (+3000 in total) and up to 2 extra free harvesters |
| Construction Yard (`FACT`) | 6-tier tree. Quality: buildings and defenses about 24% faster to construct. Quantity: up to +140 power from the yard itself |

Harvester "unloading speed" is not an upgrade: the engine fixes it per harvester type, so it cannot change at runtime.

## The tree buttons and panel (bottom right)

A 3x3 grid of buttons (INFANTRY, FACTORY, HELIPAD, AIRFIELD, NAVAL, SUB PEN, REFINERY, YARD, DEPOT) sits in the bottom right corner of the screen, and the research panel opens directly above it, to the right of the build tabs. A button is greyed out while you own no building of that type; otherwise click it to open that tree (click again to cycle through your buildings of that type, "x2" shows the count). Selecting a building still opens its tree too.

## Two factions: Allies and Soviets

The country variants (England, France, Germany, Russia, Ukraine) are gone; you pick **Allies**, **Soviets** or Random. Buildings train only their own side's units:

| Building | Allies | Soviets |
|---|---|---|
| Infantry Barracks | rifle infantry, medics, snipers, machine gunners, rockets, Tanya | rifle infantry, grenadiers, snipers, machine gunners, shock troopers, engineers |
| Anti-Armor Barracks | rocket soldiers, tank hunters, medics, engineers | rocket soldiers, tank hunters, engineers |
| Demolition Barracks | rifle, rocket, engineer, mechanics, spies, Tanya | flamers, grenadiers, attack dogs, shock troopers |
| Main Factory | medium and Patton (new Allied heavy) tanks, light tanks, chrono tanks | heavy, Mammoth, tesla tanks, flak trucks |
| Utility Factory | scout vehicles, phase transports, radar jammer, gap generator, minelayers | armored transports, flak trucks, demolition truck, tesla, minelayers |
| Siege Factory | artillery, Patton and chrono tanks | V2 launchers, MAD and Mammoth tanks |

Same tree, same costs and times for both sides: only the unit each node gives differs. Maps that force a removed country as a player's faction will not load; normal skirmish maps use Random.

## Reinforcement cooldown

Barracks wait **30 s**, factories and the air and naval buildings **60 s** after a loss, then train every missing unit in quick succession (about half a second each). Losses during the wait join the same wave; losses during a wave start a new cooldown once it finishes. A full squad waits until someone dies. A new building starts with the cooldown too. Edit `INFANTRY_COOLDOWN_S`, `VEHICLE_COOLDOWN_S` and `WAVE_SPAWN_TICKS` in `tools/generate.py`. The refinery still trickles out harvesters one at a time.

## One tree for all buildings of a kind

Every upgrade tree is shared by all buildings it fits. Two Helipads, three Refineries or two Construction Yards each use ONE tree: research a node once and every building of that type gets it, including ones you build later. The barracks share one infantry tree and the factories one factory tree (below). Per-building effects stack: each Refinery adds its own extra harvesters and silo storage, each Yard its own power.

## Universal infantry and factory trees

The Infantry, Anti-Armor and Demolition Barracks share ONE infantry tree, and the Main, Utility and Siege Factories share ONE factory tree. Each node is researched once (one at a time for the whole tree):

* Speed / damage / fire-rate nodes apply to the squads of every building in the tree.
* Unit nodes (more units, swaps, majors) make every building in the tree add or swap its OWN units, e.g. the "Elite Training" major gives the Infantry Barracks snipers, the Anti-Armor Barracks tank hunters and the Demolition Barracks shock troopers. Hover a node to see what the building you are looking at gets.
* A barracks or factory built later automatically gets everything already researched.

## The helix menu

Select a building: the helix panel opens in the right-hand sidebar, just right of the build tabs, so the tabs (buildings / defenses ...) stay clickable. Clicking a tab, or the "< Build" button at the panel's top, flips back to the normal palette while the building stays selected. Left strand = **quantity**, right strand = **quality**. 9 tiers (6 for the refinery, yard and air / naval buildings), a **major** node on tiers 3, 6 and 9.

* **Pick one node per tier.** The other node on that tier locks.
* You stay on your strand, and may **cross to the other strand only right after a major**.
* Quality: faster, higher damage, faster fire, and majors that *replace* basic units with elites (rifle to sniper, medium tank to heavy).
* Quantity: more units, mixed-in support units, and majors that *add* a new unit type. "Reinforce Elites" nodes add more of your newest special unit, so quality then quantity gives you more snipers.
* Each upgrade costs cash up front and takes time to research (one at a time per building). Hover a node for details.

Edit the numbers, names and trees in `tools/generate.py`, then run `python3 install.py ...` again.

## Honest status

This was written without a .NET compiler available, so **it has never been compiled or run**. What was done instead: every engine API it uses was checked against the OpenRA 20250330 source, and `tools/check.py` cross-checks the generated YAML against the C# fields, RA's parent actors, fluent strings and weapons. If the build reports an error, it will be small (a missing `using`, a renamed member). Please send me the compiler output.

Skirmish AI: the bots build the new barracks and factories in place of the classic ones, their units come out automatically and join attack squads, and a bot module (`HelixResearchBotModule`) buys upgrades with spare cash. A bot never rebuilds a lost MCV (nothing can make one for it) and does not buy the Depot MCV.

Known limits: the factories and barracks reuse Red Alert art (same look as the originals, told apart by name and menu); sniper / machine gunner / tank hunter reuse the rifleman and rocket soldier sprites; balance numbers are first guesses.

## Telling the buildings apart

Each building carries a coloured text badge in the world (INF green, AT red, DEMO orange, MAIN blue, UTIL teal, SIEGE purple). The Demolition Barracks, Utility Factory and Siege Factory also use a different Red Alert structure icon in the build menu (kennel, service depot, soviet tech center), since new artwork can't be made here.

## If a building does not train units

Select it and look at the bottom of the sidebar panel. The status line says what it is doing ("Next: Rifle Infantry in 4s", "Squad is at full strength") or what is blocking it in red ("no free exit...", "production is disabled...", "unknown actor ..."). The small text at the top of the panel shows the build number (currently `AutoForce build 12`); if you do not see it, the old DLL is still being used. The same messages are written to OpenRA's `debug.log` (Windows: `%APPDATA%\OpenRA\Logs\`).
