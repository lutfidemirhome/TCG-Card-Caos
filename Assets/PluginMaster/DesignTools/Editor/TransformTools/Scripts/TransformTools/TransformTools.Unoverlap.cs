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
        public class UnoverlapAxisData
        {
            private bool _unoverlap = true;
            private float _minDistance = 0f;
            public bool unoverlap { get => _unoverlap; set => _unoverlap = value; }
            public float minDistance { get => _minDistance; set => _minDistance = value; }
            public UnoverlapAxisData(bool unoverlap = true, float minDistance = 0f)
            {
                _unoverlap = unoverlap;
                _minDistance = minDistance;
            }
        }

        public class UnoverlapData
        {
            private UnoverlapAxisData _x = new UnoverlapAxisData();
            private UnoverlapAxisData _y = new UnoverlapAxisData();
            private UnoverlapAxisData _z = new UnoverlapAxisData();
            private bool _topmostfilter = true;
            public UnoverlapAxisData x { get => _x; set => _x = value; }
            public UnoverlapAxisData y { get => _y; set => _y = value; }
            public UnoverlapAxisData z { get => _z; set => _z = value; }
            public bool topmostFilter { get => _topmostfilter; set => _topmostfilter = value; }
            public UnoverlapAxisData GetData(PluginMaster.AxesUtils.Axis axis)
            {
                return axis == PluginMaster.AxesUtils.Axis.X ? _x : axis == PluginMaster.AxesUtils.Axis.Y ? _y : _z;
            }
        }

        private class OverlapData
        {
            public Vector3 size = Vector3.zero;
            public float volume = 0f;
            public Vector3 solution = Vector3.zero;
            public float solutionVolume = 0f;
        }

        private class OverlapedObject
        {
            public Bounds bounds;
#if UNITY_6000_3_OR_NEWER
            public EntityId objId = EntityId.None;
#else
            public int objId = -1;
#endif
            public Vector3 transformPosition = Vector3.zero;
            public System.Collections.Generic.List<OverlapData> _dataList = new System.Collections.Generic.List<OverlapData>();
            public int moveCount = 0;
            public bool isOverlaped
            {
                get
                {
                    foreach (var data in _dataList)
                    {
                        if (data.volume != 0) return true;
                    }
                    return false;
                }
            }

            public float overlapedVolume
            {
                get
                {
                    var retVal = 0f;
                    foreach (var data in _dataList) retVal += data.volume;
                    return retVal;
                }
            }

            public float solutionVolume
            {
                get
                {
                    var retVal = 0f;
                    foreach (var data in _dataList) retVal += data.solutionVolume;
                    return retVal;
                }
            }

            public System.Collections.Generic.List<Vector3> solutions
            {
                get
                {
                    var retVal = _dataList.Select(data => data.solution).ToList();
                    retVal.Sort((s1, s2) => s1.magnitude.CompareTo(s2.magnitude));
                    return retVal;
                }
            }

            private void MoveTest(PluginMaster.AxesUtils.Axis axis, bool unoverlap,
                Vector3 solution, ref float minVol, ref Vector3 bestMove)
            {
                var moveSize = GetVectorComponent(solution, axis);
                if (!unoverlap || moveSize == 0) return;
                var move = GetMoveVector(moveSize, axis);
                var obj = new OverlapedObject();
                obj.bounds = bounds;
                obj.bounds.center += move;
                var vol = obj.solutionVolume;
                if (vol < minVol && Mathf.Abs(moveSize) < bestMove.magnitude)
                {
                    minVol = vol;
                    bestMove = move;
                }
            }
            public bool ExecuteBestSolution(UnoverlapData unoverlapData)
            {
                var bestMove = new Vector3(100000, 100000, 100000);
                var minVol = overlapedVolume;
                foreach (var solution in solutions)
                {
                    MoveTest(PluginMaster.AxesUtils.Axis.X, unoverlapData.x.unoverlap, solution, ref minVol, ref bestMove);
                    MoveTest(PluginMaster.AxesUtils.Axis.Y, unoverlapData.y.unoverlap, solution, ref minVol, ref bestMove);
                    MoveTest(PluginMaster.AxesUtils.Axis.Z, unoverlapData.z.unoverlap, solution, ref minVol, ref bestMove);
                }
                if (overlapedVolume > minVol)
                {
                    bounds.center += bestMove;
                    transformPosition = transformPosition + bestMove;
                    ++moveCount;
                    return true;
                }
                return false;
            }
        }

        private static float GetVectorComponent(Vector3 v, PluginMaster.AxesUtils.Axis axis)
        {
            return axis == PluginMaster.AxesUtils.Axis.X ? v.x : axis == PluginMaster.AxesUtils.Axis.Y ? v.y : v.z;
        }

        private static Vector3 GetMoveVector(float move, PluginMaster.AxesUtils.Axis axis)
        {
            return new Vector3(axis == PluginMaster.AxesUtils.Axis.X
                ? move : 0f, axis == PluginMaster.AxesUtils.Axis.Y
                ? move : 0f, axis == PluginMaster.AxesUtils.Axis.Z ? move : 0f);
        }

        private static void GetOverlapedDataAxis(PluginMaster.AxesUtils.Axis axis, UnoverlapData unoverlapData,
            OverlapedObject[] selection, int index, Bounds b1, Bounds b2, bool getSolutionVolumen,
            ref OverlapData retVal, ref float minVol, ref Vector3 bestMove)
        {
            var retValSize = GetVectorComponent(retVal.size, axis);
            if (!unoverlapData.GetData(axis).unoverlap || retValSize <= 0) return;
            var tempObj = new OverlapedObject();
            var tempSelection = selection.ToList();
            tempSelection.RemoveAt(index);
            tempSelection.Insert(0, tempObj);

            var b1Min = GetVectorComponent(b1.min, axis);
            var b1Max = GetVectorComponent(b1.max, axis);
            var b2Min = GetVectorComponent(b2.min, axis);
            var b2Max = GetVectorComponent(b2.max, axis);

            var pSol = b2Max - b1Min;
            var nSol = b2Min - b1Max;

            var moveSize = pSol < -nSol ? pSol : nSol;
            var move = GetMoveVector(moveSize, axis);
            if (getSolutionVolumen)
            {
                tempObj.bounds = b1;
                tempObj.bounds.center += move;

                tempObj._dataList = GetOverlapedData(tempSelection.ToArray(), 0, unoverlapData, false);
                var vol = tempObj.overlapedVolume;
                if (vol < minVol || (vol == minVol && Mathf.Abs(moveSize) < bestMove.magnitude))
                {
                    minVol = vol;
                    bestMove = move;
                    retVal.solution = bestMove;
                    retVal.solutionVolume = tempObj.overlapedVolume;
                }
            }
            else
            {
                retVal.solution = move;
                retVal.solutionVolume = retVal.volume;
            }
        }

        private static OverlapData GetOverlapedData(OverlapedObject[] selection, int index, Bounds b2,
            UnoverlapData unoverlapData, bool getSolutionVolumen)
        {
            Bounds b1 = selection[index].bounds;
            var min = Vector3.Max(b1.min, b2.min);
            var max = Vector3.Min(b1.max, b2.max);

            var retVal = new OverlapData();
            retVal.size = Vector3.Max(max - min, Vector3.zero);

            retVal.volume = (unoverlapData.x.unoverlap ? retVal.size.x : 1f)
                * (unoverlapData.y.unoverlap ? retVal.size.y : 1f) * (unoverlapData.z.unoverlap ? retVal.size.z : 1f);

            if (retVal.volume > 0)
            {
                var bestMove = new Vector3(100000, 100000, 100000);
                var minVol = float.MaxValue;
                GetOverlapedDataAxis(PluginMaster.AxesUtils.Axis.X, unoverlapData,
                    selection, index, b1, b2, getSolutionVolumen,
                    ref retVal, ref minVol, ref bestMove);
                GetOverlapedDataAxis(PluginMaster.AxesUtils.Axis.Y, unoverlapData,
                    selection, index, b1, b2, getSolutionVolumen,
                    ref retVal, ref minVol, ref bestMove);
                GetOverlapedDataAxis(PluginMaster.AxesUtils.Axis.Z, unoverlapData,
                    selection, index, b1, b2, getSolutionVolumen,
                    ref retVal, ref minVol, ref bestMove);
            }
            return retVal;
        }

        private static System.Collections.Generic.List<OverlapData> GetOverlapedData(OverlapedObject[] selection,
            int index, UnoverlapData unoverlapData, bool getSolutionVolumen)
        {
            var retVal = new System.Collections.Generic.List<OverlapData>();
            var target = selection[index];
            foreach (var obj in selection)
            {
                if (obj == target) continue;
                var data = GetOverlapedData(selection, index, obj.bounds, unoverlapData, getSolutionVolumen);
                if (data.size != Vector3.zero && data.solution != Vector3.zero)
                {
                    retVal.Add(data);
                    if (retVal.Count >= 3) break;
                }
            }
            return retVal;
        }

        private static float GetBoundsVolume(Bounds bounds)
        {
            var size = Vector3.Max(bounds.size, new Vector3(0.001f, 0.001f, 0.001f));
            return size.x * size.y * size.z;
        }
        private static int CompareOverlapedObjects(OverlapedObject obj1, OverlapedObject obj2)
        {

            if (obj1.moveCount < obj2.moveCount) return -1;
            else if (obj1.moveCount > obj2.moveCount) return 1;
            else
            {
                float obj1Vol = GetBoundsVolume(obj1.bounds);
                float obj2Vol = GetBoundsVolume(obj2.bounds);
                float v1 = obj1.overlapedVolume / obj1Vol;
                float v2 = obj2.overlapedVolume / obj2Vol;
                if (v1 == v2)
                {
                    var r = obj1Vol.CompareTo(obj2Vol);
                    if (r != 0) return r;
                    v1 = obj1.solutionVolume / obj1Vol;
                    v2 = obj2.solutionVolume / obj2Vol;
                    return v1.CompareTo(v2);
                }
                else if (v1 == 0) return 1;
                else if (v2 == 0) return -1;
                else
                {
                    var r = v1.CompareTo(v2);
                    if (r != 0) return r;
                    return obj1Vol.CompareTo(obj2Vol);
                }
            }
        }

        public class Unoverlapper
        {
#if UNITY_6000_3_OR_NEWER
            private readonly (EntityId objId, Bounds bounds)[] _selection;
#else
            private readonly (int objId, Bounds bounds)[] _selection;
#endif
            private readonly UnoverlapData _unoverlapData;
            private bool _cancel = false;
#if UNITY_6000_3_OR_NEWER
            public Unoverlapper((EntityId objId, Bounds bounds)[] selection, UnoverlapData unoverlapData)
#else
            public Unoverlapper((int objId, Bounds bounds)[] selection, UnoverlapData unoverlapData)
#endif
            {
                _selection = selection;
                _unoverlapData = unoverlapData;
            }

            public event System.Action<float> progressChanged;
#if UNITY_6000_3_OR_NEWER
            public event System.Action<(EntityId objId, Vector3 offset)[]> OnDone;
#else
            public event System.Action<(int objId, Vector3 offset)[]> OnDone;
#endif

            public void RemoveOverlaps()
            {
                if (!_unoverlapData.x.unoverlap && !_unoverlapData.y.unoverlap && !_unoverlapData.z.unoverlap) return;
                BoundsUtils.ClearBoundsDictionaries();
                var minSize = new Vector3(0.001f, 0.001f, 0.001f);

                var overlapedList = new System.Collections.Generic.List<OverlapedObject>();

                foreach (var obj in _selection)
                {
                    var overlapedObj = new OverlapedObject();
                    overlapedObj.bounds = obj.bounds;
                    overlapedObj.objId = obj.objId;
                    overlapedObj.bounds.center = new Vector3
                        (_unoverlapData.x.unoverlap ? overlapedObj.bounds.center.x : 0f,
                        _unoverlapData.y.unoverlap ? overlapedObj.bounds.center.y : 0f,
                        _unoverlapData.z.unoverlap ? overlapedObj.bounds.center.z : 0f);
                    overlapedObj.bounds.size = Vector3.Max(overlapedObj.bounds.size, minSize)
                        + new Vector3(_unoverlapData.x.minDistance, _unoverlapData.y.minDistance,
                        _unoverlapData.z.minDistance);
                    overlapedList.Add(overlapedObj);
                }

                var i = 0;
                foreach (var obj in overlapedList)
                {
                    obj._dataList = GetOverlapedData(overlapedList.ToArray(), i, _unoverlapData, true);
                    ++i;
                }

                overlapedList.Sort((obj1, obj2) => CompareOverlapedObjects(obj1, obj2));
                var prevProgress = 0f;
                var overlapedObjects = 0;
                do
                {
                    if (_cancel) return;
                    var first = overlapedList[0];
                    if (!first.isOverlaped)
                    {
                        overlapedList.RemoveAt(0);
                        overlapedList.Add(first);
                        continue;
                    }
                    else
                    {
                        var executed = first.ExecuteBestSolution(_unoverlapData);
                        if (!executed)
                        {
                            overlapedList.RemoveAt(0);
                            overlapedList.Add(first);
                            continue;
                        }
                        else
                        {
                            overlapedList.Sort((obj1, obj2) => CompareOverlapedObjects(obj1, obj2));
                            if (overlapedList[0] == first)
                            {
                                overlapedList.RemoveAt(0);
                                overlapedList.Add(first);
                            }
                        }
                    }
                    overlapedObjects = 0;
                    i = 0;
                    foreach (var obj in overlapedList)
                    {
                        obj._dataList = GetOverlapedData(overlapedList.ToArray(), i, _unoverlapData, true);
                        ++i;
                        if (obj.isOverlaped) ++overlapedObjects;
                    }

                    var progress = Mathf.Max(1f - (float)overlapedObjects / (float)_selection.Length, prevProgress);
                    if (prevProgress != progress) progressChanged(progress);
                    prevProgress = progress;
                } while (overlapedObjects > 0);

                var boundsArray = overlapedList.Select(obj => (obj.objId, obj.transformPosition)).ToArray();
                OnDone(boundsArray);
            }

            public void Cancel()
            {
                _cancel = true;
            }
        }
    }
}
#pragma warning restore UDR0001
