using RimWorld;
using Verse;
using Verse.AI;

namespace PatientHunting;

internal static class HuntingRules
{
    internal static bool AppliesTo(Pawn? hunter) =>
        PatientHuntingMod.Settings.Enabled && hunter != null &&
        hunter.Faction != null && hunter.Faction.IsPlayer &&
        !hunter.Drafted && !hunter.InMentalState;

    internal static bool BelowThreshold(Pawn? animal)
    {
        if (animal == null || animal.Dead || animal.Destroyed || !animal.Spawned ||
            animal.RaceProps?.Animal != true || animal.health?.hediffSet == null)
            return false;

        return HuntPolicy.BelowThreshold(HealthUtility.TicksUntilDeathDueToBloodLoss(animal),
            PatientHuntingMod.Settings.BleedOutHours);
    }

    internal static bool ShouldDefer(Pawn? animal)
    {
        if (!BelowThreshold(animal)) return false;
        // Vanilla deliberately avoids executing animals with a dangerous death action.
        // Keep them deferred instead of reassigning a hunt that can only shoot them.
        return HuntPolicy.DeferHunt(true, animal!.Downed,
            animal.RaceProps.DeathActionWorker?.DangerousInMelee == true);
    }

    internal static bool ShouldBlockShot(Verb verb)
    {
        // Check the job and target before reading health. No map scans or per-pawn cache.
        var hunter = verb.CasterPawn;
        var job = hunter?.CurJob;
        if (job == null || job.def != JobDefOf.Hunt || !AppliesTo(hunter) ||
            verb.verbProps == null || verb.verbProps.IsMeleeAttack)
            return false;

        var animal = job.targetA.Thing as Pawn;
        return animal != null && ReferenceEquals(verb.CurrentTarget.Thing, animal) && BelowThreshold(animal);
    }

    internal static JobCondition CheckHunt(JobDriver_Hunt driver)
    {
        if (driver.job == null || driver.job.def != JobDefOf.Hunt || !AppliesTo(driver.pawn) ||
            !ShouldDefer(driver.job.targetA.Thing as Pawn))
            return JobCondition.Ongoing;

        // Keep the hunting context until the shot guard ends an active burst through
        // vanilla's failed-shot path. Otherwise it could continue under the next job,
        // or a raw Reset would bypass completion callbacks and per-burst fuel handling.
        var verb = driver.job.verbToUse;
        if (verb?.Bursting == true && ReferenceEquals(verb.CurrentTarget.Thing, driver.job.targetA.Thing))
            return JobCondition.Ongoing;
        return JobCondition.InterruptForced;
    }
}
