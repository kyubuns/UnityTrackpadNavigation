using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrackpadNavigation
{
    internal interface INavigationTarget
    {
        EditorWindow Window
        {
            get;
        }
        string Description
        {
            get;
        }
        bool SupportsLook
        {
            get;
        }
        bool SupportsSmartZoom
        {
            get;
        }
        bool HitTest(Vector2 localPoint);
        void Apply(TrackpadEvent value, TrackpadPreferences settings);
    }

    internal static class NavigationHitTest
    {
        public static bool IsControl(VisualElement picked, VisualElement canvas)
        {
            for (var element = picked; element != null && element != canvas; element = element.parent)
            {
                if (element is ScrollView || element is TextElement
                    {
                        selection:
                        {
                            isSelectable: true
                        }
                    } || element is Slider || element is SliderInt || element is Button || element is Scroller || element is Toggle ||
                    element.ClassListContains("unity-base-field") || element.ClassListContains("unity-toolbar") ||
                    element.GetType().Name.Contains("Blackboard") || element.GetType().Name.Contains("Inspector") || element.GetType().Name.Contains("Overlay"))
                {
                    return true;
                }
            }
            return false;
        }

        public static VisualElement Pick(EditorWindow window, Vector2 localPoint)
        {
            var root = window.rootVisualElement;
            return root.panel?.Pick(root.LocalToWorld(localPoint));
        }
    }
}
