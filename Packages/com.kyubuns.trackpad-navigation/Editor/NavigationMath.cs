using System;
using UnityEngine;

namespace TrackpadNavigation
{
    internal enum SceneGesture
    {
        Pan, Orbit, Look
    }

    internal static class NavigationMath
    {
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        public static float Sensitivity(float value) => IsFinite(value) ? Mathf.Clamp(value, 0.05f, 10f) : 1f;

        public static Vector2 Pan(TrackpadEvent value, TrackpadPreferences settings)
        {
            var delta = new Vector2((float)value.DeltaX, (float)value.DeltaY);
            if (!IsFinite(delta))
            {
                return Vector2.zero;
            }
            // NSEventはmacOSのスクロール方向を反映済みなので、重ねて反転しない。
            if (settings.InvertNaturalScrolling)
            {
                delta = -delta;
            }

            if (settings.InvertPanX)
            {
                delta.x = -delta.x;
            }

            if (settings.InvertPanY)
            {
                delta.y = -delta.y;
            }

            return delta * Sensitivity(settings.PanSensitivity);
        }

        public static float ZoomFactor(TrackpadEvent value, TrackpadPreferences settings)
        {
            if (double.IsNaN(value.Magnification) || double.IsInfinity(value.Magnification))
            {
                return 1;
            }

            double exponent = value.Magnification * Sensitivity(settings.ZoomSensitivity) * (settings.InvertZoom ? -1 : 1);
            return (float)Math.Exp(Math.Max(-4, Math.Min(4, exponent)));
        }

        public static Vector2 ZoomTranslation(Vector2 translation, Vector2 anchor, float scaleRatio) => anchor + (translation - anchor) * scaleRatio;
        public static Vector3 ZoomPivot(Vector3 pivot, Vector3 anchor, float sizeRatio) => anchor + (pivot - anchor) * sizeRatio;

        public static SceneGesture SceneAction(TrackpadEvent value, SceneGesture previousAction)
        {
            // 指を離した後の修飾キー変更では、終了・慣性入力の操作種別を切り替えない。
            if (value.IsMomentum || (value.Phase & GesturePhase.Ended) != 0)
            {
                return previousAction;
            }

            if ((value.Modifiers & GestureModifiers.Command) != 0)
            {
                return SceneGesture.Look;
            }

            return (value.Modifiers & GestureModifiers.Option) != 0 ? SceneGesture.Orbit : SceneGesture.Pan;
        }

        public static Quaternion Rotate(Quaternion rotation, Vector2 delta, TrackpadPreferences settings)
        {
            float gain = 0.2f * Sensitivity(settings.OrbitSensitivity);
            if (settings.InvertOrbitX)
            {
                delta.x = -delta.x;
            }

            if (settings.InvertOrbitY)
            {
                delta.y = -delta.y;
            }

            var forward = rotation * Vector3.forward;
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1, 1)) * Mathf.Rad2Deg;
            // 水平を保ち、極を越える反転を防ぐ。
            return Quaternion.Euler(Mathf.Clamp(pitch + delta.y * gain, -89.5f, 89.5f), yaw + delta.x * gain, 0);
        }

        public static Vector3 PivotForLook(Vector3 cameraPosition, Quaternion rotation, float distance) => cameraPosition + rotation * Vector3.forward * distance;

        public static float WorldUnitsPerPoint(Camera camera, float depth, float viewportHeight)
        {
            float halfHeight = camera.orthographic ? camera.orthographicSize : depth * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            return 2 * halfHeight / Mathf.Max(1, viewportHeight);
        }

        public static Vector3 OrbitPivot(Vector3 pivot, Vector3 pointOfInterest, Quaternion rotation, Quaternion nextRotation) => pointOfInterest + nextRotation * Quaternion.Inverse(rotation) * (pivot - pointOfInterest);
    }
}
