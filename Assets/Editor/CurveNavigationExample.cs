using UnityEditor;
using UnityEngine;

public sealed class CurveNavigationExample : ScriptableObject
{
    public AnimationCurve Curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [MenuItem("Tools/Trackpad Navigation/Open Curve Inspector")]
    public static void Open()
    {
        var example = CreateInstance<CurveNavigationExample>();
        example.name = "Curve Navigation Example";
        example.hideFlags = HideFlags.DontSave;
        Selection.activeObject = example;
        EditorApplication.ExecuteMenuItem("Window/General/Inspector");
    }
}
