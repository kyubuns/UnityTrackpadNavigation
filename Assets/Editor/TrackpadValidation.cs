#if UNITY_EDITOR_OSX
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine.Timeline;

public static class TrackpadValidation
{
    public const string Folder = "Assets/TrackpadExamples";

    [MenuItem("Tools/Trackpad Navigation/Open Scene")]
    public static void OpenScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }
        EditorSceneManager.OpenScene(Folder + "/Navigation.unity");
        SceneView.GetWindow<SceneView>().Show();
    }
    [MenuItem("Tools/Trackpad Navigation/Open Animator")]
    public static void OpenAnimator() => OpenAsset(".controller");
    [MenuItem("Tools/Trackpad Navigation/Open Animation")]
    public static void OpenAnimation()
    {
        var clip = AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>(Folder + "/Navigation.anim");
        Selection.activeObject = clip;
        var window = EditorWindow.GetWindow<AnimationWindow>();
        window.animationClip = clip;
        window.Show();
    }
    [MenuItem("Tools/Trackpad Navigation/Open Timeline")]
    public static void OpenTimeline() => TimelineEditor.GetOrCreateWindow().SetTimeline(AssetDatabase.LoadAssetAtPath<TimelineAsset>(Folder + "/Navigation.playable"));
    [MenuItem("Tools/Trackpad Navigation/Open Shader Graph")]
    public static void OpenShader() => OpenAsset(".shadergraph");
    [MenuItem("Tools/Trackpad Navigation/Open VFX Graph")]
    public static void OpenVfx() => OpenAsset(".vfx");
    [MenuItem("Tools/Trackpad Navigation/Open Sprite Editor")]
    public static void OpenSprite()
    {
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(Folder + "/Navigation.png");
        EditorApplication.ExecuteMenuItem("Window/2D/Sprite Editor");
    }
    [MenuItem("Tools/Trackpad Navigation/Open UI Builder")]
    public static void OpenBuilder() => OpenAsset(".uxml");

    static void OpenAsset(string extension) => AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(Folder + "/Navigation" + extension));
}
#endif
