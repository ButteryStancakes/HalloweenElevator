using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HalloweenElevator
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency(GUID_LOBBY_COMPATIBILITY, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal const string PLUGIN_GUID = "butterystancakes.lethalcompany.halloweenelevator", PLUGIN_NAME = "Halloween Elevator", PLUGIN_VERSION = "1.0.1";
        internal static new ManualLogSource Logger;

        const string GUID_LOBBY_COMPATIBILITY = "BMX.LobbyCompatibility";

        internal static ConfigEntry<float> configChance;
        internal static ConfigEntry<bool> configEclipse, configFog;

        void Awake()
        {
            Logger = base.Logger;

            if (Chainloader.PluginInfos.ContainsKey(GUID_LOBBY_COMPATIBILITY))
            {
                Logger.LogInfo("CROSS-COMPATIBILITY - Lobby Compatibility detected");
                LobbyCompatibility.Init();
            }

            configChance = Config.Bind(
                "Criteria",
                "Chance",
                0f,
                new ConfigDescription(
                    "The percentage chance for the elevator to be spookified any day. (0 = never, 1 = guaranteed, or anything in between - 0.5 = 50% chance)",
                    new AcceptableValueRange<float>(0f, 1f)));

            configEclipse = Config.Bind(
                "Criteria",
                "Eclipses",
                true,
                "Spookify the elevator when it's eclipsed.");

            configFog = Config.Bind(
                "Criteria",
                "Indoor Fog",
                true,
                "Spookify the elevator when the \"spooky fog\" event is active.");

            new Harmony(PLUGIN_GUID).PatchAll();

            Logger.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        }
    }

    [HarmonyPatch]
    class ClassicElevatorPatches
    {
        static bool halloween, done;
        static System.Random musicRandom = new();
        static AudioClip elevatorClip;

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.FinishGeneratingNewLevelClientRpc))]
        [HarmonyPostfix]
        static void RoundManager_Post_FinishGeneratingNewLevelClientRpc(RoundManager __instance)
        {
            if (__instance.currentMineshaftElevator == null || done)
                return;

            done = true;

            if (elevatorClip == null && __instance.currentMineshaftElevator.elevatorJingleMusic?.clip != null)
            {
                elevatorClip = __instance.currentMineshaftElevator.elevatorJingleMusic?.clip;
                Plugin.Logger.LogDebug("Cached original elevator music");
            }

            halloween = Spookify();

            Light neonLight = __instance.currentMineshaftElevator.transform.Find("AnimContainer/NeonLights/Point Light")?.GetComponent<Light>();
            if (neonLight != null)
                neonLight.colorTemperature = halloween ? 4131f : 6217f;

            if (halloween)
                musicRandom = new(StartOfRound.Instance.randomMapSeed);
        }

        static bool Spookify()
        {
            if (Plugin.configEclipse.Value && StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Eclipsed)
                return true;

            if (Plugin.configFog.Value && RoundManager.Instance.indoorFog.gameObject.activeSelf)
                return true;

            float chance = Plugin.configChance.Value;
            float roll = (float)(new System.Random(StartOfRound.Instance.randomMapSeed).NextDouble());
            Plugin.Logger.LogDebug($"RNG: {roll}");
            if (chance > 0f && (chance >= 1f || roll < chance))
                return true;

            System.DateTime date = System.DateTime.UtcNow;
            if (date.Month == 10 && (date.Day > 30 || date.Day == 22 || date.Day == 23))
                return true;

            return false;
        }

        [HarmonyPatch(typeof(MineshaftElevatorController), nameof(MineshaftElevatorController.SetElevatorMusicClientRpc))]
        [HarmonyPostfix]
        static void MineshaftElevatorController_Post_SetElevatorMusicClientRpc(MineshaftElevatorController __instance, bool setOn)
        {
            if (setOn)
            {
                bool isPlaying = __instance.elevatorJingleMusic.isPlaying;
                __instance.elevatorJingleMusic.Stop();

                if (halloween)
                {
                    if (__instance.elevatorMovingDown)
                        __instance.elevatorJingleMusic.clip = __instance.elevatorHalloweenClips[musicRandom.Next(__instance.elevatorHalloweenClips.Length)];
                    else
                        __instance.elevatorJingleMusic.clip = __instance.elevatorHalloweenClipsLoop[musicRandom.Next(__instance.elevatorHalloweenClipsLoop.Length)];
                }
                else if (elevatorClip != null)
                    __instance.elevatorJingleMusic.clip = elevatorClip;

                if (isPlaying)
                    __instance.elevatorJingleMusic.Play();
            }
        }

        [HarmonyPatch(typeof(MineshaftElevatorController), nameof(MineshaftElevatorController.OnDisable))]
        [HarmonyPostfix]
        static void MineshaftElevatorController_Post_OnDisable(MineshaftElevatorController __instance)
        {
            done = false;
        }
    }
}