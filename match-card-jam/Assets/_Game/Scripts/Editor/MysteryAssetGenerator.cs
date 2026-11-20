#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates and binds the Unlit Mystery materials (Card_mystery_mat and Dice_mystery_mat)
/// to T_CardBack_Mystery.png and T_Dice_Mystery.png.
/// </summary>
[InitializeOnLoad]
public static class MysteryAssetGenerator
{
    public const string CARD_MYSTERY_TEX_PATH = "Assets/_Game/Textures/Cards/T_CardBack_Mystery.png";
    public const string DICE_MYSTERY_TEX_PATH = "Assets/_Game/Textures/Dice/T_Dice_Mystery.png";
    public const string CARD_MYSTERY_MAT_PATH = "Assets/_Game/Materials/Card_mystery_mat.mat";
    public const string DICE_MYSTERY_MAT_PATH = "Assets/_Game/Materials/Dice_mystery_mat.mat";

    private const string REFERENCE_UNLIT_MAT_PATH = "Assets/_Game/Materials/Card Star Set/Card_blue_star_mat.mat";

    static MysteryAssetGenerator()
    {
        EditorApplication.delayCall += EnsureMysteryMaterialsCreated;
    }

    private static void EnsureMysteryMaterialsCreated()
    {
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        Material cardMat = AssetDatabase.LoadAssetAtPath<Material>(CARD_MYSTERY_MAT_PATH);
        Material diceMat = AssetDatabase.LoadAssetAtPath<Material>(DICE_MYSTERY_MAT_PATH);

        if (cardMat == null || diceMat == null ||
            (unlitShader != null && (cardMat.shader != unlitShader || diceMat.shader != unlitShader)))
        {
            GenerateMysteryMaterials();
        }
    }

    [MenuItem("Tools/Generate Mystery Materials")]
    public static void GenerateMysteryMaterials()
    {
        Material refMat = AssetDatabase.LoadAssetAtPath<Material>(REFERENCE_UNLIT_MAT_PATH);
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null)
        {
            unlitShader = refMat != null ? refMat.shader : Shader.Find("Unlit/Texture");
        }

        bool cardOk = CreateOrUpdateMaterial(CARD_MYSTERY_MAT_PATH, CARD_MYSTERY_TEX_PATH, refMat, unlitShader);
        bool diceOk = CreateOrUpdateMaterial(DICE_MYSTERY_MAT_PATH, DICE_MYSTERY_TEX_PATH, refMat, unlitShader);

        if (cardOk || diceOk)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#2ecc71><b>[MysteryAssetGenerator]</b> Generated Card_mystery_mat and Dice_mystery_mat!</color>");
        }
    }

    private static bool CreateOrUpdateMaterial(string matPath, string texPath, Material refMat, Shader targetShader)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null)
        {
            Debug.LogWarning($"[MysteryAssetGenerator] Missing texture at {texPath}");
            return false;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = targetShader != null ? new Material(targetShader) : new Material(refMat);
            mat.name = Path.GetFileNameWithoutExtension(matPath);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else if (targetShader != null && mat.shader != targetShader)
        {
            mat.shader = targetShader;
        }

        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(mat);
        return true;
    }
}
#endif
