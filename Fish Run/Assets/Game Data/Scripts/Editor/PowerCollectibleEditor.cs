using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PowerCollectible))]
public class PowerCollectibleEditor : Editor
{
    private SerializedProperty _type;
    private SerializedProperty _labelText;
    private SerializedProperty _effectSettings;

    private void OnEnable()
    {
        _type = serializedObject.FindProperty("Type");
        _labelText = serializedObject.FindProperty("LabelText");
        _effectSettings = serializedObject.FindProperty("EffectSettings");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_type);
        EditorGUILayout.PropertyField(_labelText);
        EditorGUILayout.PropertyField(_effectSettings.FindPropertyRelative("Duration"));

        CollectibleType collectibleType = (CollectibleType)_type.enumValueIndex;
        switch (collectibleType)
        {
            case CollectibleType.Speed:
                EditorGUILayout.PropertyField(_effectSettings.FindPropertyRelative("SpeedMultiplier"));
                EditorGUILayout.PropertyField(_effectSettings.FindPropertyRelative("SpeedTrailDuration"));
                EditorGUILayout.PropertyField(_effectSettings.FindPropertyRelative("SpeedTrailWidth"));
                EditorGUILayout.PropertyField(_effectSettings.FindPropertyRelative("SpeedTrailColor"));
                EditorGUILayout.PropertyField(_effectSettings.FindPropertyRelative("SpeedTrailMaterial"));
                break;

            case CollectibleType.Size:
                EditorGUILayout.PropertyField(_effectSettings.FindPropertyRelative("SizeMultiplier"));
                EditorGUILayout.PropertyField(_effectSettings.FindPropertyRelative("StoneMaterial"));
                EditorGUILayout.PropertyField(_effectSettings.FindPropertyRelative("ShieldScale"));
                EditorGUILayout.PropertyField(_effectSettings.FindPropertyRelative("ShieldMaterial"));
                break;

            case CollectibleType.SuperStrength:
                EditorGUILayout.PropertyField(_effectSettings.FindPropertyRelative("SuperStrengthPulseSpeed"));
                EditorGUILayout.PropertyField(_effectSettings.FindPropertyRelative("SuperStrengthEmissionStrength"));
                EditorGUILayout.PropertyField(_effectSettings.FindPropertyRelative("SuperStrengthPulseColor"));
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
