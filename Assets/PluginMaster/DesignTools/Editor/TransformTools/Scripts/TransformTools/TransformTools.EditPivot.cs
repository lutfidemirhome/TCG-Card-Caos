/*
Copyright (c) Omar Duarte
Unauthorized copying of this file, via any medium is strictly prohibited.
Writen by Omar Duarte.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/
#pragma warning disable UDR0001

using System.Linq;
using UnityEngine;
namespace PluginMaster
{
    public static partial class TransformTools
    {
        private const string EDIT_PIVOT_COMMAND_NAME = "Edit Pivot";
        private const string COLLIDER_CHILD_NAME = "DO NOT DELETE OR RENAME. Colliders from pivot rotation.";
        private static GameObject _pivot = null;

        public static GameObject StartEditingPivot(GameObject target)
        {
            if (target == null || target.scene.rootCount == 0) return null;
            var pivot = new GameObject("Pivot");
            UnityEditor.Tools.current = UnityEditor.Tool.Move;
            pivot.transform.SetParent(target.transform);
            pivot.transform.localPosition = Vector3.zero;
            pivot.transform.localRotation = Quaternion.identity;
            pivot.transform.localScale = Vector3.one;
            UnityEditor.Selection.activeGameObject = pivot;
            _pivot = pivot;
            return pivot;
        }
        public static void SaveMeshFilterMesh(MeshFilter meshFilter, string savePath,
            Transform pivot, System.Collections.Generic.List<Transform> otherObjects)
        {
            if (meshFilter == null) return;
            var mesh = meshFilter.sharedMesh;
            if (mesh == null) return;
            var originalPath = UnityEditor.AssetDatabase.GetAssetPath(mesh);
            var target = meshFilter.transform;
            var otherFilters = new System.Collections.Generic.List<MeshFilter>();
            if (!string.IsNullOrEmpty(UnityEditor.AssetDatabase.GetAssetPath(mesh)))
            {
                mesh = UnityEngine.Object.Instantiate(mesh);
#if UNITY_6000_4_OR_NEWER
                var allFilters = Object.FindObjectsByType<MeshFilter>();
#elif UNITY_2022_2_OR_NEWER
                var allFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
#else
                var allFilters = Object.FindObjectsOfType<MeshFilter>();
#endif
                foreach (var filter in allFilters)
                {
                    if (filter == meshFilter) continue;
                    var path = UnityEditor.AssetDatabase.GetAssetPath(filter.sharedMesh);
                    if (path != savePath) continue;
                    otherFilters.Add(filter);
                    if (!otherObjects.Contains(filter.transform)) otherObjects.Add(filter.transform);
                }
            }
            if (originalPath == savePath)
            {
                UnityEditor.EditorUtility.CopySerialized(mesh, meshFilter.sharedMesh);
                mesh = meshFilter.sharedMesh;
            }
            else
            {
                UnityEditor.AssetDatabase.DeleteAsset(savePath);
                UnityEditor.AssetDatabase.Refresh();
                UnityEditor.AssetDatabase.CreateAsset(mesh, savePath);
            }
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.Undo.RecordObject(meshFilter, EDIT_PIVOT_COMMAND_NAME);
            meshFilter.sharedMesh = mesh;
            foreach (var filter in otherFilters)
            {
                UnityEditor.Undo.RecordObject(filter, EDIT_PIVOT_COMMAND_NAME);
                filter.sharedMesh = null;
                filter.sharedMesh = mesh;
            }
        }
        public static void MoveChildren(Transform pivot)
        {
            var target = pivot.parent;
            var delta = pivot.transform.localPosition;
            foreach (Transform child in target)
            {
                UnityEditor.Undo.RecordObject(child, EDIT_PIVOT_COMMAND_NAME);
                child.transform.localPosition -= delta;
            }
            UnityEditor.Undo.RecordObject(target, EDIT_PIVOT_COMMAND_NAME);
            target.position += target.TransformVector(delta);
        }


        public static void SaveSkinnedMeshRendererMesh(SkinnedMeshRenderer renderer, string savePath,
            Transform pivot, System.Collections.Generic.List<Transform> otherObjects)
        {
            if (renderer == null) return;
            var mesh = renderer.sharedMesh;
            if (mesh == null) return;
            var originalPath = UnityEditor.AssetDatabase.GetAssetPath(mesh);
            var target = renderer.transform;
            var otherRenderers = new System.Collections.Generic.List<SkinnedMeshRenderer>();
            if (!string.IsNullOrEmpty(UnityEditor.AssetDatabase.GetAssetPath(mesh)))
            {
                mesh = UnityEngine.Object.Instantiate(mesh);
#if UNITY_6000_4_OR_NEWER
                var allRenderers = Object.FindObjectsByType<SkinnedMeshRenderer>();
#elif UNITY_2022_2_OR_NEWER
                var allRenderers = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
#else
                var allRenderers = Object.FindObjectsOfType<SkinnedMeshRenderer>();
#endif
                foreach (var skinnedRenderer in allRenderers)
                {
                    if (skinnedRenderer == renderer) continue;
                    var path = UnityEditor.AssetDatabase.GetAssetPath(skinnedRenderer.sharedMesh);
                    if (path != savePath) continue;
                    otherRenderers.Add(skinnedRenderer);
                    if (!otherObjects.Contains(skinnedRenderer.transform)) otherObjects.Add(skinnedRenderer.transform);
                }
            }
            if (originalPath == savePath)
            {
                UnityEditor.EditorUtility.CopySerialized(mesh, renderer.sharedMesh);
                mesh = renderer.sharedMesh;
            }
            else UnityEditor.AssetDatabase.CreateAsset(mesh, savePath);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.Undo.RecordObject(renderer, EDIT_PIVOT_COMMAND_NAME);
            renderer.sharedMesh = mesh;
            foreach (var otherRenderer in otherRenderers)
            {
                UnityEditor.Undo.RecordObject(otherRenderer, EDIT_PIVOT_COMMAND_NAME);
                otherRenderer.sharedMesh = null;
                otherRenderer.sharedMesh = mesh;
            }
        }

        public static void UpdateOtherObjects(System.Collections.Generic.List<Transform> otherObjects,
            Transform pivot, string orignalMeshPath)
        {
            foreach (var target in otherObjects)
            {
                EditColliders(target, pivot, orignalMeshPath);
                EditNavMeshObject(target, pivot);
                EditPivotPositionAndRotation(target, pivot);
            }
        }


        private static void EditSprite(SpriteRenderer renderer, Transform pivot)
        {
            var rect = renderer.sprite.rect;
            var pixelsPerUnit = renderer.sprite.pixelsPerUnit;
            var min = renderer.transform.InverseTransformPoint(renderer.bounds.min);
            var pivot2D = new Vector2((pivot.localPosition.x - min.x) * pixelsPerUnit
                / rect.width, (pivot.localPosition.y - min.y) * pixelsPerUnit / rect.height);

            var path = UnityEditor.AssetDatabase.GetAssetPath(renderer.sprite);
            UnityEditor.TextureImporter textureImporter
                = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
            UnityEditor.Undo.RecordObject(textureImporter, EDIT_PIVOT_COMMAND_NAME);
            var settings = new UnityEditor.TextureImporterSettings();
            textureImporter.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot2D;
            textureImporter.SetTextureSettings(settings);
            UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceUpdate);
#if UNITY_6000_4_OR_NEWER
            var allRenderers = Object.FindObjectsByType<SpriteRenderer>();
#elif UNITY_2022_2_OR_NEWER
            var allRenderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
#else
            var allRenderers = Object.FindObjectsOfType<SpriteRenderer>();
#endif
            foreach (var r in allRenderers)
            {
                if (r == renderer) continue;
                if (r.sprite != renderer.sprite) continue;
                EditColliders(r.transform, pivot, null);
                EditPivotPositionAndRotation(r.transform, pivot);
            }
        }

        private static void EditRectTransform(RectTransform transform, Transform pivot)
        {
            var localPivot = transform.InverseTransformPoint(pivot.position);
            var rect = transform.rect;
            transform.pivot = new Vector2((localPivot.x - rect.min.x) / rect.width,
                (localPivot.y - rect.min.y) / rect.height);
        }

        private static void EditMesh(Mesh mesh, Transform pivot)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var tangents = mesh.tangents;

            if (pivot.localPosition != Vector3.zero)
            {
                for (int i = 0; i < vertices.Length; ++i) vertices[i] -= pivot.localPosition;
            }

            if (pivot.localEulerAngles != Vector3.zero)
            {
                var invRot = Quaternion.Inverse(pivot.localRotation);
                for (int i = 0; i < vertices.Length; ++i)
                {
                    vertices[i] = invRot * vertices[i];
                    normals[i] = invRot * normals[i];
                    var tanDir = invRot * tangents[i];
                    tangents[i] = new Vector4(tanDir.x, tanDir.y, tanDir.z, tangents[i].w);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.RecalculateBounds();
        }

        private static void EditColliders(Transform target, Transform pivot, string originalMeshPath)
        {
            var meshFilter = target.GetComponent<MeshFilter>();
            var meshColliders = target.GetComponents<MeshCollider>();
            if (meshFilter != null)
            {
                foreach (var collider in meshColliders)
                {
                    if (collider.sharedMesh == null) continue;
                    var meshPath = UnityEditor.AssetDatabase.GetAssetPath(collider.sharedMesh);
                    if (meshPath != originalMeshPath) continue;
                    UnityEditor.Undo.RecordObject(collider, EDIT_PIVOT_COMMAND_NAME);
                    collider.sharedMesh = meshFilter.sharedMesh;
                }
            }

            var colliders2D = target.GetComponents<Collider2D>();
            foreach (var collider in colliders2D)
            {
                UnityEditor.Undo.RecordObject(collider, EDIT_PIVOT_COMMAND_NAME);
                collider.offset -= (Vector2)pivot.localPosition;
            }
            GameObject colliderChild = null;
            var colliderChildTransform = target.Find(COLLIDER_CHILD_NAME);
            if (colliderChildTransform != null)
            {
                colliderChild = colliderChildTransform.gameObject;
                if (colliderChild.GetComponent<Collider>() != null) return;
            }

            var boxColliders = target.GetComponents<BoxCollider>();
            var capsuleColliders = target.GetComponents<CapsuleCollider>();
            var sphereColliders = target.GetComponents<SphereCollider>();
            var wheelColliders = target.GetComponents<WheelCollider>();

            var nativeColliderCount = boxColliders.Length + capsuleColliders.Length
                + sphereColliders.Length + wheelColliders.Length;

            if (nativeColliderCount == 0) return;

            var addToChild = colliderChild != null || pivot.localRotation != Quaternion.identity;

            if (addToChild && colliderChild == null) colliderChild = new GameObject(COLLIDER_CHILD_NAME);

            void CloneCollider(Collider source, Collider dest)
            {
                dest.transform.rotation = target.rotation;
                UnityEditor.EditorUtility.CopySerialized(source, dest);
                source.enabled = false;
            }

            foreach (var collider in boxColliders)
            {
                UnityEditor.Undo.RecordObject(collider, EDIT_PIVOT_COMMAND_NAME);
                collider.center -= pivot.localPosition;
                if (addToChild)
                {
                    var clone = colliderChild.AddComponent<BoxCollider>();
                    CloneCollider(collider, clone);
                    collider.size = Vector3.zero;
                }
            }
            foreach (var collider in capsuleColliders)
            {
                UnityEditor.Undo.RecordObject(collider, EDIT_PIVOT_COMMAND_NAME);
                collider.center -= pivot.localPosition;
                if (addToChild)
                {
                    var clone = colliderChild.AddComponent<CapsuleCollider>();
                    CloneCollider(collider, clone);
                    collider.radius = 0;
                    collider.height = 0;
                }
            }
            foreach (var collider in sphereColliders)
            {
                UnityEditor.Undo.RecordObject(collider, EDIT_PIVOT_COMMAND_NAME);
                collider.center -= pivot.localPosition;
                if (addToChild)
                {
                    var clone = colliderChild.AddComponent<SphereCollider>();
                    CloneCollider(collider, clone);
                    collider.radius = 0;
                }
            }
            foreach (var collider in wheelColliders)
            {
                UnityEditor.Undo.RecordObject(collider, EDIT_PIVOT_COMMAND_NAME);
                collider.center -= pivot.localPosition;
                if (addToChild)
                {
                    var clone = colliderChild.AddComponent<WheelCollider>();
                    CloneCollider(collider, clone);
                    collider.radius = 0;
                }
            }

            if (colliderChild != null)
            {
                colliderChild.transform.localPosition = pivot.localPosition;
                UnityEditor.Undo.RegisterCreatedObjectUndo(colliderChild, EDIT_PIVOT_COMMAND_NAME);
                UnityEditor.Undo.SetTransformParent(colliderChild.transform, target, EDIT_PIVOT_COMMAND_NAME);
            }
        }

        private static void EditPivotPositionAndRotation(Transform target, Transform pivot)
        {
            var children = target.GetComponentsInChildren<Transform>();
            var childrenPosAndRot = children.Select(child => (child, child.position, child.rotation)).ToArray();
            target.position += target.TransformVector(pivot.localPosition);
            target.rotation *= pivot.localRotation;
            for (int i = 0; i < childrenPosAndRot.Length; ++i)
            {
                var child = childrenPosAndRot[i].child;
                if (child == target || child == pivot) continue;
                child.position = childrenPosAndRot[i].position;
                child.rotation = childrenPosAndRot[i].rotation;
            }
        }

        public static bool IsColliderChildNeeded(Transform target, Transform pivot)
        {
            if (target.Find(COLLIDER_CHILD_NAME) != null) return true;

            var boxColliders = target.GetComponents<BoxCollider>();
            var capsuleColliders = target.GetComponents<CapsuleCollider>();
            var sphereColliders = target.GetComponents<SphereCollider>();
            var wheelColliders = target.GetComponents<WheelCollider>();
            var nativeColliderCount = boxColliders.Length + capsuleColliders.Length
                + sphereColliders.Length + wheelColliders.Length;
            var obstacle = target.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            var pivotRotated = pivot.localRotation != Quaternion.identity;
            return pivotRotated && (nativeColliderCount > 0 || obstacle != null);
        }

        private static void EditNavMeshObject(Transform target, Transform pivot)
        {
            GameObject colliderChild = null;
            var colliderChildTransform = target.Find(COLLIDER_CHILD_NAME);
            if (colliderChildTransform != null)
            {
                colliderChild = colliderChildTransform.gameObject;
                if (colliderChild.GetComponent<UnityEngine.AI.NavMeshObstacle>() != null) return;
            }
            var obstacle = target.GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle == null) return;

            UnityEditor.Undo.RecordObject(obstacle, EDIT_PIVOT_COMMAND_NAME);
            obstacle.center -= pivot.localPosition;
            if (colliderChild == null && pivot.localRotation == Quaternion.identity) return;
            if (colliderChild == null) colliderChild = new GameObject(COLLIDER_CHILD_NAME);
            var clone = colliderChild.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            colliderChild.transform.rotation = target.rotation;
            UnityEditor.EditorUtility.CopySerialized(obstacle, clone);
            obstacle.enabled = false;
            obstacle.center = Vector3.zero;
            obstacle.size = Vector3.zero;
            colliderChild.transform.localPosition = pivot.localPosition;
            UnityEditor.Undo.RegisterCreatedObjectUndo(colliderChild, EDIT_PIVOT_COMMAND_NAME);
            UnityEditor.Undo.SetTransformParent(colliderChild.transform, target, EDIT_PIVOT_COMMAND_NAME);
        }

        public static void ApplyPivot(Transform pivot, string originalMeshPath)
        {
            var target = pivot.parent;
            var children = target.GetComponentsInChildren<Transform>();
            for (int i = 0; i < children.Length; ++i)
            {
                var child = children[i];
                if (child == pivot) continue;
                UnityEditor.Undo.RecordObject(child, EDIT_PIVOT_COMMAND_NAME);
            }
            var meshFilter = target.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                UnityEditor.Undo.RecordObject(meshFilter.sharedMesh, EDIT_PIVOT_COMMAND_NAME);
                EditMesh(meshFilter.sharedMesh, pivot);
            }

            var spriteRenderer = target.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.sprite != null) EditSprite(spriteRenderer, pivot);

            var rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform != null) EditRectTransform(rectTransform, pivot);

            EditColliders(target, pivot, originalMeshPath);
            EditNavMeshObject(target, pivot);
            EditPivotPositionAndRotation(target, pivot);
        }

        public static GameObject CreateCenteredPivot(Transform target)
        {
            if (target == null || target.gameObject.scene.rootCount == 0) return null;
            var bounds = BoundsUtils.GetBoundsRecursive(target);
            var pivot = new GameObject("Pivot");
            pivot.transform.SetParent(target.transform);
            pivot.transform.localRotation = Quaternion.identity;
            pivot.transform.localScale = Vector3.one;
            pivot.transform.position = bounds.center;
            _pivot = pivot;
            return pivot;
        }

        public static void CenterPivot(MeshFilter meshFilter, string savePath, GameObject pivot,
            System.Collections.Generic.List<Transform> otherObjects)
        {
            var target = meshFilter.transform;
            if (target == null || target.gameObject.scene.rootCount == 0) return;
            SaveMeshFilterMesh(meshFilter, savePath, pivot.transform, otherObjects);
            UnityEditor.Selection.activeObject = target.gameObject;
        }

        public static void CenterPivot(SkinnedMeshRenderer renderer,
            string savePath, GameObject pivot, System.Collections.Generic.List<Transform> otherObjects)
        {
            var target = renderer.transform;
            if (target == null || target.gameObject.scene.rootCount == 0) return;
            SaveSkinnedMeshRendererMesh(renderer, savePath, pivot.transform, otherObjects);
            UnityEditor.Selection.activeObject = target.gameObject;
        }

        public static void CenterPivot(Transform target)
        {
            if (target == null || target.gameObject.scene.rootCount == 0) return;
            var pivot = CreateCenteredPivot(target);
            ApplyPivot(pivot.transform, null);
            UnityEditor.Selection.activeObject = target.gameObject;
        }

        public static void DuringSceneGUI(UnityEditor.SceneView sceneView)
        {
            if (_pivot == null) return;
            var scale = UnityEditor.HandleUtility.GetHandleSize(_pivot.transform.position) * 0.2f;
            UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.4f);
            UnityEditor.Handles.SphereHandleCap(0, _pivot.transform.position, Quaternion.identity, scale, EventType.Repaint);
        }
    }
}
#pragma warning restore UDR0001
