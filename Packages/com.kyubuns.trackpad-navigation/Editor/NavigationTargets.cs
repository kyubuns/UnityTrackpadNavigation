using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace TrackpadNavigation
{
    internal static class NavigationTargets
    {
        public static INavigationTarget Resolve(EditorWindow window, Vector2 localPoint, TrackpadPreferences settings)
        {
            if (!window)
            {
                return null;
            }

            if (window is SceneView scene)
            {
                return settings.SceneIntegration ? new SceneViewNavigation(scene) : null;
            }

            if (!settings.GraphIntegration)
            {
                return null;
            }

            var picked = NavigationHitTest.Pick(window, localPoint);
            for (var element = picked; element != null; element = element.parent)
            {
                if (TrackpadCanvas.Registrations.TryGetValue(element, out var canvas))
                {
                    return canvas.Target(window);
                }

                if (element is GraphView graph)
                {
                    return new GraphNavigation(window, graph);
                }
            }
            return ReflectedNavigation.TryCreate(window);
        }
    }
}
