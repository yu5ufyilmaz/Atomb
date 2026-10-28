using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// World-space controls share a solid, lit backing and raised frame on the board.
public sealed class PhysicalBoardFixture : MonoBehaviour
{
    private Material material;

    public static void Attach(Transform parent, string label, Vector2 center, Vector2 size, Color color)
    {
        var root = new GameObject(label + " Gövdesi");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = center;
        var fixture = root.AddComponent<PhysicalBoardFixture>();
        fixture.material = new Material(Shader.Find("HDRP/Lit")) { name = label + " / Gövde Malzemesi" };
        fixture.material.SetColor("_BaseColor", color);
        fixture.material.SetFloat("_Metallic", .25f);
        fixture.material.SetFloat("_Smoothness", .25f);
        HDMaterial.ValidateMaterial(fixture.material);
        fixture.Box(label + " Arkalığı", new Vector3(0, 0, 8), new Vector3(size.x, size.y, 12));
        fixture.Box("Üst Çerçeve", new Vector3(0, size.y / 2 + 4, 2), new Vector3(size.x + 16, 8, 14));
        fixture.Box("Alt Çerçeve", new Vector3(0, -size.y / 2 - 4, 2), new Vector3(size.x + 16, 8, 14));
        fixture.Box("Sol Çerçeve", new Vector3(-size.x / 2 - 4, 0, 2), new Vector3(8, size.y, 14));
        fixture.Box("Sağ Çerçeve", new Vector3(size.x / 2 + 4, 0, 2), new Vector3(8, size.y, 14));
    }

    private void Box(string label, Vector3 position, Vector3 size)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = label; box.transform.SetParent(transform, false);
        box.transform.localPosition = position; box.transform.localScale = size;
        var collider = box.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        box.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private void OnDestroy() { if (material != null) Destroy(material); }
}
