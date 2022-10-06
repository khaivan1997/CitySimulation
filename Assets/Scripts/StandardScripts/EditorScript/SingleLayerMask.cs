using UnityEngine;
using UnityEditor;

[System.Serializable]
public class SingleLayerMask
{
    [SerializeField]
    private int layerIndex = 0;

    public void Set(int _layerIndex)
    {
        if (_layerIndex > 0 && _layerIndex < 32)
        {
            layerIndex = _layerIndex;
        }
    }
    public static implicit operator int (SingleLayerMask m) => m.layerIndex;
    public static implicit operator LayerMask(SingleLayerMask m) => 1 << m.layerIndex;
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(SingleLayerMask))]
public class SingleUnityLayerPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label)
    {
        EditorGUI.BeginProperty(_position, _label, _property);
        SerializedProperty layerIndex = _property.FindPropertyRelative("layerIndex");
        _position = EditorGUI.PrefixLabel(_position, GUIUtility.GetControlID(FocusType.Passive), _label);
        if (layerIndex != null)
        {
            layerIndex.intValue = EditorGUI.LayerField(_position, layerIndex.intValue);
        }
        EditorGUI.EndProperty();
    }
}
#endif