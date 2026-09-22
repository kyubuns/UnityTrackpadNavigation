using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TrackpadNavigation.Tests
{
    public sealed class UnityGestureGuardTests
    {
        [Test]
        public void PinchGuardFollowsNativeLifetimeAndPreservesOtherInput()
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var handler = typeof(EditorApplication).GetField("globalEventHandler", flags);
            var dispatch = typeof(EditorApplication).GetMethod("Internal_CallGlobalEventHandler", flags);
            var magnify = (EventType)typeof(EditorGUIUtility).GetProperty("magnifyGestureEventType", flags).GetValue(null);
            var allowEvent = typeof(Event).GetField("s_AllowOutsideOnGUI", flags);
            bool allowed = (bool)allowEvent.GetValue(null);
            var previousEvent = Event.current;
            bool enabled = TrackpadSettings.Current.Enabled;
            bool running = TrackpadInput.Running;
            int observed = 0;
            EditorApplication.CallbackFunction otherHandler = () => ++observed;
            try
            {
                // EditModeテストからも、UnityのGUI配送と同じEvent.currentを参照できるようにする。
                allowEvent.SetValue(null, true);
                TrackpadSettings.Current.Enabled = true;
                TrackpadInput.Start();
                Assert.That(TrackpadInput.Running && UnityGestureGuard.Installed, Is.True, TrackpadInput.Status);
                handler.SetValue(null, Delegate.Combine((Delegate)handler.GetValue(null), otherHandler));
                UnityGestureGuard.Install();
                int suppressed = UnityGestureGuard.Suppressed;
                foreach (float delta in new[] {
                    -1f,
                    1f
                })
                {
                    var pinch = new Event
                    {
                        type = magnify,
                        delta = new Vector2(delta, 0)
                    };
                    Event.current = pinch;
                    // 最大化ハンドラーを含むUnity実際の配送経路を通す。
                    dispatch.Invoke(null, null);
                    Assert.That(pinch.type, Is.EqualTo(EventType.Used));
                }
                Assert.That(UnityGestureGuard.Suppressed - suppressed, Is.EqualTo(2), "Repeated installation must not duplicate the handler");
                foreach (var type in new[] {
                    EventType.ScrollWheel,
                    EventType.KeyDown,
                    EventType.MouseDown
                })
                {
                    var input = new Event
                    {
                        type = type
                    };
                    Event.current = input;
                    ((EditorApplication.CallbackFunction)handler.GetValue(null)).Invoke();
                    Assert.That(input.type, Is.EqualTo(type));
                }
                TrackpadSettings.Current.Enabled = false;
                Event.current = new Event
                {
                    type = magnify
                };
                ((EditorApplication.CallbackFunction)handler.GetValue(null)).Invoke();
                Assert.That(Event.current.type, Is.EqualTo(magnify));
                TrackpadInput.Stop();
                TrackpadSettings.Current.Enabled = true;
                Event.current = new Event
                {
                    type = magnify
                };
                ((EditorApplication.CallbackFunction)handler.GetValue(null)).Invoke();
                Assert.That(UnityGestureGuard.Installed, Is.False);
                Assert.That(Event.current.type, Is.EqualTo(magnify));
                Assert.That(observed, Is.EqualTo(7), "Removing our hook must preserve other subscribers");
            }
            finally
            {
                handler.SetValue(null, Delegate.Remove((Delegate)handler.GetValue(null), otherHandler));
                Event.current = previousEvent;
                allowEvent.SetValue(null, allowed);
                TrackpadSettings.Current.Enabled = enabled;
                if (running)
                {
                    TrackpadInput.Start();
                }
                else
                {
                    TrackpadInput.Stop();
                }
            }
        }
    }
}
