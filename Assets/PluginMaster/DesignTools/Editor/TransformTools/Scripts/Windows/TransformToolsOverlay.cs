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
#if UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace PluginMaster
{
    [Overlay(typeof(UnityEditor.SceneView), "Transform Tools", true)]
    public partial class TransformToolsOverlay : Overlay
    {
        private TransformTools.RelativeTo _relativeTo = TransformTools.RelativeTo.LAST_SELECTED;
        private bool _filteredByTopLevel = true;
        private BoundsUtils.ObjectProperty _alignObjectProperty = BoundsUtils.ObjectProperty.BOUNDING_BOX;
        private Space _space = Space.World;
        private GameObject _pivot = null;

        private bool _alignOpen = true;
        private bool _distributeOpen = true;
        private bool _arrangeOpen = true;
        private bool _progressionOpen = true;
        private bool _randomizeOpen = true;
        private bool _homogenizeOpen = true;
        private bool _editPivotOpen = true;
        private bool _miscellaneousOpen = true;
        public TransformToolsOverlay()
        {
            CreatePanelContent();
        }

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement { name = "Transform Tools" };
#if UNITY_2022_2_OR_NEWER
            DoLoadCollapsedIcon();
#endif
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingTop = 2;
            root.style.paddingBottom = 2;
            root.style.paddingLeft = 2;
            root.style.paddingRight = 2;
            root.style.minWidth = 136;

            root.Add(CreateAlignSection());
            root.Add(CreateHorizontalSeparator());
            root.Add(CreateDistributeSection());
            root.Add(CreateHorizontalSeparator());
            root.Add(CreateArrangeSection());
            root.Add(CreateHorizontalSeparator());
            root.Add(CreateProgressionSection());
            root.Add(CreateHorizontalSeparator());
            root.Add(CreateRandomizeSection());
            root.Add(CreateHorizontalSeparator());
            root.Add(CreateHomogenizeSection());
            root.Add(CreateHorizontalSeparator());
            root.Add(CreateEditPivotSection(root));
            root.Add(CreateHorizontalSeparator());
            root.Add(CreateMiscellaneousSection());

            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (_pivot != null)
                {
                    UnityEditor.Selection.activeGameObject = _pivot.transform.parent.gameObject;
                    Object.DestroyImmediate(_pivot);
                    _pivot = null;
                }
            });

            return root;
        }

        private VisualElement CreateHorizontalSeparator()
        {
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            separator.style.marginTop = 4;
            separator.style.marginBottom = 4;
            return separator;
        }

#if UNITY_2022_2_OR_NEWER
        private async void DoLoadCollapsedIcon()
        {
            await System.Threading.Tasks.Task.Delay(1000);
            var collapsedIcon = Resources.Load<Texture2D>("Sprites/TransformTools");
            if (collapsedIcon == null) DoLoadCollapsedIcon();
        }
#endif
        private Button CreateImageButton(string spriteName, string tooltip, System.Action onClick)
        {
            var button = new Button(onClick) { tooltip = tooltip };
            button.style.width = 24;
            button.style.height = 24;
            button.style.marginLeft = 1;
            button.style.marginRight = 1;
            button.style.paddingLeft = 2;
            button.style.paddingRight = 0;
            button.style.paddingTop = 2;
            button.style.paddingBottom = 0;
            button.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);

            DoLoadButtonIcon(button, spriteName);
            return button;
        }

        private async void DoLoadButtonIcon(Button button, string spriteName)
        {
            var texture = Resources.Load<Texture2D>($"Sprites/{spriteName}");
            if (texture == null)
            {
                await System.Threading.Tasks.Task.Delay(1000);
                DoLoadButtonIcon(button, spriteName);
                return;
            }
            var icon = new Image { image = texture };
            icon.style.width = 16;
            icon.style.height = 16;
            icon.style.alignSelf = Align.Center;
            button.Add(icon);
        }

        private VisualElement CreateButtonRow(params Button[] buttons)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 2;
            foreach (var btn in buttons) row.Add(btn);
            return row;
        }
        private Foldout CreateTrackedFoldout(string title, bool currentValue, System.Action<bool> onChanged)
        {
            var foldout = new Foldout { text = title, value = currentValue };
            foldout.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return foldout;
        }

        #region ALIGN
        private VisualElement CreateAlignSection()
        {
            var foldout = CreateTrackedFoldout("Align", _alignOpen, v => _alignOpen = v);
            var content = foldout.contentContainer;

            var relativeToLabel = new Label("Relative to:");
            relativeToLabel.style.marginBottom = 2;
            content.Add(relativeToLabel);

            var relativeToChoices = new System.Collections.Generic.List<string>
            {
                "Last Selected", "First Selected", "Biggest Object",
                "Smallest Object", "Selection", "Canvas"
            };
            var relativeToDropdown = new DropdownField(relativeToChoices, (int)_relativeTo);
            relativeToDropdown.style.width = 126;
            content.Add(relativeToDropdown);

            var spaceRow = new VisualElement();
            spaceRow.style.flexDirection = FlexDirection.Row;
            spaceRow.style.alignItems = Align.Center;
            spaceRow.style.marginTop = 2;

            var spaceLabel = new Label("Space:");
            spaceLabel.style.marginRight = 4;
            spaceLabel.style.width = 40;
            spaceRow.Add(spaceLabel);

            var spaceChoices = new System.Collections.Generic.List<string> { "Global", "Local" };
            var spaceDropdown = new DropdownField(spaceChoices, (int)_space);
            spaceDropdown.style.width = 82;
            spaceDropdown.RegisterValueChangedCallback(evt =>
            {
                _space = (Space)spaceChoices.IndexOf(evt.newValue);
            });
            spaceRow.Add(spaceDropdown);
            content.Add(spaceRow);

            relativeToDropdown.RegisterValueChangedCallback(evt =>
            {
                _relativeTo = (TransformTools.RelativeTo)relativeToChoices.IndexOf(evt.newValue);
                bool show = _relativeTo != TransformTools.RelativeTo.SELECTION
                    && _relativeTo != TransformTools.RelativeTo.CANVAS;
                spaceRow.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            });

            bool showSpace = _relativeTo != TransformTools.RelativeTo.SELECTION
                && _relativeTo != TransformTools.RelativeTo.CANVAS;
            spaceRow.style.display = showSpace ? DisplayStyle.Flex : DisplayStyle.None;

            var alignPropLabel = new Label("Align property:");
            alignPropLabel.style.marginTop = 2;
            alignPropLabel.style.marginBottom = 2;
            content.Add(alignPropLabel);

            var alignPropChoices = new System.Collections.Generic.List<string>
            { "Bounding Box", "Center", "Pivot" };
            var alignPropDropdown = new DropdownField(alignPropChoices, (int)_alignObjectProperty);
            alignPropDropdown.style.width = 126;
            alignPropDropdown.RegisterValueChangedCallback(evt =>
            {
                _alignObjectProperty = (BoundsUtils.ObjectProperty)alignPropChoices.IndexOf(evt.newValue);
            });
            content.Add(alignPropDropdown);

            var filterRow = new VisualElement();
            filterRow.style.flexDirection = FlexDirection.Row;
            filterRow.style.alignItems = Align.Center;
            filterRow.style.marginTop = 2;
            filterRow.style.marginBottom = 4;

            var filterToggle = new Toggle { value = _filteredByTopLevel };
            filterToggle.RegisterValueChangedCallback(evt => _filteredByTopLevel = evt.newValue);
            filterRow.Add(filterToggle);

            var filterLabel = new Label("Topmost filter");
            filterRow.Add(filterLabel);
            content.Add(filterRow);

            // X axis
            content.Add(CreateButtonRow(
                CreateImageButton("AlignRightToAnchorLeft",
                    "Align right edges of objects to the left edge of the anchor",
                    () => DoAlign(AxesUtils.Axis.X, TransformTools.Bound.MAX, true)),
                CreateImageButton("AlignLeft", "Align left edges",
                    () => DoAlign(AxesUtils.Axis.X, TransformTools.Bound.MIN, false)),
                CreateImageButton("AlignCenterX", "Center on X axis",
                    () => DoAlign(AxesUtils.Axis.X, TransformTools.Bound.CENTER, false)),
                CreateImageButton("AlignRight", "Align right edges",
                    () => DoAlign(AxesUtils.Axis.X, TransformTools.Bound.MAX, false)),
                CreateImageButton("AlignLeftToAnchorRight",
                    "Align left edges of objects to the right edge of the anchor",
                    () => DoAlign(AxesUtils.Axis.X, TransformTools.Bound.MIN, true))
            ));

            // Y axis
            content.Add(CreateButtonRow(
                CreateImageButton("AlignTopToAnchorBottom",
                    "Align top edges of objects to the bottom edge of the anchor",
                    () => DoAlign(AxesUtils.Axis.Y, TransformTools.Bound.MAX, true)),
                CreateImageButton("AlignBottom", "Align bottom edges",
                    () => DoAlign(AxesUtils.Axis.Y, TransformTools.Bound.MIN, false)),
                CreateImageButton("AlignCenterY", "Center on Y axis",
                    () => DoAlign(AxesUtils.Axis.Y, TransformTools.Bound.CENTER, false)),
                CreateImageButton("AlignTop", "Align top edges",
                    () => DoAlign(AxesUtils.Axis.Y, TransformTools.Bound.MAX, false)),
                CreateImageButton("AlignBottomToAnchorTop",
                    "Align bottom edges of objects to the top edge of the anchor",
                    () => DoAlign(AxesUtils.Axis.Y, TransformTools.Bound.MIN, true))
            ));

            // Z axis
            content.Add(CreateButtonRow(
                CreateImageButton("AlignFrontToAnchorBack",
                    "Align front edges of objects to the back edge of the anchor",
                    () => DoAlign(AxesUtils.Axis.Z, TransformTools.Bound.MAX, true)),
                CreateImageButton("AlignBack", "Align back edges",
                    () => DoAlign(AxesUtils.Axis.Z, TransformTools.Bound.MIN, false)),
                CreateImageButton("AlignCenterZ", "Center on Z axis",
                    () => DoAlign(AxesUtils.Axis.Z, TransformTools.Bound.CENTER, false)),
                CreateImageButton("AlignFront", "Align front edges",
                    () => DoAlign(AxesUtils.Axis.Z, TransformTools.Bound.MAX, false)),
                CreateImageButton("AlignBackToAnchorFront",
                    "Align back edges of objects to the front edge of the anchor",
                    () => DoAlign(AxesUtils.Axis.Z, TransformTools.Bound.MIN, true))
            ));

            return foldout;
        }

        private void DoAlign(AxesUtils.Axis axis, TransformTools.Bound bound, bool alignToAnchor)
        {
            TransformTools.Align(SelectionManager.GetSelection(_filteredByTopLevel), _relativeTo,
                axis, _space, bound, alignToAnchor, _filteredByTopLevel, _alignObjectProperty);
        }
        #endregion

        #region DISTRIBUTE
        private VisualElement CreateDistributeSection()
        {
            var foldout = CreateTrackedFoldout("Distribute", _distributeOpen, v => _distributeOpen = v);
            var content = foldout.contentContainer;

            // X axis
            content.Add(CreateButtonRow(
                CreateImageButton("DistributeLeft", "Distribute left edges equidistantly",
                    () => TransformTools.Distribute(SelectionManager.topLevelSelection,
                        AxesUtils.Axis.X, TransformTools.Bound.MIN)),
                CreateImageButton("DistributeCenterX", "Distribute centers equidistantly on the X axis",
                    () => TransformTools.Distribute(SelectionManager.topLevelSelection,
                        AxesUtils.Axis.X, TransformTools.Bound.CENTER)),
                CreateImageButton("DistributeRight", "Distribute right edges equidistantly",
                    () => TransformTools.Distribute(SelectionManager.topLevelSelection,
                        AxesUtils.Axis.X, TransformTools.Bound.MAX)),
                CreateImageButton("DistributeGapX", "Make equal gaps between objects on the X axis",
                    () => TransformTools.DistributeGaps(SelectionManager.topLevelSelection, AxesUtils.Axis.X))
            ));

            // Y axis
            content.Add(CreateButtonRow(
                CreateImageButton("DistributeBottom", "Distribute bottom edges equidistantly",
                    () => TransformTools.Distribute(SelectionManager.topLevelSelection,
                        AxesUtils.Axis.Y, TransformTools.Bound.MIN)),
                CreateImageButton("DistributeCenterY", "Distribute centers equidistantly on the Y axis",
                    () => TransformTools.Distribute(SelectionManager.topLevelSelection,
                        AxesUtils.Axis.Y, TransformTools.Bound.CENTER)),
                CreateImageButton("DistributeTop", "Distribute top edges equidistantly",
                    () => TransformTools.Distribute(SelectionManager.topLevelSelection,
                        AxesUtils.Axis.Y, TransformTools.Bound.MAX)),
                CreateImageButton("DistributeGapY", "Make equal gaps between objects on the Y axis",
                    () => TransformTools.DistributeGaps(SelectionManager.topLevelSelection, AxesUtils.Axis.Y))
            ));

            // Z axis
            content.Add(CreateButtonRow(
                CreateImageButton("DistributeBack", "Distribute back edges equidistantly",
                    () => TransformTools.Distribute(SelectionManager.topLevelSelection,
                        AxesUtils.Axis.Z, TransformTools.Bound.MIN)),
                CreateImageButton("DistributeCenterZ", "Distribute centers equidistantly on the Z axis",
                    () => TransformTools.Distribute(SelectionManager.topLevelSelection,
                        AxesUtils.Axis.Z, TransformTools.Bound.CENTER)),
                CreateImageButton("DistributeFront", "Distribute front edges equidistantly",
                    () => TransformTools.Distribute(SelectionManager.topLevelSelection,
                        AxesUtils.Axis.Z, TransformTools.Bound.MAX)),
                CreateImageButton("DistributeGapZ", "Make equal gaps between objects on the Z axis",
                    () => TransformTools.DistributeGaps(SelectionManager.topLevelSelection, AxesUtils.Axis.Z))
            ));

            return foldout;
        }
        #endregion

        #region ARRANGE
        private VisualElement CreateArrangeSection()
        {
            var foldout = CreateTrackedFoldout("Arrange", _arrangeOpen, v => _arrangeOpen = v);
            var content = foldout.contentContainer;

            content.Add(CreateButtonRow(
                CreateImageButton("GridArrange", "Grid Arrangement",
                    () => GridArrangementToolWindow.ShowWindow()),
                CreateImageButton("RadialArrange", "Radial Arrangement",
                    () => RadialArrangeToolWindow.ShowWindow()),
                CreateImageButton("RearrangeSelectionOrder", "Exchange positions - Selection Order",
                    () => TransformTools.Rearrange(SelectionManager.topLevelSelection,
                        TransformTools.ArrangeBy.SELECTION_ORDER)),
                CreateImageButton("RearrangeHierarchyOrder", "Exchange positions - Hierarchy Order",
                    () => TransformTools.Rearrange(SelectionManager.topLevelSelection,
                        TransformTools.ArrangeBy.HIERARCHY_ORDER))
            ));

            return foldout;
        }
        #endregion

        #region PROGRESSION
        private VisualElement CreateProgressionSection()
        {
            var foldout = CreateTrackedFoldout("Progression", _progressionOpen, v => _progressionOpen = v);
            var content = foldout.contentContainer;

            content.Add(CreateButtonRow(
                CreateImageButton("IncrementalPosition", "Place objects incrementally",
                    () => PositionProgressionWindow.ShowWindow()),
                CreateImageButton("IncrementalRotation", "Rotate objects incrementally",
                    () => RotationProgressionWindow.ShowWindow()),
                CreateImageButton("IncrementalScale", "Scale objects incrementally",
                    () => ScaleProgressionWindow.ShowWindow())
            ));

            return foldout;
        }
        #endregion

        #region RANDOMIZE
        private VisualElement CreateRandomizeSection()
        {
            var foldout = CreateTrackedFoldout("Randomize", _randomizeOpen, v => _randomizeOpen = v);
            var content = foldout.contentContainer;

            content.Add(CreateButtonRow(
                CreateImageButton("RandomizePosition", "Randomize Positions",
                    () => RandomizePositionsWindow.ShowWindow()),
                CreateImageButton("RandomizeRotation", "Randomize Rotations",
                    () => RandomizeRotationsWindow.ShowWindow()),
                CreateImageButton("RandomizeScale", "Randomize Scales",
                    () => RandomizeScalesWindow.ShowWindow())
            ));

            return foldout;
        }
        #endregion

        #region HOMOGENIZE
        private VisualElement CreateHomogenizeSection()
        {
            var foldout = CreateTrackedFoldout("Homogenize", _homogenizeOpen, v => _homogenizeOpen = v);
            var content = foldout.contentContainer;

            content.Add(CreateButtonRow(
                CreateImageButton("HomogenizeSpacing", "Homogenize Spacing",
                    () => HomogenizeSpacingWindow.ShowWindow()),
                CreateImageButton("HomogenizeRotation", "Homogenize Rotation",
                    () => HomogenizeRotationWindow.ShowWindow()),
                CreateImageButton("HomogenizeScale", "Homogenize Scale",
                    () => HomogenizeScaleWindow.ShowWindow())
            ));

            return foldout;
        }
        #endregion

        #region EDIT PIVOT
        private VisualElement CreateEditPivotSection(VisualElement root)
        {
            var foldout = CreateTrackedFoldout("Edit Pivot", _editPivotOpen, v => _editPivotOpen = v);
            var content = foldout.contentContainer;

            var applyButton = CreateImageButton("Apply", "Apply", null);
            var cancelButton = CreateImageButton("Cancel", "Cancel", null);

            applyButton.style.display = DisplayStyle.None;
            cancelButton.style.display = DisplayStyle.None;

            applyButton.clickable = new Clickable(() =>
            {
                if (_pivot == null) return;
                var meshFilter = _pivot.transform.parent.GetComponent<MeshFilter>();
                var skinnedRenderer = _pivot.transform.parent.GetComponent<SkinnedMeshRenderer>();
                var target = _pivot.transform.parent;
                var otherObjects = new System.Collections.Generic.List<Transform>();
                string originalMeshPath = null;
                var warningAccepted = false;

                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    originalMeshPath = UnityEditor.AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
                    if (MeshChangeWarning(target, _pivot.transform, "MeshFilter"))
                    {
                        string savePath = UnityEditor.EditorUtility.SaveFilePanelInProject("Save As",
                            meshFilter.sharedMesh.name, "asset", string.Empty);
                        if (!string.IsNullOrEmpty(savePath))
                            TransformTools.SaveMeshFilterMesh(meshFilter,
                                savePath, _pivot.transform, otherObjects);
                        warningAccepted = true;
                    }
                }
                if (skinnedRenderer != null && skinnedRenderer.sharedMesh != null)
                {
                    if (MeshChangeWarning(target, _pivot.transform, "SkinnedMeshRenderer"))
                    {
                        string savePath = UnityEditor.EditorUtility.SaveFilePanelInProject("Save As",
                            skinnedRenderer.sharedMesh.name, "asset", string.Empty);
                        if (!string.IsNullOrEmpty(savePath))
                            TransformTools.SaveSkinnedMeshRendererMesh(skinnedRenderer,
                                savePath, _pivot.transform, otherObjects);
                        warningAccepted = true;
                    }
                }
                if (meshFilter == null && skinnedRenderer == null)
                    TransformTools.MoveChildren(_pivot.transform);

                if (warningAccepted)
                {
                    TransformTools.ApplyPivot(_pivot.transform, originalMeshPath);
                    TransformTools.UpdateOtherObjects(otherObjects, _pivot.transform, originalMeshPath);
                }
                CancelEditPivot(true);
            });

            cancelButton.clickable = new Clickable(() => CancelEditPivot(true));

            content.Add(CreateButtonRow(
                CreateImageButton("CenterPivot", "Center Pivot", () =>
                {
                    var obj = UnityEditor.Selection.activeGameObject;
                    if (obj == null) return;

                    var mf = obj.GetComponent<MeshFilter>();
                    var hasMeshFilterMesh = mf != null && mf.sharedMesh != null;
                    var otherObjects = new System.Collections.Generic.List<Transform>();
                    var pivot = TransformTools.CreateCenteredPivot(obj.transform);
                    string originalMeshPath = null;
                    var warningAccepted = false;
                    if (hasMeshFilterMesh)
                    {
                        originalMeshPath = UnityEditor.AssetDatabase.GetAssetPath(mf.sharedMesh);
                        if (MeshChangeWarning(obj.transform, pivot.transform, "MeshFilter"))
                        {
                            string savePath = UnityEditor.EditorUtility.SaveFilePanelInProject("Save As",
                                mf.sharedMesh.name, "asset", string.Empty);
                            if (!string.IsNullOrEmpty(savePath))
                                TransformTools.CenterPivot(mf, savePath, pivot, otherObjects);
                            warningAccepted = true;
                        }
                    }
                    var sr = obj.GetComponent<SkinnedMeshRenderer>();
                    var hasSkinnedRendererMesh = sr != null && sr.sharedMesh != null;
                    if (hasSkinnedRendererMesh)
                    {
                        if (MeshChangeWarning(obj.transform, pivot.transform, "SkinnedMeshRenderer"))
                        {
                            string savePath = UnityEditor.EditorUtility.SaveFilePanelInProject("Save As",
                                sr.sharedMesh.name, "asset", string.Empty);
                            if (!string.IsNullOrEmpty(savePath))
                                TransformTools.CenterPivot(sr, savePath, pivot, otherObjects);
                            warningAccepted = true;
                        }
                    }
                    if (!hasMeshFilterMesh && !hasSkinnedRendererMesh)
                        TransformTools.CenterPivot(obj.transform);
                    if (warningAccepted)
                    {
                        TransformTools.ApplyPivot(pivot.transform, originalMeshPath);
                        TransformTools.UpdateOtherObjects(otherObjects, pivot.transform, originalMeshPath);
                    }
                    Object.DestroyImmediate(pivot);
                }),
                CreateImageButton("EditPivot", "Edit pivot position and rotation", () =>
                {
                    _pivot = TransformTools.StartEditingPivot(UnityEditor.Selection.activeGameObject);
                }),
                applyButton,
                cancelButton
            ));

            foldout.schedule.Execute(() =>
            {
                var show = _pivot != null;
                applyButton.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                cancelButton.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }).Every(100);

            return foldout;
        }

        private static bool MeshChangeWarning(Transform target, Transform pivot, string compType)
        {
            if (target == null) return false;
            var colliderChildWarning = TransformTools.IsColliderChildNeeded(target, pivot)
                ? " \n\nTo prevent colliders and navmesh obstacles from being oriented incorrectly, "
                + "an empty child GameObject will be added to preserve the orientations. "
                + "The original colliders / obstacles will be deactivated." : string.Empty;
            return UnityEditor.EditorUtility.DisplayDialog(
                "Warning: The mesh will be modified",
                "Changing the pivot will modify the mesh referenced by the " + compType + " component.\n"
                + "Would you like to continue and save the mesh as new Asset?" + colliderChildWarning,
                "Continue", "Cancel");
        }

        private void CancelEditPivot(bool selectTarget)
        {
            if (_pivot == null) return;
            if (selectTarget) UnityEditor.Selection.activeObject = _pivot.transform.parent.gameObject;
            Object.DestroyImmediate(_pivot);
            _pivot = null;
        }
        #endregion

        #region MISCELLANEOUS
        private VisualElement CreateMiscellaneousSection()
        {
            var foldout = CreateTrackedFoldout("Miscellaneous", _miscellaneousOpen, v => _miscellaneousOpen = v);
            var content = foldout.contentContainer;

            content.Add(CreateButtonRow(
                CreateImageButton("PlaceOnSurface", "Place on the surface",
                    () => PlaceOnSurfaceWindow.ShowWindow()),
                CreateImageButton("SimulateGravity", "Simulate Gravity",
                    () => SimulateGravityWindow.ShowWindow()),
                CreateImageButton("Unoverlap", "Move objects so that their bounding boxes don't overlap",
                    () => UnoverlapToolWindow.ShowWindow())
            ));

            return foldout;
        }
        #endregion
    }
}
#endif
#pragma warning restore UDR0001
