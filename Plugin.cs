using BaboonAPI.Hooks.Initializer;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.IO;
using TootTally.Graphics;
using TootTally.Utils;
using TootTally.Utils.TootTallySettings;
using UnityEngine;
using UnityEngine.UI;

namespace TootTally.GameTweaks
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("TootTally", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin, ITootTallyModule
    {
        public static Plugin Instance;

        private const string CONFIG_NAME = "GameTweaks.cfg";
        public Options option;
        public ConfigEntry<bool> ModuleConfigEnabled { get; set; }
        public bool IsConfigInitialized { get; set; }
        public string Name { get => PluginInfo.PLUGIN_NAME; set => Name = value; }

        public ManualLogSource GetLogger => Logger;
        public static TootTallySettingPage settingPage;

        public void LogInfo(string msg) => Logger.LogInfo(msg);
        public void LogError(string msg) => Logger.LogError(msg);

        private void Awake()
        {
            if (Instance != null) return;
            Instance = this;
            
            GameInitializationEvent.Register(Info, TryInitialize);
        }

        private void TryInitialize()
        {
            // Bind to the TTModules Config for TootTally
            ModuleConfigEnabled = TootTally.Plugin.Instance.Config.Bind("Modules", "GameTweaks", true, "Game Tweaks with some quality of life changes.");
            TootTally.Plugin.AddModule(this);
        }

        public void LoadModule()
        {
            string configPath = Path.Combine(Paths.BepInExRootPath, "config/");
            ConfigFile config = new ConfigFile(configPath + CONFIG_NAME, true);
            option = new Options()
            {
                ChampMeterSize = config.Bind("General", "ChampMeterSize", 1f, "Resize the champ meter to make it less intrusive."),
                SyncDuringSong = config.Bind("General", "Sync During Song", false, "Allow the game to sync during a song, may cause lags but prevent desyncs."),
                RandomizeKey = config.Bind("General", "RandomizeKey", KeyCode.F5, "Press that key to randomize."),
                TouchScreenMode = config.Bind("Misc", "TouchScreenMode", false, "Tweaks for touchscreen users.")
            };

            settingPage = TootTallySettingsManager.AddNewPage("GameTweaks", "Game Tweaks", 40f, new Color(0,0,0,0));
            settingPage?.AddSlider("ChampMeterSize", 0, 1, option.ChampMeterSize, false);
            settingPage?.AddToggle("SyncDuringSong", option.SyncDuringSong);
            settingPage?.AddToggle("TouchScreenMode", option.TouchScreenMode);

            Harmony.CreateAndPatchAll(typeof(GameTweaks), PluginInfo.PLUGIN_GUID);
            LogInfo($"Module loaded!");
        }

        public void UnloadModule()
        {
            Harmony.UnpatchID(PluginInfo.PLUGIN_GUID);
            settingPage.Remove();
            LogInfo($"Module unloaded!");
        }

        public static class GameTweaks
        {
            public static bool _hasSyncedOnce;

            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPostfix]
            public static void FixChampMeterSize(GameController __instance)
            {
                if (Instance.option.ChampMeterSize.Value == 1f) return;
                //0.29f is the default localScale size
                __instance.champcontroller.letters[0].transform.parent.localScale = Vector2.one * 0.29f * Instance.option.ChampMeterSize.Value; // :skull: that's how the base game gets that object...
                __instance.healthmask.transform.parent.SetParent(__instance.champcontroller.letters[0].transform.parent, true);
                __instance.healthmask.transform.parent.localScale = Vector2.one * Instance.option.ChampMeterSize.Value;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPostfix]
            public static void TouchScreenPatch(GameController __instance)
            {
                if (!Instance.option.TouchScreenMode.Value) return;

                var gameplayCanvas = GameObject.Find("GameplayCanvas").gameObject;
                gameplayCanvas.GetComponent<GraphicRaycaster>().enabled = true;
                gameplayCanvas.transform.Find("GameSpace").transform.localScale = new Vector2(1, -1);
                var button = GameObjectFactory.CreateCustomButton(gameplayCanvas.transform, Vector2.zero, new Vector2(32, 32), AssetManager.GetSprite("Block64.png"), "PauseButton", delegate { OnPauseButtonPress(__instance); });
                button.transform.position = new Vector3(-7.95f, 4.75f,1f);
            }

            //Yoinked from DNSpy 
            //Token: 0x06000274 RID: 628 RVA: 0x000266D0 File Offset: 0x000248D0
            private static void OnPauseButtonPress(GameController __instance)
            {
                if (!__instance.quitting && __instance.musictrack.time > 0.5f && !__instance.level_finished && __instance.pausecontroller.done_animating && !__instance.freeplay)
                {
                    __instance.notebuttonpressed = false;
                    __instance.musictrack.Pause();
                    __instance.sfxrefs.backfromfreeplay.Play();
                    __instance.puppet_humanc.shaking = false;
                    __instance.puppet_humanc.stopParticleEffects();
                    __instance.puppet_humanc.playCameraRotationTween(false);
                    __instance.paused = true;
                    __instance.quitting = true;
                    __instance.pausecanvas.SetActive(true);
                    __instance.pausecontroller.showPausePanel();
                    Cursor.visible = true;
                }
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.startSong))]
            [HarmonyPostfix]
            public static void ResetSyncFlag()
            {
                _hasSyncedOnce = false;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.syncTrackPositions))]
            [HarmonyPrefix]
            public static bool SyncOnlyOnce()
            {
                if (Instance.option.SyncDuringSong.Value) return true; //always sync if enabled
                if (Replays.ReplaySystemManager.wasPlayingReplay) return true; //always sync if watching replay

                var previousSync = _hasSyncedOnce;
                _hasSyncedOnce = true;
                return !previousSync;
            }

            [HarmonyPatch(typeof(LevelSelectController), nameof(LevelSelectController.Update))]
            [HarmonyPostfix]
            public static void DetectKeyPressInLevelSelectController(LevelSelectController __instance)
            {
                if (Input.GetKeyDown(Instance.option.RandomizeKey.Value) && !__instance.randomizing)
                    __instance.clickRandomTrack();
            }
        }

        public class Options
        {
            public ConfigEntry<float> ChampMeterSize { get; set; }
            public ConfigEntry<bool> SyncDuringSong { get; set; }
            public ConfigEntry<KeyCode> RandomizeKey { get; set; }

            public ConfigEntry<bool> TouchScreenMode { get; set; }
        }
    }
}