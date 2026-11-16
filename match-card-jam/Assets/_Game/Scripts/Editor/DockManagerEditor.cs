#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DockManager))]
public class DockManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 1. Draw all existing serialized fields normally
        DrawDefaultInspector();

        DockManager dock = (DockManager)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Slot Management", EditorStyles.boldLabel);

        // 2. Generate Slots button
        GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
        if (GUILayout.Button("⚡ Generate Slots", GUILayout.Height(32)))
        {
            dock.GenerateSlotsInEditor();
        }

        EditorGUILayout.Space(4);

        // 3. Clear Slots button
        GUI.backgroundColor = new Color(0.95f, 0.4f, 0.4f);
        if (GUILayout.Button("🗑️ Clear Slots", GUILayout.Height(26)))
        {
            if (EditorUtility.DisplayDialog("Clear Slots", "All slots under Dock will be deleted. Are you sure?", "Yes, Delete", "Cancel"))
            {
                dock.ClearSlotsInEditor();
            }
        }

        GUI.backgroundColor = Color.white;
    }
}
#endif
