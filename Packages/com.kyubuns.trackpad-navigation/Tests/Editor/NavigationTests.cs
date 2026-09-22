using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;

namespace TrackpadNavigation.Tests
{
    public sealed class NavigationTests
    {
        [Test]
        public void NativeAbiHasExpectedLayout()
        {
            Assert.That(Marshal.SizeOf<TrackpadEvent>(), Is.EqualTo(96));
            Assert.That(Marshal.OffsetOf<TrackpadEvent>(nameof(TrackpadEvent.Kind)).ToInt32(), Is.EqualTo(64));
            Assert.That(Marshal.SizeOf<NativeCapture>(), Is.EqualTo(48));
            Assert.That(Marshal.SizeOf<NativePointer>(), Is.EqualTo(24));
            Assert.That(Marshal.SizeOf<NativeStats>(), Is.EqualTo(32));
        }

        [Test]
        public void PanPreservesFractionalDeltasAndSystemDirection()
        {
            var value = new TrackpadEvent
            {
                DeltaX = 0.125,
                DeltaY = -0.25,
                Flags = GestureFlags.Natural
            };
            var settings = new TrackpadPreferences
            {
                PanSensitivity = 2
            };
            Assert.That(NavigationMath.Pan(value, settings), Is.EqualTo(new Vector2(0.25f, -0.5f)), "AppKit already applies natural scrolling");
            settings.InvertNaturalScrolling = true;
            Assert.That(NavigationMath.Pan(value, settings), Is.EqualTo(new Vector2(-0.25f, 0.5f)));
        }

        [Test]
        public void MomentumKeepsTheLastPhysicalScrollAction()
        {
            foreach (var (modifiers, expectedAction) in new[]
            {
                (GestureModifiers.None, SceneGesture.Pan),
                (GestureModifiers.Option, SceneGesture.Orbit),
                (GestureModifiers.Command, SceneGesture.Look)
            })
            {
                var value = new TrackpadEvent
                {
                    Kind = GestureKind.Scroll,
                    Phase = GesturePhase.Began,
                    Modifiers = modifiers
                };
                var action = NavigationMath.SceneAction(value, SceneGesture.Pan);
                Assert.That(action, Is.EqualTo(expectedAction));

                value.Phase = GesturePhase.Ended;
                value.Modifiers = modifiers == GestureModifiers.None ? GestureModifiers.Command : GestureModifiers.None;
                action = NavigationMath.SceneAction(value, action);
                Assert.That(action, Is.EqualTo(expectedAction), "Releasing a modifier with the fingers must preserve the last action");

                value.Phase = GesturePhase.None;
                foreach (var phase in new[]
                {
                    GesturePhase.Began,
                    GesturePhase.Changed,
                    GesturePhase.Ended
                })
                {
                    value.MomentumPhase = phase;
                    action = NavigationMath.SceneAction(value, action);
                    Assert.That(action, Is.EqualTo(expectedAction), "Momentum must not switch action when modifiers change");
                }

                value.MomentumPhase = GesturePhase.None;
                value.Phase = GesturePhase.Began;
                value.Modifiers = GestureModifiers.None;
                Assert.That(NavigationMath.SceneAction(value, action), Is.EqualTo(SceneGesture.Pan), "A new gesture must use the current modifiers");
            }
        }

        [Test]
        public void ZoomIsReversibleAndPreservesThePointUnderCursor()
        {
            var settings = new TrackpadPreferences
            {
                ZoomSensitivity = 2
            };
            var value = new TrackpadEvent
            {
                Magnification = 0.12
            };
            float factor = NavigationMath.ZoomFactor(value, settings);
            value.Magnification = -value.Magnification;
            Assert.That(factor * NavigationMath.ZoomFactor(value, settings), Is.EqualTo(1).Within(0.000001));
            var translation = new Vector2(-32.5f, 40);
            var anchor = new Vector2(361, 218);
            const float scale = 0.8f;
            var moved = NavigationMath.ZoomTranslation(translation, anchor, factor);
            Assert.That(Vector2.Distance((anchor - translation) / scale, (anchor - moved) / (scale * factor)), Is.LessThan(0.0001));
        }

        [Test]
        public void LookPreservesCameraPositionAndLevelHorizon()
        {
            var rotation = Quaternion.Euler(20, 30, 0);
            var camera = new Vector3(2, 3, 4);
            const float distance = 12;
            var nextRotation = NavigationMath.Rotate(rotation, new Vector2(25, -12), new TrackpadPreferences());
            var pivot = NavigationMath.PivotForLook(camera, nextRotation, distance);
            Assert.That(Vector3.Distance(camera, pivot - nextRotation * Vector3.forward * distance), Is.LessThan(0.00001));
            var pole = NavigationMath.Rotate(rotation, new Vector2(15, 10000), new TrackpadPreferences());
            Assert.That(Mathf.Abs((pole * Vector3.forward).y), Is.LessThan(1));
            Assert.That(Mathf.Abs((pole * Vector3.right).y), Is.LessThan(0.00001));
        }

        [Test]
        public void PreferencesKeepDefaultsAndSanitizeSensitivity()
        {
            var settings = TrackpadPreferences.FromJson("{\"PanSensitivity\":-1,\"ZoomSensitivity\":2.5}");
            Assert.That(settings.PanSensitivity, Is.EqualTo(0.05f));
            Assert.That(settings.ZoomSensitivity, Is.EqualTo(2.5f));
            Assert.That(settings.OrbitSensitivity, Is.EqualTo(1));
            Assert.That(settings.Enabled && settings.SceneIntegration && settings.GraphIntegration, Is.True);
            Assert.That(TrackpadPreferences.FromJson("broken json").PanSensitivity, Is.EqualTo(1));
        }
    }
}
