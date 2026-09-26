using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace TrackpadNavigation
{
    internal static class ScenePicking
    {
        const string PickCommand = "TrackpadNavigation.PickObject";

        public static Vector3 PointOfInterest(SceneView view, Vector2 screenPoint)
        {
            var picked = Pick(view, screenPoint, out var ray);
            return picked && RaycastObject(picked, ray, out var hit) ? hit : view.pivot;
        }

        public static Vector3 ZoomAnchor(SceneView view, Vector2 screenPoint)
        {
            var picked = Pick(view, screenPoint, out var ray);
            if (!view.orthographic && picked && RaycastObject(picked, ray, out var hit))
            {
                return hit;
            }
            // 空白と平行投影ではpivotの深度面を使い、カーソル位置と表示倍率を対応させる。
            var plane = new Plane(view.rotation * Vector3.forward, view.pivot);
            return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : view.pivot;
        }

        public static void FrameAtPointer(SceneView view, Vector2 screenPoint)
        {
            var picked = Pick(view, screenPoint, out _);
            if (!picked)
            {
                return;
            }

            Selection.activeGameObject = picked;
            view.Focus();
            view.FrameSelected();
        }

        static GameObject Pick(SceneView view, Vector2 screenPoint, out Ray ray)
        {
            GameObject picked = null;
            Ray pickedRay = default;
            void Pick(SceneView current)
            {
                if (current != view || Event.current.type != EventType.ExecuteCommand || Event.current.commandName != PickCommand)
                {
                    return;
                }

                var guiPoint = GUIUtility.ScreenToGUIPoint(screenPoint);
                pickedRay = HandleUtility.GUIPointToWorldRay(guiPoint);
                picked = HandleUtility.PickGameObject(guiPoint, selectPrefabRoot: false);
                Event.current.Use();
            }
            // PickingはScene GUIのカメラ・座標系が必要。操作開始時だけ同期的に実行する。
            SceneView.duringSceneGui += Pick;
            try
            {
                view.SendEvent(EditorGUIUtility.CommandEvent(PickCommand));
            }
            finally
            {
                SceneView.duringSceneGui -= Pick;
            }
            ray = pickedRay;
            return picked;
        }

        static bool RaycastObject(GameObject picked, Ray ray, out Vector3 point)
        {
            var skin = picked.GetComponent<SkinnedMeshRenderer>();
            if (skin && skin.sharedMesh)
            {
                var baked = new Mesh();
                try
                {
                    skin.BakeMesh(baked);
                    return RaycastMesh(baked, skin.localToWorldMatrix, ray, out point);
                }
                finally
                {
                    Object.DestroyImmediate(baked);
                }
            }
            var filter = picked.GetComponent<MeshFilter>();
            if (filter && filter.sharedMesh)
            {
                return RaycastMesh(filter.sharedMesh, filter.transform.localToWorldMatrix, ray, out point);
            }

            // TerrainなどMeshを直接取得できない対象は、選択されたObjectのColliderだけを使う。
            point = default;
            float nearest = float.PositiveInfinity;
            foreach (var collider in picked.GetComponents<Collider>())
            {
                if (!collider.enabled || collider.isTrigger || !collider.Raycast(ray, out var hit, nearest))
                {
                    continue;
                }

                nearest = hit.distance;
                point = hit.point;
            }
            return !float.IsPositiveInfinity(nearest);
        }

        internal static bool RaycastMesh(Mesh mesh, Matrix4x4 localToWorld, Ray ray, out Vector3 point)
        {
            // Read/Write無効のimport済みMeshも読める公開Editor API。資産やColliderを変更しない。
            using var dataArray = MeshUtility.AcquireReadOnlyMeshData(mesh);
            var data = dataArray[0];
            using var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
            data.GetVertices(vertices);
            var inverse = localToWorld.inverse;
            var origin = inverse.MultiplyPoint3x4(ray.origin);
            var direction = inverse.MultiplyVector(ray.direction);
            float nearest = float.PositiveInfinity;
            for (int submesh = 0; submesh < data.subMeshCount; ++submesh)
            {
                var descriptor = data.GetSubMesh(submesh);
                if (descriptor.topology != MeshTopology.Triangles)
                {
                    continue;
                }

                using var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp);
                data.GetIndices(indices, submesh);
                for (int i = 0; i + 2 < indices.Length; i += 3)
                {
                    var a = vertices[indices[i]];
                    var edge1 = vertices[indices[i + 1]] - a;
                    var edge2 = vertices[indices[i + 2]] - a;
                    var cross = Vector3.Cross(direction, edge2);
                    float determinant = Vector3.Dot(edge1, cross);
                    if (Mathf.Abs(determinant) <= 0.000001f * edge1.magnitude * cross.magnitude)
                    {
                        continue;
                    }

                    var offset = origin - a;
                    float u = Vector3.Dot(offset, cross) / determinant;
                    var perpendicular = Vector3.Cross(offset, edge1);
                    float v = Vector3.Dot(direction, perpendicular) / determinant;
                    if (u < 0 || v < 0 || u + v > 1)
                    {
                        continue;
                    }

                    float distance = Vector3.Dot(edge2, perpendicular) / determinant;
                    if (distance >= 0 && distance < nearest)
                    {
                        nearest = distance;
                    }
                }
            }
            point = float.IsPositiveInfinity(nearest) ? default : ray.GetPoint(nearest);
            return !float.IsPositiveInfinity(nearest);
        }
    }
}
