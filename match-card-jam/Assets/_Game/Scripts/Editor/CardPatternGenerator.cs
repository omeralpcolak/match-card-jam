#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CardPatternGenerator
{
    public const string CardsTextureFolder = "Assets/_Game/Textures/Cards";

    [Serializable]
    public struct CardColorSpec
    {
        public string Name;
        public string TexturePath;
        public string MaterialPath;

        public CardColorSpec(string name, string texPath, string matPath)
        {
            Name = name;
            TexturePath = texPath;
            MaterialPath = matPath;
        }
    }

    public static readonly CardColorSpec[] DefaultSpecs = new[]
    {
        // Star Series
        new CardColorSpec("Red Star",    $"{CardsTextureFolder}/T_CardBack_Red.png",         "Assets/_Game/Materials/Card Star Set/Card_red_star_mat.mat"),
        new CardColorSpec("Blue Star",   $"{CardsTextureFolder}/T_CardBack_Blue.png",        "Assets/_Game/Materials/Card Star Set/Card_blue_star_mat.mat"),
        new CardColorSpec("Green Star",  $"{CardsTextureFolder}/T_CardBack_Green.png",       "Assets/_Game/Materials/Card Star Set/Card_green_star_mat.mat"),
        new CardColorSpec("Yellow Star", $"{CardsTextureFolder}/T_CardBack_Yellow.png",      "Assets/_Game/Materials/Card Star Set/Card_yellow_star_mat.mat"),
        new CardColorSpec("Purple Star", $"{CardsTextureFolder}/T_CardBack_Purple.png",      "Assets/_Game/Materials/Card Star Set/Card_purple_star_mat.mat"),
        new CardColorSpec("Orange Star", $"{CardsTextureFolder}/T_CardBack_Orange.png",      "Assets/_Game/Materials/Card Star Set/Card_orange_star_mat.mat"),
        new CardColorSpec("Cyan Star",   $"{CardsTextureFolder}/T_CardBack_Cyan.png",        "Assets/_Game/Materials/Card Star Set/Card_cyan_star_mat.mat"),
        new CardColorSpec("Black Star",  $"{CardsTextureFolder}/T_CardBack_Black.png",       "Assets/_Game/Materials/Card Star Set/Card_black_star_mat.mat"),
        new CardColorSpec("Pink Star",   $"{CardsTextureFolder}/T_CardBack_Pink.png",        "Assets/_Game/Materials/Card Star Set/Card_pink_star_mat.mat"),
        new CardColorSpec("Brown Star",  $"{CardsTextureFolder}/T_CardBack_Brown.png",       "Assets/_Game/Materials/Card Star Set/Card_brown_star_mat.mat"),
        new CardColorSpec("Gray Star",   $"{CardsTextureFolder}/T_CardBack_Gray.png",        "Assets/_Game/Materials/Card Star Set/Card_gray_star_mat.mat"),

        // Moon Series
        new CardColorSpec("Red Moon",    $"{CardsTextureFolder}/T_CardBack_Red_Moon.png",    "Assets/_Game/Materials/Card Moon Set/Card_red_moon_mat.mat"),
        new CardColorSpec("Blue Moon",   $"{CardsTextureFolder}/T_CardBack_Blue_Moon.png",   "Assets/_Game/Materials/Card Moon Set/Card_blue_moon_mat.mat"),
        new CardColorSpec("Green Moon",  $"{CardsTextureFolder}/T_CardBack_Green_Moon.png",  "Assets/_Game/Materials/Card Moon Set/Card_green_moon_mat.mat"),
        new CardColorSpec("Yellow Moon", $"{CardsTextureFolder}/T_CardBack_Yellow_Moon.png", "Assets/_Game/Materials/Card Moon Set/Card_yellow_moon_mat.mat"),
        new CardColorSpec("Purple Moon", $"{CardsTextureFolder}/T_CardBack_Purple_Moon.png", "Assets/_Game/Materials/Card Moon Set/Card_purple_moon_mat.mat"),
        new CardColorSpec("Orange Moon", $"{CardsTextureFolder}/T_CardBack_Orange_Moon.png", "Assets/_Game/Materials/Card Moon Set/Card_orange_moon_mat.mat"),
        new CardColorSpec("Cyan Moon",   $"{CardsTextureFolder}/T_CardBack_Cyan_Moon.png",   "Assets/_Game/Materials/Card Moon Set/Card_cyan_moon_mat.mat"),
        new CardColorSpec("Black Moon",  $"{CardsTextureFolder}/T_CardBack_Black_Moon.png",  "Assets/_Game/Materials/Card Moon Set/Card_black_moon_mat.mat"),
        new CardColorSpec("Pink Moon",   $"{CardsTextureFolder}/T_CardBack_Pink_Moon.png",   "Assets/_Game/Materials/Card Moon Set/Card_pink_moon_mat.mat"),
        new CardColorSpec("Brown Moon",  $"{CardsTextureFolder}/T_CardBack_Brown_Moon.png",  "Assets/_Game/Materials/Card Moon Set/Card_brown_moon_mat.mat"),
        new CardColorSpec("Gray Moon",   $"{CardsTextureFolder}/T_CardBack_Gray_Moon.png",   "Assets/_Game/Materials/Card Moon Set/Card_gray_moon_mat.mat")
    };

    static CardPatternGenerator()
    {
        EditorApplication.delayCall += BindTexturesToMaterials;
    }

    public static void BindTexturesToMaterials()
    {
        EnsureModelReadable("Assets/_Game/Models/SolitaireCard.fbx");
        EnsureModelReadable("Assets/_Game/Models/Dice_d6.fbx");

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        bool dirty = false;
        foreach (var spec in DefaultSpecs)
        {
            if (string.IsNullOrEmpty(spec.MaterialPath)) continue;

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.TexturePath);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath);
            if (tex == null || mat == null) continue;

            bool needsShader = unlitShader != null && mat.shader != unlitShader;
            bool needsTex = mat.GetTexture("_BaseMap") != tex || mat.GetColor("_BaseColor") != Color.white;

            if (needsShader || needsTex)
            {
                if (needsShader)
                {
                    mat.shader = unlitShader;
                }
                mat.SetTexture("_BaseMap", tex);
                mat.SetTexture("_MainTex", tex);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetColor("_Color", Color.white);
                EditorUtility.SetDirty(mat);
                dirty = true;
            }
        }

        if (dirty)
        {
            AssetDatabase.SaveAssets();
        }
    }

    private static void EnsureModelReadable(string modelPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }
}
#endif
