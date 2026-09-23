using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrackpadNavigation
{
    // Reflectionは既知のビュー状態に限定し、グラフの編集データを変更しない。
    internal static class EditorMember
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        public static object Get(object owner, string name)
        {
            if (owner == null)
            {
                return null;
            }

            var type = owner.GetType();
            return type.GetProperty(name, Flags)?.GetValue(owner) ?? type.GetField(name, Flags)?.GetValue(owner);
        }
        public static bool Writable(object owner, string name, Type valueType)
        {
            var property = owner?.GetType().GetProperty(name, Flags);
            return property != null && property.PropertyType == valueType && property.CanRead && property.CanWrite;
        }
        public static void Set(object owner, string name, object value) => owner.GetType().GetProperty(name, Flags).SetValue(owner, value);
    }

    internal abstract class ReflectedNavigation : INavigationTarget
    {
        public EditorWindow Window
        {
            get;
        }
        public abstract string Description
        {
            get;
        }
        public bool SupportsLook => false;
        public bool SupportsSmartZoom => false;
        protected ReflectedNavigation(EditorWindow window) => Window = window;
        public abstract bool HitTest(Vector2 localPoint);
        public abstract void Apply(TrackpadEvent value, TrackpadPreferences settings);

        public static INavigationTarget TryCreate(EditorWindow window)
        {
            switch (window.GetType().FullName)
            {
                case "UnityEditor.Graphs.AnimatorControllerTool":
                    return AnimatorNavigation.TryCreate(window);
                case "UnityEditor.AnimationWindow":
                    return AnimationNavigation.TryCreate(window);
                case "UnityEditor.Timeline.TimelineWindow":
                    return TimelineNavigation.TryCreate(window);
            }
            return BuilderNavigation.TryCreate(window);
        }
    }

    internal sealed class AnimatorNavigation : ReflectedNavigation
    {
        readonly VisualElement graph;
        public override string Description => "Animator — state machine / blend tree";
        AnimatorNavigation(EditorWindow window, VisualElement graph) : base(window) => this.graph = graph;

        public new static INavigationTarget TryCreate(EditorWindow window)
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

        public override bool HitTest(Vector2 localPoint)
        {
            if (graph.panel == null || graph.parent == null)
            {
                return false;
            }

            var point = Window.rootVisualElement.LocalToWorld(localPoint);
            return graph.parent.worldBound.Contains(point) && !NavigationHitTest.IsControl(NavigationHitTest.Pick(Window, localPoint), graph.parent);
        }

        public override void Apply(TrackpadEvent value, TrackpadPreferences settings)
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
                var panelPoint = Window.rootVisualElement.LocalToWorld(value.ScreenPosition - Window.position.position);
                var anchor = graph.parent.WorldToLocal(panelPoint) - graph.layout.position;
                position = NavigationMath.ZoomTranslation(position, anchor, next / scale.x);
                scale = new Vector3(next, next, 1);
            }
            EditorMember.Set(Window, "graphPosition", position);
            EditorMember.Set(Window, "graphScale", scale);
            Window.Repaint();
        }
    }

    internal sealed class TimelineNavigation : ReflectedNavigation
    {
        readonly object state;
        readonly object tree;
        public override string Description => "Timeline — time pan / track scroll / cursor zoom";
        TimelineNavigation(EditorWindow window, object state, object tree) : base(window)
        {
            this.state = state;
            this.tree = tree;
        }

        public new static INavigationTarget TryCreate(EditorWindow window)
        {
            var state = EditorMember.Get(window, "state");
            var tree = EditorMember.Get(window, "treeView");
            if (!EditorMember.Writable(state, "timeAreaShownRange", typeof(Vector2)) || !EditorMember.Writable(tree, "scrollPosition", typeof(Vector2)))
            {
                return null;
            }

            if (!(EditorMember.Get(window, "sequenceContentRect") is Rect))
            {
                return null;
            }

            return new TimelineNavigation(window, state, tree);
        }

        public override bool HitTest(Vector2 localPoint)
        {
            if (!ReferenceEquals(state, EditorMember.Get(Window, "state")) || !ReferenceEquals(tree, EditorMember.Get(Window, "treeView")))
            {
                return false;
            }

            var rect = (Rect)EditorMember.Get(Window, "sequenceContentRect");
            rect.yMin += 2;
            rect.xMax -= 16;
            rect.yMax -= 16;
            return rect.Contains(localPoint) && EditorMember.Get(EditorMember.Get(state, "editSequence"), "asset") != null;
        }

        public override void Apply(TrackpadEvent value, TrackpadPreferences settings)
        {
            var range = (Vector2)EditorMember.Get(state, "timeAreaShownRange");
            var timeArea = EditorMember.Get(Window, "timeArea");
            var area = (Rect)EditorMember.Get(timeArea, "drawRect");
            float width = Mathf.Max(1, area.width);
            float span = range.y - range.x;
            if (value.Kind == GestureKind.Scroll)
            {
                var delta = NavigationMath.Pan(value, settings);
                range -= Vector2.one * (delta.x * span / width);
                var scroll = (Vector2)EditorMember.Get(tree, "scrollPosition");
                scroll.y -= delta.y;
                EditorMember.Set(tree, "scrollPosition", scroll);
            }
            else if (value.Kind == GestureKind.Magnify)
            {
                var local = value.ScreenPosition - Window.position.position;
                float fraction = Mathf.Clamp01((local.x - area.x) / width);
                float anchor = Mathf.Lerp(range.x, range.y, fraction);
                float nextSpan = Mathf.Clamp(span / NavigationMath.ZoomFactor(value, settings), 0.001f, 9000000f);
                range = new Vector2(anchor - fraction * nextSpan, anchor + (1 - fraction) * nextSpan);
            }
            // 範囲制限・トラック配置・ビュー状態の保存はUnityのsetterに任せる。
            EditorMember.Set(state, "timeAreaShownRange", range);
            Window.Repaint();
        }
    }

    internal sealed class BuilderNavigation : ReflectedNavigation
    {
        readonly VisualElement owner;
        readonly VisualElement viewport;
        public override string Description => "UI Builder — canvas";
        BuilderNavigation(EditorWindow window, VisualElement owner, VisualElement viewport) : base(window)
        {
            this.owner = owner;
            this.viewport = viewport;
        }

        public new static INavigationTarget TryCreate(EditorWindow window)
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

        public override bool HitTest(Vector2 localPoint)
        {
            if (viewport.panel == null || Equals(EditorMember.Get(owner, "isPreviewEnabled"), true))
            {
                return false;
            }

            var point = Window.rootVisualElement.LocalToWorld(localPoint);
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

        public override void Apply(TrackpadEvent value, TrackpadPreferences settings)
        {
            var position = (Vector2)EditorMember.Get(owner, "contentOffset");
            float scale = (float)EditorMember.Get(owner, "zoomScale");
            if (value.Kind == GestureKind.Scroll)
            {
                position += NavigationMath.Pan(value, settings);
            }
            else if (value.Kind == GestureKind.Magnify)
            {
                float next = Mathf.Clamp(scale * NavigationMath.ZoomFactor(value, settings), 0.1f, 4);
                var panelPoint = Window.rootVisualElement.LocalToWorld(value.ScreenPosition - Window.position.position);
                position = NavigationMath.ZoomTranslation(position, viewport.WorldToLocal(panelPoint), next / scale);
                EditorMember.Set(owner, "zoomScale", next);
            }
            EditorMember.Set(owner, "contentOffset", position);
            Window.Repaint();
        }
    }
}
