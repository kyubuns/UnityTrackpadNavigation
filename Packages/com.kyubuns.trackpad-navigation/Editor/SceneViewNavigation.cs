using UnityEditor;
using UnityEngine;

namespace TrackpadNavigation
{
    internal sealed class SceneViewNavigation : NavigationTarget
    {
        readonly SceneView view;
        Vector3? orbitPoint;
        Vector3? zoomPoint;
        SceneGesture scrollAction;
        public override string Description => "Scene View — Pan / Zoom / Orbit / Look";
        public override bool SupportsLook => true;
        public override bool SupportsSmartZoom => true;
        public SceneViewNavigation(SceneView view) : base(view) => this.view = view;
        protected override bool IsCurrent => view.camera != null && view.rootVisualElement.panel != null;

        protected override bool ContainsPoint(Vector2 localPoint)
        {
            if (!view.rootVisualElement.worldBound.Contains(NavigationCoordinates.ToPanel(view, localPoint)))
            {
                return false;
            }

            var picked = NavigationHitTest.Pick(view, localPoint);
            return picked != null && !NavigationHitTest.IsControl(picked, view.rootVisualElement);
        }

        protected override void ApplyInput(TrackpadEvent value, TrackpadPreferences settings)
        {
            if (value.Kind == GestureKind.SmartZoom)
            {
                orbitPoint = zoomPoint = null;
                ScenePicking.FrameAtPointer(view, value.ScreenPosition);
                return;
            }
            var phase = value.IsMomentum ? value.MomentumPhase : value.Phase;
            if ((phase & GesturePhase.MayBegin) != 0)
            {
                return;
            }

            if ((phase & GesturePhase.Cancelled) != 0)
            {
                if (value.Kind == GestureKind.Scroll)
                {
                    orbitPoint = null;
                }

                if (value.Kind == GestureKind.Magnify)
                {
                    zoomPoint = null;
                }

                return;
            }
            if (value.IsMomentum)
            {
                orbitPoint = null;
                if (!settings.Momentum)
                {
                    return;
                }
            }
            var pivot = view.pivot;
            var rotation = view.rotation;
            float size = Mathf.Max(view.size, 0.0001f);
            if (value.Kind == GestureKind.Magnify)
            {
                if ((phase & GesturePhase.Began) != 0)
                {
                    zoomPoint = null;
                }

                if (!zoomPoint.HasValue)
                {
                    if ((phase & GesturePhase.Ended) != 0)
                    {
                        return;
                    }

                    zoomPoint = ScenePicking.ZoomAnchor(view, value.ScreenPosition);
                }
                float nextSize = Mathf.Clamp(size / NavigationMath.ZoomFactor(value, settings), 0.0001f, 10000000f);
                pivot = NavigationMath.ZoomPivot(pivot, zoomPoint.Value, nextSize / size);
                size = nextSize;
                if ((phase & GesturePhase.Ended) != 0)
                {
                    zoomPoint = null;
                }
            }
            else if (value.Kind == GestureKind.Scroll)
            {
                scrollAction = NavigationMath.SceneAction(value, scrollAction);
                var action = scrollAction;
                if (view.in2DMode || view.isRotationLocked)
                {
                    action = SceneGesture.Pan;
                }

                if (action != SceneGesture.Orbit)
                {
                    orbitPoint = null;
                }
                // Orbitは指を離した時点で終了し、慣性で別のPOIを取り直さない。
                if (action == SceneGesture.Orbit && value.IsMomentum)
                {
                    return;
                }

                if (action == SceneGesture.Pan)
                {
                    var delta = NavigationMath.Pan(value, settings);
                    // NSEventの精密deltaもcameraViewportも論理point。Retina倍率は相殺される。
                    float unitsPerPoint = NavigationMath.WorldUnitsPerPoint(view.camera, view.cameraDistance, view.cameraViewport.height);
                    pivot -= rotation * new Vector3(delta.x, -delta.y, 0) * unitsPerPoint;
                }
                else
                {
                    var delta = new Vector2((float)value.DeltaX, (float)value.DeltaY);
                    if (!NavigationMath.IsFinite(delta))
                    {
                        return;
                    }

                    if (settings.InvertNaturalScrolling)
                    {
                        delta = -delta;
                    }

                    if (action == SceneGesture.Orbit)
                    {
                        if ((value.Phase & GesturePhase.Began) != 0)
                        {
                            orbitPoint = null;
                        }

                        if (!orbitPoint.HasValue)
                        {
                            if ((value.Phase & GesturePhase.Ended) != 0)
                            {
                                return;
                            }

                            orbitPoint = ScenePicking.PointOfInterest(view, value.ScreenPosition);
                            if (!view.orthographic)
                            {
                                float distance = view.cameraDistance;
                                var cameraPosition = pivot - rotation * Vector3.forward * distance;
                                float depth = Vector3.Dot(orbitPoint.Value - cameraPosition, rotation * Vector3.forward);
                                if (depth > 0.0001f && distance > 0.0001f)
                                {
                                    // 画面端のPOIを中央へ寄せず、カメラ位置と構図を保ってnavigation depthだけを合わせる。
                                    float nextSize = Mathf.Clamp(size * depth / distance, 0.0001f, 10000000f);
                                    pivot = NavigationMath.PivotForLook(cameraPosition, rotation, distance * nextSize / size);
                                    size = nextSize;
                                }
                            }
                        }
                    }
                    var newRotation = delta == Vector2.zero ? rotation : NavigationMath.Rotate(rotation, delta, settings);
                    if (action == SceneGesture.Look)
                    {
                        // 公開cameraDistanceを使い、視野角に応じたカメラ距離を維持する。
                        float distance = view.cameraDistance;
                        var cameraPosition = pivot - rotation * Vector3.forward * distance;
                        pivot = NavigationMath.PivotForLook(cameraPosition, newRotation, distance);
                    }
                    else
                    {
                        pivot = NavigationMath.OrbitPivot(pivot, orbitPoint.Value, rotation, newRotation);
                    }

                    rotation = newRotation;
                }
                if ((value.Phase & GesturePhase.Ended) != 0)
                {
                    orbitPoint = null;
                }
            }
            view.LookAt(pivot, rotation, size, view.orthographic, instant: true);
            view.Repaint();
        }
    }
}
