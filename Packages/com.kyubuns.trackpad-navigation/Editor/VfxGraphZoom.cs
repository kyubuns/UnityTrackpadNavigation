using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrackpadNavigation
{
    [InitializeOnLoad]
    internal static class VfxGraphZoom
    {
        static readonly Dictionary<GraphView, float> OriginalSteps = new();
        static double nextRefresh;

        static VfxGraphZoom()
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

            // VFXへの依存を追加せず、開き直したGraphも検出する。Shader Graph等には触れない。
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (window.GetType().FullName != "UnityEditor.VFX.UI.VFXViewWindow")
                {
                    continue;
                }
                window.rootVisualElement.Query<GraphView>().ForEach(graph =>
                {
                    if (graph.GetType().FullName == "UnityEditor.VFX.UI.VFXView")
                    {
                        if (!OriginalSteps.ContainsKey(graph))
                        {
                            OriginalSteps.Add(graph, graph.scaleStep);
                            graph.RegisterCallback<DetachFromPanelEvent>(OnDetached);
                        }
                        SetStep(graph, settings.VfxZoomStepSize);
                    }
                });
            }
        }

        static void OnDetached(DetachFromPanelEvent evt)
        {
            if (evt.target is GraphView graph && OriginalSteps.TryGetValue(graph, out float step))
            {
                SetStep(graph, step);
                graph.UnregisterCallback<DetachFromPanelEvent>(OnDetached);
                OriginalSteps.Remove(graph);
            }
        }

        internal static void Restore()
        {
            foreach (var entry in OriginalSteps)
            {
                SetStep(entry.Key, entry.Value);
                entry.Key.UnregisterCallback<DetachFromPanelEvent>(OnDetached);
            }
            OriginalSteps.Clear();
        }

        static void SetStep(GraphView graph, float step)
        {
            if (graph.scaleStep != step)
            {
                graph.SetupZoom(graph.minScale, graph.maxScale, step, graph.referenceScale);
            }
        }
    }
}
