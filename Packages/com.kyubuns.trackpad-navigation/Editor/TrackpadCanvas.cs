using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrackpadNavigation
{
    /// <summary>EditorのUI Toolkitキャンバスを明示的に登録する。ウィンドウ終了時にDisposeする。</summary>
    public sealed class TrackpadCanvas : IDisposable
    {
        internal static readonly Dictionary<VisualElement, TrackpadCanvas> Registrations = new Dictionary<VisualElement, TrackpadCanvas>();
        readonly VisualElement viewport;
        readonly VisualElement content;
        readonly Vector2 scaleLimits;
        readonly Action<Vector2, float> changed;
        public Vector2 Position
        {
            get; private set;
        }
        public float Scale
        {
            get; private set;
        }

        public TrackpadCanvas(VisualElement viewport, VisualElement content, Vector2 scaleLimits, Action<Vector2, float> changed = null)
        {
            if (viewport == null || content?.parent != viewport)
            {
                throw new ArgumentException("Content must be a direct child of the viewport.");
            }

            if (!NavigationMath.IsFinite(scaleLimits) || scaleLimits.x <= 0 || scaleLimits.y <= scaleLimits.x)
            {
                throw new ArgumentException("Invalid scale limits.");
            }

            if (Registrations.ContainsKey(viewport))
            {
                throw new ArgumentException("Viewport already registered.");
            }

            this.viewport = viewport;
            this.content = content;
            this.scaleLimits = scaleLimits;
            this.changed = changed;
            content.style.transformOrigin = new TransformOrigin(0, 0, 0);
            SetTransform(Vector2.zero, 1);
            Registrations.Add(viewport, this);
        }

        public void SetTransform(Vector2 position, float scale)
        {
            if (!NavigationMath.IsFinite(position) || !NavigationMath.IsFinite(scale))
            {
                return;
            }

            Position = position;
            Scale = Mathf.Clamp(scale, scaleLimits.x, scaleLimits.y);
            content.style.translate = new Translate(Position.x, Position.y, 0);
            content.style.scale = new Scale(new Vector3(Scale, Scale, 1));
            changed?.Invoke(Position, Scale);
        }

        public void Dispose()
        {
            if (Registrations.TryGetValue(viewport, out var registration) && ReferenceEquals(registration, this))
            {
                Registrations.Remove(viewport);
            }
        }

        internal INavigationTarget Target(EditorWindow window) => new CanvasTarget(window, this);

        sealed class CanvasTarget : NavigationTarget
        {
            readonly TrackpadCanvas canvas;
            public override string Description => "UI Toolkit — registered canvas";
            public CanvasTarget(EditorWindow window, TrackpadCanvas canvas) : base(window)
            {
                this.canvas = canvas;
            }
            protected override bool IsCurrent => canvas.viewport.panel != null && canvas.content.parent == canvas.viewport &&
                (Window.rootVisualElement == canvas.viewport || Window.rootVisualElement.Contains(canvas.viewport)) &&
                Registrations.TryGetValue(canvas.viewport, out var registration) && ReferenceEquals(registration, canvas);

            protected override bool ContainsPoint(Vector2 localPoint)
            {
                var point = NavigationCoordinates.ToPanel(Window, localPoint);
                var picked = canvas.viewport.panel.Pick(point);
                return picked != null && (picked == canvas.viewport || canvas.viewport.Contains(picked)) &&
                    canvas.viewport.worldBound.Contains(point) && !NavigationHitTest.IsControl(picked, canvas.viewport);
            }
            protected override void ApplyInput(TrackpadEvent value, TrackpadPreferences settings)
            {
                var position = canvas.Position;
                float scale = canvas.Scale;
                if (value.Kind == GestureKind.Scroll)
                {
                    position += NavigationMath.Pan(value, settings);
                }
                else if (value.Kind == GestureKind.Magnify)
                {
                    float next = Mathf.Clamp(scale * NavigationMath.ZoomFactor(value, settings), canvas.scaleLimits.x, canvas.scaleLimits.y);
                    var point = NavigationCoordinates.ToPanel(Window, value.ScreenPosition - Window.position.position);
                    var anchor = canvas.viewport.WorldToLocal(point) - canvas.content.layout.position;
                    position = NavigationMath.ZoomTranslation(position, anchor, next / scale);
                    scale = next;
                }
                canvas.SetTransform(position, scale);
            }
        }
    }
}
