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
        #region BOUNDS

        public enum Bound { MIN, CENTER, MAX }

        public enum RelativeTo
        {
            LAST_SELECTED,
            FIRST_SELECTED,
            BIGGEST_OBJECT,
            SMALLEST_OBJECT,
            SELECTION,
            CANVAS
        }

        private static Vector3 GetBound(Bounds bounds, Bound bound)
        {
            switch (bound)
            {
                case Bound.MIN:
                    return bounds.min;
                case Bound.MAX:
                    return bounds.max;
                default:
                    return bounds.center;
            }
        }
        private static GameObject GetAnchorObject(GameObject[] selection, RelativeTo relativeTo,
            PluginMaster.AxesUtils.Axis axis, Space space, bool recursive = true)
        {
            if (selection.Length == 0) return null;
            switch (relativeTo)
            {
                case RelativeTo.LAST_SELECTED:
                    return selection.Last<GameObject>();
                case RelativeTo.FIRST_SELECTED:
                    return selection[0];
                case RelativeTo.BIGGEST_OBJECT:
                    GameObject biggestObject = null;
                    var maxSize = float.MinValue;
                    foreach (var obj in selection)
                    {
                        var bounds = space == Space.World ? BoundsUtils.GetBoundsRecursive(obj.transform, recursive)
                            : BoundsUtils.GetBoundsRecursive(obj.transform, obj.transform.rotation);
                        switch (axis)
                        {
                            case PluginMaster.AxesUtils.Axis.X:
                                if (bounds.size.x > maxSize)
                                {
                                    maxSize = bounds.size.x;
                                    biggestObject = obj;
                                }
                                break;
                            case PluginMaster.AxesUtils.Axis.Y:
                                if (bounds.size.y > maxSize)
                                {
                                    maxSize = bounds.size.y;
                                    biggestObject = obj;
                                }
                                break;
                            case PluginMaster.AxesUtils.Axis.Z:
                                if (bounds.size.z > maxSize)
                                {
                                    maxSize = bounds.size.z;
                                    biggestObject = obj;
                                }
                                break;
                        }
                    }
                    return biggestObject;
                case RelativeTo.SMALLEST_OBJECT:
                    GameObject smallestObject = null;
                    var minSize = float.MaxValue;
                    foreach (var obj in selection)
                    {
                        var bounds = space == Space.World ? BoundsUtils.GetBoundsRecursive(obj.transform, recursive)
                            : BoundsUtils.GetBoundsRecursive(obj.transform, obj.transform.rotation);
                        switch (axis)
                        {
                            case PluginMaster.AxesUtils.Axis.X:
                                if (bounds.size.x < minSize)
                                {
                                    minSize = bounds.size.x;
                                    smallestObject = obj;
                                }
                                break;
                            case PluginMaster.AxesUtils.Axis.Y:
                                if (bounds.size.y < minSize)
                                {
                                    minSize = bounds.size.y;
                                    smallestObject = obj;
                                }
                                break;
                            case PluginMaster.AxesUtils.Axis.Z:
                                if (bounds.size.z < minSize)
                                {
                                    minSize = bounds.size.z;
                                    smallestObject = obj;
                                }
                                break;
                        }
                    }
                    return smallestObject;
                default:
                    return null;
            }
        }

        private static System.Tuple<GameObject, Bounds> GetSelectionBounds(GameObject[] selection, RelativeTo relativeTo,
            PluginMaster.AxesUtils.Axis axis, Space space, bool recursive = true,
            BoundsUtils.ObjectProperty property = BoundsUtils.ObjectProperty.BOUNDING_BOX)
        {
            if (selection.Length == 0) return new System.Tuple<GameObject, Bounds>(null, new Bounds());
            var anchor = GetAnchorObject(selection, relativeTo, axis, space);
            if (anchor != null) return new System.Tuple<GameObject, Bounds>
                    (anchor, space == Space.World ? BoundsUtils.GetBoundsRecursive(anchor.transform, recursive, property)
                    : BoundsUtils.GetBoundsRecursive(anchor.transform, anchor.transform.rotation, true, property));
            if (relativeTo == RelativeTo.CANVAS)
            {
                var canvasBounds = GetCanvasBounds(selection);
                if (canvasBounds.size != Vector3.zero)
                    return new System.Tuple<GameObject, Bounds>(null, GetCanvasBounds(selection));
            }
            return new System.Tuple<GameObject, Bounds>(null, BoundsUtils.GetSelectionBounds(selection, recursive, property));
        }

        private static Bounds GetCanvasBounds(GameObject[] selection)
        {
            if (selection.Length == 0) return new Bounds();
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            bool noCanvasFound = true;
            foreach (var obj in selection)
            {
                var canvas = GetTopmostCanvas(obj);
                if (canvas == null) continue;
                noCanvasFound = false;
                var rectTransform = canvas.GetComponent<RectTransform>();
                var halfSize = rectTransform.sizeDelta / 2;
                max = Vector3.Max(max, rectTransform.position + (Vector3)halfSize);
                min = Vector3.Min(min, rectTransform.position - (Vector3)halfSize);
            }
            if (noCanvasFound) return new Bounds();
            var size = max - min;
            var center = min + size / 2f;
            return new Bounds(center, size);
        }

        private static Bounds GetCanvasBounds(Canvas canvas)
        {
            var rectTransform = canvas.GetComponent<RectTransform>();
            return new Bounds(rectTransform.position, rectTransform.sizeDelta);
        }

        private static Canvas GetTopmostCanvas(GameObject obj)
        {
            var canvasesInParent = obj.GetComponentsInParent<Canvas>();
            if (canvasesInParent.Length == 0) return null;
            if (canvasesInParent.Length == 1) return canvasesInParent[0];
            foreach (var canvasInParent in canvasesInParent)
            {
                var canvasCount = canvasInParent.GetComponentsInParent<Canvas>().Length;
                if (canvasCount == 1) return canvasInParent;
            }
            return null;
        }
        #endregion
        #region UTILS
        private static int CompareHierarchyIndex(GameObject obj1, GameObject obj2)
        {
            var idx1 = GetHierarchyIndex(obj1);
            var idx2 = GetHierarchyIndex(obj2);
            var depth = 0;
            do
            {
                if (idx1.Length <= depth) return -1;
                if (idx2.Length <= depth) return 1;
                var result = idx1[depth].CompareTo(idx2[depth]);
                if (result != 0) return result;
                ++depth;
            }
            while (true);
        }

        private static GameObject[] SortByHierarchy(GameObject[] selection)
        {
            var sortedList = selection.ToList();
            sortedList.Sort((obj1, obj2) => CompareHierarchyIndex(obj1, obj2));
            return sortedList.ToArray();
        }

        private static void LookAtCenter(Transform transform, Vector3 center,
            Vector3 axis, Vector3 orientation, Vector3 parallelAxis)
        {
            transform.rotation = Quaternion.FromToRotation(parallelAxis, axis);
            var worldOrientation = transform.TransformDirection(orientation);
            var objToCenter = center - transform.position;
            var angle = Vector3.Angle(worldOrientation, objToCenter);
            var cross = Vector3.Cross(worldOrientation, objToCenter);
            if (cross == Vector3.zero) cross = axis;
            transform.Rotate(cross, angle, Space.World);
        }

        #endregion
    }
}
#pragma warning restore UDR0001
