using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EntityDataRegistry))]
public class EntityDataRegistryEditor : Editor
{
    SerializedProperty entriesProp;

    void OnEnable()
    {
        entriesProp = serializedObject.FindProperty("entries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(entriesProp, true);

        bool changed = false;
        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            var elem = entriesProp.GetArrayElementAtIndex(i);
            var dataSOProp = elem.FindPropertyRelative("dataSO");
            var idProp = elem.FindPropertyRelative("entityId");

            var so = dataSOProp.objectReferenceValue as BaseEntityDataSO;
            if (so != null && string.IsNullOrEmpty(idProp.stringValue))
            {
                idProp.stringValue = so.name;
                changed = true;
            }
        }

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        GUILayout.Space(6);
        if (GUILayout.Button("Refresh IDs"))
        {
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var elem = entriesProp.GetArrayElementAtIndex(i);
                var dataSOProp = elem.FindPropertyRelative("dataSO");
                var idProp = elem.FindPropertyRelative("entityId");
                var so = dataSOProp.objectReferenceValue as BaseEntityDataSO;
                if (so != null)
                    idProp.stringValue = so.name;
            }
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
