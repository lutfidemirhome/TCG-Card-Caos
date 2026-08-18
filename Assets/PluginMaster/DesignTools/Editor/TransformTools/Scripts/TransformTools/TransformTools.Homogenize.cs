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
        public class HomogenizeAxis
        {
            private bool _homogenize = true;
            private float _strength = 0.1f;
            public bool homogenize { get => _homogenize; set => _homogenize = value; }
            public float strength { get => _strength; set => _strength = value; }
        }
        public class HomogenizeData
        {
            private HomogenizeAxis _x = new HomogenizeAxis();
            private HomogenizeAxis _y = new HomogenizeAxis();
            private HomogenizeAxis _z = new HomogenizeAxis();
            public HomogenizeAxis x { get => _x; set => _x = value; }
            public HomogenizeAxis y { get => _y; set => _y = value; }
            public HomogenizeAxis z { get => _z; set => _z = value; }
        }
        public static void HomogenizeSpacing(GameObject[] selection, HomogenizeData data)
        {
            const string COMMAND_NAME = "Homogenize Spacing";
            foreach (var obj in selection) UnityEditor.Undo.RecordObject(obj.transform, COMMAND_NAME);
            if (data.x.homogenize) DistributeGaps(selection, PluginMaster.AxesUtils.Axis.X, data.x.strength, false);
            if (data.y.homogenize) DistributeGaps(selection, PluginMaster.AxesUtils.Axis.Y, data.y.strength, false);
            if (data.z.homogenize) DistributeGaps(selection, PluginMaster.AxesUtils.Axis.Z, data.z.strength, false);
        }
        public static void HomogenizeRotation(GameObject[] selection, HomogenizeData data)
        {
            const string COMMAND_NAME = "Homogenize Rotation";
            var sum = Vector3.zero;
            foreach (var obj in selection)
            {
                var euler = obj.transform.eulerAngles;
                if (euler.x < 0) euler.x = 360f + euler.x;
                if (euler.y < 0) euler.y = 360f + euler.y;
                if (euler.z < 0) euler.z = 360f + euler.z;
                sum += euler;
            }
            var average = sum / (float)selection.Length;
            foreach (var obj in selection)
            {
                UnityEditor.Undo.RecordObject(obj.transform, COMMAND_NAME);
                var newEulerAngles = obj.transform.eulerAngles;
                if (data.x.homogenize)
                    newEulerAngles.x = Mathf.LerpAngle(obj.transform.eulerAngles.x, average.x, data.x.strength);
                if (data.y.homogenize)
                    newEulerAngles.y = Mathf.LerpAngle(obj.transform.eulerAngles.y, average.y, data.y.strength);
                if (data.z.homogenize)
                    newEulerAngles.z = Mathf.LerpAngle(obj.transform.eulerAngles.z, average.z, data.z.strength);
                obj.transform.eulerAngles = newEulerAngles;
            }
        }

        public static void HomogenizeScale(GameObject[] selection, HomogenizeData data)
        {
            const string COMMAND_NAME = "Homogenize Scale";
            var sum = Vector3.zero;
            foreach (var obj in selection) sum += obj.transform.localScale;
            var average = sum / (float)selection.Length;
            foreach (var obj in selection)
            {
                UnityEditor.Undo.RecordObject(obj.transform, COMMAND_NAME);
                var newScale = obj.transform.localScale;
                if (data.x.homogenize) newScale.x = Mathf.Lerp(obj.transform.localScale.x, average.x, data.x.strength);
                if (data.y.homogenize) newScale.y = Mathf.Lerp(obj.transform.localScale.y, average.y, data.y.strength);
                if (data.z.homogenize) newScale.z = Mathf.Lerp(obj.transform.localScale.z, average.z, data.z.strength);
                obj.transform.localScale = newScale;
            }
        }
    }
}
#pragma warning restore UDR0001
