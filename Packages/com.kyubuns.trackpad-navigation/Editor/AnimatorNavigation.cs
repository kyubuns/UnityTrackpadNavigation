using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrackpadNavigation
{
    internal sealed class AnimatorNavigation : NavigationTarget
    {
        readonly VisualElement graph;
        readonly object controller;
        public override string Description => "Animator — state machine / blend tree";
        AnimatorNavigation(EditorWindow window, VisualElement graph) : base(window)
        {
            this.graph = graph;
            controller = EditorMember.Get(window, "animatorController");
        }

        public static INavigationTarget TryCreate(EditorWindow window)
        {
            var graph = window.rootVisualElement.Q("GraphUI");
            if (graph == null || !EditorMember.Writable(window, "graphPosition", typeof(Vector3)) || !EditorMember.Writable(window, "graphScale", typeof(Vector3)))
            {
                return null;
            }

            if (EditorMember.Get(window, "animatorController") == null)
            {
                return null;
            }

            return new AnimatorNavigation(window, graph);
        }

        protected override bool IsCurrent => graph.panel != null && graph.parent != null && Window.rootVisualElement.Contains(graph) &&
            ReferenceEquals(controller, EditorMember.Get(Window, "animatorController"));

        protected override bool ContainsPoint(Vector2 localPoint)
        {
            var point = NavigationCoordinates.ToPanel(Window, localPoint);
            return graph.parent.worldBound.Contains(point) && !NavigationHitTest.IsControl(NavigationHitTest.Pick(Window, localPoint), graph.parent);
        }

        protected override void ApplyInput(TrackpadEvent value, TrackpadPreferences settings)
        {
            var position = (Vector3)EditorMember.Get(Window, "graphPosition");
            var scale = (Vector3)EditorMember.Get(Window, "graphScale");
            if (value.Kind == GestureKind.Scroll)
            {
                position += (Vector3)NavigationMath.Pan(value, settings);
            }
            else if (value.Kind == GestureKind.Magnify)
            {
                float next = Mathf.Clamp(scale.x * NavigationMath.ZoomFactor(value, settings), 0.1f, 3);
                var panelPoint = NavigationCoordinates.ToPanel(Window, value.ScreenPosition - Window.position.position);
                var anchor = graph.parent.WorldToLocal(panelPoint) - graph.layout.position;
                position = NavigationMath.ZoomTranslation(position, anchor, next / scale.x);
                scale = new Vector3(next, next, 1);
            }
            EditorMember.Set(Window, "graphPosition", position);
            EditorMember.Set(Window, "graphScale", scale);
            Window.Repaint();
        }
    }
}
