using BaboonAPI.Hooks.Initializer;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using TootTally.Graphics;
using TootTally.Graphics.Animation;
using TootTally.Replays;
using TootTally.Spectating;
using TootTally.Utils;
using TootTally.Utils.TootTallySettings;
using UnityEngine;
using UnityEngine.Networking;
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
            ConfigFile config = new ConfigFile(configPath + CONFIG_NAME, true) { SaveOnConfigSet = true };
            option = new Options()
            {
                ChampMeterSize = config.Bind("General", "ChampMeterSize", 1f, "Resize the champ meter to make it less intrusive."),
                SyncDuringSong = config.Bind("General", "Sync During Song", false, "Allow the game to sync during a song, may cause lags but prevent desyncs."),
                HideTromboner = config.Bind("General", "Hide Tromboner", false, "Hide the Tromboner during gameplay."),
                RandomizeKey = config.Bind("General", "RandomizeKey", KeyCode.F5, "Press that key to randomize."),
                MuteButtonTransparency = config.Bind("General", "MuteBtnAlpha", .25f, "Change the transparency of the mute button."),
                TouchScreenMode = config.Bind("Misc", "TouchScreenMode", false, "Tweaks for touchscreen users."),
                OverwriteNoteSpacing = config.Bind("NoteSpacing", "OverwriteNoteSpacing", false, "Make the note spacing always the same."),
                NoteSpacing = config.Bind("NoteSpacing", "NoteSpacing", 280.ToString(), "Note Spacing Value"),
                SkipCardAnimation = config.Bind("Misc", "SkipCardAnimation", true, "Skip the animation when opening cards."),
                RemoveLyrics = config.Bind("Misc", "RemoveLyrics", false, "Remove Lyrics from songs."),
                OptimizeGame = config.Bind("Misc", "OptimizeGame", false, "Instantiate and destroy notes as they enter and leave the screen."),
                SliderSamplePoints = config.Bind("Misc", "SliderSamplePoints", 8f, "Increase or decrease the quality of slides."),
                RememberMyBoner = config.Bind("RMB", "RememberMyBoner", true, "Remembers the things you selected in the character selection screen."),
                TootRainbow = config.Bind("RMB", "TootRainbow", false, "Remembers the tootrainbow you selected."),
                LongTrombone = config.Bind("RMB", "LongTrombone", false, "Remembers the longtrombone you selected."),
                CharacterID = config.Bind("RMB", "CharacterID", 0, "Remembers the character you selected."),
                TromboneID = config.Bind("RMB", "TromboneID", 0, "Remembers the trombone you selected."),
                VibeID = config.Bind("RMB", "VibeID", 0, "Remembers the vibe you selected."),
                SoundID = config.Bind("RMB", "SoundID", 0, "Remembers the sound you selected."),
                AudioLatencyFix = config.Bind("Misc", "AudioLatencyFix", true, "Fix audio latency bug related when playing at different game speeds."),
                RemoveConfetti = config.Bind("Misc", "Remove Confetti", false, "Removes the confetti in the score screen.")
            };

            settingPage = TootTallySettingsManager.AddNewPage("GameTweaks", "Game Tweaks", 40f, new Color(0, 0, 0, 0));
            settingPage?.AddSlider("Champ Meter Size", 0, 1, option.ChampMeterSize, false);
            settingPage?.AddSlider("Mute Btn Alpha", 0, 1, option.MuteButtonTransparency, false);
            settingPage?.AddToggle("Hide Tromboner", option.HideTromboner);
            settingPage?.AddToggle("Sync During Song", option.SyncDuringSong);
            settingPage?.AddToggle("Touchscreen Mode", option.TouchScreenMode, (value) => GlobalVariables.localsettings.mousecontrolmode = value ? 0 : 1);
            settingPage?.AddToggle("Skip Card Animation", option.SkipCardAnimation);
            settingPage?.AddToggle("Overwrite Note Spacing", option.OverwriteNoteSpacing, OnOverwriteNoteSpacingToggle);
            settingPage?.AddToggle("Remove Lyrics", option.RemoveLyrics);
            settingPage?.AddToggle("Optimize Game", option.OptimizeGame, OnOptimizeGameToggle);
            OnOptimizeGameToggle(option.OptimizeGame.Value);
            settingPage?.AddToggle("Remember My Boner", option.RememberMyBoner);
            OnOverwriteNoteSpacingToggle(option.OverwriteNoteSpacing.Value);
            settingPage?.AddToggle("Fix Audio Latency", option.AudioLatencyFix);
            settingPage?.AddToggle("Remove Confetti", option.RemoveConfetti);

            Harmony.CreateAndPatchAll(typeof(GameTweaks), PluginInfo.PLUGIN_GUID);
            LogInfo($"Module loaded!");
        }

        public void OnOptimizeGameToggle(bool value)
        {
            if (value)
                settingPage?.AddSlider("SliderSamplePoints", 2, 50, option.SliderSamplePoints, true);
            else
                settingPage?.RemoveSettingObjectFromList("SliderSamplePoints");

        }

        public void OnOverwriteNoteSpacingToggle(bool value)
        {
            if (value)
                settingPage?.AddTextField("NoteSpacing", option.NoteSpacing.Value, false, OnNoteSpacingSubmit);
            else
                settingPage?.RemoveSettingObjectFromList("NoteSpacing");
        }

        public void OnNoteSpacingSubmit(string value)
        {
            if (int.TryParse(value, out var num) && num > 0)
                option.NoteSpacing.Value = num.ToString();
            else
                PopUpNotifManager.DisplayNotif("Value has to be a positive integer.");
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
                button.transform.position = new Vector3(-7.95f, 4.75f, 1f);
            }
            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPostfix]
            public static void HideTromboner(GameController __instance)
            {
                if (!Instance.option.HideTromboner.Value) return;
                __instance.puppet_human.SetActive(false);
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.buildNotes))]
            [HarmonyPrefix]
            public static void OverwriteNoteSpacing(GameController __instance)
            {
                if (!Instance.option.OverwriteNoteSpacing.Value) return;
                if (int.TryParse(Instance.option.NoteSpacing.Value, out var num) && num > 0)
                    __instance.defaultnotelength = (int)(100f / (__instance.tempo * ReplaySystemManager.gameSpeedMultiplier) * num * GlobalVariables.gamescrollspeed);

            }

            [HarmonyPatch(typeof(MuteBtn), nameof(MuteBtn.Start))]
            [HarmonyPostfix]
            public static void SetMuteButtonAlphaOnStart(MuteBtn __instance)
            {
                __instance.cg.alpha = Instance.option.MuteButtonTransparency.Value;
            }

            [HarmonyPatch(typeof(MuteBtn), nameof(MuteBtn.hoverOut))]
            [HarmonyPostfix]
            public static void UnMuteButtonHoverOut(MuteBtn __instance)
            {
                __instance.cg.alpha = Instance.option.MuteButtonTransparency.Value;
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
                if (Input.GetKey(KeyCode.Space)) return true; //Sync if holding down spacebar
                if (Instance.option.SyncDuringSong.Value) return true; //always sync if enabled
                if (ReplaySystemManager.wasPlayingReplay) return true; //always sync if watching replay
                if (SpectatingManager.IsSpectating) return true; //always sync if spectating someone

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

            [HarmonyPatch(typeof(CardSceneController), nameof(CardSceneController.showMultiPurchaseCanvas))]
            [HarmonyPostfix]
            public static void OverwriteMaximumOpeningCard(CardSceneController __instance)
            {
                if (!Instance.option.SkipCardAnimation.Value) return;
                __instance.multipurchase_maxpacks = (int)Mathf.Clamp(__instance.currency_toots / 499f, 1, 999);
            }

            [HarmonyPatch(typeof(CardSceneController), nameof(CardSceneController.clickedContinue))]
            [HarmonyPrefix]
            public static bool OverwriteOpeningCardAnimation(CardSceneController __instance)
            {
                if (!Instance.option.SkipCardAnimation.Value) return true;

                __instance.moveAwayOpenedCards();
                return __instance.multipurchase_opened_sacks >= __instance.multipurchase_chosenpacks;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.buildAllLyrics))]
            [HarmonyPrefix]
            public static bool OverwriteBuildAllLyrics() => !Instance.option.RemoveLyrics.Value;

            private static NoteStructure[] _noteArray;

            [HarmonyPatch(typeof(GameController), nameof(GameController.buildNotes))]
            [HarmonyPrefix]
            public static bool OverwriteBuildNotes(GameController __instance)
            {
                if (!Instance.option.OptimizeGame.Value || ReplaySystemManager.wasPlayingReplay) return true;

                BuildNoteArray(__instance, TrombLoader.Plugin.Instance.beatsToShow.Value);
                BuildNotes(__instance);

                return false;
            }

            private static Queue<Coroutine> _currentCoroutines;

            [HarmonyPatch(typeof(GameController), nameof(GameController.animateOutNote))]
            [HarmonyPostfix]
            public static void OnGrabNoteRefsInstantiateNote(GameController __instance)
            {
                if (!Instance.option.OptimizeGame.Value || ReplaySystemManager.wasPlayingReplay) return;

                if (__instance.beatstoshow < __instance.leveldata.Count)
                {
                    _currentCoroutines.Enqueue(Instance.StartCoroutine(WaitForSecondsCallback(.1f,
                        delegate
                        {
                            _currentCoroutines.Dequeue();
                            BuildSingleNote(__instance, __instance.beatstoshow);
                        })));
                }

            }

            public static IEnumerator<WaitForSeconds> WaitForSecondsCallback(float seconds, Action callback)
            {
                yield return new WaitForSeconds(seconds);
                callback();
            }


            [HarmonyPatch(typeof(PointSceneController), nameof(PointSceneController.Start))]
            [HarmonyPostfix]
            public static void StopAllNoteCoroutine()
            {
                while (_currentCoroutines.TryDequeue(out Coroutine c))
                {
                    Instance.StopCoroutine(c);
                };
            }


            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPostfix]

            public static void OnGameControllerStartInitRoutineQueue()
            {
                _currentCoroutines = new Queue<Coroutine>();
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.activateNextNote))]
            [HarmonyPrefix]
            public static bool RemoveActivateNextNote() => !Instance.option.OptimizeGame.Value || ReplaySystemManager.wasPlayingReplay;

            private static void BuildNotes(GameController __instance)
            {
                __instance.beatstoshow = 0;
                for (int i = 0; i < _noteArray.Length && __instance.beatstoshow < __instance.leveldata.Count; i++)
                {
                    BuildSingleNote(__instance, i);
                }
            }

            private static void BuildNoteArray(GameController __instance, int size)
            {
                _noteArray = new NoteStructure[size];
                for (int i = 0; i < size; i++)
                {
                    _noteArray[i] = new NoteStructure(GameObject.Instantiate<GameObject>(__instance.singlenote, new Vector3(0f, 0f, 0f), Quaternion.identity, __instance.noteholder.transform));
                }
            }


            private static void BuildSingleNote(GameController __instance, int index)
            {
                if (index > __instance.leveldata.Count - 1)
                    return;

                float[] previousNoteData = new float[]
                {
                    9999f,
                    9999f,
                    9999f,
                    0f,
                    9999f
                };
                if (index > 0)
                    previousNoteData = __instance.leveldata[index - 1];

                float[] noteData = __instance.leveldata[index];
                bool previousNoteIsSlider = Mathf.Abs(previousNoteData[0] + previousNoteData[1] - noteData[0]) <= 0.02f;
                bool isTapNote = noteData[1] <= 0.0625f && __instance.tempo > 50f && noteData[3] == 0f && !previousNoteIsSlider;
                if (noteData[1] <= 0f)
                {
                    noteData[1] = 0.015f;
                    __instance.leveldata[index][1] = 0.015f;
                }
                NoteStructure currentNote = _noteArray[index % _noteArray.Length];
                currentNote.CancelLeanTweens();
                currentNote.root.transform.localScale = Vector3.one;
                __instance.allnotes.Add(currentNote.root);
                __instance.flipscheme = previousNoteIsSlider && !__instance.flipscheme;

                currentNote.SetColorScheme(__instance.note_c_start, __instance.note_c_end, __instance.flipscheme);
                currentNote.noteDesigner.enabled = false;

                if (index > 0)
                    __instance.allnotes[index - 1].transform.GetChild(1).gameObject.SetActive(!previousNoteIsSlider || index - 1 >= __instance.leveldata.Count); //End of previous note
                currentNote.noteEnd.SetActive(!isTapNote);
                currentNote.noteStart.SetActive(!previousNoteIsSlider);

                currentNote.noteRect.anchoredPosition3D = new Vector3(noteData[0] * __instance.defaultnotelength, noteData[2], 0f);
                currentNote.noteEndRect.localScale = isTapNote ? Vector2.zero : Vector3.one;

                currentNote.noteEndRect.anchoredPosition3D = new Vector3(__instance.defaultnotelength * noteData[1] - __instance.levelnotesize + 11.5f, noteData[3], 0f);
                if (!isTapNote)
                {
                    if (index >= TrombLoader.Plugin.Instance.beatsToShow.Value)
                    {
                        currentNote.noteEndRect.anchorMin = currentNote.noteEndRect.anchorMax = new Vector2(1, .5f);
                        currentNote.noteEndRect.pivot = new Vector2(0.34f, 0.5f);
                    }
                }
                float[] noteVal = new float[]
                {
                    noteData[0] * __instance.defaultnotelength,
                    noteData[0] * __instance.defaultnotelength + __instance.defaultnotelength * noteData[1],
                    noteData[2],
                    noteData[3],
                    noteData[4]
                };
                __instance.allnotevals.Add(noteVal);
                float noteLength = __instance.defaultnotelength * noteData[1];
                float pitchDelta = noteData[3];
                foreach (LineRenderer lineRenderer in currentNote.lineRenderers)
                {
                    lineRenderer.gameObject.SetActive(!isTapNote);
                    if (isTapNote) continue;
                    if (pitchDelta == 0f)
                    {
                        lineRenderer.positionCount = 2;
                        lineRenderer.SetPosition(0, new Vector3(-3f, 0f, 0f));
                        lineRenderer.SetPosition(1, new Vector3(noteLength, 0f, 0f));
                    }
                    else
                    {
                        int sliderSampleCount = (int)Instance.option.SliderSamplePoints.Value;
                        lineRenderer.positionCount = sliderSampleCount;
                        lineRenderer.SetPosition(0, new Vector3(-3f, 0f, 0f));
                        for (int k = 1; k < sliderSampleCount; k++)
                        {
                            lineRenderer.SetPosition(k,
                            new Vector3(
                            noteLength / (sliderSampleCount - 1) * k,
                            __instance.easeInOutVal(k, 0f, pitchDelta, sliderSampleCount - 1),
                            0f));
                        }
                    }
                }
                __instance.beatstoshow++;
            }

            #region CharSelectPatches
            [HarmonyPatch(typeof(CharSelectController_new), nameof(CharSelectController_new.Start))]
            [HarmonyPrefix]
            public static void OnCharSelectStart()
            {
                if (!Instance.option.RememberMyBoner.Value) return;
                GlobalVariables.chosen_character = Instance.option.CharacterID.Value;
                GlobalVariables.chosen_trombone = Instance.option.TromboneID.Value;
                GlobalVariables.chosen_soundset = Math.Min(Instance.option.SoundID.Value, 5);
                GlobalVariables.chosen_vibe = Instance.option.VibeID.Value;
                GlobalVariables.show_toot_rainbow = Instance.option.TootRainbow.Value;
                GlobalVariables.show_long_trombone = Instance.option.LongTrombone.Value;

            }

            [HarmonyPatch(typeof(CharSelectController_new), nameof(CharSelectController_new.chooseChar))]
            [HarmonyPostfix]
            public static void OnCharSelect(int puppet_choice)
            {
                if (!Instance.option.RememberMyBoner.Value) return;
                Instance.option.CharacterID.Value = puppet_choice;
            }
            [HarmonyPatch(typeof(CharSelectController_new), nameof(CharSelectController_new.chooseTromb))]
            [HarmonyPostfix]
            public static void OnColorSelect(int tromb_choice)
            {
                if (!Instance.option.RememberMyBoner.Value) return;
                Instance.option.TromboneID.Value = tromb_choice;
            }
            [HarmonyPatch(typeof(CharSelectController_new), nameof(CharSelectController_new.chooseSoundPack))]
            [HarmonyPostfix]
            public static void OnSoundSelect(int sfx_choice)
            {
                if (!Instance.option.RememberMyBoner.Value) return;
                Instance.option.SoundID.Value = sfx_choice;
            }
            [HarmonyPatch(typeof(CharSelectController_new), nameof(CharSelectController_new.clickVibeButton))]
            [HarmonyPostfix]
            public static void OnTromboneSelect(int vibe_index)
            {
                if (!Instance.option.RememberMyBoner.Value) return;
                Instance.option.VibeID.Value = vibe_index;
            }
            [HarmonyPatch(typeof(CharSelectController_new), nameof(CharSelectController_new.clickExtraButton))]
            [HarmonyPostfix]
            public static void OnTogRainbowSelect()
            {
                if (!Instance.option.RememberMyBoner.Value) return;
                Instance.option.TootRainbow.Value = GlobalVariables.show_toot_rainbow;
                Instance.option.LongTrombone.Value = GlobalVariables.show_long_trombone;
            }
            #endregion

            [HarmonyPatch(typeof(GameController), nameof(GameController.buildNotes))]
            [HarmonyPrefix]
            public static void FixAudioLatency(GameController __instance)
            {
                if (!Instance.option.AudioLatencyFix.Value) return;

                if (GlobalVariables.practicemode != 1)
                    __instance.latency_offset = GlobalVariables.localsettings.latencyadjust * 0.001f * GlobalVariables.practicemode;
                else if (GlobalVariables.turbomode)
                    __instance.latency_offset = GlobalVariables.localsettings.latencyadjust * 0.002f;
                else
                    __instance.latency_offset = GlobalVariables.localsettings.latencyadjust * 0.001f * ReplaySystemManager.gameSpeedMultiplier;
            }

            [HarmonyPatch(typeof(ConfettiMaker), nameof(ConfettiMaker.startConfetti))]
            [HarmonyPrefix]
            public static bool RemoveAllConfetti() => !Instance.option.RemoveConfetti.Value;
        }



        public class Options
        {
            public ConfigEntry<float> ChampMeterSize { get; set; }
            public ConfigEntry<float> MuteButtonTransparency { get; set; }
            public ConfigEntry<bool> SyncDuringSong { get; set; }
            public ConfigEntry<KeyCode> RandomizeKey { get; set; }
            public ConfigEntry<bool> TouchScreenMode { get; set; }
            public ConfigEntry<bool> OverwriteNoteSpacing { get; set; }
            public ConfigEntry<string> NoteSpacing { get; set; }
            public ConfigEntry<bool> HideTromboner { get; set; }
            public ConfigEntry<bool> SkipCardAnimation { get; set; }
            public ConfigEntry<bool> RemoveLyrics { get; set; }
            public ConfigEntry<bool> OptimizeGame { get; set; }
            public ConfigEntry<float> SliderSamplePoints { get; set; }
            public ConfigEntry<bool> RememberMyBoner { get; set; }
            public ConfigEntry<bool> LongTrombone { get; set; }
            public ConfigEntry<bool> TootRainbow { get; set; }
            public ConfigEntry<int> CharacterID { get; set; }
            public ConfigEntry<int> SoundID { get; set; }
            public ConfigEntry<int> VibeID { get; set; }
            public ConfigEntry<int> TromboneID { get; set; }
            public ConfigEntry<bool> AudioLatencyFix { get; set; }
            public ConfigEntry<bool> RemoveConfetti { get; set; }

        }
    }
}