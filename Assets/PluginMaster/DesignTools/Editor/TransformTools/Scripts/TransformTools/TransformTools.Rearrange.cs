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
        public static void Rearrange(GameObject[] selection, ArrangeBy arrangeBy)
        {
            const string COMMAND_NAME = "Rearrange";
            if (selection.Length < 2) return;
            if (arrangeBy == ArrangeBy.HIERARCHY_ORDER) selection = SortByHierarchy(selection);
            var firstPosition = selection[0].transform.position;
            for (int i = 0; i < selection.Length - 1; ++i)
            {
                UnityEditor.Undo.RecordObject(selection[i].transform, COMMAND_NAME);
                selection[i].transform.position = selection[i + 1].transform.position;
            }
            UnityEditor.Undo.RecordObject(selection[selection.Length - 1].transform, COMMAND_NAME);
            selection[selection.Length - 1].transform.position = firstPosition;
        }
    }
}
#pragma warning restore UDR0001
