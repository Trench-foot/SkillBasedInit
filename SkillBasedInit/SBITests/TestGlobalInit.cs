using CustomUnits;
using IRBTModUtils.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkillBasedInit;
using System.Collections.Generic;
using System.Reflection;

namespace SBITests
{
    public static class TestConsts
    {
        public const int FirstPhase = 1;
        public const int LastPhase = 30;
        public static UnitCfg DefaultUnitCfg = new UnitCfg()
        {
            TypeMod = 0,
            RandomnessMax = 0,
            RandomnessMin = 0,
            HesitationMax = -6,
            HesitationMin = -2,
            InspiredMax = 3,
            InspiredMin = 1,
            ProneModifierMax = -6,
            ProneModifierMin = -2,
            ShutdownModifierMax = -6,
            ShutdownModifierMin = -2,
            CrippledModifierMax = -6,
            CrippledModifierMin = -2,
            DefaultTonnage = 120f,
            InitBaseByTonnage = new Dictionary<int, int>
                {
                    {  5, 19 }, // 0-5
                    {  15, 18 }, // 10-15
                    {  25, 17 }, // 20-25
                    {  35, 16 }, // 30-35
                    {  45, 15 }, // 40-45
                    {  55, 14 }, // 50-55
                    {  65, 13 }, // 60-65
                    {  75, 12 }, // 70-75
                    {  85, 11 }, // 80-85
                    {  95, 10 }, // 90-95
                    { 100, 9 }, // 100
                    { 999, 6 } // 105+
                }
        };
        public static TurretCfg DefaultTurretCfg = new TurretCfg()
        {
            TypeMod = 0,
            RandomnessMax = 0,
            RandomnessMin = 5,
            HesitationMax = -6,
            HesitationMin = -2,
            InspiredMax = 3,
            InspiredMin = 1,
            ProneModifierMax = 6,
            ProneModifierMin = 2,
            ShutdownModifierMax = 6,
            ShutdownModifierMin = 2,
            CrippledModifierMax = 6,
            CrippledModifierMin = 2,
            DefaultTonnage = 120f,
            LightTonnage = 60f,
            MediumTonnage = 80f,
            HeavyTonnage = 100f,
            InitBaseByTonnage = new Dictionary<int, int>
                {
                    {  5, 19 }, // 0-5
                    {  15, 18 }, // 10-15
                    {  25, 17 }, // 20-25
                    {  35, 16 }, // 30-35
                    {  45, 15 }, // 40-45
                    {  55, 14 }, // 50-55
                    {  65, 13 }, // 60-65
                    {  75, 12 }, // 70-75
                    {  85, 11 }, // 80-85
                    {  95, 10 }, // 90-95
                    { 100, 9 }, // 100
                    { 999, 6 } // 105+
                }
        };

    }

    [TestClass]
    public static class TestGlobalInit
    {
        public static Harmony HarmonyInst;

        public static MethodInfo MI_IsMoraleInspired;
        public static MethodInfo MI_IsFuryInspired;
        public static MethodInfo MI_IsShutdown;
        public static MethodInfo MI_IsProne;
        public static MethodInfo MI_IsLegged;

        public static HarmonyMethod HM_AlwaysTrue;
        public static HarmonyMethod HM_AlwaysFalse;

        [AssemblyInitialize]
        public static void TestInitialize(TestContext testContext)
        {

            IRBTModUtils.Mod.Config = new IRBTModUtils.ModConfig();
            IRBTModUtils.Mod.Config.Init();

            // Newer IRBTModUtils builds Path.Combine the mod dir, so it can't be null
            string logDir = System.IO.Path.GetTempPath();
            IRBTModUtils.Mod.Log = new DeferringLogger(logDir, "sbi_tests_irbt", "IRBT", false, false);

            Mod.Log = new DeferringLogger(logDir, "sbi_tests", "SBI", false, false);

            ResetModConfig();

            CUSettings testCUSettings = new CustomUnits.CUSettings();
            testCUSettings.PartialMovementOnlyWalkByDefault = false;
            testCUSettings.AllowRotateWhileJumpByDefault = false;
            CustomUnits.Core.Settings = testCUSettings;

            // Initialize Harmony
            TestGlobalInit.HarmonyInst = Harmony.CreateAndPatchAll(typeof(Patch), "us.frostraptor.sbi.test");
            PropertyInfo isMoraleInspiredProp = AccessTools.Property(typeof(AbstractActor), "IsMoraleInspired");
            MI_IsMoraleInspired = isMoraleInspiredProp.GetMethod;
            PropertyInfo isFuryInspired = AccessTools.Property(typeof(AbstractActor), "IsFuryInspired");
            MI_IsFuryInspired = isFuryInspired.GetMethod;

            PropertyInfo isShutdown = AccessTools.Property(typeof(AbstractActor), "IsShutDown");
            MI_IsShutdown = isShutdown.GetMethod;

            PropertyInfo isProne = AccessTools.Property(typeof(Mech), "IsProne");
            MI_IsProne = isProne.GetMethod;

            PropertyInfo isLegged = AccessTools.Property(typeof(Mech), "IsLegged");
            MI_IsLegged = isLegged.GetMethod;

            HM_AlwaysFalse = new HarmonyMethod(typeof(AlwaysFalsePrefix), "Prefix");
            HM_AlwaysTrue = new HarmonyMethod(typeof(AlwaysTruePrefix), "Prefix");
        }

        // Builds a fresh Mod.Config so no test can leak settings into another. ShallowCopy shares the
        //   tonnage dictionary, so copy it as well.
        public static void ResetModConfig()
        {
            Mod.Config = new ModConfig();

            Mod.Config.Mech = CopyUnitCfg(TestConsts.DefaultUnitCfg);
            Mod.Config.Mech.TypeMod = 1;

            Mod.Config.Trooper = CopyUnitCfg(TestConsts.DefaultUnitCfg);
            Mod.Config.Trooper.TypeMod = 2;

            Mod.Config.Naval = CopyUnitCfg(TestConsts.DefaultUnitCfg);
            Mod.Config.Naval.TypeMod = 3;

            Mod.Config.Vehicle = CopyUnitCfg(TestConsts.DefaultUnitCfg);
            Mod.Config.Vehicle.TypeMod = 4;

            Mod.Config.Turret = (TurretCfg)CopyUnitCfg(TestConsts.DefaultTurretCfg);
            Mod.Config.Turret.TypeMod = 5;
        }

        private static UnitCfg CopyUnitCfg(UnitCfg source)
        {
            UnitCfg copy = source.ShallowCopy();
            copy.InitBaseByTonnage = new Dictionary<int, int>(source.InitBaseByTonnage);
            return copy;
        }

        [AssemblyCleanup]
        public static void TearDown()
        {

        }
    }

    // Every test class inherits this, so each test starts from the default config. MSTest runs base
    //   class [TestInitialize] methods before derived ones, so class-specific setup still applies on top.
    public abstract class SBITestBase
    {
        [TestInitialize]
        public void ResetConfigBeforeTest()
        {
            TestGlobalInit.ResetModConfig();
        }
    }

    // Harmony helper methods down here
    public static class AlwaysTruePrefix
    {
        static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    public static class AlwaysFalsePrefix
    {
        static bool Prefix(ref bool __result)
        {
            __result = false;
            return false;
        }
    }
}
