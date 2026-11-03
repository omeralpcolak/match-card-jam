#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
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
        public Color PrimaryInk;
        public Color DarkInk;

        public CardColorSpec(string name, string texPath, string matPath, Color primary, Color dark)
        {
            Name = name;
            TexturePath = texPath;
            MaterialPath = matPath;
            PrimaryInk = primary;
            DarkInk = dark;
        }
    }

    public static readonly CardColorSpec[] DefaultSpecs = new[]
    {
        new CardColorSpec("Red",    $"{CardsTextureFolder}/T_CardBack_Red.png",    "Assets/_Game/Materials/Card_red_mat.mat",    new Color(0.82f, 0.12f, 0.16f), new Color(0.52f, 0.05f, 0.08f)),
        new CardColorSpec("Blue",   $"{CardsTextureFolder}/T_CardBack_Blue.png",   "Assets/_Game/Materials/Card_blue_mat.mat",   new Color(0.11f, 0.42f, 0.85f), new Color(0.05f, 0.22f, 0.55f)),
        new CardColorSpec("Green",  $"{CardsTextureFolder}/T_CardBack_Green.png",  "Assets/_Game/Materials/Card_green_mat.mat",  new Color(0.10f, 0.58f, 0.28f), new Color(0.04f, 0.34f, 0.15f)),
        new CardColorSpec("Yellow", $"{CardsTextureFolder}/T_CardBack_Yellow.png", "Assets/_Game/Materials/Card_yellow_mat.mat", new Color(0.88f, 0.62f, 0.04f), new Color(0.62f, 0.38f, 0.00f)),
        new CardColorSpec("Purple", $"{CardsTextureFolder}/T_CardBack_Purple.png", "Assets/_Game/Materials/Card_purple_mat.mat", new Color(0.52f, 0.16f, 0.78f), new Color(0.30f, 0.07f, 0.50f)),
        new CardColorSpec("Navy",   $"{CardsTextureFolder}/T_CardBack_Navy.png",   null,                                         new Color(0.10f, 0.16f, 0.36f), new Color(0.05f, 0.08f, 0.22f)),
        new CardColorSpec("Orange", $"{CardsTextureFolder}/T_CardBack_Orange.png", null,                                         new Color(0.90f, 0.38f, 0.08f), new Color(0.60f, 0.20f, 0.02f)),
        new CardColorSpec("Cyan",   $"{CardsTextureFolder}/T_CardBack_Cyan.png",   null,                                         new Color(0.06f, 0.62f, 0.68f), new Color(0.02f, 0.38f, 0.44f)),
        new CardColorSpec("Black",  $"{CardsTextureFolder}/T_CardBack_Black.png",  null,                                         new Color(0.16f, 0.17f, 0.20f), new Color(0.07f, 0.08f, 0.10f))
    };

    static CardPatternGenerator()
    {
        EditorApplication.delayCall += CheckAndBindMaterials;
    }

    private static void CheckAndBindMaterials()
    {
        bool anyMissing = false;
        foreach (var spec in DefaultSpecs)
        {
            if (!File.Exists(spec.TexturePath))
            {
                anyMissing = true;
                break;
            }
        }

        if (anyMissing)
        {
            GenerateAllCardTextures();
        }
        else
        {
            BindTexturesToMaterials();
        }
    }

    [MenuItem("Tools/Card Pattern Generator Window...")]
    public static void OpenWindow()
    {
        CardPatternGeneratorWindow.ShowWindow();
    }

    [MenuItem("Tools/Generate Default Card Pattern Textures & Materials")]
    public static void GenerateAllCardTextures()
    {
        foreach (var spec in DefaultSpecs)
        {
            GenerateSingleCardTexture(spec.TexturePath, spec.PrimaryInk, spec.DarkInk, spec.MaterialPath);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("<color=#3498db><b>[CardPatternGenerator]</b> Tüm kart deseni Base Map dokuları oluşturuldu ve materyallere atandı!</color>");
    }

    /// <summary>
    /// Tek bir ana renkten koyu gölge tonunu otomatik hesaplar.
    /// </summary>
    public static Color CalculateDarkInk(Color primary)
    {
        Color.RGBToHSV(primary, out float h, out float s, out float v);
        return Color.HSVToRGB(h, Mathf.Clamp01(s * 1.1f), v * 0.62f);
    }

    /// <summary>
    /// İstediğin herhangi bir renkte kart deseni dokusu üretir ve isteğe bağlı olarak materyale atar.
    /// </summary>
    public static Texture2D GenerateSingleCardTexture(
        string texturePath,
        Color primaryInk,
        Color darkInk,
        string materialPath = null,
        Material targetMaterial = null,
        int resolution = 1024)
    {
        string dir = Path.GetDirectoryName(texturePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        int width = Mathf.Clamp(resolution, 256, 2048);
        int height = width;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color accum = Color.black;
                for (int sy = 0; sy < 2; sy++)
                {
                    for (int sx = 0; sx < 2; sx++)
                    {
                        float u = (x + (sx + 0.5f) * 0.5f) / width;
                        float v = (y + (sy + 0.5f) * 0.5f) / height;
                        accum += EvaluateCardPixel(u, v, primaryInk, darkInk);
                    }
                }
                Color finalCol = accum * 0.25f;
                finalCol.a = 1f;
                pixels[y * width + x] = finalCol;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        byte[] pngData = tex.EncodeToPNG();
        File.WriteAllBytes(texturePath, pngData);
        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
        }

        Texture2D importedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

        if (targetMaterial == null && !string.IsNullOrEmpty(materialPath))
        {
            targetMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        }

        if (targetMaterial != null && importedTex != null)
        {
            Undo.RecordObject(targetMaterial, "Assign Card Pattern Texture");
            targetMaterial.SetTexture("_BaseMap", importedTex);
            targetMaterial.SetTexture("_MainTex", importedTex);
            targetMaterial.SetColor("_BaseColor", Color.white);
            targetMaterial.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(targetMaterial);
            AssetDatabase.SaveAssets();
        }

        return importedTex;
    }

    public static void BindTexturesToMaterials()
    {
        bool dirty = false;
        foreach (var spec in DefaultSpecs)
        {
            if (string.IsNullOrEmpty(spec.MaterialPath)) continue;

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.TexturePath);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath);
            if (tex == null || mat == null) continue;

            if (mat.GetTexture("_BaseMap") != tex || mat.GetColor("_BaseColor") != Color.white)
            {
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

    private static float SmoothStep(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    private static float SdRoundedBox(float px, float py, float bx, float by, float r)
    {
        float qx = Mathf.Abs(px) - bx + r;
        float qy = Mathf.Abs(py) - by + r;
        float mx = Mathf.Max(qx, 0f);
        float my = Mathf.Max(qy, 0f);
        float outside = Mathf.Sqrt(mx * mx + my * my);
        float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
        return outside + inside - r;
    }

    private static float Hash21(float x, float y)
    {
        float n = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
        return n - Mathf.Floor(n);
    }

    public static Color EvaluateCardPixel(float u, float v, Color primaryInk, Color darkInk)
    {
        float grain = (Hash21(Mathf.Floor(u * 1024f), Mathf.Floor(v * 1024f)) - 0.5f) * 0.008f;
        float paperBase = 0.982f + grain;
        Color paper = new Color(
            Mathf.Clamp01(paperBase),
            Mathf.Clamp01(paperBase * 0.997f),
            Mathf.Clamp01(paperBase * 0.990f),
            1f
        );

        float cx = (u - 0.5f) / 0.625f;
        float cy = (v - 0.5f) / 0.66533f;

        const float OuterW = 0.392f;
        const float OuterH = 0.638f;
        const float OuterR = 0.056f;
        const float aa = 0.0011f;

        float dOuter = SdRoundedBox(cx, cy, OuterW, OuterH, OuterR);
        if (dOuter > aa * 2f)
        {
            return paper;
        }

        float inkAmount = 0f;
        float darkAccent = 0f;

        // 1. Dış ince çerçeve çizgisi
        if (dOuter <= aa && dOuter >= -0.0068f - aa)
        {
            float w1 = 1f - SmoothStep(-aa, aa, dOuter);
            float w2 = SmoothStep(-0.0068f - aa, -0.0068f + aa, dOuter);
            inkAmount = Mathf.Max(inkAmount, w1 * w2);
            darkAccent = Mathf.Max(darkAccent, w1 * w2 * 0.35f);
        }

        // 2. Dekoratif zincir/motif bordür kuşağı
        float dBandCenter = Mathf.Abs(dOuter + 0.0245f) - 0.0120f;
        if (dBandCenter < aa)
        {
            float bandMask = 1f - SmoothStep(-aa, aa, dBandCenter);
            float bt = (dOuter + 0.0245f) / 0.0240f;

            float straightW = OuterW - OuterR;
            float straightH = OuterH - OuterR;
            float bandR = OuterR - 0.0245f;
            float cornerArc = 0.5f * Mathf.PI * bandR;
            float totalQuadLen = straightW + cornerArc + straightH;
            const float numRepeats = 40f;

            float axC = Mathf.Abs(cx);
            float ayC = Mathf.Abs(cy);
            float qx = axC - straightW;
            float qy = ayC - straightH;

            float arcPos;
            if (qx <= 0f && qy >= qx)
            {
                arcPos = axC;
            }
            else if (qx > 0f && qy > 0f)
            {
                float ang = Mathf.Atan2(qx, qy);
                arcPos = straightW + ang * bandR;
            }
            else
            {
                arcPos = straightW + cornerArc + (straightH - ayC);
            }

            float gs = (arcPos / totalQuadLen) * numRepeats;
            float sParam = gs - Mathf.Floor(gs) - 0.5f;

            float diamond = Mathf.Abs(sParam) * 1.15f + Mathf.Abs(bt) * 1.15f;
            float crossH = Mathf.Max(Mathf.Abs(sParam) - 0.38f, Mathf.Abs(bt) - 0.09f);
            float crossV = Mathf.Max(Mathf.Abs(bt) - 0.38f, Mathf.Abs(sParam) - 0.09f);
            float motif = Mathf.Min(diamond - 0.34f, Mathf.Min(crossH, crossV));
            float innerDot = Mathf.Sqrt(sParam * sParam + bt * bt) - 0.11f;
            float dxE = Mathf.Abs(sParam) - 0.5f;
            float dyE = Mathf.Abs(bt) - 0.36f;
            float edgeDot = Mathf.Sqrt(dxE * dxE + dyE * dyE) - 0.09f;

            float cutout = 1f - SmoothStep(-0.04f, 0.04f, motif);
            float reink = 1f - SmoothStep(-0.04f, 0.04f, innerDot);
            float edgeCut = 1f - SmoothStep(-0.04f, 0.04f, edgeDot);
            float patternInk = Mathf.Clamp01(1f - cutout + reink - edgeCut);
            inkAmount = Mathf.Max(inkAmount, bandMask * patternInk);
        }

        // 3. İç ince çerçeve çizgisi
        if (dOuter <= -0.0425f + aa && dOuter >= -0.0490f - aa)
        {
            float w1 = 1f - SmoothStep(-0.0425f - aa, -0.0425f + aa, dOuter);
            float w2 = SmoothStep(-0.0490f - aa, -0.0490f + aa, dOuter);
            inkAmount = Mathf.Max(inkAmount, w1 * w2);
            darkAccent = Mathf.Max(darkAccent, w1 * w2 * 0.35f);
        }

        // 4. Merkez 7x12 Geometrik Elmas & Yıldız Kafes Deseni
        float innerW = OuterW - 0.0565f;
        float innerH = OuterH - 0.0565f;
        const float innerR = 0.012f;
        float dInner = SdRoundedBox(cx, cy, innerW, innerH, innerR);

        if (dInner < aa)
        {
            float fieldMask = 1f - SmoothStep(-aa, aa, dInner);
            float dInnerFrame = dInner + 0.0055f;

            const float cols = 7f;
            const float rows = 12f;
            float gx = (cx / (2f * innerW) + 0.5f) * cols;
            float gy = (cy / (2f * innerH) + 0.5f) * rows;

            float ux = gx - Mathf.Floor(gx) - 0.5f;
            float uy = gy - Mathf.Floor(gy) - 0.5f;
            float ax = Mathf.Abs(ux);
            float ay = Mathf.Abs(uy);

            float cellWhite = 0f;

            float diagDist = Mathf.Abs(ax + ay - 0.5f);
            float diagOuter = 1f - SmoothStep(0.068f, 0.082f, diagDist);
            float diagCenterLine = 1f - SmoothStep(0.012f, 0.024f, diagDist);
            cellWhite = Mathf.Max(cellWhite, diagOuter * (1f - 0.65f * diagCenterLine));

            float rCenter = Mathf.Sqrt(ux * ux + uy * uy);
            float dDiamond = ax + ay;
            float dBox = Mathf.Max(ax, ay) * 1.32f;
            float star8 = Mathf.Min(dDiamond, dBox);
            float petalH = ax * 0.85f + ay * 2.2f;
            float petalV = ay * 0.85f + ax * 2.2f;
            float rosette = Mathf.Min(star8, Mathf.Min(petalH, petalV));

            float rosOuterWhite = 1f - SmoothStep(0.285f, 0.302f, rosette);
            float rosInnerInk = 1f - SmoothStep(0.198f, 0.215f, Mathf.Min(dDiamond, dBox * 1.04f));
            float innerStar = Mathf.Min(ax * 0.7f + ay * 1.65f, ay * 0.7f + ax * 1.65f);
            innerStar = Mathf.Min(innerStar, dDiamond * 0.95f);
            float rosCoreWhite = 1f - SmoothStep(0.115f, 0.130f, innerStar);
            float rosPinInk = 1f - SmoothStep(0.035f, 0.048f, rCenter);

            cellWhite = Mathf.Max(cellWhite, rosOuterWhite);
            cellWhite *= (1f - rosInnerInk);
            cellWhite = Mathf.Max(cellWhite, rosCoreWhite);
            cellWhite *= (1f - rosPinInk);

            float vx = 0.5f - ax;
            float vy = 0.5f - ay;
            float vDiamond = vx + vy;
            float vPetal = Mathf.Min(vx * 0.75f + vy * 2.1f, vy * 0.75f + vx * 2.1f);
            float vShape = Mathf.Min(vDiamond, vPetal);
            float vOuterWhite = 1f - SmoothStep(0.235f, 0.252f, vShape);
            float vInnerInk = 1f - SmoothStep(0.135f, 0.152f, vDiamond);
            float vCenterDotWhite = 1f - SmoothStep(0.062f, 0.078f, Mathf.Sqrt(vx * vx + vy * vy));

            cellWhite = Mathf.Max(cellWhite, vOuterWhite);
            cellWhite *= (1f - vInnerInk);
            cellWhite = Mathf.Max(cellWhite, vCenterDotWhite);

            float dPearl1 = Mathf.Sqrt((ax - 0.355f) * (ax - 0.355f) + ay * ay);
            float dPearl2 = Mathf.Sqrt(ax * ax + (ay - 0.355f) * (ay - 0.355f));
            float pearlWhite = 1f - SmoothStep(0.042f, 0.056f, Mathf.Min(dPearl1, dPearl2));
            cellWhite = Mathf.Max(cellWhite, pearlWhite);

            float dEdge1 = Mathf.Abs(0.5f - ax) + ay;
            float dEdge2 = ax + Mathf.Abs(0.5f - ay);
            float edgeDiamondWhite = 1f - SmoothStep(0.075f, 0.090f, Mathf.Min(dEdge1, dEdge2));
            float edgeDiamondCoreInk = 1f - SmoothStep(0.030f, 0.044f, Mathf.Min(dEdge1, dEdge2));
            cellWhite = Mathf.Max(cellWhite, edgeDiamondWhite);
            cellWhite *= (1f - edgeDiamondCoreInk);

            if (dInnerFrame > -aa)
            {
                float borderKeepInk = SmoothStep(-aa, aa, dInnerFrame);
                cellWhite *= (1f - borderKeepInk);
            }

            float cellDark = (1f - cellWhite) * (0.25f * SmoothStep(0.35f, 0f, rCenter) + 0.2f * diagCenterLine);
            float fieldInk = 1f - Mathf.Clamp01(cellWhite);

            inkAmount = Mathf.Max(inkAmount, fieldMask * fieldInk);
            darkAccent = Mathf.Max(darkAccent, fieldMask * cellDark);
        }

        Color ink = Color.Lerp(primaryInk, darkInk, Mathf.Clamp01(darkAccent));
        ink.r = Mathf.Clamp01(ink.r + grain * 0.4f);
        ink.g = Mathf.Clamp01(ink.g + grain * 0.4f);
        ink.b = Mathf.Clamp01(ink.b + grain * 0.4f);

        return Color.Lerp(paper, ink, Mathf.Clamp01(inkAmount));
    }
}

/// <summary>
/// İstediğin herhangi bir renkte kart deseni üretmek için görsel Unity Editor penceresi.
/// </summary>
public class CardPatternGeneratorWindow : EditorWindow
{
    private string customColorName = "Pink";
    private Color customPrimaryColor = new Color(0.92f, 0.22f, 0.56f, 1f);
    private bool autoCalculateDarkColor = true;
    private Color customDarkColor = new Color(0.58f, 0.10f, 0.34f, 1f);
    private Material targetMaterial;
    private int resolution = 1024;
    private Texture2D lastGeneratedPreview;

    public static void ShowWindow()
    {
        var window = GetWindow<CardPatternGeneratorWindow>("Card Pattern Generator");
        window.minSize = new Vector2(380f, 440f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Özel Renkte Kart Deseni Üretici", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Seçtiğin herhangi bir renkte aynı kart sırt desenini (T_CardBack_<İsim>.png) üretir ve istersen seçtiğin materyalin Base Map slotuna otomatik atar.",
            MessageType.Info);

        EditorGUILayout.Space(6f);

        customColorName = EditorGUILayout.TextField("Renk / Dosya Adı", customColorName);

        EditorGUI.BeginChangeCheck();
        customPrimaryColor = EditorGUILayout.ColorField("Ana Desen Rengi", customPrimaryColor);
        autoCalculateDarkColor = EditorGUILayout.Toggle("Gölge Tonunu Otomatik Hesapla", autoCalculateDarkColor);

        if (autoCalculateDarkColor)
        {
            customDarkColor = CardPatternGenerator.CalculateDarkInk(customPrimaryColor);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ColorField("Koyu Detay Rengi (Otomatik)", customDarkColor);
            }
        }
        else
        {
            customDarkColor = EditorGUILayout.ColorField("Koyu Detay Rengi", customDarkColor);
        }
        EditorGUI.EndChangeCheck();

        resolution = EditorGUILayout.IntPopup(
            "Çözünürlük",
            resolution,
            new[] { "512 x 512", "1024 x 1024 (Önerilen)", "2048 x 2048" },
            new[] { 512, 1024, 2048 });

        targetMaterial = (Material)EditorGUILayout.ObjectField(
            "Hedef Materyal (Opsiyonel)",
            targetMaterial,
            typeof(Material),
            false);

        string safeName = string.IsNullOrWhiteSpace(customColorName) ? "Custom" : customColorName.Trim();
        string outputPath = $"{CardPatternGenerator.CardsTextureFolder}/T_CardBack_{safeName}.png";
        EditorGUILayout.LabelField("Kaydedilecek Yol:", outputPath, EditorStyles.miniLabel);

        EditorGUILayout.Space(10f);

        GUI.backgroundColor = new Color(0.25f, 0.75f, 0.45f);
        if (GUILayout.Button($"'{safeName}' Renginde Base Map Üret", GUILayout.Height(36f)))
        {
            lastGeneratedPreview = CardPatternGenerator.GenerateSingleCardTexture(
                outputPath,
                customPrimaryColor,
                customDarkColor,
                null,
                targetMaterial,
                resolution);

            if (lastGeneratedPreview != null)
            {
                EditorGUIUtility.PingObject(lastGeneratedPreview);
                Debug.Log($"<color=#2ecc71><b>[CardPatternGenerator]</b> Özel kart dokusu üretildi: {outputPath}</color>");
            }
        }
        GUI.backgroundColor = Color.white;

        if (lastGeneratedPreview != null)
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Son Üretilen Doku Önizlemesi:", EditorStyles.boldLabel);
            Rect previewRect = GUILayoutUtility.GetRect(160f, 160f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawPreviewTexture(previewRect, lastGeneratedPreview, null, ScaleMode.ScaleToFit);
        }

        EditorGUILayout.Space(14f);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        if (GUILayout.Button("Varsayılan 9 Rengi Yeniden Üret ve Materyallere Bağla", GUILayout.Height(28f)))
        {
            CardPatternGenerator.GenerateAllCardTextures();
        }
    }
}
#endif
