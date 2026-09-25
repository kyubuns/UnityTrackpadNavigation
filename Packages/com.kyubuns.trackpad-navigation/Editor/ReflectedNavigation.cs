using UnityEditor;

namespace TrackpadNavigation
{
    internal static class ReflectedNavigation
    {
        public static INavigationTarget TryCreate(EditorWindow window)
        {
            switch (window.GetType().FullName)
            {
                case "Unity.UI.Builder.Builder":
                    return BuilderNavigation.TryCreate(window);
                case "UnityEditor.GameView":
                    return GameViewNavigation.TryCreate(window);
                case "UnityEditor.ProfilerWindow":
                    return ProfilerNavigation.TryCreate(window);
                case "UnityEditor.U2D.Sprites.SpriteEditorWindow":
                    return SpriteNavigation.TryCreate(window);
                case "UnityEditor.Graphs.AnimatorControllerTool":
                    return AnimatorNavigation.TryCreate(window);
                case "UnityEditor.AnimationWindow":
                    return AnimationNavigation.TryCreate(window);
                case "UnityEditor.CurveEditorWindow":
                    return CurveNavigation.TryCreate(window);
                case "UnityEditor.Timeline.TimelineWindow":
                    return TimelineNavigation.TryCreate(window);
            }
            return null;
        }
    }
}
