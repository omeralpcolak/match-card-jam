#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor Window for rapidly generating Match Card Jam level prefabs with guaranteed
/// triplet (multiple of 3) item counts, configurable layouts, and LevelDatabase registration.
/// </summary>
public class LevelGeneratorWindow : EditorWindow
{
    private enum LayoutMode
    {
        RandomPile,
        StructuredGrid
    }

    private class ItemEntry
    {
        public MatchItem Prefab;
        public string FolderGroup;
        public int SetCount;
    }

    private const string MATCH_ITEMS_ROOT = "Assets/_Game/Prefabs/MatchItems";
    private const string LEVELS_OUTPUT_DIR = "Assets/_Game/Prefabs/Levels";
    private const string DEFAULT_TEMPLATE_PATH = "Assets/_Game/Prefabs/Levels/Level-1.prefab";

    // Level & Database Settings
    private GameObject _templatePrefab;
    private int _levelNumber = 4;
    private int _dockCapacity = 7;
    private bool _useTimer = true;
    private float _timeLimit = 100f;
    private bool _addToLevelDatabase = true;

    // Layout Settings
    private LayoutMode _layoutMode = LayoutMode.RandomPile;
    private Vector3 _spawnCenter = new Vector3(0f, 0.25f, 1.0f);
    private Vector2 _pileAreaSize = new Vector2(4.2f, 5.0f);
    private float _layerHeightStep = 0.4f;
    private int _itemsPerLayer = 8;
    private bool _randomYawRotation = true;
    private int _gridColumns = 4;
    private Vector2 _gridSpacing = new Vector2(1.35f, 1.85f);

    // Quick Random Fill
    private int _randomTypeCount = 6;
    private int _randomSetsPerType = 1;
    private string _searchFilter = "";

    private readonly List<ItemEntry> _catalog = new();
    private readonly Dictionary<string, bool> _groupFoldouts = new();
    private Vector2 _scrollPos;

    [MenuItem("Tools/Level Generator")]
    public static void ShowWindow()
    {
        LevelGeneratorWindow window = GetWindow<LevelGeneratorWindow>("Level Generator");
        window.minSize = new Vector2(420, 620);
        window.Show();
    }

    private void OnEnable()
    {
        LoadDefaultTemplate();
        SuggestNextLevelNumber();
        RefreshItemCatalog();
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawHeader();
        EditorGUILayout.Space(8);

        DrawLevelConfigSection();
        EditorGUILayout.Space(8);

        DrawLayoutSection();
        EditorGUILayout.Space(8);

        DrawQuickFillSection();
        EditorGUILayout.Space(8);

        DrawCatalogSection();
        EditorGUILayout.Space(12);

        DrawActionsSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Level Generator", EditorStyles.boldLabel);
        GUILayout.Label("Build level prefabs with guaranteed 3-item sets and automatic LevelDatabase registration.", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }

    private void DrawLevelConfigSection()
    {
        GUILayout.Label("1. Level Configuration", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        GameObject newTemplate = (GameObject)EditorGUILayout.ObjectField("Template Prefab", _templatePrefab, typeof(GameObject), false);
        if (newTemplate != _templatePrefab)
        {
            _templatePrefab = newTemplate;
            SyncDockCapacityFromTemplate();
        }

        if (_templatePrefab != null && GUILayout.Button("Load Settings & Items From Template", EditorStyles.miniButton))
        {
            LoadFromExistingPrefab(_templatePrefab);
        }

        _levelNumber = Mathf.Max(1, EditorGUILayout.IntField("Level Number", _levelNumber));
        _dockCapacity = EditorGUILayout.IntSlider("Dock Capacity", _dockCapacity, 3, 10);
        _useTimer = EditorGUILayout.Toggle("Use Timer", _useTimer);

        if (_useTimer)
        {
            _timeLimit = Mathf.Max(10f, EditorGUILayout.FloatField("Time Limit (sec)", _timeLimit));
        }

        _addToLevelDatabase = EditorGUILayout.Toggle("Add to LevelDatabase", _addToLevelDatabase);

        EditorGUILayout.EndVertical();
    }

    private void DrawLayoutSection()
    {
        GUILayout.Label("2. Layout Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        _layoutMode = (LayoutMode)EditorGUILayout.EnumPopup("Layout Mode", _layoutMode);
        _spawnCenter = EditorGUILayout.Vector3Field("Board Center", _spawnCenter);
        _layerHeightStep = Mathf.Max(0.05f, EditorGUILayout.FloatField("Layer Height Step", _layerHeightStep));

        if (_layoutMode == LayoutMode.RandomPile)
        {
            _pileAreaSize = EditorGUILayout.Vector2Field("Pile Area (X, Z)", _pileAreaSize);
            _itemsPerLayer = Mathf.Max(1, EditorGUILayout.IntField("Items Per Layer", _itemsPerLayer));
            _randomYawRotation = EditorGUILayout.Toggle("Random Y Rotation", _randomYawRotation);
        }
        else
        {
            _gridColumns = Mathf.Max(1, EditorGUILayout.IntField("Grid Columns", _gridColumns));
            _gridSpacing = EditorGUILayout.Vector2Field("Grid Spacing (X, Z)", _gridSpacing);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawQuickFillSection()
    {
        GUILayout.Label("3. Quick Random Fill", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        _randomTypeCount = Mathf.Max(1, EditorGUILayout.IntField("Types", _randomTypeCount));
        _randomSetsPerType = Mathf.Max(1, EditorGUILayout.IntField("Sets / Type", _randomSetsPerType));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto-Pick Random Sets"))
        {
            AutoPickRandomSets();
        }
        if (GUILayout.Button("Clear Selection", GUILayout.Width(120)))
        {
            ClearAllSelections();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawCatalogSection()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("4. MatchItems (1 Set = 3 Items)", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(65)))
        {
            RefreshItemCatalog();
        }
        EditorGUILayout.EndHorizontal();

        _searchFilter = EditorGUILayout.TextField("Search", _searchFilter, EditorStyles.toolbarSearchField);

        EditorGUILayout.BeginVertical("box");

        if (_catalog.Count == 0)
        {
            EditorGUILayout.HelpBox($"No MatchItem prefabs found under {MATCH_ITEMS_ROOT}.", MessageType.Warning);
        }
        else
        {
            string currentGroup = null;
            bool groupOpen = true;

            for (int i = 0; i < _catalog.Count; i++)
            {
                ItemEntry entry = _catalog[i];
                if (entry.Prefab == null) continue;

                if (!string.IsNullOrEmpty(_searchFilter) &&
                    !entry.Prefab.name.ToLower().Contains(_searchFilter.ToLower()))
                {
                    continue;
                }

                if (entry.FolderGroup != currentGroup)
                {
                    currentGroup = entry.FolderGroup;
                    if (!_groupFoldouts.ContainsKey(currentGroup))
                    {
                        _groupFoldouts[currentGroup] = true;
                    }
                    _groupFoldouts[currentGroup] = EditorGUILayout.Foldout(_groupFoldouts[currentGroup], currentGroup, true, EditorStyles.foldoutHeader);
                    groupOpen = _groupFoldouts[currentGroup];
                }

                if (!groupOpen) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{entry.Prefab.name} (ID: {entry.Prefab.Id})", GUILayout.MinWidth(180));

                if (GUILayout.Button("-", EditorStyles.miniButtonLeft, GUILayout.Width(24)))
                {
                    entry.SetCount = Mathf.Max(0, entry.SetCount - 1);
                }

                entry.SetCount = Mathf.Max(0, EditorGUILayout.IntField(entry.SetCount, GUILayout.Width(36)));

                if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(24)))
                {
                    entry.SetCount++;
                }

                GUILayout.Label($"({entry.SetCount * 3})", EditorStyles.miniLabel, GUILayout.Width(36));
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndVertical();

        CalculateTotals(out int selectedTypes, out int totalSets, out int totalItems);
        EditorGUILayout.HelpBox($"Selected: {selectedTypes} Types  |  {totalSets} Sets  |  {totalItems} Total Items", MessageType.Info);
    }

    private void DrawActionsSection()
    {
        CalculateTotals(out _, out _, out int totalItems);
        GUI.enabled = _templatePrefab != null && totalItems > 0;

        GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
        if (GUILayout.Button($"Generate Level-{_levelNumber}.prefab", GUILayout.Height(36)))
        {
            GenerateLevelPrefab();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn Preview in Scene", GUILayout.Height(26)))
        {
            SpawnPreviewInScene();
        }
        GUI.enabled = true;

        if (GUILayout.Button("Reshuffle Active Scene Level", GUILayout.Height(26)))
        {
            ReshuffleActiveSceneLevel();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void GenerateLevelPrefab()
    {
        if (!Directory.Exists(LEVELS_OUTPUT_DIR))
        {
            Directory.CreateDirectory(LEVELS_OUTPUT_DIR);
            AssetDatabase.Refresh();
        }

        string targetPath = $"{LEVELS_OUTPUT_DIR}/Level-{_levelNumber}.prefab";
        if (File.Exists(targetPath))
        {
            if (!EditorUtility.DisplayDialog(
                "Overwrite Level Prefab?",
                $"'{targetPath}' already exists. Do you want to overwrite it?",
                "Overwrite",
                "Cancel"))
            {
                return;
            }
        }

        GameObject tempInstance = CreatePopulatedLevelInstance($"Level-{_levelNumber}");
        if (tempInstance == null) return;

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempInstance, targetPath);
        DestroyImmediate(tempInstance);

        if (savedPrefab != null)
        {
            if (_addToLevelDatabase)
            {
                RegisterInLevelDatabase(savedPrefab);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(savedPrefab);
            Debug.Log($"<color=#2ecc71><b>[LevelGenerator]</b> Generated '{targetPath}' successfully.</color>");

            SuggestNextLevelNumber();
        }
    }

    private void SpawnPreviewInScene()
    {
        GameObject instance = CreatePopulatedLevelInstance($"Level-{_levelNumber}_Preview");
        if (instance != null)
        {
            Undo.RegisterCreatedObjectUndo(instance, "Spawn Level Preview");
            Selection.activeGameObject = instance;
        }
    }

    private void ReshuffleActiveSceneLevel()
    {
        LevelManager levelManager = FindFirstObjectByType<LevelManager>();
        if (levelManager == null)
        {
            EditorUtility.DisplayDialog("No Level Found", "No active LevelManager found in the current scene.", "OK");
            return;
        }

        Transform root = levelManager.MatchItemRoot != null ? levelManager.MatchItemRoot : levelManager.transform;
        MatchItem[] items = root.GetComponentsInChildren<MatchItem>(true);
        if (items.Length == 0) return;

        List<Transform> itemTransforms = new(items.Length);
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
            {
                Undo.RecordObject(items[i].transform, "Reshuffle Level Items");
                itemTransforms.Add(items[i].transform);
            }
        }

        ShuffleList(itemTransforms);
        ApplyLayoutPositions(itemTransforms);
        levelManager.ScanBoardItems();
    }

    private GameObject CreatePopulatedLevelInstance(string objectName)
    {
        if (_templatePrefab == null)
        {
            Debug.LogError("[LevelGenerator] Template Prefab is not assigned!");
            return null;
        }

        GameObject instance = Instantiate(_templatePrefab);
        instance.name = objectName;

        LevelManager levelManager = instance.GetComponent<LevelManager>();
        if (levelManager == null)
        {
            Debug.LogError("[LevelGenerator] Template Prefab does not have a LevelManager component!");
            DestroyImmediate(instance);
            return null;
        }

        // Apply timer configuration via SerializedObject
        SerializedObject so = new SerializedObject(levelManager);
        SerializedProperty useTimerProp = so.FindProperty("useTimer");
        SerializedProperty timeLimitProp = so.FindProperty("timeLimit");
        if (useTimerProp != null) useTimerProp.boolValue = _useTimer;
        if (timeLimitProp != null) timeLimitProp.floatValue = _timeLimit;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Apply dock capacity and regenerate slots if changed from template
        DockManager dockManager = instance.GetComponentInChildren<DockManager>(true);
        if (dockManager != null && dockManager.Capacity != _dockCapacity)
        {
            SerializedObject dockSo = new SerializedObject(dockManager);
            SerializedProperty capacityProp = dockSo.FindProperty("capacity");
            if (capacityProp != null)
            {
                capacityProp.intValue = _dockCapacity;
                dockSo.ApplyModifiedPropertiesWithoutUndo();
            }
            dockManager.GenerateSlotsInEditor();
        }

        // Locate or create MatchItemRoot
        Transform itemRoot = levelManager.MatchItemRoot;
        if (itemRoot == null)
        {
            Transform found = instance.transform.Find("MatchItemRoot");
            if (found != null)
            {
                itemRoot = found;
            }
            else
            {
                GameObject rootGo = new GameObject("MatchItemRoot");
                rootGo.transform.SetParent(instance.transform, false);
                itemRoot = rootGo.transform;
            }
        }

        // Clear any existing MatchItems from the template
        for (int i = itemRoot.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(itemRoot.GetChild(i).gameObject);
        }

        // Instantiate 3 items per set as linked prefab instances
        List<Transform> spawnedTransforms = new();
        for (int i = 0; i < _catalog.Count; i++)
        {
            ItemEntry entry = _catalog[i];
            if (entry.Prefab == null || entry.SetCount <= 0) continue;

            int count = entry.SetCount * 3;
            for (int j = 0; j < count; j++)
            {
                GameObject itemGo = (GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab.gameObject, itemRoot);
                if (itemGo != null)
                {
                    spawnedTransforms.Add(itemGo.transform);
                }
            }
        }

        ShuffleList(spawnedTransforms);
        ApplyLayoutPositions(spawnedTransforms);
        levelManager.ScanBoardItems();

        return instance;
    }

    private void ApplyLayoutPositions(List<Transform> items)
    {
        if (_layoutMode == LayoutMode.RandomPile)
        {
            float halfX = _pileAreaSize.x * 0.5f;
            float halfZ = _pileAreaSize.y * 0.5f;

            for (int i = 0; i < items.Count; i++)
            {
                int layer = i / _itemsPerLayer;
                float x = _spawnCenter.x + UnityEngine.Random.Range(-halfX, halfX);
                float z = _spawnCenter.z + UnityEngine.Random.Range(-halfZ, halfZ);
                float y = _spawnCenter.y + layer * _layerHeightStep + UnityEngine.Random.Range(0f, _layerHeightStep * 0.25f);

                items[i].localPosition = new Vector3(x, y, z);

                float yaw = _randomYawRotation ? UnityEngine.Random.Range(0f, 360f) : 0f;
                items[i].localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }
        else
        {
            int rowsPerLayer = Mathf.Max(1, Mathf.CeilToInt((float)_itemsPerLayer / _gridColumns));
            int perLayer = _gridColumns * rowsPerLayer;
            float totalWidth = (_gridColumns - 1) * _gridSpacing.x;
            float totalDepth = (rowsPerLayer - 1) * _gridSpacing.y;

            for (int i = 0; i < items.Count; i++)
            {
                int layer = i / perLayer;
                int indexInLayer = i % perLayer;
                int col = indexInLayer % _gridColumns;
                int row = indexInLayer / _gridColumns;

                float x = _spawnCenter.x - (totalWidth * 0.5f) + col * _gridSpacing.x;
                float z = _spawnCenter.z - (totalDepth * 0.5f) + row * _gridSpacing.y;
                float y = _spawnCenter.y + layer * _layerHeightStep;

                items[i].localPosition = new Vector3(x, y, z);
                items[i].localRotation = Quaternion.identity;
            }
        }
    }

    private void RegisterInLevelDatabase(GameObject levelPrefab)
    {
        LevelDatabase db = LevelDatabase.Instance;
        if (db == null) return;

        SerializedObject dbSo = new SerializedObject(db);
        SerializedProperty listProp = dbSo.FindProperty("levelPrefabs");
        if (listProp == null) return;

        for (int i = 0; i < listProp.arraySize; i++)
        {
            if (listProp.GetArrayElementAtIndex(i).objectReferenceValue == levelPrefab)
            {
                return;
            }
        }

        int newIndex = listProp.arraySize;
        listProp.InsertArrayElementAtIndex(newIndex);
        listProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = levelPrefab;
        dbSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(db);
    }

    private void RefreshItemCatalog()
    {
        _catalog.Clear();

        if (!AssetDatabase.IsValidFolder(MATCH_ITEMS_ROOT))
        {
            Debug.LogWarning($"[LevelGenerator] Folder not found: {MATCH_ITEMS_ROOT}");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { MATCH_ITEMS_ROOT });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            MatchItem item = go.GetComponent<MatchItem>();
            if (item == null) continue;

            string dir = Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "";
            string group = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(group) || dir == MATCH_ITEMS_ROOT)
            {
                group = "Root";
            }

            _catalog.Add(new ItemEntry
            {
                Prefab = item,
                FolderGroup = group,
                SetCount = 0
            });
        }

        _catalog.Sort((a, b) =>
        {
            int groupCmp = string.Compare(a.FolderGroup, b.FolderGroup, StringComparison.Ordinal);
            if (groupCmp != 0) return groupCmp;
            return string.Compare(a.Prefab.name, b.Prefab.name, StringComparison.Ordinal);
        });
    }

    private void AutoPickRandomSets()
    {
        ClearAllSelections();
        if (_catalog.Count == 0) return;

        List<ItemEntry> pool = new(_catalog);
        ShuffleList(pool);

        int pickCount = Mathf.Min(_randomTypeCount, pool.Count);
        for (int i = 0; i < pickCount; i++)
        {
            pool[i].SetCount = _randomSetsPerType;
        }
    }

    private void ClearAllSelections()
    {
        for (int i = 0; i < _catalog.Count; i++)
        {
            _catalog[i].SetCount = 0;
        }
    }

    private void CalculateTotals(out int selectedTypes, out int totalSets, out int totalItems)
    {
        selectedTypes = 0;
        totalSets = 0;
        for (int i = 0; i < _catalog.Count; i++)
        {
            if (_catalog[i].SetCount > 0)
            {
                selectedTypes++;
                totalSets += _catalog[i].SetCount;
            }
        }
        totalItems = totalSets * 3;
    }

    private void LoadDefaultTemplate()
    {
        if (_templatePrefab == null)
        {
            _templatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DEFAULT_TEMPLATE_PATH);
        }
        SyncDockCapacityFromTemplate();
    }

    private void SyncDockCapacityFromTemplate()
    {
        if (_templatePrefab == null) return;

        DockManager dock = _templatePrefab.GetComponentInChildren<DockManager>(true);
        if (dock != null)
        {
            _dockCapacity = Mathf.Clamp(dock.Capacity, 3, 10);
        }
    }

    private void LoadFromExistingPrefab(GameObject levelPrefab)
    {
        if (levelPrefab == null) return;

        // 1. Parse level number if named "Level-X"
        string prefabName = levelPrefab.name;
        if (prefabName.StartsWith("Level-", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(prefabName.Substring(6), out int parsedLevel) &&
            parsedLevel > 0)
        {
            _levelNumber = parsedLevel;
        }

        // 2. Read timer settings from LevelManager
        LevelManager lm = levelPrefab.GetComponent<LevelManager>();
        if (lm != null)
        {
            SerializedObject lmSo = new SerializedObject(lm);
            SerializedProperty useTimerProp = lmSo.FindProperty("useTimer");
            SerializedProperty timeLimitProp = lmSo.FindProperty("timeLimit");
            if (useTimerProp != null) _useTimer = useTimerProp.boolValue;
            if (timeLimitProp != null) _timeLimit = Mathf.Max(10f, timeLimitProp.floatValue);
        }

        // 3. Read dock capacity
        SyncDockCapacityFromTemplate();

        // 4. Count existing MatchItems and map them back to catalog SetCounts
        ClearAllSelections();
        MatchItem[] existingItems = levelPrefab.GetComponentsInChildren<MatchItem>(true);
        Dictionary<int, int> countsById = new();

        for (int i = 0; i < existingItems.Length; i++)
        {
            if (existingItems[i] == null) continue;
            int id = existingItems[i].Id;
            countsById[id] = countsById.TryGetValue(id, out int c) ? c + 1 : 1;
        }

        for (int i = 0; i < _catalog.Count; i++)
        {
            ItemEntry entry = _catalog[i];
            if (entry.Prefab != null && countsById.TryGetValue(entry.Prefab.Id, out int count))
            {
                entry.SetCount = Mathf.Max(0, count / 3);
            }
        }
    }

    private void SuggestNextLevelNumber()
    {
        int next = 1;
        while (File.Exists($"{LEVELS_OUTPUT_DIR}/Level-{next}.prefab"))
        {
            next++;
        }
        _levelNumber = next;
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int rand = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[rand]) = (list[rand], list[i]);
        }
    }
}
#endif
