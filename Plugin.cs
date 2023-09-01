using BaboonAPI.Hooks.Initializer;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
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
                OptimizeGameLoop = config.Bind("General", "Optimize Game Lopp", false, "Rewrite the entire gameloop to increase framerate."),
                RandomizeKey = config.Bind("General", "RandomizeKey", KeyCode.F5, "Press that key to randomize."),
                MuteButtonTransparency = config.Bind("General", "MuteBtnAlpha", .25f, "Change the transparency of the mute button"),
                TouchScreenMode = config.Bind("Misc", "TouchScreenMode", false, "Tweaks for touchscreen users.")
            };

            settingPage = TootTallySettingsManager.AddNewPage("GameTweaks", "Game Tweaks", 40f, new Color(0, 0, 0, 0));
            settingPage?.AddSlider("ChampMeterSize", 0, 1, option.ChampMeterSize, false);
            settingPage?.AddSlider("MuteBtnAlpha", 0, 1, option.MuteButtonTransparency, false);
            settingPage?.AddToggle("SyncDuringSong", option.SyncDuringSong);
            settingPage?.AddToggle("TouchScreenMode", option.TouchScreenMode, (value) => GlobalVariables.localsettings.mousecontrolmode = value ? 0 : 1);
            settingPage?.AddToggle("OptimizingGameLoop", option.OptimizeGameLoop);

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
                button.transform.position = new Vector3(-7.95f, 4.75f, 1f);
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

            [HarmonyPatch(typeof(GameController), nameof(GameController.Update))]
            [HarmonyPrefix]
            public static bool OverwriteGameLoop(GameController __instance)
            {
                if (!Instance.option.OptimizeGameLoop.Value && !__instance.freeplay && !__instance.playingineditor && GlobalVariables.localsettings.acc_autotoot) return true;

                #region HealthMovementStuff
                float num4 = __instance.healthfill.transform.localPosition.x + ((-4.4f + __instance.currenthealth * 0.0368f - __instance.healthfill.transform.localPosition.x) * (6.85f * Time.deltaTime));
                __instance.healthfill.transform.localPosition = new Vector3(num4, __instance.healthposy, 0f);
                __instance.healthposy = __instance.healthposy > 3.08f ? -2 : __instance.healthposy + 13.5f * Time.deltaTime;
                #endregion

                #region NoteScrollingAndSomeOtherShit
                float currentTiming = __instance.musictrack.time - __instance.latency_offset - __instance.noteoffset;

                #region SongEndingAndTimerUpdate
                if (__instance.musictrack.time > __instance.levelendtime)
                {
                    if (__instance.levelendtime > 0f && !__instance.level_finished && !__instance.quitting)
                    {
                        __instance.level_finished = true;
                        __instance.curtainc.closeCurtain(true);
                        if (__instance.totalscore < 0)
                            __instance.totalscore = 0;
                        GlobalVariables.gameplay_scoretotal = __instance.totalscore;
                        GlobalVariables.gameplay_scoreperc = (float)__instance.totalscore / __instance.maxlevelscore;
                        GlobalVariables.gameplay_notescores = new int[]
                        {
                            __instance.scores_F,
                            __instance.scores_D,
                            __instance.scores_C,
                            __instance.scores_B,
                            __instance.scores_A
                        };
                    }
                }
                else
                    __instance.updateTimeCounter(currentTiming);
                #endregion

                #region BeatTickStuff
                if (__instance.musictrack.time > __instance.tempotimer)
                {
                    __instance.tempotimer = 60f / __instance.tempo * (__instance.beatnum + 1);
                    __instance.beatnum++;
                    __instance.timesigcount++;
                    if (__instance.timesigcount > __instance.beatspermeasure)
                    {
                        __instance.timesigcount = 1;
                        __instance.bgcontroller.tickbg();
                        if (__instance.beatspermeasure == 3)
                            __instance.flashLeftBounds();
                    }
                    if (__instance.beatspermeasure != 3)
                        __instance.flashLeftBounds();

                    __instance.breathscale = __instance.breathscale == 1f ? 0.75f : 1f;
                }

                if (__instance.musictrack.time > __instance.tempotimerdot)
                {
                    __instance.beatnumdot++;
                    __instance.tempotimerdot = 60f / (__instance.tempo * 4f) * __instance.beatnumdot;
                    __instance.animatePlayerDot();
                }
                #endregion

                #region Scrolling
                if (!__instance.paused)
                {
                    //find a way to simplify `__instance.musictrack.time < __instance.musictrack.clip.length`
                    if (!__instance.smooth_scrolling && __instance.musictrack.time < __instance.musictrack.clip.length)
                        __instance.noteholderr.anchoredPosition3D = new Vector3(__instance.zeroxpos + currentTiming * -__instance.trackmovemult, 0f, 0f);
                    else if (__instance.musictrack.time > 0f)
                    {
                        __instance.noteholderr.anchoredPosition3D = new Vector3(__instance.noteholderr.anchoredPosition3D.x - Time.deltaTime * __instance.trackmovemult * __instance.smooth_scrolling_move_mult * __instance.smooth_scrolling_mod_mult, 0f, 0f);
                        if (__instance.musictrack.time < __instance.musictrack.clip.length && Mathf.Abs(__instance.noteholderr.anchoredPosition3D.x - (__instance.zeroxpos + currentTiming * -__instance.trackmovemult)) >= 4f)
                            __instance.syncTrackPositions(currentTiming);
                    }

                    __instance.lyricsholderr.anchoredPosition3D = __instance.noteholderr.anchoredPosition3D;
                }
                #endregion

                #region BeatLinePositioningImNotSure??
                if (__instance.noteholderr.anchoredPosition3D.x - __instance.zeroxpos + __instance.allbeatlines[__instance.beatlineindex].anchoredPosition3D.x < 0f)
                {
                    __instance.maxbeatlinex += __instance.defaultnotelength * __instance.beatspermeasure;
                    __instance.allbeatlines[__instance.beatlineindex].anchoredPosition3D = new Vector3(__instance.maxbeatlinex, 0f, 0f);
                    __instance.beatlineindex = ++__instance.beatlineindex >= __instance.numbeatlines ? 0 : __instance.beatlineindex;
                }
                #endregion

                #region BGCheck
                if (__instance.bgindex < __instance.bgdata.Count && currentTiming > __instance.bgdata[__instance.bgindex][0])
                {
                    __instance.bgcontroller.bgMove((int)__instance.bgdata[__instance.bgindex][1]);
                    __instance.bgindex++;
                }
                #endregion

                #region ImprovZoneChecks
                if (__instance.improv_zones != null && __instance.improv_zones.Count > 0)
                {
                    var noteHolderPos = Math.Abs(__instance.noteholderr.anchoredPosition3D.x);
                    var zoneData = __instance.improv_zone_data[__instance.current_improv_zone];

                    if (!__instance.improv_zone_active && noteHolderPos > zoneData[0])
                    {
                        __instance.improv_zone_active = true;
                        __instance.improv_zone_text.transform.localScale = new Vector3(0.001f, 0.001f, 1f);
                        __instance.improv_zone_text.SetActive(true);
                        LeanTween.scale(__instance.improv_zone_text, new Vector3(1f, 1f, 1f), 0.1f).setEaseOutBack().setOnComplete(delegate ()
                        {
                            LeanTween.scale(__instance.improv_zone_text, new Vector3(0.9f, 0.9f, 1f), 60f / __instance.tempo).setEaseInOutQuad().setLoopPingPong();
                        });
                    }
                    else if (__instance.improv_zone_active && noteHolderPos > zoneData[1])
                    {
                        __instance.improv_zone_active = false;
                        LeanTween.cancel(__instance.improv_zone_text);
                        LeanTween.scale(__instance.improv_zone_text, new Vector3(0.9f, 0.001f, 1f), 0.25f).setEaseInQuart().setOnComplete(delegate ()
                        {
                            __instance.improv_zone_text.SetActive(false);
                        });
                        __instance.hideImprovZone(__instance.improv_zone_to_hide);
                        __instance.improv_zone_to_hide = __instance.improv_zone_data.Count > ++__instance.current_improv_zone + 1 ? __instance.improv_zone_objects[__instance.current_improv_zone] : null;
                        if (__instance.improv_zone_to_hide == null)
                            __instance.improv_zones = null;
                    }
                }
                #endregion

                #endregion

                #region VolumeControl
                if (!__instance.paused && (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.Equals)))
                    HandleKeysPresses(__instance, KeyCode.Minus, KeyCode.Equals);
                #endregion

                #region Keypress
                __instance.notebuttonpressed = __instance.isNoteButtonPressed() && !GlobalVariables.localsettings.acc_autotoot;
                #endregion

                //This just updates the counter, usually every 0.01s but now every frame
                __instance.scorecounter += Time.deltaTime;
                if (__instance.scorecounter > 0.01f)
                {
                    __instance.scorecounter = 0;
                    __instance.tallyScore();
                }

                #region TimeCheck
                float currentPosition = __instance.noteholderr.anchoredPosition3D.x - __instance.zeroxpos;
                currentPosition = currentPosition > 0f ? -1f : Mathf.Abs(currentPosition);
                #endregion

                #region NoteScoringAndNoteActive
                if (__instance.noteactive)
                {
                    __instance.currentnotetimestamp = __instance.musictrack.time;

                    if (currentPosition > __instance.currentnoteend)
                    {
                        __instance.activateNextNote(__instance.currentnoteindex);
                        __instance.note_end_timer = __instance.max_note_end_timer;
                        __instance.noteactive = false;
                        __instance.getScoreAverage();
                        __instance.grabNoteRefs(1);
                    }
                    else
                    {
                        float frameScore = 0f;
                        if (__instance.noteplaying)
                        {
                            float distance = Mathf.Abs(1f - (__instance.currentnoteend - currentPosition) / (__instance.currentnoteend - __instance.currentnotestart));
                            float pitchVariance = __instance.easeInOutVal(distance, 0f, __instance.currentnotepshift, 1f);
                            frameScore = 100f - Mathf.Abs(__instance.pointerrect.anchoredPosition.y - (__instance.currentnotestarty + pitchVariance));

                            frameScore += frameScore >= 99.25f ? 1f : 0f;

                            frameScore = Mathf.Clamp(frameScore, 0f, 100f);
                            __instance.notescoretotal += frameScore;
                        }

                        if (__instance.notescoresamples <= 0f)
                            __instance.notescoresamples = frameScore <= 0f ? 0.2f : 1f;
                        else
                            __instance.notescoresamples += frameScore <= 0f ? 0.2f : 1f;

                        __instance.notescoreaverage = __instance.notescoretotal / __instance.notescoresamples;
                    }
                }
                else //no note active
                {
                    __instance.noteactive = currentPosition > __instance.currentnotestart;
                    if (!__instance.released_button_between_notes && !__instance.notebuttonpressed)
                        __instance.released_button_between_notes = true;
                }
                #endregion

                #region MovementInputDetection
                if (!__instance.controllermode && !__instance.paused)
                {
                    float num10 = 0f;
                    if (GlobalVariables.localsettings.mouse_movementmode == 0)
                    {
                        Vector3 mousePosition = Input.mousePosition;
                        if (GlobalVariables.localsettings.mousecontrolmode <= 1)
                            num10 = mousePosition.y / Screen.height;
                        else
                            num10 = mousePosition.x / Screen.width;
                        num10 = Mathf.Clamp(num10, 0f, 10f);
                        num10 -= 0.5f;
                        num10 *= __instance.mousemult * (GlobalVariables.localsettings.sensitivity * 0.2f + 0.8f);
                    }
                    else
                    {
                        float movement = 0f;
                        if (GlobalVariables.localsettings.mousecontrolmode <= 1)
                            movement = Input.GetAxis("Mouse Y") * 0.01f * GlobalVariables.localsettings.sensitivity;
                        else
                            movement = Input.GetAxis("Mouse X") * 0.01f * GlobalVariables.localsettings.sensitivity;

                        __instance.mouse_rel_pos += movement;
                        __instance.mouse_rel_pos = Mathf.Clamp(__instance.mouse_rel_pos, 0f, 1f);
                        num10 -= 0.5f;
                        num10 = __instance.mouse_rel_pos;
                    }

                    if (GlobalVariables.localsettings.mousecontrolmode == 1 || GlobalVariables.localsettings.mousecontrolmode == 2)
                        num10 = -num10;

                    __instance.puppet_humanc.doPuppetControl(num10 * 2f);
                    __instance.puppet_humanc.vibrato = __instance.vibratoamt;

                    var posY = Mathf.Clamp(num10 * __instance.vsensitivity, -__instance.vbounds - __instance.outerbuffer, __instance.vbounds + __instance.outerbuffer);
                    Vector3 vector = new Vector2(__instance.zeroxpos - __instance.dotsize * 0.5f, posY);
                    Vector3 b = (vector - __instance.pointer.transform.localPosition) * (0.7f - GlobalVariables.localsettings.mouse_smoothing * 0.0066f);
                    __instance.pointer.transform.localPosition += b;
                }
                #endregion

                #region PauseAndRetryKeyDetection
                if (!__instance.quitting && __instance.musictrack.time > 0.5f)
                {
                    HandleKeysDown(__instance, KeyCode.Escape, KeyCode.R);
                    if (!Input.GetKey(KeyCode.R) && !__instance.retrying && __instance.restarttimer > 0f)
                    {
                        __instance.restarttimer = 0f;
                        __instance.restartfader.alpha = 0f;
                        for (int j = 0; j < 7; j++)
                        {
                            __instance.restartletters_all[j].SetActive(false);
                        }
                    }
                }
                #endregion

                #region TootingHandle
                if (__instance.notebuttonpressed && !__instance.noteplaying && !__instance.outofbreath && __instance.readytoplay)
                {
                    __instance.noteplaying = true;
                    __instance.setPuppetShake(true);
                    __instance.currentnotesound.time = 0f;
                    __instance.trombvol_current = __instance.trombvol_default;
                    __instance.currentnotesound.volume = __instance.trombvol_current;
                    __instance.playNote();
                }
                else if (!__instance.notebuttonpressed && __instance.noteplaying)
                {
                    __instance.noteplaying = false;
                    __instance.setPuppetShake(false);
                }
                #endregion

                #region BreathingHandlingAndTootSoundHandling
                if (__instance.noteplaying)
                {
                    if (__instance.currentnotesound.time > __instance.currentnotesound.clip.length - 1.25f)
                        __instance.currentnotesound.time = 1f;

                    float num11 = Mathf.Pow(__instance.notestartpos - __instance.pointer.transform.localPosition.y, 2f) * 6.8E-06f;
                    float num12 = (__instance.notestartpos - __instance.pointer.transform.localPosition.y) * (1f + num11);
                    if (num12 > 0f)
                        num12 = (__instance.notestartpos - __instance.pointer.transform.localPosition.y) * 0.696f;

                    __instance.currentnotesound.pitch = Mathf.Clamp(1f - num12 * __instance.pitchamount, 0.5f, 2f);

                    if (__instance.breathcounter < 1f)
                    {
                        __instance.breathcounter += Time.deltaTime * 0.22f * GlobalVariables.practicemode;
                        if (__instance.breathcounter > 1f)
                        {
                            __instance.breathcounter = 1f;
                            __instance.sfxrefs.outofbreath.Play();
                            __instance.breathglow.anchoredPosition3D = new Vector3(-380f, 0f, 0f);
                            __instance.outofbreath = true;
                            __instance.noteplaying = false;
                            __instance.setPuppetShake(false);
                            __instance.setPuppetBreath(true);
                            __instance.stopNote();
                        }
                    }
                }
                else
                {
                    __instance.trombvol_current = __instance.trombvol_current <= 0f ? 0 : __instance.trombvol_current - Time.deltaTime * 18f;
                    __instance.currentnotesound.volume = __instance.trombvol_current;
                    if (__instance.breathcounter > 0f)
                    {
                        __instance.breathcounter -= Time.deltaTime * (__instance.outofbreath ? 0.29f : 8.5f);
                        if (__instance.outofbreath)
                        {
                            __instance.breathglow.anchoredPosition3D = new Vector3(-380f + (__instance.breathcounter - 1f) * 120f, 0f, 0f);
                            if (__instance.breathcounter < 0f)
                            {
                                __instance.outofbreath = false;
                                __instance.setPuppetBreath(false);
                            }
                        }
                        __instance.breathcounter = Mathf.Max(0, __instance.breathcounter);
                    }
                }


                var idkwtfthisis = (__instance.breathcounter + 0.2f) * (Time.deltaTime * 310f);

                float num14 = __instance.topbreathr.anchoredPosition3D.y;
                num14 += idkwtfthisis;

                float num15 = __instance.bottombreathr.anchoredPosition3D.y;
                num15 -= idkwtfthisis;

                if (num14 > -75f)
                {
                    num14 = -131f;
                    num15 = -37f;
                }

                float x2 = 37f - 72f * __instance.breathcounter;
                __instance.topbreathr.anchoredPosition3D = new Vector3(x2, num14, 0f);
                __instance.bottombreathr.anchoredPosition3D = new Vector3(x2, num15, 0f);
                #endregion

                return false;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.tallyScore))]
            [HarmonyPrefix]
            public static bool OverwriteTallyScore(GameController __instance)
            {
                if (!Instance.option.OptimizeGameLoop.Value && !__instance.freeplay && !__instance.playingineditor && GlobalVariables.localsettings.acc_autotoot) return true;

                if (__instance.currentscore <= __instance.totalscore)
                {
                    var text = __instance.currentscore.ToString("n0");
                    var diff = (int)(Math.Max(2000, __instance.totalscore - __instance.currentscore) * 100f * Time.deltaTime);
                    __instance.currentscore = __instance.currentscore + diff < __instance.totalscore ? __instance.currentscore + diff : __instance.totalscore;
                    __instance.ui_score.text = text;
                    __instance.ui_score_shadow.text = text;
                }

                return false;
            }


            public static void HandleKeysPresses(GameController __instance, params KeyCode[] keys)
            {
                foreach (KeyCode k in keys)
                {
                    if (!Input.GetKeyDown(k)) return;

                    switch (k)
                    {
                        case KeyCode.Minus:
                            __instance.adjustTrackVolume(-1f);
                            break;
                        case KeyCode.Equals:
                            __instance.adjustTrackVolume(1f);
                            break;
                    }
                }
            }

            public static void HandleKeysDown(GameController __instance, params KeyCode[] keys)
            {
                foreach (KeyCode k in keys)
                {
                    if (!Input.GetKey(k)) return;

                    switch (k)
                    {
                        case KeyCode.Escape:
                            if (!__instance.level_finished && __instance.pausecontroller.done_animating)
                            {
                                __instance.notebuttonpressed = false;
                                __instance.musictrack.Pause();
                                __instance.sfxrefs.backfromfreeplay.Play();
                                __instance.puppet_humanc.shaking = false;
                                __instance.puppet_humanc.stopParticleEffects();
                                __instance.puppet_humanc.playCameraRotationTween(false);
                                __instance.paused = __instance.quitting = true;
                                __instance.pausecanvas.SetActive(true);
                                __instance.pausecontroller.showPausePanel();
                                Cursor.visible = true;
                                if (!__instance.track_is_pausable)
                                {
                                    __instance.curtainc.closeCurtain(false);
                                }
                            }
                            break;
                        case KeyCode.R:
                            if (!__instance.retrying)
                            {
                                __instance.restarttimer += Time.deltaTime * 0.65f;
                                __instance.restartfader.alpha = __instance.restarttimer * 3f;
                                for (int i = 0; i < 7; i++)
                                {
                                    if (!__instance.restartletters_all[i].activeSelf && __instance.restarttimer > 0.05f + i * 0.04f)
                                    {
                                        __instance.sfxrefs.click.volume = 0.5f;
                                        __instance.sfxrefs.click.Play();
                                        __instance.restartletters_all[i].SetActive(true);
                                        __instance.restartletters_all[i].transform.localScale = new Vector3(0.001f, 5f, 1f);
                                        LeanTween.scale(__instance.restartletters_all[i], new Vector3(1f, 1f, 1f), 0.15f).setEaseInOutQuart();
                                    }
                                }
                                if (__instance.restarttimer > 0.4f)
                                {
                                    __instance.notebuttonpressed = false;
                                    __instance.musictrack.Pause();
                                    __instance.sfxrefs.backfromfreeplay.Play();
                                    __instance.curtainc.closeCurtain(true);
                                    __instance.paused = __instance.retrying = __instance.quitting = true;
                                }
                            }
                            break;
                    }
                }
            }
        }

        public class Options
        {
            public ConfigEntry<float> ChampMeterSize { get; set; }
            public ConfigEntry<float> MuteButtonTransparency { get; set; }
            public ConfigEntry<bool> SyncDuringSong { get; set; }
            public ConfigEntry<bool> OptimizeGameLoop { get; set; }
            public ConfigEntry<KeyCode> RandomizeKey { get; set; }
            public ConfigEntry<bool> TouchScreenMode { get; set; }
        }
    }
}