using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TrackpadNavigation.Tests
{
    public sealed class BuilderScrollZoomTests
    {
        [Test]
        public void PreferencesPreserveScrollStepAndSanitizeInvalidValues()
        {
            var settings = TrackpadPreferences.FromJson("{}");
            Assert.That(settings.BuilderZoomStepSize, Is.EqualTo(BuilderScrollZoom.DefaultStep));
            settings.BuilderZoomStepSize = 0.025f;
            Assert.That(TrackpadPreferences.FromJson(JsonUtility.ToJson(settings)).BuilderZoomStepSize, Is.EqualTo(0.025f));
            foreach (float invalid in new[]
            {
                float.NaN, float.PositiveInfinity, float.NegativeInfinity
            })
            {
                settings.BuilderZoomStepSize = invalid;
                settings.Validate();
                Assert.That(settings.BuilderZoomStepSize, Is.EqualTo(BuilderScrollZoom.DefaultStep));
            }
            settings.BuilderZoomStepSize = -1;
            settings.Validate();
            Assert.That(settings.BuilderZoomStepSize, Is.EqualTo(0.001f));
            settings.BuilderZoomStepSize = 2;
            settings.Validate();
            Assert.That(settings.BuilderZoomStepSize, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator StandardZoomLevelsUpdateAndRestoreWithoutChangingTheViewport()
        {
            var type = Type.GetType("Unity.UI.Builder.Builder, UnityEditor.UIBuilderModule");
            if (type == null)
            {
                Assert.Ignore("UI Builder is unavailable.");
            }
            var settings = TrackpadSettings.Current;
            string originalSettings = JsonUtility.ToJson(settings);
            var existing = Resources.FindObjectsOfTypeAll(type).OfType<EditorWindow>().FirstOrDefault();
            var window = existing != null ? existing : (EditorWindow)ScriptableObject.CreateInstance(type);
            try
            {
                settings.Enabled = false;
                BuilderScrollZoom.Refresh();
                window.Show();
                yield return null;
                var owner = window.rootVisualElement.Query<VisualElement>().Where(element => element.GetType().FullName == "Unity.UI.Builder.BuilderViewport").First();
                Assert.That(owner, Is.Not.Null);
                var zoomer = EditorMember.Get(owner, "zoomer");
                var values = (List<float>)EditorMember.Get(zoomer, "zoomScaleValues");
                var original = values.ToArray();
                var originalScale = EditorMember.Get(owner, "zoomScale");
                var originalOffset = EditorMember.Get(owner, "contentOffset");
                var menu = ((List<float>)EditorMember.Get(zoomer, "zoomMenuScaleValues")).ToArray();
                var calculate = zoomer.GetType().GetMethod("CalculateNewZoom", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(calculate, Is.Not.Null);

                settings.Enabled = settings.GraphIntegration = true;
                settings.BuilderZoomStepSize = 0.025f;
                BuilderScrollZoom.Refresh();
                Assert.That(NextZoom(calculate, current: 1, delta: 1, values), Is.EqualTo(1.025f).Within(0.000001f));
                Assert.That(NextZoom(calculate, current: 1, delta: -1, values), Is.EqualTo(0.975f).Within(0.000001f));
                Assert.That(NextZoom(calculate, current: 1, delta: 0, values), Is.EqualTo(1));

                foreach (float step in new[]
                {
                    0.001f, 0.035f, 0.05f, 0.125f, 1f
                })
                {
                    settings.BuilderZoomStepSize = step;
                    BuilderScrollZoom.Refresh();
                    Assert.That(values.First(), Is.EqualTo(original.First()));
                    Assert.That(values.Last(), Is.EqualTo(original.Last()));
                    Assert.That(values, Does.Contain(1f));
                    for (int index = 1; index < values.Count; ++index)
                    {
                        Assert.That(values[index], Is.GreaterThan(values[index - 1]));
                    }
                    Assert.That(NextZoom(calculate, current: values.First(), delta: -1, values), Is.EqualTo(values.First()));
                    Assert.That(NextZoom(calculate, current: values.Last(), delta: 1, values), Is.EqualTo(values.Last()));
                }

                settings.BuilderZoomStepSize = BuilderScrollZoom.DefaultStep;
                BuilderScrollZoom.Refresh();
                CollectionAssert.AreEqual(original, values);
                settings.BuilderZoomStepSize = 0.025f;
                BuilderScrollZoom.Refresh();
                settings.GraphIntegration = false;
                BuilderScrollZoom.Refresh();
                CollectionAssert.AreEqual(original, values);
                settings.GraphIntegration = true;
                BuilderScrollZoom.Refresh();
                settings.Enabled = false;
                BuilderScrollZoom.Refresh();
                CollectionAssert.AreEqual(original, values);
                settings.Enabled = true;
                BuilderScrollZoom.Refresh();
                BuilderScrollZoom.Restore();
                CollectionAssert.AreEqual(original, values);
                Assert.That(EditorMember.Get(owner, "zoomScale"), Is.EqualTo(originalScale));
                Assert.That(EditorMember.Get(owner, "contentOffset"), Is.EqualTo(originalOffset));
                CollectionAssert.AreEqual(menu, (List<float>)EditorMember.Get(zoomer, "zoomMenuScaleValues"));

                BuilderScrollZoom.Refresh();
                var parent = owner.parent;
                int sibling = parent.IndexOf(owner);
                owner.RemoveFromHierarchy();
                try
                {
                    CollectionAssert.AreEqual(original, values);
                }
                finally
                {
                    parent.Insert(sibling, owner);
                }
                BuilderScrollZoom.Refresh();
                Assert.That(NextZoom(calculate, current: 1, delta: 1, values), Is.EqualTo(1.025f).Within(0.000001f));
            }
            finally
            {
                BuilderScrollZoom.Restore();
                if (existing == null)
                {
                    window.Close();
                }
                JsonUtility.FromJsonOverwrite(originalSettings, settings);
                BuilderScrollZoom.Refresh();
            }
        }

        static float NextZoom(MethodInfo calculate, float current, float delta, List<float> values)
        {
            return (float)calculate.Invoke(null, new object[]
            {
                current, delta, values
            });
        }
    }
}
