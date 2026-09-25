using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TrackpadNavigation
{
    internal sealed class GameViewNavigation : ReflectedNavigation
    {
        readonly object area;
        readonly MethodInfo setTransform;
        readonly MethodInfo constrain;
        public override string Description => "Game View — pan / cursor zoom";

        GameViewNavigation(EditorWindow window, object area, MethodInfo setTransform, MethodInfo constrain) : base(window)
        {
            this.area = area;
            this.setTransform = setTransform;
            this.constrain = constrain;
        }

        public new static INavigationTarget TryCreate(EditorWindow window)
        {
            var area = EditorMember.Get(window, "m_ZoomArea");
            var setTransform = area?.GetType().GetMethod("SetTransform", new[]
            {
                typeof(Vector2), typeof(Vector2)
            });
            var constrain = window.GetType().GetMethod("EnforceZoomAreaConstraints", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (!(EditorMember.Get(area, "drawRect") is Rect) ||
                !(EditorMember.Get(area, "translation") is Vector2) || !(EditorMember.Get(area, "scale") is Vector2) ||
                !(EditorMember.Get(window, "minScale") is float) || !(EditorMember.Get(window, "maxScale") is float) ||
                setTransform == null || constrain == null)
            {
                return null;
            }
            return new GameViewNavigation(window, area, setTransform, constrain);
        }

        // 再生中はゲーム入力を優先する。取得済みの入力先も再生開始時点で無効にする。
        bool IsCurrent => Window != null && (!EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPaused) &&
            ReferenceEquals(area, EditorMember.Get(Window, "m_ZoomArea"));

        public override bool HitTest(Vector2 localPoint)
        {
            if (!IsCurrent)
            {
                return false;
            }
            var rect = (Rect)EditorMember.Get(area, "drawRect");
            return rect.width > 1 && rect.height > 1 && rect.Contains(localPoint);
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
                float minimum = (float)EditorMember.Get(Window, "minScale");
                float maximum = (float)EditorMember.Get(Window, "maxScale");
                float next = Mathf.Clamp(scale.x * NavigationMath.ZoomFactor(value, settings), minimum, maximum);
                var rect = (Rect)EditorMember.Get(area, "drawRect");
                var anchor = value.ScreenPosition - Window.position.position - rect.position;
                translation = NavigationMath.ZoomTranslation(translation, anchor, next / scale.x);
                scale = Vector2.one * next;
            }
            else
            {
                return;
            }
            setTransform.Invoke(area, new object[]
            {
                translation, scale
            });
            // Game View固有の画像端へのクランプも即時適用する。
            constrain.Invoke(Window, null);
            Window.Repaint();
        }
    }
}
