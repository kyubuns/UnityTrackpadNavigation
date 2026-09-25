using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrackpadNavigation
{
    internal sealed class ProfilerNavigation : ZoomAreaNavigation
    {
        readonly object module;
        readonly object timeline;
        readonly VisualElement canvas;
        public override string Description => "Profiler CPU Timeline — time pan / thread scroll / cursor zoom";

        ProfilerNavigation(EditorWindow window, object module, object timeline, object area, VisualElement canvas) : base(window, area)
        {
            this.module = module;
            this.timeline = timeline;
            this.canvas = canvas;
        }

        public static INavigationTarget TryCreate(EditorWindow window)
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
            return canvas != null && SupportsArea(area) ? new ProfilerNavigation(window, module, timeline, area, canvas) : null;
        }

        protected override bool IsCurrent => canvas.panel != null && canvas.resolvedStyle.display != DisplayStyle.None &&
            ReferenceEquals(module, EditorMember.Get(Window, "selectedModule")) &&
            ReferenceEquals(Area, EditorMember.Get(timeline, "m_TimeArea")) &&
            EditorMember.Get(module, "ViewType")?.ToString() == "Timeline" && Equals(EditorMember.Get(module, "fetchData"), true) &&
            string.IsNullOrEmpty(EditorMember.Get(timeline, "dataAvailabilityMessage") as string) &&
            ((ProfilerWindow)Window).selectedFrameIndex >= UnityEditorInternal.ProfilerDriver.firstFrameIndex &&
            UnityEditorInternal.ProfilerDriver.lastFrameIndex >= 0;

        // Timelineの座標は詳細欄のIMGUIContainerを基準にする。
        protected override Vector2 AreaPoint(Vector2 windowPoint) => NavigationCoordinates.ToLocal(Window, canvas, windowPoint);
        protected override bool ContainsPoint(Vector2 localPoint) => base.ContainsPoint(localPoint) && canvas.localBound.Contains(AreaPoint(localPoint));

        // 時間軸だけを拡大し、スレッドの高さと縦スクロール位置を保つ。
        protected override Vector2 ZoomScale(Vector2 scale, float factor) => new Vector2(scale.x * factor, scale.y);
    }
}
