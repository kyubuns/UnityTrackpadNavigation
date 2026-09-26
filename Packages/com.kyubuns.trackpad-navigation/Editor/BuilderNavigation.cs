using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrackpadNavigation
{
    internal sealed class BuilderNavigation : NavigationTarget
    {
        readonly VisualElement owner;
        readonly VisualElement viewport;
        public override string Description => "UI Builder — canvas";
        public override bool SupportsSmartZoom => EditorMember.Method(owner, "PickElement", typeof(Vector2), typeof(List<VisualElement>)) != null &&
            EditorMember.Method(owner, "FitViewport") != null;

        BuilderNavigation(EditorWindow window, VisualElement owner, VisualElement viewport) : base(window)
        {
            this.owner = owner;
            this.viewport = viewport;
        }

        public static INavigationTarget TryCreate(EditorWindow window)
        {
            if (window.GetType().FullName != "Unity.UI.Builder.Builder")
            {
                return null;
            }

            var owner = window.rootVisualElement.Query<VisualElement>().Where(element => element.GetType().FullName == "Unity.UI.Builder.BuilderViewport").First();
            var viewport = owner?.Q("viewport");
            if (viewport == null || !EditorMember.Writable(owner, "zoomScale", typeof(float)) || !EditorMember.Writable(owner, "contentOffset", typeof(Vector2)))
            {
                return null;
            }

            return new BuilderNavigation(window, owner, viewport);
        }

        protected override bool IsCurrent => viewport.panel != null && Window.rootVisualElement.Contains(viewport) &&
            !Equals(EditorMember.Get(owner, "isPreviewEnabled"), true);

        protected override bool ContainsPoint(Vector2 localPoint)
        {
            var point = NavigationCoordinates.ToPanel(Window, localPoint);
            var picked = viewport.panel.Pick(point);
            if (picked == null || (picked != viewport && !viewport.Contains(picked)))
            {
                return false;
            }

            for (var element = picked; element != null && element != viewport; element = element.parent)
            {
                if (element is TextField)
                {
                    return false;
                }
            }

            return viewport.worldBound.Contains(point);
        }

        protected override void ApplyInput(TrackpadEvent value, TrackpadPreferences settings)
        {
            if (value.Kind == GestureKind.SmartZoom)
            {
                var point = NavigationCoordinates.ToPanel(Window, value.ScreenPosition - Window.position.position);
                var picked = EditorMember.Method(owner, "PickElement", typeof(Vector2), typeof(List<VisualElement>))
                    .Invoke(owner, new object[] { point, null }) as VisualElement;
                if (picked == null)
                {
                    return;
                }

                var selection = EditorMember.Get(owner, "selection");
                var notifier = owner.GetType().Assembly.GetType("Unity.UI.Builder.IBuilderSelectionNotifier");
                var select = notifier == null ? null : EditorMember.Method(selection, "Select", notifier, typeof(VisualElement));
                if (select == null)
                {
                    return;
                }

                select.Invoke(selection, new object[] { owner, picked });
                EditorMember.Method(owner, "SetInnerSelection", typeof(VisualElement))?.Invoke(owner, new object[] { picked });
                Window.Focus();
                EditorMember.Method(owner, "FitViewport").Invoke(owner, null);
                Window.Repaint();
                return;
            }

            var position = (Vector2)EditorMember.Get(owner, "contentOffset");
            float scale = (float)EditorMember.Get(owner, "zoomScale");
            if (value.Kind == GestureKind.Scroll)
            {
                position += NavigationMath.Pan(value, settings);
            }
            else if (value.Kind == GestureKind.Magnify)
            {
                float next = Mathf.Clamp(scale * NavigationMath.ZoomFactor(value, settings), 0.1f, 4);
                var panelPoint = NavigationCoordinates.ToPanel(Window, value.ScreenPosition - Window.position.position);
                position = NavigationMath.ZoomTranslation(position, viewport.WorldToLocal(panelPoint), next / scale);
                EditorMember.Set(owner, "zoomScale", next);
            }
            EditorMember.Set(owner, "contentOffset", position);
            Window.Repaint();
        }
    }
}
