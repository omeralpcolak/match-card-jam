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
    public const string ICE_SHADER_NAME = "Custom/URP_IceOverlay";

    static IceAssetGenerator()
    {
        EditorApplication.delayCall += EnsureIceMaterialCreated;
    }

    private static void EnsureIceMaterialCreated()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(ICE_MAT_PATH);
        if (existing == null || existing.shader == null || existing.shader.name != ICE_SHADER_NAME || existing.GetTexture("_BaseMap") == null)
        {
            GenerateIceMaterial();
        }
    }

    [MenuItem("Tools/Generate Ice Overlay Material")]
    public static void GenerateIceMaterial()
    {
        Shader shader = Shader.Find(ICE_SHADER_NAME);
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(ICE_MAT_PATH);
        if (mat == null)
        {
            mat = new Material(shader);
            mat.name = Path.GetFileNameWithoutExtension(ICE_MAT_PATH);
            AssetDatabase.CreateAsset(mat, ICE_MAT_PATH);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }

        Texture2D iceTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ICE_TEX_PATH);
        if (iceTex != null)
        {
            mat.SetTexture("_BaseMap", iceTex);
        }

        mat.SetColor("_IceColor", new Color(0.68f, 0.92f, 1f, 1f));
        mat.SetColor("_RimColor", new Color(0.94f, 0.99f, 1f, 1f));
        mat.SetFloat("_RimPower", 2.0f);
        mat.SetFloat("_RimIntensity", 1.2f);
        mat.SetFloat("_CenterAlpha", 0.22f);
        mat.SetFloat("_TextureAlpha", 0.62f);
        mat.SetFloat("_ShimmerSpeed", 0.85f);
        mat.SetFloat("_ShimmerIntensity", 0.5f);
        mat.SetFloat("_MeltProgress", 0f);
        mat.SetFloat("_MeltEdgeWidth", 0.075f);
        mat.SetColor("_MeltEdgeColor", new Color(0.95f, 1f, 1f, 1f));
        mat.SetFloat("_FlashAmount", 0f);
        mat.SetFloat("_AlphaMultiplier", 1f);
        mat.renderQueue = (int)RenderQueue.Transparent;

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=#74b9ff><b>[IceAssetGenerator]</b> Generated unlit M_IceOverlay.mat with Custom/URP_IceOverlay!</color>");
    }
}
#endif
