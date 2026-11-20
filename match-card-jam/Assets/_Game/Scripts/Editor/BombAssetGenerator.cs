#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates stylized casual 3D Bomb materials and Bomb_Item.prefab
/// using Unity primitives and URP Unlit / URP_Outline shaders.
/// </summary>
[InitializeOnLoad]
public static class BombAssetGenerator
{
    public const string HAZARDS_DIR = "Assets/_Game/Prefabs/Hazards";
    public const string BOMB_PREFAB_PATH = HAZARDS_DIR + "/Bomb_Item.prefab";
    private const string BOMB_MAT_DIR = "Assets/_Game/Materials/Bomb";
    private const string CARD_OUTLINE_MAT_PATH = "Assets/_Game/Materials/M_CardOutline.mat";

    static BombAssetGenerator()
    {
        EditorApplication.delayCall += EnsureBombPrefabCreated;
    }

    private static void EnsureBombPrefabCreated()
    {
        if (!File.Exists(BOMB_PREFAB_PATH))
        {
            GenerateBombPrefab();
        }
    }

    [MenuItem("Tools/Generate Bomb MatchItem Prefab")]
    public static void GenerateBombPrefab()
    {
        EnsureDirectories();

        Material bodyMat = CreateOrUpdateUnlitMat($"{BOMB_MAT_DIR}/M_Bomb_Body.mat", new Color(0.145f, 0.153f, 0.204f, 1f));
        Material bandMat = CreateOrUpdateUnlitMat($"{BOMB_MAT_DIR}/M_Bomb_Band.mat", new Color(0.902f, 0.224f, 0.275f, 1f));
        Material capMat = CreateOrUpdateUnlitMat($"{BOMB_MAT_DIR}/M_Bomb_Cap.mat", new Color(0.478f, 0.510f, 0.588f, 1f));
        Material fuseMat = CreateOrUpdateUnlitMat($"{BOMB_MAT_DIR}/M_Bomb_Fuse.mat", new Color(0.851f, 0.690f, 0.494f, 1f));
        Material sparkMat = CreateOrUpdateUnlitMat($"{BOMB_MAT_DIR}/M_Bomb_Spark.mat", new Color(1f, 0.68f, 0.12f, 1f));
        Material outlineMat = CreateOrUpdateBombOutlineMat();

        GameObject root = new GameObject("Bomb_Item");

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 0.6f;
        rb.angularDamping = 2.5f;

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.radius = 0.43f;

        // Main body sphere
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "BombBody";
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = Vector3.one * 0.85f;
        body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

        // Outline mesh on body
        GameObject outlineGo = new GameObject("OutlineMesh");
        outlineGo.transform.SetParent(body.transform, false);
        outlineGo.AddComponent<MeshFilter>().sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;
        MeshRenderer outlineRenderer = outlineGo.AddComponent<MeshRenderer>();
        outlineRenderer.sharedMaterial = outlineMat;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;

        // Danger red belt
        GameObject band = CreateVisualPart("BombBand", PrimitiveType.Cylinder, root.transform, Vector3.zero, Quaternion.identity, new Vector3(0.86f, 0.06f, 0.86f), bandMat);
        band.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);

        // Metallic fuse cap
        CreateVisualPart("BombCap", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.39f, 0f), Quaternion.identity, new Vector3(0.28f, 0.07f, 0.28f), capMat);

        // Fuse cord
        CreateVisualPart("BombFuse", PrimitiveType.Cylinder, root.transform, new Vector3(0.05f, 0.52f, 0f), Quaternion.Euler(0f, 0f, -22f), new Vector3(0.065f, 0.14f, 0.065f), fuseMat);

        // Glowing spark tip
        CreateVisualPart("BombSpark", PrimitiveType.Sphere, root.transform, new Vector3(0.11f, 0.66f, 0f), Quaternion.identity, Vector3.one * 0.18f, sparkMat);

        // Configure MatchItem & MatchItemOutline
        MatchItemOutline itemOutline = root.AddComponent<MatchItemOutline>();
        SerializedObject outlineSo = new SerializedObject(itemOutline);
        outlineSo.FindProperty("mainMeshRenderer").objectReferenceValue = body.GetComponent<MeshRenderer>();
        outlineSo.FindProperty("outlineRenderer").objectReferenceValue = outlineRenderer;
        outlineSo.FindProperty("normalColor").colorValue = new Color(0.85f, 0.15f, 0.15f, 1f);
        outlineSo.FindProperty("selectedColor").colorValue = Color.white;
        outlineSo.ApplyModifiedPropertiesWithoutUndo();

        MatchItem matchItem = root.AddComponent<MatchItem>();
        SerializedObject itemSo = new SerializedObject(matchItem);
        itemSo.FindProperty("id").intValue = 999;
        itemSo.FindProperty("itemKey").stringValue = "Bomb";
        itemSo.FindProperty("outline").objectReferenceValue = itemOutline;
        itemSo.FindProperty("rb").objectReferenceValue = rb;
        itemSo.FindProperty("itemCollider").objectReferenceValue = col;
        itemSo.FindProperty("dockScaleMultiplier").floatValue = 1.12f;
        itemSo.FindProperty("isBomb").boolValue = true;
        itemSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, BOMB_PREFAB_PATH);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=#e74c3c><b>[BombAssetGenerator]</b> Generated {BOMB_PREFAB_PATH}!</color>");
    }

    private static GameObject CreateVisualPart(string name, PrimitiveType type, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    private static Material CreateOrUpdateUnlitMat(string matPath, Color color)
    {
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(unlitShader);
            mat.name = Path.GetFileNameWithoutExtension(matPath);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material CreateOrUpdateBombOutlineMat()
    {
        string matPath = $"{BOMB_MAT_DIR}/M_Bomb_Outline.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Material refOutline = AssetDatabase.LoadAssetAtPath<Material>(CARD_OUTLINE_MAT_PATH);
            Shader shader = refOutline != null ? refOutline.shader : Shader.Find("Custom/URP_Outline");
            mat = refOutline != null ? new Material(refOutline) : new Material(shader);
            mat.name = "M_Bomb_Outline";
            AssetDatabase.CreateAsset(mat, matPath);
        }

        mat.SetFloat("_UseRadialExtrusion", 1f);
        mat.SetFloat("_OutlineWidth", 0.006f);
        mat.SetColor("_OutlineColor", new Color(0.85f, 0.15f, 0.15f, 1f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void EnsureDirectories()
    {
        if (!Directory.Exists(HAZARDS_DIR)) Directory.CreateDirectory(HAZARDS_DIR);
        if (!Directory.Exists(BOMB_MAT_DIR)) Directory.CreateDirectory(BOMB_MAT_DIR);
        AssetDatabase.Refresh();
    }
}
#endif
