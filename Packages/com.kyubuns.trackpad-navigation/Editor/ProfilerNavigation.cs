using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrackpadNavigation
{
    internal sealed class ProfilerNavigation : ReflectedNavigation
    {
        readonly object module;
        readonly object timeline;
        readonly object area;
        readonly VisualElement canvas;
        readonly MethodInfo setTransform;
        public override string Description => "Profiler CPU Timeline — time pan / thread scroll / cursor zoom";

        ProfilerNavigation(EditorWindow window, object module, object timeline, object area, VisualElement canvas, MethodInfo setTransform) : base(window)
        {
            this.module = module;
            this.timeline = timeline;
            this.area = area;
            this.canvas = canvas;
            this.setTransform = setTransform;
        }

        public new static INavigationTarget TryCreate(EditorWindow window)
        {
            var module = EditorMember.Get(window, "selectedModule");
            if (module?.GetType().FullName != "UnityEditorInternal.Profiling.CPUProfilerModule" || EditorMember.Get(module, "ViewType")?.ToString() != "Timeline")
            {
                return null;
            }
            var timeline = EditorMember.Get(module, "m_TimelineGUI");
            var area = EditorMember.Get(timeline, "m_TimeArea");
            var controller = EditorMember.Get(module, "m_DetailsViewController");
            var canvas = EditorMember.Get(controller, "m_LegacyIMGUIView") as VisualElement;
            var setTransform = area?.GetType().GetMethod("SetTransform", new[]
            {
                typeof(Vector2), typeof(Vector2)
            });
            if (canvas == null || setTransform == null || !(EditorMember.Get(area, "drawRect") is Rect) ||
                !(EditorMember.Get(area, "translation") is Vector2) || !(EditorMember.Get(area, "scale") is Vector2))
            {
                return null;
            }
            return new ProfilerNavigation(window, module, timeline, area, canvas, setTransform);
        }

        bool IsCurrent => Window != null && canvas.panel != null && canvas.resolvedStyle.display != DisplayStyle.None &&
            ReferenceEquals(module, EditorMember.Get(Window, "selectedModule")) &&
            ReferenceEquals(area, EditorMember.Get(timeline, "m_TimeArea")) &&
            EditorMember.Get(module, "ViewType")?.ToString() == "Timeline" && Equals(EditorMember.Get(module, "fetchData"), true) &&
            string.IsNullOrEmpty(EditorMember.Get(timeline, "dataAvailabilityMessage") as string) &&
            ((ProfilerWindow)Window).selectedFrameIndex >= UnityEditorInternal.ProfilerDriver.firstFrameIndex &&
            UnityEditorInternal.ProfilerDriver.lastFrameIndex >= 0;

        Vector2 CanvasPoint(Vector2 windowPoint) => canvas.WorldToLocal(Window.rootVisualElement.LocalToWorld(windowPoint));

        public override bool HitTest(Vector2 localPoint)
        {
            if (!IsCurrent)
            {
                return false;
            }
            // Timelineの座標はウィンドウではなく詳細欄のIMGUIContainerを基準にする。
            var point = CanvasPoint(localPoint);
            var rect = (Rect)EditorMember.Get(area, "drawRect");
            return rect.width > 1 && rect.height > 1 && rect.Contains(point) && canvas.localBound.Contains(point);
        }

        public override void Apply(TrackpadEvent value, TrackpadPreferences settings)
        {
            if (!IsCurrent)
            {
                return;
            }
            var translation = (Vector2)EditorMember.Get(area, "translation");
            var scale = (Vector2)EditorMember.Get(area, "scale");
            if (!NavigationMath.IsFinite(scale) || !NavigationMath.IsFinite(translation) || scale.x <= 0 || scale.y <= 0)
            {
                return;
            }
            if (value.Kind == GestureKind.Scroll)
            {
                translation += NavigationMath.Pan(value, settings);
            }
            else if (value.Kind == GestureKind.Magnify)
            {
                float factor = NavigationMath.ZoomFactor(value, settings);
                var rect = (Rect)EditorMember.Get(area, "drawRect");
                var anchor = CanvasPoint(value.ScreenPosition - Window.position.position) - rect.position;
                translation.x = NavigationMath.ZoomTranslation(translation, anchor, factor).x;
                // 時間軸だけを拡大し、スレッドの高さと縦スクロール位置を保つ。
                scale.x *= factor;
            }
            else
            {
                return;
            }
            setTransform.Invoke(area, new object[]
            {
                translation, scale
            });
            Window.Repaint();
        }
    }
}
