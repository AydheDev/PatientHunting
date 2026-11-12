using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PatientHunting;

[HarmonyPatch(typeof(WorkGiver_HunterHunt), nameof(WorkGiver_HunterHunt.HasJobOnThing))]
internal static class HuntAssignmentPatch
{
    internal static void Postfix(Pawn pawn, Thing t, bool forced, ref bool __result)
    {
        if (!__result || !HuntingRules.AppliesTo(pawn) || !HuntingRules.ShouldDefer(t as Pawn)) return;
        __result = false;
        if (forced) JobFailReason.Is("PH_WaitingForPrey".Translate());
    }
}

[HarmonyPatch(typeof(JobDriver_Hunt), "MakeNewToils")]
internal static class ActiveHuntPatch
{
    // Add a condition without replacing or enumerating another mod's hunting toils.
    internal static void Postfix(JobDriver_Hunt __instance) =>
        __instance.AddEndCondition(() => HuntingRules.CheckHunt(__instance));
}

[HarmonyPatch(typeof(Verb), "TryCastNextBurstShot")]
internal static class HuntingShotPatch
{
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var shot = AccessTools.Method(typeof(Verb), "TryCastShot");
        var guard = AccessTools.Method(typeof(HuntingRules), nameof(HuntingRules.ShouldBlockShot));
        var matches = codes.Select((code, index) => (code, index))
            .Where(pair => pair.code.Calls(shot)).Select(pair => pair.index).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException("Patient Hunting: expected one TryCastShot call in Verb.TryCastNextBurstShot; refusing to patch an unknown body.");

        int call = matches[0];
        if (call < 1 || call + 1 >= codes.Count || codes[call - 1].opcode != OpCodes.Ldarg_0 ||
            (codes[call + 1].opcode != OpCodes.Brfalse && codes[call + 1].opcode != OpCodes.Brfalse_S) ||
            codes[call + 1].operand is not Label failedShot)
            throw new InvalidOperationException("Patient Hunting: the shot failure branch has changed; refusing an unsafe patch.");

        // Reuse vanilla's failed-shot exit: it ends the burst, applies cooldown and
        // invokes the normal completion callback. Ammo-consuming TryCastShot never runs.
        var loadVerb = new CodeInstruction(OpCodes.Ldarg_0);
        loadVerb.labels.AddRange(codes[call - 1].labels);
        codes[call - 1].labels.Clear();
        loadVerb.blocks.AddRange(codes[call - 1].blocks);
        codes[call - 1].blocks.Clear();
        codes.InsertRange(call - 1, new[]
        {
            loadVerb,
            new CodeInstruction(OpCodes.Call, guard),
            new CodeInstruction(OpCodes.Brtrue, failedShot)
        });
        return codes;
    }
}
