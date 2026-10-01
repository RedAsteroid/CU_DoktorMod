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
            "在注射小游戏中显示注射器剩余液体量，以及各液体成分的累计注射量。");
        ShowBandageTreatmentReadout = Config.Bind(
            "Bandage minigame",
            "ShowTreatmentReadout",
            true,
            "在绷带与淤伤治疗包小游戏中显示流血、皮肤与肌肉的治疗进度。");
        SyringeTextVerticalOffset = Config.Bind(
            "Syringe minigame",
            "TextVerticalOffset",
            DefaultSyringeTextVerticalOffset,
            "液体读数相对原版小游戏物品/百分比文本的垂直偏移量。");
        SyringeTextHorizontalOffset = Config.Bind(
            "Syringe minigame",
            "TextHorizontalOffset",
            DefaultSyringeTextHorizontalOffset,
            "液体读数相对原版小游戏物品/百分比文本的水平偏移量。");
        ShowTimedEffectMoodles = Config.Bind(
            "Timed effect moodles",
            "ShowTimedEffectMoodles",
            true,
            "为具有固定持续时间的医疗与药物效果添加原版样式的侧边状态图标。");
        TimedEffectIconScale = Config.Bind(
            "Timed effect moodles",
            "IconScale",
            DefaultTimedEffectIconScale,
            "自定义药品/物品图标在每个状态图标中所占的比例大小。");
        RevealSmallInfections = Config.Bind(
            "Health visibility",
            "RevealSmallInfections",
            true,
            "一旦出现感染就立即显示感染图标，而不必等到原版 25% 的显示阈值。");
        ShowBodyDollOverlay = Config.Bind(
            "Body doll overlay",
            "ShowOverlay",
            true,
            "在常规游戏界面下显示一个小型医疗面板样式的身体模型。");
        DollOverlayX = Config.Bind(
            "Body doll overlay",
            "OverlayX",
            DefaultDollOverlayX,
            "保存身体模型的中心 X 坐标（画布单位，相对左上角锚点）。");
        DollOverlayY = Config.Bind(
            "Body doll overlay",
            "OverlayY",
            DefaultDollOverlayY,
            "保存身体模型的中心 Y 坐标（画布单位，相对左上角锚点）。");
        DollOverlayScale = Config.Bind(
            "Body doll overlay",
            "OverlayScale",
            DefaultDollOverlayScale,
            "保存身体模型的缩放比例。");
        DollStatusIconSize = Config.Bind(
            "Body doll overlay",
            "StatusIconSize",
            DefaultDollStatusIconSize,
            "身体模型上流血/感染/骨折等状态图标的大小（在应用模型缩放之前）。");
        ShowDollDuringInventoryHover = Config.Bind(
            "Body doll overlay",
            "ShowDuringInventoryHover",
            true,
            "打开物品栏/轮盘界面时仍保持身体模型可见。");
        AlwaysShowOpiateLevel = Config.Bind(
            "Diagnostics overlay",
            "AlwaysShowOpiateLevel",
            false,
            "即使当前没有阿片摄入量或耐受，也始终在医疗面板的隐藏生命体征叠加层中显示阿片K线图。");

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
            "抗辐射药", "辐射病治疗生效中",
            overdoseLine: BuildDoseLine("即达过量", null, DoseToThreshold(CoUtils.instance.DurationOf("antirad"), 180f, 4.5f)));
        AddTimedOp(__instance, "amiodarone", "doktor_amiodarone", "amiodarone", "arrythmia",
            "胺碘酮", "心律失常治疗生效中");
        AddTimedOp(__instance, "epinephrine", "doktor_epinephrine", "epinephrine", "fightorflight",
            "肾上腺素", "肾上腺素支持生效中");
        AddTimedOp(__instance, "oxyline", "doktor_oxyline", "oxyline", "oxygen",
            "氧脉素", "氧脉素支持生效中");
        AddTimedOp(__instance, "procoagulant", "doktor_procoagulant", "bloodcoagulant", "bleeding",
            "促凝剂", "凝血支持生效中");
        AddTimedOp(__instance, "highgradestimulant", "doktor_highgradestim", "combatpen", "stimulants",
            "医用级兴奋剂", "兴奋剂效果生效中",
            overdoseLine: BuildDoseLine("即达过量",
                DoseToThreshold(CoUtils.instance.DurationOf("highgradestimulant"), 320f, 2.4f),
                DoseToThreshold(CoUtils.instance.DurationOf("highgradestimulant"), 320f, 2f)));
        AddTimedOp(__instance, "midgradestimulant", "doktor_midgradestim", "midgradestimulant", "stimulants",
            "强效兴奋剂", "兴奋剂效果生效中",
            overdoseLine: BuildDoseLine("即达过量",
                DoseToThreshold(CoUtils.instance.DurationOf("midgradestimulant"), 220f, 3.6f), null));
        AddTimedOp(__instance, "lowgradestimulant", "doktor_lowgradestim", "lowgradestimulant", "stimulants",
            "杂牌兴奋剂", "兴奋剂效果生效中",
            overdoseLine: BuildDoseLine("即达过量",
                DoseToThreshold(CoUtils.instance.DurationOf("lowgradestimulant"), 160f, 3.25f),
                DoseToThreshold(CoUtils.instance.DurationOf("lowgradestimulant"), 160f, 2.5f)));
        AddTimedOp(__instance, "naltrexone", "doktor_naltrexone", "naltrexone", "withdrawal",
            "纳曲酮", "阿片拮抗剂效果生效中");
        AddTimedOp(__instance, "chloroform", "doktor_chloroform", "chloroform", "asleep",
            "氯仿", "镇静效果生效中");
        AddTimedOp(__instance, "biochem", "doktor_biochem", "biochem", "sick",
            "口服生化流体", "生化流体暴露正在造成损伤", 3);
        AddTimedOp(__instance, "bleach", "doktor_bleach", "bleach", "sick",
            "口服漂白剂", "腐蚀性物质暴露正在造成损伤", 3);
        AddTimedOp(__instance, "oxylinedrink", "doktor_oxylinedrink", "oxyline", "oxygen",
            "口服氧脉素", "口服氧脉素暴露正在损伤肺部", 3);

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
            "抗生素覆盖", GetAntibioticDisplayName(antibioticId) + " 覆盖生效中");
    }

    private static void AddEarlyConditionMoodles(MoodleManager manager, Body body)
    {
        if (body.strokeAmount > 0.05f && body.strokeAmount <= 70f)
        {
            int intensity = Mathf.Clamp(Mathf.CeilToInt(body.strokeAmount / 25f) - 1, 0, 3);
            manager.AddMoodle(intensity, "stroke", "中风警告", $"中风进度: {body.strokeAmount:0}%");
        }

        if (body.hasPulmonaryEmbolism && WorldGeneration.unchipped)
        {
            manager.AddMoodle(3, "pulmonaryembolism", "肺栓塞", "已检测到肺栓塞");
        }
        else if (!body.hasPulmonaryEmbolism && body.bloodViscosity > EmbolismRiskViscosity)
        {
            int intensity = body.bloodViscosity > 95f ? 3 : 2;
            manager.AddMoodle(intensity, "pulmonaryembolism", "肺栓塞风险", $"血液黏稠度: {body.bloodViscosity:0}%");
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
        sb.Append("<color=#FFFFFF>当前毒素水平: ").Append(current.ToString("0.#")).AppendLine("</color>");
        sb.Append("毒素含量: ").Append(total.ToString("0.#")).AppendLine();
        sb.AppendLine("当前效果:");
        sb.Append("血氧饱和度上限: ").Append(oxygenCap.ToString("0.#")).AppendLine("%");
        if (body.bloodOxygen > oxygenCap)
        {
            sb.AppendLine("-0.7 血氧/s 趋向上限");
        }
        sb.Append('-').Append(bloodDrain.ToString("0.###")).AppendLine(" 血容量/s");
        sb.Append("血液黏稠度目标: ").Append(current.ToString("0.#"));
        if (body.bloodViscosity < current)
        {
            sb.Append(" (+").Append(viscosityRiseRate.ToString("0.###")).AppendLine("/s 低于目标期间)");
        }
        else
        {
            sb.AppendLine();
        }
        sb.Append("自然伤口凝血: ").Append((venomClottingMultiplier * 100f).ToString("0.#")).AppendLine("% 来自当前毒素水平");
        sb.Append("当前凝血速率: ").Append(clottingRate.ToString("0.####")).AppendLine(" 流血/s 每个伤口");

        sb.AppendLine();
        sb.Append("毒素含量消退: -").Append(toxinClearRate.ToString("0.###")).AppendLine("/s 趋向 0");
        sb.Append("毒素含量追踪: ").Append(currentChange >= 0f ? "+" : "")
            .Append(currentChange.ToString("0.###")).AppendLine("/s");
        sb.Append("清空毒素含量所需抗毒血清: ").Append(antivenomMl.ToString("0.#")).AppendLine("mL 注射");
        sb.AppendLine();
        sb.Append("档位: 2 / 25 / 55 / 90\n达到 20 时凝血降至 0%");
        return sb.ToString();
    }

    private static string BuildDrugOverdoseDescription(Body body)
    {
        StringBuilder sb = new StringBuilder(768);

        if (body.TryGetComponent(out Antidepressants antidepressants) && antidepressants.currentAmount >= 250f)
        {
            AppendOverdoseSection(sb, "抗抑郁药",
                FormatSingleRouteOverdose(antidepressants.currentAmount, 250f, 5f));
            sb.AppendLine("当前效果:");
            sb.AppendLine("-3 血压/s");
        }

        if (body.TryGetComponent(out SleepingPills sleepingPills))
        {
            float threshold = body.TryGetComponent(out Painkillers painkillers) && painkillers.actualOpiateReception > 15f
                ? 150f
                : 900f;
            if (sleepingPills.amount > threshold)
            {
                AppendOverdoseSection(sb, "安眠药",
                    FormatSingleRouteOverdose(sleepingPills.amount, threshold, 60f));
                sb.AppendLine("当前效果:");
                sb.AppendLine(body.conscious ? "-2.5 血压/s" : "-5 血压/s");
                sb.AppendLine(body.conscious ? "呼吸以 15/s 趋向 70" : "呼吸以 15/s 趋向 40");
                if (!body.conscious)
                {
                    sb.AppendLine("-1 心率/s");
                }
            }
        }

        float antiradDuration = CoUtils.instance.DurationOf("antirad");
        if (antiradDuration > 180f)
        {
            AppendOverdoseSection(sb, "抗辐射药",
                FormatSingleRouteOverdose(antiradDuration, 180f, 4.5f));
            sb.AppendLine("当前效果:");
            sb.AppendLine("+0.6 反胃程度/s");
            sb.AppendLine("+1.5 胸部疼痛/s");
        }

        float highGradeDuration = CoUtils.instance.DurationOf("highgradestimulant");
        if (highGradeDuration > 320f)
        {
            AppendOverdoseSection(sb, "医用级兴奋剂",
                FormatDualRouteOverdose(highGradeDuration, 320f, 2.4f, 2f));
            sb.AppendLine("当前效果:");
            sb.AppendLine("18% 概率/s: +3 身体抖动");
            sb.AppendLine("6% 概率/s: 布娃娃状态");
            sb.AppendLine("5% 概率/s: -50 意识清醒度");
            sb.AppendLine("5% 概率/s: -1 核心体温");
            sb.AppendLine("3% 概率/s: 操作和画面随机镜像翻转");
            sb.AppendLine("-0.1 兴奋剂倍率/s (至 -0.5)");
        }

        float midGradeDuration = CoUtils.instance.DurationOf("midgradestimulant");
        if (midGradeDuration > 220f)
        {
            AppendOverdoseSection(sb, "强效兴奋剂",
                FormatSingleRouteOverdose(midGradeDuration, 220f, 3.6f));
            sb.AppendLine("当前效果:");
            sb.AppendLine("+0.15 内出血/s");
            sb.AppendLine("-0.05 脑组织完整度/s");
            sb.AppendLine("+4 胸部疼痛/s (低于 60 时)");
            sb.AppendLine("18% 概率/s: +1.5 身体抖动");
            sb.AppendLine("10% 概率/s: -35 体力");
            sb.AppendLine("6% 概率/s: 布娃娃状态");
        }

        float lowGradeDuration = CoUtils.instance.DurationOf("lowgradestimulant");
        if (lowGradeDuration > 160f)
        {
            AppendOverdoseSection(sb, "杂牌兴奋剂",
                FormatDualRouteOverdose(lowGradeDuration, 160f, 3.25f, 2.5f));
            sb.AppendLine("当前效果:");
            sb.AppendLine("+0.04 核心体温/s");
            sb.AppendLine("-0.08 脑组织完整度/s");
            sb.AppendLine("+4 头部与胸部疼痛/s (低于 60 时)");
            sb.AppendLine("20% 概率/s: +1.5 身体抖动");
            sb.AppendLine("10% 概率/s: -25 体力");
            sb.AppendLine("10% 概率/s: 布娃娃状态");
            sb.AppendLine("7.5% 概率/s: -3 血氧饱和度");
            sb.AppendLine("6% 概率/s: 无意识");
            sb.AppendLine("4% 概率/s: -10 精力");
            sb.AppendLine("3.5% 概率/s: 呕吐");
            sb.AppendLine("3% 概率/s: 操作和画面随机镜像翻转");
            sb.AppendLine("2% 概率/s: 肾上腺素水平归零");
        }

        return sb.ToString();
    }

    private static void AppendOverdoseSection(StringBuilder sb, string name, string amount)
    {
        if (sb.Length > 0)
        {
            sb.AppendLine();
        }

        sb.Append("<color=#FFFFFF>").Append(name).Append(" 过量 ").Append(amount).AppendLine("</color>");
    }

    private static string FormatSingleRouteOverdose(float current, float threshold, float amountPerMl)
    {
        return (Mathf.Max(0f, current - threshold) / amountPerMl).ToString("0.#") + "mL";
    }

    private static string FormatDualRouteOverdose(float current, float threshold, float injectedPerMl, float ingestedPerMl)
    {
        float excess = Mathf.Max(0f, current - threshold);
        return "注射 " + (excess / injectedPerMl).ToString("0.#") + "mL / 口服 " +
            (excess / ingestedPerMl).ToString("0.#") + "mL";
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

        string desc = $"{descPrefix}, 持续 {FormatDuration(duration)}";
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
                "硝普钠", "降压效果生效中");
        }
        else if (pressureMedicine < -0.5f)
        {
            AddBodyTimer(manager, -pressureMedicine, "doktor_vasopressin", "vasopressin", "hypertension",
                "血管加压素", "升压效果生效中");
        }
    }

    private static void AddAntidepressants(MoodleManager manager, Body body)
    {
        if (!body.TryGetComponent(out Antidepressants antidepressants) || antidepressants.amount <= 0.05f)
        {
            return;
        }

        AddBodyTimer(manager, antidepressants.amount / 0.185f, "doktor_antidepressants", "antidepressants", "happy",
            "抗抑郁药", "情绪稳定效果生效中", overdoseLine: BuildDoseLine("即达过量", null,
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
            "安眠药", "镇静效果生效中", overdoseLine: BuildDoseLine("即达过量", null,
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
        desc.Append("实际阿片受体水平: ").Append(painkillers.actualOpiateReception.ToString("0.#")).Append(".");
        desc.AppendLine();
        desc.Append("鸦片: ").Append(BuildDoseLine("即达过量",
            DoseToThreshold(baseline, 80f, 0.4f), DoseToThreshold(baseline, 80f, 0.2f)));
        desc.AppendLine();
        desc.Append("吗啡: ").Append(BuildDoseLine("即达过量",
            DoseToThreshold(baseline, 80f, 0.9f), DoseToThreshold(baseline, 80f, 0.4f)));
        desc.AppendLine();
        desc.Append("止痛药: ").Append(BuildDoseLine("即达过量",
            null, DoseToThreshold(baseline, 80f, 1.4f)));
        desc.AppendLine();
        desc.Append("海洛因: ").Append(BuildDoseLine("即达过量",
            DoseToThreshold(baseline, 80f, 1.3f), DoseToThreshold(baseline, 80f, 0.6f)));
        desc.AppendLine();
        desc.Append("芬太尼: ").Append(BuildDoseLine("即达过量",
            DoseToThreshold(baseline, 80f, 42f), DoseToThreshold(baseline, 80f, 40f)));
        AddScaledMoodle(manager, 5, icon, "阿片类暴露", desc.ToString());
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
        return string.IsNullOrWhiteSpace(localized) ? "抗生素" : localized;
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
        return "距过量: 注射 " + FormatDose(injectMl) + " / 口服 " + FormatDose(ingestMl) + " (" + suffix + ")。";
    }

    private static string FormatDose(float? ml)
    {
        if (!ml.HasValue)
        {
            return "不适用";
        }

        return ml.Value <= 0f ? "已过量" : ml.Value.ToString("0.#") + "mL";
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
            view.versionText.text = $"中风 {body.strokeAmount:0}%";
            view.versionText.color = Color.red;
            view.versionText.enabled = Mathf.Sin(Time.unscaledTime * 20f) > 0f;
            return;
        }

        if (view.versionText.text.StartsWith("中风", StringComparison.Ordinal))
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
            peText.text = "肺栓塞已发生";
            peText.color = UiRed;
            peText.enabled = Mathf.Sin(Time.unscaledTime * 20f) > 0f;
            return;
        }

        peText.text = "肺栓塞风险";
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
        hemothoraxText.text = $"{body.hemothorax:0}% 血胸";
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
        SetTooltip(pressureWidget.Root, "血压", BuildBloodPressureTooltip(body));
        SetTooltip(viscosityWidget.Root, "血液黏稠度", BuildViscosityTooltip(body));

        Painkillers painkillers = null;
        bool hasOpiate = body.TryGetComponent(out painkillers) && Mathf.Abs(painkillers.opiateTolerance) > 0.001f;
        bool showOpiate = hasOpiate || DoktorModPlugin.AlwaysShowOpiateLevel.Value;
        opiateWidget.Root.SetActive(showOpiate);
        if (showOpiate)
        {
            float reception = painkillers != null ? painkillers.actualOpiateReception : 0f;
            float tolerance = painkillers != null ? painkillers.opiateTolerance : 0f;
            opiateWidget.Update(reception, -60f, 90f, -15f, 80f, -25f, 50f, -34f, 80f, -15f, 80f,
                $"受体水平: {reception:0.#}\n耐受: {tolerance * 0.01f:0.0}u");
            SetTooltip(opiateWidget.Root, "阿片受体水平", BuildOpiateTooltip(painkillers));
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
            SetTooltip(fibWidget.Root, "室颤进度", BuildFibrillationTooltip(body, smoothedFibRate));
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
        SetTooltip(view.happyText, "情绪值", moodTooltip);
        if (view.happinessIcon != null)
        {
            view.happinessIcon.raycastTarget = true;
            SetTooltip(view.happinessIcon.gameObject, "情绪值", moodTooltip);
        }

        SetTooltip(view.energyText, "精力", BuildEnergyTooltip(body));
        SetTooltip(view.immunityText, "免疫力", BuildImmunityTooltip(body));
        SetTooltip(view.painText, "总疼痛度", BuildPainTooltip(body));
        SetTooltip(view.weightText, "体重", BuildWeightTooltip(body));
        SetTooltip(view.sickText, "反胃程度", BuildSicknessTooltip(body));
        SetTooltip(view.brainHealthText, "脑组织完整度", BuildBrainTooltip(body));
        SetHeartPressureTooltips(view, body);
        SetTooltip(view.oxyText, "血氧饱和度", BuildOxygenTooltip(body));
        SetTooltip(view.bleedText, "总失血速度", BuildBleedingTooltip(body));
        SetTooltip(view.bloodText, "血容量", BuildBloodVolumeTooltip(body));
        SetTooltip(view.hungerText, "饥饿", BuildHungerTooltip(body));
        SetTooltip(view.thirstText, "口渴", BuildThirstTooltip(body));
        SetTooltip(view.limbForceText, "肢体力量", BuildLimbStrengthTooltip(view, body));
        SetTooltip(view.radText, "辐射量", BuildRadiationTooltip(body));
        SetTooltip(view.tempText, "核心体温", BuildTemperatureTooltip(body));
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
        AppendLine(sb, "当前", body.totalHappiness.ToString("0.#"));
        AppendLine(sb, "基础情绪值", Signed(body.happiness));
        AppendLine(sb, "失血", Signed(bleeding));
        AppendLine(sb, "疼痛", Signed(pain));
        AppendLine(sb, "反胃程度", Signed(sickness));
        AppendLine(sb, "饥饿", Signed(hunger));
        AppendLine(sb, "口渴", Signed(thirst));
        AppendLine(sb, "辐射量", Signed(radiation));
        AppendLine(sb, "听力损失", Signed(hearing));
        AppendLine(sb, "血容量损失", Signed(blood));
        AppendLine(sb, "创伤", Signed(trauma));
        AppendLine(sb, "潮湿", Signed(wetness));
        AppendLine(sb, "阿片类药物水平", Signed(body.opiateHappiness));
        AppendLine(sb, "抗抑郁药", Signed(body.antidepressantHappiness));
        AppendLine(sb, "原始 / clamped", $"{raw:0.#} / {clamped:0.#}");
        if (body.mindWipe)
        {
            AppendLine(sb, "精神抹除", "x0");
        }
        if (!Mathf.Approximately(horrorFactor, 1f))
        {
            AppendLine(sb, "恐惧值", $"x{horrorFactor:0.###}");
        }

        float metabolism = WorldGeneration.GetRunSettingFloat("metabolismrate");
        sb.AppendLine();
        if (!body.conscious)
        {
            float normalization = body.sleeping
                ? 0.01f * (body.happiness < 0f ? 1f : 0.5f) * WorldGeneration.GetRunSettingFloat("moodnormalizationrate")
                : 0f;
            AppendLine(sb, "状态", body.sleeping ? "睡眠" : "无意识");
            AppendLine(sb, "基础变化", normalization > 0f ? $"{normalization:0.###}/s 趋向 0" : "无");
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

            AppendLine(sb, "直接变化", SignedFine(directRate, "/s"));
            AppendLine(sb, "反胃程度消耗", $"-{sicknessDrain:0.###}/s");
            AppendLine(sb, "饥饿消耗", $"-{hungerDrain:0.###}/s");
            AppendLine(sb, "口渴消耗", $"-{thirstDrain:0.###}/s");
            AppendLine(sb, "疼痛消耗", $"-{painDrain:0.###}/s");
            AppendLine(sb, "失血消耗", $"-{bleedDrain:0.###}/s");
            AppendLine(sb, "核心体温消耗", $"-{temperatureDrain:0.###}/s");
            AppendLine(sb, "吃饱增益", $"+{wellFedGain:0.###}/s");
            sb.AppendLine("基础情绪值会缓慢趋向 0。");
        }

        sb.AppendLine();
        sb.Append("状态图标档位: >50, >10, >-10, >-30, >-50, >-75, 低于 -75 时进入危险状态。")
            .Append("\n低于 -75 时, 医疗面板无法使用非阿片类物品。");
        return sb.ToString();
    }

    private static string BuildEnergyTooltip(Body body)
    {
        string SleepQualityName(Body.SleepQuality q)
        {
            switch (q)
            {
                case Body.SleepQuality.Bad: return "糟糕";
                case Body.SleepQuality.Mediocre: return "一般";
                case Body.SleepQuality.Okay: return "普通";
                case Body.SleepQuality.Good: return "优质";
                default: return "普通";
            }
        }

        float sleepCycleSpeed = WorldGeneration.GetRunSettingFloat("sleepcyclespeed");
        Painkillers painkillers = body.GetComponent<Painkillers>();
        float opiateDrain = painkillers != null && painkillers.actualOpiateReception > 30f
            ? painkillers.actualOpiateReception * 0.0025f
            : 0f;

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "当前", $"{body.energy:0.#}%");

        if (!body.alive)
        {
            AppendLine(sb, "状态", "死亡");
            AppendLine(sb, "变化", opiateDrain > 0f ? $"-{opiateDrain:0.###}/s 来自阿片类药物" : "无");
        }
        else if (!body.conscious)
        {
            Body.SleepQuality quality = body.forcedSleepQuality ?? body.curSleep;
            string qualityName = SleepQualityName(quality);
            float qualityMultiplier = body.SleepQualityToRegen(quality);
            float regeneration = 0.4f * qualityMultiplier * sleepCycleSpeed;
            float net = regeneration - opiateDrain;
            AppendLine(sb, "状态", body.sleeping ? $"睡眠 ({qualityName})" : $"无意识 ({qualityName})");
            AppendLine(sb, "基础睡眠恢复", "+0.4/s");
            AppendLine(sb, "睡眠质量", $"x{qualityMultiplier:0.##}");
            AppendLine(sb, "精力变化速度设置", $"x{sleepCycleSpeed:0.##}");
            if (opiateDrain > 0f)
            {
                AppendLine(sb, "高阿片类药物水平消耗", $"-{opiateDrain:0.###}/s");
            }
            AppendLine(sb, "净变化", SignedFine(net, "/s"));
        }
        else
        {
            float staminaFactor = 2f - body.stamina * 0.01f;
            float sicknessFactor = 1f + body.sicknessAmount * 0.02f;
            float moodFactor = 1f - Math.Clamp(body.totalHappiness * 0.01f, -1f, 0f);
            float caffeineFactor = body.caffeinated > 0f ? 0.55f : 1f;
            float drain = 0.07f * sleepCycleSpeed * staminaFactor * sicknessFactor * moodFactor * caffeineFactor;
            float netDrain = drain + opiateDrain;

            AppendLine(sb, "状态", "清醒");
            AppendLine(sb, "基础消耗", "-0.07/s");
            AppendLine(sb, "低体力值", $"x{staminaFactor:0.###}");
            AppendLine(sb, "反胃程度", $"x{sicknessFactor:0.###}");
            AppendLine(sb, "负数情绪值", $"x{moodFactor:0.###}");
            AppendLine(sb, "咖啡因", $"x{caffeineFactor:0.##}");
            AppendLine(sb, "精力变化速度设置", $"x{sleepCycleSpeed:0.##}");
            if (opiateDrain > 0f)
            {
                AppendLine(sb, "高阿片类药物水平消耗", $"-{opiateDrain:0.###}/s");
            }
            AppendLine(sb, "净变化", $"-{netDrain:0.###}/s");
        }

        Body.SleepQuality currentQuality = body.forcedSleepQuality ?? body.curSleep;
        string currentQualityName = SleepQualityName(currentQuality);
        float normalWakeTarget = currentQuality == Body.SleepQuality.Bad
            ? 70f
            : currentQuality == Body.SleepQuality.Mediocre ? 85f : 99f;
        sb.AppendLine();
        sb.Append("低于 35% 时, 通常可以选择睡觉。")
            .Append(currentQualityName).Append("睡眠的正常醒来目标: ").Append(normalWakeTarget.ToString("0")).Append("%。 ")
            .Append("精力还会影响免疫力、体力恢复、核心体温和意识清醒度。");
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

        SetTooltip(label, "心率", BuildHeartRateTooltip(body));
        Transform pressureHover = view.transform.Find("StatMenu/PressureHover");
        if (pressureHover != null)
        {
            SetTooltip(pressureHover.gameObject, "血压", BuildBloodPressureTooltip(body));
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
        AppendLine(sb, "当前", $"{body.immunity:0.#}/200 (原始 {raw:0.#})");
        AppendLine(sb, "基础", "+100.0");
        AppendLine(sb, "饥饿", Signed(hunger));
        AppendLine(sb, "口渴", Signed(thirst));
        AppendLine(sb, "精力", Signed(energy));
        AppendLine(sb, "核心体温", Signed(temperature));
        AppendLine(sb, "血容量", Signed(blood));
        AppendLine(sb, "肮脏程度", Signed(-dirt));
        AppendLine(sb, "反胃程度", Signed(-sickness));
        AppendLine(sb, "辐射量", Signed(-radiation));
        if (antibiotic > 0f)
        {
            AppendLine(sb, "抗生素", "+70.0");
        }

        sb.AppendLine();
        sb.Append("感染速度曲线值: ").Append(body.curImmunityMult.ToString("0.###"));
        return sb.ToString();
    }

    private static string BuildPainTooltip(Body body)
    {
        float adrenalineReduction = body.curAdrenaline * 0.5f;
        float resilienceMult = 1f - body.skills.RESFrom10 * 0.025f;

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "当前", $"{body.averagePain:0.#}%");
        AppendLine(sb, "肾上腺素掩盖", $"-{adrenalineReduction:0.#} 每个肢体");
        AppendLine(sb, "韧性倍率", $"{resilienceMult:0.###}x");
        sb.AppendLine();
        sb.AppendLine("<color=#FFFFFF>伤势最重的肢体</color>");

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
        AppendLine(sb, "创伤", body.averagePain > 50f ? $"{body.averagePain * 0.0034f:0.###}/s" : "疼痛超过 50% 时开始增长");
        AppendLine(sb, "疼痛休克", body.averagePain > 75f ? $"{body.painShock:0%}" : "疼痛超过 75% 时开始增长");
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
        AppendLine(sb, "当前", $"{WeightTierName(body.weightOffset)}, {kg:0.0}kg (偏移 {body.weightOffset:0.#})");
        AppendLine(sb, "体重漂移", $"{SignedFine(kgDeltaPerMinute, "kg/min")} 当前饥饿状态下");
        AppendLine(sb, "负重上限", $"{body.maxEncumberance:0.#}u");
        AppendLine(sb, "负重上限影响", Signed(weightCapPenalty, "u (本局倍率修正前)"));
        AppendLine(sb, "恢复上限所需", kgToBestCap > 0f ? $"增重 {kgToBestCap:0.0}kg 可进入无惩罚区间" : "当前体重未降低负重上限");
        AppendLine(sb, "负重超重", $"{body.overEncumberance * 100f:0}%");
        sb.AppendLine();
        sb.AppendLine("<color=#FFFFFF>体重阶梯</color>");
        AppendWeightTier(sb, body.weightOffset, "肥胖臃肿", 50f, true);
        AppendWeightTier(sb, body.weightOffset, "略显圆润", 15f, true);
        sb.AppendLine("当前: " + new string('-', Mathf.Clamp(Mathf.RoundToInt(Mathf.InverseLerp(-80f, 100f, body.weightOffset) * 18f), 0, 18)));
        AppendWeightTier(sb, body.weightOffset, "略显消瘦", -15f, false);
        AppendWeightTier(sb, body.weightOffset, "身形瘦弱", -30f, false);
        AppendWeightTier(sb, body.weightOffset, "骨瘦如柴", -50f, false);
        sb.AppendLine();
        sb.Append($"{WeightKg(-55f):0.0}kg 或 {WeightKg(55f):0.0}kg 时闪烁; {WeightKg(-60f):0.0}kg 或 {WeightKg(60f):0.0}kg 时强制增加室颤进度。");
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
        AppendLine(sb, "显示净速率", LitersPerMinute(body, body.totalBleedSpeed));
        AppendLine(sb, "外出血总量", LitersPerMinute(body, external));
        AppendLine(sb, "内出血", LitersPerMinute(body, internalRate));
        AppendLine(sb, "血液再生", "-" + LitersPerMinute(body, regenRate));
        AppendLine(sb, "自然凝血速率", $"{body.bleedClottingSpeed:0.###}");
        AppendLine(sb, "流血速率倍率", $"{body.bleedingSpeedMultiplier:0.###}x");
        sb.AppendLine();
        sb.AppendLine("<color=#FFFFFF>出血最多的肢体</color>");
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
        AppendLine(sb, "当前", $"{liters:0.00}L ({body.bloodVolume:0.#})");
        AppendLine(sb, "血容量系数", $"{body.bloodVolumePercentage:0.###}x");
        AppendLine(sb, "血液再生", $"{LitersPerMinute(body, body.bloodRegenSpeed)} 来自饥饿/治疗");
        AppendLine(sb, "出血消耗", LitersPerMinute(body, externalInternalLoss));
        AppendLine(sb, "净变化", Signed((body.bloodRegenSpeed - externalInternalLoss) * 60f * 0.025f, "L/min"));
        if (body.bloodVolumePercentage < 0.6f)
        {
            AppendLine(sb, "血氧饱和度上限", $"{body.bloodVolumePercentage / 0.6f * 100f:0.#}% 因血量过低");
        }

        sb.AppendLine();
        sb.Append("低于 25 时闪烁。\n总出血大于 0.02L/min 且血容量低于 40 时进入危险状态, 血容量低于 30 时进入危重状态。"); // Body.isCriticallyDying
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
        AppendLine(sb, "当前", $"{body.hunger:0.#}");
        AppendLine(sb, "自然消耗", $"-{hungerDrainPerMinute:0.##}/min");
        AppendLine(sb, "免疫力", Signed(immunity));
        AppendLine(sb, "体重漂移", SignedFine(-weightOffsetPerMinute * 0.34f, "kg/min"));
        AppendLine(sb, "肢体愈合", $"{body.hungerLimbHealCurrent:0.###}x");
        if (moodDrain > 0f)
        {
            AppendLine(sb, "情绪值消耗", $"-{moodDrain:0.###}/s");
        }

        sb.AppendLine();
        sb.Append("低于 40 时, 降低负重上限和目标血压。\n降到 0 时, 损失肌肉健康度。\n低于 10 时进入危险状态。");
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
        AppendLine(sb, "当前", $"{body.thirst:0.#}");
        AppendLine(sb, "自然消耗", $"-{thirstDrainPerMinute:0.##}/min");
        AppendLine(sb, "免疫力", Signed(immunity));
        AppendLine(sb, "血压倍率", $"{body.thirstBloodPressure:0.###}x");
        if (moodDrain > 0f)
        {
            AppendLine(sb, "情绪值消耗", $"-{moodDrain:0.###}/s");
        }

        sb.AppendLine();
        sb.Append("低于 40 会降低负重上限。\n低于 0 时会增加血液黏稠度。\n高于 175 时会损伤脑组织完整度, 并可能诱发室颤。\n低于 10 时进入危险状态。");
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
            return "选择一个肢体以查看力量详情。";
        }

        float structure = limb.muscleHealth * 0.01f;
        float injury = (limb.broken || limb.dislocated || limb.splinted) ? 0f : 1f;
        float attached = limb.dismembered ? 0f : 1f;
        float painTerm = Mathf.Clamp01(1f - (Mathf.Max(limb.pain, body.averagePain * 0.9f) - body.curAdrenaline * 0.5f) * 0.007f);
        float oxygen = body.bloodOxygen * 0.01f;
        float stroke = limb.strokeAffected ? Mathf.Clamp01(1f - body.strokeAmount / 50f) : 1f;

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "当前", $"{limb.totalForce * 100f:0}%");
        AppendLine(sb, "肌肉健康度", $"{structure:0.###}x");
        AppendLine(sb, "骨折/脱臼", $"{injury:0.###}x");
        AppendLine(sb, "连接状态", $"{attached:0.###}x");
        AppendLine(sb, "疼痛/肾上腺素", $"{painTerm:0.###}x");
        AppendLine(sb, "血氧饱和度", $"{oxygen:0.###}x");
        if (limb.strokeAffected || body.strokeAmount > 0f)
        {
            AppendLine(sb, "中风", $"{stroke:0.###}x");
        }

        sb.AppendLine();
        sb.Append("肢体力量会影响三个方面: 手部能否正常使用, 腿部对移动能力的贡献, 以及所选肢体在医疗面板中显示的力量值。");
        return sb.ToString();
    }

    private static string BuildSicknessTooltip(Body body)
    {
        float metabolism = WorldGeneration.GetRunSettingFloat("metabolismrate");
        float decayPerMinute = 0.06f * metabolism * 60f;
        float immunityPenalty = body.sicknessAmount * 0.8f;

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "当前", $"{body.sicknessAmount:0.#}%");
        AppendLine(sb, "自然消退", $"-{decayPerMinute:0.#}%/min");
        AppendLine(sb, "免疫力惩罚", $"-{immunityPenalty:0.#}");
        if (body.sicknessAmount > 20f)
        {
            AppendLine(sb, "情绪值消耗", $"{Mathf.Clamp01(body.sicknessAmount * 0.01f) * 0.05f * metabolism:0.###}/s");
        }

        float radiationTarget = body.radiationSickness * 0.4f;
        if (radiationTarget > body.sicknessAmount)
        {
            AppendLine(sb, "辐射量目标", $"{radiationTarget:0.#}%");
        }

        sb.AppendLine();
        sb.AppendLine("<color=#FFFFFF>档位</color>");
        sb.AppendLine("10 / 30 / 50 / 75 显示更强的反胃程度状态图标。");
        sb.Append("95+ 时强制腹部感染, 感染会导致败血症进展从而进入危险或危重状态。");
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
        AppendLine(sb, "当前", $"{body.brainHealth:0.#}%");
        AppendLine(sb, "自然恢复", $"+{healingRate:0.####}/s");
        if (braingrowHeal > 0f)
        {
            AppendLine(sb, "增脑剂", $"+{braingrowHeal:0.####}/s 生效期间");
        }
        if (oxygenDrain > 0f)
        {
            AppendLine(sb, "低血氧饱和度", $"-{oxygenDrain:0.####}/s");
        }
        if (dyingDrain > 0f)
        {
            AppendLine(sb, "大脑濒死", $"-{dyingDrain:0.####}/s"); // 血压<10, 意识<5
        }
        if (thirstDrain > 0f)
        {
            AppendLine(sb, "水中毒", $"-{thirstDrain:0.####}/s");
        }
        if (strokeDrain > 0f)
        {
            AppendLine(sb, "中风", $"-{strokeDrain:0.####}/s");
        }
        if (heatDrain > 0f)
        {
            AppendLine(sb, "极端高温", $"-{heatDrain:0.####}/s");
        }
        if (radiationDrain > 0f)
        {
            AppendLine(sb, "辐射量", $"-{radiationDrain:0.####}/s");
        }
        AppendLine(sb, "净变化", Signed(net, "/s"));
        if (net > 0f)
        {
            AppendLine(sb, "恢复到 95%", TimeToTarget(body.brainHealth, 95f, net));
            AppendLine(sb, "恢复到 100%", TimeToTarget(body.brainHealth, 100f, net));
        }
        else
        {
            AppendLine(sb, "恢复预计时间", "未在恢复");
        }

        sb.AppendLine();
        sb.AppendLine("脑损伤效果在低于 95% 时出现。");
        sb.Append("每 20 mL 增脑剂随时间增加 10 点脑组织完整度, 单次服用超过 39mL 或重复用药将导致精神抹除。");
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
        AppendLine(sb, "当前", $"{body.radiationSickness:0.#}%");
        AppendLine(sb, "自然消退", $"-{naturalDecay:0.###}/s");
        if (antiradReduction > 0f)
        {
            AppendLine(sb, "抗辐射药", $"-{antiradReduction:0.###}/s 持续 {FormatDuration(antiradDuration)}");
        }

        AppendLine(sb, "反胃程度目标", $"{sicknessTarget:0.#}%");
        AppendLine(sb, "免疫力惩罚", $"-{body.radiationSickness * 0.5f:0.#}");
        if (brainDrain > 0f)
        {
            AppendLine(sb, "脑组织完整度消耗", $"-{brainDrain:0.####}/s");
        }
        if (bloodDrain > 0f)
        {
            AppendLine(sb, "血容量消耗", $"-{bloodDrain:0.####}/s");
        }
        if (thirstDrain > 0f)
        {
            AppendLine(sb, "口渴消耗", $"-{thirstDrain:0.####}/s");
        }

        sb.AppendLine();
        sb.AppendLine("高于 10 时消耗血容量, 高于 30 时发生肢体感染, 增加内出血并损伤皮肤健康度。");
        sb.Append("高于 60 时进入危险状态。");
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
        AppendLine(sb, "当前", $"{body.temperature:0.0}C ({Signed(body.tempDiffFromNormal, "C")})");
        if (WorldGeneration.world != null)
        {
            AppendLine(sb, "层级", $"{WorldGeneration.world.ambientTemperature:0.0}C");
        }
        AppendLine(sb, "移动能力倍率", $"{body.currentTemperatureMovementMult:0.###}x");
        AppendLine(sb, "免疫力影响", Signed(immunityEffect));
        AppendLine(sb, "身体总保温值", $"{body.GetTotalInsulation():0.###}x");
        AppendLine(sb, "保温性能", $"{body.clothingTemperature:0.###}");
        AppendLine(sb, "代谢产热", SignedFine(metabolicHeat, "/s")); // 函数调用错误, 精度太低始终显示为0/s
        if (hungerWarmth > 0f)
        {
            AppendLine(sb, "寒冷恢复", SignedFine(hungerWarmth, "/s")); // 函数调用错误, 精度太低始终显示为0/s
        }
        if (wetCooling > 0f)
        {
            AppendLine(sb, "潮湿降温", $"-{wetCooling:0.###}/s");
        }

        sb.AppendLine();
        sb.AppendLine("低于 28 时, 强制增加室颤进度。\n低于 29 时, 进入危险状态。\n低于 27 时, 进入危重状态。");
        sb.Append("\n高于 41 时, 进入危险状态。\n 高于 41.5 时, 进入危重状态, 高于 42 时, 减少脑组织完整度。");
        return sb.ToString();
    }

    private static string BuildBloodPressureTooltip(Body body)
    {
        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "血压", $"{body.bloodPressure:0.#} ({Mathf.RoundToInt(body.bloodPressure)}/{Mathf.RoundToInt(body.bloodPressure * 0.66f)})");
        AppendLine(sb, "心率", $"{body.heartRate:0.#} bpm");
        if (body.bloodPressureChangeFromMedicine > 0.5f)
        {
            AppendLine(sb, "药物", $"降压 x0.75 (剩余 {body.bloodPressureChangeFromMedicine:0.#}s)");
        }
        else if (body.bloodPressureChangeFromMedicine < -0.5f)
        {
            AppendLine(sb, "药物", $"升压 x1.25 (剩余 {-body.bloodPressureChangeFromMedicine:0.#}s)");
        }
        else
        {
            AppendLine(sb, "药物", "无");
        }
        AppendLine(sb, "血管直径", $"{body.bloodVesselSize:0.###}x ({VesselToneName(body.bloodVesselSize)})");
        AppendLine(sb, "血压系数", $"{1f / Mathf.Max(0.01f, body.bloodVesselSize):0.###}x 来自血管直径");
        sb.AppendLine();
        sb.AppendLine("血管直径越大表示血管舒张/血压越低, 直径越小表示血管收缩/血压越高。");
        sb.AppendLine("血管收缩 <0.97x; 正常 0.97-1.03x; 血管舒张 >1.03x.");
        sb.AppendLine();
        sb.AppendLine("<color=#FFFFFF>低血压</color> 110 / 96 / 83 / 60");
        sb.AppendLine("<color=#FFFFFF>高血压</color> 130 / 145 / 162 / 180");
        sb.AppendLine("危险状态警告: 低于 80 或高于 170。危重状态: 低于 70。");
        sb.Append("高于 180 开始中风判定。低于 10 且意识清醒度低于 5 时大脑濒死。");
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
            ? "上升 +1.5/s"
            : body.bloodPressure > pressureReference + 5f
                ? "下降 -1.5/s"
                : "保持";

        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "当前", $"{body.heartRate:0.#} bpm");
        AppendLine(sb, "目标", $"{target:0.#} bpm (当前值会向此靠拢)");
        sb.AppendLine();
        sb.AppendLine("<color=#FFFFFF>目标值来源</color>");
        AppendLine(sb, "基础", "+70 bpm");
        AppendLine(sb, "疼痛", Signed(painContribution, " bpm"));
        AppendLine(sb, "体力不足", Signed(staminaContribution, " bpm"));
        AppendLine(sb, "肾上腺素", Signed(adrenalineContribution, " bpm"));
        AppendLine(sb, "正血液黏稠度", Signed(viscosityContribution, " bpm"));
        AppendLine(sb, "核心体温", Signed(temperatureContribution, " bpm"));
        AppendLine(sb, "阿片类药物水平", Signed(opiateContribution, " bpm"));
        AppendLine(sb, "血压反应", $"{Signed(body.heartRatePressureOffset, " bpm")} ({pressureTrend})");
        AppendLine(sb, "室颤进度", Signed(fibrillationContribution, " bpm"));
        if (advancedFibrillationContribution > 0f)
        {
            AppendLine(sb, "重度室颤进度", Signed(advancedFibrillationContribution, " bpm"));
        }

        sb.AppendLine();
        AppendLine(sb, "血压实际值 / 参考值", $"{body.bloodPressure:0.#} / {pressureReference:0.#}");
        if (body.inCardiacArrest)
        {
            sb.AppendLine("<color=#FF7777>心脏骤停会将心率锁定为 0。</color>");
        }
        sb.AppendLine();
        sb.AppendLine("心动过缓 <60 (严重 <40); 心动过速 >110 (严重 >160, 危险状态 >200)。");
        sb.Append("高于 200 将推进室颤进度, 高于 280 时室颤进度陡增, 心脏骤停为低于 20。");
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
        AppendLine(sb, "当前", $"{body.bloodViscosity:0.#}");
        AppendLine(sb, "肺栓塞", body.hasPulmonaryEmbolism ? "已发生" : body.bloodViscosity > EmbolismRiskViscosity ? "高于 90 有风险" : "未检测到");
        AppendLine(sb, "血氧饱和度上限", $"{100f - Mathf.Abs(Mathf.MoveTowards(body.bloodViscosity, 0f, 40f)) * 0.4f:0.#}%");
        AppendLine(sb, "凝血倍率", $"x{Mathf.Clamp01(body.bloodViscosity.Remap(-100f, 0f, 0f, 1f)):0.##}");
        sb.AppendLine();
        sb.Append("高于 80 时增加室颤进度。\n高于 90 时可能触发肺栓塞。");
        return sb.ToString();
    }

    private static string BuildOpiateTooltip(Painkillers painkillers)
    {
        float amount = painkillers != null ? painkillers.opiateAmount : 0f;
        float tolerance = painkillers != null ? painkillers.opiateTolerance : 0f;
        float reception = painkillers != null ? painkillers.opiateReception : 0f;
        float actual = painkillers != null ? painkillers.actualOpiateReception : 0f;
        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "摄入量", $"{amount:0.#}");
        AppendLine(sb, "耐受", $"{tolerance:0.#}");
        AppendLine(sb, "受体水平", $"{reception:0.#}");
        AppendLine(sb, "实际受体水平", $"{actual:0.#}");
        AppendLine(sb, "疼痛缓解", actual > 0f ? $"{actual * 0.3f:0.#}/s 每个肢体" : "无");
        AppendLine(sb, "情绪值", Signed(actual > 0f ? actual : Mathf.Max(-80f, actual * 1.66f)));
        sb.AppendLine();
        sb.AppendLine("过量状态档位: 5 / 20 / 50 / 80");
        sb.Append("戒断状态档位: -5 / -15 / -25 / -34");
        return sb.ToString();
    }

    private static string BuildFibrillationTooltip(Body body, float rate)
    {
        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "进度", $"{body.fibrillationProgress:0.#}%");
        AppendLine(sb, "速率", Signed(rate, "%/s"));
        sb.AppendLine();
        sb.AppendLine("<color=#FFFFFF>活跃因素</color>");
        bool any = false;
        any |= AppendFactor(sb, body.bloodOxygen < 60f, $"血氧饱和度 {body.bloodOxygen:0.#}% < 60");
        any |= AppendFactor(sb, body.bloodPressure < 88f, $"血压 {body.bloodPressure:0.#} < 88");
        any |= AppendFactor(sb, body.heartRate > 200f, $"心率 {body.heartRate:0.#} > 200");
        any |= AppendFactor(sb, body.fibrillationForced, "强制室颤标记");
        any |= AppendFactor(sb, body.bloodViscosity > 80f, $"血液黏度度 {body.bloodViscosity:0.#} > 80");
        any |= AppendFactor(sb, body.temperature < 28.5f, $"核心体温 {body.temperature:0.#}C < 28.5");
        if (!any)
        {
            sb.AppendLine("没有上升因素, 室颤进度应当消退。");
        }

        sb.AppendLine();
        sb.Append("只要有活跃因素进度就会上升, 达到 100% 时终止。");
        return sb.ToString();
    }

    private static string BuildOxygenTooltip(Body body)
    {
        StringBuilder sb = NewTooltipBuilder();
        AppendLine(sb, "血氧", $"{body.bloodOxygen:0.#}%");
        AppendLine(sb, "呼吸", $"{body.respiratoryRate:0.#}/m");
        AppendLine(sb, "血胸上限", $"{100f - body.hemothorax * 0.3f:0.#}%");
        AppendLine(sb, "血液黏稠度上限", $"{100f - Mathf.Abs(Mathf.MoveTowards(body.bloodViscosity, 0f, 40f)) * 0.4f:0.#}%");
        sb.AppendLine();
        sb.Append("低于 80 时, 损伤脑组织健康度。\n低于 60 时, 促进室颤进度。");
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
        string label = active ? "当前 " + name : name;
        sb.Append("<color=#FFFFFF>").Append(label).Append("</color>: ");
        sb.Append(WeightKg(thresholdOffset).ToString("0.0")).Append("kg");
        if (!active)
        {
            sb.Append(" (距离 ").Append(Mathf.Abs(WeightKg(currentOffset) - WeightKg(thresholdOffset)).ToString("0.0")).Append("kg)");
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
            return "血管舒张";
        }

        if (bloodVesselSize < 0.97f)
        {
            return "血管收缩";
        }

        return "正常";
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
            return "肥胖臃肿";
        }

        if (weightOffset > 15f)
        {
            return "略显圆润";
        }

        if (weightOffset <= -50f)
        {
            return "骨瘦如柴";
        }

        if (weightOffset < -30f)
        {
            return "身形瘦弱";
        }

        if (weightOffset < -15f)
        {
            return "略显消瘦";
        }

        return "正常";
    }

    private static string TimeToTarget(float current, float target, float rate)
    {
        if (current >= target)
        {
            return "当前";
        }

        if (rate <= 0f)
        {
            return "未增长";
        }

        return FormatDuration((target - current) / rate);
    }

    private static string FormatDuration(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
        {
            return "未知";
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
        tip.tipName = "身体模型叠加层";
        tip.tipDesc = "调整身体模型叠加层。";
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
        editHeaderText.text = "拖动身体模型以移动位置 | 拖动边角以调整大小 | 按 Esc 退出";
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
            lines.Add("<color=#ffffff>已注射: 0.0mL");
        }
        else
        {
            lines.Add("<color=#ffffff>已注射:");
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

        return string.IsNullOrWhiteSpace(id) ? "<未知>" : id;
    }
}
