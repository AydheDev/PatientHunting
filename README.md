# Patient Hunting

A standalone RimWorld **1.7** mod. Requires **Harmony**. Version **0.1.0**.

Hunters stop shooting an animal when RimWorld's estimated time until death from blood loss is **less than 12 in-game hours**. They leave its hunt designation in place and choose other work. When it becomes downed, a hunter can return to execute it and haul the corpse using the normal hunting job.

## Setup

Place the `PatientHunting` folder in RimWorld's `Mods` directory. Restart RimWorld and enable **Patient Hunting**, below **Harmony** (and below **Progression: Ammunition**, if used). No ammunition mod is required. Open **Options → Mod settings → Patient Hunting** to enable/disable the rule or set its threshold from 1–24 hours.

## Behavior

- The threshold is strict: exactly 12 hours still permits shooting; less than 12 stops it.
- Uses the game's own blood-loss estimate, including the animal's current blood loss. It does not predict infection or other causes of death.
- Prevents assigning more hunters to the same standing, bleeding-out prey. It also stops an active hunt and checks before each new shot in a burst.
- Shots already in flight still land normally.
- Downed prey that vanilla considers dangerous to finish in melee remains deferred; the mod does not send hunters to execute explosive animals.
- If wounds heal, bleeding stops, or the estimate rises above the threshold, the animal becomes eligible for hunting again.
- Applies to player-faction hunting jobs, including manually prioritized hunts. Drafted attacks, combat/self-defense jobs, mental-state attacks, turrets, human targets and slaughter jobs are unchanged.
- Normal hunting priorities and availability still determine when a hunter returns. The mod does not assign a dedicated watcher or guarantee instant corpse collection.
- Does not add save data, custom jobs, items or buildings. Settings are stored in the normal mod configuration. No persistent list of deferred animals needs cleanup.

## Compatibility

Adds conditions to the existing hunting job and a small guard before the standard verb's shot attempt. It does not replace hunting toils or modify ammunition counts. A cancelled shot uses vanilla's normal failed-shot exit, preserving burst completion and cooldown handling.

Built against the installed RimWorld **1.7.5005** assemblies. Automated tests install the actual Harmony patches in a separate process and exercise the patched burst method. Progression: Ammunition's installed source was checked: its per-shot ammo consumption runs after a successful shot, which this guard prevents for deferred prey. This is not a claim of live validation with every installed mod. Mods that replace hunting jobs or bypass the standard burst method may need integration.

## In-game verification

1. Hunt an animal with a bow or gun. Once its health panel estimates less than 12 hours until death from blood loss, the hunter should stop and choose other work. The hunt designation should remain.
2. A second hunter should not repeatedly select that standing animal.
3. Once it becomes downed, normal hunting should become available again and perform the usual close-range finish and corpse hauling.
4. Try a burst weapon. No new shots should launch after the threshold is crossed; already airborne shots can still hit. Confirm ammunition is not spent on suppressed shots.
5. Draft a pawn and issue an attack on that animal: the manual combat order should still work.
6. Change the threshold or disable the mod and check that normal hunting becomes available again as appropriate. Save/reload with a deferred animal to verify it remains designated without becoming stuck.

## Build

Run `./build.ps1 -Tests` with PowerShell 7.6+ (.NET 10), then `./build.ps1`. Both accept `-RimWorldDir` and `-HarmonyDll`. Alternatively build `Source/PatientHunting.csproj` with the .NET 10 SDK and those MSBuild properties. Game and Harmony assemblies are referenced locally and are never bundled.

Run `./package.ps1 -Destination <zip-path>` after a production build. The package includes the source, tests and build scripts as well as the ready-to-load mod.
