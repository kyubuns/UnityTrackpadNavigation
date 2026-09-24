using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TrackpadNavigation.Tests
{
    public sealed class VfxGraphZoomTests
    {
        sealed class OtherGraph : GraphView
        {
        }

        [Test]
        public void PreferencesPreserveScrollStepAndSanitizeInvalidValues()
        {
            var settings = TrackpadPreferences.FromJson("{}");
            Assert.That(settings.VfxZoomStepSize, Is.EqualTo(ContentZoomer.DefaultScaleStep));
            settings.VfxZoomStepSize = 0.025f;
            Assert.That(TrackpadPreferences.FromJson(JsonUtility.ToJson(settings)).VfxZoomStepSize, Is.EqualTo(0.025f));
            settings.VfxZoomStepSize = float.NaN;
            settings.Validate();
            Assert.That(settings.VfxZoomStepSize, Is.EqualTo(ContentZoomer.DefaultScaleStep));
            settings.VfxZoomStepSize = -1;
            settings.Validate();
            Assert.That(settings.VfxZoomStepSize, Is.EqualTo(0.001f));
        }

        [UnityTest]
        public IEnumerator VfxWindowUpdatesRestoresAndLeavesOtherGraphsAlone()
        {
            var type = Type.GetType("UnityEditor.VFX.UI.VFXViewWindow, Unity.VisualEffectGraph.Editor");
            if (type == null)
            {
                Assert.Ignore("VFX Graph is not installed.");
            }
            var settings = TrackpadSettings.Current;
            string originalSettings = JsonUtility.ToJson(settings);
            EditorWindow window = null;
            try
            {
                settings.Enabled = false;
                VfxGraphZoom.Refresh();
                window = (EditorWindow)ScriptableObject.CreateInstance(type);
                window.Show();
                yield return null;
                var graph = window.rootVisualElement.Q<GraphView>();
                Assert.That(graph, Is.Not.Null);
                float originalStep = graph.scaleStep;
                float minimum = graph.minScale;
                float maximum = graph.maxScale;
                float reference = graph.referenceScale;
                var other = new OtherGraph();
                other.SetupZoom(0.1f, 4, 0.3f, 1);
                window.rootVisualElement.Add(other);

                settings.Enabled = settings.GraphIntegration = true;
                settings.VfxZoomStepSize = 0.025f;
                VfxGraphZoom.Refresh();
                Assert.That(graph.scaleStep, Is.EqualTo(0.025f));
                Assert.That(other.scaleStep, Is.EqualTo(0.3f));
                Assert.That(graph.minScale, Is.EqualTo(minimum));
                Assert.That(graph.maxScale, Is.EqualTo(maximum));
                Assert.That(graph.referenceScale, Is.EqualTo(reference));

                settings.VfxZoomStepSize = 0.05f;
                VfxGraphZoom.Refresh();
                Assert.That(graph.scaleStep, Is.EqualTo(0.05f));
                settings.GraphIntegration = false;
                VfxGraphZoom.Refresh();
                Assert.That(graph.scaleStep, Is.EqualTo(originalStep));
                settings.GraphIntegration = true;
                VfxGraphZoom.Refresh();
                settings.Enabled = false;
                VfxGraphZoom.Refresh();
                Assert.That(graph.scaleStep, Is.EqualTo(originalStep));

                settings.Enabled = true;
                VfxGraphZoom.Refresh();
                graph.RemoveFromHierarchy();
                Assert.That(graph.scaleStep, Is.EqualTo(originalStep));
                window.rootVisualElement.Add(graph);
                VfxGraphZoom.Refresh();
                Assert.That(graph.scaleStep, Is.EqualTo(0.05f));
                VfxGraphZoom.Restore();
                Assert.That(graph.scaleStep, Is.EqualTo(originalStep));
            }
            finally
            {
                if (window != null)
                {
                    window.Close();
                }
                JsonUtility.FromJsonOverwrite(originalSettings, settings);
                VfxGraphZoom.Refresh();
            }
        }
    }
}
