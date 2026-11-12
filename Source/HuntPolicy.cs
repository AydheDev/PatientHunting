using System;

namespace PatientHunting;

internal static class HuntPolicy
{
    public const int DefaultHours = 12;
    public const int TicksPerHour = 2500;

    public static int NormalizeHours(int hours) => Math.Clamp(hours, 1, 24);

    public static bool BelowThreshold(int ticksUntilDeath, int hours) =>
        ticksUntilDeath >= 0 && ticksUntilDeath < NormalizeHours(hours) * TicksPerHour;

    public static bool DeferHunt(bool belowThreshold, bool downed, bool dangerousInMelee) =>
        belowThreshold && (!downed || dangerousInMelee);
}
