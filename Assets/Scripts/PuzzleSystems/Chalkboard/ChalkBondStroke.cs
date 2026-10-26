using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// A single flat mesh: dry gaps and grains are geometry, with no camera-facing ribbon.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class ChalkBondStroke : MonoBehaviour
{
    private Mesh mesh;
    private Vector3 lastStart, lastEnd;
    private float lastOffset = float.NaN;
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<int> triangles = new List<int>();

    public void Draw(Transform board, Material material, Vector3 start, Vector3 end, float offset, float seed)
    {
        start = board.InverseTransformPoint(start); end = board.InverseTransformPoint(end);
        start.z = end.z = .018f;
        gameObject.SetActive(true);
        if (mesh != null && start == lastStart && end == lastEnd && offset == lastOffset) return;
        lastStart = start; lastEnd = end; lastOffset = offset;
        if (mesh == null)
        {
            mesh = new Mesh { name = "Kuru tebeşir bağı" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        vertices.Clear(); triangles.Clear();
        Vector3 direction = end - start;
        Vector3 side = Vector3.Cross(Vector3.forward, direction).normalized;
        start += side * offset; end += side * offset;
        int count = Mathf.Clamp(Mathf.CeilToInt(direction.magnitude / .009f), 4, 512);
        const float width = .0065f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count, next = (i + .88f) / count;
            float wobble = (Mathf.PerlinNoise(seed, t * 17) - .5f) * .004f;
            float pressure = Mathf.Lerp(.65f, 1.2f, Grain(i, 7, seed));
            for (int lane = -1; lane <= 1; lane++)
            {
                if (Grain(i, lane + 2, seed) < (lane == 0 ? .09f : .28f)) continue;
                Vector3 grain = side * (wobble + lane * width / 3);
                Vector3 a = Vector3.Lerp(start, end, t) + grain;
                Vector3 b = Vector3.Lerp(start, end, Mathf.Min(1, next)) + grain;
                Vector3 half = side * (width / 6 * pressure);
                int v = vertices.Count;
                vertices.Add(a - half); vertices.Add(a + half); vertices.Add(b + half); vertices.Add(b - half);
                triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
                triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 3);
            }
        }
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
    }

    private static float Grain(int step, int lane, float seed) => Mathf.Repeat(Mathf.Sin(step * 127.1f + lane * 311.7f + seed) * 43758.5453f, 1);

    private void OnDestroy()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
    }
}
