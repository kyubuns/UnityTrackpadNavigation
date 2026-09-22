using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrackpadNavigation
{
    internal sealed class GraphNavigation : INavigationTarget
    {
        public EditorWindow Window
        {
            get;
        }
        readonly GraphView graph;
        Vector2 remainder;
        Vector2 lastApplied;
        public string Description => $"{Window.titleContent.text} — GraphView";
        public bool SupportsLook => false;
        public bool SupportsSmartZoom => true;

        public GraphNavigation(EditorWindow window, GraphView graph)
        {
            Window = window;
            this.graph = graph;
        }

        public bool HitTest(Vector2 localPoint)
        {
            if (graph.panel == null)
            {
                return false;
            }

            var point = Window.rootVisualElement.LocalToWorld(localPoint);
            if (!graph.worldBound.Contains(point))
            {
                return false;
            }

            var picked = graph.panel.Pick(point);
            if (picked == null || (picked != graph && !graph.Contains(picked)))
            {
                return false;
            }
            // ノード内の入力欄でも2本指はキャンバス操作。クリックやキー入力には触れない。
            return picked is Node || picked.GetFirstAncestorOfType<Node>() != null || !NavigationHitTest.IsControl(picked, graph);
        }

        public void Apply(TrackpadEvent value, TrackpadPreferences settings)
        {
            var content = graph.contentViewContainer;
            var translate = content.style.translate.value;
            var current = new Vector2(translate.x.value, translate.y.value);
            if (current != lastApplied)
            {
                remainder = Vector2.zero;
            }

            var position = (Vector3)(current + remainder);
            var scale = content.style.scale.value.value;
            if (scale.x <= 0)
            {
                scale = Vector3.one;
            }

            if (value.Kind == GestureKind.SmartZoom)
            {
                var picked = NavigationHitTest.Pick(Window, value.ScreenPosition - Window.position.position);
                var node = picked as Node ?? picked?.GetFirstAncestorOfType<Node>();
                if (node == null || !graph.Contains(node))
                {
                    return;
                }

                var bounds = graph.contentViewContainer.WorldToLocal(node.worldBound);
                GraphView.CalculateFrameTransform(bounds, graph.layout, 30, out position, out scale);
            }
            else if (value.Kind == GestureKind.Scroll)
            {
                position += (Vector3)NavigationMath.Pan(value, settings);
            }
            else if (value.Kind == GestureKind.Magnify)
            {
                float minimum = graph.minScale > 0 ? graph.minScale : 0.05f;
                float maximum = graph.maxScale > minimum ? graph.maxScale : 8f;
                float next = Mathf.Clamp(scale.x * NavigationMath.ZoomFactor(value, settings), minimum, maximum);
                var panelPoint = Window.rootVisualElement.LocalToWorld(value.ScreenPosition - Window.position.position);
                var anchor = graph.contentViewContainer.parent.WorldToLocal(panelPoint) - graph.contentViewContainer.layout.position;
                position = NavigationMath.ZoomTranslation(position, anchor, next / scale.x);
                scale = new Vector3(next, next, 1);
            }
            graph.UpdateViewTransform(position, scale);
            var applied = content.style.translate.value;
            lastApplied = new Vector2(applied.x.value, applied.y.value);
            // Shader Graphは位置がゼロになると保存済みのビューを復元するため、その分岐を避ける。
            if (((Vector2)position).sqrMagnitude < 1 && Vector2.Distance(lastApplied, position) > 2)
            {
                graph.UpdateViewTransform(new Vector3(1, position.y, 0), scale);
                applied = content.style.translate.value;
                lastApplied = new Vector2(applied.x.value, applied.y.value);
            }
            // GraphViewのピクセル丸めで細かな移動が消えないよう、端数を次の入力へ持ち越す。
            remainder = (Vector2)position - lastApplied;
            Window.Repaint();
        }
    }

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
