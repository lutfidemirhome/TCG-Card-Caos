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
        public enum IncrementalDataType
        {
            CONSTANT_DELTA,
            CURVE,
            OBJECT_SIZE
        }

        public enum ArrangeBy
        {
            SELECTION_ORDER,
            HIERARCHY_ORDER
        }

        public class ProgressiveAxisData
        {
            private float _constantDelta = 0f;
            private AnimationCurve _curve = AnimationCurve.Constant(0, 1, 0);
            private float _curveRangeMin = 0f;
            private float _curveRangeSize = 0f;
            private Rect _curveRange = new Rect(0, 0, 1, 1);
            private bool _overwrite = true;

            public float constantDelta { get => _constantDelta; set => _constantDelta = value; }
            public AnimationCurve curve { get => _curve; set => _curve = value; }
            public float curveRangeMin { get => _curveRangeMin; set => _curveRangeMin = value; }
            public float curveRangeSize { get => _curveRangeSize; set => _curveRangeSize = value; }
            public Rect curveRange { get => _curveRange; set => _curveRange = value; }
            public bool overwrite { get => _overwrite; set => _overwrite = value; }
        }

        public class ProgressionData
        {
            private ArrangeBy _arrangeOrder = ArrangeBy.SELECTION_ORDER;
            private IncrementalDataType _type = IncrementalDataType.CONSTANT_DELTA;
            private ProgressiveAxisData _x = new ProgressiveAxisData();
            private ProgressiveAxisData _y = new ProgressiveAxisData();
            private ProgressiveAxisData _z = new ProgressiveAxisData();

            public ArrangeBy arrangeOrder { get => _arrangeOrder; set => _arrangeOrder = value; }
            public IncrementalDataType type { get => _type; set => _type = value; }
            public Vector3 constantDelta
            {
                get => new Vector3(_x.constantDelta, _y.constantDelta, _z.constantDelta);
                set
                {
                    _x.constantDelta = value.x;
                    _y.constantDelta = value.y;
                    _z.constantDelta = value.z;
                }
            }
            public Vector3 curveRangeMin
            {
                get => new Vector3(_x.curveRangeMin, _y.curveRangeMin, _z.curveRangeMin);
                set
                {
                    if (new Vector3(_x.curveRangeMin, _y.curveRangeMin, _z.curveRangeMin) == value) return;
                    var rangeX = _x.curveRange;
                    rangeX.yMin = _x.curveRangeMin = value.x;
                    _x.curveRange = rangeX;
                    var rangeY = _y.curveRange;
                    rangeY.yMin = _y.curveRangeMin = value.y;
                    _y.curveRange = rangeY;
                    var rangeZ = _z.curveRange;
                    rangeZ.yMin = _z.curveRangeMin = value.z;
                    _z.curveRange = rangeZ;
                    UpdateRanges();
                }
            }
            public Vector3 curveRangeSize
            {
                get => new Vector3(_x.curveRangeSize, _y.curveRangeSize, _z.curveRangeSize);
                set
                {
                    if (new Vector3(_x.curveRangeSize, _y.curveRangeSize, _z.curveRangeSize) == value) return;
                    _x.curveRangeSize = value.x;
                    _y.curveRangeSize = value.y;
                    _z.curveRangeSize = value.z;
                    UpdateRanges();
                }
            }

            public ProgressiveAxisData x { get => _x; set => _x = value; }
            public ProgressiveAxisData y { get => _y; set => _y = value; }
            public ProgressiveAxisData z { get => _z; set => _z = value; }

            private void UpdateRanges()
            {
                var rangeX = _x.curveRange;
                rangeX.yMax = _x.curveRangeMin + _x.curveRangeSize;
                _x.curveRange = rangeX;
                var rangeY = _y.curveRange;
                rangeY.yMax = _y.curveRangeMin + _y.curveRangeSize;
                _y.curveRange = rangeY;
                var rangeZ = _z.curveRange;
                rangeZ.yMax = _z.curveRangeMin + _z.curveRangeSize;
                _z.curveRange = rangeZ;
            }

            public Vector3 EvaluateCurve(float t)
            {
                return new Vector3(
                    _x.overwrite ? _x.curve.Evaluate(t) : 0f,
                    _y.overwrite ? _y.curve.Evaluate(t) : 0f,
                    _z.overwrite ? _z.curve.Evaluate(t) : 0f);
            }

            public Rect GetRect(PluginMaster.AxesUtils.Axis axis)
            {
                switch (axis)
                {
                    case PluginMaster.AxesUtils.Axis.X:
                        return _x.curveRange;
                    case PluginMaster.AxesUtils.Axis.Y:
                        return _y.curveRange;
                    default:
                        return _z.curveRange;
                }
            }
        }
        private static int[] GetHierarchyIndex(GameObject obj)
        {
            var idxList = new System.Collections.Generic.List<int>();
            var parent = obj.transform;
            do
            {
                idxList.Insert(0, parent.transform.GetSiblingIndex());
                parent = parent.transform.parent;
            }
            while (parent != null);
            return idxList.ToArray();
        }

        public static void IncrementalPosition(GameObject[] selection,
            ProgressionData data, bool orientToPath, Vector3 orientation)
        {
            if (selection.Length < 2) return;
            BoundsUtils.ClearBoundsDictionaries();
            const string COMMAND_NAME = "Position Progression";
            if (data.arrangeOrder == ArrangeBy.HIERARCHY_ORDER) selection = SortByHierarchy(selection);
            var position = selection[0].transform.position;
            var t = 0f;
            var delta = 1f / ((float)selection.Length - 1f);
            var i = 0;
            GameObject prevObj = null;
            foreach (var obj in selection)
            {
                var bounds = BoundsUtils.GetBoundsRecursive(obj.transform);
                var centerLocalPos = obj.transform.TransformVector(obj.transform.InverseTransformPoint(bounds.center));
                if (i > 0 && data.type == IncrementalDataType.OBJECT_SIZE) position += bounds.size / 2f - centerLocalPos;
                ++i;
                if (!orientToPath || (orientToPath && data.type != IncrementalDataType.OBJECT_SIZE))
                    UnityEditor.Undo.RecordObject(obj.transform, COMMAND_NAME);
                obj.transform.position = new Vector3(
                    data.x.overwrite ? position.x : obj.transform.position.x,
                    data.y.overwrite ? position.y : obj.transform.position.y,
                    data.z.overwrite ? position.z : obj.transform.position.z);
                t += delta;

                position = data.type == IncrementalDataType.CONSTANT_DELTA
                    ? position + data.constantDelta
                    : data.type == IncrementalDataType.CURVE
                        ? selection[0].transform.position + data.EvaluateCurve(t)
                        : position + centerLocalPos + bounds.size / 2f;

                if (!orientToPath) continue;
                if (data.type != IncrementalDataType.OBJECT_SIZE) LookAtNext(obj.transform, position, orientation);
                else if (i > 1)
                {
                    UnityEditor.Undo.RecordObject(prevObj.transform, COMMAND_NAME);
                    LookAtNext(prevObj.transform, obj.transform.position, orientation);
                }
                if (data.type == IncrementalDataType.OBJECT_SIZE && i == selection.Length)
                {
                    UnityEditor.Undo.RecordObject(obj.transform, COMMAND_NAME);
                    obj.transform.eulerAngles = prevObj.transform.eulerAngles;
                }
                prevObj = obj;
            }
        }

        private static void LookAtNext(Transform transform, Vector3 next, Vector3 orientation)
        {
            var objToCenter = next - transform.position;
            transform.rotation = Quaternion.FromToRotation(orientation, objToCenter);
        }

        public static void IncrementalRotation(GameObject[] selection, ProgressionData data)
        {
            if (selection.Length < 2) return;
            const string COMMAND_NAME = "Rotation Progression";
            if (data.arrangeOrder == ArrangeBy.HIERARCHY_ORDER)
            {
                selection = SortByHierarchy(selection);
            }
            var eulerAngles = selection[0].transform.rotation.eulerAngles;
            var firstObjEulerAngles = eulerAngles;
            var t = 0f;
            foreach (var obj in selection)
            {
                UnityEditor.Undo.RecordObject(obj.transform, COMMAND_NAME);
                if (data.type == IncrementalDataType.CURVE)
                {
                    eulerAngles = firstObjEulerAngles + data.EvaluateCurve(t);
                    t += 1f / ((float)selection.Length - 1f);
                }
                obj.transform.rotation = Quaternion.Euler(
                    data.x.overwrite ? eulerAngles.x : obj.transform.rotation.eulerAngles.x,
                    data.y.overwrite ? eulerAngles.y : obj.transform.rotation.eulerAngles.y,
                    data.z.overwrite ? eulerAngles.z : obj.transform.rotation.eulerAngles.z);
                if (data.type == IncrementalDataType.CONSTANT_DELTA) eulerAngles += data.constantDelta;
            }
        }

        public static void IncrementalScale(GameObject[] selection, ProgressionData data)
        {
            if (selection.Length < 2) return;
            const string COMMAND_NAME = "Scale Progression";
            if (data.arrangeOrder == ArrangeBy.HIERARCHY_ORDER) selection = SortByHierarchy(selection);
            var scale = selection[0].transform.localScale;
            var firstObjScale = scale;
            var t = 0f;
            foreach (var obj in selection)
            {
                UnityEditor.Undo.RecordObject(obj.transform, COMMAND_NAME);

                if (data.type == IncrementalDataType.CURVE)
                {
                    scale = firstObjScale + data.EvaluateCurve(t);
                    t += 1f / ((float)selection.Length - 1f);
                }

                obj.transform.localScale = new Vector3(
                    data.x.overwrite ? scale.x : obj.transform.localScale.x,
                    data.y.overwrite ? scale.y : obj.transform.localScale.y,
                    data.z.overwrite ? scale.z : obj.transform.localScale.z);

                if (data.type == IncrementalDataType.CONSTANT_DELTA) scale += data.constantDelta;
            }
        }
    }
}
#pragma warning restore UDR0001
