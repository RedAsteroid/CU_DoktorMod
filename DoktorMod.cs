using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Kef.PluginRuntime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoktorMod;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(PluginRuntime.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
public sealed class DoktorModPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "kef.casualtiesunknown.doktormod";
    public const string PluginName = "DoktorMod";
    public const string PluginVersion = "0.3.0";
    private const float DefaultSyringeTextVerticalOffset = -76f;
    private const float DefaultSyringeTextHorizontalOffset = 28f;
    private const float DefaultTimedEffectIconScale = 0.66f;
    private const float DefaultDollOverlayX = 118f;
    private const float DefaultDollOverlayY = -300f;
    private const float DefaultDollOverlayScale = 0.34f;
    private const float DefaultDollStatusIconSize = 64f;

    internal static ManualLogSource Log;
    internal static ConfigEntry<bool> ShowSyringeInjectionMenu;
    internal static ConfigEntry<bool> ShowBandageTreatmentReadout;
    internal static ConfigEntry<float> SyringeTextVerticalOffset;
    internal static ConfigEntry<float> SyringeTextHorizontalOffset;
    internal static ConfigEntry<bool> ShowTimedEffectMoodles;
    internal static ConfigEntry<float> TimedEffectIconScale;
    internal static ConfigEntry<bool> RevealSmallInfections;
    internal static ConfigEntry<bool> ShowBodyDollOverlay;
    internal static ConfigEntry<float> DollOverlayX;
    internal static ConfigEntry<float> DollOverlayY;
    internal static ConfigEntry<float> DollOverlayScale;
    internal static ConfigEntry<float> DollStatusIconSize;
    internal static ConfigEntry<bool> ShowDollDuringInventoryHover;
    internal static ConfigEntry<bool> AlwaysShowOpiateLevel;
    private Harmony harmony;

    private void Awake()
    {
        Log = Logger;
        ShowSyringeInjectionMenu = Config.Bind(
            "Syringe minigame",
            "ShowInjectionMenu",
            true,
            "Shows syringe liquid remaining and total injected per liquid component during the syringe minigame.");
        ShowBandageTreatmentReadout = Config.Bind(
            "Bandage minigame",
            "ShowTreatmentReadout",
            true,
            "Shows bleed, skin, and muscle treatment progress during bandage and bruise-kit minigames.");
        SyringeTextVerticalOffset = Config.Bind(
            "Syringe minigame",
            "TextVerticalOffset",
            DefaultSyringeTextVerticalOffset,
            "Vertical offset from the native minigame item/percentage text to the syringe liquid readout.");
        SyringeTextHorizontalOffset = Config.Bind(
            "Syringe minigame",
            "TextHorizontalOffset",
            DefaultSyringeTextHorizontalOffset,
            "Horizontal offset from the native minigame item/percentage text to the syringe liquid readout.");
        ShowTimedEffectMoodles = Config.Bind(
            "Timed effect moodles",
            "ShowTimedEffectMoodles",
            true,
            "Adds native side moodles for fixed-duration medical and drug effects.");
        TimedEffectIconScale = Config.Bind(
            "Timed effect moodles",
            "IconScale",
            DefaultTimedEffectIconScale,
            "How much of each moodle badge the custom medicine/item sprite should fill.");
        RevealSmallInfections = Config.Bind(
            "Health visibility",
            "RevealSmallInfections",
            true,
            "Shows infection indicators immediately when infection is present instead of waiting for the vanilla 25% reveal threshold.");
        ShowBodyDollOverlay = Config.Bind(
            "Body doll overlay",
            "ShowOverlay",
            true,
            "Shows a small health-panel-style body doll while normal gameplay UI is active.");
        DollOverlayX = Config.Bind(
            "Body doll overlay",
            "OverlayX",
            DefaultDollOverlayX,
            "Saved body doll center X position in canvas units from the top-left anchor.");
        DollOverlayY = Config.Bind(
            "Body doll overlay",
            "OverlayY",
            DefaultDollOverlayY,
            "Saved body doll center Y position in canvas units from the top-left anchor.");
        DollOverlayScale = Config.Bind(
            "Body doll overlay",
            "OverlayScale",
            DefaultDollOverlayScale,
            "Saved body doll scale.");
        DollStatusIconSize = Config.Bind(
            "Body doll overlay",
            "StatusIconSize",
            DefaultDollStatusIconSize,
            "Size of bleed/infection/fracture/etc status icons on the body doll before doll scaling is applied.");
        ShowDollDuringInventoryHover = Config.Bind(
            "Body doll overlay",
            "ShowDuringInventoryHover",
            true,
            "Keeps the body doll visible while the inventory/radial hover UI is open.");
        AlwaysShowOpiateLevel = Config.Bind(
            "Diagnostics overlay",
            "AlwaysShowOpiateLevel",
            false,
            "Always shows the opiate candlestick meter on the health panel hidden-vitals overlay, even when no opiate amount or tolerance is present.");

        harmony = new Harmony(PluginGuid);
        harmony.PatchAll();
        PluginRuntime.RegisterUpdate(SyringeInjectionTracker.RuntimeUpdate);
        PluginRuntime.RegisterUpdate(BandageTreatmentReadoutController.RuntimeUpdate);
        PluginRuntime.RegisterUpdate(DollOverlayController.RuntimeUpdate);
        PluginRuntime.RegisterUpdate(HealthPanelAugmentController.RuntimeUpdate);
        PluginRuntime.RegisterUpdate(RemoteMoodlePanelController.RuntimeUpdate);
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private void OnDestroy()
    {
        Logger.LogInfo("Plugin component destroyed; KefPluginRuntime callback remains active.");
    }
}

[HarmonyPatch(typeof(PlayerCamera), "HandleLimbInfectionShows")]
internal static class PlayerCameraHandleLimbInfectionShowsPatch
{
    private static void Postfix(PlayerCamera __instance)
    {
        InfectionRevealController.RevealSmallInfections(__instance);
    }
}

internal static class InfectionRevealController
{
    public static void RevealSmallInfections(PlayerCamera camera)
    {
        if (!DoktorModPlugin.RevealSmallInfections.Value ||
            camera == null ||
            camera.body == null ||
            camera.body.limbs == null ||
            camera.showInfection == null)
        {
            return;
        }

        int count = Mathf.Min(camera.body.limbs.Length, camera.showInfection.Length);
        for (int i = 0; i < count; i++)
        {
            Limb limb = camera.body.limbs[i];
            if (limb != null && limb.infected && limb.infectionAmount > 0f)
            {
                camera.showInfection[i] = true;
            }
        }
    }
}

[HarmonyPatch(typeof(MoodleManager), "AddAllMoodles")]
internal static class MoodleManagerAddAllMoodlesPatch
{
    private const float EmbolismRiskViscosity = 90f;
    private static readonly AccessTools.FieldRef<MoodleManager, int> MoodleCount =
        AccessTools.FieldRefAccess<MoodleManager, int>("moodleCount");
    private static readonly AccessTools.FieldRef<MoodleManager, int> MainCount =
        AccessTools.FieldRefAccess<MoodleManager, int>("mainCount");

    private static void Postfix(MoodleManager __instance)
    {
        if (!DoktorModPlugin.ShowTimedEffectMoodles.Value ||
            __instance == null ||
            PlayerCamera.main == null ||
            PlayerCamera.main.body == null ||
            !PlayerCamera.main.body.alive)
        {
            return;
        }

        Body body = PlayerCamera.main.body;
        int before = MoodleCount(__instance);
        RemoveVanillaBonusBadge(__instance);
        UpdateDrugOverdoseDescription(__instance, body);
        UpdateToxicosisDescription(__instance, body);

        AddEarlyConditionMoodles(__instance, body);

        __instance.sideMoodles = true;
        AddAntibioticCoverage(__instance, body.antibioticImmunityTime);
        AddBloodPressureMedicine(__instance, body);
        AddAntidepressants(__instance, body);
        AddSleepingPills(__instance, body);
        AddOpiateExposure(__instance, body);
        AddTimedOp(__instance, "antirad", "doktor_antirad", "antirad", "irradiated",
            "Anti-radiation medicine", "Radiation sickness is being treated",
            overdoseLine: BuildDoseLine("before OD", null, DoseToThreshold(CoUtils.instance.DurationOf("antirad"), 180f, 4.5f)));
        AddTimedOp(__instance, "amiodarone", "doktor_amiodarone", "amiodarone", "arrythmia",
            "Amiodarone", "Arrhythmia treatment is active");
        AddTimedOp(__instance, "epinephrine", "doktor_epinephrine", "epinephrine", "fightorflight",
            "Epinephrine", "Adrenaline support is active");
        AddTimedOp(__instance, "oxyline", "doktor_oxyline", "oxyline", "oxygen",
            "Oxyline", "Oxygenation support is active");
        AddTimedOp(__instance, "procoagulant", "doktor_procoagulant", "bloodcoagulant", "bleeding",
            "Procoagulant", "Clotting support is active");
        AddTimedOp(__instance, "highgradestimulant", "doktor_highgradestim", "combatpen", "stimulants",
            "High-grade stimulant", "Stimulant effect is active",
            overdoseLine: BuildDoseLine("before OD",
                DoseToThreshold(CoUtils.instance.DurationOf("highgradestimulant"), 320f, 2.4f),
                DoseToThreshold(CoUtils.instance.DurationOf("highgradestimulant"), 320f, 2f)));
        AddTimedOp(__instance, "midgradestimulant", "doktor_midgradestim", "midgradestimulant", "stimulants",
            "Medical-grade stimulant", "Stimulant effect is active",
            overdoseLine: BuildDoseLine("before OD",
                DoseToThreshold(CoUtils.instance.DurationOf("midgradestimulant"), 220f, 3.6f), null));
        AddTimedOp(__instance, "lowgradestimulant", "doktor_lowgradestim", "lowgradestimulant", "stimulants",
            "Low-grade stimulant", "Stimulant effect is active",
            overdoseLine: BuildDoseLine("before OD",
                DoseToThreshold(CoUtils.instance.DurationOf("lowgradestimulant"), 160f, 3.25f),
                DoseToThreshold(CoUtils.instance.DurationOf("lowgradestimulant"), 160f, 2.5f)));
        AddTimedOp(__instance, "naltrexone", "doktor_naltrexone", "naltrexone", "withdrawal",
            "Naltrexone", "Opioid antagonist effect is active");
        AddTimedOp(__instance, "chloroform", "doktor_chloroform", "chloroform", "asleep",
            "Chloroform", "Sedative effect is active");
        AddTimedOp(__instance, "biochem", "doktor_biochem", "biochem", "sick",
            "Bio-chem ingestion", "Bio-chem exposure is causing harm", 3);
        AddTimedOp(__instance, "bleach", "doktor_bleach", "bleach", "sick",
            "Bleach ingestion", "Caustic ingestion is causing harm", 3);
        AddTimedOp(__instance, "oxylinedrink", "doktor_oxylinedrink", "oxyline", "oxygen",
            "Ingested oxyline", "Oral oxyline exposure is harming the lungs", 3);

        if (MoodleCount(__instance) != before)
        {
            RecreateBonusBadge(__instance);
        }
    }

    private static void AddTimedOp(MoodleManager manager, string opId, string iconKey, string itemResource,
        string fallbackIcon, string name, string descPrefix, int backgroundIntensity = 5, string overdoseLine = null)
    {
        float duration = CoUtils.instance.DurationOf(opId);
        AddBodyTimer(manager, duration, iconKey, itemResource, fallbackIcon, name, descPrefix, backgroundIntensity, overdoseLine);
    }

    private static void AddAntibioticCoverage(MoodleManager manager, float duration)
    {
        string antibioticId = AntibioticUseTracker.LastAntibioticId;
        if (!AntibioticUseTracker.IsAntibiotic(antibioticId))
        {
            antibioticId = "ceftriaxone";
        }

        AddBodyTimer(manager, duration, "doktor_antibiotics_" + antibioticId, antibioticId, "highimmunity",
            "Antibiotic coverage", GetAntibioticDisplayName(antibioticId) + " coverage is active");
    }

    private static void AddEarlyConditionMoodles(MoodleManager manager, Body body)
    {
        if (body.strokeAmount > 0.05f && body.strokeAmount <= 70f)
        {
            int intensity = Mathf.Clamp(Mathf.CeilToInt(body.strokeAmount / 25f) - 1, 0, 3);
            manager.AddMoodle(intensity, "stroke", "Stroke warning", $"Stroke progress: {body.strokeAmount:0}%.");
        }

        if (body.hasPulmonaryEmbolism && WorldGeneration.unchipped)
        {
            manager.AddMoodle(3, "pulmonaryembolism", "Embolism detected", "Pulmonary embolism detected.");
        }
        else if (!body.hasPulmonaryEmbolism && body.bloodViscosity > EmbolismRiskViscosity)
        {
            int intensity = body.bloodViscosity > 95f ? 3 : 2;
            manager.AddMoodle(intensity, "pulmonaryembolism", "Embolism risk", $"Blood viscosity: {body.bloodViscosity:0}%.");
        }
    }

    private static void UpdateDrugOverdoseDescription(MoodleManager manager, Body body)
    {
        if (manager.moodles == null)
        {
            return;
        }

        string description = BuildDrugOverdoseDescription(body);
        if (string.IsNullOrEmpty(description))
        {
            return;
        }

        foreach (Transform child in manager.moodles)
        {
            Moodle moodle = child != null ? child.GetComponent<Moodle>() : null;
            if (moodle == null || string.IsNullOrEmpty(moodle.type) ||
                !moodle.type.StartsWith("drugoverdose", StringComparison.Ordinal))
            {
                continue;
            }

            UITooltip tooltip = child.GetComponent<UITooltip>();
            if (tooltip != null)
            {
                tooltip.tipDesc = description;
            }
        }
    }

    private static void UpdateToxicosisDescription(MoodleManager manager, Body body)
    {
        if (manager.moodles == null)
        {
            return;
        }

        string description = BuildToxicosisDescription(body);
        foreach (Transform child in manager.moodles)
        {
            Moodle moodle = child != null ? child.GetComponent<Moodle>() : null;
            if (moodle == null || string.IsNullOrEmpty(moodle.type) ||
                !moodle.type.StartsWith("venom", StringComparison.Ordinal))
            {
                continue;
            }

            UITooltip tooltip = child.GetComponent<UITooltip>();
            if (tooltip != null)
            {
                tooltip.tipDesc = description;
            }
        }
    }

    private static string BuildToxicosisDescription(Body body)
    {
        const float toxinClearRate = 1f / 10.5f;
        const float currentTrackRate = 1f;
        const float viscosityRiseRate = 1f / 4.5f;

        float current = body.venomCurrent;
        float total = body.venomTotal;
        float oxygenCap = Mathf.Clamp(100f - current * 0.5f, 0f, 100f);
        float bloodDrain = current / 500f;
        float venomClottingMultiplier = Mathf.Clamp01(1f - current / 20f);
        float viscosityClottingMultiplier = Mathf.Clamp01((body.bloodViscosity + 100f) / 100f);
        float clottingRate = 0.025f * viscosityClottingMultiplier * venomClottingMultiplier *
            WorldGeneration.GetRunSettingFloat("healingrate");
        float currentChange = Mathf.MoveTowards(current, total, currentTrackRate) - current;
        float antivenomMl = Mathf.Max(0f, total) / 0.8f;

        StringBuilder sb = new StringBuilder(512);
        sb.Append("<color=#FFFFFF>Toxicosis: ").Append(current.ToString("0.#")).AppendLine("</color>");
        sb.Append("Toxin reservoir: ").Append(total.ToString("0.#")).AppendLine();
        sb.AppendLine("Current effects:");
        sb.Append("Blood oxygen ceiling: ").Append(oxygenCap.ToString("0.#")).AppendLine("%");
        if (body.bloodOxygen > oxygenCap)
        {
            sb.AppendLine("-0.7 blood oxygen/s toward ceiling");
        }
        sb.Append('-').Append(bloodDrain.ToString("0.###")).AppendLine(" blood volume/s");
        sb.Append("Blood viscosity target: ").Append(current.ToString("0.#"));
        if (body.bloodViscosity < current)
        {
            sb.Append(" (+").Append(viscosityRiseRate.ToString("0.###")).AppendLine("/s while below target)");
        }
        else
        {
            sb.AppendLine();
        }
        sb.Append("Natural wound clotting: ").Append((venomClottingMultiplier * 100f).ToString("0.#")).AppendLine("% from toxicosis");
        sb.Append("Current clotting rate: ").Append(clottingRate.ToString("0.####")).AppendLine(" bleed/s per wound");

        sb.AppendLine();
        sb.Append("Reservoir recovery: -").Append(toxinClearRate.ToString("0.###")).AppendLine("/s toward 0");
        sb.Append("Current tracking reservoir: ").Append(currentChange >= 0f ? "+" : "")
            .Append(currentChange.ToString("0.###")).AppendLine("/s");
        sb.Append("Antivenom to clear reservoir: ").Append(antivenomMl.ToString("0.#")).AppendLine("mL injected");
        sb.AppendLine();
        sb.Append("Tiers: 2 / 25 / 55 / 90. Clotting reaches 0% at 20.");
        return sb.ToString();
    }

    private static string BuildDrugOverdoseDescription(Body body)
    {
        StringBuilder sb = new StringBuilder(768);

        if (body.TryGetComponent(out Antidepressants antidepressants) && antidepressants.currentAmount >= 250f)
        {
            AppendOverdoseSection(sb, "Antidepressants",
                FormatSingleRouteOverdose(antidepressants.currentAmount, 250f, 5f));
            sb.AppendLine("Current effects:");
            sb.AppendLine("-3 blood pressure/s");
        }

        if (body.TryGetComponent(out SleepingPills sleepingPills))
        {
            float threshold = body.TryGetComponent(out Painkillers painkillers) && painkillers.actualOpiateReception > 15f
                ? 150f
                : 900f;
            if (sleepingPills.amount > threshold)
            {
                AppendOverdoseSection(sb, "Sleeping pills",
                    FormatSingleRouteOverdose(sleepingPills.amount, threshold, 60f));
                sb.AppendLine("Current effects:");
                sb.AppendLine(body.conscious ? "-2.5 blood pressure/s" : "-5 blood pressure/s");
                sb.AppendLine(body.conscious ? "Respiration moves toward 70 at 15/s" : "Respiration moves toward 40 at 15/s");
                if (!body.conscious)
                {
                    sb.AppendLine("-1 heart rate/s");
                }
            }
        }

        float antiradDuration = CoUtils.instance.DurationOf("antirad");
        if (antiradDuration > 180f)
        {
            AppendOverdoseSection(sb, "Anti-radiation medicine",
                FormatSingleRouteOverdose(antiradDuration, 180f, 4.5f));
            sb.AppendLine("Current effects:");
            sb.AppendLine("+0.6 sickness/s");
            sb.AppendLine("+1.5 thorax pain/s");
        }

        float highGradeDuration = CoUtils.instance.DurationOf("highgradestimulant");
        if (highGradeDuration > 320f)
        {
            AppendOverdoseSection(sb, "High-grade stimulant",
                FormatDualRouteOverdose(highGradeDuration, 320f, 2.4f, 2f));
            sb.AppendLine("Current effects:");
            sb.AppendLine("18% chance/s: +3 shaking");
            sb.AppendLine("6% chance/s: ragdoll");
            sb.AppendLine("5% chance/s: -50 consciousness");
            sb.AppendLine("5% chance/s: -1 temperature");
            sb.AppendLine("3% chance/s: reverse controls");
            sb.AppendLine("-0.1 stimulant multiplier/s (to -0.5)");
        }

        float midGradeDuration = CoUtils.instance.DurationOf("midgradestimulant");
        if (midGradeDuration > 220f)
        {
            AppendOverdoseSection(sb, "Medical-grade stimulant",
                FormatSingleRouteOverdose(midGradeDuration, 220f, 3.6f));
            sb.AppendLine("Current effects:");
            sb.AppendLine("+0.15 internal bleeding/s");
            sb.AppendLine("-0.05 brain health/s");
            sb.AppendLine("+4 thorax pain/s (while below 60)");
            sb.AppendLine("18% chance/s: +1.5 shaking");
            sb.AppendLine("10% chance/s: -35 stamina");
            sb.AppendLine("6% chance/s: ragdoll");
        }

        float lowGradeDuration = CoUtils.instance.DurationOf("lowgradestimulant");
        if (lowGradeDuration > 160f)
        {
            AppendOverdoseSection(sb, "Low-grade stimulant",
                FormatDualRouteOverdose(lowGradeDuration, 160f, 3.25f, 2.5f));
            sb.AppendLine("Current effects:");
            sb.AppendLine("+0.04 temperature/s");
            sb.AppendLine("-0.08 brain health/s");
            sb.AppendLine("+4 head and thorax pain/s (while below 60)");
            sb.AppendLine("20% chance/s: +1.5 shaking");
            sb.AppendLine("10% chance/s: -25 stamina");
            sb.AppendLine("10% chance/s: ragdoll");
            sb.AppendLine("7.5% chance/s: -3 blood oxygen");
            sb.AppendLine("6% chance/s: unconscious");
            sb.AppendLine("4% chance/s: -10 energy");
            sb.AppendLine("3.5% chance/s: vomit");
            sb.AppendLine("3% chance/s: reverse controls");
            sb.AppendLine("2% chance/s: clear adrenaline");
        }

        return sb.ToString();
    }

    private static void AppendOverdoseSection(StringBuilder sb, string name, string amount)
    {
        if (sb.Length > 0)
        {
            sb.AppendLine();
        }

        sb.Append("<color=#FFFFFF>").Append(name).Append(": ").Append(amount).AppendLine(" OD</color>");
    }

    private static string FormatSingleRouteOverdose(float current, float threshold, float amountPerMl)
    {
        return (Mathf.Max(0f, current - threshold) / amountPerMl).ToString("0.#") + "mL";
    }

    private static string FormatDualRouteOverdose(float current, float threshold, float injectedPerMl, float ingestedPerMl)
    {
        float excess = Mathf.Max(0f, current - threshold);
        return (excess / injectedPerMl).ToString("0.#") + "mL injected / " +
            (excess / ingestedPerMl).ToString("0.#") + "mL ingested";
    }

    private static void AddBodyTimer(MoodleManager manager, float duration, string iconKey, string itemResource,
        string fallbackIcon, string name, string descPrefix, int backgroundIntensity = 5, string overdoseLine = null)
    {
        if (duration <= 0.05f)
        {
            return;
        }

        string icon = EnsureIcon(manager, iconKey, itemResource, fallbackIcon);
        if (string.IsNullOrEmpty(icon))
        {
            return;
        }

        string desc = $"{descPrefix} for {FormatDuration(duration)}.";
        if (!string.IsNullOrEmpty(overdoseLine))
        {
            desc += "\n" + overdoseLine;
        }

        AddScaledMoodle(manager, backgroundIntensity, icon, name, desc);
    }

    private static void AddBloodPressureMedicine(MoodleManager manager, Body body)
    {
        float pressureMedicine = body.bloodPressureChangeFromMedicine;
        if (pressureMedicine > 0.5f)
        {
            AddBodyTimer(manager, pressureMedicine, "doktor_sodiumnitroprusside", "sodiumnitroprusside", "hypotension",
                "Sodium nitroprusside", "Blood pressure lowering effect is active");
        }
        else if (pressureMedicine < -0.5f)
        {
            AddBodyTimer(manager, -pressureMedicine, "doktor_vasopressin", "vasopressin", "hypertension",
                "Vasopressin", "Blood pressure raising effect is active");
        }
    }

    private static void AddAntidepressants(MoodleManager manager, Body body)
    {
        if (!body.TryGetComponent(out Antidepressants antidepressants) || antidepressants.amount <= 0.05f)
        {
            return;
        }

        AddBodyTimer(manager, antidepressants.amount / 0.185f, "doktor_antidepressants", "antidepressants", "happy",
            "Antidepressants", "Mood stabilizing effect is active", overdoseLine: BuildDoseLine("before OD", null,
                DoseToThreshold(Mathf.Max(antidepressants.currentAmount, antidepressants.amount), 250f, 5f)));
    }

    private static void AddSleepingPills(MoodleManager manager, Body body)
    {
        if (!body.TryGetComponent(out SleepingPills sleepingPills) || sleepingPills.amount <= 0.05f)
        {
            return;
        }

        float threshold = body.TryGetComponent(out Painkillers painkillers) && painkillers.actualOpiateReception > 15f ? 150f : 900f;
        AddBodyTimer(manager, sleepingPills.amount, "doktor_sleepingpills", "sleepingpills", "asleep",
            "Sleeping pills", "Sedative effect is active", overdoseLine: BuildDoseLine("before OD", null,
                DoseToThreshold(sleepingPills.amount, threshold, 60f)));
    }

    private static void AddOpiateExposure(MoodleManager manager, Body body)
    {
        if (!body.TryGetComponent(out Painkillers painkillers) ||
            Mathf.Max(painkillers.opiateAmount, Mathf.Abs(painkillers.opiateTolerance), Mathf.Abs(painkillers.actualOpiateReception)) <= 0.05f)
        {
            return;
        }

        float baseline = Mathf.Max(painkillers.actualOpiateReception, painkillers.opiateReception);
        string opiateId = OpiateUseTracker.LastOpiateId;
        if (!OpiateUseTracker.IsOpiateIconSource(opiateId))
        {
            opiateId = "painkillers";
        }

        string icon = EnsureIcon(manager, "doktor_opiate_" + opiateId, opiateId, "overdose");
        if (string.IsNullOrEmpty(icon))
        {
            return;
        }

        StringBuilder desc = new StringBuilder(256);
        desc.Append("Actual opiate reception: ").Append(painkillers.actualOpiateReception.ToString("0.#")).Append(".");
        desc.AppendLine();
        desc.Append("Opium: ").Append(BuildDoseLine("before OD",
            DoseToThreshold(baseline, 80f, 0.4f), DoseToThreshold(baseline, 80f, 0.2f)));
        desc.AppendLine();
        desc.Append("Morphine: ").Append(BuildDoseLine("before OD",
            DoseToThreshold(baseline, 80f, 0.9f), DoseToThreshold(baseline, 80f, 0.4f)));
        desc.AppendLine();
        desc.Append("Painkillers: ").Append(BuildDoseLine("before OD",
            null, DoseToThreshold(baseline, 80f, 1.4f)));
        desc.AppendLine();
        desc.Append("Heroin: ").Append(BuildDoseLine("before OD",
            DoseToThreshold(baseline, 80f, 1.3f), DoseToThreshold(baseline, 80f, 0.6f)));
        desc.AppendLine();
        desc.Append("Fentanyl: ").Append(BuildDoseLine("before OD",
            DoseToThreshold(baseline, 80f, 42f), DoseToThreshold(baseline, 80f, 40f)));
        AddScaledMoodle(manager, 5, icon, "Opiate exposure", desc.ToString());
    }

    private static void AddScaledMoodle(MoodleManager manager, int intensity, string icon, string name, string desc)
    {
        int childStart = manager.moodles != null ? manager.moodles.childCount : 0;
        manager.AddMoodle(intensity, icon, name, desc);
        ScaleNewMoodleIcons(manager, childStart);
    }

    private static void ScaleNewMoodleIcons(MoodleManager manager, int childStart)
    {
        if (manager.moodles == null)
        {
            return;
        }

        float scale = Mathf.Clamp(DoktorModPlugin.TimedEffectIconScale.Value, 0.1f, 1f);
        for (int i = childStart; i < manager.moodles.childCount; i++)
        {
            Transform moodle = manager.moodles.GetChild(i);
            if (moodle == null || moodle.GetComponent<Moodle>() == null || moodle.childCount == 0)
            {
                continue;
            }

            RectTransform badgeRect = moodle.GetComponent<RectTransform>();
            RectTransform iconRect = moodle.GetChild(0).GetComponent<RectTransform>();
            Image iconImage = moodle.GetChild(0).GetComponent<Image>();
            if (badgeRect == null || iconRect == null)
            {
                continue;
            }

            Vector2 badgeSize = badgeRect.sizeDelta;
            if (badgeSize.x <= 0f || badgeSize.y <= 0f)
            {
                badgeSize = Vector2.one * 70f;
            }

            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.localScale = Vector3.one;
            iconRect.sizeDelta = badgeSize * scale;
            if (iconImage != null)
            {
                iconImage.preserveAspect = true;
            }
        }
    }

    private static string EnsureIcon(MoodleManager manager, string iconKey, string itemResource, string fallbackIcon)
    {
        if (manager.icons.ContainsKey(iconKey))
        {
            return iconKey;
        }

        GameObject prefab = Resources.Load<GameObject>(itemResource);
        Sprite sprite = prefab != null ? prefab.GetComponent<SpriteRenderer>()?.sprite : null;
        if (sprite != null)
        {
            manager.icons[iconKey] = sprite;
            return iconKey;
        }

        return manager.icons.ContainsKey(fallbackIcon) ? fallbackIcon : null;
    }

    private static string GetAntibioticDisplayName(string id)
    {
        string localized = Locale.GetItem(id);
        return string.IsNullOrWhiteSpace(localized) ? "Antibiotic" : localized;
    }

    private static void RemoveVanillaBonusBadge(MoodleManager manager)
    {
        if (manager.moodles == null)
        {
            return;
        }

        foreach (Transform child in manager.moodles)
        {
            if (child != null && child.GetComponent<BonusMoodleShowScript>() != null)
            {
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }

    private static void RecreateBonusBadge(MoodleManager manager)
    {
        int hiddenCount = MoodleCount(manager) - MainCount(manager);
        if (hiddenCount <= 0 || manager.bonusMoodlePrefab == null || manager.moodles == null)
        {
            return;
        }

        GameObject bonus = UnityEngine.Object.Instantiate(manager.bonusMoodlePrefab, manager.moodles);
        bonus.GetComponent<RectTransform>().anchoredPosition = new Vector2(MainCount(manager) * 70 - 10, 0f);
        TextMeshProUGUI text = bonus.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = "+" + hiddenCount;
        }
    }

    private static string FormatDuration(float seconds)
    {
        TimeSpan span = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
        return span.TotalHours >= 1.0
            ? $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}"
            : $"{span.Minutes:D2}:{span.Seconds:D2}";
    }

    private static float? DoseToThreshold(float current, float threshold, float addedPerMl)
    {
        if (addedPerMl <= 0f)
        {
            return null;
        }

        return Mathf.Max(0f, threshold - current) / addedPerMl;
    }

    private static string BuildDoseLine(string suffix, float? injectMl, float? ingestMl)
    {
        return FormatDose(injectMl) + " to inject, " + FormatDose(ingestMl) + " to ingest " + suffix + ".";
    }

    private static string FormatDose(float? ml)
    {
        if (!ml.HasValue)
        {
            return "N/A";
        }

        return ml.Value <= 0f ? "OD now" : ml.Value.ToString("0.#") + "mL";
    }
}

[HarmonyPatch(typeof(WaterContainerItem), "Drink")]
internal static class WaterContainerItemDrinkPatch
{
    private static void Prefix(WaterContainerItem __instance, float amount)
    {
        AntibioticUseTracker.TrackFromContainer(__instance, amount);
        OpiateUseTracker.TrackFromContainer(__instance, amount);
    }
}

[HarmonyPatch(typeof(WaterContainerItem), "ApplyToLimb")]
internal static class WaterContainerItemApplyToLimbPatch
{
    private static void Prefix(WaterContainerItem __instance, float amount)
    {
        AntibioticUseTracker.TrackFromContainer(__instance, amount);
        OpiateUseTracker.TrackFromContainer(__instance, amount);
    }
}

[HarmonyPatch(typeof(WaterContainerItem), "Inject")]
internal static class WaterContainerItemInjectPatch
{
    private static void Prefix(WaterContainerItem __instance, float amount)
    {
        AntibioticUseTracker.TrackFromContainer(__instance, amount);
        OpiateUseTracker.TrackFromContainer(__instance, amount);
    }
}

[HarmonyPatch(typeof(PlayerCamera), "ApplyWoundItem")]
internal static class PlayerCameraApplyWoundItemPatch
{
    private static void Prefix(Item item)
    {
        if (item != null && item.id == "analgesicgauze")
        {
            OpiateUseTracker.TrackItem("analgesicgauze");
        }
        else if (item != null && (item.id == "lrd" || item.id == "makeshiftlrd"))
        {
            OpiateUseTracker.TrackItem("lrd");
        }
    }
}

internal static class AntibioticUseTracker
{
    public static string LastAntibioticId = "ceftriaxone";

    public static bool IsAntibiotic(string id)
    {
        return id == "antibiotics" || id == "ceftriaxone" || id == "antiserum";
    }

    public static void TrackFromContainer(WaterContainerItem container, float amount)
    {
        if (container == null || container.stack == null || container.stack.Count == 0 || amount <= 0f)
        {
            return;
        }

        List<float> drain = container.CalculateDrain(amount);
        if (drain.Count != container.stack.Count)
        {
            return;
        }

        for (int i = 0; i < container.stack.Count; i++)
        {
            LiquidStack stack = container.stack[i];
            if (stack != null && drain[i] > 0.0001f && IsAntibiotic(stack.liquidId))
            {
                LastAntibioticId = stack.liquidId;
            }
        }
    }
}

internal static class OpiateUseTracker
{
    public static string LastOpiateId = "painkillers";

    public static bool IsOpiateIconSource(string id)
    {
        return id == "analgesicgauze" || id == "lrd" || IsOpiate(id);
    }

    public static bool IsOpiate(string id)
    {
        if (string.IsNullOrEmpty(id) || !Liquids.Registry.TryGetValue(id, out LiquidType liquid) || liquid.qualities == null)
        {
            return false;
        }

        for (int i = 0; i < liquid.qualities.Count; i++)
        {
            CraftingQuality quality = liquid.qualities[i];
            if (quality != null && quality.id == "opiate")
            {
                return true;
            }
        }

        return false;
    }

    public static void TrackItem(string id)
    {
        if (id == "analgesicgauze" || id == "lrd")
        {
            LastOpiateId = id;
        }
    }

    public static void TrackFromContainer(WaterContainerItem container, float amount)
    {
        if (container == null || container.stack == null || container.stack.Count == 0 || amount <= 0f)
        {
            return;
        }

        List<float> drain = container.CalculateDrain(amount);
        if (drain.Count != container.stack.Count)
        {
            return;
        }

        for (int i = 0; i < container.stack.Count; i++)
        {
            LiquidStack stack = container.stack[i];
            if (stack != null && drain[i] > 0.0001f && IsOpiate(stack.liquidId))
            {
                LastOpiateId = stack.liquidId;
            }
        }
    }
}

internal static class HealthPanelAugmentController
{
    private const float EmbolismRiskViscosity = 90f;
    private const string PeTextName = "DoktorModPEText";
    private const string HemothoraxTextName = "DoktorModHemothoraxText";
    private const string DiagnosticsRootName = "DoktorModHiddenVitalsOverlay";
    private static readonly Color32 UiGreen = new Color32(76, 255, 119, 255);
    private static readonly Color32 UiYellow = new Color32(255, 247, 92, 255);
    private static readonly Color32 UiOrange = new Color32(255, 158, 73, 255);
    private static readonly Color32 UiRed = new Color32(255, 32, 32, 255);
    private static WoundView cachedView;
    private static string originalVersionText;
    private static Color originalVersionColor;
    private static TextMeshProUGUI peText;
    private static TextMeshProUGUI hemothoraxText;
    private static GameObject diagnosticsRoot;
    private static RectTransform diagnosticsRect;
    private static CandlestickWidget pressureWidget;
    private static CandlestickWidget viscosityWidget;
    private static CandlestickWidget opiateWidget;
    private static FibWidget fibWidget;
    private static Sprite solidSprite;
    private static float lastFibProgress;
    private static float lastFibTime;
    private static float smoothedFibRate;
    private static Body lastFibBody;

    public static void RuntimeUpdate()
    {
        WoundView view = WoundView.view;
        Body body = view != null ? view.body : null;
        if (view == null || body == null)
        {
            cachedView = null;
            peText = null;
            hemothoraxText = null;
            if (diagnosticsRoot != null)
            {
                diagnosticsRoot.SetActive(false);
            }
            return;
        }

        if (cachedView != view)
        {
            cachedView = view;
            originalVersionText = view.versionText != null ? view.versionText.text : "";
            originalVersionColor = view.versionText != null ? view.versionText.color : Color.white;
            peText = null;
            hemothoraxText = null;
            ClearDiagnosticsOverlay();
        }

        EnsureLabels(view);
        UpdateStrokeHeader(view, body);
        UpdatePulmonaryEmbolismText(body);
        UpdateHemothoraxText(body);
        EnsureDiagnosticsOverlay(view);
        UpdateDiagnosticsOverlay(view, body);
        UpdateHealthValueTooltips(view, body);
    }

    private static void EnsureLabels(WoundView view)
    {
        Transform statMenu = view.respiratoryText != null ? view.respiratoryText.transform.parent : view.transform.Find("StatMenu");
        if (statMenu == null)
        {
            return;
        }

        if (peText == null)
        {
            peText = FindOrCreateLabel(statMenu, PeTextName, view.respiratoryText);
        }
        ConfigureLabel(peText, view.respiratoryText, new Vector2(30f, 62f), new Vector2(360f, 32f),
            TextAlignmentOptions.Left, new Vector2(0f, 0.5f), 0.86f);

        if (hemothoraxText == null)
        {
            hemothoraxText = FindOrCreateLabel(statMenu, HemothoraxTextName, view.respiratoryText);
        }
        ConfigureLabel(hemothoraxText, view.respiratoryText, new Vector2(232f, 26f), new Vector2(285f, 32f),
            TextAlignmentOptions.Right, new Vector2(1f, 0.5f));
    }

    private static TextMeshProUGUI FindOrCreateLabel(Transform parent, string name, TextMeshProUGUI template)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out TextMeshProUGUI existingText))
        {
            return existingText;
        }

        GameObject labelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        labelObject.layer = parent.gameObject.layer;
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        RectTransform rect = label.rectTransform;

        if (template != null)
        {
            label.font = template.font;
            label.fontSharedMaterial = template.fontSharedMaterial;
            label.fontSize = template.fontSize;
            label.alignment = TextAlignmentOptions.Left;
        }
        else
        {
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.Left;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(190f, -210f);
        }

        rect.sizeDelta = new Vector2(285f, 32f);
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        labelObject.SetActive(false);
        return label;
    }

    private static void ConfigureLabel(TextMeshProUGUI label, TextMeshProUGUI template, Vector2 offsetFromTemplate,
        Vector2 size, TextAlignmentOptions alignment, Vector2 pivot, float fontSizeScale = 1f)
    {
        if (label == null)
        {
            return;
        }

        RectTransform rect = label.rectTransform;
        if (template != null)
        {
            RectTransform templateRect = template.rectTransform;
            rect.anchorMin = templateRect.anchorMin;
            rect.anchorMax = templateRect.anchorMax;
            rect.anchoredPosition = templateRect.anchoredPosition + offsetFromTemplate;
            label.fontSize = template.fontSize;
        }

        rect.pivot = pivot;
        rect.sizeDelta = size;
        label.alignment = alignment;
        label.enableWordWrapping = false;
        label.enableAutoSizing = true;
        label.fontSizeMax = (template != null ? template.fontSize : 24f) * fontSizeScale;
        label.fontSizeMin = 14f;
    }

    private static void UpdateStrokeHeader(WoundView view, Body body)
    {
        if (view.versionText == null)
        {
            return;
        }

        if (body.strokeAmount > 0.05f)
        {
            view.versionText.text = $"STROKE {body.strokeAmount:0}%";
            view.versionText.color = Color.red;
            view.versionText.enabled = Mathf.Sin(Time.unscaledTime * 20f) > 0f;
            return;
        }

        if (view.versionText.text.StartsWith("STROKE", StringComparison.Ordinal))
        {
            view.versionText.text = string.IsNullOrWhiteSpace(WoundView.firmwareVer) ? originalVersionText : "UNI-HEALTH " + WoundView.firmwareVer;
        }

        view.versionText.enabled = true;
        view.versionText.color = originalVersionColor;
    }

    private static void UpdatePulmonaryEmbolismText(Body body)
    {
        if (peText == null)
        {
            return;
        }

        bool active = body.hasPulmonaryEmbolism || body.bloodViscosity > EmbolismRiskViscosity;
        peText.gameObject.SetActive(active);
        if (!active)
        {
            peText.enabled = true;
            return;
        }

        if (body.hasPulmonaryEmbolism)
        {
            peText.text = "EMBOLISM FOUND";
            peText.color = UiRed;
            peText.enabled = Mathf.Sin(Time.unscaledTime * 20f) > 0f;
            return;
        }

        peText.text = "EMBOLISM RISK";
        peText.color = UiOrange;
        peText.enabled = true;
    }

    private static void UpdateHemothoraxText(Body body)
    {
        if (hemothoraxText == null)
        {
            return;
        }

        bool active = body.hemothorax > 0.05f;
        hemothoraxText.gameObject.SetActive(active);
        if (!active)
        {
            hemothoraxText.enabled = true;
            return;
        }

        hemothoraxText.enabled = true;
        hemothoraxText.text = $"{body.hemothorax:0}% Hemothorax";
        hemothoraxText.color = HemothoraxColor(body.hemothorax);
    }

    private static Color HemothoraxColor(float hemothorax)
    {
        if (hemothorax >= 75f)
        {
            return UiRed;
        }

        if (hemothorax >= 50f)
        {
            return UiOrange;
        }

        if (hemothorax >= 25f)
        {
            return UiYellow;
        }

        return UiGreen;
    }

    private static void EnsureDiagnosticsOverlay(WoundView view)
    {
        if (diagnosticsRoot != null)
        {
            return;
        }

        Transform parent = view.transform.Find("ComputerBack") ?? view.transform;
        diagnosticsRoot = new GameObject(DiagnosticsRootName, typeof(RectTransform));
        diagnosticsRoot.transform.SetParent(parent, false);
        diagnosticsRoot.layer = parent.gameObject.layer;
        diagnosticsRect = diagnosticsRoot.GetComponent<RectTransform>();
        diagnosticsRect.anchorMin = new Vector2(1f, 1f);
        diagnosticsRect.anchorMax = new Vector2(1f, 1f);
        diagnosticsRect.pivot = new Vector2(0f, 1f);
        diagnosticsRect.anchoredPosition = new Vector2(-42f, -1f);
        diagnosticsRect.sizeDelta = new Vector2(340f, 620f);

        TextMeshProUGUI title = CreateOverlayLabel(diagnosticsRect, "BLOOD", view.versionText, new Vector2(0f, 0f),
            new Vector2(220f, 38f), 34f, TextAlignmentOptions.Left, UiGreen);
        title.fontSizeMin = 18f;

        pressureWidget = CreateCandlestickWidget("PRESS", new Vector2(-50f, -62f), view.versionText);
        viscosityWidget = CreateCandlestickWidget("VISC", new Vector2(46f, -62f), view.versionText);
        opiateWidget = CreateCandlestickWidget("OPIATE", new Vector2(-50f, -270f), view.versionText);
        fibWidget = CreateFibWidget(new Vector2(-120f, -464f), view.versionText);  //fib widget position 
    }

    private static void UpdateDiagnosticsOverlay(WoundView view, Body body)
    {
        if (diagnosticsRoot == null)
        {
            return;
        }

        bool show = view.gameObject.activeInHierarchy;
        diagnosticsRoot.SetActive(show);
        if (!show)
        {
            return;
        }

        pressureWidget.Update(body.bloodPressure, 40f, 220f, 96f, 145f, 96f, 145f, 70f, 180f, 110f, 130f,
            string.Empty);
        viscosityWidget.Update(body.bloodViscosity, -100f, 100f, -25f, 50f, -60f, 80f, -90f, 95f, -25f, 50f,
            $"{body.bloodViscosity:0}");
        SetTooltip(pressureWidget.Root, "Blood pressure", BuildBloodPressureTooltip(body));
        SetTooltip(viscosityWidget.Root, "Blood viscosity", BuildViscosityTooltip(body));

        Painkillers painkillers = null;
        bool hasOpiate = body.TryGetComponent(out painkillers) && Mathf.Abs(painkillers.opiateTolerance) > 0.001f;
        bool showOpiate = hasOpiate || DoktorModPlugin.AlwaysShowOpiateLevel.Value;
        opiateWidget.Root.SetActive(showOpiate);
        if (showOpiate)
        {
            float reception = painkillers != null ? painkillers.actualOpiateReception : 0f;
            float tolerance = painkillers != null ? painkillers.opiateTolerance : 0f;
            opiateWidget.Update(reception, -60f, 90f, -15f, 80f, -25f, 50f, -34f, 80f, -15f, 80f,
                $"Rec: {reception:0.#}\nTol: {tolerance * 0.01f:0.0}u");
            SetTooltip(opiateWidget.Root, "Opiate reception", BuildOpiateTooltip(painkillers));
        }

        bool hasFib = body.fibrillationProgress > 0.05f;
        fibWidget.Root.SetActive(hasFib);
        if (hasFib)
        {
            float now = Time.unscaledTime;
            float deltaTime = lastFibTime > 0f ? Mathf.Max(0.001f, now - lastFibTime) : 1f;
            float rate = (body.fibrillationProgress - lastFibProgress) / deltaTime;
            if (lastFibBody != body || lastFibTime <= 0f)
            {
                smoothedFibRate = rate;
            }
            else
            {
                float smoothing = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f);
                smoothedFibRate = Mathf.Lerp(smoothedFibRate, rate, smoothing);
            }
            lastFibBody = body;
            lastFibProgress = body.fibrillationProgress;
            lastFibTime = now;
            Vector2 ecgRange = SampleEcgRange(body);
            fibWidget.Update(body.fibrillationProgress, smoothedFibRate, ecgRange.x, ecgRange.y);
            SetTooltip(fibWidget.Root, "Fibrillation", BuildFibrillationTooltip(body, smoothedFibRate));
        }
        else
        {
            lastFibProgress = body.fibrillationProgress;
            lastFibTime = Time.unscaledTime;
            smoothedFibRate = 0f;
            lastFibBody = null;
        }
    }

    private static void ClearDiagnosticsOverlay()
    {
        if (diagnosticsRoot != null)
        {
            UnityEngine.Object.Destroy(diagnosticsRoot);
        }

        diagnosticsRoot = null;
        diagnosticsRect = null;
        pressureWidget = null;
        viscosityWidget = null;
        opiateWidget = null;
        fibWidget = null;
        lastFibProgress = 0f;
        lastFibTime = 0f;
        smoothedFibRate = 0f;
        lastFibBody = null;
    }

    private static Vector2 SampleEcgRange(Body body)
    {
        float low = 1f;
        float high = -1f;
        for (int i = 0; i < 18; i++)
        {
            float height = body.GetECGHeight(i / 17f);
            if (height < low)
            {
                low = height;
            }

            if (height > high)
            {
                high = height;
            }
        }

        return new Vector2(Mathf.Clamp(low, -1f, 1f), Mathf.Clamp(high, -1f, 1f));
    }

    private static void UpdateHealthValueTooltips(WoundView view, Body body)
    {
        string moodTooltip = BuildMoodTooltip(body);
        SetTooltip(view.happyText, "Mood", moodTooltip);
        if (view.happinessIcon != null)
        {
            view.happinessIcon.raycastTarget = true;
            SetTooltip(view.happinessIcon.gameObject, "Mood", moodTooltip);
        }

        SetTooltip(view.energyText, "Energy", BuildEnergyTooltip(body));
        SetTooltip(view.immunityText, "Immunity", BuildImmunityTooltip(body));
        SetTooltip(view.painText, "Pain", BuildPainTooltip(body));
        SetTooltip(view.weightText, "Weight", BuildWeightTooltip(body));
        SetTooltip(view.sickText, "Sickness", BuildSicknessTooltip(body));
        SetTooltip(view.brainHealthText, "Brain health", BuildBrainTooltip(body));
        SetHeartPressureTooltips(view, body);
        SetTooltip(view.oxyText, "Blood oxygen", BuildOxygenTooltip(body));
        SetTooltip(view.bleedText, "Bleeding", BuildBleedingTooltip(body));
        SetTooltip(view.bloodText, "Blood volume", BuildBloodVolumeTooltip(body));
        SetTooltip(view.hungerText, "Hunger", BuildHungerTooltip(body));
        SetTooltip(view.thirstText, "Thirst", BuildThirstTooltip(body));
        SetTooltip(view.limbForceText, "Limb strength", BuildLimbStrengthTooltip(view, body));
        SetTooltip(view.radText, "Radiation", BuildRadiationTooltip(body));
        SetTooltip(view.tempText, "Temperature", BuildTemperatureTooltip(body));
    }

    private static string BuildMoodTooltip(Body body)
    {
        float bleeding = body.happiness < -50f ? -body.totalBleedSpeed * 15f : 0f;
        float pain = -body.averagePain * 0.1f;
        float sickness = -body.sicknessAmount * 0.1f;
        float hunger = -(1f - Mathf.Clamp01(body.hunger * 0.01f + 0.6f)) * 18f;
        float thirst = -(1f - Mathf.Clamp01(Mathf.Min(body.thirst, 100f) * 0.01f + 0.6f)) * 18f;
        float radiation = -body.radiationSickness * 0.1f;
        float hearing = -body.hearingLoss * 0.2f;
        float blood = -(100f - Mathf.Min(body.bloodVolume, 100f)) * 0.2f;
        float trauma = -body.traumaAmount * 0.525f;
        float wetness = -body.wetness * 0.05f;
        float raw = body.happiness + bleeding + pain + sickness + hunger + thirst + radiation + hearing + blood +
            trauma + wetness + body.opiateHappiness + body.antidepressantHappiness;
        float clamped = Mathf.Clamp(raw, -100f, 100f);
        float horrorFactor = 1f - body.horrifiedLevel * 0.005f;

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Current", body.totalHappiness.ToString("0.#"));
        AppendLine(sb, "Base happiness", Signed(body.happiness));
        AppendLine(sb, "Bleeding", Signed(bleeding));
        AppendLine(sb, "Pain", Signed(pain));
        AppendLine(sb, "Sickness", Signed(sickness));
        AppendLine(sb, "Hunger", Signed(hunger));
        AppendLine(sb, "Thirst", Signed(thirst));
        AppendLine(sb, "Radiation", Signed(radiation));
        AppendLine(sb, "Hearing loss", Signed(hearing));
        AppendLine(sb, "Blood loss", Signed(blood));
        AppendLine(sb, "Trauma", Signed(trauma));
        AppendLine(sb, "Wetness", Signed(wetness));
        AppendLine(sb, "Opiates", Signed(body.opiateHappiness));
        AppendLine(sb, "Antidepressants", Signed(body.antidepressantHappiness));
        AppendLine(sb, "Raw / clamped", $"{raw:0.#} / {clamped:0.#}");
        if (body.mindWipe)
        {
            AppendLine(sb, "Mindwipe", "x0");
        }
        if (!Mathf.Approximately(horrorFactor, 1f))
        {
            AppendLine(sb, "Horror", $"x{horrorFactor:0.###}");
        }

        float metabolism = WorldGeneration.GetRunSettingFloat("metabolismrate");
        sb.AppendLine();
        if (!body.conscious)
        {
            float normalization = body.sleeping
                ? 0.01f * (body.happiness < 0f ? 1f : 0.5f) * WorldGeneration.GetRunSettingFloat("moodnormalizationrate")
                : 0f;
            AppendLine(sb, "State", body.sleeping ? "Sleeping" : "Unconscious");
            AppendLine(sb, "Base normalization", normalization > 0f ? $"{normalization:0.###}/s toward 0" : "None");
        }
        else
        {
            float sicknessDrain = body.sicknessAmount > 20f
                ? Mathf.Clamp01(body.sicknessAmount * 0.01f) * 0.05f * metabolism
                : 0f;
            float hungerDrain = Mathf.Clamp01(0.65f - body.hunger * 0.01f) * 0.065f * metabolism;
            float thirstDrain = Mathf.Clamp01(0.65f - Mathf.Min(body.thirst, 120f) * 0.01f) * 0.065f * metabolism;
            float painDrain = body.averagePain > 50f ? body.averagePain * 0.001f : 0f;
            float bleedDrain = body.happiness > -50f ? Mathf.Clamp01(body.totalBleedSpeed) * 0.12f : 0f;
            float temperatureDrain = body.temperature < 33.5f || body.temperature > 40f ? 0.03f : 0f;
            float wellFedGain = body.hunger > 101f ? body.hunger * 0.0001f * metabolism : 0f;
            float directRate = wellFedGain - sicknessDrain - hungerDrain - thirstDrain - painDrain - bleedDrain -
                temperatureDrain;

            AppendLine(sb, "Direct change", SignedFine(directRate, "/s"));
            AppendLine(sb, "Sickness drain", $"-{sicknessDrain:0.###}/s");
            AppendLine(sb, "Hunger drain", $"-{hungerDrain:0.###}/s");
            AppendLine(sb, "Thirst drain", $"-{thirstDrain:0.###}/s");
            AppendLine(sb, "Pain drain", $"-{painDrain:0.###}/s");
            AppendLine(sb, "Bleeding drain", $"-{bleedDrain:0.###}/s");
            AppendLine(sb, "Temperature drain", $"-{temperatureDrain:0.###}/s");
            AppendLine(sb, "Well-fed gain", $"+{wellFedGain:0.###}/s");
            sb.AppendLine("Base happiness also slowly normalizes toward 0.");
        }

        sb.AppendLine();
        sb.Append("Icon tiers: >50, >10, >-10, >-40, >-75, and critical. ")
            .Append("At -75 or below, the medical cutoff is failed.");
        return sb.ToString();
    }

    private static string BuildEnergyTooltip(Body body)
    {
        float sleepCycleSpeed = WorldGeneration.GetRunSettingFloat("sleepcyclespeed");
        Painkillers painkillers = body.GetComponent<Painkillers>();
        float opiateDrain = painkillers != null && painkillers.actualOpiateReception > 30f
            ? painkillers.actualOpiateReception * 0.0025f
            : 0f;

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Current", $"{body.energy:0.#}%");

        if (!body.alive)
        {
            AppendLine(sb, "State", "Dead");
            AppendLine(sb, "Change", opiateDrain > 0f ? $"-{opiateDrain:0.###}/s from opiates" : "None");
        }
        else if (!body.conscious)
        {
            Body.SleepQuality quality = body.forcedSleepQuality ?? body.curSleep;
            float qualityMultiplier = body.SleepQualityToRegen(quality);
            float regeneration = 0.4f * qualityMultiplier * sleepCycleSpeed;
            float net = regeneration - opiateDrain;
            AppendLine(sb, "State", body.sleeping ? $"Sleeping ({quality})" : $"Unconscious ({quality})");
            AppendLine(sb, "Base sleep regen", "+0.4/s");
            AppendLine(sb, "Sleep quality", $"x{qualityMultiplier:0.##}");
            AppendLine(sb, "Sleep-cycle setting", $"x{sleepCycleSpeed:0.##}");
            if (opiateDrain > 0f)
            {
                AppendLine(sb, "High-opiate drain", $"-{opiateDrain:0.###}/s");
            }
            AppendLine(sb, "Net change", SignedFine(net, "/s"));
        }
        else
        {
            float staminaFactor = 2f - body.stamina * 0.01f;
            float sicknessFactor = 1f + body.sicknessAmount * 0.02f;
            float moodFactor = 1f - Math.Clamp(body.totalHappiness * 0.01f, -1f, 0f);
            float caffeineFactor = body.caffeinated > 0f ? 0.55f : 1f;
            float drain = 0.07f * sleepCycleSpeed * staminaFactor * sicknessFactor * moodFactor * caffeineFactor;
            float netDrain = drain + opiateDrain;

            AppendLine(sb, "State", "Awake");
            AppendLine(sb, "Base drain", "-0.07/s");
            AppendLine(sb, "Low stamina", $"x{staminaFactor:0.###}");
            AppendLine(sb, "Sickness", $"x{sicknessFactor:0.###}");
            AppendLine(sb, "Negative mood", $"x{moodFactor:0.###}");
            AppendLine(sb, "Caffeine", $"x{caffeineFactor:0.##}");
            AppendLine(sb, "Sleep-cycle setting", $"x{sleepCycleSpeed:0.##}");
            if (opiateDrain > 0f)
            {
                AppendLine(sb, "High-opiate drain", $"-{opiateDrain:0.###}/s");
            }
            AppendLine(sb, "Net change", $"-{netDrain:0.###}/s");
        }

        Body.SleepQuality currentQuality = body.forcedSleepQuality ?? body.curSleep;
        float normalWakeTarget = currentQuality == Body.SleepQuality.Bad
            ? 70f
            : currentQuality == Body.SleepQuality.Mediocre ? 85f : 99f;
        sb.AppendLine();
        sb.Append("Below 35% you can normally choose to sleep. Normal wake target for ")
            .Append(currentQuality).Append(" sleep: ").Append(normalWakeTarget.ToString("0")).Append("%. ")
            .Append("Energy also affects immunity, stamina recovery, body heat, and consciousness.");
        return sb.ToString();
    }

    private static void SetTooltip(TextMeshProUGUI label, string title, string desc)
    {
        if (label == null)
        {
            return;
        }

        label.raycastTarget = true;
        SetTooltip(label.gameObject, title, desc);
    }

    private static void SetHeartPressureTooltips(WoundView view, Body body)
    {
        TextMeshProUGUI label = view != null ? view.bpmText : null;
        if (label == null)
        {
            return;
        }

        SetTooltip(label, "Heart rate", BuildHeartRateTooltip(body));
        Transform pressureHover = view.transform.Find("StatMenu/PressureHover");
        if (pressureHover != null)
        {
            SetTooltip(pressureHover.gameObject, "Blood pressure", BuildBloodPressureTooltip(body));
        }
    }

    private static void SetTooltip(GameObject obj, string title, string desc)
    {
        if (obj == null)
        {
            return;
        }

        UITooltip tooltip = obj.GetComponent<UITooltip>() ?? obj.AddComponent<UITooltip>();
        tooltip.skipLocale = true;
        tooltip.tipName = title;
        tooltip.tipDesc = desc;
    }

    private static string BuildImmunityTooltip(Body body)
    {
        float hunger = (body.hunger - 70f) * 0.75f;
        float thirst = (body.thirst - 60f) * 0.3f;
        float energy = (body.energy - 60f) * 0.2f;
        float temperature = (body.temperature - 37f) * 8f;
        float blood = (body.bloodVolume - 100f) * 0.2f;
        float dirt = Mathf.Max(0f, body.dirtyness - 50f);
        float sickness = body.sicknessAmount * 0.8f;
        float radiation = body.radiationSickness * 0.5f;
        float antibiotic = body.antibioticImmunityTime > 0f ? 70f : 0f;
        float raw = 100f + hunger + thirst + energy + temperature + blood - dirt - sickness - radiation + antibiotic;

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Current", $"{body.immunity:0.#}/200 (raw {raw:0.#})");
        AppendLine(sb, "Base", "+100.0");
        AppendLine(sb, "Hunger", Signed(hunger));
        AppendLine(sb, "Thirst", Signed(thirst));
        AppendLine(sb, "Energy", Signed(energy));
        AppendLine(sb, "Temperature", Signed(temperature));
        AppendLine(sb, "Blood volume", Signed(blood));
        AppendLine(sb, "Dirt", Signed(-dirt));
        AppendLine(sb, "Sickness", Signed(-sickness));
        AppendLine(sb, "Radiation", Signed(-radiation));
        if (antibiotic > 0f)
        {
            AppendLine(sb, "Antibiotics", "+70.0");
        }

        sb.AppendLine();
        sb.Append("Infection speed curve value: ").Append(body.curImmunityMult.ToString("0.###"));
        return sb.ToString();
    }

    private static string BuildPainTooltip(Body body)
    {
        float adrenalineReduction = body.curAdrenaline * 0.5f;
        float resilienceMult = 1f - body.skills.RESFrom10 * 0.025f;

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Current", $"{body.averagePain:0.#}%");
        AppendLine(sb, "Adrenaline mask", $"-{adrenalineReduction:0.#} per limb");
        AppendLine(sb, "RES multiplier", $"{resilienceMult:0.###}x");
        sb.AppendLine();
        sb.AppendLine("<color=#FFFFFF>Worst limbs</color>");

        Limb first = null;
        Limb second = null;
        Limb third = null;
        float firstPain = -1f;
        float secondPain = -1f;
        float thirdPain = -1f;
        if (body.limbs != null)
        {
            for (int i = 0; i < body.limbs.Length; i++)
            {
                Limb limb = body.limbs[i];
                if (limb == null || limb.dismembered)
                {
                    continue;
                }

                float masked = Mathf.Max(0f, limb.pain - adrenalineReduction);
                if (masked > firstPain)
                {
                    third = second;
                    thirdPain = secondPain;
                    second = first;
                    secondPain = firstPain;
                    first = limb;
                    firstPain = masked;
                }
                else if (masked > secondPain)
                {
                    third = second;
                    thirdPain = secondPain;
                    second = limb;
                    secondPain = masked;
                }
                else if (masked > thirdPain)
                {
                    third = limb;
                    thirdPain = masked;
                }
            }
        }

        AppendLimbPain(sb, first, firstPain);
        AppendLimbPain(sb, second, secondPain);
        AppendLimbPain(sb, third, thirdPain);
        sb.AppendLine();
        AppendLine(sb, "Trauma rises", body.averagePain > 50f ? $"{body.averagePain * 0.0034f:0.###}/s" : "above 50% pain");
        AppendLine(sb, "Pain shock", body.averagePain > 75f ? $"{body.painShock:0%}" : "starts above 75%");
        return sb.ToString();
    }

    private static string BuildWeightTooltip(Body body)
    {
        float kg = WeightKg(body.weightOffset);
        float metabolism = WorldGeneration.GetRunSettingFloat("metabolismrate");
        float lossOffsetPerMinute = ((1f - body.hunger * 0.01f) * 0.015f + 0.003f) * metabolism * 60f;
        float kgDeltaPerMinute = -lossOffsetPerMinute * 0.34f;
        float encumbranceBase = 11f + Mathf.Clamp(body.weightOffset + 15f, -60f, 0f) * 0.1f -
            body.sicknessAmount * 0.025f + ((body.hunger > 100f) ? 1.5f : 0f) -
            ((body.hunger < 40f) ? 1.5f : 0f) - ((body.thirst < 40f) ? 1f : 0f) +
            Mathf.Min(body.skills.STRFrom10 * 0.5f, body.skills.RESFrom10 * 0.5f);
        float weightCapPenalty = Mathf.Clamp(body.weightOffset + 15f, -60f, 0f) * 0.1f;
        float kgToBestCap = body.weightOffset < -15f ? WeightKg(-15f) - kg : 0f;

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Current", $"{WeightTierName(body.weightOffset)} at {kg:0.0}kg (offset {body.weightOffset:0.#})");
        AppendLine(sb, "Drift", $"{SignedFine(kgDeltaPerMinute, "kg/min")} at current hunger");
        AppendLine(sb, "Encumbrance cap", $"{body.maxEncumberance:0.#}u");
        AppendLine(sb, "Weight cap effect", Signed(weightCapPenalty, "u before run multiplier"));
        AppendLine(sb, "Best cap direction", kgToBestCap > 0f ? $"gain {kgToBestCap:0.0}kg to reach the no-penalty zone" : "weight is not lowering cap");
        AppendLine(sb, "Over-encumbered", $"{body.overEncumberance * 100f:0}%");
        sb.AppendLine();
        sb.AppendLine("<color=#FFFFFF>Weight ladder</color>");
        AppendWeightTier(sb, body.weightOffset, "Obese", 50f, true);
        AppendWeightTier(sb, body.weightOffset, "Overweight", 15f, true);
        sb.AppendLine("Current: " + new string('-', Mathf.Clamp(Mathf.RoundToInt(Mathf.InverseLerp(-80f, 100f, body.weightOffset) * 18f), 0, 18)));
        AppendWeightTier(sb, body.weightOffset, "Underweight", -15f, false);
        AppendWeightTier(sb, body.weightOffset, "Very underweight", -30f, false);
        AppendWeightTier(sb, body.weightOffset, "Emaciated", -50f, false);
        sb.AppendLine();
        sb.Append($"{WeightKg(-55f):0.0}kg or {WeightKg(55f):0.0}kg flashes; {WeightKg(-60f):0.0}kg or {WeightKg(60f):0.0}kg can force fibrillation.");
        return sb.ToString();
    }

    private static string BuildBleedingTooltip(Body body)
    {
        float external = 0f;
        Limb first = null;
        Limb second = null;
        Limb third = null;
        float firstRate = -1f;
        float secondRate = -1f;
        float thirdRate = -1f;
        if (body.limbs != null)
        {
            for (int i = 0; i < body.limbs.Length; i++)
            {
                Limb limb = body.limbs[i];
                if (limb == null || limb.dismembered)
                {
                    continue;
                }

                float rate = limb.bleedAmount * limb.bleedSpeedMult * (limb.blockedBleeding ? 0f : 1f);
                external += rate;
                if (rate > firstRate)
                {
                    third = second;
                    thirdRate = secondRate;
                    second = first;
                    secondRate = firstRate;
                    first = limb;
                    firstRate = rate;
                }
                else if (rate > secondRate)
                {
                    third = second;
                    thirdRate = secondRate;
                    second = limb;
                    secondRate = rate;
                }
                else if (rate > thirdRate)
                {
                    third = limb;
                    thirdRate = rate;
                }
            }
        }

        float internalRate = Mathf.Clamp(body.internalBleeding, 0f, 25f) * 0.0057f;
        float regenRate = body.bloodRegenSpeed;
        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Displayed net", LitersPerMinute(body, body.totalBleedSpeed));
        AppendLine(sb, "External total", LitersPerMinute(body, external));
        AppendLine(sb, "Internal", LitersPerMinute(body, internalRate));
        AppendLine(sb, "Blood regen", "-" + LitersPerMinute(body, regenRate));
        AppendLine(sb, "Clotting speed", $"{body.bleedClottingSpeed:0.###}");
        AppendLine(sb, "Bleed multiplier", $"{body.bleedingSpeedMultiplier:0.###}x");
        sb.AppendLine();
        sb.AppendLine("<color=#FFFFFF>Max limb bleeds</color>");
        AppendLimbBleed(sb, body, first, firstRate);
        AppendLimbBleed(sb, body, second, secondRate);
        AppendLimbBleed(sb, body, third, thirdRate);
        return sb.ToString();
    }

    private static string BuildBloodVolumeTooltip(Body body)
    {
        float liters = body.bloodToLitersBody(body.bloodVolume);
        float externalInternalLoss = Mathf.Max(0f, body.totalBleedSpeed + body.bloodRegenSpeed);
        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Current", $"{liters:0.00}L ({body.bloodVolume:0.#})");
        AppendLine(sb, "Volume factor", $"{body.bloodVolumePercentage:0.###}x");
        AppendLine(sb, "Regen", $"{LitersPerMinute(body, body.bloodRegenSpeed)} from hunger/healing");
        AppendLine(sb, "Bleed drain", LitersPerMinute(body, externalInternalLoss));
        AppendLine(sb, "Net change", Signed((body.bloodRegenSpeed - externalInternalLoss) * 60f * 0.025f, "L/min"));
        if (body.bloodVolumePercentage < 0.6f)
        {
            AppendLine(sb, "Oxygen cap", $"{body.bloodVolumePercentage / 0.6f * 100f:0.#}% from low blood");
        }

        sb.AppendLine();
        sb.Append("Flashes below 25. Dying checks combine bleeding with blood below 40; critical below 30.");
        return sb.ToString();
    }

    private static string BuildHungerTooltip(Body body)
    {
        float metabolism = WorldGeneration.GetRunSettingFloat("metabolismrate");
        float hungerDrainPerMinute = 60f / 23f * metabolism;
        float immunity = (body.hunger - 70f) * 0.75f;
        float weightOffsetPerMinute = ((1f - body.hunger * 0.01f) * 0.015f + 0.003f) * metabolism * 60f;
        float moodDrain = Mathf.Clamp01(0.65f - body.hunger * 0.01f) * 0.065f * metabolism;

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Current", $"{body.hunger:0.#}");
        AppendLine(sb, "Drain", $"-{hungerDrainPerMinute:0.##}/min");
        AppendLine(sb, "Immunity", Signed(immunity));
        AppendLine(sb, "Weight drift", SignedFine(-weightOffsetPerMinute * 0.34f, "kg/min"));
        AppendLine(sb, "Limb healing", $"{body.hungerLimbHealCurrent:0.###}x");
        if (moodDrain > 0f)
        {
            AppendLine(sb, "Mood drain", $"-{moodDrain:0.###}/s");
        }

        sb.AppendLine();
        sb.Append("Below 40 lowers encumbrance cap and pressure target. At 0, limbs lose muscle; below 10 is dying.");
        return sb.ToString();
    }

    private static string BuildThirstTooltip(Body body)
    {
        float metabolism = WorldGeneration.GetRunSettingFloat("metabolismrate");
        float divisor = Mathf.Max(0.1f, 17f - body.tempDiffFromNormal * 0.5f);
        float thirstDrainPerMinute = 60f / divisor * (body.thirst > 100f ? (body.thirst > 175f ? 2.5f : 2f) : 1f) * metabolism;
        float immunity = (body.thirst - 60f) * 0.3f;
        float moodDrain = Mathf.Clamp01(0.65f - Mathf.Min(body.thirst, 120f) * 0.01f) * 0.065f * metabolism;

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Current", $"{body.thirst:0.#}");
        AppendLine(sb, "Drain", $"-{thirstDrainPerMinute:0.##}/min");
        AppendLine(sb, "Immunity", Signed(immunity));
        AppendLine(sb, "Blood pressure mult", $"{body.thirstBloodPressure:0.###}x");
        if (moodDrain > 0f)
        {
            AppendLine(sb, "Mood drain", $"-{moodDrain:0.###}/s");
        }

        sb.AppendLine();
        sb.Append("Below 40 lowers encumbrance cap. Below 0 thickens blood. Above 175 hurts brain and can force fibrillation.");
        return sb.ToString();
    }

    private static string BuildLimbStrengthTooltip(WoundView view, Body body)
    {
        Limb limb = null;
        if (body.limbs != null && view.limbLookingAt >= 0 && view.limbLookingAt < body.limbs.Length)
        {
            limb = body.limbs[view.limbLookingAt];
        }

        if (limb == null)
        {
            return "Select a limb for force details.";
        }

        float structure = limb.muscleHealth * 0.01f;
        float injury = (limb.broken || limb.dislocated || limb.splinted) ? 0f : 1f;
        float attached = limb.dismembered ? 0f : 1f;
        float painTerm = Mathf.Clamp01(1f - (Mathf.Max(limb.pain, body.averagePain * 0.9f) - body.curAdrenaline * 0.5f) * 0.007f);
        float oxygen = body.bloodOxygen * 0.01f;
        float stroke = limb.strokeAffected ? Mathf.Clamp01(1f - body.strokeAmount / 50f) : 1f;

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Current", $"{limb.totalForce * 100f:0}%");
        AppendLine(sb, "Muscle", $"{structure:0.###}x");
        AppendLine(sb, "Fracture/dislocation", $"{injury:0.###}x");
        AppendLine(sb, "Attached", $"{attached:0.###}x");
        AppendLine(sb, "Pain/adrenaline", $"{painTerm:0.###}x");
        AppendLine(sb, "Oxygen", $"{oxygen:0.###}x");
        if (limb.strokeAffected || body.strokeAmount > 0f)
        {
            AppendLine(sb, "Stroke", $"{stroke:0.###}x");
        }

        sb.AppendLine();
        sb.Append("Limb force drives hand usability, leg movement contribution, and selected-limb health panel force.");
        return sb.ToString();
    }

    private static string BuildSicknessTooltip(Body body)
    {
        float metabolism = WorldGeneration.GetRunSettingFloat("metabolismrate");
        float decayPerMinute = 0.06f * metabolism * 60f;
        float immunityPenalty = body.sicknessAmount * 0.8f;

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Current", $"{body.sicknessAmount:0.#}%");
        AppendLine(sb, "Natural decay", $"-{decayPerMinute:0.#}%/min");
        AppendLine(sb, "Immunity penalty", $"-{immunityPenalty:0.#}");
        if (body.sicknessAmount > 20f)
        {
            AppendLine(sb, "Mood drain", $"{Mathf.Clamp01(body.sicknessAmount * 0.01f) * 0.05f * metabolism:0.###}/s");
        }

        float radiationTarget = body.radiationSickness * 0.4f;
        if (radiationTarget > body.sicknessAmount)
        {
            AppendLine(sb, "Radiation target", $"{radiationTarget:0.#}%");
        }

        sb.AppendLine();
        sb.AppendLine("<color=#FFFFFF>Tiers</color>");
        sb.AppendLine("10 / 30 / 50 / 75 show stronger sickness moodles.");
        sb.Append("95+ can infect the abdomen and is critical.");
        return sb.ToString();
    }

    private static string BuildBrainTooltip(Body body)
    {
        float healingRate = 0f;
        if (body.brainHealth > 0f)
        {
            healingRate = 0.003f * WorldGeneration.GetRunSettingFloat("healingrate");
        }

        float oxygenDrain = body.bloodOxygen < 80f && !body.brainDying ? (80f - body.bloodOxygen) / 600f : 0f;
        float dyingDrain = body.brainDying ? 1.5f : 0f;
        float thirstDrain = body.thirst > 175f ? 0.05f : 0f;
        float strokeDrain = body.strokeAmount > 0f ? 0.025f : 0f;
        float heatDrain = body.temperature > 42f ? 0.5f : 0f;
        float radiationDrain = body.radiationSickness * 0.0003f;
        float braingrowHeal = CoUtils.instance.DurationOf("braingrow") > 0f ? 0.1f * (body.brainGrowSickness / 1200f) : 0f;
        float net = healingRate + braingrowHeal - oxygenDrain - dyingDrain - thirstDrain - strokeDrain - heatDrain - radiationDrain;

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Current", $"{body.brainHealth:0.#}%");
        AppendLine(sb, "Heal", $"+{healingRate:0.####}/s");
        if (braingrowHeal > 0f)
        {
            AppendLine(sb, "Braingrow", $"+{braingrowHeal:0.####}/s while active");
        }
        if (oxygenDrain > 0f)
        {
            AppendLine(sb, "Low oxygen", $"-{oxygenDrain:0.####}/s");
        }
        if (dyingDrain > 0f)
        {
            AppendLine(sb, "Brain dying", $"-{dyingDrain:0.####}/s");
        }
        if (thirstDrain > 0f)
        {
            AppendLine(sb, "Extreme thirst", $"-{thirstDrain:0.####}/s");
        }
        if (strokeDrain > 0f)
        {
            AppendLine(sb, "Stroke", $"-{strokeDrain:0.####}/s");
        }
        if (heatDrain > 0f)
        {
            AppendLine(sb, "Extreme heat", $"-{heatDrain:0.####}/s");
        }
        if (radiationDrain > 0f)
        {
            AppendLine(sb, "Radiation", $"-{radiationDrain:0.####}/s");
        }
        AppendLine(sb, "Net", Signed(net, "/s"));
        if (net > 0f)
        {
            AppendLine(sb, "To 95%", TimeToTarget(body.brainHealth, 95f, net));
            AppendLine(sb, "To 100%", TimeToTarget(body.brainHealth, 100f, net));
        }
        else
        {
            AppendLine(sb, "Recovery ETA", "not improving");
        }

        sb.AppendLine();
        sb.AppendLine("Brain damage effects can happen below 95%.");
        sb.Append("Braingrow gives about 10 brain health per 20mL over time; >39mL or overlapping doses can mindwipe.");
        return sb.ToString();
    }

    private static string BuildRadiationTooltip(Body body)
    {
        float sicknessTarget = body.radiationSickness * 0.4f;
        float brainDrain = body.radiationSickness * 0.0003f;
        float bloodDrain = body.radiationSickness > 10f ? body.radiationSickness * 0.00025f : 0f;
        float thirstDrain = body.radiationSickness * 0.0002f;
        float naturalDecay = body.radiationSickness > 0f ? 0.033f : 0f;
        float antiradDuration = CoUtils.instance.DurationOf("antirad");
        float antiradReduction = antiradDuration > 0f ? 0.2f : 0f;

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Current", $"{body.radiationSickness:0.#}%");
        AppendLine(sb, "Natural decay", $"-{naturalDecay:0.###}/s");
        if (antiradReduction > 0f)
        {
            AppendLine(sb, "Antirad", $"-{antiradReduction:0.###}/s for {FormatDuration(antiradDuration)}");
        }

        AppendLine(sb, "Sickness target", $"{sicknessTarget:0.#}%");
        AppendLine(sb, "Immunity penalty", $"-{body.radiationSickness * 0.5f:0.#}");
        if (brainDrain > 0f)
        {
            AppendLine(sb, "Brain drain", $"-{brainDrain:0.####}/s");
        }
        if (bloodDrain > 0f)
        {
            AppendLine(sb, "Blood drain", $"-{bloodDrain:0.####}/s");
        }
        if (thirstDrain > 0f)
        {
            AppendLine(sb, "Thirst drain", $"-{thirstDrain:0.####}/s");
        }

        sb.AppendLine();
        sb.AppendLine("Above 10 drains blood. Above 30 can infect limbs, add internal bleeding, and damage tissue.");
        sb.Append("Dying check starts above 60.");
        return sb.ToString();
    }

    private static string BuildTemperatureTooltip(Body body)
    {
        float metabolicHeat = 0f;
        if (body.energy > 0f)
        {
            float heatMult = 1f - Mathf.Clamp01(0.3f - body.energy * 0.01f);
            if (body.TryGetComponent(out Painkillers painkillers) && painkillers.actualOpiateReception > 0f)
            {
                heatMult -= painkillers.actualOpiateReception * 0.005f;
            }

            metabolicHeat = 0.04f * heatMult;
        }

        float hungerWarmth = body.temperature < 36.5f
            ? Mathf.Max(body.hunger * 0.01f, 0.3f) * 0.03f * Mathf.Max(0f, 1f - Mathf.Clamp01(0.3f - body.energy * 0.01f))
            : 0f;
        float wetCooling = body.wetness * 0.001f;
        float immunityEffect = (body.temperature - 37f) * 8f;

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Current", $"{body.temperature:0.0}C ({Signed(body.tempDiffFromNormal, "C")})");
        if (WorldGeneration.world != null)
        {
            AppendLine(sb, "Layer", $"{WorldGeneration.world.ambientTemperature:0.0}C");
        }
        AppendLine(sb, "Movement mult", $"{body.currentTemperatureMovementMult:0.###}x");
        AppendLine(sb, "Immunity effect", Signed(immunityEffect));
        AppendLine(sb, "Insulation", $"{body.GetTotalInsulation():0.###}x");
        AppendLine(sb, "Clothing", $"{body.clothingTemperature:0.###}");
        AppendLine(sb, "Metabolic heat", Signed(metabolicHeat, "/s"));
        if (hungerWarmth > 0f)
        {
            AppendLine(sb, "Cold recovery", Signed(hungerWarmth, "/s"));
        }
        if (wetCooling > 0f)
        {
            AppendLine(sb, "Wet cooling", $"-{wetCooling:0.###}/s");
        }

        sb.AppendLine();
        sb.AppendLine("Below 28 can start fibrillation; below 29 is dying and below 27 critical.");
        sb.Append("Above 41 is dying, above 41.5 critical, and above 42 drains brain health.");
        return sb.ToString();
    }

    private static string BuildBloodPressureTooltip(Body body)
    {
        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Pressure", $"{body.bloodPressure:0.#} ({Mathf.RoundToInt(body.bloodPressure)}/{Mathf.RoundToInt(body.bloodPressure * 0.66f)})");
        AppendLine(sb, "Pulse", $"{body.heartRate:0.#} bpm");
        if (body.bloodPressureChangeFromMedicine > 0.5f)
        {
            AppendLine(sb, "Medicine", $"lowering x0.75 ({body.bloodPressureChangeFromMedicine:0.#}s left)");
        }
        else if (body.bloodPressureChangeFromMedicine < -0.5f)
        {
            AppendLine(sb, "Medicine", $"raising x1.25 ({-body.bloodPressureChangeFromMedicine:0.#}s left)");
        }
        else
        {
            AppendLine(sb, "Medicine", "none");
        }
        AppendLine(sb, "Vessel tone", $"{body.bloodVesselSize:0.###}x ({VesselToneName(body.bloodVesselSize)})");
        AppendLine(sb, "Pressure factor", $"{1f / Mathf.Max(0.01f, body.bloodVesselSize):0.###}x from vessel size");
        sb.AppendLine();
        sb.AppendLine("Higher vessel size means vasodilation/lower pressure; lower means vasoconstriction/higher pressure.");
        sb.AppendLine("Vasoconstricted <0.97x; neutral 0.97-1.03x; vasodilated >1.03x.");
        sb.AppendLine();
        sb.AppendLine("<color=#FFFFFF>Hypotension</color> 110 / 96 / 83 / 60");
        sb.AppendLine("<color=#FFFFFF>Hypertension</color> 130 / 145 / 162 / 180");
        sb.AppendLine("Dying warning: below 80 or above 170. Critical: below 70.");
        sb.Append("Stroke rolls begin above 180. Below 10 can cause brain failure when consciousness is under 5.");
        return sb.ToString();
    }

    private static string BuildHeartRateTooltip(Body body)
    {
        Painkillers painkillers = null;
        float opiateReception = body.TryGetComponent(out painkillers) ? painkillers.actualOpiateReception : 0f;
        float painContribution = body.averagePain;
        float staminaContribution = (100f - body.stamina) * 0.6f;
        float adrenalineContribution = body.curAdrenaline * 0.55f;
        float viscosityContribution = -Mathf.Max(0f, body.bloodViscosity) * 0.3f;
        float temperatureContribution = body.tempDiffFromNormal * 0.5f;
        float opiateContribution = -opiateReception / 5f;
        float fibrillationContribution = body.fibrillationProgress;
        float advancedFibrillationContribution = 0f;
        if (body.fibrillationProgress > 75f)
        {
            advancedFibrillationContribution += (body.fibrillationProgress - 75f) * 4f;
        }
        if (body.fibrillationProgress > 95f)
        {
            advancedFibrillationContribution += (body.fibrillationProgress - 95f) * 30f;
        }

        float target = 70f + painContribution + staminaContribution + adrenalineContribution
            + viscosityContribution + temperatureContribution + opiateContribution
            + body.heartRatePressureOffset + fibrillationContribution + advancedFibrillationContribution;
        float pressureReference = HeartRatePressureReference(body, opiateReception);
        string pressureTrend = body.bloodPressure < pressureReference - 5f
            ? "rising +1.5/s"
            : body.bloodPressure > pressureReference + 5f
                ? "falling -1.5/s"
                : "holding";

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Current", $"{body.heartRate:0.#} bpm");
        AppendLine(sb, "Target", $"{target:0.#} bpm (current eases toward this)");
        sb.AppendLine();
        sb.AppendLine("<color=#FFFFFF>Target contributors</color>");
        AppendLine(sb, "Base", "+70 bpm");
        AppendLine(sb, "Pain", Signed(painContribution, " bpm"));
        AppendLine(sb, "Stamina deficit", Signed(staminaContribution, " bpm"));
        AppendLine(sb, "Adrenaline", Signed(adrenalineContribution, " bpm"));
        AppendLine(sb, "Positive viscosity", Signed(viscosityContribution, " bpm"));
        AppendLine(sb, "Temperature", Signed(temperatureContribution, " bpm"));
        AppendLine(sb, "Opiate reception", Signed(opiateContribution, " bpm"));
        AppendLine(sb, "Pressure response", $"{Signed(body.heartRatePressureOffset, " bpm")} ({pressureTrend})");
        AppendLine(sb, "Fibrillation", Signed(fibrillationContribution, " bpm"));
        if (advancedFibrillationContribution > 0f)
        {
            AppendLine(sb, "Advanced fibrillation", Signed(advancedFibrillationContribution, " bpm"));
        }

        sb.AppendLine();
        AppendLine(sb, "Pressure actual / reference", $"{body.bloodPressure:0.#} / {pressureReference:0.#}");
        if (body.inCardiacArrest)
        {
            sb.AppendLine("<color=#FF7777>Cardiac arrest locks heart rate to 0.</color>");
        }
        sb.AppendLine();
        sb.AppendLine("Bradycardia <60 (severe <40); tachycardia >110 (severe >160, critical >200).");
        sb.Append("Above 200 can start fibrillation; above 280 accelerates it. Cardiac arrest is below 20.");
        return sb.ToString();
    }

    private static float HeartRatePressureReference(Body body, float opiateReception)
    {
        float reference = 120f;
        reference -= (100f - body.bloodVolume) / 4f;
        reference += (100f - body.stamina) * 0.2f;
        reference += body.curAdrenaline * 0.2f;
        reference -= body.septicShock * 0.4f;
        reference += body.tempDiffFromNormal * 2f;
        reference += body.weightOffset * 0.333f;
        if (body.thirst < 0f)
        {
            reference += body.thirst;
        }
        if (body.hunger < 40f)
        {
            reference += (body.hunger - 40f) * 0.25f;
        }
        if (body.bloodPressureChangeFromMedicine > 0f)
        {
            reference *= 0.75f;
        }
        if (body.bloodPressureChangeFromMedicine < 0f)
        {
            reference *= 1.25f;
        }
        reference -= opiateReception * 0.4f;
        return reference;
    }

    private static string BuildViscosityTooltip(Body body)
    {
        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Current", $"{body.bloodViscosity:0.#}");
        AppendLine(sb, "Pulmonary embolism", body.hasPulmonaryEmbolism ? "detected" : body.bloodViscosity > EmbolismRiskViscosity ? "risk above 90" : "not detected");
        AppendLine(sb, "Oxygen cap", $"{100f - Mathf.Abs(Mathf.MoveTowards(body.bloodViscosity, 0f, 40f)) * 0.4f:0.#}%");
        AppendLine(sb, "Clotting", $"x{Mathf.Clamp01(body.bloodViscosity.Remap(-100f, 0f, 0f, 1f)):0.##}");
        sb.AppendLine();
        sb.Append("Above 80 contributes to fibrillation. Above 90 can roll embolism.");
        return sb.ToString();
    }

    private static string BuildOpiateTooltip(Painkillers painkillers)
    {
        float amount = painkillers != null ? painkillers.opiateAmount : 0f;
        float tolerance = painkillers != null ? painkillers.opiateTolerance : 0f;
        float reception = painkillers != null ? painkillers.opiateReception : 0f;
        float actual = painkillers != null ? painkillers.actualOpiateReception : 0f;
        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Amount", $"{amount:0.#}");
        AppendLine(sb, "Tolerance", $"{tolerance:0.#}");
        AppendLine(sb, "Reception", $"{reception:0.#}");
        AppendLine(sb, "Actual", $"{actual:0.#}");
        AppendLine(sb, "Pain relief", actual > 0f ? $"{actual * 0.3f:0.#}/s per limb" : "none");
        AppendLine(sb, "Mood", Signed(actual > 0f ? actual : Mathf.Max(-80f, actual * 1.66f)));
        sb.AppendLine();
        sb.AppendLine("OD tiers: 5 / 20 / 50 / 80.");
        sb.Append("Withdrawal tiers: -5 / -15 / -25 / -34.");
        return sb.ToString();
    }

    private static string BuildFibrillationTooltip(Body body, float rate)
    {
        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Progress", $"{body.fibrillationProgress:0.#}%");
        AppendLine(sb, "Rate", Signed(rate, "%/s"));
        sb.AppendLine();
        sb.AppendLine("<color=#FFFFFF>Active factors</color>");
        bool any = false;
        any |= AppendFactor(sb, body.bloodOxygen < 60f, $"Oxygen {body.bloodOxygen:0.#}% < 60");
        any |= AppendFactor(sb, body.bloodPressure < 88f, $"Pressure {body.bloodPressure:0.#} < 88");
        any |= AppendFactor(sb, body.heartRate > 200f, $"Pulse {body.heartRate:0.#} > 200");
        any |= AppendFactor(sb, body.fibrillationForced, "Forced fibrillation flag");
        any |= AppendFactor(sb, body.bloodViscosity > 80f, $"Viscosity {body.bloodViscosity:0.#} > 80");
        any |= AppendFactor(sb, body.temperature < 28.5f, $"Temperature {body.temperature:0.#}C < 28.5");
        if (!any)
        {
            sb.AppendLine("No rising factor; fibrillation should decay.");
        }

        sb.AppendLine();
        sb.Append("Progress rises while a factor is active and arrests at 100%.");
        return sb.ToString();
    }

    private static string BuildOxygenTooltip(Body body)
    {
        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "Oxygen", $"{body.bloodOxygen:0.#}%");
        AppendLine(sb, "Respiration", $"{body.respiratoryRate:0.#}/m");
        AppendLine(sb, "Hemothorax cap", $"{100f - body.hemothorax * 0.3f:0.#}%");
        AppendLine(sb, "Viscosity cap", $"{100f - Mathf.Abs(Mathf.MoveTowards(body.bloodViscosity, 0f, 40f)) * 0.4f:0.#}%");
        sb.AppendLine();
        sb.Append("Below 80 can damage brain health; below 60 contributes to fibrillation.");
        return sb.ToString();
    }

    private static StringBuilder NewTooltipBuilder()
    {
        return new StringBuilder(512);
    }

    private static void AppendLine(StringBuilder sb, string label, string value)
    {
        sb.Append("<color=#FFFFFF>").Append(label).Append("</color>: ").AppendLine(value);
    }

    private static bool AppendFactor(StringBuilder sb, bool active, string text)
    {
        if (active)
        {
            sb.AppendLine(text);
        }

        return active;
    }

    private static void AppendLimbPain(StringBuilder sb, Limb limb, float pain)
    {
        if (limb == null || pain < 0f)
        {
            return;
        }

        string name = string.IsNullOrWhiteSpace(limb.fullName) ? limb.name : limb.fullName;
        sb.Append(name).Append(": ").Append(pain.ToString("0.#")).AppendLine("%");
    }

    private static void AppendLimbBleed(StringBuilder sb, Body body, Limb limb, float rate)
    {
        if (limb == null || rate <= 0.0001f)
        {
            return;
        }

        string name = string.IsNullOrWhiteSpace(limb.fullName) ? limb.name : limb.fullName;
        sb.Append(name).Append(": ").AppendLine(LitersPerMinute(body, rate));
    }

    private static void AppendWeightTier(StringBuilder sb, float currentOffset, string name, float thresholdOffset, bool highTier)
    {
        bool active = highTier ? currentOffset >= thresholdOffset : currentOffset <= thresholdOffset;
        string label = active ? "Current " + name : name;
        sb.Append("<color=#FFFFFF>").Append(label).Append("</color>: ");
        sb.Append(WeightKg(thresholdOffset).ToString("0.0")).Append("kg");
        if (!active)
        {
            sb.Append(" (").Append(Mathf.Abs(WeightKg(currentOffset) - WeightKg(thresholdOffset)).ToString("0.0")).Append("kg away)");
        }

        sb.AppendLine();
    }

    private static string LitersPerMinute(Body body, float rate)
    {
        return $"{body.bloodToLiters(rate) * 60f:0.00}L/min";
    }

    private static string Signed(float value, string suffix = "")
    {
        return (value >= 0f ? "+" : "") + value.ToString("0.#") + suffix;
    }

    private static string SignedFine(float value, string suffix = "")
    {
        return (value >= 0f ? "+" : "") + value.ToString("0.###") + suffix;
    }

    private static string VesselToneName(float bloodVesselSize)
    {
        if (bloodVesselSize > 1.03f)
        {
            return "vasodilated";
        }

        if (bloodVesselSize < 0.97f)
        {
            return "vasoconstricted";
        }

        return "neutral";
    }

    private static float WeightKg(float weightOffset)
    {
        return weightOffset * 0.34f + 50f;
    }

    private static string WeightDistance(float currentOffset, float targetOffset)
    {
        float currentKg = WeightKg(currentOffset);
        float targetKg = WeightKg(targetOffset);
        bool crossed = targetOffset < 0f ? currentOffset <= targetOffset : currentOffset >= targetOffset;
        if (crossed)
        {
            return $"active at {targetKg:0.0}kg";
        }

        return $"{Mathf.Abs(currentKg - targetKg):0.0}kg away ({targetKg:0.0}kg)";
    }

    private static string WeightTierName(float weightOffset)
    {
        if (weightOffset >= 50f)
        {
            return "Obese";
        }

        if (weightOffset > 15f)
        {
            return "Overweight";
        }

        if (weightOffset <= -50f)
        {
            return "Emaciated";
        }

        if (weightOffset < -30f)
        {
            return "Very underweight";
        }

        if (weightOffset < -15f)
        {
            return "Underweight";
        }

        return "Normal";
    }

    private static string TimeToTarget(float current, float target, float rate)
    {
        if (current >= target)
        {
            return "now";
        }

        if (rate <= 0f)
        {
            return "not improving";
        }

        return FormatDuration((target - current) / rate);
    }

    private static string FormatDuration(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
        {
            return "unknown";
        }

        TimeSpan span = TimeSpan.FromSeconds(seconds);
        if (span.TotalHours >= 1d)
        {
            return $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}";
        }

        return $"{span.Minutes:00}:{span.Seconds:00}";
    }

    private static CandlestickWidget CreateCandlestickWidget(string title, Vector2 position, TextMeshProUGUI template)
    {
        GameObject root = new GameObject("DoktorMod" + title + "Widget", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(diagnosticsRect, false);
        root.layer = diagnosticsRoot.layer;
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(78f, 188f);
        ConfigureTooltipHitArea(root.GetComponent<Image>());

        TextMeshProUGUI label = CreateOverlayLabel(rect, title, template, new Vector2(0f, 0f),
            new Vector2(90f, 28f), 22f, TextAlignmentOptions.Center, UiGreen);
        RectTransform chart = CreateRect(root.transform, "Chart", new Vector2(39f, -34f), new Vector2(58f, 136f), new Vector2(0.5f, 1f));
        Image wick = CreateImage(chart, "Wick", UiGreen, new Vector2(0f, 0f), new Vector2(4f, 136f), new Vector2(0.5f, 0f));
        Image safe = CreateImage(chart, "Safe", new Color32(76, 255, 119, 55), Vector2.zero, new Vector2(46f, 30f), new Vector2(0.5f, 0f));
        Image left = CreateImage(chart, "Left", UiGreen, Vector2.zero, new Vector2(4f, 30f), new Vector2(0.5f, 0f));
        Image right = CreateImage(chart, "Right", UiGreen, Vector2.zero, new Vector2(4f, 30f), new Vector2(0.5f, 0f));
        Image top = CreateImage(chart, "Top", UiGreen, Vector2.zero, new Vector2(46f, 4f), new Vector2(0.5f, 0f));
        Image bottom = CreateImage(chart, "Bottom", UiGreen, Vector2.zero, new Vector2(46f, 4f), new Vector2(0.5f, 0f));
        Image value = CreateImage(chart, "Value", UiGreen, Vector2.zero, new Vector2(22f, 4f), new Vector2(0.5f, 0f));
        TextMeshProUGUI valueText = CreateOverlayLabel(rect, "ValueText", template, new Vector2(62f, -84f), //opioid display text
            new Vector2(110f, 46f), 18f, TextAlignmentOptions.Center, UiGreen);

        return new CandlestickWidget(root, chart, label, valueText, wick, safe, left, right, top, bottom, value);
    }

    private static FibWidget CreateFibWidget(Vector2 position, TextMeshProUGUI template)
    {
        GameObject root = new GameObject("DoktorModFibWidget", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(diagnosticsRect, false);
        root.layer = diagnosticsRoot.layer;
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(220f, 120f);
        ConfigureTooltipHitArea(root.GetComponent<Image>());

        TextMeshProUGUI title = CreateOverlayLabel(rect, "FIB", template, new Vector2(0f, 0f),
            new Vector2(65f, 28f), 22f, TextAlignmentOptions.Left, UiGreen);
        TextMeshProUGUI rate = CreateOverlayLabel(rect, "Rate", template, new Vector2(95f, 0f),
            new Vector2(125f, 28f), 22f, TextAlignmentOptions.Left, UiRed);
        Image high = CreateOverlayImage(rect, "HighLine", new Color32(76, 255, 119, 70), new Vector2(-50f, -50f),
            new Vector2(330f, 3f), new Vector2(0f, 0.5f));
        Image low = CreateOverlayImage(rect, "LowLine", new Color32(76, 255, 119, 70), new Vector2(-50f, -96f),
            new Vector2(330f, 3f), new Vector2(0f, 0.5f));
        TextMeshProUGUI progress = CreateOverlayLabel(rect, "Progress", template, new Vector2(95f, -55f),
            new Vector2(115f, 34f), 24f, TextAlignmentOptions.Center, UiRed);
        root.SetActive(false);
        return new FibWidget(root, rate, progress, high, low);
    }

    private static void ConfigureTooltipHitArea(Image image)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = SolidSprite();
        image.color = new Color32(0, 0, 0, 0);
        image.raycastTarget = true;
    }

    private static TextMeshProUGUI CreateOverlayLabel(Transform parent, string name, TextMeshProUGUI template,
        Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject labelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        labelObject.layer = parent.gameObject.layer;
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        if (template != null)
        {
            label.font = template.font;
            label.fontSharedMaterial = template.fontSharedMaterial;
        }

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        label.fontSize = fontSize;
        label.fontSizeMax = fontSize;
        label.fontSizeMin = 12f;
        label.enableAutoSizing = true;
        label.enableWordWrapping = false;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        obj.layer = parent.gameObject.layer;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image CreateImage(Transform parent, string name, Color color, Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        obj.layer = parent.gameObject.layer;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = obj.GetComponent<Image>();
        image.sprite = SolidSprite();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateOverlayImage(Transform parent, string name, Color color, Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
    {
        Image image = CreateImage(parent, name, color, anchoredPosition, size, pivot);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        return image;
    }

    private static Sprite SolidSprite()
    {
        if (solidSprite == null)
        {
            solidSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        }

        return solidSprite;
    }

    private static Color ValueColor(float value, float safeLow, float safeHigh, float warningLow, float warningHigh,
        float criticalLow, float criticalHigh)
    {
        if (value <= criticalLow || value >= criticalHigh)
        {
            return UiRed;
        }

        if (value < safeLow || value > safeHigh)
        {
            if (value <= warningLow || value >= warningHigh)
            {
                return UiOrange;
            }

            return UiYellow;
        }

        return UiGreen;
    }

    private sealed class CandlestickWidget
    {
        public readonly GameObject Root;
        private readonly RectTransform chart;
        private readonly TextMeshProUGUI valueText;
        private readonly Image safeBody;
        private readonly Image safeLeft;
        private readonly Image safeRight;
        private readonly Image safeTop;
        private readonly Image safeBottom;
        private readonly Image valueBar;

        public CandlestickWidget(GameObject root, RectTransform chart, TextMeshProUGUI label, TextMeshProUGUI valueText,
            Image wick, Image safeBody, Image safeLeft, Image safeRight, Image safeTop, Image safeBottom, Image valueBar)
        {
            Root = root;
            this.chart = chart;
            this.valueText = valueText;
            this.safeBody = safeBody;
            this.safeLeft = safeLeft;
            this.safeRight = safeRight;
            this.safeTop = safeTop;
            this.safeBottom = safeBottom;
            this.valueBar = valueBar;
        }

        public void Update(float value, float min, float max, float widgetSafeLow, float widgetSafeHigh,
            float warningLow, float warningHigh, float criticalLow, float criticalHigh,
            float colorSafeLow, float colorSafeHigh, string text)
        {
            float height = chart.sizeDelta.y;
            float widgetSafeMin = ToY(widgetSafeLow, min, max, height);
            float widgetSafeMax = ToY(widgetSafeHigh, min, max, height);
            float widgetSafeHeight = Mathf.Max(6f, widgetSafeMax - widgetSafeMin);
            Move(safeBody.rectTransform, widgetSafeMin, widgetSafeHeight, 0f);
            Move(safeLeft.rectTransform, widgetSafeMin, widgetSafeHeight);
            Move(safeRight.rectTransform, widgetSafeMin, widgetSafeHeight, 23f);
            MoveHorizontal(safeTop.rectTransform, widgetSafeMax);
            MoveHorizontal(safeBottom.rectTransform, widgetSafeMin);

            float mid = (widgetSafeMin + widgetSafeMax) * 0.5f;
            float y = ToY(value, min, max, height);
            RectTransform valueRect = valueBar.rectTransform;
            valueRect.pivot = new Vector2(0.5f, y >= mid ? 0f : 1f);
            valueRect.anchoredPosition = new Vector2(0f, mid);
            valueRect.sizeDelta = new Vector2(22f, Mathf.Max(5f, Mathf.Abs(y - mid)));
            Color color = ValueColor(value, colorSafeLow, colorSafeHigh, warningLow, warningHigh, criticalLow, criticalHigh);
            valueBar.color = color;
            valueText.text = text;
            valueText.color = color;
        }

        private static float ToY(float value, float min, float max, float height)
        {
            return Mathf.Clamp01(Mathf.InverseLerp(min, max, value)) * height;
        }

        private static void Move(RectTransform rect, float y, float height, float x = -23f)
        {
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        private static void MoveHorizontal(RectTransform rect, float y)
        {
            rect.anchoredPosition = new Vector2(0f, y);
        }
    }

    private sealed class FibWidget
    {
        public readonly GameObject Root;
        private readonly TextMeshProUGUI rateText;
        private readonly TextMeshProUGUI progressText;
        private readonly Image highLine;
        private readonly Image lowLine;

        public FibWidget(GameObject root, TextMeshProUGUI rateText, TextMeshProUGUI progressText, Image highLine, Image lowLine)
        {
            Root = root;
            this.rateText = rateText;
            this.progressText = progressText;
            this.highLine = highLine;
            this.lowLine = lowLine;
        }

        public void Update(float progress, float rate, float ecgLow, float ecgHigh)
        {
            string arrow = rate >= 0f ? "^" : "v";
            rateText.text = $"{arrow}{Mathf.Abs(rate):0.#}%/s";
            progressText.text = $"{progress:0}%";
            Color color = progress >= 60f ? new Color32(255, 74, 74, 205) : new Color32(255, 158, 73, 190);
            rateText.color = color;
            progressText.color = color;
            highLine.rectTransform.anchoredPosition = new Vector2(0f, ToFibY(ecgHigh));
            lowLine.rectTransform.anchoredPosition = new Vector2(0f, ToFibY(ecgLow));
            highLine.color = new Color32(76, 255, 119, 70);
            lowLine.color = new Color32(76, 255, 119, 70);
        }

        private static float ToFibY(float ecgHeight)
        {
            return -96f + Mathf.Clamp01((ecgHeight + 1f) * 0.5f) * 58f;
        }
    }
}

internal static class RemoteMoodlePanelController
{
    private const string RootName = "DoktorModRemoteMoodles";
    private static readonly AccessTools.FieldRef<MoodleManager, Body> MoodleBody =
        AccessTools.FieldRefAccess<MoodleManager, Body>("body");
    private static readonly AccessTools.FieldRef<MoodleManager, int> MoodleCount =
        AccessTools.FieldRefAccess<MoodleManager, int>("moodleCount");
    private static readonly AccessTools.FieldRef<MoodleManager, int> MainCount =
        AccessTools.FieldRefAccess<MoodleManager, int>("mainCount");
    private static readonly AccessTools.FieldRef<MoodleManager, float> UpdateTime =
        AccessTools.FieldRefAccess<MoodleManager, float>("updateTime");
    private static readonly AccessTools.FieldRef<MoodleManager, List<string>> PreviousMoodles =
        AccessTools.FieldRefAccess<MoodleManager, List<string>>("prevMoodles");
    private static Body activeBody;
    private static GameObject root;
    private static RectTransform rootRect;
    private static float refreshTime;
    private static bool loggedRenderFailure;

    public static void RuntimeUpdate()
    {
        WoundView view = WoundView.view;
        MoodleManager manager = MoodleManager.main;
        Body localBody = PlayerCamera.main != null ? PlayerCamera.main.body : null;
        Body viewedBody = view != null && view.gameObject.activeInHierarchy ? view.body : null;
        bool showRemote = manager != null && viewedBody != null && localBody != null && viewedBody != localBody;

        if (showRemote)
        {
            ShowRemoteMoodles(view, manager, viewedBody);
        }
        else
        {
            HideRemoteMoodles();
        }
    }

    private static void ShowRemoteMoodles(WoundView view, MoodleManager manager, Body body)
    {
        if (manager.moodles == null)
        {
            return;
        }

        EnsureRoot(view);
        if (root == null)
        {
            return;
        }

        root.SetActive(true);
        refreshTime -= Time.unscaledDeltaTime;
        if (activeBody == body && refreshTime > 0f)
        {
            return;
        }

        activeBody = body;
        refreshTime = 0.5f;
        RenderRemoteMoodles(manager, body);
    }

    private static void EnsureRoot(WoundView view)
    {
        if (root != null)
        {
            return;
        }

        Transform parent = view.transform.Find("ComputerBack") ?? view.transform;
        root = new GameObject(RootName, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        root.layer = parent.gameObject.layer;
        rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(0f, 0f);
        rootRect.pivot = new Vector2(0f, 0f);
        rootRect.anchoredPosition = new Vector2(58f, -100f);
        rootRect.sizeDelta = new Vector2(420f, 145f);
        root.SetActive(false);
    }

    private static void RenderRemoteMoodles(MoodleManager manager, Body body)
    {
        Transform localContainer = manager.moodles;
        Body localBody = MoodleBody(manager);
        int localMoodleCount = MoodleCount(manager);
        int localMainCount = MainCount(manager);
        float localUpdateTime = UpdateTime(manager);
        List<string> localPreviousMoodles = PreviousMoodles(manager);
        bool localSideMoodles = manager.sideMoodles;

        try
        {
            manager.moodles = root.transform;
            MoodleBody(manager) = body;
            manager.UpdateMoodles();
            loggedRenderFailure = false;
        }
        catch (Exception ex)
        {
            if (!loggedRenderFailure)
            {
                DoktorModPlugin.Log.LogWarning("Could not render remote-player moodles: " + ex);
                loggedRenderFailure = true;
            }
        }
        finally
        {
            manager.moodles = localContainer;
            MoodleBody(manager) = localBody;
            MoodleCount(manager) = localMoodleCount;
            MainCount(manager) = localMainCount;
            UpdateTime(manager) = localUpdateTime;
            PreviousMoodles(manager) = localPreviousMoodles;
            manager.sideMoodles = localSideMoodles;
        }
    }

    private static void HideRemoteMoodles()
    {
        if (root != null)
        {
            root.SetActive(false);
        }

        activeBody = null;
        refreshTime = 0f;
    }
}

internal static class DollOverlayController
{
    private const string RootName = "DoktorModBodyDollOverlay";
    private const string CogName = "DoktorModBodyDollCog";
    private const float IconRefreshInterval = 0.2f;
    private static readonly List<DollLimb> dollLimbs = new List<DollLimb>();
    private static readonly List<GameObject> statusIcons = new List<GameObject>();
    private static readonly List<GameObject> editHandles = new List<GameObject>();
    private static GameObject root;
    private static GameObject content;
    private static GameObject iconRoot;
    private static RectTransform rootRect;
    private static RectTransform canvasRect;
    private static Image frameImage;
    private static GameObject editHeader;
    private static TextMeshProUGUI editHeaderText;
    private static GameObject cogButton;
    private static Vector2 baseSize = new Vector2(260f, 440f);
    private static bool editMode;
    private static DragMode dragMode;
    private static Vector2 lastMouse;
    private static Vector2 resizeStartMouse;
    private static Vector2 resizeSign;
    private static float resizeStartScale;
    private static float iconRefreshTimer;

    private enum DragMode
    {
        None,
        Move,
        Resize
    }

    public static void RuntimeUpdate()
    {
        PlayerCamera camera = PlayerCamera.main;
        WoundView view = WoundView.view;
        if (camera == null || camera.mainCanvas == null || view == null || view.limbImages == null || view.body == null)
        {
            ClearSceneObjects();
            return;
        }

        EnsureSettingsButton(view);
        EnsureOverlay(camera, view);
        if (root == null)
        {
            return;
        }

        bool visible = DoktorModPlugin.ShowBodyDollOverlay.Value && camera.body != null && camera.body.alive;
        bool blockedByGui = IsBlockedByGui(camera);
        root.SetActive((visible && !blockedByGui) || editMode);

        if (!root.activeSelf)
        {
            return;
        }

        ApplySavedTransform();
        UpdateLimbDisplay(view);
        UpdateEditMode(camera);
    }

    public static void BeginEditMode()
    {
        PlayerCamera camera = PlayerCamera.main;
        if (camera == null)
        {
            return;
        }

        if (camera.woundView != null && camera.woundView.activeSelf)
        {
            camera.ToggleWoundView(sound: false);
        }

        if (camera.radialOpen)
        {
            camera.radialOpen = false;
        }

        editMode = true;
        dragMode = DragMode.None;
        if (root != null)
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }
    }

    private static void EndEditMode()
    {
        editMode = false;
        dragMode = DragMode.None;
        SaveTransform();
    }

    private static bool IsBlockedByGui(PlayerCamera camera)
    {
        bool inventoryHoverBlocked = camera.radialOpen && !DoktorModPlugin.ShowDollDuringInventoryHover.Value;
        return (camera.woundView != null && camera.woundView.activeSelf) ||
            (camera.craftingPanel != null && camera.craftingPanel.activeSelf) ||
            (camera.tradeMenu != null && camera.tradeMenu.activeSelf) ||
            (camera.containerMenu != null && camera.containerMenu.activeSelf) ||
            inventoryHoverBlocked ||
            PauseHandler.paused ||
            camera.didDeathScreen ||
            (MinigameBase.main != null && MinigameBase.main.currentMinigame != null);
    }

    private static void EnsureOverlay(PlayerCamera camera, WoundView view)
    {
        if (root != null && root.transform.parent == camera.mainCanvas.transform && dollLimbs.Count == view.limbImages.Length)
        {
            return;
        }

        ClearOverlay();
        canvasRect = camera.mainCanvas.GetComponent<RectTransform>();
        root = new GameObject(RootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(camera.mainCanvas.transform, false);
        root.transform.SetAsLastSibling();
        root.layer = camera.mainCanvas.gameObject.layer;
        rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        frameImage = root.GetComponent<Image>();
        frameImage.color = Color.clear;
        frameImage.raycastTarget = false;

        content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(root.transform, false);
        content.layer = root.layer;
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        iconRoot = new GameObject("StatusIcons", typeof(RectTransform));
        iconRoot.transform.SetParent(root.transform, false);
        iconRoot.layer = root.layer;
        RectTransform iconRect = iconRoot.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        BuildLimbs(view);
        BuildEditHandles();
        EnsureEditHeader(camera, view);
        ApplySavedTransform();
    }

    private static void BuildLimbs(WoundView view)
    {
        dollLimbs.Clear();
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < view.limbImages.Length; i++)
        {
            Image source = view.limbImages[i];
            if (source == null)
            {
                continue;
            }

            RectTransform sourceRect = source.rectTransform;
            Vector2 size = ImageSize(source);
            Vector2 pos = view.origPositions != null && i < view.origPositions.Length ? view.origPositions[i] : sourceRect.anchoredPosition;
            min = Vector2.Min(min, pos - size * 0.5f);
            max = Vector2.Max(max, pos + size * 0.5f);
        }

        if (min.x == float.MaxValue)
        {
            min = new Vector2(-120f, -220f);
            max = new Vector2(120f, 220f);
        }

        Vector2 center = (min + max) * 0.5f;
        baseSize = max - min + new Vector2(48f, 48f);
        rootRect.sizeDelta = baseSize;

        for (int i = 0; i < view.limbImages.Length; i++)
        {
            Image source = view.limbImages[i];
            if (source == null)
            {
                continue;
            }

            RectTransform sourceRect = source.rectTransform;
            Vector2 pos = view.origPositions != null && i < view.origPositions.Length ? view.origPositions[i] : sourceRect.anchoredPosition;
            GameObject limbObject = new GameObject("Limb" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            limbObject.transform.SetParent(content.transform, false);
            limbObject.layer = root.layer;
            Image limbImage = limbObject.GetComponent<Image>();
            limbImage.sprite = source.sprite;
            limbImage.raycastTarget = false;
            RectTransform limbRect = limbImage.rectTransform;
            limbRect.anchorMin = new Vector2(0.5f, 0.5f);
            limbRect.anchorMax = new Vector2(0.5f, 0.5f);
            limbRect.pivot = sourceRect.pivot;
            limbRect.anchoredPosition = pos - center;
            limbRect.sizeDelta = ImageSize(source);
            limbRect.localRotation = sourceRect.localRotation;
            limbRect.localScale = sourceRect.localScale;

            Image innerImage = null;
            if (sourceRect.childCount > 0 && sourceRect.GetChild(0).TryGetComponent(out Image sourceInner))
            {
                GameObject innerObject = new GameObject("LimbInner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                innerObject.transform.SetParent(limbObject.transform, false);
                innerObject.layer = root.layer;
                innerImage = innerObject.GetComponent<Image>();
                innerImage.sprite = sourceInner.sprite;
                innerImage.raycastTarget = false;
                RectTransform innerRect = innerImage.rectTransform;
                RectTransform sourceInnerRect = sourceInner.rectTransform;
                innerRect.anchorMin = sourceInnerRect.anchorMin;
                innerRect.anchorMax = sourceInnerRect.anchorMax;
                innerRect.pivot = sourceInnerRect.pivot;
                innerRect.anchoredPosition = sourceInnerRect.anchoredPosition;
                innerRect.sizeDelta = ImageSize(sourceInner);
                innerRect.localRotation = sourceInnerRect.localRotation;
                innerRect.localScale = sourceInnerRect.localScale;
            }

            dollLimbs.Add(new DollLimb(i, limbImage, innerImage, limbRect.anchoredPosition));
        }
    }

    private static Vector2 ImageSize(Image image)
    {
        Vector2 size = image.rectTransform.sizeDelta;
        if ((size.x <= 0f || size.y <= 0f) && image.sprite != null)
        {
            size = image.sprite.rect.size;
        }

        if (size.x <= 0f || size.y <= 0f)
        {
            size = Vector2.one * 48f;
        }

        return size;
    }

    private static void UpdateLimbDisplay(WoundView view)
    {
        Body body = view.body;
        if (body == null || body.limbs == null)
        {
            return;
        }

        for (int i = 0; i < dollLimbs.Count; i++)
        {
            DollLimb doll = dollLimbs[i];
            if (doll.LimbIndex < 0 || doll.LimbIndex >= body.limbs.Length)
            {
                continue;
            }

            Limb limb = body.limbs[doll.LimbIndex];
            if (limb == null)
            {
                continue;
            }

            Vector2 jitter = Vector2.zero;
            if (limb.pain > 0f && !limb.dismembered)
            {
                jitter = new Vector2(
                    UnityEngine.Random.Range(limb.pain * -0.02f, limb.pain * 0.02f),
                    UnityEngine.Random.Range(limb.pain * -0.02f, limb.pain * 0.02f));
            }

            doll.Outer.rectTransform.anchoredPosition = doll.BasePosition + jitter;

            if (view.armorMode)
            {
                Color armorColor = view.limbArmorGradient.Evaluate(1f / limb.GetArmorReduction());
                doll.Outer.color = limb.dismembered ? Color.clear : Color.white;
                if (doll.Inner != null)
                {
                    armorColor.a = limb.dismembered ? 0f : 0.9f;
                    doll.Inner.color = armorColor;
                }
            }
            else
            {
                float alpha = limb.dismembered ? 0f : 0.85f;
                doll.Outer.color = new Color(1f, limb.skinHealth * 0.01f, limb.skinHealth * 0.01f, alpha);
                if (doll.Inner != null)
                {
                    Color inner = new Color(
                        WorldGeneration.unchipped ? 0f : (1f - limb.muscleHealth * 0.01f),
                        0f,
                        WorldGeneration.unchipped ? Mathf.Max(0f, limb.infectionAmount * 0.01f - 0.2f) : 0f,
                        alpha);
                    if (WorldGeneration.unchipped && limb.pain > 80f)
                    {
                        inner = new Color(Mathf.PingPong(Time.unscaledTime * 4f, 1f), 0f, 0f, alpha);
                    }
                    else if (!WorldGeneration.unchipped && limb.muscleHealth < 10f)
                    {
                        inner = new Color(Mathf.PingPong(Time.unscaledTime * 4f, 1f), 0f, 0f, alpha);
                    }

                    doll.Inner.color = inner;
                }
            }
        }

        iconRefreshTimer += Time.unscaledDeltaTime;
        if (iconRefreshTimer >= IconRefreshInterval || statusIcons.Count == 0)
        {
            iconRefreshTimer = 0f;
            RebuildStatusIcons(view);
        }
    }

    private static void RebuildStatusIcons(WoundView view)
    {
        foreach (GameObject icon in statusIcons)
        {
            if (icon != null)
            {
                UnityEngine.Object.Destroy(icon);
            }
        }

        statusIcons.Clear();
        Body body = view.body;
        if (body == null || body.limbs == null)
        {
            return;
        }

        for (int i = 0; i < dollLimbs.Count; i++)
        {
            DollLimb doll = dollLimbs[i];
            if (doll.LimbIndex < 0 || doll.LimbIndex >= body.limbs.Length)
            {
                continue;
            }

            Limb limb = body.limbs[doll.LimbIndex];
            if (limb == null || limb.dismembered)
            {
                continue;
            }

            List<int> iconIds = new List<int>();
            if (limb.totalBleedAmount > 1f)
            {
                iconIds.Add(0);
            }

            if (limb.infectionAmount >= 25f || (PlayerCamera.main.showInfection != null && doll.LimbIndex < PlayerCamera.main.showInfection.Length && PlayerCamera.main.showInfection[doll.LimbIndex]))
            {
                iconIds.Add(1);
            }

            if (limb.broken && !WorldGeneration.unchipped)
            {
                iconIds.Add(2);
            }

            if (limb.dislocated && !WorldGeneration.unchipped)
            {
                iconIds.Add(3);
            }

            if (limb.disinfectionTime > 0f && !WorldGeneration.unchipped)
            {
                iconIds.Add(4);
            }

            if (limb.hasShrapnel)
            {
                iconIds.Add(5);
            }

            if (limb.TryGetComponent<ChilledLimb>(out var chilled))
            {
                iconIds.Add(6);
            }

            for (int iconIndex = 0; iconIndex < iconIds.Count; iconIndex++)
            {
                float iconSize = Mathf.Clamp(DoktorModPlugin.DollStatusIconSize.Value, 20f, 140f);
                float offset = ((float)iconIndex * iconSize - (float)(iconIds.Count - 1) * iconSize * 0.5f) * 0.55f;
                Vector2 pos = doll.BasePosition + new Vector2(offset, offset * 0.2f);
                AddStatusIcon(view, limb, chilled, iconIds[iconIndex], pos);
            }
        }
    }

    private static void AddStatusIcon(WoundView view, Limb limb, ChilledLimb chilled, int iconId, Vector2 pos)
    {
        Sprite sprite = null;
        Color color = Color.white;
        if (iconId == 0)
        {
            (sprite, color) = view.BleedImageFromLimb(limb);
        }
        else if (iconId == 1 && view.icons.Length > 11)
        {
            sprite = view.icons[limb.GetInfectionSpeed() > 0f ? 11 : 10];
            color = Color32.Lerp(Color.white, new Color32(163, 0, 182, byte.MaxValue), limb.infectionAmount * 0.01f);
        }
        else if (iconId == 2 && view.icons.Length > 2)
        {
            sprite = view.icons[2];
            color = new Color(1f, 1f - limb.boneHealTimer * 0.01f, 1f - limb.boneHealTimer * 0.01f);
        }
        else if (iconId == 3 && view.icons.Length > 1)
        {
            sprite = view.icons[1];
            color = new Color(1f, 1f - limb.dislocationTimer * 0.01f, 1f - limb.dislocationTimer * 0.01f);
        }
        else if (iconId == 4 && view.icons.Length > 4)
        {
            sprite = view.icons[4];
            float shade = 0.1f + limb.disinfectionTime / 220f;
            color = new Color(shade, shade, shade);
        }
        else if (iconId == 5 && view.icons.Length > 5)
        {
            sprite = view.icons[5];
            color = new Color32(173, 106, 54, byte.MaxValue);
        }
        else if (iconId == 6 && view.icons.Length > 6 && chilled != null)
        {
            sprite = view.icons[6];
            color = Color.Lerp(Color.gray, new Color32(54, 137, byte.MaxValue, byte.MaxValue), chilled.timeLeft / chilled.maxTime);
        }

        if (sprite == null)
        {
            return;
        }

        GameObject iconObject = new GameObject("StatusIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.transform.SetParent(iconRoot.transform, false);
        iconObject.layer = root.layer;
        Image image = iconObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = Vector2.one * Mathf.Clamp(DoktorModPlugin.DollStatusIconSize.Value, 20f, 140f);
        statusIcons.Add(iconObject);
    }

    private static void EnsureSettingsButton(WoundView view)
    {
        Transform title = view.transform.Find("ExperimentTitle");
        Transform parent = title != null ? title : view.transform;
        if (cogButton != null && cogButton.transform.parent == parent)
        {
            return;
        }

        if (cogButton != null)
        {
            UnityEngine.Object.Destroy(cogButton);
            cogButton = null;
        }

        Transform existing = parent.Find(CogName);
        if (existing != null)
        {
            cogButton = existing.gameObject;
            return;
        }

        cogButton = new GameObject(CogName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        cogButton.transform.SetParent(parent, false);
        cogButton.layer = view.gameObject.layer;
        RectTransform rect = cogButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-38f, 0f);
        rect.sizeDelta = Vector2.one * 56f;
        Image image = cogButton.GetComponent<Image>();
        image.sprite = FindPauseSettingsSprite();
        image.color = new Color32(50, 255, 92, 255);
        image.preserveAspect = true;
        Button button = cogButton.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(BeginEditMode);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(cogButton.transform, false);
        labelObject.layer = cogButton.layer;
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "⚙";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 24f;
        label.raycastTarget = false;
        if (view.nameText != null)
        {
            label.font = view.nameText.font;
            label.fontSharedMaterial = view.nameText.fontSharedMaterial;
        }

        labelObject.SetActive(false);

        UITooltip tip = cogButton.AddComponent<UITooltip>();
        tip.skipLocale = true;
        tip.tipName = "Body doll overlay";
        tip.tipDesc = "Adjust the body doll overlay.";
    }

    private static Sprite FindPauseSettingsSprite()
    {
        GameObject pauseSettings = GameObject.Find("Main Camera/Canvas/PauseMenu/Settings");
        if (pauseSettings != null && pauseSettings.TryGetComponent(out Image image) && image.sprite != null)
        {
            return image.sprite;
        }

        return null;
    }

    private static void UpdateEditMode(PlayerCamera camera)
    {
        bool showEdit = editMode;
        frameImage.color = showEdit ? new Color(0f, 0f, 0f, 0.35f) : Color.clear;
        if (editHeader != null)
        {
            editHeader.SetActive(showEdit);
        }

        foreach (GameObject handle in editHandles)
        {
            if (handle != null)
            {
                handle.SetActive(showEdit);
            }
        }

        if (!editMode)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            EndEditMode();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            dragMode = HitResizeCorner(Input.mousePosition, out resizeSign) ? DragMode.Resize : (HitRoot(Input.mousePosition) ? DragMode.Move : DragMode.None);
            lastMouse = Input.mousePosition;
            resizeStartMouse = Input.mousePosition;
            resizeStartScale = DoktorModPlugin.DollOverlayScale.Value;
        }

        if (Input.GetMouseButtonUp(0))
        {
            dragMode = DragMode.None;
            SaveTransform();
        }

        if (!Input.GetMouseButton(0))
        {
            return;
        }

        if (dragMode == DragMode.Move)
        {
            Vector2 delta = ((Vector2)Input.mousePosition - lastMouse) / Mathf.Max(0.01f, PlayerCamera.uiScale);
            rootRect.anchoredPosition += delta;
            lastMouse = Input.mousePosition;
            SaveTransform();
        }
        else if (dragMode == DragMode.Resize)
        {
            Vector2 delta = (Vector2)Input.mousePosition - resizeStartMouse;
            float resizeDelta = (delta.x * resizeSign.x + delta.y * resizeSign.y) / 600f;
            DoktorModPlugin.DollOverlayScale.Value = Mathf.Clamp(resizeStartScale + resizeDelta, 0.12f, 1.5f);
        }
    }

    private static bool HitRoot(Vector2 screenPoint)
    {
        return rootRect != null && RectTransformUtility.RectangleContainsScreenPoint(rootRect, screenPoint, null);
    }

    private static bool HitResizeCorner(Vector2 screenPoint, out Vector2 sign)
    {
        sign = Vector2.one;
        if (rootRect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, screenPoint, null, out Vector2 local))
        {
            return false;
        }

        Vector2 half = baseSize * 0.5f;
        const float threshold = 42f;
        bool nearX = Mathf.Abs(Mathf.Abs(local.x) - half.x) <= threshold;
        bool nearY = Mathf.Abs(Mathf.Abs(local.y) - half.y) <= threshold;
        if (!nearX || !nearY)
        {
            return false;
        }

        sign = new Vector2(local.x < 0f ? -1f : 1f, local.y < 0f ? -1f : 1f);
        return true;
    }

    private static void ApplySavedTransform()
    {
        if (rootRect == null)
        {
            return;
        }

        rootRect.anchoredPosition = new Vector2(DoktorModPlugin.DollOverlayX.Value, DoktorModPlugin.DollOverlayY.Value);
        float scale = Mathf.Clamp(DoktorModPlugin.DollOverlayScale.Value, 0.12f, 1.5f);
        rootRect.localScale = Vector3.one * scale;
    }

    private static void SaveTransform()
    {
        if (rootRect == null)
        {
            return;
        }

        DoktorModPlugin.DollOverlayX.Value = rootRect.anchoredPosition.x;
        DoktorModPlugin.DollOverlayY.Value = rootRect.anchoredPosition.y;
    }

    private static void BuildEditHandles()
    {
        editHandles.Clear();
        Vector2[] anchors =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f)
        };

        for (int i = 0; i < anchors.Length; i++)
        {
            GameObject handle = new GameObject("ResizeHandle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(root.transform, false);
            handle.layer = root.layer;
            RectTransform rect = handle.GetComponent<RectTransform>();
            rect.anchorMin = anchors[i];
            rect.anchorMax = anchors[i];
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.one * 18f;
            Image image = handle.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.75f);
            image.raycastTarget = false;
            handle.SetActive(false);
            editHandles.Add(handle);
        }
    }

    private static void EnsureEditHeader(PlayerCamera camera, WoundView view)
    {
        if (editHeader != null && editHeader.transform.parent == camera.mainCanvas.transform)
        {
            return;
        }

        if (editHeader != null)
        {
            UnityEngine.Object.Destroy(editHeader);
        }

        editHeader = new GameObject("DoktorModBodyDollEditHeader", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        editHeader.transform.SetParent(camera.mainCanvas.transform, false);
        editHeader.layer = camera.mainCanvas.gameObject.layer;
        RectTransform rect = editHeader.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -28f);
        rect.sizeDelta = new Vector2(820f, 48f);

        editHeaderText = editHeader.GetComponent<TextMeshProUGUI>();
        editHeaderText.text = "Drag body doll to move | drag corners to resize | Esc to exit";
        editHeaderText.alignment = TextAlignmentOptions.Center;
        editHeaderText.fontSize = 24f;
        editHeaderText.color = new Color32(76, 255, 119, 255);
        editHeaderText.raycastTarget = false;
        if (view.nameText != null)
        {
            editHeaderText.font = view.nameText.font;
            editHeaderText.fontSharedMaterial = view.nameText.fontSharedMaterial;
        }

        editHeader.SetActive(false);
    }

    private static void ClearSceneObjects()
    {
        ClearOverlay();
        if (cogButton != null)
        {
            UnityEngine.Object.Destroy(cogButton);
            cogButton = null;
        }
    }

    private static void ClearOverlay()
    {
        if (root != null)
        {
            UnityEngine.Object.Destroy(root);
        }

        root = null;
        content = null;
        iconRoot = null;
        rootRect = null;
        frameImage = null;
        if (editHeader != null)
        {
            UnityEngine.Object.Destroy(editHeader);
        }

        editHeader = null;
        editHeaderText = null;
        dollLimbs.Clear();
        statusIcons.Clear();
        editHandles.Clear();
        editMode = false;
        dragMode = DragMode.None;
    }

    private sealed class DollLimb
    {
        public readonly int LimbIndex;
        public readonly Image Outer;
        public readonly Image Inner;
        public readonly Vector2 BasePosition;

        public DollLimb(int limbIndex, Image outer, Image inner, Vector2 basePosition)
        {
            LimbIndex = limbIndex;
            Outer = outer;
            Inner = inner;
            BasePosition = basePosition;
        }
    }
}

internal static class BandageTreatmentReadoutController
{
    private const string RootName = "DoktorModBandageTreatmentReadout";
    private static readonly Color32 UiGreen = new Color32(76, 255, 119, 255);
    private static readonly Color32 UiYellow = new Color32(255, 222, 54, 255);
    private static readonly Color32 UiRed = new Color32(255, 63, 78, 255);
    private static GameObject root;
    private static TreatmentBar bleedBar;
    private static TreatmentBar skinBar;
    private static TreatmentBar muscleBar;
    private static Sprite solidSprite;
    private static Sprite muscleIcon;
    private static Sprite skinIcon;
    private static Sprite bleedIcon;

    public static void RuntimeUpdate()
    {
        if (!DoktorModPlugin.ShowBandageTreatmentReadout.Value)
        {
            Clear();
            return;
        }

        MinigameBase game = Minigame.game;
        BandageMinigame minigame = game != null ? game.currentMinigame as BandageMinigame : null;
        Limb limb = minigame != null ? minigame.limb : null;
        if (game == null || minigame == null || limb == null || limb.body == null || game.minigameScreen == null)
        {
            Clear();
            return;
        }

        EnsurePanel(game);
        Refresh(game, limb);
    }

    private static void EnsurePanel(MinigameBase game)
    {
        Transform parent = PlayerCamera.main != null && PlayerCamera.main.mainCanvas != null
            ? PlayerCamera.main.mainCanvas.transform
            : game.minigameScreen.parent;
        if (parent == null)
        {
            return;
        }

        if (root != null && root.transform.parent == parent)
        {
            return;
        }

        Clear();
        root = new GameObject(RootName, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        root.transform.SetAsLastSibling();
        root.layer = parent.gameObject.layer;
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-580f, -10f);
        rect.sizeDelta = new Vector2(170f, 420f);

        bleedBar = CreateBar(rect, "Bleed", 26f, LoadEmbeddedSprite("limbBleed.png"));
        skinBar = CreateBar(rect, "Skin", 85f, LoadEmbeddedSprite("healthicons2.png"));
        muscleBar = CreateBar(rect, "Muscle", 144f, LoadEmbeddedSprite("healthicons1.png"));
    }

    private static void Refresh(MinigameBase game, Limb limb)
    {
        bool bruiseKit = game.currentItem != null && game.currentItem.id == "bruisekit";
        float healingRate = WorldGeneration.GetRunSettingFloat("healingrate");
        float bandageSeconds = limb.bandageSlowAmount / 1.25f;
        float skinQueueSeconds = limb.skinHealAmount / 0.5f;
        float clotRate = limb.hasShrapnel ? 0f : limb.body.bleedClottingSpeed * healingRate;
        float bleedReductionRate = clotRate + (limb.bandageSlowAmount > 0f ? 1.25f : 0f);
        float bleedSeconds = bleedReductionRate > 0.0001f ? Mathf.Min(bandageSeconds, limb.bleedAmount / bleedReductionRate) : 0f;
        float endBleed = Mathf.Max(0f, limb.bleedAmount - bleedReductionRate * bleedSeconds);
        float muscleSeconds = CoUtils.instance.DurationOf("bruisekit" + limb.name);
        float skinNaturalRecovery = LimbRecoveryPerSecond(limb.SkinHealRate) * skinQueueSeconds;
        float muscleNaturalRecovery = LimbRecoveryPerSecond(limb.MuscleHealRate) * muscleSeconds;
        float skinEnd = Mathf.Min(100f, limb.skinHealth + limb.skinHealAmount + skinNaturalRecovery);
        float muscleEnd = Mathf.Min(100f, limb.muscleHealth + muscleSeconds + muscleNaturalRecovery);
        (_, Color bleedColor) = WoundView.view.BleedImageFromLimb(limb);
        Color skinColor = HealthBarColor(limb.skinHealth);
        Color muscleColor = HealthBarColor(limb.muscleHealth);

        bleedBar.Update(limb.totalBleedAmount, endBleed, 100f, !bruiseKit && limb.bandageSlowAmount > 0.01f,
            bleedColor, bleedIcon, new Color32(224, 224, 214, 255));
        skinBar.Update(limb.skinHealth, skinEnd, 100f, bruiseKit && limb.skinHealAmount > 0.01f,
            skinColor, skinIcon, UiGreen);
        muscleBar.Update(limb.muscleHealth, muscleEnd, 100f, bruiseKit && muscleSeconds > 0.01f,
            muscleColor, muscleIcon, UiGreen);
    }

    private static float LimbRecoveryPerSecond(float recoveryThisFrame)
    {
        return Time.deltaTime > 0.0001f ? recoveryThisFrame / Time.deltaTime : 0f;
    }

    private static TreatmentBar CreateBar(Transform parent, string name, float x, Sprite iconSprite)
    {
        RectTransform barRoot = CreateRect(parent, name, new Vector2(x, 0f), new Vector2(46f, 400f), new Vector2(0.5f, 0.5f));
        Image frame = CreateImage(barRoot, "Frame", UiGreen, new Vector2(0f, -8f), new Vector2(36f, 342f), new Vector2(0.5f, 0.5f));
        CreateImage(barRoot, "Empty", new Color32(0, 0, 0, 170), new Vector2(0f, -8f), new Vector2(30f, 336f), new Vector2(0.5f, 0.5f));
        Image current = CreateFill(barRoot, "Current", UiGreen);
        Image marker = CreateImage(barRoot, "Endpoint", UiGreen, new Vector2(0f, -176f), new Vector2(42f, 4f), new Vector2(0.5f, 0.5f));
        marker.gameObject.SetActive(false);
        Image icon = CreateImage(barRoot, "Icon", Color.white, new Vector2(0f, 183f), new Vector2(30f, 30f), new Vector2(0.5f, 0.5f));
        icon.sprite = iconSprite;
        icon.preserveAspect = true;
        return new TreatmentBar(frame, current, marker, icon);
    }

    private static Image CreateFill(Transform parent, string name, Color color)
    {
        Image image = CreateImage(parent, name, color, new Vector2(0f, -176f), new Vector2(21f, 334f), new Vector2(0.5f, 0f));
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Vertical;
        image.fillOrigin = 0;
        image.fillAmount = 0f;
        return image;
    }

    private static Color HealthBarColor(float value)
    {
        if (value >= 10f)
        {
            return UiGreen;
        }

        return Mathf.Sin(Time.unscaledTime * 20f) > 0f ? UiRed : new Color32(255, 32, 32, 0);
    }

    private static Sprite LoadEmbeddedSprite(string fileName)
    {
        Sprite cached = fileName == "healthicons1.png"
            ? muscleIcon
            : fileName == "healthicons2.png" ? skinIcon : bleedIcon;
        if (cached != null)
        {
            return cached;
        }

        using (Stream stream = typeof(BandageTreatmentReadoutController).Assembly.GetManifestResourceStream("DoktorMod." + fileName))
        {
            if (stream == null)
            {
                return null;
            }

            byte[] bytes = new byte[(int)stream.Length];
            stream.Read(bytes, 0, bytes.Length);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!ImageConversion.LoadImage(texture, bytes, markNonReadable: false))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            if (fileName == "healthicons1.png")
            {
                muscleIcon = sprite;
            }
            else if (fileName == "healthicons2.png")
            {
                skinIcon = sprite;
            }
            else
            {
                bleedIcon = sprite;
            }

            return sprite;
        }
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, TextMeshProUGUI template, string text,
        Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        obj.layer = parent.gameObject.layer;
        TextMeshProUGUI label = obj.GetComponent<TextMeshProUGUI>();
        if (template != null)
        {
            label.font = template.font;
            label.fontSharedMaterial = template.fontSharedMaterial;
        }

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        label.text = text;
        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 position, Vector2 size, Vector2 pivot)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        obj.layer = parent.gameObject.layer;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image CreateImage(Transform parent, string name, Color color, Vector2 position, Vector2 size, Vector2 pivot)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        obj.layer = parent.gameObject.layer;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = obj.GetComponent<Image>();
        image.sprite = SolidSprite();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Sprite SolidSprite()
    {
        if (solidSprite == null)
        {
            solidSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        }

        return solidSprite;
    }

    private static string FormatDuration(float seconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
        return time.TotalHours >= 1d ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes:00}:{time.Seconds:00}";
    }

    private static void Clear()
    {
        if (root != null)
        {
            UnityEngine.Object.Destroy(root);
        }

        root = null;
        bleedBar = null;
        skinBar = null;
        muscleBar = null;
    }

    private sealed class TreatmentBar
    {
        private readonly Image frame;
        private readonly Image current;
        private readonly Image marker;
        private readonly Image icon;

        public TreatmentBar(Image frame, Image current, Image marker, Image icon)
        {
            this.frame = frame;
            this.current = current;
            this.marker = marker;
            this.icon = icon;
        }

        public void Update(float currentValue, float targetValue, float max, bool showMarker, Color color, Sprite sprite, Color markerColor)
        {
            current.fillAmount = Mathf.Clamp01(currentValue / max);
            current.color = color;
            frame.color = UiGreen;
            icon.color = Color.white;
            if (sprite != null)
            {
                icon.sprite = sprite;
            }

            marker.gameObject.SetActive(showMarker);
            if (showMarker)
            {
                marker.color = markerColor;
                marker.rectTransform.anchoredPosition = new Vector2(0f, -176f + Mathf.Clamp01(targetValue / max) * 334f);
            }
        }
    }
}

internal static class SyringeInjectionTracker
{
    private const string RootName = "DoktorModSyringePanel";

    private static SyringeMinigame activeMinigame;
    private static WaterContainerItem activeContainer;
    private static readonly Dictionary<string, float> previousAmounts = new Dictionary<string, float>();
    private static readonly Dictionary<string, float> injectedByLiquid = new Dictionary<string, float>();
    private static readonly List<string> liquidOrder = new List<string>();
    private static GameObject panelRoot;
    private static TextMeshProUGUI panelText;

    public static void RuntimeUpdate()
    {
        if (!DoktorModPlugin.ShowSyringeInjectionMenu.Value)
        {
            Clear();
            return;
        }

        MinigameBase game = Minigame.game;
        SyringeMinigame minigame = game != null ? game.currentMinigame as SyringeMinigame : null;
        Item item = game != null ? game.currentItem : null;
        WaterContainerItem container = null;
        if (item != null)
        {
            item.TryGetComponent(out container);
        }

        if (minigame == null || container == null || container.Capacity <= 0f)
        {
            Clear();
            return;
        }

        if (minigame != activeMinigame || container != activeContainer)
        {
            Start(minigame, container);
            return;
        }

        AccumulateChanges(container);
        RefreshPanel();
    }

    private static void Start(SyringeMinigame minigame, WaterContainerItem container)
    {
        Clear();

        if (minigame == null || container == null)
        {
            return;
        }

        activeMinigame = minigame;
        activeContainer = container;
        Snapshot(container, previousAmounts);
        liquidOrder.Clear();
        foreach (KeyValuePair<string, float> pair in previousAmounts)
        {
            RememberLiquid(pair.Key);
        }

        EnsurePanel();
        RefreshPanel();
    }

    private static void AccumulateChanges(WaterContainerItem container)
    {
        Dictionary<string, float> current = new Dictionary<string, float>();
        Snapshot(container, current);

        foreach (KeyValuePair<string, float> pair in previousAmounts)
        {
            current.TryGetValue(pair.Key, out float now);
            float injected = Mathf.Max(0f, pair.Value - now);
            if (injected <= 0.0001f)
            {
                continue;
            }

            if (!injectedByLiquid.ContainsKey(pair.Key))
            {
                injectedByLiquid[pair.Key] = 0f;
            }

            RememberLiquid(pair.Key);
            injectedByLiquid[pair.Key] += injected;
        }

        previousAmounts.Clear();
        foreach (KeyValuePair<string, float> pair in current)
        {
            previousAmounts[pair.Key] = pair.Value;
        }
    }

    public static void Clear()
    {
        activeMinigame = null;
        activeContainer = null;
        previousAmounts.Clear();
        injectedByLiquid.Clear();
        liquidOrder.Clear();

        if (panelRoot != null)
        {
            UnityEngine.Object.Destroy(panelRoot);
        }

        panelRoot = null;
        panelText = null;
    }

    private static void Snapshot(WaterContainerItem container, Dictionary<string, float> target)
    {
        target.Clear();
        if (container?.stack == null)
        {
            return;
        }

        foreach (LiquidStack stack in container.stack)
        {
            if (stack == null || string.IsNullOrWhiteSpace(stack.liquidId))
            {
                continue;
            }

            if (!target.ContainsKey(stack.liquidId))
            {
                target[stack.liquidId] = 0f;
            }

            target[stack.liquidId] += stack.amount;
            RememberLiquid(stack.liquidId);
        }
    }

    private static void RememberLiquid(string id)
    {
        if (!string.IsNullOrWhiteSpace(id) && !liquidOrder.Contains(id))
        {
            liquidOrder.Add(id);
        }
    }

    private static void EnsurePanel()
    {
        MinigameBase game = Minigame.game;
        if (game == null || game.minigameScreen == null)
        {
            return;
        }

        Transform existing = game.minigameScreen.Find(RootName);
        if (existing != null)
        {
            panelRoot = existing.gameObject;
            panelText = existing.GetComponent<TextMeshProUGUI>();
            PositionPanel(game);
            return;
        }

        panelRoot = new GameObject(
            RootName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        panelRoot.layer = game.itemText != null
            ? game.itemText.gameObject.layer
            : game.minigameScreen.gameObject.layer;
        panelRoot.transform.SetParent(game.itemText != null ? game.itemText.transform.parent : game.minigameScreen, false);
        panelRoot.transform.SetAsLastSibling();

        panelText = panelRoot.GetComponent<TextMeshProUGUI>();
        if (game.itemText != null)
        {
            panelText.font = game.itemText.font;
            panelText.fontSharedMaterial = game.itemText.fontSharedMaterial;
            panelText.fontSize = game.itemText.fontSize;
            panelText.color = game.itemText.color;
        }
        else if (game.guideText != null)
        {
            panelText.font = game.guideText.font;
            panelText.fontSharedMaterial = game.guideText.fontSharedMaterial;
            panelText.fontSize = 22f;
            panelText.color = Color.white;
        }

        panelText.enableAutoSizing = false;
        panelText.fontSizeMin = panelText.fontSize;
        panelText.fontSizeMax = panelText.fontSize;
        panelText.alignment = TextAlignmentOptions.TopLeft;
        panelText.enableWordWrapping = false;
        panelText.raycastTarget = false;
        PositionPanel(game);
    }

    private static void PositionPanel(MinigameBase game)
    {
        if (panelRoot == null || game == null)
        {
            return;
        }

        RectTransform rect = panelRoot.GetComponent<RectTransform>();
        if (rect == null)
        {
            return;
        }

        RectTransform source = game.itemText != null ? game.itemText.rectTransform : null;
        if (source != null)
        {
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = source.anchoredPosition + new Vector2(
                DoktorModPlugin.SyringeTextHorizontalOffset.Value,
                DoktorModPlugin.SyringeTextVerticalOffset.Value);
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(26f, -84f);
        }
    }

    private static void RefreshPanel()
    {
        if (panelText == null || activeContainer == null)
        {
            return;
        }

        float remaining = activeContainer.CurrentTotal;
        float capacity = activeContainer.Capacity;
        List<string> lines = new List<string>
        {
            $"<color=#{ColorUtility.ToHtmlStringRGB(activeContainer.CurrentTotal > 0f ? activeContainer.AverageColor() : Color.white)}><sprite index=21 tint=1>{FormatMl(remaining)}/{FormatMl(capacity)}mL"
        };

        if (liquidOrder.Count == 0)
        {
            lines.Add("<color=#ffffff>Injected: 0.0mL");
        }
        else
        {
            lines.Add("<color=#ffffff>Injected:");
            foreach (string id in liquidOrder)
            {
                injectedByLiquid.TryGetValue(id, out float amount);
                lines.Add($"{GetLiquidColorTag(id)}{GetLiquidName(id)}: {FormatMl(amount)}mL");
            }
        }

        PositionPanel(Minigame.game);
        if (panelRoot != null)
        {
            RectTransform rect = panelRoot.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(420f, Mathf.Max(70f, 10f + lines.Count * 24f));
            }
        }

        panelText.text = string.Join("\n", lines);
    }

    private static string FormatMl(float value)
    {
        return value.ToString("0.0");
    }

    private static string GetLiquidColorTag(string id)
    {
        if (id != null && Liquids.Registry.TryGetValue(id, out LiquidType liquid))
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(liquid.color) + ">";
        }

        return "<color=#ffffff>";
    }

    private static string GetLiquidName(string id)
    {
        if (id != null && Liquids.Registry.TryGetValue(id, out LiquidType liquid))
        {
            string localeKey = liquid.localeFromItem ? id : liquid.localeName;
            string localized = liquid.localeFromItem ? Locale.GetItem(localeKey) : Locale.GetOther(localeKey);
            if (!string.IsNullOrWhiteSpace(localized))
            {
                return localized;
            }
        }

        return string.IsNullOrWhiteSpace(id) ? "<unknown>" : id;
    }
}
