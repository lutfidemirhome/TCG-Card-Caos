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
using UnityEngine;
namespace PluginMaster
{
    public static partial class BoundsUtils
    {
        public static readonly Vector3 MIN_VECTOR3 = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        public static readonly Vector3 MAX_VECTOR3 = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

        public enum ObjectProperty
        {
            BOUNDING_BOX,
            CENTER,
            PIVOT
        }

        public static Vector3 GetMaxVector(Vector3[] values)
        {
            var max = MIN_VECTOR3;
            foreach (var value in values) max = Vector3.Max(max, value);
            return max;
        }

        public static Vector3 GetMaxSize(GameObject[] objs)
        {
            var max = MIN_VECTOR3;
            foreach (var obj in objs)
            {
                var size = Vector3.zero;
                if (obj != null) size = GetBoundsRecursive(obj.transform).size;
                max = Vector3.Max(max, size);
            }
            return max;
        }
        
#if UNITY_6000_3_OR_NEWER
        private static System.Collections.Generic.Dictionary<(EntityId, ObjectProperty), Bounds> _boundsDictionary
            = new System.Collections.Generic.Dictionary<(EntityId, ObjectProperty), Bounds>();
#else
        private static System.Collections.Generic.Dictionary<(int, ObjectProperty), Bounds> _boundsDictionary
            = new System.Collections.Generic.Dictionary<(int, ObjectProperty), Bounds>();
#endif

        public static Bounds GetBounds(Transform transform, ObjectProperty property = ObjectProperty.BOUNDING_BOX,
            bool useDictionary = true)
        {
#if UNITY_6000_3_OR_NEWER
            var key = (transform.gameObject.GetEntityId(), property);
#else
            var key = (transform.gameObject.GetInstanceID(), property);
#endif
            if (useDictionary && _boundsDictionary.ContainsKey(key)) return _boundsDictionary[key];
            var terrain = transform.GetComponent<Terrain>();
            var renderer = transform.GetComponent<Renderer>();
            var rectTransform = transform.GetComponent<RectTransform>();
            var lodGroup = renderer == null ? transform.GetComponent<LODGroup>() : null;
            Bounds DoGetBounds()
            {
                if (lodGroup != null && property == ObjectProperty.BOUNDING_BOX)
                {
                    var lods = lodGroup.GetLODs();
                    if (lods != null && lods.Length > 0)
                    {
                        int validLodCount = 0;
                        for (int i = 0; i < lods.Length; ++i)
                        {
                            if (lods[i].renderers != null) lods[validLodCount++] = lods[i];
                        }
                        if (validLodCount > 0)
                        {
                            var renderers = lods[0].renderers;
                            var lodGameObjectsList = new System.Collections.Generic.List<GameObject>(renderers.Length);
                            for (int i = 0; i < renderers.Length; ++i)
                            {
                                var r = renderers[i];
                                if (r == null) continue;
                                var go = r.gameObject;
                                if (go.GetComponent<LODGroup>() == null)
                                    lodGameObjectsList.Add(go);
                            }
                            return GetSelectionBounds(lodGameObjectsList.ToArray(), false);
                        }
                    }
                }

                if (rectTransform == null && terrain == null)
                {
                    if (renderer == null || !renderer.enabled || property == ObjectProperty.PIVOT)
                        return new Bounds(transform.position, Vector3.zero);
                    if (property == ObjectProperty.CENTER) return new Bounds(renderer.bounds.center, Vector3.zero);
                    return renderer.bounds;
                }

                if (property == ObjectProperty.PIVOT) return new Bounds(transform.position, Vector3.zero);
                if (terrain != null)
                {
                    var bounds = terrain.terrainData.bounds;
                    bounds.center += transform.position;
                    return bounds;
                }
                return new Bounds(rectTransform.TransformPoint(rectTransform.rect.center),
                        rectTransform.TransformVector(rectTransform.rect.size));
            }
            var result = DoGetBounds();
            if (useDictionary) _boundsDictionary.Add(key, result);
            return result;
        }
        
#if UNITY_6000_3_OR_NEWER
        private static System.Collections.Generic.Dictionary<(EntityId, ObjectProperty, Vector2), Bounds>
            _boundsRecursiveDictionary = new System.Collections.Generic
            .Dictionary<(EntityId, ObjectProperty, Vector2), Bounds>();
#else
        private static System.Collections.Generic.Dictionary<(int, ObjectProperty, Vector2), Bounds>
            _boundsRecursiveDictionary = new System.Collections.Generic.Dictionary<(int, ObjectProperty, Vector2), Bounds>();
#endif

        public static Bounds GetBoundsRecursive(Transform transform, bool recursive = true,
            ObjectProperty property = ObjectProperty.BOUNDING_BOX, bool useDictionary = true)
        {
            if (!recursive) return GetBounds(transform, property, useDictionary);
            var pivot2D = Vector2.zero;
            var spriteRenderer = transform.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.enabled && spriteRenderer.sprite != null)
                pivot2D = spriteRenderer.sprite.pivot;
#if UNITY_6000_3_OR_NEWER
            var key = (transform.gameObject.GetEntityId(), property, pivot2D);
#else
            var key = (transform.gameObject.GetInstanceID(), property, pivot2D);
#endif
            if (useDictionary && _boundsRecursiveDictionary.ContainsKey(key))
                return _boundsRecursiveDictionary[key];

            var children = transform.GetComponentsInChildren<Transform>(true);
            var min = MAX_VECTOR3;
            var max = MIN_VECTOR3;
            var emptyHierarchy = true;
            bool IsActiveInHierarchy(Transform obj)
            {
                var parent = obj;
                do
                {
                    if (!parent.gameObject.activeSelf) return false;
                    parent = parent.parent;
                }
                while (parent != null);
                return true;
            }
            foreach (var child in children)
            {
                var notActive = !IsActiveInHierarchy(child);
                if (notActive) continue;
                var renderer = child.GetComponent<Renderer>();
                var rectTransform = child.GetComponent<RectTransform>();
                var terrain = child.GetComponent<Terrain>();
                var lodGroup = child.GetComponent<LODGroup>();
                if ((renderer == null || !renderer.enabled) && rectTransform == null && terrain == null && lodGroup == null)
                    continue;
                var bounds = GetBounds(child, property, useDictionary);
                if (bounds.size == Vector3.zero) continue;
                emptyHierarchy = false;
                min = Vector3.Min(bounds.min, min);
                max = Vector3.Max(bounds.max, max);
            }
            if (emptyHierarchy) return new Bounds(transform.position, Vector3.zero);
            var size = max - min;
            var center = min + size / 2f;
            var result = new Bounds(center, size);
            if (useDictionary) _boundsRecursiveDictionary.Add(key, result);
            return result;
        }

        public static Bounds GetSelectionBounds(GameObject[] selection, bool recursive = true,
            BoundsUtils.ObjectProperty property = BoundsUtils.ObjectProperty.BOUNDING_BOX)
        {
            var max = MIN_VECTOR3;
            var min = MAX_VECTOR3;
            if (selection.Length == 0) return new Bounds();
            foreach (var obj in selection)
            {
                if (obj == null) continue;
                var bounds = GetBoundsRecursive(obj.transform, recursive, property);
                max = Vector3.Max(bounds.max, max);
                min = Vector3.Min(bounds.min, min);
            }
            var size = max - min;
            var center = min + size / 2f;
            return new Bounds(center, size);
        }

        public static void ClearBoundsDictionaries()
        {
            _boundsDictionary.Clear();
            _boundsRecursiveDictionary.Clear();
            _boundsRotDictionary.Clear();
        }
    }
}
#pragma warning restore UDR0001
