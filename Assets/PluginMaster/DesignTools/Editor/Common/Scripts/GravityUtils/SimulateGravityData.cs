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
    [System.Serializable]
    public class SimulateGravityData
    {
        [SerializeField] private int _maxIterations = 1000;
        [SerializeField] private Vector3 _gravity = Physics.gravity;
        [SerializeField] private float _drag = 0f;
        [SerializeField] private float _angularDrag = 0.05f;
        [SerializeField] private float _maxSpeed = 100;
        private float _maxSpeedSquared = 10000;
        [SerializeField] private float _maxAngularSpeed = 10;
        private float _maxAngularSpeedSquared = 100;
        [SerializeField] private float _mass = 1f;
        [SerializeField] private bool _changeLayer = false;
        [SerializeField] private int _tempLayer = 0;
        [SerializeField] private bool _ignoreSceneColliders = false;
        public int maxIterations
        {
            get => _maxIterations;
            set
            {
                value = Mathf.Clamp(value, 1, 100000);
                if (_maxIterations == value) return;
                _maxIterations = value;
            }
        }
        public Vector3 gravity
        {
            get => _gravity;
            set
            {
                if (_gravity == value) return;
                _gravity = value;
            }
        }
        public float drag
        {
            get => _drag;
            set
            {
                value = Mathf.Max(value, 0f);
                if (_drag == value) return;
                _drag = value;
            }
        }
        public float angularDrag
        {
            get => _angularDrag;
            set
            {
                value = Mathf.Max(value, 0f);
                if (_angularDrag == value) return;
                _angularDrag = value;
            }
        }
        public float maxSpeed
        {
            get => _maxSpeed;
            set
            {
                value = Mathf.Max(value, 0f);
                if (_maxSpeed == value) return;
                _maxSpeed = value;
                _maxSpeedSquared = _maxSpeed * _maxSpeed;
            }
        }
        public float maxAngularSpeed
        {
            get => _maxAngularSpeed;
            set
            {
                value = Mathf.Max(value, 0f);
                if (_maxAngularSpeed == value) return;
                _maxAngularSpeed = value;
                _maxAngularSpeedSquared = _maxAngularSpeed * _maxAngularSpeed;
            }
        }
        public float maxSpeedSquared => _maxSpeedSquared;
        public float maxAngularSpeedSquared => _maxAngularSpeedSquared;
        public float mass
        {
            get => _mass;
            set
            {
                value = Mathf.Max(value, 1e-7f);
                if (_mass == value) return;
                _mass = value;
            }
        }
        public bool changeLayer
        {
            get => _changeLayer;
            set
            {
                if (_changeLayer == value) return;
                _changeLayer = value;
            }
        }
        public int tempLayer
        {
            get => _tempLayer;
            set
            {
                if (_tempLayer == value) return;
                _tempLayer = value;
            }
        }

        public bool ignoreSceneColliders
        {
            get => _ignoreSceneColliders;
            set
            {
                if (_ignoreSceneColliders == value) return;
                _ignoreSceneColliders = value;
            }
        }
        public void Copy(SimulateGravityData other)
        {
            _maxIterations = other._maxIterations;
            _gravity = other._gravity;
            _drag = other._drag;
            _angularDrag = other._angularDrag;
            _maxSpeed = other._maxSpeed;
            _maxSpeedSquared = other._maxSpeedSquared;
            _maxAngularSpeed = other._maxAngularSpeed;
            _maxAngularSpeedSquared = other._maxAngularSpeedSquared;
            _mass = other._mass;
            _changeLayer = other._changeLayer;
            _tempLayer = other._tempLayer;
            _ignoreSceneColliders = other._ignoreSceneColliders;
        }
    }
}
#pragma warning restore UDR0001
