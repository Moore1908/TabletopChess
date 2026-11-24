using UnityEngine;

namespace VRChess
{
    /// <summary>
    /// Builds grey-box piece shapes from Unity primitives. Every piece shares a round base
    /// and gets a distinct silhouette on top. The piece's pivot is at its base (y = 0).
    /// Swap this out later for real models or Kenney-icon tokens.
    /// </summary>
    public static class PieceFactory
    {
        public static GameObject Part(Transform parent, PrimitiveType shape, Vector3 localPos,
                                      Vector3 localScale, Material mat, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = shape.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.transform.localEulerAngles = euler;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // Unity cylinder/capsule primitives are 2 units tall, so Y scale = height / 2.
        static void Cylinder(Transform p, float y, float diameter, float height, Material m) =>
            Part(p, PrimitiveType.Cylinder, new Vector3(0, y, 0), new Vector3(diameter, height / 2f, diameter), m);

        static void Sphere(Transform p, float y, float diameter, Material m) =>
            Part(p, PrimitiveType.Sphere, new Vector3(0, y, 0), Vector3.one * diameter, m);

        public static void Build(Transform visual, PieceType type, float s, Material m)
        {
            // Shared base: 0.12s tall
            Cylinder(visual, 0.06f * s, 0.75f * s, 0.12f * s, m);

            switch (type)
            {
                case PieceType.Pawn:
                    Cylinder(visual, 0.295f * s, 0.35f * s, 0.35f * s, m);
                    Sphere(visual, 0.6f * s, 0.38f * s, m);
                    break;

                case PieceType.Rook:
                    Part(visual, PrimitiveType.Cube, new Vector3(0, 0.42f * s, 0), new Vector3(0.5f, 0.6f, 0.5f) * s, m);
                    Part(visual, PrimitiveType.Cube, new Vector3(0, 0.78f * s, 0), new Vector3(0.6f, 0.12f, 0.6f) * s, m);
                    break;

                case PieceType.Knight:
                    Part(visual, PrimitiveType.Cube, new Vector3(0, 0.395f * s, 0), new Vector3(0.4f, 0.55f, 0.4f) * s, m);
                    // "Head" leans forward, towards the opponent
                    Part(visual, PrimitiveType.Cube, new Vector3(0, 0.68f * s, 0.12f * s),
                         new Vector3(0.3f, 0.25f, 0.55f) * s, m, new Vector3(20f, 0, 0));
                    break;

                case PieceType.Bishop:
                    Part(visual, PrimitiveType.Capsule, new Vector3(0, 0.47f * s, 0), new Vector3(0.4f, 0.35f, 0.4f) * s, m);
                    Sphere(visual, 0.9f * s, 0.15f * s, m);
                    break;

                case PieceType.Queen:
                    Cylinder(visual, 0.5f * s, 0.45f * s, 0.76f * s, m);
                    Sphere(visual, 0.95f * s, 0.5f * s, m);
                    Sphere(visual, 1.22f * s, 0.15f * s, m);
                    break;

                case PieceType.King:
                    Cylinder(visual, 0.54f * s, 0.5f * s, 0.84f * s, m);
                    Part(visual, PrimitiveType.Cube, new Vector3(0, 1.13f * s, 0), new Vector3(0.1f, 0.35f, 0.1f) * s, m);
                    Part(visual, PrimitiveType.Cube, new Vector3(0, 1.17f * s, 0), new Vector3(0.28f, 0.1f, 0.1f) * s, m);
                    break;
            }
        }

        /// <summary>Marks a promoted pawn. No collider, so the grab setup is unaffected.</summary>
        public static void AddCrown(Transform visual, float s, Material m)
        {
            var crown = Part(visual, PrimitiveType.Sphere, new Vector3(0, 0.88f * s, 0), Vector3.one * 0.25f * s, m);
            Object.Destroy(crown.GetComponent<Collider>());
        }
    }
}
