using System.Collections.Generic;
using UnityEngine;

namespace ElementalBreaker
{
    // 트레일을 정해진 개수만 미리 만들어 두고 공에 빌려줘. 공이 몇 개든 트레일 수는 늘지 않아.
    public sealed class BallTrailPool
    {
        private readonly Stack<TrailRenderer> idle;
        private readonly Transform root;
        private readonly Material generatedMaterial;

        public BallTrailPool(Transform parent, int count, float time, float width, int sortingOrder, Material material)
        {
            idle = new Stack<TrailRenderer>(count);
            root = new GameObject("Ball Trails").transform;
            root.SetParent(parent, false);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                if (shader != null)
                    material = generatedMaterial = new Material(shader);
            }

            // 공 바로 뒤는 굵고 빠르게 가늘어지는 혜성 꼬리 모양이야. 모든 트레일이 같이 써.
            AnimationCurve taper = new AnimationCurve(new Keyframe(0f, 1f, -2.6f, -2.6f), new Keyframe(1f, 0f, 0f, 0f));
            for (int i = 0; i < count; i++)
            {
                TrailRenderer trail = new GameObject("Trail").AddComponent<TrailRenderer>();
                trail.transform.SetParent(root, false);
                trail.sharedMaterial = material;
                trail.time = time;
                trail.widthMultiplier = width;
                trail.widthCurve = taper;
                trail.minVertexDistance = 0.05f;
                trail.numCapVertices = 0;
                trail.numCornerVertices = 0;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.sortingOrder = sortingOrder;
                trail.emitting = false;
                trail.gameObject.SetActive(false);
                idle.Push(trail);
            }
        }

        // 남은 트레일이 없으면 null을 돌려줘.
        public TrailRenderer Rent() => idle.Count > 0 ? idle.Pop() : null;

        public void Return(TrailRenderer trail)
        {
            trail.emitting = false;
            trail.Clear();
            trail.transform.SetParent(root, false);
            trail.gameObject.SetActive(false);
            idle.Push(trail);
        }

        public void Dispose()
        {
            if (generatedMaterial != null)
                Object.Destroy(generatedMaterial);
        }
    }
}
