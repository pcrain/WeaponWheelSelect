using System;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using tk2dRuntime;
using System.Collections.Generic;
using InControl;
using HarmonyLib;
using System.IO;
using System.Reflection;

namespace RadialGunSelect
{
    public static class RadialGunSelectController
    {
        public static Shader radialShader;
        public static RadialSegment[] segments;
        private static int hoveredIndex;

        public static void Init()
        {
            // AssetBundle assetBundle = AssetsManager.LoadAssetBundleFromResource("RadialGunSelect/AssetBundles/RadialGunSelect");
            // radialShader = assetBundle.LoadAsset<Shader>("RadialSegmentShader");
            string platform =
                Application.platform == RuntimePlatform.LinuxPlayer ? "linux" :
                Application.platform == RuntimePlatform.OSXPlayer ? "osx" : "windows";

            AssetBundle assetBundle = LoadAssetBundleFromResource($"RadialGunSelect/AssetBundles/wwshaders-{platform}");
            radialShader = assetBundle.LoadAsset<Shader>("assets/weaponwheel.shader");
        }

        public static AssetBundle LoadAssetBundleFromResource(string filePath)
        {
            filePath = filePath.Replace("/", ".");
            filePath = filePath.Replace("\\", ".");
            using (Stream manifestResourceStream = Assembly.GetCallingAssembly().GetManifestResourceStream(filePath))
            {
                if (manifestResourceStream != null)
                {
                    byte[] array = new byte[manifestResourceStream.Length];
                    manifestResourceStream.Read(array, 0, array.Length);
                    return AssetBundle.LoadFromMemory(array);
                }
            }
            ETGModConsole.Log("No bytes found in " + filePath, false);
            return null;
        }

        [HarmonyPatch(typeof(GameUIRoot), nameof(GameUIRoot.HandleMetalGearGunSelect)/*, MethodType.Enumerator*/)]
        private class WeaponWheelPatch
        {
            private static bool Prefix(GameUIRoot __instance, PlayerController targetPlayer, int numToL, ref IEnumerator __result)
            {
                __result = RadialGunSelectController.HandleRadialGunSelect(targetPlayer, numToL);
                return false; // skip original method
            }
        }

        public static IEnumerator HandleRadialGunSelect(PlayerController targetPlayer, int numToL)
        {
            GameUIRoot UIRoot = GameUIRoot.Instance;
            dfGUIManager GUIManager = UIRoot.m_manager;

            GameUIAmmoController ammoController = UIRoot.ammoControllers[0];
            if (GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
            {
                ammoController = UIRoot.ammoControllers[(!targetPlayer.IsPrimaryPlayer) ? 0 : 1];
            }
            BraveInput inputInstance = BraveInput.GetInstanceForPlayer(targetPlayer.PlayerIDX);
            while (ammoController.IsFlipping)
            {
                if (!inputInstance.ActiveActions.GunQuickEquipAction.IsPressed && !targetPlayer.ForceMetalGearMenu)
                {
                    targetPlayer.DoQuickEquip();
                    yield break;
                }
                yield return null;
            }
            if (GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
            {
                UIRoot.ToggleItemPanels(false);
            }

            UIRoot.ClearGunName(targetPlayer.IsPrimaryPlayer);
            targetPlayer.SetInputOverride("metal gear");
            BraveTime.RegisterTimeScaleMultiplier(0.05f, UIRoot.gameObject);
            UIRoot.m_metalGearGunSelectActive = true;
            Tribool gunSelectPhase = Tribool.Unready;
            GunInventory playerInventory = targetPlayer.inventory;
            List<Gun> playerGuns = playerInventory.AllGuns;

            // no guns
            if (playerGuns.Count <= 1)
            {
                UIRoot.m_metalGearGunSelectActive = false;
                yield break;
            }

            float totalTimeMetalGeared = 0f;
            Dictionary<dfSprite, Gun> frameToGunMap = new Dictionary<dfSprite, Gun>();
            Pixelator.Instance.FadeColor = Color.black;
            int totalGunShift = 0;
            float ignoreStickTimer = 0f;
            Vector2 mousePosition = GetCenteredMousePosition();
            Vector2 lastMousePosition = mousePosition;
            dfLabel ammoLabel = new GameObject("RadialGunSelectLabel").AddComponent<dfLabel>();
            float cachedGuiScale = 0.0f;

            // LOOP
            while (UIRoot.m_metalGearGunSelectActive)
            {
                Pixelator.Instance.fade = Mathf.Lerp(1f, 0.5f, totalTimeMetalGeared * 8f);

                // kill if anything is preventing select
                if ((!inputInstance.ActiveActions.GunQuickEquipAction.IsPressed && !GameManager.Instance.PrimaryPlayer.ForceMetalGearMenu) || GameManager.IsBossIntro || GameManager.Instance.IsPaused || GameManager.Instance.IsLoadingLevel)
                {
                    UIRoot.m_metalGearGunSelectActive = false;
                    break;
                }

                // spawn sprites
                if (gunSelectPhase == Tribool.Unready)
                {
                    // unfold
                    UIRoot.GunventoryFolded = false;
                    yield return null;

                    // setup
                    int currentGunIndex = playerGuns.IndexOf(targetPlayer.CurrentGun);
                    hoveredIndex = currentGunIndex;

                    dfGUIManager manager = UIRoot.m_manager;

                    int selectedGunIndex = playerGuns.IndexOf(targetPlayer.CurrentGun);

                    segments = new RadialSegment[playerGuns.Count];
                    var gap = playerGuns.Count > 20 ? 0 : playerGuns.Count > 15 ? 2 : playerGuns.Count > 10 ? 5 : 10;
                    for (int i = 0; i < playerGuns.Count; i++)
                    {
                        // spawn segment
                        var angle = 360f / playerGuns.Count();
                        var rotation = i * angle;
                        var segment = new RadialSegment(201, angle - gap, rotation);
                        segments[i] = segment;

                        // set gun to segment
                        segment.AssignGun(playerGuns[i]);
                        segment.SetHovered(i == currentGunIndex);
                    }

                    ammoLabel.transform.parent = GUIManager.transform;
                    ammoLabel.Size = (Vector2.one * 500).WithY(64);
                    ammoLabel.TextScale *= 3;
                    ammoLabel.TextAlignment = TextAlignment.Center;
                    ammoLabel.BackgroundColor = Color.blue.WithAlpha(0.5f);
                    ammoLabel.Text = targetPlayer.CurrentGun.InfiniteAmmo ? "" : targetPlayer.CurrentGun.ammo + "/" + targetPlayer.CurrentGun.AdjustedMaxAmmo;
                    ammoLabel.Anchor = dfAnchorStyle.CenterHorizontal | dfAnchorStyle.CenterVertical;

                    gunSelectPhase = Tribool.Ready;
                }

                // HANDLE INPUT
                // mouse input
                GungeonActions currentActions = inputInstance.ActiveActions;
                InputDevice currentDevice = inputInstance.ActiveActions.Device;
                bool gunUp = inputInstance.IsKeyboardAndMouse(true) && currentActions.GunUpAction.WasPressed;
                bool gunDown = inputInstance.IsKeyboardAndMouse(true) && currentActions.GunDownAction.WasPressed;
                var targetIndex = hoveredIndex;

                mousePosition = GetCenteredMousePosition();

                var mouseAngle = Mathf.Atan2(mousePosition.x, mousePosition.y) * Mathf.Rad2Deg;
                mouseAngle = FMod(mouseAngle, 360);
                var mouseDistance = Vector2.Distance(Vector2.zero, mousePosition);

                var segmentWidth = GUIManager.UIScale * 3f * 201 / 2f;
                if (Vector2.Distance(mousePosition, lastMousePosition) >= 4f)
                {
                    if (mouseDistance > segmentWidth * 0.25f)
                    {
                        var anglePerSegment = 360 / segments.Length;
                        targetIndex = Mathf.FloorToInt(FMod((mouseAngle + 90) / anglePerSegment + 0.5f, segments.Length));
                    }
                    lastMousePosition = mousePosition;
                }
                else
                {
                    targetIndex -= gunUp ? 1 : gunDown ? -1 : 0;
                }

                // controller input
                if (!gunUp && !gunDown && currentDevice != null && (!inputInstance.IsKeyboardAndMouse(true) || GameManager.Options.AllowMoveKeysToChangeGuns))
                {
                    bool dpadUp = currentDevice.DPadRight.WasPressedRepeating || currentDevice.DPadUp.WasPressedRepeating;
                    bool dpadDown = currentDevice.DPadLeft.WasPressedRepeating || currentDevice.DPadDown.WasPressedRepeating;
                    if (dpadUp || dpadDown)
                        ignoreStickTimer = 0.25f;

                    if (ignoreStickTimer <= 0f)
                    {
                        if (Vector2.Distance(Vector2.zero, currentDevice.LeftStick.Value) >= 0.4f)
                        {
                            var anglePerSegment = 360 / segments.Length;
                            targetIndex = Mathf.FloorToInt(FMod((-currentDevice.LeftStick.Angle - 90) / anglePerSegment + 0.5f, segments.Length));
                        }
                    }
                    else
                    {
                        targetIndex -= dpadUp ? 1 : dpadDown ? -1 : 0;
                    }
                }

                // apply hover
                targetIndex = (int)FMod(targetIndex, segments.Length);
                if (hoveredIndex != targetIndex)
                {
                    segments[hoveredIndex].SetHovered(false);
                    segments[targetIndex].SetHovered(true);
                    hoveredIndex = targetIndex;
                    ammoLabel.Text = playerGuns[targetIndex].InfiniteAmmo ? "" : playerGuns[targetIndex].ammo + "/" + playerGuns[targetIndex].AdjustedMaxAmmo;
                }

                // run update
                float newGuiScale = GUIManager.PixelsToUnits();
                if (newGuiScale != cachedGuiScale)
                {
                    cachedGuiScale = newGuiScale;
                    float dfScale = GameUIUtility.GetCurrentTK2D_DFScale(GUIManager);
                    foreach (var seg in segments)
                        seg.Rescale(newGuiScale, dfScale);
                }

                UIRoot.GunventoryFolded = true;
                yield return null;
                ignoreStickTimer = Mathf.Max(0f, ignoreStickTimer - GameManager.INVARIANT_DELTA_TIME);
                totalTimeMetalGeared += GameManager.INVARIANT_DELTA_TIME;
            }

            // finally switch weapon
            if (targetPlayer.inventory.AllGuns.Count != 0)
            {
                targetPlayer.CacheQuickEquipGun();
                targetPlayer.ChangeToGunSlot(hoveredIndex);
                ammoController.SuppressNextGunFlip = false;
            }
            else
            {
                UIRoot.TemporarilyShowGunName(targetPlayer.IsPrimaryPlayer);
            }

            // return everything to normal
            foreach (var seg in segments)
                seg.Destroy();

            ammoLabel.Text = "";
            Pixelator.Instance.fade = 1f;
            BraveTime.ClearMultiplier(UIRoot.gameObject);
            targetPlayer.ClearInputOverride("metal gear");
            UIRoot.m_metalGearGunSelectActive = false;

            if (totalGunShift == 0 && totalTimeMetalGeared < 0.005f)
                targetPlayer.DoQuickEquip();

            ammoController.GunAmmoCountLabel.IsVisible = true;
            yield break;
        }

        static Vector2 GetCenteredMousePosition()
        {
            var screenCenter = Screen.safeArea.center;
            var mousePosition = BraveInput.GetInstanceForPlayer(0).MousePosition;
            mousePosition.y = (float)Screen.height - mousePosition.y;
            mousePosition = mousePosition - screenCenter;
            return mousePosition;
        }

        static float FMod(float x, float m)
        {
            return (x % m + m) % m;
        }

        // ---------------------------------------------------------------- \\
        //                                                                  \\
        // original method, with cleaned up obfuscation and variable names  \\
        //                                                                  \\
        // ---------------------------------------------------------------- \\

        // gungeon code is kinda sucky damn..

        public static IEnumerator OriginalHandleMetalGearGunSelect(PlayerController targetPlayer, int numToL)
        {
            GameUIRoot UIRoot = GameUIRoot.Instance;

            GameUIAmmoController ammoController = UIRoot.ammoControllers[0];
            if (GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
            {
                ammoController = UIRoot.ammoControllers[(!targetPlayer.IsPrimaryPlayer) ? 0 : 1];
            }
            BraveInput inputInstance = BraveInput.GetInstanceForPlayer(targetPlayer.PlayerIDX);
            while (ammoController.IsFlipping)
            {
                if (!inputInstance.ActiveActions.GunQuickEquipAction.IsPressed && !targetPlayer.ForceMetalGearMenu)
                {
                    targetPlayer.DoQuickEquip();
                    yield break;
                }
                yield return null;
            }
            if (GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
            {
                UIRoot.ToggleItemPanels(false);
            }

            UIRoot.ClearGunName(targetPlayer.IsPrimaryPlayer);
            targetPlayer.SetInputOverride("metal gear");
            BraveTime.RegisterTimeScaleMultiplier(0.05f, UIRoot.gameObject);
            UIRoot.m_metalGearGunSelectActive = true;
            Tribool gunSelectPhase = Tribool.Unready;
            List<dfSprite> additionalGunBoxesSecondary = UIRoot.additionalGunBoxesSecondary;
            List<dfSprite> additionalGunBoxes = UIRoot.additionalGunBoxes;
            List<dfSprite> additionalGunFrames = (!targetPlayer.IsPrimaryPlayer) ? additionalGunBoxesSecondary : additionalGunBoxes;
            GunInventory playerInventory = targetPlayer.inventory;
            List<Gun> playerGuns = playerInventory.AllGuns;
            dfSprite baseBoxSprite = ammoController.GunBoxSprite;

            // no guns
            if (playerGuns.Count <= 1)
            {
                UIRoot.m_metalGearGunSelectActive = false;
                yield break;
            }

            Vector3 originalBaseBoxSpriteRelativePosition = baseBoxSprite.RelativePosition;
            dfSprite boxToMoveOffTop = null;
            dfSprite boxToMoveOffBottom = null;
            int totalGunShift = 0;
            float totalTimeMetalGeared = 0f;
            bool isTransitioning = false;
            int queuedTransition = 0;
            float transitionSpeed = 12.5f;
            float boxWidth = baseBoxSprite.Size.x + 3f;
            List<tk2dSprite> noAmmoIcons = new List<tk2dSprite>();
            Dictionary<dfSprite, Gun> frameToGunMap = new Dictionary<dfSprite, Gun>();
            Pixelator.Instance.FadeColor = Color.black;
            bool triedQueueLeft = false;
            bool triedQueueRight = false;
            bool prevStickLeft = true;
            bool prevStickRight = true;
            float ignoreStickTimer = 0f;
            bool isLeftAligned = targetPlayer.IsPrimaryPlayer && GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER;

            while (UIRoot.m_metalGearGunSelectActive)
            {
                Pixelator.Instance.fade = Mathf.Lerp(1f, 0.5f, totalTimeMetalGeared * 8f);

                // kill if anything is preventing select
                if ((!inputInstance.ActiveActions.GunQuickEquipAction.IsPressed && !GameManager.Instance.PrimaryPlayer.ForceMetalGearMenu) || GameManager.IsBossIntro || GameManager.Instance.IsPaused || GameManager.Instance.IsLoadingLevel)
                {
                    UIRoot.m_metalGearGunSelectActive = false;
                    break;
                }

                // spawn sprites
                if (gunSelectPhase == Tribool.Unready)
                {
                    // unfold
                    UIRoot.GunventoryFolded = false;
                    UIRoot.StartCoroutine(UIRoot.HandlePauseInventoryFolding(targetPlayer, true, false, 0.1f, numToL, false));
                    yield return null;

                    // setup
                    for (int i = 0; i < additionalGunFrames.Count; i++)
                    {
                        dfSprite gunFrame = additionalGunFrames[i];
                        dfSprite gunSprite = gunFrame.transform.GetChild(0).GetComponent<dfSprite>();
                        UIRoot.SetFadeMaterials(gunFrame, isLeftAligned);
                        UIRoot.SetFadeMaterials(gunSprite, isLeftAligned);
                        float y = gunFrame.GUIManager.RenderCamera.WorldToViewportPoint(baseBoxSprite.transform.position + new Vector3(0f, baseBoxSprite.Size.y * (float)(additionalGunFrames.Count - (Mathf.Abs(numToL) + ((numToL != 0) ? -1 : 0))) * baseBoxSprite.PixelsToUnits(), 0f)).y;
                        float fadeXStart = 0f;
                        float fadeXEnd = 1f;
                        if (numToL < 0)
                        {
                            fadeXStart = gunFrame.GUIManager.RenderCamera.WorldToViewportPoint(additionalGunFrames[0].transform.position + new Vector3(boxWidth * -2f * baseBoxSprite.PixelsToUnits(), 0f, 0f)).x;
                        }
                        else if (numToL > 0)
                        {
                            fadeXEnd = gunFrame.GUIManager.RenderCamera.WorldToViewportPoint(additionalGunFrames[0].transform.position + new Vector3(boxWidth * 2f * baseBoxSprite.PixelsToUnits(), 0f, 0f)).x;
                        }
                        tk2dClippedSprite clippedSprite = gunFrame.GetComponentInChildren<tk2dClippedSprite>();
                        UIRoot.AssignClippedSpriteFadeFractions(clippedSprite, y, fadeXStart, fadeXEnd, isLeftAligned);
                        frameToGunMap.Add(gunFrame, playerGuns[(i + playerGuns.IndexOf(playerInventory.CurrentGun)) % playerGuns.Count]);

                        // setup no ammo
                        if (frameToGunMap[gunFrame].CurrentAmmo == 0)
                        {
                            clippedSprite.renderer.material.SetFloat("_Saturation", 0f);
                            tk2dSprite noAmmoIcon = clippedSprite.transform.Find("NoAmmoIcon").GetComponent<tk2dSprite>();
                            noAmmoIcon.transform.parent = gunFrame.transform;
                            noAmmoIcon.HeightOffGround = 2f;
                            noAmmoIcon.OverrideMaterialMode = tk2dBaseSprite.SpriteMaterialOverrideMode.OVERRIDE_MATERIAL_SIMPLE;
                            noAmmoIcon.renderer.material.shader = ShaderCache.Acquire("tk2d/BlendVertexColorFadeRange");
                            noAmmoIcon.renderer.material.SetFloat("_YFadeStart", Mathf.Min(0.75f, y));
                            noAmmoIcon.renderer.material.SetFloat("_YFadeEnd", 0.03f);
                            noAmmoIcon.renderer.material.SetFloat("_XFadeStart", fadeXStart);
                            noAmmoIcon.renderer.material.SetFloat("_XFadeEnd", fadeXEnd);
                            noAmmoIcon.scale = clippedSprite.scale;
                            noAmmoIcon.transform.position = gunFrame.GetCenter().Quantize(0.0625f * noAmmoIcon.scale.x);
                            noAmmoIcons.Add(noAmmoIcon);
                        }
                        UIRoot.SetFadeFractions(gunFrame, fadeXStart, fadeXEnd, y, isLeftAligned);
                        UIRoot.SetFadeFractions(gunSprite, fadeXStart, fadeXEnd, y, isLeftAligned);
                        gunFrame.Invalidate();
                    }
                    gunSelectPhase = Tribool.Ready;
                }
                else if (gunSelectPhase == Tribool.Ready)
                {
                    if (!isTransitioning)
                    {
                        if (triedQueueLeft || queuedTransition > 0)
                        {
                            isTransitioning = true;
                            queuedTransition = Mathf.Max(queuedTransition - 1, 0);
                            totalGunShift--;
                            if (boxToMoveOffTop != null)
                            {
                                UnityEngine.Object.Destroy(boxToMoveOffTop.gameObject);
                                boxToMoveOffTop = null;
                            }
                            dfSprite dfSprite2 = additionalGunFrames[additionalGunFrames.Count - 1];
                            if (numToL != 0 && additionalGunFrames.Count > 2)
                            {
                                dfSprite2 = additionalGunFrames[additionalGunFrames.Count - 2];
                            }
                            GameObject gameObject = UnityEngine.Object.Instantiate(dfSprite2.gameObject, dfSprite2.transform.position, Quaternion.identity);
                            boxToMoveOffTop = gameObject.GetComponent<dfSprite>();
                            dfSprite2.Parent.AddControl(boxToMoveOffTop);
                            boxToMoveOffTop.RelativePosition = dfSprite2.RelativePosition;
                            dfSprite topBoxSprite = boxToMoveOffTop.transform.GetChild(0).GetComponent<dfSprite>();
                            if (numToL != 0 && additionalGunFrames.Count > 2)
                            {
                                dfSprite2.RelativePosition = originalBaseBoxSpriteRelativePosition.WithX(originalBaseBoxSpriteRelativePosition.x + boxWidth * 2f * Mathf.Sign((float)numToL));
                            }
                            else
                            {
                                dfSprite2.RelativePosition = originalBaseBoxSpriteRelativePosition.WithY(originalBaseBoxSpriteRelativePosition.y + baseBoxSprite.Size.y);
                            }
                            UIRoot.SetFadeMaterials(boxToMoveOffTop, isLeftAligned);
                            UIRoot.SetFadeMaterials(topBoxSprite, isLeftAligned);
                            boxToMoveOffTop.Invalidate();
                            additionalGunFrames.Insert(0, additionalGunFrames[additionalGunFrames.Count - 1]);
                            additionalGunFrames.RemoveAt(additionalGunFrames.Count - 1);
                        }
                        else if (triedQueueRight || queuedTransition < 0)
                        {
                            isTransitioning = true;
                            queuedTransition = Mathf.Min(queuedTransition + 1, 0);
                            totalGunShift++;
                            if (boxToMoveOffBottom != null)
                            {
                                UnityEngine.Object.Destroy(boxToMoveOffBottom.gameObject);
                                boxToMoveOffBottom = null;
                            }
                            dfSprite dfSprite3 = additionalGunFrames[0];
                            if (numToL != 0 && additionalGunFrames.Count > 2)
                            {
                                dfSprite3 = additionalGunFrames[additionalGunFrames.Count - 1];
                            }
                            GameObject gameObject2 = UnityEngine.Object.Instantiate<GameObject>(dfSprite3.gameObject, dfSprite3.transform.position, Quaternion.identity);
                            boxToMoveOffBottom = gameObject2.GetComponent<dfSprite>();
                            dfSprite3.Parent.AddControl(boxToMoveOffBottom);
                            boxToMoveOffBottom.RelativePosition = dfSprite3.RelativePosition;
                            dfSprite component4 = boxToMoveOffBottom.transform.GetChild(0).GetComponent<dfSprite>();
                            if (numToL != 0 && additionalGunFrames.Count > 2)
                            {
                                dfSprite3.RelativePosition = originalBaseBoxSpriteRelativePosition.WithY(originalBaseBoxSpriteRelativePosition.y - baseBoxSprite.Size.y * (float)(additionalGunFrames.Count - 1));
                            }
                            else
                            {
                                dfSprite3.RelativePosition = originalBaseBoxSpriteRelativePosition.WithY(originalBaseBoxSpriteRelativePosition.y - baseBoxSprite.Size.y * (float)additionalGunFrames.Count);
                            }
                            UIRoot.SetFadeMaterials(boxToMoveOffBottom, isLeftAligned);
                            UIRoot.SetFadeMaterials(component4, isLeftAligned);
                            boxToMoveOffBottom.Invalidate();
                            additionalGunFrames.Add(additionalGunFrames[0]);
                            additionalGunFrames.RemoveAt(0);
                        }
                    }
                    else if (isTransitioning)
                    {
                        if (triedQueueLeft)
                        {
                            queuedTransition++;
                            triedQueueLeft = false;
                        }
                        else if (triedQueueRight)
                        {
                            queuedTransition--;
                            triedQueueRight = false;
                        }
                    }

                    bool finishedTransition = true;
                    for (int j = 0; j < additionalGunFrames.Count; j++)
                    {
                        dfSprite dfSprite4 = additionalGunFrames[j];
                        float num3 = 1f / (float)(additionalGunFrames.Count + 1);
                        Vector3 a = originalBaseBoxSpriteRelativePosition - baseBoxSprite.Size.WithX(0f).ToVector3ZUp(0f) * (float)(j - 1);
                        Vector3 b = a - baseBoxSprite.Size.WithX(0f).ToVector3ZUp(0f);
                        if (numToL != 0 && additionalGunFrames.Count > 2 && j == additionalGunFrames.Count - 1)
                        {
                            a = originalBaseBoxSpriteRelativePosition;
                            b = a + new Vector3(boxWidth, 0f, 0f) * Mathf.Sign((float)numToL);
                        }
                        float num4 = num3 * (float)j;
                        float t = Mathf.Clamp01((1f - num4) / num3);
                        float t2 = Mathf.SmoothStep(0f, 1f, t);
                        Vector3 vector = Vector3.Lerp(a, b, t2);
                        if (dfSprite4.RelativePosition.IntXY(VectorConversions.Round) != vector.IntXY(VectorConversions.Round))
                        {
                            finishedTransition = false;
                        }
                        float num5 = GameManager.INVARIANT_DELTA_TIME * baseBoxSprite.Size.y * transitionSpeed;
                        float maxDeltaX = num5 * (baseBoxSprite.Size.x / baseBoxSprite.Size.y);
                        dfSprite4.RelativePosition = BraveMathCollege.LShapedMoveTowards(dfSprite4.RelativePosition, vector, maxDeltaX, num5);
                    }

                    if (finishedTransition)
                        isTransitioning = false;

                    if (boxToMoveOffTop != null)
                    {
                        Vector3 a2 = originalBaseBoxSpriteRelativePosition - baseBoxSprite.Size.WithX(0f).ToVector3ZUp(0f) * (float)(additionalGunFrames.Count - 1 - Mathf.Abs(numToL));
                        Vector3 vector2 = a2 - baseBoxSprite.Size.WithX(0f).ToVector3ZUp(0f);
                        float num6 = GameManager.INVARIANT_DELTA_TIME * baseBoxSprite.Size.y * transitionSpeed;
                        float maxDeltaX2 = num6 * (baseBoxSprite.Size.x / baseBoxSprite.Size.y);
                        boxToMoveOffTop.RelativePosition = BraveMathCollege.LShapedMoveTowards(boxToMoveOffTop.RelativePosition, vector2, maxDeltaX2, num6);
                        if (boxToMoveOffTop.RelativePosition.IntXY(VectorConversions.Round) == vector2.IntXY(VectorConversions.Round))
                        {
                            UnityEngine.Object.Destroy(boxToMoveOffTop.gameObject);
                            boxToMoveOffTop = null;
                        }
                    }
                    if (boxToMoveOffBottom != null)
                    {
                        Vector3 a3 = originalBaseBoxSpriteRelativePosition;
                        Vector3 vector3 = a3 + baseBoxSprite.Size.WithX(0f).ToVector3ZUp(0f);
                        if (numToL != 0 && additionalGunFrames.Count > 2)
                        {
                            a3 = originalBaseBoxSpriteRelativePosition + new Vector3(boxWidth * Mathf.Sign((float)numToL), 0f, 0f);
                            vector3 = a3 + new Vector3(boxWidth * Mathf.Sign((float)numToL), 0f, 0f);
                        }
                        float num7 = GameManager.INVARIANT_DELTA_TIME * baseBoxSprite.Size.y * transitionSpeed;
                        float maxDeltaX3 = num7 * (baseBoxSprite.Size.x / baseBoxSprite.Size.y);
                        boxToMoveOffBottom.RelativePosition = BraveMathCollege.LShapedMoveTowards(boxToMoveOffBottom.RelativePosition, vector3, maxDeltaX3, num7);
                        if (boxToMoveOffBottom.RelativePosition.IntXY(VectorConversions.Round) == vector3.IntXY(VectorConversions.Round))
                        {
                            UnityEngine.Object.Destroy(boxToMoveOffBottom.gameObject);
                            boxToMoveOffBottom = null;
                        }
                    }
                }

                GungeonActions currentActions = inputInstance.ActiveActions;
                InputDevice currentDevice = inputInstance.ActiveActions.Device;
                bool gunUp = inputInstance.IsKeyboardAndMouse(true) && currentActions.GunUpAction.WasPressed;
                bool gunDown = inputInstance.IsKeyboardAndMouse(true) && currentActions.GunDownAction.WasPressed;
                if (targetPlayer.ForceMetalGearMenu)
                    gunUp = true;

                if (!gunUp && !gunDown && currentDevice != null && (!inputInstance.IsKeyboardAndMouse(true) || GameManager.Options.AllowMoveKeysToChangeGuns))
                {
                    bool dpadUp = currentDevice.DPadRight.WasPressedRepeating || currentDevice.DPadUp.WasPressedRepeating;
                    bool dpadDown = currentDevice.DPadLeft.WasPressedRepeating || currentDevice.DPadDown.WasPressedRepeating;
                    if (dpadUp || dpadDown)
                        ignoreStickTimer = 0.25f;

                    bool stickDown = false;
                    bool stickUp = false;
                    if (ignoreStickTimer <= 0f)
                    {
                        stickDown |= (currentDevice.LeftStickDown.RawValue > 0.4f || currentActions.Down.RawValue > 0.4f);
                        stickDown |= (currentDevice.LeftStickLeft.RawValue > 0.4f || currentActions.Left.RawValue > 0.4f);
                        stickUp |= (currentDevice.LeftStickUp.RawValue > 0.4f || currentActions.Up.RawValue > 0.4f);
                        stickUp |= (currentDevice.LeftStickRight.RawValue > 0.4f || currentActions.Right.RawValue > 0.4f);
                    }
                    triedQueueLeft = (dpadDown || (stickDown && !prevStickLeft));
                    triedQueueRight = (dpadUp || (stickUp && !prevStickRight));
                    prevStickLeft = stickDown;
                    prevStickRight = stickUp;
                }
                else
                {
                    triedQueueLeft = gunUp;
                    triedQueueRight = gunDown;
                }
                yield return null;
                ignoreStickTimer = Mathf.Max(0f, ignoreStickTimer - GameManager.INVARIANT_DELTA_TIME);
                totalTimeMetalGeared += GameManager.INVARIANT_DELTA_TIME;
            }

            if (GameManager.Instance.CurrentGameType == GameManager.GameType.COOP_2_PLAYER)
                UIRoot.ToggleItemPanels(true);

            Pixelator.Instance.fade = 1f;
            if (boxToMoveOffTop != null)
            {
                UnityEngine.Object.Destroy(boxToMoveOffTop.gameObject);
                boxToMoveOffTop = null;
            }
            if (boxToMoveOffBottom != null)
            {
                UnityEngine.Object.Destroy(boxToMoveOffBottom.gameObject);
                boxToMoveOffBottom = null;
            }
            totalGunShift -= queuedTransition;
            if (totalGunShift % targetPlayer.inventory.AllGuns.Count != 0)
            {
                targetPlayer.CacheQuickEquipGun();
                targetPlayer.ChangeGun(totalGunShift, true, false);
                ammoController.SuppressNextGunFlip = true;
            }
            else
            {
                UIRoot.TemporarilyShowGunName(targetPlayer.IsPrimaryPlayer);
            }
            BraveTime.ClearMultiplier(UIRoot.gameObject);
            targetPlayer.ClearInputOverride("metal gear");
            UIRoot.m_metalGearGunSelectActive = false;

            if (totalGunShift == 0 && totalTimeMetalGeared < 0.005f)
                targetPlayer.DoQuickEquip();

            for (int k = 0; k < noAmmoIcons.Count; k++)
                UnityEngine.Object.Destroy(noAmmoIcons[k].gameObject);

            UIRoot.GunventoryFolded = true;
            yield return UIRoot.StartCoroutine(UIRoot.HandlePauseInventoryFolding(targetPlayer, true, false, 0.25f, numToL, true));
            ammoController.GunAmmoCountLabel.IsVisible = true;
            yield break;
        }
    }
}
