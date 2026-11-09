#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CardVariantGenerator
{
    private const string Folder = "Assets/_Game/Prefabs/MatchItems";
    private const string BasePrefabPath = Folder + "/Card_Base.prefab";
    private const string SessionKey = "CardVariantGenerator_NewFormula_v1";

    public enum CardSet
    {
        Star = 1,
        Moon = 2
    }

    public enum CardColor
    {
        Red = 1,
        Blue = 2,
        Green = 3,
        Yellow = 4,
        Purple = 5,
        Orange = 6,
        Cyan = 7,
        Black = 8,
        Pink = 9,
        Brown = 10,
        Gray = 11
    }

    static CardVariantGenerator()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        GenerateAllVariants();
    }

    [MenuItem("Tools/Generate All Card Variants (Star & Moon)")]
    public static void GenerateAllVariants()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
        if (basePrefab == null)
        {
            Debug.LogError($"[CardVariantGenerator] '{BasePrefabPath}' bulunamadı!");
            return;
        }

        Debug.Log("<color=#3498db><b>[CardVariantGenerator]</b> Card_Base bulundu, Star & Moon Variant üretimi başlıyor...</color>");

        // 1. Eski numaralı prefabları temizle (0-Card-Star-Red vb.)
        CleanLegacyPrefabs();

        // 2. Star (101-111) ve Moon (201-211) variantlarını üret
        int createdCount = 0;
        CardSet[] sets = new[] { CardSet.Star, CardSet.Moon };
        CardColor[] colors = (CardColor[])Enum.GetValues(typeof(CardColor));

        foreach (CardSet set in sets)
        {
            string setFolder = set == CardSet.Star ? "Card Star Set" : "Card Moon Set";
            string setSuffix = set == CardSet.Star ? "star" : "moon";

            foreach (CardColor color in colors)
            {
                int id = ((int)set * 100) + (int)color;
                string prefabName = $"Card_{set}_{color}";
                string variantPath = $"{Folder}/{prefabName}.prefab";
                string matPath = $"Assets/_Game/Materials/{setFolder}/Card_{color.ToString().ToLower()}_{setSuffix}_mat.mat";

                // Eğer varsa önce eskini silip taze variant oluştur
                if (File.Exists(variantPath))
                {
                    AssetDatabase.DeleteAsset(variantPath);
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
                instance.name = prefabName;

                // MatchItem ID ayarla (SerializedObject ile Prefab Override olarak kaydedilir)
                MatchItem matchItem = instance.GetComponent<MatchItem>();
                if (matchItem != null)
                {
                    SerializedObject so = new SerializedObject(matchItem);
                    SerializedProperty idProp = so.FindProperty("id");
                    if (idProp != null)
                    {
                        idProp.intValue = id;
                        so.ApplyModifiedProperties();
                    }
                }

                // CardMesh Materyalini ayarla
                Transform cardMeshTr = instance.transform.Find("Mesh/CardMesh");
                if (cardMeshTr != null)
                {
                    MeshRenderer mr = cardMeshTr.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if (mat != null)
                        {
                            mr.sharedMaterial = mat;
                            EditorUtility.SetDirty(mr);
                        }
                        else
                        {
                            Debug.LogWarning($"[CardVariantGenerator] Materyal bulunamadı: {matPath}");
                        }
                    }
                }

                // Prefab Variant olarak kaydet
                GameObject variantAsset = PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
                UnityEngine.Object.DestroyImmediate(instance);

                if (variantAsset != null)
                {
                    createdCount++;
                    Debug.Log($"<color=#2ecc71><b>[CardVariantGenerator]</b> Variant oluşturuldu: {prefabName} (ID={id})</color>");
                }
                else
                {
                    Debug.LogError($"[CardVariantGenerator] Variant kaydedilemedi: {prefabName}");
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=#2ecc71><b>[CardVariantGenerator]</b> Başarılı! Toplam {createdCount} adet Prefab Variant (Star 101-111 & Moon 201-211) üretildi!</color>");
    }

    private static void CleanLegacyPrefabs()
    {
        string[] oldPrefabs = Directory.GetFiles(Folder, "*-Card-*.prefab");
        foreach (string oldFile in oldPrefabs)
        {
            string unityPath = oldFile.Replace('\\', '/');
            AssetDatabase.DeleteAsset(unityPath);
        }
    }
}
#endif
