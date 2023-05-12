using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using BepInEx.Logging;

using HarmonyLib;

using System.Collections.Generic;
using System.Reflection;

namespace SoDMod
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        internal static new ManualLogSource Log;
        internal readonly ConfigEntry<bool> fixSmelly;
        internal readonly ConfigEntry<bool> buildAnywhere;
        internal readonly ConfigEntry<bool> buildForFree;

        public Plugin() {
            fixSmelly = Config.Bind("SoDMod", "Fix Smelly status", true, "Disabling smelly/installing the syncdisk is bugged and " +
                                                                         "only hides the GUI icon. This fixes it. Restart the game if you change this option.");
            buildAnywhere = Config.Bind("SoDMod", "Build Anywhere", false, "Enables usage of the \"Edit Decor\" button from anywhere");
            buildForFree = Config.Bind("SoDMod", "Build For Free", false, "Enables you to build everything for free");
        }
        public override void Load()
        {
            // Plugin startup logic
            Log = base.Log;
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} loaded");

            var harmony = new Harmony("SoDMod");
            if (fixSmelly.Value)
                harmony.PatchAll(typeof(FixHygiene));
            new BuildAnywherePatch(harmony, buildAnywhere);
            new BuildForFreePatch(harmony, buildForFree);
        }
    }

    internal static class FixHygiene {
        [HarmonyPatch(typeof(Player), nameof(Player.AddHygiene))]
        [HarmonyPrefix]
        public static void AddHygiene(Player __instance, ref float addVal) {
            if (UpgradeEffectController._instance.GetUpgradeEffect(SyncDiskPreset.Effect.noSmelly) > 0 || !Game._instance.smellyStatusEnabled)
                addVal = 1;
        }
    }

    internal class BuildAnywherePatch {
        private static ConfigEntry<bool> enabled;
        internal BuildAnywherePatch(Harmony harmony, ConfigEntry<bool> cfg) {
            enabled = cfg;
            harmony.PatchAll(GetType());
        }

        [HarmonyPatch(typeof(BioScreenController), nameof(BioScreenController.UpdateDecorEditButton))]
        [HarmonyPostfix]
        public static void UpdateDecorEditButton(BioScreenController __instance) {
            if (enabled.Value)
                __instance.editDecorButton.gameObject.SetActive(true);
        }

        [HarmonyPatch(typeof(BioScreenController), nameof(BioScreenController.DecorEditButton))]
        [HarmonyPrefix]
        public static bool DecorEditButton() {
            if (!enabled.Value)
                return true;
            InteractionController._instance.StartDecorEdit();
            return false;
        }
    }

    internal class BuildForFreePatch {
        private static ConfigEntry<bool> enabled;
        internal BuildForFreePatch(Harmony harmony, ConfigEntry<bool> cfg) {
            enabled = cfg;
            harmony.PatchAll(GetType());
            harmony.PatchAll(typeof(UpdatePurchaseabilityPatch));
            harmony.PatchAll(typeof(OnPlaceButtonPatch));
        }

        [HarmonyPatch(typeof(FirstPersonItemController), nameof(FirstPersonItemController.PlaceFurniture))]
        [HarmonyPrefix]
        public static void PlaceFurniture_Prefix(ref int __state) {
            if (enabled.Value) {
                __state = PlayerApartmentController._instance.furnPlacement.preset.cost;
                PlayerApartmentController._instance.furnPlacement.preset.cost = 0;
            }
        }

        [HarmonyPatch(typeof(FirstPersonItemController), nameof(FirstPersonItemController.PlaceFurniture))]
        [HarmonyPostfix]
        public static void PlaceFurniture_Postfix(int __state) {
            if (enabled.Value)
                PlayerApartmentController._instance.furnPlacement.preset.cost = __state;
        }

        [HarmonyPatch(typeof(PlayerApartmentController), nameof(PlayerApartmentController.GetCurrentCost))]
        [HarmonyPrefix]
        public static bool GetCurrentCost(ref int __result) {
            if (!enabled.Value)
                return true;
            __result = 0;
            return false;
        }

        [HarmonyPatch(typeof(PlayerApartmentController), nameof(PlayerApartmentController.ExecutePlacement))]
        [HarmonyPrefix]
        public static void ExecutePlacement_Prefix(PlayerApartmentController __instance, ref int __state) {
            if (enabled.Value) {
                __state = __instance.furnPlacement.preset.cost;
                __instance.furnPlacement.preset.cost = 0;
            }
        }

        [HarmonyPatch(typeof(PlayerApartmentController), nameof(PlayerApartmentController.ExecutePlacement))]
        [HarmonyPostfix]
        public static void ExecutePlacement_Postfix(PlayerApartmentController __instance, int __state) {
            if (enabled.Value)
                __instance.furnPlacement.preset.cost = __state;
        }

        private static class UpdatePurchaseabilityPatch {
            [HarmonyTargetMethods]
            public static IEnumerable<MethodBase> TargetMethods() {
                yield return AccessTools.Method(typeof(DecorElementController), nameof(DecorElementController.UpdatePurchaseAbility));
                yield return AccessTools.Method(typeof(ApartmentItemElementController), nameof(ApartmentItemElementController.UpdatePurchaseAbility));
            }

            [HarmonyPatch]
            [HarmonyPostfix]
            public static void UpdatePurchaseAbility(object __instance) {
                if (enabled.Value) {
                    var btn = (ButtonController)__instance.GetType().GetProperty("placeButton").GetValue(__instance);
                    btn.SetInteractable(true);
                }
            }
        }

        private static class OnPlaceButtonPatch {
            [HarmonyTargetMethods]
            public static IEnumerable<MethodBase> TargetMethods() {
                yield return AccessTools.Method(typeof(DecorElementController), nameof(DecorElementController.OnPlaceButton));
                yield return AccessTools.Method(typeof(ApartmentItemElementController), nameof(ApartmentItemElementController.OnPlaceButton));
            }

            private static void UpdatePriceInt(object obj, out int old, int neu) {
                var prop = obj.GetType().GetProperty("price");
                old = (int)prop.GetValue(obj);
                prop.SetValue(obj, neu);
            }

            [HarmonyPatch]
            [HarmonyPrefix]
            public static void OnPlaceButton_Prefix(object __instance, ref int __state) {
                if (enabled.Value)
                    UpdatePriceInt(__instance, out __state, 0);
            }

            [HarmonyPatch]
            [HarmonyPostfix]
            public static void OnPlaceButton_Postfix(object __instance, int __state) {
                if (enabled.Value)
                    UpdatePriceInt(__instance, out __state, __state);
            }
        }
    }
}
