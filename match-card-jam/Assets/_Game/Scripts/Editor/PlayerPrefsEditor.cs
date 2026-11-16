using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Editor Window for inspecting, modifying, and testing PlayerPrefs values in the Unity Editor.
/// Provides shortcuts for core game data (CurrentLevel, PlayerCoins, Audio/Haptic settings)
/// as well as generic key-value management and global clearing.
/// </summary>
public class PlayerPrefsEditor : EditorWindow
{
    private const string PREFS_KEYS_LIST_KEY = "PlayerPrefsEditor_TrackedKeys";

    // Known game keys
    private const string KEY_CURRENT_LEVEL = "CurrentLevel";
    private const string KEY_PLAYER_COINS = "PlayerCoins";
    private const string KEY_PLAYER_LIVES = "PlayerLives";
    private const string KEY_SOUND = "Setting_Sound";
    private const string KEY_MUSIC = "Setting_Music";
    private const string KEY_HAPTIC = "Setting_Haptic";

    private enum KeyType
    {
        Int,
        Float,
        String
    }

    private Vector2 scrollPos;
    private List<string> trackedKeys = new();

    // New key input fields
    private string newKeyName = "";
    private KeyType newKeyType = KeyType.Int;
    private int newIntValue = 0;
    private float newFloatValue = 0f;
    private string newStringValue = "";

    // Search filter
    private string searchFilter = "";

    [MenuItem("Tools/PlayerPrefs Editor")]
    public static void ShowWindow()
    {
        PlayerPrefsEditor window = GetWindow<PlayerPrefsEditor>("PlayerPrefs Editor");
        window.minSize = new Vector2(380, 520);
        window.Show();
    }

    private void OnEnable()
    {
        LoadTrackedKeys();
        EnsureDefaultGameKeys();
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawHeader();
        EditorGUILayout.Space(8);

        DrawGameShortcutsSection();
        EditorGUILayout.Space(12);

        DrawPowerupsSection();
        EditorGUILayout.Space(12);

        DrawGenericKeysSection();
        EditorGUILayout.Space(12);

        DrawAddNewKeySection();
        EditorGUILayout.Space(16);

        DrawGlobalActionsSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("🎮 Match Card Jam - PlayerPrefs Editor", EditorStyles.boldLabel);
        GUILayout.Label("Inspect, test, and edit persistent saved game values in real-time.", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }

    #region Game Shortcuts

    private void DrawGameShortcutsSection()
    {
        GUILayout.Label("⚡ Core Game Shortcuts", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        // 1. Current Level
        int currentLevel = PlayerPrefs.GetInt(KEY_CURRENT_LEVEL, 1);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Current Level:", GUILayout.Width(110));
        int newLevel = EditorGUILayout.IntField(currentLevel, GUILayout.Width(60));
        if (newLevel != currentLevel)
        {
            PlayerPrefs.SetInt(KEY_CURRENT_LEVEL, Mathf.Max(1, newLevel));
            PlayerPrefs.Save();
        }

        if (GUILayout.Button("Level 1", EditorStyles.miniButtonLeft))
        {
            SetInt(KEY_CURRENT_LEVEL, 1);
        }
        if (GUILayout.Button("Level 5", EditorStyles.miniButtonMid))
        {
            SetInt(KEY_CURRENT_LEVEL, 5);
        }
        if (GUILayout.Button("Level 10", EditorStyles.miniButtonMid))
        {
            SetInt(KEY_CURRENT_LEVEL, 10);
        }
        if (GUILayout.Button("+1", EditorStyles.miniButtonRight))
        {
            SetInt(KEY_CURRENT_LEVEL, currentLevel + 1);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // 2. Player Coins
        int coins = GameData.Coins;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Player Coins:", GUILayout.Width(110));
        int newCoins = EditorGUILayout.IntField(coins, GUILayout.Width(60));
        if (newCoins != coins)
        {
            GameData.Coins = newCoins;
        }

        if (GUILayout.Button("Reset (0)", EditorStyles.miniButtonLeft))
        {
            GameData.Coins = 0;
        }
        if (GUILayout.Button("+100", EditorStyles.miniButtonMid))
        {
            GameData.AddCoins(100);
        }
        if (GUILayout.Button("+500", EditorStyles.miniButtonRight))
        {
            GameData.AddCoins(500);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // 3. Player Lives
        int lives = PlayerPrefs.GetInt(KEY_PLAYER_LIVES, 5);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Player Lives:", GUILayout.Width(110));
        int newLives = EditorGUILayout.IntField(lives, GUILayout.Width(60));
        if (newLives != lives)
        {
            if (Application.isPlaying) LifeManager.SetLives(newLives);
            else SetInt(KEY_PLAYER_LIVES, Mathf.Clamp(newLives, 0, 5));
        }

        if (GUILayout.Button("0", EditorStyles.miniButtonLeft))
        {
            if (Application.isPlaying) LifeManager.SetLives(0);
            else SetInt(KEY_PLAYER_LIVES, 0);
        }
        if (GUILayout.Button("1", EditorStyles.miniButtonMid))
        {
            if (Application.isPlaying) LifeManager.SetLives(1);
            else SetInt(KEY_PLAYER_LIVES, 1);
        }
        if (GUILayout.Button("5 (Full)", EditorStyles.miniButtonRight))
        {
            if (Application.isPlaying) LifeManager.SetLives(5);
            else SetInt(KEY_PLAYER_LIVES, 5);
        }
        EditorGUILayout.EndHorizontal();

        if (lives < 5)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Regen Test:", GUILayout.Width(110));
            if (GUILayout.Button("⏩ Simulate +15m (+1 Life)", EditorStyles.miniButton))
            {
                if (Application.isPlaying)
                {
                    LifeManager.SetLives(lives + 1);
                }
                else
                {
                    SetInt(KEY_PLAYER_LIVES, Mathf.Min(5, lives + 1));
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4);

        // 4. Audio & Haptic Settings
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Settings:", GUILayout.Width(110));

        bool sound = PlayerPrefs.GetInt(KEY_SOUND, 1) == 1;
        bool newSound = GUILayout.Toggle(sound, " Sound", "Button");
        if (newSound != sound) SetInt(KEY_SOUND, newSound ? 1 : 0);

        bool music = PlayerPrefs.GetInt(KEY_MUSIC, 1) == 1;
        bool newMusic = GUILayout.Toggle(music, " Music", "Button");
        if (newMusic != music) SetInt(KEY_MUSIC, newMusic ? 1 : 0);

        bool haptic = PlayerPrefs.GetInt(KEY_HAPTIC, 1) == 1;
        bool newHaptic = GUILayout.Toggle(haptic, " Haptic", "Button");
        if (newHaptic != haptic) SetInt(KEY_HAPTIC, newHaptic ? 1 : 0);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawPowerupsSection()
    {
        GUILayout.Label("✨ Powerups", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        foreach (PowerupType type in Enum.GetValues(typeof(PowerupType)))
        {
            int count = GameData.GetPowerupCount(type);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(type.ToString(), GUILayout.Width(110));

            int newCount = EditorGUILayout.IntField(count, GUILayout.Width(60));
            if (newCount != count)
            {
                GameData.SetPowerupCount(type, newCount);
            }

            if (GUILayout.Button("Reset (0)", EditorStyles.miniButtonLeft))
            {
                GameData.SetPowerupCount(type, 0);
            }
            if (GUILayout.Button("+1", EditorStyles.miniButtonMid))
            {
                GameData.AddPowerup(type, 1);
            }
            if (GUILayout.Button("+5", EditorStyles.miniButtonRight))
            {
                GameData.AddPowerup(type, 5);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    #endregion

    #region Generic Keys Explorer

    private void DrawGenericKeysSection()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("🔑 Tracked Keys Explorer", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(140));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginVertical("box");

        if (trackedKeys.Count == 0)
        {
            EditorGUILayout.HelpBox("No keys currently tracked. Add keys below or use game shortcuts.", MessageType.Info);
        }
        else
        {
            string keyToDelete = null;

            for (int i = 0; i < trackedKeys.Count; i++)
            {
                string key = trackedKeys[i];

                if (!string.IsNullOrEmpty(searchFilter) && !key.ToLower().Contains(searchFilter.ToLower()))
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(key, GUILayout.Width(140));

                // Determine if key is stored or missing
                if (!PlayerPrefs.HasKey(key))
                {
                    EditorGUILayout.LabelField("(Not set / default)", EditorStyles.miniLabel);
                }
                else
                {
                    // Draw value field based on stored type heuristics
                    DrawValueField(key);
                }

                // Delete single key button
                GUI.color = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                {
                    keyToDelete = key;
                }
                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(keyToDelete))
            {
                PlayerPrefs.DeleteKey(keyToDelete);
                PlayerPrefs.Save();
                trackedKeys.Remove(keyToDelete);
                SaveTrackedKeys();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawValueField(string key)
    {
        // Try int first, if has key
        string strVal = PlayerPrefs.GetString(key, "__NOT_STRING__");
        if (strVal != "__NOT_STRING__")
        {
            string newStr = EditorGUILayout.TextField(strVal);
            if (newStr != strVal)
            {
                PlayerPrefs.SetString(key, newStr);
                PlayerPrefs.Save();
            }
            return;
        }

        float floatVal = PlayerPrefs.GetFloat(key, float.NaN);
        int intVal = PlayerPrefs.GetInt(key, int.MinValue);

        // Disambiguate float vs int
        if (!float.IsNaN(floatVal) && floatVal % 1 != 0)
        {
            float newF = EditorGUILayout.FloatField(floatVal);
            if (Math.Abs(newF - floatVal) > 0.0001f)
            {
                PlayerPrefs.SetFloat(key, newF);
                PlayerPrefs.Save();
            }
        }
        else if (intVal != int.MinValue)
        {
            int newI = EditorGUILayout.IntField(intVal);
            if (newI != intVal)
            {
                PlayerPrefs.SetInt(key, newI);
                PlayerPrefs.Save();
            }
        }
    }

    #endregion

    #region Add New Key

    private void DrawAddNewKeySection()
    {
        GUILayout.Label("➕ Add / Override Key", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        newKeyName = EditorGUILayout.TextField("Key Name:", newKeyName);
        newKeyType = (KeyType)EditorGUILayout.EnumPopup(newKeyType, GUILayout.Width(70));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        switch (newKeyType)
        {
            case KeyType.Int:
                newIntValue = EditorGUILayout.IntField("Value:", newIntValue);
                break;
            case KeyType.Float:
                newFloatValue = EditorGUILayout.FloatField("Value:", newFloatValue);
                break;
            case KeyType.String:
                newStringValue = EditorGUILayout.TextField("Value:", newStringValue);
                break;
        }

        GUI.color = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("Save Key", GUILayout.Width(80)))
        {
            if (!string.IsNullOrEmpty(newKeyName.Trim()))
            {
                string trimmedKey = newKeyName.Trim();
                switch (newKeyType)
                {
                    case KeyType.Int:
                        PlayerPrefs.SetInt(trimmedKey, newIntValue);
                        break;
                    case KeyType.Float:
                        PlayerPrefs.SetFloat(trimmedKey, newFloatValue);
                        break;
                    case KeyType.String:
                        PlayerPrefs.SetString(trimmedKey, newStringValue);
                        break;
                }
                PlayerPrefs.Save();

                if (!trackedKeys.Contains(trimmedKey))
                {
                    trackedKeys.Add(trimmedKey);
                    SaveTrackedKeys();
                }

                newKeyName = "";
            }
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    #endregion

    #region Global Actions

    private void DrawGlobalActionsSection()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("💾 Save All (PlayerPrefs.Save)", GUILayout.Height(28)))
        {
            PlayerPrefs.Save();
            Debug.Log("<color=#2ecc71><b>[PlayerPrefsEditor]</b> PlayerPrefs successfully saved to disk.</color>");
        }

        GUI.color = new Color(1f, 0.35f, 0.35f);
        if (GUILayout.Button("🗑️ Delete All PlayerPrefs", GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog(
                "Delete All PlayerPrefs?",
                "Are you sure you want to delete ALL PlayerPrefs? This action cannot be undone.",
                "Yes, Delete All",
                "Cancel"))
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                Debug.LogWarning("[PlayerPrefsEditor] ALL PlayerPrefs deleted!");
            }
        }
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Helper Methods

    private void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
        if (!trackedKeys.Contains(key))
        {
            trackedKeys.Add(key);
            SaveTrackedKeys();
        }
    }

    private void EnsureDefaultGameKeys()
    {
        AddKeyIfMissing(KEY_CURRENT_LEVEL);
        AddKeyIfMissing(KEY_PLAYER_COINS);
        AddKeyIfMissing(KEY_PLAYER_LIVES);
        AddKeyIfMissing(KEY_SOUND);
        AddKeyIfMissing(KEY_MUSIC);
        AddKeyIfMissing(KEY_HAPTIC);

        foreach (PowerupType type in Enum.GetValues(typeof(PowerupType)))
        {
            AddKeyIfMissing($"{GameData.KEY_POWERUP_PREFIX}{type}");
        }
    }

    private void AddKeyIfMissing(string key)
    {
        if (!trackedKeys.Contains(key))
        {
            trackedKeys.Add(key);
        }
    }

    private void LoadTrackedKeys()
    {
        trackedKeys.Clear();
        string raw = EditorPrefs.GetString(PREFS_KEYS_LIST_KEY, "");
        if (!string.IsNullOrEmpty(raw))
        {
            string[] split = raw.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in split)
            {
                if (!trackedKeys.Contains(s))
                {
                    trackedKeys.Add(s);
                }
            }
        }
    }

    private void SaveTrackedKeys()
    {
        string raw = string.Join(";", trackedKeys);
        EditorPrefs.SetString(PREFS_KEYS_LIST_KEY, raw);
    }

    #endregion
}
