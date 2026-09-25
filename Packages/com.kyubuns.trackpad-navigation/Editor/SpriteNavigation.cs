using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrackpadNavigation
{
    internal sealed class SpriteNavigation : NavigationTarget
    {
        readonly VisualElement canvas;
        readonly object texture;
        public override string Description => "Sprite Editor — pan / cursor zoom";

        SpriteNavigation(EditorWindow window, VisualElement canvas, object texture) : base(window)
        {
            this.canvas = canvas;
            this.texture = texture;
        }

        public static INavigationTarget TryCreate(EditorWindow window)
        {
            var canvas = window.rootVisualElement.Q<IMGUIContainer>("mainViewIMGUIElement");
            var texture = EditorMember.Get(window, "previewTexture");
            if (canvas == null || texture == null || !(EditorMember.Get(window, "windowDimension") is Rect) ||
                !EditorMember.Writable(window, "zoomLevel", typeof(float)) || !EditorMember.Writable(window, "scrollPosition", typeof(Vector2)))
            {
                return null;
            }
            return new SpriteNavigation(window, canvas, texture);
        }

        protected override bool IsCurrent => canvas.panel != null && ReferenceEquals(texture, EditorMember.Get(Window, "previewTexture"));
        Vector2 CanvasPoint(Vector2 windowPoint) => NavigationCoordinates.ToLocal(Window, canvas, windowPoint);

        protected override bool ContainsPoint(Vector2 localPoint)
        {
            var rect = (Rect)EditorMember.Get(Window, "windowDimension");
            var picked = NavigationHitTest.Pick(Window, localPoint);
            // 画像の上に重なるSprite Inspectorやモジュール固有のUIを除外する。
            return rect.width > 1 && rect.height > 1 && rect.Contains(CanvasPoint(localPoint)) &&
                (picked == canvas || (picked != null && canvas.Contains(picked)));
        }

        protected override void ApplyInput(TrackpadEvent value, TrackpadPreferences settings)
        {
            var scroll = (Vector2)EditorMember.Get(Window, "scrollPosition");
            float scale = (float)EditorMember.Get(Window, "zoomLevel");
            if (!NavigationMath.IsFinite(scale) || !NavigationMath.IsFinite(scroll) || scale <= 0)
            {
                return;
            }
            if (value.Kind == GestureKind.Scroll)
            {
                scroll -= NavigationMath.Pan(value, settings);
            }
            else if (value.Kind == GestureKind.Magnify)
            {
                // setterによるズーム制限後の実倍率で、画像中央からの距離を補正する。
                EditorMember.Set(Window, "zoomLevel", scale * NavigationMath.ZoomFactor(value, settings));
                float next = (float)EditorMember.Get(Window, "zoomLevel");
                var rect = (Rect)EditorMember.Get(Window, "windowDimension");
                var anchor = CanvasPoint(value.ScreenPosition - Window.position.position) - rect.center;
                scroll = -NavigationMath.ZoomTranslation(-scroll, anchor, next / scale);
            }
            else
            {
                return;
            }
            EditorMember.Set(Window, "scrollPosition", scroll);
            Window.Repaint();
        }
    }
}
