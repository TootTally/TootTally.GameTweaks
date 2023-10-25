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
            ConfigFile config = new ConfigFile(configPath + CONFIG_NAME, true);
            config.SaveOnConfigSet = true;
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
                IncreaseTromboneRange = config.Bind("Misc", "IncreaseTromboneRange", false, "Increase the range of notes the trombone can play."),
                OptimizeGame = config.Bind("Misc", "OptimizeGame", false, "Instantiate and destroy notes as they enter and leave the screen."),
                SliderSamplePoints = config.Bind("Misc", "SliderSamplePoints", 8f, "Increase or decrease the quality of slides."),
                RememberMyBoner = config.Bind("RMB", "RememberMyBoner", true, "Remembers the things you selected in the character selection screen."),
                TootRainbow = config.Bind("RMB", "TootRainbow", false, "Remembers the tootrainbow you selected."),
                LongTrombone = config.Bind("RMB", "LongTrombone", false, "Remembers the longtrombone you selected."),
                CharacterID = config.Bind("RMB", "CharacterID", 0, "Remembers the character you selected."),
                TromboneID = config.Bind("RMB", "TromboneID", 0, "Remembers the trombone you selected."),
                VibeID = config.Bind("RMB", "VibeID", 0, "Remembers the vibe you selected."),
                SoundID = config.Bind("RMB", "SoundID", 0, "Remembers the sound you selected."),
            };

            settingPage = TootTallySettingsManager.AddNewPage("GameTweaks", "Game Tweaks", 40f, new Color(0, 0, 0, 0));
            settingPage?.AddSlider("ChampMeterSize", 0, 1, option.ChampMeterSize, false);
            settingPage?.AddSlider("MuteBtnAlpha", 0, 1, option.MuteButtonTransparency, false);
            settingPage?.AddToggle("HideTromboner", option.HideTromboner);
            settingPage?.AddToggle("SyncDuringSong", option.SyncDuringSong);
            settingPage?.AddToggle("TouchScreenMode", option.TouchScreenMode, (value) => GlobalVariables.localsettings.mousecontrolmode = value ? 0 : 1);
            settingPage?.AddToggle("SkipCardAnimation", option.SkipCardAnimation);
            settingPage?.AddToggle("OverwriteNoteSpacing", option.OverwriteNoteSpacing, OnOverwriteNoteSpacingToggle);
            settingPage?.AddToggle("IncreaseTromboneRange", option.IncreaseTromboneRange);
            settingPage?.AddToggle("OptimizeGame", option.OptimizeGame, OnOptimizeGameToggle);
            OnOptimizeGameToggle(option.OptimizeGame.Value);
            settingPage?.AddToggle("RememberMyBoner", option.RememberMyBoner);
            OnOverwriteNoteSpacingToggle(option.OverwriteNoteSpacing.Value);

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

            private const float semitone = 13.75f;
            private static Dictionary<float, AudioClip> _posToClipDict;

            [HarmonyPatch(typeof(GameController), nameof(GameController.loadSoundBundleResources))]
            [HarmonyPostfix]
            public static void SetLinePos(GameController __instance)
            {
                if (!Instance.option.IncreaseTromboneRange.Value) return;
                string folderPath = Path.Combine(Path.GetDirectoryName(Instance.Info.Location), "AudioClips");

                Plugin.Instance.StartCoroutine(LoadAudioClip(folderPath, clips => { __instance.trombclips.tclips = clips.ToArray(); OnClipsLoaded(__instance); }));
            }

            public static void OnClipsLoaded(GameController __instance)
            {
                var clipCount = __instance.trombclips.tclips.Length;

                _posToClipDict = new Dictionary<float, AudioClip>(clipCount);

                var value = (clipCount - (clipCount % 12)) * -semitone;
                for (int i = 0; i < clipCount; i++)
                {
                    _posToClipDict.Add(value, __instance.trombclips.tclips[i]);
                    var mod = i % 7;
                    if (mod == 2 || mod == 6)
                        value += semitone;
                    else
                        value += semitone * 2f;
                }
            }
            public static readonly List<char> notesLetters = new List<char> { 'C', 'D', 'E', 'F', 'G', 'A', 'B' };
            public static IEnumerator<UnityWebRequestAsyncOperation> LoadAudioClip(string folderPath, Action<List<AudioClip>> callback)
            {
                List<AudioClip> audioClips = new List<AudioClip>();

                var orderedFiles = Directory.GetFiles(folderPath).OrderBy(x => Path.GetFileNameWithoutExtension(x)[0], new NoteComparer()).OrderBy(x => Path.GetFileNameWithoutExtension(x)[1]);
                foreach (string f in orderedFiles)
                {
                    Plugin.Instance.LogInfo(f);
                    UnityWebRequest webRequest = UnityWebRequestMultimedia.GetAudioClip(f, AudioType.WAV);
                    yield return webRequest.SendWebRequest();
                    if (!webRequest.isNetworkError && !webRequest.isHttpError)
                        audioClips.Add(DownloadHandlerAudioClip.GetContent(webRequest));
                }

                if (audioClips.Count > 0)
                    callback(audioClips);
            }

            public class NoteComparer : IComparer<char>
            {
                public int Compare(char x, char y)
                {
                    if (notesLetters.FindIndex(l => l == x) < notesLetters.FindIndex(l => l == y)) return -1;
                    if (notesLetters.FindIndex(l => l == x) > notesLetters.FindIndex(l => l == y)) return 1;
                    return 0;
                }
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.playNote))]
            [HarmonyPrefix]
            public static bool OverwritePlayNote(GameController __instance)
            {
                if (!Instance.option.IncreaseTromboneRange.Value) return true;

                var pointerY = __instance.pointer.transform.localPosition.y;
                var clipPair = _posToClipDict.Aggregate((x, y) => Math.Abs(x.Key - pointerY) < Math.Abs(y.Key - pointerY) ? x : y);

                __instance.notestartpos = clipPair.Key;
                __instance.currentnotesound.clip = clipPair.Value;
                __instance.currentnotesound.Play();

                return false;
            }

            private static NoteStructure[] _noteArray;

            [HarmonyPatch(typeof(GameController), nameof(GameController.buildNotes))]
            [HarmonyPrefix]
            public static bool OverwriteBuildNotes(GameController __instance)
            {
                if (!Instance.option.OptimizeGame.Value) return true;

                BuildNoteArray(__instance, TrombLoader.Plugin.Instance.beatsToShow.Value);
                buildNotes(__instance);

                return false;
            }

            private static Queue<Coroutine> _currentCoroutines;

            [HarmonyPatch(typeof(GameController), nameof(GameController.animateOutNote))]
            [HarmonyPostfix]
            public static void OnGrabNoteRefsInstantiateNote(GameController __instance)
            {
                if (!Instance.option.OptimizeGame.Value) return;
                if (_noteInitIndex < __instance.leveldata.Count)
                {
                    _currentCoroutines.Enqueue(Instance.StartCoroutine(TootTallyAPIService.WaitForSecondsCallback(.1f,
                        delegate {
                            _currentCoroutines.Dequeue();
                            BuildSingleNote(__instance, _noteInitIndex);
                        })));
                }

            }

            [HarmonyPatch(typeof(PointSceneController), nameof(PointSceneController.Start))]
            [HarmonyPostfix]
            public static void StopAllNoteCoroutine()
            {
                while(_currentCoroutines.TryDequeue(out Coroutine c))
                {
                    Instance.StopCoroutine(c);
                };
            }

            private static decimal _preciseNoteHolderPosition;

            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPostfix]

            public static void GetNoteHolderStartPosition(GameController __instance)
            {
                _currentCoroutines = new Queue<Coroutine>();
                _preciseNoteHolderPosition = (decimal)__instance.noteholderr.anchoredPosition3D.x;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.syncTrackPositions))]
            [HarmonyPostfix]
            public static void SyncPreciseNoteHolder(float track_time, GameController __instance)
            {
                if (SyncOnlyOnce())
                    _preciseNoteHolderPosition = (decimal)(__instance.zeroxpos + track_time * -__instance.trackmovemult);
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.Update))]
            [HarmonyPostfix]
            public static void FixNoteHolderPosition(GameController __instance)
            {
                if (__instance.smooth_scrolling && !__instance.paused && __instance.musictrack.time > 0f)
                {
                    _preciseNoteHolderPosition -= (decimal)(Time.deltaTime * __instance.trackmovemult * __instance.smooth_scrolling_move_mult * __instance.smooth_scrolling_mod_mult);
                    __instance.noteholderr.anchoredPosition3D = new Vector3((float)_preciseNoteHolderPosition, 0f);
                }
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.activateNextNote))]
            [HarmonyPrefix]
            private static bool SkipFirstActivateNextNote(object[] __args) => (int)__args[0] != 1;

            private static int _noteInitIndex;

            private static void buildNotes(GameController __instance)
            {
                _noteInitIndex = 0;
                for (int i = 0; i < _noteArray.Length && _noteInitIndex < __instance.leveldata.Count; i++)
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
                bool previousNoteIsSlider = Mathf.Abs(previousNoteData[0] + previousNoteData[1] - noteData[0]) < 0.01f;
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
                {
                    __instance.allnotes[index - 1].transform.GetChild(1).gameObject.SetActive(!previousNoteIsSlider); //End of previous note
                    currentNote.noteStart.SetActive(!previousNoteIsSlider);
                }

                currentNote.noteRect.anchoredPosition3D = new Vector3(noteData[0] * __instance.defaultnotelength, noteData[2], 0f);
                currentNote.noteEndRect.localScale = isTapNote ? Vector2.zero : Vector3.one;

                currentNote.noteEndRect.anchoredPosition3D = new Vector3(__instance.defaultnotelength * noteData[1] - __instance.levelnotesize + 11.5f, noteData[3], 0f);
                if (!isTapNote)
                {
                    if (_noteInitIndex >= TrombLoader.Plugin.Instance.beatsToShow.Value)
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
                _noteInitIndex++;
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
            public static void OnCharSelect(object[] __args)
            {
                if (!Instance.option.RememberMyBoner.Value) return;
                Instance.option.CharacterID.Value = (int)__args[0];
            }
            [HarmonyPatch(typeof(CharSelectController_new), nameof(CharSelectController_new.chooseTromb))]
            [HarmonyPostfix]
            public static void OnColorSelect(object[] __args)
            {
                if (!Instance.option.RememberMyBoner.Value) return;
                Instance.option.TromboneID.Value = (int)__args[0];
            }
            [HarmonyPatch(typeof(CharSelectController_new), nameof(CharSelectController_new.chooseSoundPack))]
            [HarmonyPostfix]
            public static void OnSoundSelect(object[] __args)
            {
                if (!Instance.option.RememberMyBoner.Value) return;
                Instance.option.SoundID.Value = (int)__args[0];
            }
            [HarmonyPatch(typeof(CharSelectController_new), nameof(CharSelectController_new.clickVibeButton))]
            [HarmonyPostfix]
            public static void OnTromboneSelect(object[] __args)
            {
                if (!Instance.option.RememberMyBoner.Value) return;
                Instance.option.VibeID.Value = (int)__args[0];
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
            public ConfigEntry<bool> IncreaseTromboneRange { get; set; }
            public ConfigEntry<bool> OptimizeGame { get; set; }
            public ConfigEntry<float> SliderSamplePoints { get; set; }
            public ConfigEntry<bool> RememberMyBoner { get; set; }
            public ConfigEntry<bool> LongTrombone { get; set; }
            public ConfigEntry<bool> TootRainbow { get; set; }
            public ConfigEntry<int> CharacterID { get; set; }
            public ConfigEntry<int> SoundID { get; set; }
            public ConfigEntry<int> VibeID { get; set; }
            public ConfigEntry<int> TromboneID { get; set; }

        }
    }
}