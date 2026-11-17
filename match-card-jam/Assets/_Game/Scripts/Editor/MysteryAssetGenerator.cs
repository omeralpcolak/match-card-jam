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
        if (!File.Exists(CARD_MYSTERY_MAT_PATH) || !File.Exists(DICE_MYSTERY_MAT_PATH))
        {
            GenerateMysteryMaterials();
        }
    }

    [MenuItem("Tools/Generate Mystery Materials")]
    public static void GenerateMysteryMaterials()
    {
        Material refMat = AssetDatabase.LoadAssetAtPath<Material>(REFERENCE_UNLIT_MAT_PATH);
        Shader fallbackShader = refMat != null ? refMat.shader : Shader.Find("Universal Render Pipeline/Unlit");
        if (fallbackShader == null)
        {
            fallbackShader = Shader.Find("Unlit/Texture");
        }

        bool cardOk = CreateOrUpdateMaterial(CARD_MYSTERY_MAT_PATH, CARD_MYSTERY_TEX_PATH, refMat, fallbackShader);
        bool diceOk = CreateOrUpdateMaterial(DICE_MYSTERY_MAT_PATH, DICE_MYSTERY_TEX_PATH, refMat, fallbackShader);

        if (cardOk || diceOk)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#2ecc71><b>[MysteryAssetGenerator]</b> Generated Card_mystery_mat and Dice_mystery_mat!</color>");
        }
    }

    private static bool CreateOrUpdateMaterial(string matPath, string texPath, Material refMat, Shader fallbackShader)
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
            mat = refMat != null ? new Material(refMat) : new Material(fallbackShader);
            mat.name = Path.GetFileNameWithoutExtension(matPath);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        mat.SetTexture("_BaseMap", tex);
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(mat);
        return true;
    }
}
#endif
