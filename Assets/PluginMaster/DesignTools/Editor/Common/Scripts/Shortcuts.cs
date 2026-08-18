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
    public static partial class Shortcuts
    {
#if UNITY_2019_1_OR_NEWER
        public static void UpdateTooltipShortcut(GUIContent button, string tooltip, string shortcutId)
        {
            var shortcut = UnityEditor.ShortcutManagement.ShortcutManager.instance.GetShortcutBinding(shortcutId).ToString();
            if(shortcut != string.Empty) button.tooltip = tooltip + " ... " + shortcut;
        }
#endif
    }
}
#pragma warning restore UDR0001
