using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TrackpadNavigation
{
    // UnityのZoomableAreaを使うビュー共通の入力処理。派生型は対象の寿命と固有の制約を定義する。
    internal abstract class ZoomAreaNavigation : NavigationTarget
    {
        protected readonly object Area;
        readonly MethodInfo setTransform;

        protected ZoomAreaNavigation(EditorWindow window, object area) : base(window)
        {
            Area = area;
            setTransform = EditorMember.Method(area, "SetTransform", typeof(Vector2), typeof(Vector2));
        }

        protected static bool SupportsArea(object area) => EditorMember.Get(area, "drawRect") is Rect &&
            EditorMember.Get(area, "translation") is Vector2 && EditorMember.Get(area, "scale") is Vector2 &&
            EditorMember.Method(area, "SetTransform", typeof(Vector2), typeof(Vector2)) != null;

        // IMGUIのdrawRectはタブや枠を除いたEditorWindowの描画原点を基準にする。
        protected virtual Vector2 AreaPoint(Vector2 windowPoint) => NavigationCoordinates.ToLocal(Window, Window.rootVisualElement, windowPoint);
        protected virtual Vector2 PanDelta(Vector2 delta) => delta;
        protected virtual Vector2 ZoomScale(Vector2 scale, float factor) => scale * factor;
        protected virtual void AfterApply()
        {
        }

        protected override bool ContainsPoint(Vector2 localPoint)
        {
            // drawRectはルーラーとスクロールバーを除いた表示領域。
            var rect = (Rect)EditorMember.Get(Area, "drawRect");
            return rect.width > 1 && rect.height > 1 && rect.Contains(AreaPoint(localPoint));
        }

        protected override void ApplyInput(TrackpadEvent value, TrackpadPreferences settings)
        {
            var translation = (Vector2)EditorMember.Get(Area, "translation");
            var scale = (Vector2)EditorMember.Get(Area, "scale");
            if (!NavigationMath.IsFinite(translation) || !NavigationMath.IsFinite(scale) || scale.x == 0 || scale.y == 0)
            {
                return;
            }
            if (value.Kind == GestureKind.Scroll)
            {
                translation += PanDelta(NavigationMath.Pan(value, settings));
            }
            else if (value.Kind == GestureKind.Magnify)
            {
                var nextScale = ZoomScale(scale, NavigationMath.ZoomFactor(value, settings));
                var rect = (Rect)EditorMember.Get(Area, "drawRect");
                var anchor = AreaPoint(value.ScreenPosition - Window.position.position) - rect.position;
                var requestedTranslation = anchor + Vector2.Scale(translation - anchor, nextScale / scale);
                if (!SetTransform(requestedTranslation, nextScale))
                {
                    return;
                }
                // Unityが余白や倍率を制限した後のscaleで補正し、ズーム限界でも表示をずらさない。
                nextScale = (Vector2)EditorMember.Get(Area, "scale");
                translation = anchor + Vector2.Scale(translation - anchor, nextScale / scale);
                scale = nextScale;
            }
            else
            {
                return;
            }
            if (SetTransform(translation, scale))
            {
                Window.Repaint();
            }
        }

        bool SetTransform(Vector2 translation, Vector2 scale)
        {
            if (!NavigationMath.IsFinite(translation) || !NavigationMath.IsFinite(scale))
            {
                return false;
            }
            // 負のYスケールも保持し、Unity自身に表示範囲を制限させる。
            setTransform.Invoke(Area, new object[]
            {
                translation, scale
            });
            AfterApply();
            return true;
        }
    }
}
