#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BilliardFeltGenerator
{
    private const string TexturePath = "Assets/_Game/Textures/T_BilliardFelt.png";
    private const string MaterialPath = "Assets/_Game/Materials/M_BilliardTable.mat";

    static BilliardFeltGenerator()
    {
        EditorApplication.delayCall += CheckAndGenerate;
    }

    private static void CheckAndGenerate()
    {
        if (!File.Exists(TexturePath))
        {
            GenerateTexture();
        }
    }

    [MenuItem("Tools/Generate Billiard Felt Texture & Material")]
    public static void GenerateTexture()
    {
        int width = 1024;
        int height = 1024;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);

        // Classic billiard table emerald/forest green shades
        Color darkGreen = new Color(0.06f, 0.32f, 0.18f);
        Color baseGreen = new Color(0.08f, 0.40f, 0.23f);
        Color lightGreen = new Color(0.12f, 0.46f, 0.27f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Multi-layered Perlin noise for felt fabric texture
                float n1 = Mathf.PerlinNoise(x * 0.04f, y * 0.04f);
                float n2 = Mathf.PerlinNoise(x * 0.18f, y * 0.18f) * 0.5f;
                float n3 = Mathf.PerlinNoise(x * 0.70f, y * 0.70f) * 0.25f;

                // Subtle cloth weave micro-pattern
                float weave = (Mathf.Sin(x * 1.8f) * Mathf.Cos(y * 1.8f)) * 0.025f;

                float blend = Mathf.Clamp01((n1 + n2 + n3) / 1.75f + weave);
                Color pixel = Color.Lerp(darkGreen, lightGreen, blend);

                // Micro-grain for wool fibers
                float grain = (Random.value - 0.5f) * 0.035f;
                pixel.r = Mathf.Clamp01(pixel.r + grain);
                pixel.g = Mathf.Clamp01(pixel.g + grain);
                pixel.b = Mathf.Clamp01(pixel.b + grain);
                pixel.a = 1f;

                tex.SetPixel(x, y, pixel);
            }
        }

        tex.Apply();

        string dir = Path.GetDirectoryName(TexturePath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        byte[] pngData = tex.EncodeToPNG();
        File.WriteAllBytes(TexturePath, pngData);
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);

        // Configure texture importer settings for felt fabric
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        // Create or update material
        CreateOrUpdateMaterial();

        Debug.Log("<color=#2ecc71><b>[BilliardFelt]</b> Billiard felt texture and material created successfully!</color>");
    }

    private static void CreateOrUpdateMaterial()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null) return;

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            mat = new Material(litShader);
            mat.SetTexture("_BaseMap", texture);
            mat.SetFloat("_Smoothness", 0.08f); // Felt fabric is matte (no glossy reflections)
            mat.SetColor("_BaseColor", Color.white);

            // Tiling across the plane surface
            mat.SetTextureScale("_BaseMap", new Vector2(4f, 4f));

            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        else
        {
            mat.SetTexture("_BaseMap", texture);
            mat.SetFloat("_Smoothness", 0.08f);
            mat.SetTextureScale("_BaseMap", new Vector2(4f, 4f));
            EditorUtility.SetDirty(mat);
        }

        AssetDatabase.SaveAssets();
    }
}
#endif
