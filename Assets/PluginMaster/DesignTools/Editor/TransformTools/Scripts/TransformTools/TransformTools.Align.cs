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
    public static partial class TransformTools
    {
        public static void AlignGlobal(GameObject[] selection, RelativeTo relativeTo,
            PluginMaster.AxesUtils.Axis axis, Bound bound,
            bool AlignToAnchor, bool filterByTopLevel = true,
            BoundsUtils.ObjectProperty property = BoundsUtils.ObjectProperty.BOUNDING_BOX, bool recordAction = true)
        {
            if (selection.Length == 0) return;
            if (bound == Bound.CENTER && AlignToAnchor) return;
            BoundsUtils.ClearBoundsDictionaries();

            const string COMMAND_NAME = "Align";

            var selectionBoundsTuple = GetSelectionBounds(selection, relativeTo, axis, Space.World, filterByTopLevel);
            var selectionBound = GetBound(selectionBoundsTuple.Item2, AlignToAnchor
                ? (bound == Bound.MAX ? Bound.MIN : Bound.MAX) : bound);
            var anchor = selectionBoundsTuple.Item1;

            for (int i = 0; i < selection.Length; ++i)
            {
                var obj = selection[i];
                if (obj == anchor && relativeTo != RelativeTo.SELECTION) continue;

                if (recordAction) UnityEditor.Undo.RecordObject(obj.transform, COMMAND_NAME);

                var objBound = GetBound(BoundsUtils.GetBoundsRecursive(obj.transform, filterByTopLevel, property), bound);
                var alignedPosition = obj.transform.position;

                switch (axis)
                {
                    case PluginMaster.AxesUtils.Axis.X:
                        alignedPosition.x = obj.transform.position.x + selectionBound.x - objBound.x;
                        break;
                    case PluginMaster.AxesUtils.Axis.Y:
                        alignedPosition.y = obj.transform.position.y + selectionBound.y - objBound.y;
                        break;
                    case PluginMaster.AxesUtils.Axis.Z:
                        alignedPosition.z = obj.transform.position.z + selectionBound.z - objBound.z;
                        break;
                }
                var delta = alignedPosition - obj.transform.position;
                obj.transform.position = alignedPosition;
                if (anchor != null && anchor.transform.parent == obj.transform)
                {
                    UnityEditor.Undo.RecordObject(anchor.transform, COMMAND_NAME);
                    anchor.transform.position -= delta;
                }
            }
        }

        public static void AlignLocal(GameObject[] selection, RelativeTo relativeTo, PluginMaster.AxesUtils.Axis axis,
            Bound bound, bool AlignToAnchor, bool filterByTopLevel = true,
            BoundsUtils.ObjectProperty property = BoundsUtils.ObjectProperty.BOUNDING_BOX, bool recordAction = true)
        {
            if (selection.Length == 0) return;
            if (bound == Bound.CENTER && AlignToAnchor) return;
            BoundsUtils.ClearBoundsDictionaries();
            const string COMMAND_NAME = "Align";

            var selectionBoundsTuple = GetSelectionBounds(selection, relativeTo, axis, Space.Self, filterByTopLevel);
            var selectionBoundsLocal = new Bounds(Vector3.zero, selectionBoundsTuple.Item2.size);

            var anchor = selectionBoundsTuple.Item1;
            var boundType = AlignToAnchor ? (bound == Bound.MAX ? Bound.MIN : Bound.MAX) : bound;
            var selectionBound = GetBound(selectionBoundsLocal, boundType);

            for (int i = 0; i < selection.Length; ++i)
            {
                var obj = selection[i];
                if (obj == anchor) continue;

                if (recordAction) UnityEditor.Undo.RecordObject(obj.transform, COMMAND_NAME);
                var bounds = BoundsUtils.GetBoundsRecursive(obj.transform, anchor.transform.rotation,
                    true, property, filterByTopLevel);
                var localBounds = new Bounds(Vector3.zero, bounds.size);
                var localObjBound = GetBound(localBounds, bound);

                var objLocalPos = anchor.transform.InverseTransformPoint(obj.transform.position);
                var alignedPosition = objLocalPos;

                switch (axis)
                {
                    case PluginMaster.AxesUtils.Axis.X:
                        alignedPosition.x = (selectionBound.x - localObjBound.x) / anchor.transform.localScale.x;
                        break;
                    case PluginMaster.AxesUtils.Axis.Y:
                        alignedPosition.y = (selectionBound.y - localObjBound.y) / anchor.transform.localScale.y;
                        break;
                    case PluginMaster.AxesUtils.Axis.Z:
                        alignedPosition.z = (selectionBound.z - localObjBound.z) / anchor.transform.localScale.z;
                        break;
                }

                alignedPosition = anchor.transform.TransformPoint(alignedPosition);
                var delta = alignedPosition - obj.transform.position;
                obj.transform.position = alignedPosition;
                if (anchor != null && anchor.transform.parent == obj.transform)
                {
                    UnityEditor.Undo.RecordObject(anchor.transform, COMMAND_NAME);
                    anchor.transform.position -= delta;
                }
            }
        }

        public static void Align(GameObject[] selection, RelativeTo relativeTo, PluginMaster.AxesUtils.Axis axis,
            Space space, Bound bound, bool AlignToAnchor, bool filterByTopLevel = true,
            BoundsUtils.ObjectProperty property = BoundsUtils.ObjectProperty.BOUNDING_BOX, bool recordAction = true)
        {
            if (relativeTo == RelativeTo.SELECTION || relativeTo == RelativeTo.CANVAS) space = Space.World;
            if (space == Space.World)
                AlignGlobal(selection, relativeTo, axis, bound, AlignToAnchor, filterByTopLevel, property, recordAction);
            else AlignLocal(selection, relativeTo, axis, bound, AlignToAnchor, filterByTopLevel, property, recordAction);
        }
    }
}
#pragma warning restore UDR0001
