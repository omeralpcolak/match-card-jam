#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SoundManagerSetup
{
    private const string PREFAB_DIR = "Assets/_Game/Resources";
    private const string PREFAB_PATH = PREFAB_DIR + "/SoundManager.prefab";
    private const string SESSION_KEY = "SoundManagerSetup_Initialized_v1";

    static SoundManagerSetup()
    {
        EditorApplication.delayCall += CheckAndCreatePrefab;
    }

    private static void CheckAndCreatePrefab()
    {
        if (SessionState.GetBool(SESSION_KEY, false)) return;
        SessionState.SetBool(SESSION_KEY, true);

        // Only create if prefab doesn't already exist
        if (!File.Exists(PREFAB_PATH))
        {
            CreatePrefab();
        }
    }

    [MenuItem("Tools/Audio/Setup or Rebuild SoundManager Prefab")]
    public static void CreatePrefab()
    {
        if (!Directory.Exists(PREFAB_DIR))
        {
            Directory.CreateDirectory(PREFAB_DIR);
            AssetDatabase.Refresh();
        }

        AudioClip btnClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audios/Sounds/button_sound.mp3");
        AudioClip winClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audios/Sounds/level_complete_sound.mp3");
        AudioClip failClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audios/Sounds/level_fail_sound.mp3");

        GameObject go = new GameObject("SoundManager");
        SoundManager sm = go.AddComponent<SoundManager>();

        SerializedObject so = new SerializedObject(sm);
        SerializedProperty soundConfigsProp = so.FindProperty("soundConfigs");

        soundConfigsProp.ClearArray();

        // 1. ButtonClick
        AddSoundConfig(soundConfigsProp, SoundType.ButtonClick, btnClip, 1f, 0.98f, 1.02f);

        // 2. LevelComplete
        AddSoundConfig(soundConfigsProp, SoundType.LevelComplete, winClip, 1f, 1f, 1f);

        // 3. LevelFail
        AddSoundConfig(soundConfigsProp, SoundType.LevelFail, failClip, 1f, 1f, 1f);

        // 4. CardSelect (Fly to dock)
        AudioClip selectClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audios/Sounds/card_select_sound.wav");
        if (selectClip != null)
        {
            AddSoundConfig(soundConfigsProp, SoundType.CardSelect, selectClip, 1f, 0.98f, 1.02f);
        }

        // 5. CardMatch (Merge)
        AudioClip mergeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audios/Sounds/merge_sound.mp3");
        if (mergeClip != null)
        {
            AddSoundConfig(soundConfigsProp, SoundType.CardMatch, mergeClip, 1f, 0.98f, 1.02f);
        }

        // Music Configs
        SerializedProperty musicConfigsProp = so.FindProperty("musicConfigs");
        musicConfigsProp.ClearArray();

        AudioClip menuMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audios/Musics/main_menu_music.mp3");
        AudioClip gameMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Game/Audios/Musics/gameplay_music.mp3");

        if (menuMusic != null)
        {
            AddMusicConfig(musicConfigsProp, MusicType.MainMenu, menuMusic, 0.5f);
        }
        if (gameMusic != null)
        {
            AddMusicConfig(musicConfigsProp, MusicType.Gameplay, gameMusic, 0.45f);
        }

        so.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(go, PREFAB_PATH);
        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=#2ecc71><b>[SoundManagerSetup]</b> SoundManager prefab successfully created at: " + PREFAB_PATH + "</color>");
    }

    private static void AddSoundConfig(SerializedProperty arrayProp, SoundType type, AudioClip clip, float volume, float minPitch, float maxPitch)
    {
        int index = arrayProp.arraySize;
        arrayProp.InsertArrayElementAtIndex(index);
        SerializedProperty element = arrayProp.GetArrayElementAtIndex(index);

        SerializedProperty typeProp = element.FindPropertyRelative("soundType");
        SerializedProperty clipProp = element.FindPropertyRelative("clip");
        SerializedProperty volProp = element.FindPropertyRelative("volume");
        SerializedProperty minPitchProp = element.FindPropertyRelative("minPitch");
        SerializedProperty maxPitchProp = element.FindPropertyRelative("maxPitch");

        if (typeProp != null) typeProp.enumValueIndex = (int)type;
        if (clipProp != null) clipProp.objectReferenceValue = clip;
        if (volProp != null) volProp.floatValue = volume;
        if (minPitchProp != null) minPitchProp.floatValue = minPitch;
        if (maxPitchProp != null) maxPitchProp.floatValue = maxPitch;
    }

    private static void AddMusicConfig(SerializedProperty arrayProp, MusicType type, AudioClip clip, float volume)
    {
        int index = arrayProp.arraySize;
        arrayProp.InsertArrayElementAtIndex(index);
        SerializedProperty element = arrayProp.GetArrayElementAtIndex(index);

        SerializedProperty typeProp = element.FindPropertyRelative("musicType");
        SerializedProperty clipProp = element.FindPropertyRelative("clip");
        SerializedProperty volProp = element.FindPropertyRelative("volume");

        if (typeProp != null) typeProp.enumValueIndex = (int)type;
        if (clipProp != null) clipProp.objectReferenceValue = clip;
        if (volProp != null) volProp.floatValue = volume;
    }
}
#endif
