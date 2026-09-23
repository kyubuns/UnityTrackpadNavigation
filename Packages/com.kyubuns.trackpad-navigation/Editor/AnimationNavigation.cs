using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TrackpadNavigation
{
    internal sealed class AnimationNavigation : ReflectedNavigation
    {
        readonly object state;
        readonly object timeArea;
        readonly object hierarchy;
        readonly object hierarchyState;
        readonly bool curves;
        readonly MethodInfo setTransform;
        readonly MethodInfo getContentSize;
        readonly MethodInfo getTotalRect;
        readonly FieldInfo scrollPosition;

        public override string Description => curves ? "Animation — curves" : "Animation — dope sheet";

        AnimationNavigation(EditorWindow window, object state, object timeArea) : base(window)
        {
            this.state = state;
            this.timeArea = timeArea;
            curves = Equals(EditorMember.Get(state, "showCurveEditor"), true);
            hierarchy = EditorMember.Get(EditorMember.Get(window, "animEditor"), "m_Hierarchy");
            hierarchyState = EditorMember.Get(state, "hierarchyState");
            setTransform = timeArea.GetType().GetMethod("SetTransform", new[]
            {
                typeof(Vector2), typeof(Vector2)
            });
            getContentSize = hierarchy?.GetType().GetMethod("GetContentSize", Type.EmptyTypes);
            getTotalRect = hierarchy?.GetType().GetMethod("GetTotalRect", Type.EmptyTypes);
            scrollPosition = hierarchyState?.GetType().GetField("scrollPos");
        }

        public new static INavigationTarget TryCreate(EditorWindow window)
        {
            var state = EditorMember.Get(window, "state");
            var timeArea = EditorMember.Get(state, "timeArea");
            if (!(EditorMember.Get(timeArea, "drawRect") is Rect) ||
                !(EditorMember.Get(timeArea, "scale") is Vector2) ||
                !(EditorMember.Get(timeArea, "translation") is Vector2) ||
                !(EditorMember.Get(state, "showCurveEditor") is bool))
            {
                return null;
            }

            var target = new AnimationNavigation(window, state, timeArea);
            if (target.setTransform == null || target.getContentSize?.ReturnType != typeof(Vector2) ||
                target.getTotalRect?.ReturnType != typeof(Rect) || target.scrollPosition?.FieldType != typeof(Vector2))
            {
                return null;
            }
            return target;
        }

        bool IsCurrent => ReferenceEquals(state, EditorMember.Get(Window, "state")) &&
            ReferenceEquals(timeArea, EditorMember.Get(state, "timeArea")) &&
            ReferenceEquals(hierarchyState, EditorMember.Get(state, "hierarchyState")) &&
            Equals(EditorMember.Get(state, "disabled"), false) &&
            Equals(EditorMember.Get(state, "animatorIsOptimized"), false);

        public override bool HitTest(Vector2 localPoint)
        {
            if (!IsCurrent)
            {
                return false;
            }

            // drawRectは時間ルーラー・イベント行・スクロールバーを含まない。
            var rect = (Rect)EditorMember.Get(timeArea, "drawRect");
            return rect.width > 1 && rect.height > 1 && rect.Contains(localPoint);
        }

        public override void Apply(TrackpadEvent value, TrackpadPreferences settings)
        {
            if (!IsCurrent)
            {
                return;
            }

            var scale = (Vector2)EditorMember.Get(timeArea, "scale");
            var translation = (Vector2)EditorMember.Get(timeArea, "translation");
            if (!NavigationMath.IsFinite(scale) || scale.x == 0 || scale.y == 0)
            {
                return;
            }

            if (value.Kind == GestureKind.Scroll)
            {
                var delta = NavigationMath.Pan(value, settings);
                translation.x += delta.x;
                if (curves)
                {
                    translation.y += delta.y;
                }
                else
                {
                    // ドープシートの行と左側のプロパティ一覧は同じスクロール状態を使う。
                    var scroll = (Vector2)scrollPosition.GetValue(hierarchyState);
                    var contentSize = (Vector2)getContentSize.Invoke(hierarchy, null);
                    var viewport = (Rect)getTotalRect.Invoke(hierarchy, null);
                    scroll.y = Mathf.Clamp(scroll.y - delta.y, 0, Mathf.Max(0, contentSize.y - viewport.height));
                    scrollPosition.SetValue(hierarchyState, scroll);
                }
            }
            else if (value.Kind == GestureKind.Magnify)
            {
                float factor = NavigationMath.ZoomFactor(value, settings);
                var rect = (Rect)EditorMember.Get(timeArea, "drawRect");
                var anchor = value.ScreenPosition - Window.position.position - rect.position;
                var zoomed = NavigationMath.ZoomTranslation(translation, anchor, factor);
                translation.x = zoomed.x;
                scale.x *= factor;
                if (curves)
                {
                    translation.y = zoomed.y;
                    scale.y *= factor;
                }
            }
            else
            {
                return;
            }

            // Unity自身に表示範囲の制限を適用させ、クリップやキー選択には触れない。
            setTransform.Invoke(timeArea, new object[]
            {
                translation, scale
            });
            Window.Repaint();
        }
    }
}
