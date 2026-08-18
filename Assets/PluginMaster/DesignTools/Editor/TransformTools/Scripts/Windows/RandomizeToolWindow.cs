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
    public abstract class RandomizeToolWindow : BaseToolWindow
    {
        protected TransformTools.RandomizeData _data = new TransformTools.RandomizeData();

        protected enum Attribute
        {
            POSITION,
            ROTATION,
            SCALE
        }
        protected Attribute _attribute = Attribute.POSITION;

        protected override void OnGUI()
        {
            base.OnGUI();

            UnityEditor.EditorGUIUtility.labelWidth = 30;
            UnityEditor.EditorGUIUtility.fieldWidth = 70;

            OnGUIValue();
            GUILayout.Space(8);
            using (new GUILayout.HorizontalScope(UnityEditor.EditorStyles.helpBox))
            {
                GUILayout.FlexibleSpace();
                UnityEditor.EditorGUIUtility.labelWidth = 60;
                _data.multiplier = UnityEditor.EditorGUILayout.FloatField("Multiplier:", _data.multiplier);
            }
            GUILayout.Space(8);
            using (new GUILayout.HorizontalScope())
            {
                var statusStyle = new GUIStyle(UnityEditor.EditorStyles.label);
                GUILayout.Space(8);
                var statusMessage = "";
                if (SelectionManager.topLevelSelection.Length == 0)
                {
                    statusMessage = "No objects selected.";
                    GUILayout.Label(new GUIContent(Resources.Load<Texture2D>("Sprites/Warning")),
                        new GUIStyle() { alignment = TextAnchor.LowerLeft });
                }
                else
                {
                    statusMessage = SelectionManager.topLevelSelection.Length + " objects selected.";
                }
                GUILayout.Label(statusMessage, statusStyle);
                GUILayout.FlexibleSpace();
                using (new UnityEditor.EditorGUI.DisabledGroupScope(SelectionManager.topLevelSelection.Length == 0))
                {
                    if (GUILayout.Button(new GUIContent("Randomize", string.Empty
#if UNITY_2019_1_OR_NEWER
                            + (this is RandomizePositionsWindow ? RandomizePositionsWindow.shortcut
                            : this is RandomizeRotationsWindow ? RandomizeRotationsWindow.shortcut
                            : this is RandomizeScalesWindow ? RandomizeScalesWindow.shortcut : string.Empty)
#endif
                        ), UnityEditor.EditorStyles.miniButtonRight)) Randomize();
                }
            }
        }

        protected virtual void OnGUIValue()
        {
            using (new GUILayout.VerticalScope(UnityEditor.EditorStyles.helpBox)) //X
            {
                _data.useConstantValueX = UnityEditor.EditorGUILayout.ToggleLeft("Constant Value", _data.useConstantValueX);

                if (_data.useConstantValueX)
                {
                    _data.x.offset.v1 = _data.x.offset.v2 = UnityEditor.EditorGUILayout.FloatField(_data.x.offset.v1,
                                UnityEditor.EditorStyles.numberField);
                }
                else
                {
                    using (var toggleGroup
                        = new UnityEditor.EditorGUILayout.ToggleGroupScope("Randomize X", _data.x.randomizeAxis))
                    {
                        _data.x.randomizeAxis = toggleGroup.enabled;
                        using (new GUILayout.HorizontalScope())
                        {
                            GUILayout.FlexibleSpace();
                            GUILayout.Label("Between:");
                            _data.x.offset.v1 = UnityEditor.EditorGUILayout.FloatField(_data.x.offset.v1,
                                UnityEditor.EditorStyles.textField);
                            GUILayout.Space(8);
                            _data.x.offset.v2 = UnityEditor.EditorGUILayout.FloatField(_data.x.offset.v2,
                                UnityEditor.EditorStyles.numberField);
                        }
                    }
                }

            }
            GUILayout.Space(8);
            using (new GUILayout.VerticalScope(UnityEditor.EditorStyles.helpBox)) //Y
            {
                _data.useConstantValueY = UnityEditor.EditorGUILayout.ToggleLeft("Constant Value", _data.useConstantValueY);
                if (_data.useConstantValueY)
                {
                    _data.y.offset.v1 = _data.y.offset.v2 = UnityEditor.EditorGUILayout.FloatField(_data.y.offset.v1,
                                UnityEditor.EditorStyles.numberField);
                }
                else
                {
                    using (var toggleGroup
                        = new UnityEditor.EditorGUILayout.ToggleGroupScope("Randomize Y", _data.y.randomizeAxis))
                    {
                        _data.y.randomizeAxis = toggleGroup.enabled;
                        using (new GUILayout.HorizontalScope())
                        {
                            GUILayout.FlexibleSpace();
                            GUILayout.Label("Between:");
                            _data.y.offset.v1 = UnityEditor.EditorGUILayout.FloatField(_data.y.offset.v1,
                                UnityEditor.EditorStyles.textField);
                            GUILayout.Space(8);
                            _data.y.offset.v2 = UnityEditor.EditorGUILayout.FloatField(_data.y.offset.v2,
                                UnityEditor.EditorStyles.numberField);
                        }
                    }
                }
            }
            GUILayout.Space(8);
            using (new GUILayout.VerticalScope(UnityEditor.EditorStyles.helpBox)) //Z
            {
                _data.useConstantValueZ = UnityEditor.EditorGUILayout.ToggleLeft("Constant Value", _data.useConstantValueZ);
                if (_data.useConstantValueZ)
                {
                    _data.z.offset.v1 = _data.z.offset.v2 = UnityEditor.EditorGUILayout.FloatField(_data.z.offset.v1,
                                UnityEditor.EditorStyles.numberField);
                }
                else
                {
                    using (var toggleGroup
                        = new UnityEditor.EditorGUILayout.ToggleGroupScope("Randomize Z", _data.z.randomizeAxis))
                    {
                        _data.z.randomizeAxis = toggleGroup.enabled;
                        using (new GUILayout.HorizontalScope())
                        {
                            GUILayout.FlexibleSpace();
                            GUILayout.Label("Between:");
                            _data.z.offset.v1 = UnityEditor.EditorGUILayout.FloatField(_data.z.offset.v1,
                                UnityEditor.EditorStyles.textField);
                            GUILayout.Space(8);
                            _data.z.offset.v2 = UnityEditor.EditorGUILayout.FloatField(_data.z.offset.v2,
                                UnityEditor.EditorStyles.numberField);
                        }
                    }
                }
            }
        }

        protected abstract void Randomize();
    }

    public class RandomizePositionsWindow : RandomizeToolWindow
    {
        [UnityEditor.MenuItem("Tools/Plugin Master/Transform Tools/Randomize Positions", false, 1400)]
        public static void ShowWindow() => GetWindow<RandomizePositionsWindow>();

#if UNITY_2019_1_OR_NEWER
        public const string SHORTCUT_ID = "Transform Tools/Randomize Positions";
        [UnityEditor.ShortcutManagement.Shortcut(SHORTCUT_ID)]
        public static void RandomizePositions() => GetWindow<RandomizePositionsWindow>().Randomize();
        public static string shortcut
            => UnityEditor.ShortcutManagement.ShortcutManager.instance.GetShortcutBinding(SHORTCUT_ID).ToString();
#endif

        protected override void OnEnable()
        {
            base.OnEnable();
            titleContent = new GUIContent("Randomize Positions");
            _attribute = RandomizeToolWindow.Attribute.POSITION;
            _data.z.offset.v1 = _data.y.offset.v1 = _data.x.offset.v1 = -1f;
            _data.z.offset.v2 = _data.y.offset.v2 = _data.x.offset.v2 = 1f;
            minSize = new Vector2(240, 220);
        }

        protected override void Randomize()
            => TransformTools.RandomizePositions(SelectionManager.topLevelSelection, _data);
    }

    public class RandomizeRotationsWindow : RandomizeToolWindow
    {
        [UnityEditor.MenuItem("Tools/Plugin Master/Transform Tools/Randomize Rotations", false, 1400)]
        public static void ShowWindow() => GetWindow<RandomizeRotationsWindow>();

#if UNITY_2019_1_OR_NEWER
        public const string SHORTCUT_ID = "Transform Tools/Randomize Rotations";
        [UnityEditor.ShortcutManagement.Shortcut(SHORTCUT_ID)]
        public static void RandomizeRotations() => GetWindow<RandomizeRotationsWindow>().Randomize();
        public static string shortcut
            => UnityEditor.ShortcutManagement.ShortcutManager.instance.GetShortcutBinding(SHORTCUT_ID).ToString();
#endif

        protected override void OnEnable()
        {
            base.OnEnable();
            titleContent = new GUIContent("Randomize Rotations");
            _attribute = RandomizeToolWindow.Attribute.ROTATION;
            _data.z.offset.v1 = _data.y.offset.v1 = _data.x.offset.v1 = -180f;
            _data.z.offset.v2 = _data.y.offset.v2 = _data.x.offset.v2 = 180f;
            minSize = new Vector2(240, 220);
        }

        protected override void Randomize()
        {
            TransformTools.RandomizeRotations(SelectionManager.topLevelSelection, _data);
        }
    }

    public class RandomizeScalesWindow : RandomizeToolWindow
    {
        [UnityEditor.MenuItem("Tools/Plugin Master/Transform Tools/Randomize Scales", false, 1400)]
        public static void ShowWindow() => GetWindow<RandomizeScalesWindow>();

#if UNITY_2019_1_OR_NEWER
        public const string SHORTCUT_ID = "Transform Tools/Randomize Scales";
        [UnityEditor.ShortcutManagement.Shortcut(SHORTCUT_ID)]
        public static void RandomizeScales() => GetWindow<RandomizeScalesWindow>().Randomize();
        public static string shortcut
            => UnityEditor.ShortcutManagement.ShortcutManager.instance.GetShortcutBinding(SHORTCUT_ID).ToString();
#endif
        protected override void OnEnable()
        {
            base.OnEnable();
            titleContent = new GUIContent("Randomize Scales");
            _attribute = RandomizeToolWindow.Attribute.SCALE;
            _data.z.offset.v1 = _data.y.offset.v1 = _data.x.offset.v1 = -0.1f;
            _data.z.offset.v2 = _data.y.offset.v2 = _data.x.offset.v2 = 0.1f;
        }

        protected override void OnGUIValue()
        {
            UnityEditor.EditorGUIUtility.labelWidth = 90;
            _data.separateAxes = UnityEditor.EditorGUILayout.Toggle("Separate Axes", _data.separateAxes);
            

            if (_data.separateAxes)
            {
                minSize = new Vector2(240, 235);
                base.OnGUIValue();
            }
            else
            {
                UnityEditor.EditorGUIUtility.labelWidth = 100;
                _data.useConstantValues = UnityEditor.EditorGUILayout.Toggle("Constant Values", _data.useConstantValues);
                minSize = new Vector2(240, 105);
                using (new GUILayout.VerticalScope(UnityEditor.EditorStyles.helpBox))
                {
                    if (_data.useConstantValues)
                    {
                        _data.x.offset.v1 = _data.x.offset.v2 = _data.y.offset.v1 = _data.y.offset.v2
                            = _data.z.offset.v1 = _data.z.offset.v2 = UnityEditor.EditorGUILayout.FloatField(_data.x.offset.v1,
                                    UnityEditor.EditorStyles.numberField);
                    }
                    else
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            GUILayout.FlexibleSpace();
                            GUILayout.Label("Between:");
                            _data.x.offset.v1 = _data.y.offset.v1 = _data.z.offset.v1
                                = UnityEditor.EditorGUILayout.FloatField(_data.x.offset.v1,
                                UnityEditor.EditorStyles.textField);
                            GUILayout.Space(8);
                            _data.x.offset.v2 = _data.y.offset.v2 = _data.z.offset.v2
                                = UnityEditor.EditorGUILayout.FloatField("max:", _data.x.offset.v2,
                                UnityEditor.EditorStyles.numberField);
                        }
                    }
                }
            }
        }

        protected override void Randomize()
            => TransformTools.RandomizeScales(SelectionManager.topLevelSelection, _data, _data.separateAxes);
    }
}
#pragma warning restore UDR0001
