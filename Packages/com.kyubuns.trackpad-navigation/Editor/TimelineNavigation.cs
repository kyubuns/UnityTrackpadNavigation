using UnityEditor;
using UnityEngine;

namespace TrackpadNavigation
{
    internal sealed class TimelineNavigation : NavigationTarget
    {
        readonly object state;
        readonly object tree;
        readonly object sequence;
        public override string Description => "Timeline — time pan / track scroll / cursor zoom";
        TimelineNavigation(EditorWindow window, object state, object tree) : base(window)
        {
            this.state = state;
            this.tree = tree;
            sequence = EditorMember.Get(state, "editSequence");
        }

        public static INavigationTarget TryCreate(EditorWindow window)
        {
            var state = EditorMember.Get(window, "state");
            var tree = EditorMember.Get(window, "treeView");
            if (!EditorMember.Writable(state, "timeAreaShownRange", typeof(Vector2)) || !EditorMember.Writable(tree, "scrollPosition", typeof(Vector2)))
            {
                return null;
            }

            var area = EditorMember.Get(window, "timeArea");
            if (!(EditorMember.Get(window, "sequenceContentRect") is Rect) || !(EditorMember.Get(area, "drawRect") is Rect))
            {
                return null;
            }

            return new TimelineNavigation(window, state, tree);
        }

        protected override bool IsCurrent => ReferenceEquals(state, EditorMember.Get(Window, "state")) &&
            ReferenceEquals(tree, EditorMember.Get(Window, "treeView")) && ReferenceEquals(sequence, EditorMember.Get(state, "editSequence")) &&
            EditorMember.Get(sequence, "asset") != null;

        protected override bool ContainsPoint(Vector2 localPoint)
        {
            var rect = (Rect)EditorMember.Get(Window, "sequenceContentRect");
            rect.yMin += 2;
            rect.xMax -= 16;
            rect.yMax -= 16;
            return rect.Contains(NavigationCoordinates.ToLocal(Window, Window.rootVisualElement, localPoint));
        }

        protected override void ApplyInput(TrackpadEvent value, TrackpadPreferences settings)
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
                var local = NavigationCoordinates.ToLocal(Window, Window.rootVisualElement, value.ScreenPosition - Window.position.position);
                float fraction = Mathf.Clamp01((local.x - area.x) / width);
                float anchor = Mathf.Lerp(range.x, range.y, fraction);
                float nextSpan = Mathf.Clamp(span / NavigationMath.ZoomFactor(value, settings), 0.001f, 9000000f);
                EditorMember.Set(state, "timeAreaShownRange", new Vector2(anchor - fraction * nextSpan, anchor + (1 - fraction) * nextSpan));
                // 余白も含む倍率制限はUnityに任せ、補正後の幅でカーソル下の時刻を維持する。
                var constrained = (Vector2)EditorMember.Get(state, "timeAreaShownRange");
                nextSpan = constrained.y - constrained.x;
                range = new Vector2(anchor - fraction * nextSpan, anchor + (1 - fraction) * nextSpan);
            }
            // 範囲制限・トラック配置・ビュー状態の保存はUnityのsetterに任せる。
            EditorMember.Set(state, "timeAreaShownRange", range);
            Window.Repaint();
        }
    }
}
