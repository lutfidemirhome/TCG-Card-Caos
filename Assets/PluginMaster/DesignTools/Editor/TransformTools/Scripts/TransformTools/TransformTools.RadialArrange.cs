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
        public enum RotateAround
        {
            SELECTION_CENTER,
            TRANSFORM_POSITION,
            OBJECT_BOUNDS_CENTER,
            CUSTOM_POSITION
        }

        public enum Shape
        {
            CIRCLE,
            CIRCULAR_SPIRAL,
            ELLIPSE,
            ELLIPTICAL_SPIRAL
        }


        public class RadialArrangeData
        {
            private ArrangeBy _arrangeBy = ArrangeBy.SELECTION_ORDER;
            private RotateAround _rotateAround = RotateAround.SELECTION_CENTER;
            private Transform _centerTransform = null;
            private Vector3 _center = Vector3.zero;
            private Vector3 _axis = Vector3.forward;
            private Shape _shape = Shape.CIRCLE;
            private Vector2 _startEllipseAxes = Vector2.one;
            private Vector2 _endEllipseAxes = Vector2.one;
            private float _startAngle = 0f;
            private float _maxArcAngle = 360f;
            private bool _orientToRadius = false;
            private Vector3 _orientDirection = Vector3.right;
            private Vector3 _parallelDirection = Vector3.up;
            private bool _overwriteX = true;
            private bool _overwriteY = true;
            private bool _overwriteZ = true;
            private bool _lastSpotEmpty = false;
            private float _spacing = 0f;
            private Space _space = Space.World;

            public ArrangeBy arrangeBy { get => _arrangeBy; set => _arrangeBy = value; }
            public Vector3 axis { get => _axis; set => _axis = value; }
            public Shape shape { get => _shape; set => _shape = value; }
            public Vector2 startEllipseAxes { get => _startEllipseAxes; set => _startEllipseAxes = value; }
            public Vector2 endEllipseAxes { get => _endEllipseAxes; set => _endEllipseAxes = value; }
            public float startAngle { get => _startAngle; set => _startAngle = value; }
            public float maxArcAngle { get => _maxArcAngle; set => _maxArcAngle = value; }
            public bool orientToRadius { get => _orientToRadius; set => _orientToRadius = value; }
            public Vector3 center { get => _center; set => _center = value; }
            public Vector3 orientDirection { get => _orientDirection; set => _orientDirection = value; }
            public Vector3 parallelDirection { get => _parallelDirection; set => _parallelDirection = value; }
            public Transform centerTransform
            {
                get => _centerTransform;
                set
                {
                    if (_centerTransform == value) return;
                    _centerTransform = value;
                    UpdateCenter();
                }
            }
            public RotateAround rotateAround
            {
                get => _rotateAround;
                set
                {
                    if (_rotateAround == value) return;
                    _rotateAround = value;
                    UpdateCenter();
                }
            }

            public bool overwriteX { get => _overwriteX; set => _overwriteX = value; }
            public bool overwriteY { get => _overwriteY; set => _overwriteY = value; }
            public bool overwriteZ { get => _overwriteZ; set => _overwriteZ = value; }
            public bool lastSpotEmpty { get => _lastSpotEmpty; set => _lastSpotEmpty = value; }
            public float spacing { get => _spacing; set => _spacing = value; }
            public Space space { get => _space; set => _space = value; }

            public void UpdateCenter()
            {
                if (_centerTransform == null &&
                    (_rotateAround == RotateAround.TRANSFORM_POSITION
                    || _rotateAround == RotateAround.OBJECT_BOUNDS_CENTER)) _center = Vector3.zero;
                else if (_rotateAround == RotateAround.TRANSFORM_POSITION) _center = _centerTransform.transform.position;
                else if (_rotateAround == RotateAround.OBJECT_BOUNDS_CENTER)
                    _center = BoundsUtils.GetBoundsRecursive(_centerTransform).center;
            }

            public void UpdateCenter(GameObject[] selection)
            {
                if (_rotateAround != RotateAround.SELECTION_CENTER) return;
                if (selection.Length == 0) _center = Vector3.zero;
                else _center = BoundsUtils.GetSelectionBounds(selection).center;
            }

            public void UpdateCircleSpacing(int selectionCount)
            {
                if (selectionCount == 0)
                {
                    spacing = 0f;
                    return;
                }
                var perimeter = Mathf.PI * startEllipseAxes.x * Mathf.Abs(maxArcAngle) / 180f;
                spacing = perimeter / ((float)selectionCount - (lastSpotEmpty ? 0f : 1f));
            }

            public void UpdateCircleRadius(int selectionCount)
            {
                if (selectionCount == 0)
                {
                    startEllipseAxes = endEllipseAxes = Vector2.zero;
                    return;
                }
                var perimeter = spacing * ((float)selectionCount - (lastSpotEmpty ? 0f : 1f));
                startEllipseAxes = endEllipseAxes = Vector2.one * (perimeter / Mathf.PI / Mathf.Abs(maxArcAngle) * 180f);
            }
        }

        private static float GetEllipseRadius(Vector2 ellipseAxes, float angle)
        {
            if (ellipseAxes.x == ellipseAxes.y) return ellipseAxes.x;
            var a = ellipseAxes.x;
            var b = ellipseAxes.y;
            var sin = Mathf.Sin(angle * Mathf.Deg2Rad);
            var cos = Mathf.Cos(angle * Mathf.Deg2Rad);
            return a * b / Mathf.Sqrt(a * a * sin * sin + b * b * cos * cos);
        }

        private static Vector3 GetRadialPosition(Vector3 center, Vector3 axis, float radius, float angle)
        {
            var radiusDirection = Vector3.right;
            if (axis.x > 0 || axis.y < 0) radiusDirection = Vector3.forward;
            else if (axis.x < 0 || axis.z > 0) radiusDirection = Vector3.up;
            return center + Quaternion.AngleAxis(angle, axis) * radiusDirection * radius;
        }

        public static void RadialArrange(GameObject[] selection, RadialArrangeData data)
        {
            BoundsUtils.ClearBoundsDictionaries();
            const string COMMAND_NAME = "Radial Arrange";
            if (data.arrangeBy == ArrangeBy.HIERARCHY_ORDER) selection = SortByHierarchy(selection);
            data.UpdateCenter();
            var angle = data.startAngle;

            var deltaAngle = data.maxArcAngle / ((float)selection.Length - (data.lastSpotEmpty ? 0f : 1f));
            var ellipseAxes = data.startEllipseAxes;
            var deltaEllipseAxes = (data.endEllipseAxes - data.startEllipseAxes) / ((float)selection.Length - 1);
            foreach (var obj in selection)
            {
                UnityEditor.Undo.RecordObject(obj.transform, COMMAND_NAME);
                var radius = GetEllipseRadius(ellipseAxes, angle);
                var position = GetRadialPosition(data.center, data.axis, radius, angle);
                if (data.space == Space.Self && data.centerTransform != null)
                {
                    var localPos = position - data.center;
                    position = (data.centerTransform.rotation * localPos) + data.center;
                }
                obj.transform.position = new Vector3(
                    data.overwriteX ? position.x : obj.transform.position.x,
                    data.overwriteY ? position.y : obj.transform.position.y,
                    data.overwriteZ ? position.z : obj.transform.position.z);
                if (data.orientToRadius)
                {
                    obj.transform.rotation = Quaternion.identity;
                    LookAtCenter(obj.transform, data.center, data.axis, data.orientDirection, data.parallelDirection);
                }
                angle += deltaAngle;
                ellipseAxes += deltaEllipseAxes;
            }
        }
    }
}
#pragma warning restore UDR0001
