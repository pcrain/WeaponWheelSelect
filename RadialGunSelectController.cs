using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using InControl;

namespace RadialGunSelect
{
    public static class RadialGunSelectController
    {
        private const int _SEGMENT_SIZE = 201;
        internal static Shader radialShader;

        public static void Init()
        {
            string platform =
                Application.platform == RuntimePlatform.LinuxPlayer ? "linux" :
                Application.platform == RuntimePlatform.OSXPlayer ? "osx" : "windows";
            AssetBundle assetBundle = LoadAssetBundleFromResource($"RadialGunSelect.AssetBundles.wwshaders-{platform}");
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
                __result = RadialGunSelectController.HandleRadialGunSelect(targetPlayer, numToL);
                return false; // skip original method
            }
        }

        private static IEnumerator HandleRadialGunSelect(PlayerController targetPlayer, int numToL)
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
            Pixelator.Instance.FadeColor = Color.black;
            int totalGunShift = 0;
            float ignoreStickTimer = 0f;
            Vector2 mousePosition = GetCenteredMousePosition();
            Vector2 lastMousePosition = mousePosition;
            dfLabel ammoLabel = new GameObject("RadialGunSelectLabel").AddComponent<dfLabel>();
            float cachedGuiScale = 0.0f;
            RadialSegment[] segments = new RadialSegment[playerGuns.Count];
            int hoveredIndex = 0;

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
                    hoveredIndex = playerGuns.IndexOf(targetPlayer.CurrentGun);
                    int gap = playerGuns.Count > 20 ? 0 : playerGuns.Count > 15 ? 2 : playerGuns.Count > 10 ? 5 : 10;
                    float dfScale = GameUIUtility.GetCurrentTK2D_DFScale(GUIManager);
                    for (int i = 0; i < playerGuns.Count; i++)
                    {
                        // spawn segment
                        float angle = 360f / playerGuns.Count;
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
                    ammoLabel.Text = targetPlayer.CurrentGun.InfiniteAmmo ? "" : targetPlayer.CurrentGun.ammo + "/" + targetPlayer.CurrentGun.AdjustedMaxAmmo;
                    ammoLabel.Anchor = dfAnchorStyle.CenterHorizontal | dfAnchorStyle.CenterVertical;

                    gunSelectPhase = Tribool.Ready;
                }

                // HANDLE INPUT
                // mouse input
                GungeonActions currentActions = inputInstance.ActiveActions;
                InputDevice currentDevice = currentActions.Device;
                bool gunUp = inputInstance.IsKeyboardAndMouse(true) && currentActions.GunUpAction.WasPressed;
                bool gunDown = inputInstance.IsKeyboardAndMouse(true) && currentActions.GunDownAction.WasPressed;
                int targetIndex = hoveredIndex;

                mousePosition = GetCenteredMousePosition(); //TODO: make this work for co-op

                float mouseAngle = BraveMathCollege.ClampAngle360(Mathf.Atan2(mousePosition.x, mousePosition.y) * Mathf.Rad2Deg);

                float segmentWidth = GUIManager.UIScale * 3f * _SEGMENT_SIZE / 2f;
                if (Vector2.Distance(mousePosition, lastMousePosition) >= 4f)
                {
                    if (mousePosition.magnitude > segmentWidth * 0.25f)
                    {
                        float anglePerSegment = 360f / segments.Length;
                        targetIndex = Mathf.FloorToInt((mouseAngle + 90) / anglePerSegment + 0.5f);
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
                        targetIndex = Mathf.FloorToInt((-currentDevice.LeftStick.Angle - 90) / anglePerSegment + 0.5f);
                    }
                }

                // apply hover
                targetIndex = (targetIndex + segments.Length) % segments.Length;
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

            // return everything to normal
            foreach (RadialSegment seg in segments)
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
    }
}
