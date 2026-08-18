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
        public class RandomizeAxisData
        {
            private bool _randomizeAxis = true;
            private RandomUtils.Range _offset = new RandomUtils.Range();
            public bool randomizeAxis { get => _randomizeAxis; set => _randomizeAxis = value; }
            public RandomUtils.Range offset { get => _offset; set => _offset = value; }

        }
        public class RandomizeData
        {
            private RandomizeAxisData _x = new RandomizeAxisData();
            private RandomizeAxisData _y = new RandomizeAxisData();
            private RandomizeAxisData _z = new RandomizeAxisData();
            private float _multiplier = 1f;
            private bool _useConstantValueX;
            private bool _useConstantValueY;
            private bool _useConstantValueZ;
            private bool _separateAxes;
            private bool _useConstantValues;
            public RandomizeAxisData x { get => _x; set => _x = value; }
            public RandomizeAxisData y { get => _y; set => _y = value; }
            public RandomizeAxisData z { get => _z; set => _z = value; }
            public float multiplier { get => _multiplier; set => _multiplier = value; }
            public bool useConstantValueX { get => _useConstantValueX; set => _useConstantValueX = value; }
            public bool useConstantValueY { get => _useConstantValueY; set => _useConstantValueY = value; }
            public bool useConstantValueZ { get => _useConstantValueZ; set => _useConstantValueZ = value; }
            public bool separateAxes { get => _separateAxes; set => _separateAxes = value; }
            public bool useConstantValues { get => _useConstantValues; set => _useConstantValues = value; }
        }
        public static void RandomizePositions(GameObject[] selection, RandomizeData data)
        {
            const string COMMAND_NAME = "Randomize Position";
            foreach (var obj in selection)
            {
                UnityEditor.Undo.RecordObject(obj.transform, COMMAND_NAME);
                obj.transform.position += new Vector3(
                    data.x.randomizeAxis ? data.x.offset.randomValue * data.multiplier : 0f,
                    data.y.randomizeAxis ? data.y.offset.randomValue * data.multiplier : 0f,
                    data.z.randomizeAxis ? data.z.offset.randomValue * data.multiplier : 0f);
            }
        }

        public static void RandomizeRotations(GameObject[] selection, RandomizeData data)
        {
            const string COMMAND_NAME = "Randomize Rotation";
            foreach (var obj in selection)
            {
                UnityEditor.Undo.RecordObject(obj.transform, COMMAND_NAME);
                obj.transform.Rotate(
                    data.x.randomizeAxis ? data.x.offset.randomValue * data.multiplier : 0f,
                    data.y.randomizeAxis ? data.y.offset.randomValue * data.multiplier : 0f,
                    data.z.randomizeAxis ? data.z.offset.randomValue * data.multiplier : 0f);
            }
        }

        public static void RandomizeScales(GameObject[] selection, RandomizeData data, bool separateAxes)
        {
            const string COMMAND_NAME = "Randomize Scale";
            foreach (var obj in selection)
            {
                UnityEditor.Undo.RecordObject(obj.transform, COMMAND_NAME);
                if (separateAxes)
                {
                    obj.transform.localScale += new Vector3(
                        data.x.randomizeAxis ? data.x.offset.randomValue * data.multiplier : 0,
                        data.y.randomizeAxis ? data.y.offset.randomValue * data.multiplier : 0,
                        data.z.randomizeAxis ? data.z.offset.randomValue * data.multiplier : 0);
                }
                else
                {
                    var value = data.x.offset.randomValue * data.multiplier;
                    obj.transform.localScale += new Vector3(value, value, value);
                }
            }
        }
    }
}
#pragma warning restore UDR0001
