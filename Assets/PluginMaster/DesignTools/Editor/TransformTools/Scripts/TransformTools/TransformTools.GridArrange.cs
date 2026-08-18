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
        public class ArrangeAxisData
        {
            private bool _overwrite = true;
            private int _direction = 1;
            private int _priority = 0;
            private int _cells = 1;
            private CellSizeType _cellSizeType = CellSizeType.BIGGEST_OBJECT;
            private float _cellSize = 0f;
            private TransformTools.Bound _aligment = TransformTools.Bound.CENTER;
            private float _spacing = 0f;
            private bool _autoSpacing = false;

            public ArrangeAxisData(int priority) => _priority = priority;

            public int direction { get => _direction; set => _direction = value; }
            public int priority { get => _priority; set => _priority = value; }
            public int cells { get => _cells; set => _cells = value; }
            public Bound aligment { get => _aligment; set => _aligment = value; }
            public float spacing { get => _spacing; set => _spacing = value; }
            public bool autoSpacing
            {
                get => _autoSpacing;
                set
                {
                    if (_autoSpacing == value) return;
                    _autoSpacing = value;
                    if (_autoSpacing) _spacing = 0;
                }
            }
            public CellSizeType cellSizeType { get => _cellSizeType; set => _cellSizeType = value; }
            public float cellSize
            {
                get => _cellSize;
                set
                {
                    if (value < 0 || _cellSize == value) return;
                    _cellSize = value;
                }
            }
            public bool overwrite
            {
                get => _overwrite;
                set
                {
                    if (_overwrite == value) return;
                    _overwrite = value;
                    if (!_overwrite)
                    {
                        _cells = 1;
                        _priority = 2;
                    }
                }
            }
        }

        public enum SortBy
        {
            SELECTION,
            POSITION,
            HIERARCHY
        }

        public enum CellSizeType
        {
            BIGGEST_OBJECT_PER_GROUP,
            BIGGEST_OBJECT,
            CUSTOM
        }

        public enum ArrangeRelativeTo
        {
            SELECTION,
            FIRST_OBJECT
        }

        public class ArrangeData
        {
            private ArrangeAxisData _x = new ArrangeAxisData(0);
            private ArrangeAxisData _y = new ArrangeAxisData(1);
            private ArrangeAxisData _z = new ArrangeAxisData(2);
            private SortBy _sortBy = SortBy.POSITION;
            private System.Collections.Generic.List<PluginMaster.AxesUtils.Axis> _priorityList
                = new System.Collections.Generic.List<PluginMaster.AxesUtils.Axis>
                { PluginMaster.AxesUtils.Axis.X, PluginMaster.AxesUtils.Axis.Y, PluginMaster.AxesUtils.Axis.Z };
            private BoundsUtils.ObjectProperty _alignProperty = BoundsUtils.ObjectProperty.BOUNDING_BOX;
            private ArrangeRelativeTo _arrangeRelativeTo = ArrangeRelativeTo.SELECTION;

            public ArrangeAxisData x { get => _x; set => _x = value; }
            public ArrangeAxisData y { get => _y; set => _y = value; }
            public ArrangeAxisData z { get => _z; set => _z = value; }
            public SortBy sortBy
            {
                get => _sortBy;
                set
                {
                    if (_sortBy == value) return;
                    _sortBy = value;
                    if (_sortBy == SortBy.POSITION)
                    {
                        x.priority = 0;
                        y.priority = 1;
                        z.priority = 2;
                        z.direction = y.direction = x.direction = +1;
                    }
                }
            }

            public BoundsUtils.ObjectProperty alignProperty { get => _alignProperty; set => _alignProperty = value; }
            public ArrangeRelativeTo arrangeRelativeTo { get => _arrangeRelativeTo; set => _arrangeRelativeTo = value; }

            public ArrangeAxisData GetData(PluginMaster.AxesUtils.Axis axis)
            {
                return axis == PluginMaster.AxesUtils.Axis.X ? x : axis == PluginMaster.AxesUtils.Axis.Y ? y : z;
            }
            public void UpdatePriorities(PluginMaster.AxesUtils.Axis axis)
            {
                var activeAxes = System.Convert.ToInt32(x.overwrite) + System.Convert.ToInt32(y.overwrite)
                    + System.Convert.ToInt32(z.overwrite);
                if (activeAxes > 0)
                {
                    if (x.overwrite) x.priority = Mathf.Min(x.priority, activeAxes - 1);
                    if (y.overwrite) y.priority = Mathf.Min(y.priority, activeAxes - 1);
                    if (z.overwrite) z.priority = Mathf.Min(z.priority, activeAxes - 1);
                }
                _priorityList.Remove(axis);
                _priorityList.Insert(GetData(axis).priority, axis);


                for (int priority = 0; priority < 3; ++priority)
                {
                    switch (_priorityList[priority])
                    {
                        case PluginMaster.AxesUtils.Axis.X:
                            x.priority = priority;
                            break;
                        case PluginMaster.AxesUtils.Axis.Y:
                            y.priority = priority;
                            break;
                        case PluginMaster.AxesUtils.Axis.Z:
                            z.priority = priority;
                            break;
                    }
                }
            }
            public PluginMaster.AxesUtils.Axis GetAxisByPriority(int priority) => _priorityList[priority];
            public ArrangeAxisData GetAxisDataByPriority(int priority) => GetData(_priorityList[priority]);
        }

        private static System.Collections.Generic.Dictionary<(int i, int j, int k),
            GameObject> SortBySelectionOrder(GameObject[] selection, ArrangeData data)
        {
            int i = 0;
            int j = 0;
            int k = 0;

            var dataList = new System.Collections.Generic.List<ArrangeAxisData>() { data.x, data.y, data.z };
            dataList.Sort((data1, data2) => data1.priority.CompareTo(data2.priority));

            var p0 = dataList[0] == data.x ? i : dataList[0] == data.y ? j : k;
            var p1 = dataList[1] == data.x ? i : dataList[1] == data.y ? j : k;
            var p2 = dataList[2] == data.x ? i : dataList[2] == data.y ? j : k;

            var objDictionary = new System.Collections.Generic.Dictionary<(int i, int j, int k), GameObject>();

            int GetNextCellIndex(int currentIndex, int cellCount)
                => IsLastCell(currentIndex, cellCount) ? 0 : currentIndex + 1;
            bool IsFirstCell(int currentIndex) => currentIndex == 0;
            bool IsLastCell(int currentIndex, int cellCount) => currentIndex == cellCount - 1;

            foreach (var obj in selection)
            {
                objDictionary.Add((
                    dataList[0] == data.x ? p0 : dataList[1] == data.x ? p1 : p2,
                    dataList[0] == data.y ? p0 : dataList[1] == data.y ? p1 : p2,
                    dataList[0] == data.z ? p0 : dataList[1] == data.z ? p1 : p2), obj);

                p0 = GetNextCellIndex(p0, dataList[0].cells);
                if (!IsFirstCell(p0)) continue;
                p1 = GetNextCellIndex(p1, dataList[1].cells);
                if (!IsFirstCell(p1)) continue;
                p2 = GetNextCellIndex(p2, dataList[2].cells);
            }
            return objDictionary;
        }

        private static System.Collections.Generic.Dictionary<(int i, int j, int k),
            GameObject> SortByPosition(GameObject[] selection, ArrangeData data, Bounds selectionBounds)
        {
            var maxSize = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var averageSize = Vector3.zero;
            foreach (var obj in selection)
            {
                var objBounds = BoundsUtils.GetBoundsRecursive(obj.transform, true, data.alignProperty);
                maxSize = Vector3.Max(maxSize, objBounds.size);
                averageSize += objBounds.size;
            }
            averageSize /= selection.Length;
            var cellSize = new Vector3(
                data.x.cellSizeType == CellSizeType.BIGGEST_OBJECT
                ? maxSize.x : data.x.cellSizeType == CellSizeType.BIGGEST_OBJECT_PER_GROUP ? averageSize.x : data.x.cellSize,
                data.y.cellSizeType == CellSizeType.BIGGEST_OBJECT
                ? maxSize.y : data.y.cellSizeType == CellSizeType.BIGGEST_OBJECT_PER_GROUP ? averageSize.y : data.y.cellSize,
                data.z.cellSizeType == CellSizeType.BIGGEST_OBJECT
                ? maxSize.z : data.z.cellSizeType == CellSizeType.BIGGEST_OBJECT_PER_GROUP ? averageSize.z : data.z.cellSize);

            var firstCellCenter = selectionBounds.min + cellSize / 2f;

            var cellDict = new System.Collections.Generic.Dictionary<(int i, int j, int k), Bounds>();

            for (int k = 0; k < data.z.cells; ++k)
            {
                for (int j = 0; j < data.y.cells; ++j)
                {
                    for (int i = 0; i < data.x.cells; ++i)
                    {
                        var cellCenter = firstCellCenter + new Vector3(cellSize.x * i, cellSize.y * j, cellSize.z * k);
                        var cellBounds = new Bounds(cellCenter, cellSize);
                        cellDict.Add((i, j, k), cellBounds);
                    }
                }
            }
            var unsorted = new System.Collections.Generic.List<GameObject>(selection);
            var objDict = new System.Collections.Generic.Dictionary<(int i, int j, int k), GameObject>();

            while (unsorted.Count > 0)
            {
                var cellObjectsDict = new System.Collections.Generic.Dictionary<(int i, int j, int k),
                    System.Collections.Generic.List<(GameObject obj, float sqrDistanceToCorner, float sqrDistanceToCenter)>>();
                foreach (var obj in unsorted)
                {
                    var objBounds = BoundsUtils.GetBoundsRecursive(obj.transform, true, data.alignProperty);
                    var minSqrDistanceToCorner = float.MaxValue;
                    var minSqrDistanceToCenter = float.MaxValue;
                    var closestCell = new System.Collections.Generic.KeyValuePair<(int i, int j, int k), Bounds>();
                    foreach (var cell in cellDict)
                    {
                        var objToCorner = new Vector3(
                            objBounds.min.x - cell.Value.min.x,
                            objBounds.min.y - cell.Value.min.y,
                            objBounds.min.z - cell.Value.min.z);
                        var sqrDistanceToCorner = Vector3.SqrMagnitude(objToCorner);
                        var sqrDistanceToCenter = Vector3.SqrMagnitude(objBounds.center - cell.Value.center);
                        if (sqrDistanceToCorner < minSqrDistanceToCorner)
                        {
                            minSqrDistanceToCorner = sqrDistanceToCorner;
                            minSqrDistanceToCenter = sqrDistanceToCenter;
                            closestCell = cell;
                        }
                        else if (minSqrDistanceToCorner == sqrDistanceToCorner
                            && sqrDistanceToCenter < minSqrDistanceToCenter)
                        {
                            minSqrDistanceToCenter = sqrDistanceToCenter;
                            closestCell = cell;
                        }
                    }
                    if (cellObjectsDict.ContainsKey((closestCell.Key)))
                        cellObjectsDict[closestCell.Key].Add((obj, minSqrDistanceToCorner, minSqrDistanceToCenter));
                    else
                    {
                        cellObjectsDict.Add(closestCell.Key,
                            new System.Collections.Generic.List<(GameObject obj,
                            float sqrDistanceToCorner, float sqrDistanceToCenter)>());
                        cellObjectsDict[closestCell.Key].Add((obj, minSqrDistanceToCorner, minSqrDistanceToCenter));
                    }
                }

                int GetKeyValue((int i, int j, int k) key) => key.i * (int)Mathf.Pow(10, data.x.priority + 1)
                    + key.j * (int)Mathf.Pow(10, data.y.priority + 1)
                    + key.k * (int)Mathf.Pow(10, data.z.priority + 1);

                foreach (var cellObjs in cellObjectsDict)
                {
                    var minSqrDistanceToCorner = cellObjs.Value[0].sqrDistanceToCorner;
                    var minSqrDistanceToCenter = cellObjs.Value[0].sqrDistanceToCenter;
                    int minKeyValue = 0;
                    GameObject closestObj = cellObjs.Value[0].obj;
                    for (int i = 1; i < cellObjs.Value.Count; ++i)
                    {
                        var objData = cellObjs.Value[i];
                        var keyValue = GetKeyValue(cellObjs.Key);
                        if (objData.sqrDistanceToCorner < minSqrDistanceToCorner
                            || (objData.sqrDistanceToCorner == minSqrDistanceToCorner && keyValue < minKeyValue))
                        {
                            minKeyValue = keyValue;
                            minSqrDistanceToCorner = objData.sqrDistanceToCorner;
                            minSqrDistanceToCenter = objData.sqrDistanceToCenter;
                            closestObj = objData.obj;
                        }
                        else if (minSqrDistanceToCorner == objData.sqrDistanceToCorner
                            && objData.sqrDistanceToCenter < minSqrDistanceToCenter)
                        {
                            minSqrDistanceToCenter = objData.sqrDistanceToCenter;
                            closestObj = objData.obj;
                        }
                    }
                    objDict.Add(cellObjs.Key, closestObj);
                    unsorted.Remove(closestObj);
                    cellDict.Remove(cellObjs.Key);
                }
            }
            return objDict;
        }

        public static bool Arrange(GameObject[] selection, ArrangeData data)
        {
            var cellCount = data.x.cells * data.y.cells * data.z.cells;
            if (selection.Length < 2 || selection.Length > cellCount) return false;

            BoundsUtils.ClearBoundsDictionaries();
            if (data.sortBy == SortBy.HIERARCHY) selection = SortByHierarchy(selection);
            var firstPosition = selection[0].transform.position;
            var selectionCenter = Vector3.zero;
            var selectionBounds = new Bounds();
            if (data.alignProperty == BoundsUtils.ObjectProperty.PIVOT)
            {
                selectionBounds.size = new Vector3(
                    data.x.cellSize * data.x.cells + data.x.spacing * (data.x.cells - 1),
                    data.y.cellSize * data.y.cells + data.y.spacing * (data.y.cells - 1),
                    data.z.cellSize * data.z.cells + data.z.spacing * (data.z.cells - 1));
                selectionBounds.center = Vector3.zero;
                foreach (var obj in selection) selectionBounds.center += obj.transform.position;
                selectionBounds.center /= selection.Length;
            }
            else selectionBounds = BoundsUtils.GetSelectionBounds(selection, true, data.alignProperty);
            var originalSelectionCenter = selectionCenter = selectionBounds.center;
            System.Collections.Generic.Dictionary<(int i, int j, int k), GameObject> objDictionary;
            if (data.sortBy == SortBy.POSITION)
            {
                if (data.alignProperty == BoundsUtils.ObjectProperty.BOUNDING_BOX)
                {
                    var centerBounds = BoundsUtils.GetSelectionBounds(selection, true, BoundsUtils.ObjectProperty.CENTER);
                    if (data.x.cellSizeType == CellSizeType.CUSTOM) selectionCenter.x = centerBounds.center.x;
                    if (data.y.cellSizeType == CellSizeType.CUSTOM) selectionCenter.y = centerBounds.center.y;
                    if (data.z.cellSizeType == CellSizeType.CUSTOM) selectionCenter.z = centerBounds.center.z;
                }
                objDictionary = SortByPosition(selection, data, selectionBounds);
            }
            else objDictionary = SortBySelectionOrder(selection, data);

            if (selection.Length < cellCount)
            {
                var usedCells = Vector3Int.zero;
                foreach (var key in objDictionary.Keys)
                {
                    usedCells.x = Mathf.Max(usedCells.x, key.i);
                    usedCells.y = Mathf.Max(usedCells.y, key.j);
                    usedCells.z = Mathf.Max(usedCells.z, key.k);
                }
                data.x.cells = usedCells.x + 1;
                data.y.cells = usedCells.y + 1;
                data.z.cells = usedCells.z + 1;
            }

            float[] GetCellSizes(PluginMaster.AxesUtils.Axis mainAxis, out float totalSize)
            {
                totalSize = 0f;
                var mainAxisData = data.GetData(mainAxis);
                var cellSizes = new float[mainAxisData.cells];
                for (int a = 0; a < mainAxisData.cells; ++a)
                {
                    cellSizes[a] = mainAxisData.cellSize;
                    if (mainAxisData.cellSizeType == CellSizeType.CUSTOM)
                    {
                        totalSize += cellSizes[a];
                        continue;
                    }
                    PluginMaster.AxesUtils.Axis secondaryAxis1 = PluginMaster.AxesUtils.Axis.Y;
                    PluginMaster.AxesUtils.Axis secondaryAxis2 = PluginMaster.AxesUtils.Axis.Z;
                    if (mainAxis == PluginMaster.AxesUtils.Axis.Y)
                    {
                        secondaryAxis1 = PluginMaster.AxesUtils.Axis.X;
                        secondaryAxis2 = PluginMaster.AxesUtils.Axis.Z;
                    }
                    else if (mainAxis == PluginMaster.AxesUtils.Axis.Z)
                    {
                        secondaryAxis1 = PluginMaster.AxesUtils.Axis.X;
                        secondaryAxis2 = PluginMaster.AxesUtils.Axis.Y;
                    }
                    var seondaryAxisData1 = data.GetData(secondaryAxis1);
                    var seondaryAxisData2 = data.GetData(secondaryAxis2);

                    System.Collections.Generic.List<GameObject> objList = new System.Collections.Generic.List<GameObject>();
                    for (int b = 0; b < seondaryAxisData1.cells; ++b)
                        for (int c = 0; c < seondaryAxisData2.cells; ++c)
                        {
                            var i = mainAxis == PluginMaster.AxesUtils.Axis.X
                                ? a : secondaryAxis1 == PluginMaster.AxesUtils.Axis.X ? b : c;
                            var j = mainAxis == PluginMaster.AxesUtils.Axis.Y
                                ? a : secondaryAxis1 == PluginMaster.AxesUtils.Axis.Y ? b : c;
                            var k = mainAxis == PluginMaster.AxesUtils.Axis.Z
                                ? a : secondaryAxis1 == PluginMaster.AxesUtils.Axis.Z ? b : c;
                            if (objDictionary.ContainsKey((i, j, k))) objList.Add(objDictionary[(i, j, k)]);
                        }

                    var size = BoundsUtils.GetMaxSize(objList.ToArray());
                    cellSizes[a] = mainAxis == PluginMaster.AxesUtils.Axis.X
                        ? size.x : mainAxis == PluginMaster.AxesUtils.Axis.Y ? size.y : size.z;
                    totalSize += cellSizes[a];
                }
                totalSize += mainAxisData.spacing * (mainAxisData.cells - 1);
                return cellSizes;
            }

            var ArrangementSize = Vector3.zero;
            var cellSizesX = GetCellSizes(PluginMaster.AxesUtils.Axis.X, out ArrangementSize.x);
            var cellSizesY = GetCellSizes(PluginMaster.AxesUtils.Axis.Y, out ArrangementSize.y);
            var cellSizesZ = GetCellSizes(PluginMaster.AxesUtils.Axis.Z, out ArrangementSize.z);

            var firstCellCenter = data.arrangeRelativeTo == ArrangeRelativeTo.FIRST_OBJECT ? firstPosition
                : selectionCenter - Vector3.Scale(ArrangementSize,
                new Vector3(data.x.direction, data.y.direction, data.z.direction)) / 2f
                + new Vector3(cellSizesX[0] * data.x.direction,
                cellSizesY[0] * data.y.direction, cellSizesZ[0] * data.z.direction) / 2f;

            Transform CommonParent()
            {
                if (!objDictionary.ContainsKey((0, 0, 0))) return null;
                Transform parent = objDictionary[(0, 0, 0)].transform.parent;
                var parentFound = false;
                while (parent != null && !parentFound)
                {
                    parentFound = true;
                    foreach (var key in objDictionary.Keys)
                    {
                        var obj = objDictionary[key];
                        if (!obj.transform.IsChildOf(parent))
                        {
                            parent = parent.parent;
                            parentFound = false;
                            break;
                        }
                    }
                }
                return parent;
            }

            var spacing = new Vector3(data.x.spacing, data.y.spacing, data.z.spacing);
            void ComputeAutoSpacing(PluginMaster.AxesUtils.Axis axis, RectTransform rectTransform)
            {
                if (rectTransform == null) return;
                var axisData = data.GetData(axis);
                if (!axisData.autoSpacing) return;
                var span = (Vector3)rectTransform.sizeDelta - ArrangementSize;
                var axisSpan = PluginMaster.AxesUtils.GetAxisValue(span, axis);
                PluginMaster.AxesUtils.SetAxisValue(ref spacing, axis, axisSpan / axisData.cells);
                var axisCanvasSize = PluginMaster.AxesUtils.GetAxisValue(rectTransform.rect.size, axis);
                var axisSpacing = PluginMaster.AxesUtils.GetAxisValue(spacing, axis);
                var canvasCellSize = axisCanvasSize / axisData.cells - axisSpacing;
                var axisCanvasPosition = PluginMaster.AxesUtils.GetAxisValue(rectTransform.rect.position, axis);
                PluginMaster.AxesUtils.SetAxisValue(ref firstCellCenter, axis,
                    axisCanvasPosition + (axisCanvasSize + canvasCellSize + axisSpacing) / 2);
                if (axisData.aligment == Bound.MIN)
                    PluginMaster.AxesUtils.AddValueToAxis(ref firstCellCenter, axis, -axisSpacing / 2);
                else if (axisData.aligment == Bound.MAX)
                    PluginMaster.AxesUtils.AddValueToAxis(ref firstCellCenter, axis, axisSpacing / 2);
                var cellSizes = axis == PluginMaster.AxesUtils.Axis.X ? cellSizesX : cellSizesY;
                for (int i = 0; i < cellSizes.Length; ++i) cellSizes[i] = canvasCellSize;
            }

            if (data.x.autoSpacing || data.y.autoSpacing)
            {
                var commonParent = CommonParent();
                if (commonParent != null)
                {
                    var rectTransform = commonParent.GetComponentInParent<RectTransform>();
                    ComputeAutoSpacing(PluginMaster.AxesUtils.Axis.X, rectTransform);
                    ComputeAutoSpacing(PluginMaster.AxesUtils.Axis.Y, rectTransform);
                }
            }

            if (data.x.cellSizeType == CellSizeType.CUSTOM)
            {
                if (data.x.aligment == Bound.MIN) firstCellCenter.x += cellSizesX[0] / 2f;
                else if (data.x.aligment == Bound.MAX) firstCellCenter.x -= cellSizesX[0] / 2f;
            }
            if (data.y.cellSizeType == CellSizeType.CUSTOM)
            {
                if (data.y.aligment == Bound.MIN) firstCellCenter.y += cellSizesY[0] / 2f;
                else if (data.y.aligment == Bound.MAX) firstCellCenter.y -= cellSizesY[0] / 2f;
            }
            if (data.z.cellSizeType == CellSizeType.CUSTOM)
            {
                if (data.z.aligment == Bound.MIN) firstCellCenter.z += cellSizesZ[0] / 2f;
                else if (data.z.aligment == Bound.MAX) firstCellCenter.z -= cellSizesZ[0] / 2f;
            }

            var cells = new System.Collections.Generic.Dictionary<(int i, int j, int k), Bounds>();
            var cellCenter = firstCellCenter;
            var cellSize = Vector3.zero;
            for (int i = 0; i < data.x.cells; ++i)
            {
                cellSize.x = cellSizesX[i];
                if (i > 0) cellCenter.x += (cellSizesX[i - 1] / 2f + spacing.x + cellSize.x / 2f) * data.x.direction;
                cellCenter.y = firstCellCenter.y;
                for (int j = 0; j < data.y.cells; ++j)
                {
                    cellSize.y = cellSizesY[j];
                    if (j > 0) cellCenter.y += (cellSizesY[j - 1] / 2f + spacing.y + cellSize.y / 2) * data.y.direction;
                    cellCenter.z = firstCellCenter.z;
                    for (int k = 0; k < data.z.cells; ++k)
                    {
                        cellSize.z = cellSizesZ[k];
                        if (k > 0)
                            cellCenter.z += (cellSizesZ[k - 1] / 2f + spacing.z + cellSize.z / 2) * data.z.direction;
                        cells.Add((i, j, k), new Bounds(cellCenter, cellSize));
                    }
                }
            }

            void AlignObjectInCell(GameObject obj, Bounds cellBounds)
            {
                var objBounds = BoundsUtils.GetBoundsRecursive(obj.transform, true, data.alignProperty);
                var alignedPosition = obj.transform.position;
                if (data.x.overwrite)
                    alignedPosition.x += GetBound(cellBounds, data.x.aligment).x - GetBound(objBounds, data.x.aligment).x;
                if (data.y.overwrite)
                    alignedPosition.y += GetBound(cellBounds, data.y.aligment).y - GetBound(objBounds, data.y.aligment).y;
                if (data.z.overwrite)
                    alignedPosition.z += GetBound(cellBounds, data.z.aligment).z - GetBound(objBounds, data.z.aligment).z;
                obj.transform.position = alignedPosition;
            }

            const string COMMAND_NAME = "Grid Arrange";
            foreach (var key in objDictionary.Keys)
            {
                var obj = objDictionary[key];
                UnityEditor.Undo.RecordObject(obj.transform, COMMAND_NAME);
                AlignObjectInCell(obj, cells[key]);
            }

            if (data.alignProperty == BoundsUtils.ObjectProperty.BOUNDING_BOX
                && (data.x.cellSizeType == CellSizeType.CUSTOM || data.z.cellSizeType == CellSizeType.CUSTOM
                || data.z.cellSizeType == CellSizeType.CUSTOM))
            {
                var newBounds = BoundsUtils.GetSelectionBounds(selection, true, data.alignProperty);
                var centerDelta = newBounds.center - originalSelectionCenter;
                for (int i = 0; i < selection.Length; ++i) selection[i].transform.position -= centerDelta;
            }
            return true;
        }
    }
}
#pragma warning restore UDR0001
