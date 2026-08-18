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
        private static (Vector3 vertex, Transform transform)[] GetDirectionVertices(Transform target, Vector3 worldProjDir)
        {
            var meshFilters = System.Array.FindAll(target.GetComponentsInChildren<MeshFilter>(),
                filter => filter != null && filter.sharedMesh != null);
            var children = meshFilters.Select(filter => (filter.transform, filter.sharedMesh)).ToArray();
            var maxSqrDistance = float.MinValue;
            var bounds = BoundsUtils.GetBoundsRecursive(target);
            var vertices = new System.Collections.Generic.List<(Vector3 vertex, Transform transform)>()
            { (bounds.center, target) };
            foreach (var child in children)
            {
                foreach (var vertex in child.sharedMesh.vertices)
                {
                    var centerToVertex = child.transform.TransformPoint(vertex) - bounds.center;
                    var projection = Vector3.Project(centerToVertex, worldProjDir);
                    var sqrDistance = projection.sqrMagnitude * (projection.normalized != worldProjDir.normalized ? -1 : 1);
                    var vertexTrans = (vertex, child.transform);
                    if (sqrDistance > maxSqrDistance)
                    {
                        vertices.Clear();
                        maxSqrDistance = sqrDistance;
                        vertices.Add(vertexTrans);
                    }
                    else if (sqrDistance + 0.001 >= maxSqrDistance)
                    {
                        if (vertices.Exists(item => item.vertex == vertexTrans.vertex)) continue;
                        vertices.Add(vertexTrans);
                    }
                }
            }
            return vertices.ToArray();
        }

        private static void PlaceOnSurface(Transform target,
            PlaceOnSurfaceUtils.PlaceOnSurfaceData data, GameObject[] filters)
        {
            var worldProjDir = (data.projectionDirectionSpace == Space.World
                ? data.projectionDirection
                : target.TransformDirection(data.projectionDirection)).normalized;

            var originalPosition = target.position;
            var originalRotation = target.rotation;
            const string COMMAND_NAME = "Place On Surface";
            UnityEditor.Undo.RecordObject(target, COMMAND_NAME);
            if (data.rotateToSurface)
            {
                var worldOrientDir = target.TransformDirection(data.objectOrientation);
                var orientAngle = Vector3.Angle(worldOrientDir, worldProjDir);
                var cross = Vector3.Cross(worldOrientDir, worldProjDir);
                if (cross == Vector3.zero)
                {
                    cross = target.TransformDirection(data.objectOrientation.y != 0
                        ? Vector3.forward : data.objectOrientation.z != 0 ? Vector3.right : Vector3.up);
                    orientAngle = worldOrientDir == worldProjDir ? 0 : 180;
                }
                target.Rotate(cross, orientAngle);
            }

            bool Raycast(Vector3 rayOrigin, out RaycastHit hitInfo)
            {
                hitInfo = new RaycastHit();
                RaycastHit meshHitInfo = new RaycastHit();
                RaycastHit colliderHitInfo = new RaycastHit();
                bool DoRaycast(out RaycastHit hitInf)
                {
                    hitInf = new RaycastHit();
                    bool meshHit = false;
                    if (filters != null) meshHit = MeshUtils.Raycast(rayOrigin - worldProjDir * 0.01f, worldProjDir,
                        out meshHitInfo, out GameObject collider, filters, float.MaxValue);

                    var rayHits = Physics.RaycastAll(rayOrigin - worldProjDir * 0.01f, worldProjDir,
                        float.MaxValue, data.mask, QueryTriggerInteraction.Ignore).Where
#if UNITY_6000_3_OR_NEWER
                        (h => h.collider.gameObject.GetEntityId() != target.gameObject.GetEntityId()).ToArray();
#else
                        (h => h.collider.gameObject.GetInstanceID() != target.gameObject.GetInstanceID()).ToArray();
#endif


                    bool colliderHit = rayHits.Length > 0;
                    var colliderDistance = float.MaxValue;

                    if (colliderHit)
                    {
                        foreach (var hit in rayHits)
                        {
                            if (hit.distance < colliderDistance)
                            {
                                colliderDistance = hit.distance;
                                colliderHitInfo = hit;
                            }
                        }
                    }
                    if (colliderHit && meshHit)
                    {
                        hitInf = colliderHitInfo.distance < meshHitInfo.distance ? colliderHitInfo : meshHitInfo;
                        return true;
                    }
                    if (colliderHit)
                    {
                        hitInf = colliderHitInfo;
                        return true;
                    }
                    if (meshHit)
                    {
                        hitInf = meshHitInfo;
                        return true;
                    }
                    return false;
                }
                if (DoRaycast(out hitInfo)) return true;
                var distance = 1000000f;
                GameObject surfObj = null;

                var hits = Physics.RaycastAll(rayOrigin, -worldProjDir, float.MaxValue, data.mask,
                    QueryTriggerInteraction.Ignore).Where
#if UNITY_6000_3_OR_NEWER
                    (h => h.collider.gameObject.GetEntityId() != target.gameObject.GetEntityId()).ToArray();
#else
                    (h => h.collider.gameObject.GetInstanceID() != target.gameObject.GetInstanceID()).ToArray();
#endif

                if (hits.Length > 1)
                {
                    foreach (var hit in hits)
                    {
                        if (hit.distance < distance)
                        {
                            distance = hit.distance;
                            surfObj = hit.collider.gameObject;
                        }
                    }
                }

                if (filters != null && MeshUtils.Raycast(rayOrigin - worldProjDir * 0.01f, -worldProjDir,
                    out meshHitInfo, out GameObject c, filters, float.MaxValue))
                {
                    if (meshHitInfo.distance < distance)
                    {
                        distance = Mathf.Min(distance, meshHitInfo.distance);
                        surfObj = c;
                    }
                }
                float surfObjSize = 0f;
                if (surfObj != null)
                {
                    surfObjSize = BoundsUtils.GetBoundsRecursive(surfObj.transform,
                        Quaternion.LookRotation(worldProjDir)).size.z * 1.01f;
                }
                rayOrigin += -worldProjDir * (distance + surfObjSize);

                if (DoRaycast(out hitInfo)) return true;
                return false;
            }

            if (target.GetComponentsInChildren<MeshFilter>().Count() == 0)
            {
                if (Raycast(target.position, out RaycastHit hitInfo)) target.position = hitInfo.point;
                return;
            }

            var dirVert = GetDirectionVertices(target, worldProjDir);
            var minDistance = float.MaxValue;
            var closestVertexInfoList
                = new System.Collections.Generic.List<((Vector3 vertex, Transform transform), RaycastHit hitInfo)>();
            foreach (var vertexTransform in dirVert)
            {
                RaycastHit hitInfo;
                var rayOrigin = vertexTransform.transform.TransformPoint(vertexTransform.vertex);
                if (!Raycast(rayOrigin, out hitInfo)) continue;
                if (hitInfo.distance < minDistance)
                {
                    minDistance = hitInfo.distance;
                    closestVertexInfoList.Clear();
                    closestVertexInfoList.Add((vertexTransform, hitInfo));
                }
                else if (hitInfo.distance - 0.001 <= minDistance)
                {
                    closestVertexInfoList.Add((vertexTransform, hitInfo));
                }
            }
            if (closestVertexInfoList.Count == 0)
            {
                target.SetPositionAndRotation(originalPosition, originalRotation);
                return;
            }
            var averageWorldVertex = Vector3.zero;
            var averageHitPoint = Vector3.zero;
            var averageNormal = Vector3.zero;
            foreach (var vertInfo in closestVertexInfoList)
            {
                averageWorldVertex += vertInfo.Item1.transform.TransformPoint(vertInfo.Item1.vertex);
                averageHitPoint += vertInfo.hitInfo.point;
                averageNormal += vertInfo.hitInfo.normal;
            }
            averageWorldVertex /= closestVertexInfoList.Count;
            var averageVertex = target.InverseTransformPoint(averageWorldVertex);
            averageHitPoint /= closestVertexInfoList.Count;
            averageNormal /= closestVertexInfoList.Count;

            if (data.rotateToSurface)
            {
                var worldOrientDir = target.TransformDirection(-data.objectOrientation);
                var angle = Vector3.Angle(worldOrientDir, averageNormal);
                var cross = Vector3.Cross(worldOrientDir, averageNormal);
                if (cross != Vector3.zero)
                {
                    target.RotateAround(target.TransformPoint(averageVertex), cross, angle);
                }
            }

            target.position = averageHitPoint - target.TransformVector(averageVertex) - worldProjDir * data.surfaceDistance;
        }

        public static void PlaceOnSurface(GameObject[] selection, PlaceOnSurfaceUtils.PlaceOnSurfaceData data)
        {
            BoundsUtils.ClearBoundsDictionaries();
            var ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
            var layerDictionary = new System.Collections.Generic.Dictionary<GameObject, int>();
            foreach (var obj in selection)
            {
                var children = obj.transform.GetComponentsInChildren<Transform>(true);
                foreach (var child in children)
                {
                    layerDictionary.Add(child.gameObject, child.gameObject.layer);
                    child.gameObject.layer = ignoreRaycast;
                }
            }
            var filters = data.placeOnColliders ? null : MeshUtils.FindFilters(data.mask, selection);
            foreach (var obj in selection) PlaceOnSurface(obj.transform, data, filters);
            foreach (var item in layerDictionary) item.Key.layer = item.Value;
        }
    }
}
#pragma warning restore UDR0001
