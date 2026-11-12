using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PatientHunting.Tests;

public static class Suite
{
    private static int checks;

    public static string Run()
    {
        checks = 0;
        Check(!HuntPolicy.BelowThreshold(int.MaxValue, 12), "non-bleeding prey is eligible");
        Check(!HuntPolicy.BelowThreshold(-1, 12), "invalid estimate does not suppress hunting");
        Check(!HuntPolicy.BelowThreshold(30000, 12), "exactly 12 hours remains eligible");
        Check(HuntPolicy.BelowThreshold(29999, 12), "one tick below 12 hours stops shooting");
        Check(HuntPolicy.BelowThreshold(0, 12), "imminent bleed-out stops shooting");
        Check(!HuntPolicy.BelowThreshold(30001, 12), "above 12 hours remains eligible");
        Check(HuntPolicy.BelowThreshold(14999, 6), "custom six-hour threshold works");
        Check(!HuntPolicy.BelowThreshold(15000, 6), "custom threshold remains strictly below");
        Check(HuntPolicy.NormalizeHours(0) == 1, "invalid low setting is clamped");
        Check(HuntPolicy.NormalizeHours(int.MaxValue) == 24, "invalid high setting cannot overflow");
        Check(HuntPolicy.DeferHunt(true, false, false), "standing bleeding prey is deferred");
        Check(!HuntPolicy.DeferHunt(true, true, false), "downed prey becomes eligible for execution");
        Check(HuntPolicy.DeferHunt(true, true, true), "dangerous downed prey cannot cause a shooting retry loop");
        Check(!HuntPolicy.DeferHunt(false, false, false), "hunting resumes when bleeding improves");
        Check(!HuntPolicy.DeferHunt(false, true, true), "healthy dangerous prey is unchanged");
        Check(new PatientHuntingSettings().BleedOutHours == 12, "new settings default to 12 hours");
        Check(!HuntingRules.AppliesTo(null), "missing hunter is ignored");
        Check(!HuntingRules.BelowThreshold(null), "missing prey is ignored");
        Check(!HuntingRules.ShouldBlockShot(new TestVerb()), "non-pawn caster is ignored");
        Check(!HuntingRules.ShouldDefer(null), "corpse/non-pawn target can proceed through vanilla hauling");
        Check(HuntingRules.CheckHunt(new JobDriver_Hunt()) == JobCondition.Ongoing, "uninitialized hunt driver does not fail during loading");

        // Install the actual production patches against the installed 1.7 game assembly
        // in this isolated test process. No connection to the running game is made.
        var harmony = new Harmony("aydhe.patienthunting.tests");
        var assembly = typeof(Suite).Assembly;
        try
        {
            harmony.PatchAll(assembly);
            var huntWork = AccessTools.Method(typeof(WorkGiver_HunterHunt), nameof(WorkGiver_HunterHunt.HasJobOnThing));
            var huntToils = AccessTools.Method(typeof(JobDriver_Hunt), "MakeNewToils");
            var burst = AccessTools.Method(typeof(Verb), "TryCastNextBurstShot");
            Check(Harmony.GetPatchInfo(huntWork)?.Owners.Contains(harmony.Id) == true, "hunting work patch binds to game API");
            Check(Harmony.GetPatchInfo(huntToils)?.Owners.Contains(harmony.Id) == true, "active hunt patch binds to game API");
            Check(Harmony.GetPatchInfo(burst)?.Owners.Contains(harmony.Id) == true, "burst transpiler compiles against real game IL");

            // Substitute only the health decision. Exercise the REAL patched burst method
            // with a harmless verb: no map, game session, graphics or real ammunition.
            var decision = AccessTools.Method(typeof(HuntingRules), nameof(HuntingRules.ShouldBlockShot));
            harmony.Patch(decision, prefix: new HarmonyMethod(typeof(Suite), nameof(TestDecision)));
            var shotsLeft = AccessTools.Field(typeof(Verb), "burstShotsLeft");
            var verb = new TestVerb();
            int completed = 0;
            verb.castCompleteCallback = () => completed++;
            verb.state = VerbState.Bursting;
            shotsLeft.SetValue(verb, 3);
            burst.Invoke(verb, null);
            Check(verb.Shots == 1, "eligible prey fires a normal shot");
            Check(verb.AmmoConsumed == 1, "normal shot consumes its ammo once");
            Check((int)shotsLeft.GetValue(verb)! == 2, "normal burst continues");
            Check(completed == 0, "unfinished burst does not complete early");

            verb.Block = true;
            burst.Invoke(verb, null);
            Check(verb.Shots == 1, "crossing threshold cancels remaining burst shots");
            Check(verb.AmmoConsumed == 1, "cancelled shot does not consume additional ammo");
            Check((int)shotsLeft.GetValue(verb)! == 0, "cancelled burst is drained");
            Check(verb.state == VerbState.Idle, "cancelled burst returns to idle");
            Check(completed == 1, "cancelled burst invokes vanilla completion callback");

            var warmup = new TestVerb { Block = true };
            shotsLeft.SetValue(warmup, 1);
            warmup.state = VerbState.Bursting;
            burst.Invoke(warmup, null);
            Check(warmup.Shots == 0 && warmup.AmmoConsumed == 0, "threshold crossed during warmup suppresses the first shot");

            // The guard must not change the game's normal failed-shot path either.
            var failed = new TestVerb { ShotSucceeds = false };
            int failedCompleted = 0;
            failed.castCompleteCallback = () => failedCompleted++;
            shotsLeft.SetValue(failed, 3);
            burst.Invoke(failed, null);
            Check(failed.Shots == 1 && failed.AmmoConsumed == 0, "ordinary failed shots retain original behavior");
            Check(failedCompleted == 1 && failed.state == VerbState.Idle, "ordinary failed burst completes normally");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
        return $"PASS: {checks} checks, including executing the patched RimWorld burst method. Live colony behavior still requires an in-game test.";
    }

    private static bool TestDecision(Verb verb, ref bool __result)
    {
        if (verb is not TestVerb test) return true;
        __result = test.Block;
        return false;
    }

    private static void Check(bool success, string description)
    {
        if (!success) throw new InvalidOperationException("FAIL: " + description);
        checks++;
    }

    private sealed class TestVerb : Verb
    {
        internal bool Block;
        internal bool ShotSucceeds = true;
        internal int Shots;
        internal int AmmoConsumed;
        public TestVerb()
        {
            verbTracker = new VerbTracker(null!);
            verbProps = new VerbProperties { muzzleFlashScale = 0, ticksBetweenBurstShots = 10 };
        }
        public override bool Available() => true;
        public override bool CasterIsPawn => false;
        protected override bool TryCastShot()
        {
            Shots++;
            if (ShotSucceeds) AmmoConsumed++;
            return ShotSucceeds;
        }
    }
}
