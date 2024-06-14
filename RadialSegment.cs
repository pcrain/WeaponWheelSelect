using UnityEngine;

namespace RadialGunSelect
{
    internal class RadialSegment
    {
        private static readonly Color unhoveredOutlineColor = new Color(96 / 255f, 96 / 255f, 101 / 255f);
        private static readonly Color hoveredOutlineColor = Color.white;
        private static readonly Color innerColor = Color.black.WithAlpha(0.5f);

        private Transform container;
        private MeshRenderer renderer;
        private Transform gunContainer;
        private tk2dClippedSprite gunSprite;
        private float resolution;
        private Vector3 basePos;

        internal RadialSegment(float size, float angle, float rotation)
        {
            dfGUIManager GUIManager = GameUIRoot.Instance.m_manager;

            container = new GameObject("SegmentContainer").transform;
            container.parent = GUIManager.transform;
            container.localPosition = Vector3.zero;
            container.SetAsFirstSibling();

            float adjustedRot = (-rotation - 90) * Mathf.Deg2Rad;
            this.basePos = new Vector3(0.375f * Mathf.Sin(adjustedRot), 0.375f * Mathf.Cos(adjustedRot));
            this.resolution = size;

            GameObject segGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            segGO.transform.parent = container;
            segGO.transform.localScale = GUIManager.PixelsToUnits() * 3f * size * Vector2.one;
            segGO.transform.localPosition = Vector3.zero;

            Material material = new Material(RadialGunSelectController.radialShader);
            material.SetFloat("_Resolution", size);
            material.SetFloat("_Angle", angle);
            material.SetFloat("_Rotation", rotation);
            material.SetColor("_Color", innerColor);
            material.SetColor("_OutlineColor", unhoveredOutlineColor);
            material.SetFloat("_OutlineWidth", 1f);
            material.SetFloat("_LowBound", 0.25f);
            material.SetFloat("_HighBound", 0.5f);

            renderer = segGO.GetComponent<MeshRenderer>();
            renderer.material = material;
            renderer.sortingOrder = 1;

            container.gameObject.SetLayerRecursively(LayerMask.NameToLayer("GUI"));
        }

        internal void AssignGun(Gun gun, float dfScale)
        {
            tk2dBaseSprite originalGunSprite = gun.GetSprite();

            gunContainer = new GameObject("GunContainer").transform;
            gunContainer.parent = container;

            GameObject gunGO = new GameObject("SomeGun");
            gunGO.transform.parent = gunContainer;

            gunSprite = tk2dBaseSprite.AddComponent<tk2dClippedSprite>(gunGO, originalGunSprite.Collection, originalGunSprite.spriteId);
            gunSprite.scale = dfScale * Vector3.one;
            gunSprite.ignoresTiltworldDepth = true;

            gunSprite.renderer.material.shader = ShaderCache.Acquire("tk2d/BlendVertexColorFadeRange");
            gunSprite.transform.localPosition = GameUIRoot.Instance.ammoControllers[0].GetOffsetVectorForGun(gun, false);

            if (gun.CurrentAmmo == 0)
            {
                gunSprite.renderer.material.SetFloat("_Saturation", 0f);
                tk2dSprite noAmmoIcon = ((GameObject)UnityEngine.Object.Instantiate(BraveResources.Load("Global Prefabs/NoAmmoIcon", ".prefab"))).GetComponent<tk2dSprite>();
                noAmmoIcon.transform.parent = gunContainer;
                noAmmoIcon.HeightOffGround = 2f;
                noAmmoIcon.transform.position = Vector3.zero;
                noAmmoIcon.scale = gunSprite.scale;
                noAmmoIcon.ignoresTiltworldDepth = true;
            }
            gunContainer.gameObject.SetLayerRecursively(LayerMask.NameToLayer("GUI"));
        }

        internal void Rescale(float guiScale, float dfScale)
        {
            Vector2 newScale = guiScale * 3f * this.resolution * Vector2.one;
            renderer.transform.localScale = newScale;
            gunSprite.scale = dfScale * Vector3.one;
            gunContainer.localPosition = newScale.x * this.basePos; // move gun
        }

        internal void Destroy()
        {
            GameObject.Destroy(container.gameObject);
        }

        internal void SetHovered(bool hovered)
        {
            renderer.material.SetColor("_OutlineColor", hovered ? hoveredOutlineColor : unhoveredOutlineColor);
        }
    }
}
