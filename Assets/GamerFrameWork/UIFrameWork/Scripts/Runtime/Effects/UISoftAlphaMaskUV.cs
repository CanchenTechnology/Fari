using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GamerFrameWork.UIFrameWork
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class UISoftAlphaMaskUV : BaseMeshEffect
    {
        private const string SoftAlphaMaskShaderName = "GamerFrameWork/UI/SoftAlphaMask";
        private static readonly List<Graphic> GraphicsBuffer = new List<Graphic>();

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureCanvasTexCoord1();
            graphic?.SetVerticesDirty();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            EnsureCanvasTexCoord1();
            graphic?.SetVerticesDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureCanvasTexCoord1();
            graphic?.SetVerticesDirty();
        }
#endif

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0)
            {
                return;
            }

            EnsureCanvasTexCoord1();

            UIVertex vertex = default(UIVertex);
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            int vertexCount = vh.currentVertCount;
            for (int i = 0; i < vertexCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                Vector3 position = vertex.position;
                minX = Mathf.Min(minX, position.x);
                minY = Mathf.Min(minY, position.y);
                maxX = Mathf.Max(maxX, position.x);
                maxY = Mathf.Max(maxY, position.y);
            }

            float width = Mathf.Max(maxX - minX, 0.0001f);
            float height = Mathf.Max(maxY - minY, 0.0001f);

            for (int i = 0; i < vertexCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                Vector3 position = vertex.position;
                vertex.uv1 = new Vector2(
                    Mathf.Clamp01((position.x - minX) / width),
                    Mathf.Clamp01((position.y - minY) / height));
                vh.SetUIVertex(vertex, i);
            }
        }

        public static void EnsureUnder(Transform root)
        {
            if (root == null)
            {
                return;
            }

            root.GetComponentsInChildren(true, GraphicsBuffer);
            for (int i = 0; i < GraphicsBuffer.Count; i++)
            {
                Graphic targetGraphic = GraphicsBuffer[i];
                if (!UsesSoftAlphaMask(targetGraphic))
                {
                    continue;
                }

                UISoftAlphaMaskUV maskUV = targetGraphic.GetComponent<UISoftAlphaMaskUV>();
                if (maskUV == null)
                {
                    maskUV = targetGraphic.gameObject.AddComponent<UISoftAlphaMaskUV>();
                }

                maskUV.EnsureCanvasTexCoord1();
                targetGraphic.SetVerticesDirty();
            }

            GraphicsBuffer.Clear();
        }

        private static bool UsesSoftAlphaMask(Graphic targetGraphic)
        {
            if (targetGraphic == null)
            {
                return false;
            }

            Material material = targetGraphic.material;
            return material != null &&
                   material.shader != null &&
                   material.shader.name == SoftAlphaMaskShaderName;
        }

        private void EnsureCanvasTexCoord1()
        {
            Canvas targetCanvas = graphic != null ? graphic.canvas : null;
            if (targetCanvas == null)
            {
                return;
            }

            if ((targetCanvas.additionalShaderChannels & AdditionalCanvasShaderChannels.TexCoord1) == 0)
            {
                targetCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            }
        }
    }
}
