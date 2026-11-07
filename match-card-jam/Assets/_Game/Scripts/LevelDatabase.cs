using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject database holding all level prefabs for the game.
/// Provides a static self-returning singleton instance (LevelDatabase.Instance / LevelDatabase.GetPrefab)
/// automatically loaded from the Resources folder, eliminating the need to drag references into the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "LevelDatabase", menuName = "Game/Level Database")]
public class LevelDatabase : ScriptableObject
{
    private const string RESOURCE_PATH = "LevelDatabase";

    private static LevelDatabase instance;

    /// <summary>
    /// Self-loading singleton instance loaded automatically from Resources/LevelDatabase.
    /// </summary>
    public static LevelDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<LevelDatabase>(RESOURCE_PATH);

                if (instance == null)
                {
                    // Fallback: search for any LevelDatabase in any Resources folder
                    LevelDatabase[] all = Resources.LoadAll<LevelDatabase>("");
                    if (all != null && all.Length > 0)
                    {
                        instance = all[0];
                    }
                }

                if (instance == null)
                {
                    Debug.LogError("[LevelDatabase] LevelDatabase asset not found in Resources! Please ensure your LevelDatabase asset is inside 'Assets/_Game/Resources/LevelDatabase'.");
                }
            }

            return instance;
        }
    }

    /// <summary>
    /// Static shortcut to fetch a level prefab directly from anywhere in the codebase.
    /// E.g.: LevelDatabase.GetPrefab(currentLevel);
    /// </summary>
    public static GameObject GetPrefab(int levelNumber)
    {
        return Instance != null ? Instance.GetLevelPrefab(levelNumber) : null;
    }

    [Header("Level Prefabs")]
    [Tooltip("Ordered list of level prefabs (Level-1, Level-2, Level-3, etc.).")]
    [SerializeField] private List<GameObject> levelPrefabs = new();

    [Header("Progression Settings")]
    [Tooltip("If true, loops back through levels once the player passes the last defined level.")]
    [SerializeField] private bool loopLevels = true;

    public int TotalLevels => levelPrefabs != null ? levelPrefabs.Count : 0;
    public IReadOnlyList<GameObject> LevelPrefabs => levelPrefabs;

    /// <summary>
    /// Retrieves the prefab for a given 1-based level number.
    /// Handles 1-based indexing, looping, and bounds safety.
    /// </summary>
    public GameObject GetLevelPrefab(int levelNumber)
    {
        if (levelPrefabs == null || levelPrefabs.Count == 0)
        {
            Debug.LogError("[LevelDatabase] No level prefabs assigned in LevelDatabase!");
            return null;
        }

        int targetIndex;

        if (loopLevels)
        {
            // 1-based wrap-around (e.g., Level 4 with 3 prefabs returns index 0 -> Level 1)
            targetIndex = (levelNumber - 1) % levelPrefabs.Count;
            if (targetIndex < 0) targetIndex += levelPrefabs.Count;
        }
        else
        {
            targetIndex = Mathf.Clamp(levelNumber - 1, 0, levelPrefabs.Count - 1);
        }

        GameObject selectedPrefab = levelPrefabs[targetIndex];

        if (selectedPrefab == null)
        {
            Debug.LogError($"[LevelDatabase] Level prefab at index {targetIndex} (Level {levelNumber}) is null!");
        }

        return selectedPrefab;
    }
}
