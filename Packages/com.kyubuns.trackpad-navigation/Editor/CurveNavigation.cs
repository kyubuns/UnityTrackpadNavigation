using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TrackpadNavigation
{
    internal sealed class CurveNavigation : ReflectedNavigation
    {
        readonly object editor;
        readonly MethodInfo setTransform;
        public override string Description => "Curve Editor — time / value pan / cursor zoom";

        CurveNavigation(EditorWindow window, object editor, MethodInfo setTransform) : base(window)
        {
            this.editor = editor;
            this.setTransform = setTransform;
        }

        public new static INavigationTarget TryCreate(EditorWindow window)
        {
            var editor = EditorMember.Get(window, "m_CurveEditor");
            var setTransform = editor?.GetType().GetMethod("SetTransform", new[]
            {
                typeof(Vector2), typeof(Vector2)
            });
            if (!(EditorMember.Get(editor, "drawRect") is Rect) ||
                !(EditorMember.Get(editor, "translation") is Vector2) ||
                !(EditorMember.Get(editor, "scale") is Vector2) || setTransform == null)
            {
                return null;
            }
            return new CurveNavigation(window, editor, setTransform);
        }

        bool IsCurrent => Window != null && ReferenceEquals(editor, EditorMember.Get(Window, "m_CurveEditor"));

        public override bool HitTest(Vector2 localPoint)
        {
            if (!IsCurrent)
            {
                return false;
            }
            // 下部のプリセット一覧とスクロールバーはUnity標準の操作を残す。
            var rect = (Rect)EditorMember.Get(editor, "drawRect");
            return rect.width > 1 && rect.height > 1 && rect.Contains(localPoint);
        }

        public override void Apply(TrackpadEvent value, TrackpadPreferences settings)
        {
            if (!IsCurrent)
            {
                return;
            }
            var translation = (Vector2)EditorMember.Get(editor, "translation");
            var scale = (Vector2)EditorMember.Get(editor, "scale");
            if (!NavigationMath.IsFinite(scale) || !NavigationMath.IsFinite(translation) || scale.x == 0 || scale.y == 0)
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
                var rect = (Rect)EditorMember.Get(editor, "drawRect");
                var anchor = value.ScreenPosition - Window.position.position - rect.position;
                translation = NavigationMath.ZoomTranslation(translation, anchor, factor);
                scale *= factor;
            }
            else
            {
                return;
            }
            // 表示範囲の制限はUnityに任せ、カーブとキー選択には触れない。
            setTransform.Invoke(editor, new object[]
            {
                translation, scale
            });
            Window.Repaint();
        }
    }
}
