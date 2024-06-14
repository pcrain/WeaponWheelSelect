﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RadialGunSelect
{
    public class RadialSegment
    {
        private GameUIRoot UIRoot => GameUIRoot.Instance;
        private dfGUIManager GUIManager;
        private Transform container;
        private MeshRenderer renderer;
        private Material material;
        private Gun originalGun;
        private Transform gunContainer;
        private tk2dClippedSprite gunSprite;
        private tk2dSprite noAmmoIcon;
        private tk2dSprite[] gunOutlineSprites;
        private float resolution;
        private float rotation;

        static readonly Color unhoveredOutlineColor = new Color(96 / 255f, 96 / 255f, 101 / 255f);
        static readonly Color unhoveredFillColor = Color.black.WithAlpha(0.5f);
        static readonly Color hoveredOutlineColor = Color.white;
        static readonly Color hoveredFillColor = Color.gray.WithAlpha(0.5f);
        static readonly Color innerColor = Color.black.WithAlpha(0.5f);
        static readonly Color outerColor = new Color(96/255f,96/255f,101/255f);

        static int numSegmentsCreated = 0;
        public RadialSegment(float size, float angle, float rotation)
        {
            GUIManager = UIRoot.GetObject("m_manager") as dfGUIManager;

            container = new GameObject("SegmentContainer").transform;
            container.parent = GUIManager.transform;
            container.localPosition = Vector3.zero;
            container.SetAsFirstSibling();

            this.resolution = size;
            this.rotation = rotation;

            var segGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            segGO.transform.parent = container;
            segGO.transform.localScale = GUIManager.PixelsToUnits() * 3f * size * Vector2.one;
            segGO.transform.localPosition = Vector3.zero;
            renderer = segGO.GetComponent<MeshRenderer>();
            material = new Material(RadialGunSelectController.radialShader);
            renderer.material = material;
            renderer.sortingOrder = 1;
            material.SetFloat("_Resolution", size);
            material.SetFloat("_Angle", angle);
            material.SetFloat("_Rotation", rotation);
            material.SetColor("_Color", innerColor);
            material.SetColor("_OutlineColor", outerColor);
            material.SetFloat("_OutlineWidth", 1f);
            material.SetFloat("_LowBound", 0.5f);

            container.gameObject.SetLayerRecursively(LayerMask.NameToLayer("GUI"));
            ++numSegmentsCreated;
            ETGModConsole.Log($"created segment # {numSegmentsCreated}");
        }

        public void AssignGun(Gun gun)
        {
            originalGun = gun;
            var originalGunSprite = originalGun.GetSprite();

            gunContainer = new GameObject("GunContainer").transform;
            gunContainer.parent = container;

            GameObject gunGO = new GameObject("SomeGun");
            gunGO.transform.parent = gunContainer;

            gunSprite = tk2dBaseSprite.AddComponent<tk2dClippedSprite>(gunGO, originalGunSprite.Collection, originalGunSprite.spriteId);
            gunSprite.scale = GameUIUtility.GetCurrentTK2D_DFScale(GUIManager) * Vector3.one;
            gunSprite.ignoresTiltworldDepth = true;

            gunSprite.renderer.material.shader = ShaderCache.Acquire("tk2d/BlendVertexColorFadeRange");

            if (gun.CurrentAmmo == 0)
            {
                gunSprite.renderer.material.SetFloat("_Saturation", 0f);
                noAmmoIcon = ((GameObject)UnityEngine.Object.Instantiate(BraveResources.Load("Global Prefabs/NoAmmoIcon", ".prefab"))).GetComponent<tk2dSprite>();
                noAmmoIcon = tk2dBaseSprite.AddComponent<tk2dSprite>(noAmmoIcon.gameObject, noAmmoIcon.Collection, noAmmoIcon.spriteId);
                noAmmoIcon.name = "NoAmmoIcon";
                noAmmoIcon.transform.parent = gunContainer;
                noAmmoIcon.HeightOffGround = 2f;
                noAmmoIcon.transform.position = Vector3.zero;
                noAmmoIcon.scale = gunSprite.scale;
                noAmmoIcon.ignoresTiltworldDepth = true;
            }
            else
            {
                SpriteOutlineManager.AddOutlineToSprite<tk2dSprite>(gunSprite, Color.white);
                gunOutlineSprites = SpriteOutlineManager.GetOutlineSprites(gunSprite);
            }
            gunContainer.gameObject.SetLayerRecursively(LayerMask.NameToLayer("GUI"));

        }

        public void Update()
        {
            // rescale segment
            renderer.transform.localScale = GUIManager.PixelsToUnits() * 3f * this.resolution * Vector2.one;

            // move gun
            GameUIAmmoController ammoController = UIRoot.ammoControllers[0];
            gunSprite.scale = GameUIUtility.GetCurrentTK2D_DFScale(GUIManager) * Vector3.one;
            if (gunOutlineSprites != null)
                foreach (var outlineSprite in SpriteOutlineManager.GetOutlineSprites(gunSprite))
                    outlineSprite.scale = gunSprite.scale;
            var gunOffset = ammoController.GetOffsetVectorForGun(originalGun, false);
            var adjustedRot = (-this.rotation - 90) * Mathf.Deg2Rad;
            var segmentWidth = renderer.transform.localScale.x / 2f;
            var pos = new Vector3(Mathf.Sin(adjustedRot), Mathf.Cos(adjustedRot)) * 0.75f * segmentWidth;
            gunContainer.localPosition = pos;
            gunSprite.transform.localPosition = gunOffset;
        }

        public void Destroy()
        {
            GameObject.Destroy(container.gameObject);
        }

        public void SetHovered(bool hovered)
        {
            var oCol = hovered ? hoveredOutlineColor : unhoveredOutlineColor;
            material.SetColor("_OutlineColor", oCol);

            if (gunOutlineSprites != null)
                foreach(var outlineSprite in SpriteOutlineManager.GetOutlineSprites(gunSprite))
                    outlineSprite.renderer.material.SetColor("_OverrideColor", oCol);
        }
    }
}
