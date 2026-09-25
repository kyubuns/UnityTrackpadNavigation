using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TrackpadNavigation
{
    internal sealed class AnimationNavigation : ZoomAreaNavigation
    {
        readonly object state;
        readonly object hierarchy;
        readonly object hierarchyState;
        readonly bool curves;
        readonly MethodInfo getContentSize;
        readonly MethodInfo getTotalRect;
        readonly FieldInfo scrollPosition;

        public override string Description => curves ? "Animation — curves" : "Animation — dope sheet";

        AnimationNavigation(EditorWindow window, object state, object area) : base(window, area)
        {
            this.state = state;
            curves = Equals(EditorMember.Get(state, "showCurveEditor"), true);
            hierarchy = EditorMember.Get(EditorMember.Get(window, "animEditor"), "m_Hierarchy");
            hierarchyState = EditorMember.Get(state, "hierarchyState");
            getContentSize = EditorMember.Method(hierarchy, "GetContentSize");
            getTotalRect = EditorMember.Method(hierarchy, "GetTotalRect");
            scrollPosition = hierarchyState?.GetType().GetField("scrollPos");
        }

        public static INavigationTarget TryCreate(EditorWindow window)
        {
            var state = EditorMember.Get(window, "state");
            var area = EditorMember.Get(state, "timeArea");
            if (!SupportsArea(area) || !(EditorMember.Get(state, "showCurveEditor") is bool))
            {
                return null;
            }
            var target = new AnimationNavigation(window, state, area);
            if (target.getContentSize?.ReturnType != typeof(Vector2) || target.getTotalRect?.ReturnType != typeof(Rect) ||
                target.scrollPosition?.FieldType != typeof(Vector2))
            {
                return null;
            }
            return target;
        }

        protected override bool IsCurrent => ReferenceEquals(state, EditorMember.Get(Window, "state")) &&
            ReferenceEquals(Area, EditorMember.Get(state, "timeArea")) &&
            ReferenceEquals(hierarchyState, EditorMember.Get(state, "hierarchyState")) &&
            Equals(EditorMember.Get(state, "showCurveEditor"), curves) &&
            Equals(EditorMember.Get(state, "disabled"), false) && Equals(EditorMember.Get(state, "animatorIsOptimized"), false);

        protected override Vector2 ZoomScale(Vector2 scale, float factor) => new Vector2(scale.x * factor, curves ? scale.y * factor : scale.y);

        protected override Vector2 PanDelta(Vector2 delta)
        {
            if (curves)
            {
                return delta;
            }
            // ドープシートの行と左側のプロパティ一覧は同じスクロール状態を使う。
            var scroll = (Vector2)scrollPosition.GetValue(hierarchyState);
            var contentSize = (Vector2)getContentSize.Invoke(hierarchy, null);
            var viewport = (Rect)getTotalRect.Invoke(hierarchy, null);
            scroll.y = Mathf.Clamp(scroll.y - delta.y, 0, Mathf.Max(0, contentSize.y - viewport.height));
            scrollPosition.SetValue(hierarchyState, scroll);
            return new Vector2(delta.x, 0);
        }
    }
}
