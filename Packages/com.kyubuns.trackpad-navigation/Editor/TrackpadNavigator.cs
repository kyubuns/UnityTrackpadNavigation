using System;
using UnityEditor;

namespace TrackpadNavigation
{
    [InitializeOnLoad]
    internal static class TrackpadNavigator
    {
        public static TrackpadEvent LastEvent
        {
            get; private set;
        }
        public static bool Observe
        {
            get; set;
        }
        public static string TargetDescription {
            get; private set;
        } = "No navigation target";
        public static string LastError {
            get; private set;
        } = "";
        static INavigationTarget target;
        static int targetId;
        static bool attemptedStart;

        static TrackpadNavigator()
        {
            EditorApplication.delayCall += Start;
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += TrackpadInput.Stop;
            EditorApplication.quitting += TrackpadInput.Stop;
        }

        static void Start()
        {
            attemptedStart = true;
            if (TrackpadSettings.Current.Enabled)
            {
                TrackpadInput.Start();
            }
        }

        public static void ClearTarget()
        {
            target = null;
            ++targetId;
            TrackpadInput.SetCapture(default, Observe);
        }

        static void Update()
        {
            var settings = TrackpadSettings.Current;
            if (!settings.Enabled)
            {
                if (TrackpadInput.Running)
                {
                    TrackpadInput.Stop();
                }

                attemptedStart = false;
                ClearTarget();
                TargetDescription = "Disabled";
                return;
            }
            if (!attemptedStart)
            {
                Start();
            }

            if (!TrackpadInput.Running)
            {
                return;
            }

            try
            {
                if (target != null && !target.IsAvailable)
                {
                    ClearTarget();
                }
                while (TrackpadInput.Poll(out var value))
                {
                    LastEvent = value;
                    if (!value.IsCaptured || value.Target != targetId || target == null || !target.Window)
                    {
                        continue;
                    }
                    // Sceneは取消・終了も受け取り、一時POIを解放する。他のCanvasは移動可能な入力だけを受け取る。
                    var phase = value.IsMomentum ? value.MomentumPhase : value.Phase;
                    if (target is SceneViewNavigation || ((phase & GesturePhase.Cancelled) == 0 && (!value.IsMomentum || settings.Momentum)))
                    {
                        target.Apply(value, settings);
                    }
                }
                if (!TrackpadInput.GetPointer(out var pointer) || pointer.Active == 0 || EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    ClearTarget();
                    TargetDescription = "Inactive / importing";
                    return;
                }
                TrackpadInput.SetCapture(CaptureFor(EditorWindow.mouseOverWindow, pointer, settings), Observe);
            }
            catch (Exception exception)
            {
                ClearTarget();
                LastError = exception.GetType().Name + ": " + exception.Message;
                TargetDescription = "Adapter unavailable — standard input retained";
            }
        }

        internal static NativeCapture CaptureFor(EditorWindow window, NativePointer pointer, TrackpadPreferences settings)
        {
            var localPoint = window ? pointer.Position - window.position.position : default;
            if (window != target?.Window || (target != null && !target.IsAvailable))
            {
                ClearTarget();
            }

            bool canBegin = target != null && target.HitTest(localPoint);
            if (!canBegin)
            {
                var candidate = NavigationTargets.Resolve(window, localPoint, settings);
                if (candidate != null && candidate.HitTest(localPoint))
                {
                    ClearTarget();
                    target = candidate;
                    canBegin = true;
                }
                else if (candidate == null)
                {
                    ClearTarget();
                }
            }
            if (target == null)
            {
                TargetDescription = window ? $"Standard input — {window.titleContent.text}" : "No Editor canvas under pointer";
                return default;
            }
            // ノードがカーソル下へ動いても進行中の入力先を残す。Kindsは新しい操作の開始可否だけを表す。
            TargetDescription = target.Description;
            int kinds = canBegin ? 3 | (target.SupportsLook ? 4 : 0) | (target.SupportsSmartZoom ? 8 : 0) : 0;
            return new NativeCapture
            {
                X = pointer.X - 0.5,
                Y = pointer.Y - 0.5,
                Width = 1,
                Height = 1,
                Target = targetId,
                WindowNumber = pointer.WindowNumber,
                Kinds = kinds
            };
        }
    }
}
