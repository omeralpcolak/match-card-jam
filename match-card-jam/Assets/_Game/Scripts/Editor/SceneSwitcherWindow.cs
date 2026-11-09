#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitcherWindow : EditorWindow
{
    private class SceneEntry
    {
        public string Name;
        public string Path;
        public bool Enabled;
        public bool InBuild;
    }

    private Vector2 scrollPos;
    private string searchFilter = "";
    private bool showOtherScenes = false;

    private GUIStyle activeSceneBoxStyle;
    private GUIStyle normalSceneBoxStyle;
    private GUIStyle badgeStyle;

    [MenuItem("Tools/Scene Switcher...", false, 20)]
    [MenuItem("Window/Scene Switcher")]
    public static void ShowWindow()
    {
        var window = GetWindow<SceneSwitcherWindow>("Scene Switcher");
        window.minSize = new Vector2(300, 240);
        window.Show();
    }

    private void OnEnable()
    {
        EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
    }

    private void OnDisable()
    {
        EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
    }

    private void OnSceneChanged(Scene current, Scene next)
    {
        Repaint();
    }

    private void OnGUI()
    {
        InitStyles();

        // 1. Toolbar & Search
        DrawHeader();

        EditorGUILayout.Space(6f);

        // 2. Collect Build Profile / Settings Scenes
        string profileTitle;
        List<SceneEntry> buildScenes = GetBuildProfileScenes(out profileTitle);
        HashSet<string> buildPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in buildScenes)
        {
            buildPaths.Add(s.Path);
        }

        // 3. Collect Other Project Scenes
        List<SceneEntry> otherScenes = GetOtherProjectScenes(buildPaths);

        string currentActivePath = EditorSceneManager.GetActiveScene().path;

        // 4. Scroll List
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // --- Build Scenes Section ---
        EditorGUILayout.LabelField($"{profileTitle} ({buildScenes.Count} Scenes)", EditorStyles.boldLabel);

        if (buildScenes.Count == 0)
        {
            EditorGUILayout.HelpBox("No scenes configured in Build Settings or Build Profile.", MessageType.Info);
        }
        else
        {
            foreach (var scene in buildScenes)
            {
                if (PassesFilter(scene))
                {
                    DrawSceneRow(scene, currentActivePath);
                }
            }
        }

        EditorGUILayout.Space(12f);

        // --- Other Project Scenes Section (Collapsible) ---
        if (otherScenes.Count > 0)
        {
            showOtherScenes = EditorGUILayout.Foldout(showOtherScenes, $"Other Project Scenes ({otherScenes.Count})", true, EditorStyles.foldoutHeader);
            if (showOtherScenes)
            {
                EditorGUI.indentLevel++;
                foreach (var scene in otherScenes)
                {
                    if (PassesFilter(scene))
                    {
                        DrawSceneRow(scene, currentActivePath);
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        EditorGUILayout.EndScrollView();

        // 5. Footer / Quick Actions
        DrawFooter();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
        if (!string.IsNullOrEmpty(searchFilter))
        {
            if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSearchCancelButton") ?? GUI.skin.button, GUILayout.Width(18)))
            {
                searchFilter = "";
                GUI.FocusControl(null);
            }
        }

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(56)))
        {
            Repaint();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSceneRow(SceneEntry scene, string activePath)
    {
        bool isActive = string.Equals(scene.Path, activePath, StringComparison.OrdinalIgnoreCase);

        GUIStyle boxStyle = isActive ? activeSceneBoxStyle : normalSceneBoxStyle;
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.BeginHorizontal();

        // Active indicator dot
        if (isActive)
        {
            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
            GUILayout.Label("●", GUILayout.Width(14));
            GUI.backgroundColor = Color.white;
        }
        else
        {
            GUILayout.Space(14);
        }

        // Scene Name & Path
        GUIContent nameContent = new GUIContent(
            scene.Name,
            scene.Path + (scene.InBuild ? "" : " (Not in build list)")
        );

        GUIStyle labelStyle = isActive ? EditorStyles.boldLabel : EditorStyles.label;
        if (GUILayout.Button(nameContent, labelStyle, GUILayout.Height(24), GUILayout.ExpandWidth(true)))
        {
            OpenScene(scene.Path, false);
        }

        // Ping (Highlight in Project) button
        GUIContent pingContent = EditorGUIUtility.IconContent("d_ViewToolZoom") ?? new GUIContent("🔍");
        pingContent.tooltip = "Ping scene asset in Project window";
        if (GUILayout.Button(pingContent, EditorStyles.miniButtonLeft, GUILayout.Width(28), GUILayout.Height(22)))
        {
            PingScene(scene.Path);
        }

        // Play Button (Open scene and enter Play Mode)
        GUIContent playContent = EditorGUIUtility.IconContent("d_PlayButton") ?? new GUIContent("▶");
        playContent.tooltip = "Open scene and enter Play Mode";
        if (GUILayout.Button(playContent, EditorStyles.miniButtonMid, GUILayout.Width(28), GUILayout.Height(22)))
        {
            OpenSceneAndPlay(scene.Path);
        }

        // Open Button
        GUI.backgroundColor = isActive ? new Color(0.6f, 0.9f, 0.6f) : Color.white;
        if (GUILayout.Button(isActive ? "Active" : "Open", EditorStyles.miniButtonRight, GUILayout.Width(50), GUILayout.Height(22)))
        {
            if (!isActive)
            {
                OpenScene(scene.Path, false);
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2f);
    }

    private void DrawFooter()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        if (GUILayout.Button("Build Settings...", EditorStyles.toolbarButton))
        {
            EditorWindow.GetWindow(Type.GetType("UnityEditor.BuildPlayerWindow, UnityEditor"));
        }

        GUILayout.FlexibleSpace();
        
        string sceneCountText = $"{EditorSceneManager.sceneCountInBuildSettings} Scenes in Build";
        EditorGUILayout.LabelField(sceneCountText, EditorStyles.miniLabel, GUILayout.Width(130));

        EditorGUILayout.EndHorizontal();
    }

    private void OpenScene(string path, bool playAfter)
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (playAfter)
            {
                EditorApplication.isPlaying = true;
            }
        }
    }

    private void OpenSceneAndPlay(string path)
    {
        OpenScene(path, true);
    }

    private void PingScene(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        if (asset != null)
        {
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }
    }

    private bool PassesFilter(SceneEntry entry)
    {
        if (string.IsNullOrWhiteSpace(searchFilter)) return true;
        return entry.Name.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
               entry.Path.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private List<SceneEntry> GetBuildProfileScenes(out string sourceTitle)
    {
        var result = new List<SceneEntry>();
        sourceTitle = "Build Settings Scenes";

        // Unity 6 BuildProfile Reflection Support
        try
        {
            Type profileType = Type.GetType("UnityEditor.Build.Profile.BuildProfile, UnityEditor");
            if (profileType != null)
            {
                MethodInfo getActiveMethod = profileType.GetMethod("GetActiveBuildProfile", BindingFlags.Public | BindingFlags.Static);
                object activeProfile = getActiveMethod?.Invoke(null, null);
                if (activeProfile != null)
                {
                    PropertyInfo nameProp = profileType.GetProperty("name");
                    string profileName = nameProp?.GetValue(activeProfile) as string ?? "Active Profile";
                    sourceTitle = $"Build Profile ({profileName})";

                    PropertyInfo scenesProp = profileType.GetProperty("scenes");
                    if (scenesProp != null)
                    {
                        IEnumerable scenesEnum = scenesProp.GetValue(activeProfile) as IEnumerable;
                        if (scenesEnum != null)
                        {
                            foreach (object sc in scenesEnum)
                            {
                                Type scType = sc.GetType();
                                bool enabled = (bool)(scType.GetProperty("enabled")?.GetValue(sc) ?? true);
                                string path = (string)(scType.GetProperty("path")?.GetValue(sc) ?? "");
                                if (!string.IsNullOrEmpty(path))
                                {
                                    result.Add(new SceneEntry
                                    {
                                        Path = path,
                                        Enabled = enabled,
                                        InBuild = true,
                                        Name = Path.GetFileNameWithoutExtension(path)
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback to EditorBuildSettings below
        }

        // Standard EditorBuildSettings.scenes Fallback
        if (result.Count == 0)
        {
            sourceTitle = "Build Settings Scenes";
            foreach (var bss in EditorBuildSettings.scenes)
            {
                if (string.IsNullOrEmpty(bss.path)) continue;
                result.Add(new SceneEntry
                {
                    Path = bss.path,
                    Enabled = bss.enabled,
                    InBuild = true,
                    Name = Path.GetFileNameWithoutExtension(bss.path)
                });
            }
        }

        return result;
    }

    private List<SceneEntry> GetOtherProjectScenes(HashSet<string> buildPaths)
    {
        var otherList = new List<SceneEntry>();
        string[] allGuids = AssetDatabase.FindAssets("t:SceneAsset");
        foreach (string guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;
            if (path.StartsWith("Packages/")) continue; // Exclude internal package scenes

            if (!buildPaths.Contains(path))
            {
                otherList.Add(new SceneEntry
                {
                    Path = path,
                    Enabled = false,
                    InBuild = false,
                    Name = Path.GetFileNameWithoutExtension(path)
                });
            }
        }

        otherList.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return otherList;
    }

    private void InitStyles()
    {
        if (activeSceneBoxStyle == null)
        {
            activeSceneBoxStyle = new GUIStyle(EditorStyles.helpBox);
            activeSceneBoxStyle.padding = new RectOffset(6, 6, 4, 4);
            activeSceneBoxStyle.margin = new RectOffset(4, 4, 2, 2);
        }

        if (normalSceneBoxStyle == null)
        {
            normalSceneBoxStyle = new GUIStyle(EditorStyles.helpBox);
            normalSceneBoxStyle.padding = new RectOffset(6, 6, 4, 4);
            normalSceneBoxStyle.margin = new RectOffset(4, 4, 2, 2);
        }

        if (badgeStyle == null)
        {
            badgeStyle = new GUIStyle(EditorStyles.miniLabel);
            badgeStyle.normal.textColor = new Color(0.25f, 0.85f, 0.45f);
            badgeStyle.fontStyle = FontStyle.Bold;
        }
    }
}
#endif
