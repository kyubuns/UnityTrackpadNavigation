using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrackpadNavigation
{
    [InitializeOnLoad]
    internal static class BuilderScrollZoom
    {
        internal const float DefaultStep = 0.05f;
        static readonly Dictionary<VisualElement, ZoomSteps> Overrides = new();
        static double nextRefresh;

        sealed class ZoomSteps
        {
            internal readonly List<float> Values;
            readonly float[] original;
            float currentStep = DefaultStep;

            internal ZoomSteps(List<float> values)
            {
                Values = values;
                original = values.ToArray();
            }

            internal void Apply(float step)
            {
                if (currentStep == step)
                {
                    return;
                }
                currentStep = step;
                Values.Clear();
                if (step == DefaultStep)
                {
                    Values.AddRange(original);
                    return;
                }

                // 標準の段階を補間し、拡大率ごとの刻みの違いと上下限を維持する。
                // 100%を起点にするため、標準のリセット位置からも同じ幅で拡縮できる。
                int reference = Array.IndexOf(original, 1f);
                if (reference < 0)
                {
                    reference = 0;
                }
                double stride = (double)step / DefaultStep;
                int first = -(int)Math.Floor(reference / stride);
                int last = (int)Math.Ceiling((original.Length - 1 - reference) / stride);
                Values.Add(original[0]);
                for (int offset = first; offset < last; ++offset)
                {
                    double index = reference + offset * stride;
                    int lower = (int)index;
                    float value = Mathf.Lerp(original[lower], original[lower + 1], (float)(index - lower));
                    if (value > Values[Values.Count - 1] && value < original[original.Length - 1])
                    {
                        Values.Add(value);
                    }
                }
                Values.Add(original[original.Length - 1]);
            }

            internal void Restore()
            {
                Values.Clear();
                Values.AddRange(original);
            }
        }

        static BuilderScrollZoom()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Restore;
            EditorApplication.quitting += Restore;
        }

        static void Update()
        {
            if (EditorApplication.timeSinceStartup < nextRefresh || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }
            nextRefresh = EditorApplication.timeSinceStartup + 0.5;
            Refresh();
        }

        internal static void Refresh()
        {
            var settings = TrackpadSettings.Current;
            if (!settings.Enabled || !settings.GraphIntegration)
            {
                Restore();
                return;
            }
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window.GetType().FullName != "Unity.UI.Builder.Builder")
                {
                    continue;
                }
                window.rootVisualElement.Query<VisualElement>().Where(element => element.GetType().FullName == "Unity.UI.Builder.BuilderViewport").ForEach(viewport =>
                {
                    var zoomer = EditorMember.Get(viewport, "zoomer");
                    if (!(EditorMember.Get(zoomer, "zoomScaleValues") is List<float> values) || values.Count < 2)
                    {
                        return;
                    }
                    if (Overrides.TryGetValue(viewport, out var steps) && !ReferenceEquals(steps.Values, values))
                    {
                        Remove(viewport);
                        steps = null;
                    }
                    if (steps == null)
                    {
                        steps = new ZoomSteps(values);
                        Overrides.Add(viewport, steps);
                        viewport.RegisterCallback<DetachFromPanelEvent>(OnDetached);
                    }
                    steps.Apply(settings.BuilderZoomStepSize);
                });
            }
        }

        static void OnDetached(DetachFromPanelEvent evt)
        {
            if (evt.target is VisualElement viewport)
            {
                Remove(viewport);
            }
        }

        static void Remove(VisualElement viewport)
        {
            if (Overrides.TryGetValue(viewport, out var steps))
            {
                steps.Restore();
                viewport.UnregisterCallback<DetachFromPanelEvent>(OnDetached);
                Overrides.Remove(viewport);
            }
        }

        internal static void Restore()
        {
            foreach (var entry in Overrides)
            {
                entry.Value.Restore();
                entry.Key.UnregisterCallback<DetachFromPanelEvent>(OnDetached);
            }
            Overrides.Clear();
        }
    }
}
