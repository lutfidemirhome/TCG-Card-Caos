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
        public static void Distribute(GameObject[] selection, PluginMaster.AxesUtils.Axis axis, Bound bound)
        {
            if (selection.Length < 2) return;
            BoundsUtils.ClearBoundsDictionaries();
            const string COMMAND_NAME = "Distribute";
            var sortedList = new System.Collections.Generic.List<GameObject>(selection);
            switch (axis)
            {
                case PluginMaster.AxesUtils.Axis.X:
                    sortedList.Sort((obj1, obj2) => GetBound(BoundsUtils.GetBoundsRecursive(obj1.transform),
                        bound).x.CompareTo(GetBound(BoundsUtils.GetBoundsRecursive(obj2.transform), bound).x));
                    break;
                case PluginMaster.AxesUtils.Axis.Y:
                    sortedList.Sort((obj1, obj2) => GetBound(BoundsUtils.GetBoundsRecursive(obj1.transform),
                        bound).y.CompareTo(GetBound(BoundsUtils.GetBoundsRecursive(obj2.transform), bound).y));
                    break;
                case PluginMaster.AxesUtils.Axis.Z:
                    sortedList.Sort((obj1, obj2) => GetBound(BoundsUtils.GetBoundsRecursive(obj1.transform),
                        bound).z.CompareTo(GetBound(BoundsUtils.GetBoundsRecursive(obj2.transform), bound).z));
                    break;
            }

            var min = GetBound(BoundsUtils.GetBoundsRecursive(sortedList.First<GameObject>().transform), bound);
            var max = GetBound(BoundsUtils.GetBoundsRecursive(sortedList.Last<GameObject>().transform), bound);

            var objDistance = 0f;
            switch (axis)
            {
                case PluginMaster.AxesUtils.Axis.X:
                    objDistance = (max.x - min.x) / (float)(selection.Length - 1);
                    break;
                case PluginMaster.AxesUtils.Axis.Y:
                    objDistance = (max.y - min.y) / (float)(selection.Length - 1);
                    break;
                case PluginMaster.AxesUtils.Axis.Z:
                    objDistance = (max.z - min.z) / (float)(selection.Length - 1);
                    break;
            }
            for (int i = 0; i < sortedList.Count; ++i)
            {
                var transform = sortedList[i].transform;
                UnityEditor.Undo.RecordObject(transform, COMMAND_NAME);
                var distributedPosition = transform.position;
                var objBound = GetBound(BoundsUtils.GetBoundsRecursive(transform), bound);
                switch (axis)
                {
                    case PluginMaster.AxesUtils.Axis.X:
                        distributedPosition.x += min.x - objBound.x + objDistance * i;
                        break;
                    case PluginMaster.AxesUtils.Axis.Y:
                        distributedPosition.y += min.y - objBound.y + objDistance * i;
                        break;
                    case PluginMaster.AxesUtils.Axis.Z:
                        distributedPosition.z += min.z - objBound.z + objDistance * i;
                        break;
                }
                transform.position = distributedPosition;
            }
        }

        public static void DistributeGaps(GameObject[] selection, PluginMaster.AxesUtils.Axis axis,
            float strength = 1f, bool recordAction = true)
        {
            if (selection.Length < 2) return;
            BoundsUtils.ClearBoundsDictionaries();
            const string COMMAND_NAME = "Distribute Gaps";
            var selectionBounds = GetSelectionBounds(selection, RelativeTo.SELECTION, axis, Space.World).Item2;
            var gapSize = selectionBounds.size;
            foreach (var obj in selection)
                gapSize -= BoundsUtils.GetBoundsRecursive(obj.transform).size;
            gapSize /= (float)(selection.Length - 1);

            var sortedList = new System.Collections.Generic.List<GameObject>(selection);
            switch (axis)
            {
                case PluginMaster.AxesUtils.Axis.X:
                    sortedList.Sort((obj1, obj2) => BoundsUtils.GetBoundsRecursive(obj1.transform).center.x.CompareTo
                        (BoundsUtils.GetBoundsRecursive(obj2.transform).center.x));
                    break;
                case PluginMaster.AxesUtils.Axis.Y:
                    sortedList.Sort((obj1, obj2) => BoundsUtils.GetBoundsRecursive(obj1.transform).center.y.CompareTo
                    (BoundsUtils.GetBoundsRecursive(obj2.transform).center.y));
                    break;
                case PluginMaster.AxesUtils.Axis.Z:
                    sortedList.Sort((obj1, obj2) => BoundsUtils.GetBoundsRecursive(obj1.transform).center.z.CompareTo
                    (BoundsUtils.GetBoundsRecursive(obj2.transform).center.z));
                    break;
            }

            var minPosition = selectionBounds.min + gapSize
                + BoundsUtils.GetBoundsRecursive(sortedList.First<GameObject>().transform).size;
            for (int i = 1; i < sortedList.Count - 1; ++i)
            {
                var obj = sortedList[i];
                if (recordAction) UnityEditor.Undo.RecordObject(obj.transform, COMMAND_NAME);
                var objBounds = BoundsUtils.GetBoundsRecursive(obj.transform);
                var delta = minPosition - objBounds.min;
                var distributedPosition = obj.transform.position
                    + AxesUtils.GetVector(1, axis) * AxesUtils.GetAxisValue(delta, axis);
                obj.transform.position = Vector3.Lerp(obj.transform.position, distributedPosition, strength);
                minPosition += objBounds.size + gapSize;
            }
        }
    }
}
#pragma warning restore UDR0001
