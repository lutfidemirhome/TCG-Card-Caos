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
    public class BaseToolWindow : UnityEditor.EditorWindow
    {
        protected GUISkin _skin = null;
        protected GUIContent _warningIcon = null;
        private void LoadSkin() => _skin = Resources.Load<GUISkin>("TransformToolsSkin");
        protected GUISkin skin
        {
            get
            {
                if (_skin == null) LoadSkin();
                return _skin;
            }
        }
        protected virtual void OnEnable()
        {
            LoadSkin();
            SelectionManager.UpdateSelection();
            _warningIcon = new GUIContent(Resources.Load<Texture2D>("Sprites/Warning"));
        }
        protected virtual void OnGUI() 
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                GUI.FocusControl(null);
                Repaint();
            }
        }
    }
}
#pragma warning restore UDR0001
