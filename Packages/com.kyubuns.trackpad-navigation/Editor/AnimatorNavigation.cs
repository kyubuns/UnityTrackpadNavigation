using System.Collections;
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
        public override bool SupportsSmartZoom => EditorMember.Method(Window, "FrameSelection") != null &&
            EditorMember.Method(EditorMember.Get(Window, "activeGraphGUI"), "UpdateUnitySelection") != null;

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
            if (value.Kind == GestureKind.SmartZoom)
            {
                FrameAtPointer(value.ScreenPosition);
                return;
            }

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

        void FrameAtPointer(Vector2 screenPoint)
        {
            var graphGUI = EditorMember.Get(Window, "activeGraphGUI");
            var nodes = EditorMember.Get(EditorMember.Get(graphGUI, "graph"), "nodes") as IList;
            var selection = EditorMember.Get(graphGUI, "selection") as IList;
            var clear = EditorMember.Method(graphGUI, "ClearSelection");
            var update = EditorMember.Method(graphGUI, "UpdateUnitySelection");
            if (nodes == null || selection == null || clear == null || update == null)
            {
                return;
            }

            var panelPoint = NavigationCoordinates.ToPanel(Window, screenPoint - Window.position.position);
            var point = graph.WorldToLocal(panelPoint);
            // GraphUIの変換でパン・ズームを戻し、描画順の手前からノードを拾う。
            for (int i = nodes.Count - 1; i >= 0; --i)
            {
                var node = nodes[i];
                if (!(EditorMember.Get(node, "position") is Rect bounds) || !bounds.Contains(point))
                {
                    continue;
                }

                clear.Invoke(graphGUI, null);
                selection.Add(node);
                update.Invoke(graphGUI, null);
                Window.Focus();
                EditorMember.Method(Window, "FrameSelection").Invoke(Window, null);
                Window.Repaint();
                return;
            }
        }
    }
}
