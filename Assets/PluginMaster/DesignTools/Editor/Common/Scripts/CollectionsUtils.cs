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
    public class SerializableDictionary<KEY, VALUE> : ISerializationCallbackReceiver
    {
        private System.Collections.Generic.Dictionary<KEY, VALUE> _dict
            = new System.Collections.Generic.Dictionary<KEY, VALUE>();

        [SerializeField] private KEY[] _keys;
        [SerializeField] private VALUE[] _values;

        public void Add(KEY key, VALUE value) => _dict.Add(key, value);
        public bool ContainsKey(KEY key) => _dict.ContainsKey(key);
        public System.Collections.Generic.ICollection<KEY> Keys => _dict.Keys;

        public VALUE this[KEY key]
        {
            get => _dict[key];
            set => _dict[key] = value;
        }

        public System.Collections.Generic.Dictionary<KEY, VALUE> ToDictionary()
            => new System.Collections.Generic.Dictionary<KEY, VALUE>(_dict);

        public void OnBeforeSerialize()
        {
            int count = _dict.Count;
            _keys = new KEY[count];
            _values = new VALUE[count];
            int i = 0;
            foreach (var kv in _dict)
            {
                _keys[i] = kv.Key;
                _values[i] = kv.Value;
                ++i;
            }
        }

        public void OnAfterDeserialize()
        {
            if (_keys == null || _values == null) return;
            _dict = new System.Collections.Generic.Dictionary<KEY, VALUE>();
            int count = Mathf.Min(_keys.Length, _values.Length);
            for (int i = 0; i < count; ++i) _dict.Add(_keys[i], _values[i]);
        }
    }
}
#pragma warning restore UDR0001
