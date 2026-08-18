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
    public static class AxesUtils
    {
        public enum Axis { X, Y, Z }
        [System.Serializable]
        public class SignedAxis
        {
            [SerializeField] private Axis _axis;
            [SerializeField] private int _sign;
            private SignedAxis(Axis axis, int sign)
            {
                _axis = axis;
                _sign = sign;
            }
            public const int RIGHT = 0;
            public const int LEFT = 1;
            public const int UP = 2;
            public const int DOWN = 3;
            public const int FORWARD = 4;
            public const int BACK = 5;

            private static readonly System.Collections.Generic.Dictionary<Vector3, SignedAxis> _dictionary
                = new System.Collections.Generic.Dictionary<Vector3, SignedAxis>
            {
                { Vector3.right, RIGHT },
                { Vector3.left, LEFT },
                { Vector3.up, UP },
                { Vector3.down, DOWN },
                { Vector3.forward, FORWARD },
                { Vector3.back, BACK }
            };
            public Axis axis { get => _axis; set => _axis = value; }
            public int sign { get => _sign; set => _sign = value; }
            public static implicit operator Axis(SignedAxis value) => value._axis;
            public static implicit operator SignedAxis(Axis value)
                => (value == Axis.X ? RIGHT : value == Axis.Y ? UP : FORWARD);
            public static implicit operator int(SignedAxis value) => value.GetHashCode();
            public static implicit operator SignedAxis(int value)
            {
                if(value == RIGHT) return new SignedAxis(Axis.X, 1);
                else if (value == LEFT) return new SignedAxis(Axis.X, -1);
                else if (value == UP) return new SignedAxis(Axis.Y, 1);
                else if (value == DOWN) return new SignedAxis(Axis.Y, -1);
                else if (value == FORWARD) return new SignedAxis(Axis.Z, 1);
                else return new SignedAxis(Axis.Z, -1);
            }
            public static implicit operator SignedAxis(Vector3 value)
            {
                value.x = Mathf.Clamp(Mathf.Round(value.x), -1f, 1f);
                value.y = Mathf.Clamp(Mathf.Round(value.y), -1f, 1f);
                value.z = Mathf.Clamp(Mathf.Round(value.z), -1f, 1f);
                if (_dictionary.ContainsKey(value)) return _dictionary[value];
                if (Mathf.Approximately(value.x, 0f) && Mathf.Approximately(value.y, 1f) && Mathf.Approximately(value.z, 0f))
                    return UP;
                if (Mathf.Approximately(value.x, 0f) && Mathf.Approximately(value.y, -1f) && Mathf.Approximately(value.z, 0f))
                    return DOWN;
                if (Mathf.Approximately(value.x, 1f) && Mathf.Approximately(value.y, 0f) && Mathf.Approximately(value.z, 0f))
                    return RIGHT;
                if (Mathf.Approximately(value.x, -1f) && Mathf.Approximately(value.y, 0f) && Mathf.Approximately(value.z, 0f))
                    return LEFT;
                if (Mathf.Approximately(value.x, 0f) && Mathf.Approximately(value.y, 0f) && Mathf.Approximately(value.z, 1f))
                    return FORWARD;
                if (Mathf.Approximately(value.x, 0f) && Mathf.Approximately(value.y, 0f) && Mathf.Approximately(value.z, -1f))
                    return BACK;
                if (Mathf.Approximately(value.x, 0f) && Mathf.Approximately(value.y, 0f) && Mathf.Approximately(value.z, 0f))
                    Debug.LogWarning($"Zero: ({value.x}, {value.y}, {value.z})");
                else Debug.LogWarning($"Rotated Axis: ({value.x}, {value.y}, {value.z})");
                return UP;
            }
            public override bool Equals(object obj) => Equals(obj as SignedAxis);
            public bool Equals(SignedAxis other) => _axis == other._axis && _sign == other._sign;
            public override int GetHashCode() => (int)_axis * 2 + (_sign > 0 ? 0 : 1);
            public static bool operator ==(SignedAxis l, SignedAxis r) => l.Equals(r);
            public static bool operator !=(SignedAxis l, SignedAxis r) => !l.Equals(r);
            public static implicit operator Vector3(SignedAxis value)
            {
                if (value == RIGHT) return Vector3.right;
                if (value == LEFT) return Vector3.left;
                if (value == UP) return Vector3.up;
                if (value == DOWN) return Vector3.down;
                if (value == FORWARD) return Vector3.forward;
                else return Vector3.back;
            }

            public static Vector3 GetEulerAnglesFromAxes(SignedAxis forwardAxis, SignedAxis upwardAxis)
            {
                var angle = Vector3.zero;
                if (upwardAxis == UP)
                {
                    if (forwardAxis == RIGHT) angle.y = 270;
                    else if (forwardAxis == LEFT) angle.y = 90;
                    else if (forwardAxis == BACK) angle.y = 180;
                    return angle;
                }
                if (upwardAxis == DOWN)
                {
                    angle.z = 180;
                    if (forwardAxis == RIGHT) angle.y = 90;
                    else if (forwardAxis == LEFT) angle.y = 270;
                    else if (forwardAxis == BACK) angle.y = 180;
                    return angle;
                }
                if (upwardAxis == FORWARD)
                {
                    angle.x = 270;
                    if (forwardAxis == RIGHT) angle.y = 270;
                    else if (forwardAxis == LEFT) angle.y = 90;
                    else if (forwardAxis == UP) angle.y = 180;
                    return angle;
                }
                if (upwardAxis == BACK)
                {
                    angle.x = 90;
                    if (forwardAxis == RIGHT) angle.y = 270;
                    else if (forwardAxis == LEFT) angle.y = 90;
                    else if (forwardAxis == DOWN) angle.y = 180;
                    return angle;
                }
                if (upwardAxis == RIGHT)
                {
                    angle.z = 90;
                    if (forwardAxis == UP) angle.y = 90;
                    else if (forwardAxis == DOWN) angle.y = 270;
                    else if (forwardAxis == BACK) angle.y = 180;
                    return angle;
                }
                if (upwardAxis == LEFT)
                {
                    angle.z = 270;
                    if (forwardAxis == UP) angle.y = 270;
                    else if (forwardAxis == DOWN) angle.y = 90;
                    else if (forwardAxis == BACK) angle.y = 180;
                    return angle;
                }
                return angle;
            }
        }
        public static float GetAxisValue(Vector3 v, Axis axis) => (axis == Axis.X ? v.x : axis == Axis.Y ? v.y : v.z);
        public static void SetAxisValue(ref Vector3 v, Axis axis, float value)
        {
            if (axis == Axis.X) v.x = value;
            else if (axis == Axis.Y) v.y = value;
            else v.z = value;
        }
        public static void AddValueToAxis(ref Vector3 v, Axis axis, float value)
        {
            if (axis == Axis.X) v.x += value;
            else if (axis == Axis.Y) v.y += value;
            else v.z += value;
        }
        public static Axis[] GetOtherAxes(Axis axis) 
            => (axis == Axis.X ? new Axis[] { Axis.Y, Axis.Z } 
            : axis == Axis.Y ? new Axis[] { Axis.X, Axis.Z } : new Axis[] { Axis.X, Axis.Y });
        public static Axis GetOtherAxis(Axis axis1, Axis axis2)
            => ((axis1 == Axis.X && axis2 == Axis.Z) || (axis1 == Axis.Z && axis2 == Axis.X) ? Axis.Y
            : (axis1 == Axis.Y && axis2 == Axis.Z) || (axis1 == Axis.Z && axis2 == Axis.Y) ? Axis.X : Axis.Z);
        public static Vector3 GetVector(float signedSize, Axis axis)
            => (axis == Axis.X ? Vector3.right : axis == Axis.Y ? Vector3.up : Vector3.forward) * signedSize;
        public static Vector3 GetVector(Vector3 v, Axis axis)
            => (axis == Axis.X ? Vector3.right * v.x : axis == Axis.Y ? Vector3.up * v.y : Vector3.forward * v.z);
        public static bool IsParallelToAxis(Vector3 v, out Axis axis)
        {
            axis = Axis.Y;
            if (v.x == 0 && v.z == 0)
            {
                axis = Axis.Y;
                return true;
            }
            if (v.y == 0 && v.z == 0)
            {
                axis = Axis.X;
                return true;
            }
            if (v.x == 0 && v.y == 0)
            {
                axis = Axis.Z;
                return true;
            }
            return false;
        }
    }
}
#pragma warning restore UDR0001
