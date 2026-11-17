#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Automatically creates and binds the 22 Unlit Dice materials, Dice_Base.prefab,
/// and 22 Dice MatchItem prefab variants (Dice Star Set: IDs 301-311, Dice Moon Set: IDs 401-411).
/// </summary>
[InitializeOnLoad]
public static class DiceAssetGenerator
{
    private const string DICE_MODEL_PATH = "Assets/_Game/Models/Dice_d6.fbx";
    private const string DICE_TEXTURES_DIR = "Assets/_Game/Textures/Dice";
    private const string DICE_STAR_MAT_DIR = "Assets/_Game/Materials/Dice Star Set";
    private const string DICE_MOON_MAT_DIR = "Assets/_Game/Materials/Dice Moon Set";
    private const string DICE_BASE_PREFAB_PATH = "Assets/_Game/Prefabs/MatchItems/Dice_Base.prefab";
    private const string DICE_STAR_PREFAB_DIR = "Assets/_Game/Prefabs/MatchItems/Dice Star Set";
    private const string DICE_MOON_PREFAB_DIR = "Assets/_Game/Prefabs/MatchItems/Dice Moon Set";

    private const string REFERENCE_UNLIT_MAT_PATH = "Assets/_Game/Materials/Card Star Set/Card_blue_star_mat.mat";
    private const string REFERENCE_CARD_BASE_PATH = "Assets/_Game/Prefabs/MatchItems/Card_Base.prefab";
    private const string CARD_OUTLINE_MAT_PATH = "Assets/_Game/Materials/M_CardOutline.mat";
    private const string DICE_OUTLINE_MAT_PATH = "Assets/_Game/Materials/M_DiceOutline.mat";

    private const float TARGET_DICE_WORLD_SIZE = 0.9f;

    private static readonly string[] Colors =
    {
        "Red", "Blue", "Green", "Yellow", "Purple",
        "Orange", "Cyan", "Black", "Pink", "Brown", "Gray"
    };

    static DiceAssetGenerator()
    {
        EditorApplication.delayCall += EnsureDiceAssetsCreated;
    }

    [MenuItem("Tools/Generate Dice Materials & Prefabs")]
    public static void GenerateAllDiceAssets()
    {
        EnsureDirectories();
        GenerateMaterials();
        GameObject basePrefab = CreateOrUpdateDiceBasePrefab();
        if (basePrefab != null)
        {
            GenerateDiceVariants(basePrefab);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=#2ecc71><b>[DiceAssetGenerator]</b> Generated 22 Unlit Dice materials, Dice_Base.prefab, and 22 Dice MatchItem prefabs!</color>");
    }

    private static void EnsureDiceAssetsCreated()
    {
        if ((!File.Exists(DICE_BASE_PREFAB_PATH) || !File.Exists(DICE_OUTLINE_MAT_PATH)) && File.Exists(DICE_MODEL_PATH))
        {
            GenerateAllDiceAssets();
        }
    }

    private static void EnsureDirectories()
    {
        string[] dirs =
        {
            DICE_STAR_MAT_DIR,
            DICE_MOON_MAT_DIR,
            DICE_STAR_PREFAB_DIR,
            DICE_MOON_PREFAB_DIR
        };

        for (int i = 0; i < dirs.Length; i++)
        {
            if (!Directory.Exists(dirs[i]))
            {
                Directory.CreateDirectory(dirs[i]);
            }
        }
        AssetDatabase.Refresh();
    }

    private static void GenerateMaterials()
    {
        Material refMat = AssetDatabase.LoadAssetAtPath<Material>(REFERENCE_UNLIT_MAT_PATH);
        Shader unlitShader = refMat != null ? refMat.shader : Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null)
        {
            unlitShader = Shader.Find("Unlit/Texture");
        }

        for (int i = 0; i < Colors.Length; i++)
        {
            string color = Colors[i];
            string lowerColor = color.ToLowerInvariant();

            string starTexPath = $"{DICE_TEXTURES_DIR}/T_Dice_{color}_Star.png";
            string starMatPath = $"{DICE_STAR_MAT_DIR}/Dice_{lowerColor}_star_mat.mat";
            CreateOrUpdateMaterial(starMatPath, starTexPath, refMat, unlitShader);

            string moonTexPath = $"{DICE_TEXTURES_DIR}/T_Dice_{color}_Moon.png";
            string moonMatPath = $"{DICE_MOON_MAT_DIR}/Dice_{lowerColor}_moon_mat.mat";
            CreateOrUpdateMaterial(moonMatPath, moonTexPath, refMat, unlitShader);
        }
    }

    private static void CreateOrUpdateMaterial(string matPath, string texPath, Material refMat, Shader fallbackShader)
    {
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null)
        {
            Debug.LogWarning($"[DiceAssetGenerator] Missing texture at {texPath}");
            return;
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
    }

    private static Material CreateOrUpdateDiceOutlineMaterial()
    {
        Material diceOutlineMat = AssetDatabase.LoadAssetAtPath<Material>(DICE_OUTLINE_MAT_PATH);
        if (diceOutlineMat == null)
        {
            Material cardOutlineMat = AssetDatabase.LoadAssetAtPath<Material>(CARD_OUTLINE_MAT_PATH);
            Shader outlineShader = cardOutlineMat != null ? cardOutlineMat.shader : Shader.Find("Custom/URP_Outline");
            diceOutlineMat = cardOutlineMat != null ? new Material(cardOutlineMat) : new Material(outlineShader);
            diceOutlineMat.name = "M_DiceOutline";
            AssetDatabase.CreateAsset(diceOutlineMat, DICE_OUTLINE_MAT_PATH);
        }

        diceOutlineMat.SetFloat("_UseRadialExtrusion", 1f);
        diceOutlineMat.SetFloat("_OutlineWidth", 0.0042f);
        diceOutlineMat.SetColor("_OutlineColor", Color.black);
        EditorUtility.SetDirty(diceOutlineMat);
        return diceOutlineMat;
    }

    private static GameObject CreateOrUpdateDiceBasePrefab()
    {
        Material diceOutlineMat = CreateOrUpdateDiceOutlineMaterial();

        // If Dice_Base.prefab already exists, preserve user's custom scale/transform/collider tweaks and only update the Outline material!
        GameObject existingBase = AssetDatabase.LoadAssetAtPath<GameObject>(DICE_BASE_PREFAB_PATH);
        if (existingBase != null)
        {
            if (diceOutlineMat != null)
            {
                MatchItemOutline existingOutline = existingBase.GetComponent<MatchItemOutline>();
                if (existingOutline != null && existingOutline.OutlineRenderer != null)
                {
                    existingOutline.OutlineRenderer.sharedMaterial = diceOutlineMat;
                    EditorUtility.SetDirty(existingBase);
                    PrefabUtility.SavePrefabAsset(existingBase);
                }
            }
            return existingBase;
        }

        Mesh diceMesh = LoadDiceMesh();
        if (diceMesh == null)
        {
            Debug.LogError($"[DiceAssetGenerator] Could not load mesh from {DICE_MODEL_PATH}");
            return null;
        }

        // Extract physics material from Card_Base.prefab
        Material outlineMat = diceOutlineMat;
        PhysicsMaterial physMat = null;
        GameObject cardBase = AssetDatabase.LoadAssetAtPath<GameObject>(REFERENCE_CARD_BASE_PATH);
        if (cardBase != null)
        {
            BoxCollider cardCol = cardBase.GetComponentInChildren<BoxCollider>(true);
            if (cardCol != null)
            {
                physMat = cardCol.sharedMaterial;
            }
        }

        string defaultMatPath = $"{DICE_STAR_MAT_DIR}/Dice_red_star_mat.mat";
        Material defaultDiceMat = AssetDatabase.LoadAssetAtPath<Material>(defaultMatPath);

        Bounds meshBounds = diceMesh.bounds;
        float maxExtent = Mathf.Max(0.0001f, Mathf.Max(meshBounds.size.x, Mathf.Max(meshBounds.size.y, meshBounds.size.z)));
        float uniformMeshScale = TARGET_DICE_WORLD_SIZE / maxExtent;

        GameObject root = new GameObject("Dice_Base");

        // Child 1: Mesh container with BoxCollider
        GameObject meshContainer = new GameObject("Mesh");
        meshContainer.transform.SetParent(root.transform, false);

        BoxCollider boxCol = meshContainer.AddComponent<BoxCollider>();
        boxCol.size = Vector3.one * TARGET_DICE_WORLD_SIZE;
        boxCol.center = Vector3.zero;
        if (physMat != null)
        {
            boxCol.sharedMaterial = physMat;
        }

        // Child 2: DiceMesh (visual mesh)
        GameObject diceMeshGo = new GameObject("DiceMesh");
        diceMeshGo.transform.SetParent(meshContainer.transform, false);
        diceMeshGo.transform.localPosition = -meshBounds.center * uniformMeshScale;
        diceMeshGo.transform.localRotation = Quaternion.identity;
        diceMeshGo.transform.localScale = Vector3.one * uniformMeshScale;

        MeshFilter mf = diceMeshGo.AddComponent<MeshFilter>();
        mf.sharedMesh = diceMesh;

        MeshRenderer mr = diceMeshGo.AddComponent<MeshRenderer>();
        if (defaultDiceMat != null)
        {
            mr.sharedMaterial = defaultDiceMat;
        }

        // Child 3: Outline mesh under DiceMesh
        GameObject outlineGo = new GameObject("Outline");
        outlineGo.transform.SetParent(diceMeshGo.transform, false);
        outlineGo.transform.localPosition = Vector3.zero;
        outlineGo.transform.localRotation = Quaternion.identity;
        outlineGo.transform.localScale = Vector3.one;

        MeshFilter outlineMf = outlineGo.AddComponent<MeshFilter>();
        outlineMf.sharedMesh = diceMesh;

        MeshRenderer outlineMr = outlineGo.AddComponent<MeshRenderer>();
        if (outlineMat != null)
        {
            outlineMr.sharedMaterial = outlineMat;
        }

        // Root components: Rigidbody, MatchItemOutline, MatchItem
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 10f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.None;

        MatchItemOutline itemOutline = root.AddComponent<MatchItemOutline>();
        SerializedObject outlineSo = new SerializedObject(itemOutline);
        outlineSo.FindProperty("mainMeshRenderer").objectReferenceValue = mr;
        outlineSo.FindProperty("outlineRenderer").objectReferenceValue = outlineMr;
        outlineSo.ApplyModifiedPropertiesWithoutUndo();

        MatchItem matchItem = root.AddComponent<MatchItem>();
        SerializedObject itemSo = new SerializedObject(matchItem);
        itemSo.FindProperty("id").intValue = 300;
        itemSo.FindProperty("outline").objectReferenceValue = itemOutline;
        itemSo.FindProperty("rb").objectReferenceValue = rb;
        itemSo.FindProperty("itemCollider").objectReferenceValue = boxCol;
        itemSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject savedBase = PrefabUtility.SaveAsPrefabAsset(root, DICE_BASE_PREFAB_PATH);
        UnityEngine.Object.DestroyImmediate(root);
        return savedBase;
    }

    private static void GenerateDiceVariants(GameObject basePrefab)
    {
        for (int i = 0; i < Colors.Length; i++)
        {
            string color = Colors[i];
            string lowerColor = color.ToLowerInvariant();

            // Star Variant (IDs 301..311)
            string starMatPath = $"{DICE_STAR_MAT_DIR}/Dice_{lowerColor}_star_mat.mat";
            string starPrefabPath = $"{DICE_STAR_PREFAB_DIR}/Dice_Star_{color}.prefab";
            CreateVariantPrefab(basePrefab, starPrefabPath, $"Dice_Star_{color}", 301 + i, starMatPath);

            // Moon Variant (IDs 401..411)
            string moonMatPath = $"{DICE_MOON_MAT_DIR}/Dice_{lowerColor}_moon_mat.mat";
            string moonPrefabPath = $"{DICE_MOON_PREFAB_DIR}/Dice_Moon_{color}.prefab";
            CreateVariantPrefab(basePrefab, moonPrefabPath, $"Dice_Moon_{color}", 401 + i, moonMatPath);
        }
    }

    private static void CreateVariantPrefab(GameObject basePrefab, string variantPath, string variantName, int itemId, string matPath)
    {
        if (File.Exists(variantPath)) return;

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        instance.name = variantName;

        MatchItem matchItem = instance.GetComponent<MatchItem>();
        if (matchItem != null)
        {
            SerializedObject itemSo = new SerializedObject(matchItem);
            itemSo.FindProperty("id").intValue = itemId;
            itemSo.FindProperty("itemKey").stringValue = variantName;
            itemSo.ApplyModifiedPropertiesWithoutUndo();
        }

        Transform diceMeshTransform = instance.transform.Find("Mesh/DiceMesh");
        if (diceMeshTransform != null && mat != null)
        {
            MeshRenderer mr = diceMeshTransform.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
            }
        }

        PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
        UnityEngine.Object.DestroyImmediate(instance);
    }

    private static Mesh LoadDiceMesh()
    {
        Mesh directMesh = AssetDatabase.LoadAssetAtPath<Mesh>(DICE_MODEL_PATH);
        if (directMesh != null) return directMesh;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(DICE_MODEL_PATH);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Mesh m)
            {
                return m;
            }
        }
        return null;
    }
}
#endif
