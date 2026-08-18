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
    public static class GravityUtils
    {

        private static bool _isPlaying = false;
        private static bool _stop = false;


        private static void AddCollider(GameObject obj, Mesh mesh, SimulateGravityData data)
        {
            var collider = MeshUtils.AddCollider(mesh, obj);
            if (collider is MeshCollider) (collider as MeshCollider).convex = true;
            if (data.changeLayer) obj.layer = data.tempLayer;
        }
        private static bool IsVisible(GameObject obj)
        {
            if (obj == null) return false;
            var target = obj;
            var parentRenderer = target.GetComponentInParent<Renderer>();
            var parentTerrain = target.GetComponentInParent<Terrain>();
            if (parentRenderer != null) target = parentRenderer.gameObject;
            else if (parentTerrain != null) target = parentTerrain.gameObject;
            else
            {
                var parent = target.transform.parent;
                if (parent != null)
                {
                    var siblingRenderer = parent.GetComponentInChildren<Renderer>();
                    var siblingTerrain = parent.GetComponentInChildren<Terrain>();
                    if (siblingRenderer != null) target = parent.gameObject;
                    else if (siblingTerrain != null) target = parent.gameObject;

                }
            }
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                foreach (var renderer in renderers)
                    if (renderer.enabled) return true;
            }
            var terrains = target.GetComponentsInChildren<Terrain>();
            if (terrains.Length > 0)
            {
                foreach (var terrain in terrains)
                    if (terrain.enabled) return true;
            }
            return false;
        }
        public static Pose[] SimulateGravity(GameObject[] selection, SimulateGravityData simData, bool recordAction)
        {

            if (_isPlaying && recordAction) return null;
            System.Collections.Generic.List<Collider> tempColliders = null;

            Collider[] sceneColliders = null;
            Collider[] invisibleColliders = null;

            if (simData.ignoreSceneColliders)
            {
#if UNITY_6000_4_OR_NEWER
                sceneColliders = Object.FindObjectsByType<Collider>()
                   .Where(sc => sc.enabled && sc.gameObject.activeInHierarchy && !sc.isTrigger).ToArray();
#elif UNITY_2022_2_OR_NEWER
                sceneColliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                    .Where(sc => sc.enabled && sc.gameObject.activeInHierarchy && !sc.isTrigger).ToArray();
#else
                sceneColliders = Object.FindObjectsOfType<Collider>()
                    .Where(sc => sc.enabled && sc.gameObject.activeInHierarchy && !sc.isTrigger).ToArray();
#endif
                foreach (var sceneCollider in sceneColliders) sceneCollider.enabled = false;
#if UNITY_6000_4_OR_NEWER
                var sceneMeshFilters = Object.FindObjectsByType<MeshFilter>()
                    .Where(mf => mf.gameObject.activeInHierarchy && mf.sharedMesh != null).ToArray();
#elif UNITY_2022_2_OR_NEWER
                var sceneMeshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                    .Where(mf => mf.gameObject.activeInHierarchy && mf.sharedMesh != null).ToArray();
#else
                var sceneMeshFilters = Object.FindObjectsOfType<MeshFilter>()
                    .Where(mf => mf.gameObject.activeInHierarchy && mf.sharedMesh != null).ToArray();
#endif
                tempColliders = new System.Collections.Generic.List<Collider>();
                foreach (var meshFilter in sceneMeshFilters)
                {
                    var mesh = meshFilter.sharedMesh;
                    Collider tempCollider = MeshUtils.AddCollider(meshFilter.sharedMesh, meshFilter.gameObject);
                    if (tempCollider != null) tempColliders.Add(tempCollider);
                }
            }
            else
            {
#if UNITY_6000_4_OR_NEWER
                invisibleColliders = Object.FindObjectsByType<Collider>()
                  .Where(sc => sc.enabled && sc.gameObject.activeInHierarchy && !IsVisible(sc.gameObject)).ToArray();
#elif UNITY_2022_2_OR_NEWER
                invisibleColliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                  .Where(sc => sc.enabled && sc.gameObject.activeInHierarchy && !IsVisible(sc.gameObject)).ToArray();
#else
                invisibleColliders = Object.FindObjectsOfType<Collider>()
                  .Where(sc => sc.enabled && sc.gameObject.activeInHierarchy && !IsVisible(sc.gameObject)).ToArray();
#endif
                foreach (var invisibleCollider in invisibleColliders) invisibleCollider.enabled = false;
            }
            var originalGravity = Physics.gravity;
            Physics.gravity = simData.gravity;
#if UNITY_6000_4_OR_NEWER
            var allBodies = Object.FindObjectsByType<Rigidbody>();
#elif UNITY_2022_2_OR_NEWER
            var allBodies = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
#else
            var allBodies = Object.FindObjectsOfType<Rigidbody>();
#endif
            var originalBodies = new System.Collections.Generic.List<(Rigidbody body, bool useGravity, bool isKinematic,
                float drag, float angularDrag, float mass, RigidbodyConstraints constraints,
                RigidbodyInterpolation interpolation, CollisionDetectionMode collisionDetectionMode)>();
            foreach (var rigidBody in allBodies)
            {
                originalBodies.Add((rigidBody, rigidBody.useGravity, rigidBody.isKinematic,
#if UNITY_6000_0_OR_NEWER
                     rigidBody.linearDamping, rigidBody.angularDamping,
#else
                    rigidBody.drag, rigidBody.angularDrag,
#endif
                    rigidBody.mass, rigidBody.constraints, rigidBody.interpolation, rigidBody.collisionDetectionMode));

                rigidBody.useGravity = false;
                rigidBody.isKinematic = true;
#if UNITY_6000_0_OR_NEWER
                rigidBody.linearDamping = simData.drag;
                rigidBody.angularDamping = simData.angularDrag;
#else
                rigidBody.drag = simData.drag;
                rigidBody.angularDrag = simData.angularDrag;
#endif
                rigidBody.mass = simData.mass;
                rigidBody.constraints = RigidbodyConstraints.None;
                rigidBody.interpolation = RigidbodyInterpolation.None;
                rigidBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }

            var simBodies = new System.Collections.Generic.List<Rigidbody>();

            var clones = new GameObject[selection.Length];

            var animData = new System.Collections.Generic.List<AnimData>();
            void AddColliders(GameObject source, GameObject dest)
            {
                var meshFilters = source.GetComponents<MeshFilter>();
                foreach (var meshFilter in meshFilters)
                {
                    var mesh = meshFilter.sharedMesh;
                    if (mesh == null) continue;
                    AddCollider(dest, mesh, simData);
                }
                var skinnedMeshRenderers = source.GetComponents<SkinnedMeshRenderer>();
                foreach (var renderer in skinnedMeshRenderers)
                {
                    var mesh = renderer.sharedMesh;
                    if (mesh == null) continue;
                    AddCollider(dest, mesh, simData);
                }
                foreach (Transform child in source.transform)
                {
                    var destChild = new GameObject();
                    destChild.transform.SetParent(dest.transform);
                    destChild.transform.localPosition = child.localPosition;
                    destChild.transform.localRotation = child.localRotation;
                    destChild.transform.localScale = child.localScale;
                    AddColliders(child.gameObject, destChild);
                }
            }

            for (int i = 0; i < selection.Length; ++i)
            {
                animData.Add(new AnimData(selection[i]));
                var obj = new GameObject();
                obj.layer = selection[i].layer;
                obj.transform.position = selection[i].transform.position;
                obj.transform.rotation = selection[i].transform.rotation;
                obj.transform.localScale = selection[i].transform.lossyScale;
                AddColliders(selection[i], obj);
                var magnitude = BoundsUtils.GetMagnitude(selection[i].transform);
                selection[i].transform.position += Vector3.up * (100 * magnitude);
                var rigidBody = obj.AddComponent<Rigidbody>();
                if (simData.changeLayer) obj.layer = simData.tempLayer;
                simBodies.Add(rigidBody);
                rigidBody.useGravity = true;
                rigidBody.isKinematic = false;
                clones[i] = obj;
            }

#if UNITY_2022_2_OR_NEWER
            var prevSimMode = Physics.simulationMode;
            Physics.simulationMode =  SimulationMode.Script;
#else
            Physics.autoSimulation = false;
#endif
            for (int i = 0; i < simData.maxIterations; ++i)
            {
                Physics.Simulate(Time.fixedDeltaTime);
                for (int objIdx = 0; objIdx < selection.Length; ++objIdx)
                {
                    var body = simBodies[objIdx];
#if UNITY_6000_0_OR_NEWER
                    if (body.linearVelocity.sqrMagnitude > simData.maxSpeedSquared)
                        body.linearVelocity = body.linearVelocity.normalized * simData.maxSpeed;
#else
                    if (body.velocity.sqrMagnitude > simData.maxSpeedSquared)
                        body.velocity = body.velocity.normalized * simData.maxSpeed;
#endif
                    if (body.angularVelocity.sqrMagnitude > simData.maxAngularSpeedSquared)
                        body.angularVelocity = body.angularVelocity.normalized * simData.maxAngularSpeed;
                    if (i % 10 == 0) animData[objIdx].poses.Add(new Pose(body.position, body.rotation));
                }

                if (simBodies.All(rb => rb.IsSleeping())) break;
            }
#if UNITY_2022_2_OR_NEWER
            Physics.simulationMode = prevSimMode;
#else
            Physics.autoSimulation = true;
#endif

            for (int i = 0; i < selection.Length; ++i)
            {
                selection[i].transform.position = clones[i].transform.position;
                selection[i].transform.rotation = clones[i].transform.rotation;
                animData[i].poses.Add(new Pose(selection[i].transform.position, selection[i].transform.rotation));
                Object.DestroyImmediate(clones[i]);
            }

            foreach (var item in originalBodies)
            {
                if (item.body == null) continue;
                item.body.useGravity = item.useGravity;
                item.body.isKinematic = item.isKinematic;
#if UNITY_6000_0_OR_NEWER
                item.body.linearDamping = item.drag;
                item.body.angularDamping = item.angularDrag;
#else
                item.body.drag = item.drag;
                item.body.angularDrag = item.angularDrag;
#endif
                item.body.mass = item.mass;
                item.body.constraints = item.constraints;
                item.body.interpolation = item.interpolation;
                item.body.collisionDetectionMode = item.collisionDetectionMode;
            }

            Physics.gravity = originalGravity;

            if (simData.ignoreSceneColliders)
            {
                foreach (var sceneCollider in sceneColliders) sceneCollider.enabled = true;
                foreach (var tempCollider in tempColliders) Object.DestroyImmediate(tempCollider);
            }
            else
            {
                foreach (var invisibleCollider in invisibleColliders) invisibleCollider.enabled = true;
            }
            Animate(animData, simData, recordAction);
            var finalPoses = new System.Collections.Generic.List<Pose>();
            foreach (var d in animData) finalPoses.Add(d.poses.Last());
            return finalPoses.ToArray();
        }
        private class AnimData
        {
            public GameObject obj = null;
            public System.Collections.Generic.List<Pose> poses = new System.Collections.Generic.List<Pose>();
            public System.Collections.Generic.List<Collider> enabledColliders
                = new System.Collections.Generic.List<Collider>();
            public System.Collections.Generic.List<GameObject> tempColliders
                = new System.Collections.Generic.List<GameObject>();
            public AnimData(GameObject obj) => this.obj = obj;
        }
        private static void Animate(System.Collections.Generic.List<AnimData> animData,
            SimulateGravityData simData, bool recordAction)
        {
            _stop = false;
            _isPlaying = true;
            foreach (var item in animData)
            {
                item.enabledColliders = item.obj.GetComponentsInChildren<Collider>()
                    .Where(collider => collider.enabled).ToList();
                item.tempColliders.Clear();
                var temp = new GameObject();
                temp.hideFlags = HideFlags.HideAndDontSave;
                var lastPose = item.poses.Last();
                temp.transform.position = lastPose.position;
                temp.transform.rotation = lastPose.rotation;
                temp.transform.localScale = item.obj.transform.lossyScale;

                var meshFilters = item.obj.GetComponentsInChildren<MeshFilter>();
                foreach (var meshFilter in meshFilters)
                {
                    var mesh = meshFilter.sharedMesh;
                    if (mesh == null) continue;
                    AddCollider(temp, mesh, simData);
                }
                var skinnedMeshRenderers = item.obj.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var renderer in skinnedMeshRenderers)
                {
                    var mesh = renderer.sharedMesh;
                    if (mesh == null) continue;
                    var meshFilter = renderer.gameObject.AddComponent<MeshFilter>();
                    meshFilter.sharedMesh = mesh;
                    AddCollider(temp, mesh, simData);
                }
                item.tempColliders.Add(temp);

                foreach (var collider in item.enabledColliders) collider.enabled = false;
            }
            Animate(animData, 0, recordAction);
        }
        private async static void Animate(System.Collections.Generic.List<AnimData> data, int frame, bool recordAction)
        {
            void EnableColliders(AnimData item)
            {
                foreach (var collider in item.enabledColliders)
                {
                    if (collider == null) continue;
                    collider.enabled = true;
                }
            }
            void DestroyTempColliders(AnimData item)
            {
                foreach (var temp in item.tempColliders) UnityEngine.Object.DestroyImmediate(temp);
            }
            void Record()
            {
                foreach (var item in data)
                {
                    if (item.obj == null) continue;
                    EnableColliders(item);
                    DestroyTempColliders(item);
                    item.obj.transform.position = item.poses.First().position;
                    item.obj.transform.rotation = item.poses.First().rotation;
                    UnityEditor.Undo.RecordObject(item.obj.transform, "Simulate Gravity");
                    item.obj.transform.position = item.poses.Last().position;
                    item.obj.transform.rotation = item.poses.Last().rotation;
                }
            }
            var isPlaying = false;
            if (_stop)
            {
                if (recordAction) Record();
                return;
            }
            foreach (var item in data)
            {
                if (item.obj == null) break;
                if (frame >= item.poses.Count)
                {
                    item.obj.transform.position = item.poses.Last().position;
                    item.obj.transform.rotation = item.poses.Last().rotation;
                    continue;
                }
                isPlaying = true;
                item.obj.transform.position = item.poses[frame].position;
                item.obj.transform.rotation = item.poses[frame].rotation;
            }
            if (isPlaying)
            {
                await System.Threading.Tasks.Task.Delay((int)(Time.fixedDeltaTime * 1000));
                Animate(data, frame + 1, recordAction);
            }
            else
            {
                if (recordAction) Record();
                else
                {
                    foreach (var item in data)
                    {
                        if (item.obj == null) continue;
                        EnableColliders(item);
                        DestroyTempColliders(item);
                    }
                }
                _isPlaying = false;
            }
        }
        [UnityEditor.InitializeOnLoad]
        private static class UndoEventHandler
        {
            
            static UndoEventHandler()
            {
                UnityEditor.Undo.undoRedoPerformed -= OnUndoRedoPerformed;
                UnityEditor.Undo.undoRedoPerformed += OnUndoRedoPerformed;
            }
            private static void OnUndoRedoPerformed()
            {
                _isPlaying = false;
                _stop = true;
            }
        }
    }
}
#pragma warning restore UDR0001
