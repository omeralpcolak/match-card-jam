#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Creates and configures the translucent URP Ice overlay material (M_IceOverlay.mat)
/// used by Frozen MatchItems.
/// </summary>
[InitializeOnLoad]
public static class IceAssetGenerator
{
    public const string ICE_TEX_PATH = "Assets/_Game/Textures/T_IceOverlay.png";
    public const string ICE_MAT_PATH = "Assets/_Game/Materials/M_IceOverlay.mat";

    static IceAssetGenerator()
    {
        EditorApplication.delayCall += EnsureIceMaterialCreated;
    }

    private static void EnsureIceMaterialCreated()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(ICE_MAT_PATH);
        if (existing == null || existing.GetTexture("_BaseMap") == null)
        {
            GenerateIceMaterial();
        }
    }

    [MenuItem("Tools/Generate Ice Overlay Material")]
    public static void GenerateIceMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(ICE_MAT_PATH);
        if (mat == null)
        {
            mat = new Material(shader);
            mat.name = Path.GetFileNameWithoutExtension(ICE_MAT_PATH);
            AssetDatabase.CreateAsset(mat, ICE_MAT_PATH);
        }

        // Configure as Transparent Surface in URP Lit
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;

        Texture2D iceTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ICE_TEX_PATH);
        if (iceTex != null)
        {
            mat.SetTexture("_BaseMap", iceTex);
            mat.SetTexture("_MainTex", iceTex);
        }

        Color iceColor = iceTex != null ? Color.white : new Color(0.62f, 0.88f, 1f, 0.55f);
        mat.SetColor("_BaseColor", iceColor);
        mat.SetColor("_Color", iceColor);
        mat.SetFloat("_Smoothness", 0.88f);
        mat.SetFloat("_Metallic", 0.05f);

        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.16f, 0.36f, 0.52f));

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=#74b9ff><b>[IceAssetGenerator]</b> Generated M_IceOverlay.mat!</color>");
    }
}
#endif
