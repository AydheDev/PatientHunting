using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

[assembly: AssemblyTitle("Patient Hunting")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

namespace PatientHunting;

public sealed class PatientHuntingSettings : ModSettings
{
    public bool Enabled = true;
    public int BleedOutHours = HuntPolicy.DefaultHours;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref Enabled, "enabled", true);
        Scribe_Values.Look(ref BleedOutHours, "bleedOutHours", HuntPolicy.DefaultHours);
        BleedOutHours = HuntPolicy.NormalizeHours(BleedOutHours);
        base.ExposeData();
    }
}

public sealed class PatientHuntingMod : Mod
{
    internal const string HarmonyId = "aydhe.patienthunting";
    internal static PatientHuntingSettings Settings = new PatientHuntingSettings();

    public PatientHuntingMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<PatientHuntingSettings>();
        new Harmony(HarmonyId).PatchAll(typeof(PatientHuntingMod).Assembly);
        Log.Message("[Patient Hunting] 0.1.0 loaded. Hunting bleed-out threshold: " + Settings.BleedOutHours + " hours.");
    }

    public override string SettingsCategory() => "Patient Hunting";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);
        listing.CheckboxLabeled("PH_Enabled".Translate(), ref Settings.Enabled);
        listing.Gap();
        listing.Label("PH_Threshold".Translate(Settings.BleedOutHours));
        Settings.BleedOutHours = HuntPolicy.NormalizeHours(Mathf.RoundToInt(listing.Slider(Settings.BleedOutHours, 1f, 24f)));
        listing.Gap();
        listing.Label("PH_Explanation".Translate());
        listing.Gap();
        if (listing.ButtonText("PH_Reset".Translate()))
        {
            Settings.Enabled = true;
            Settings.BleedOutHours = HuntPolicy.DefaultHours;
        }
        listing.End();
    }
}
