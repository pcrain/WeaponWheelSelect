using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using InControl;

namespace WeaponWheelSelect
{
    public static class WeaponWheelSelectController
    {
        private const int _SEGMENT_SIZE = 201;
        internal static Shader radialShader;

        private static RadialSegment[] segments = null;
        private static dfLabel ammoLabel = null;
        private static bool weaponWheelActive = false;

        public static void Init()
        {
            string platform =
                Application.platform == RuntimePlatform.LinuxPlayer ? "linux" :
                Application.platform == RuntimePlatform.OSXPlayer ? "osx" : "windows";
            AssetBundle assetBundle = LoadAssetBundleFromResource($"WeaponWheelSelect.AssetBundles.wwshaders-{platform}");
            radialShader = assetBundle.LoadAsset<Shader>("assets/weaponwheel.shader");
        }

        private static AssetBundle LoadAssetBundleFromResource(string filePath)
        {
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

        private static Vector2 GetCenteredMousePosition()
        {
            Vector2 mousePosition = BraveInput.GetInstanceForPlayer(0).MousePosition;
            mousePosition.y = (float)Screen.height - mousePosition.y;
            return mousePosition - Screen.safeArea.center;
        }

        [HarmonyPatch(typeof(GameUIRoot), nameof(GameUIRoot.HandleMetalGearGunSelect))]
        private class WeaponWheelPatch
        {
            private static bool Prefix(GameUIRoot __instance, PlayerController targetPlayer, int numToL, ref IEnumerator __result)
            {
                __result = WeaponWheelSelectController.HandleWeaponWheelSelect(targetPlayer, numToL);
                return false; // skip original method
            }
        }

        private static void CleanUpWeaponWheel(PlayerController targetPlayer, GameUIAmmoController ammoController)
        {
            foreach (RadialSegment seg in segments)
                seg.Destroy();

            if (ammoLabel != null)
            {
                // ammoLabel.Text = "";
                UnityEngine.Object.Destroy(ammoLabel.gameObject);
                ammoLabel = null;
            }
            Pixelator.Instance.fade = 1f;
            BraveTime.ClearMultiplier(GameUIRoot.Instance.gameObject);
            targetPlayer.ClearInputOverride("metal gear");
            GameUIRoot.Instance.m_metalGearGunSelectActive = false;
            ammoController.GunAmmoCountLabel.IsVisible = true;
            weaponWheelActive = false;
        }

        private static IEnumerator HandleWeaponWheelSelect(PlayerController targetPlayer, int numToL)
        {
            if (weaponWheelActive)
                yield break; // don't allow multiple players to have weapon wheel active at the same time

            weaponWheelActive = true;
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
            Pixelator.Instance.FadeColor = Color.black;
            int totalGunShift = 0;
            float ignoreStickTimer = 0f;
            Vector2 mousePosition = GetCenteredMousePosition();
            Vector2 lastMousePosition = mousePosition;
            ammoLabel = new GameObject("WeaponWheelSelectLabel").AddComponent<dfLabel>();
            float cachedGuiScale = 0.0f;
            int hoveredIndex = 0;
            int numGuns = 0;

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
                    numGuns = playerGuns.Count;  // cache after yield return null above on the off chance it changed
                    segments = new RadialSegment[numGuns];
                    hoveredIndex = Mathf.Clamp(playerGuns.IndexOf(targetPlayer.CurrentGun), 0, numGuns - 1);
                    int gap = numGuns > 20 ? 0 : numGuns > 15 ? 2 : numGuns > 10 ? 5 : 10;
                    float dfScale = GameUIUtility.GetCurrentTK2D_DFScale(GUIManager);
                    float angle = 360f / numGuns;
                    for (int i = 0; i < numGuns; i++)
                    {
                        // spawn segment
                        float rotation = i * angle;
                        RadialSegment segment = new RadialSegment(_SEGMENT_SIZE, angle - gap, rotation);
                        segments[i] = segment;

                        // set gun to segment
                        segment.AssignGun(playerGuns[i], dfScale);
                        segment.SetHovered(i == hoveredIndex);
                    }

                    ammoLabel.transform.parent = GUIManager.transform;
                    ammoLabel.Size = new Vector2(500, 64);  //TODO: make this work for co-op
                    ammoLabel.TextScale *= 3;
                    ammoLabel.TextAlignment = TextAlignment.Center;
                    ammoLabel.BackgroundColor = Color.blue.WithAlpha(0.5f);
                    //TODO: make this mirror actual ammo display

                    ammoLabel.ProcessMarkup = true;
                    ammoLabel.ColorizeSymbols = false;
                    ammoLabel.Text = targetPlayer.CurrentGun.InfiniteAmmo
                        ? "[sprite \"infinite-big\"]"
                        : targetPlayer.CurrentGun.ammo + "/" + targetPlayer.CurrentGun.AdjustedMaxAmmo;
                    ammoLabel.Anchor = dfAnchorStyle.CenterHorizontal | dfAnchorStyle.CenterVertical;

                    gunSelectPhase = Tribool.Ready;
                }

                // if our inventory changes while the weapon wheel is active, bail out FAST
                if (numGuns != playerGuns.Count)
                {
                    CleanUpWeaponWheel(targetPlayer, ammoController);
                    yield break;
                }

                // HANDLE INPUT
                // mouse input
                GungeonActions currentActions = inputInstance.ActiveActions;
                InputDevice currentDevice = currentActions.Device;
                bool gunUp = inputInstance.IsKeyboardAndMouse(true) && currentActions.GunUpAction.WasPressed;
                bool gunDown = inputInstance.IsKeyboardAndMouse(true) && currentActions.GunDownAction.WasPressed;
                int targetIndex = hoveredIndex;

                mousePosition = GetCenteredMousePosition(); //TODO: make this work for co-op

                if ((lastMousePosition - mousePosition).sqrMagnitude >= 16f)
                {
                    float segmentWidth = GUIManager.UIScale * 3f * _SEGMENT_SIZE / 2f;
                    if (mousePosition.magnitude > segmentWidth * 0.25f)
                    {
                        //NOTE: uses -x in Atan2 since shader is flipped for some reason
                        float mouseAngle = BraveMathCollege.ClampAngle360(Mathf.Atan2(mousePosition.y, -mousePosition.x) * Mathf.Rad2Deg);
                        targetIndex = Mathf.FloorToInt(0.5f + (segments.Length * mouseAngle) / 360f);
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
                    {
                        ignoreStickTimer = 0.25f;
                        targetIndex += (dpadDown ? 1 : -1);
                    }
                    else if (ignoreStickTimer <= 0f && Vector2.Distance(Vector2.zero, currentDevice.LeftStick.Value) >= 0.4f)
                    {
                        float anglePerSegment = 360f / segments.Length;
                        targetIndex = Mathf.FloorToInt((-currentDevice.LeftStick.Angle + 270) / anglePerSegment + 0.5f);
                    }
                }

                // apply hover
                targetIndex = (targetIndex + segments.Length) % segments.Length;
                if (hoveredIndex != targetIndex)
                {
                    segments[hoveredIndex].SetHovered(false);
                    segments[targetIndex].SetHovered(true);
                    hoveredIndex = targetIndex;
                    ammoLabel.Text = playerGuns[targetIndex].InfiniteAmmo
                        ? "[sprite \"infinite-big\"]"
                        : playerGuns[targetIndex].ammo + "/" + playerGuns[targetIndex].AdjustedMaxAmmo;
                }

                // run update
                float newGuiScale = GUIManager.PixelsToUnits();
                if (newGuiScale != cachedGuiScale)
                {
                    cachedGuiScale = newGuiScale;
                    float dfScale = GameUIUtility.GetCurrentTK2D_DFScale(GUIManager);
                    foreach (RadialSegment seg in segments)
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


            if (totalGunShift == 0 && totalTimeMetalGeared < 0.005f)
                targetPlayer.DoQuickEquip();

            CleanUpWeaponWheel(targetPlayer, ammoController);

            yield break;
        }
    }
}
