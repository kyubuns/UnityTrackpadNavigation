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
        bool IsAvailable
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

    internal abstract class NavigationTarget : INavigationTarget
    {
        public EditorWindow Window
        {
            get;
        }
        public abstract string Description
        {
            get;
        }
        public virtual bool SupportsLook => false;
        public virtual bool SupportsSmartZoom => false;
        public bool IsAvailable => Window != null && IsCurrent;
        protected abstract bool IsCurrent
        {
            get;
        }

        protected NavigationTarget(EditorWindow window) => Window = window;

        // 寿命とヒット判定を分離する。ポインタがUI上へ動いても、開始済み操作の入力先は保持する。
        public bool HitTest(Vector2 localPoint) => IsAvailable && ContainsPoint(localPoint);
        public void Apply(TrackpadEvent value, TrackpadPreferences settings)
        {
            // ネイティブのキューに残った入力を、閉じたウィンドウや切替前のビューへ適用しない。
            if (IsAvailable)
            {
                ApplyInput(value, settings);
            }
        }
        protected abstract bool ContainsPoint(Vector2 localPoint);
        protected abstract void ApplyInput(TrackpadEvent value, TrackpadPreferences settings);
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
            return root.panel?.Pick(NavigationCoordinates.ToPanel(window, localPoint));
        }
    }
}
