#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Grabbit
{
    [ExecuteInEditMode]
    public class GrabbitData : MonoBehaviour
    {
        [HideInInspector] public bool IsIgnorableLOD = false;
        [HideInInspector] public int ExistingLayer;

        [HideInInspector] public List<Joint> preExistingJoints = new List<Joint>();
        [HideInInspector] public List<Rigidbody> jointConnectedBodies = new List<Rigidbody>();

        [HideInInspector] public List<Collider> NonGrabbitColliders = new List<Collider>();
        [HideInInspector] public List<Collider> NonGrabbitCollidersStaticOnly = new List<Collider>();
        [HideInInspector] public List<MeshCollider> AddedStaticColliders = new List<MeshCollider>();
        [HideInInspector] public List<MeshCollider> AddedDynamicColliders = new List<MeshCollider>();

        [HideInInspector] public Bounds bounds;

        public bool HasExistingColliders => NonGrabbitColliders.Count > 0;
        public bool HasAddedStaticColliders => AddedStaticColliders.Count > 0;
        public bool HasAddedDynamicColliders => AddedDynamicColliders.Count > 0;
        [HideInInspector] public Rigidbody Body;
        [HideInInspector] public bool WasBodyAdded = false;
        [HideInInspector] public bool WasBodyRemoved = false;
        [HideInInspector] public List<GrabbitData> CreatedSubDatas = new List<GrabbitData>();
        [HideInInspector] public List<GrabbitData> SubDatas = new List<GrabbitData>();
        [HideInInspector] [SerializeField] private List<FixedJoint> AddedJoints = new List<FixedJoint>();
        [HideInInspector] public bool isStaticConfigured;
        [HideInInspector] public bool isDynamicConfigured;
        private bool isBoundInitialized;
        
        public void Cleanup()
        {
            gameObject.layer = ExistingLayer;

            RestoreRemovedBody();

            foreach (var col in NonGrabbitColliders)
            {
                if (col)
                    col.enabled = true;
            }

            foreach (Collider staticCol in NonGrabbitCollidersStaticOnly)
            {
                if (staticCol)
                    staticCol.enabled = true;
            }

            for (var i = AddedStaticColliders.Count - 1; i >= 0; i--)
            {
                var addedStaticCollider = AddedStaticColliders[i];
                if (addedStaticCollider)
                    DestroyImmediate(addedStaticCollider);
            }

            for (var i = AddedDynamicColliders.Count - 1; i >= 0; i--)
            {
                var addedDynamicCollider = AddedDynamicColliders[i];
                if (addedDynamicCollider)
                    DestroyImmediate(addedDynamicCollider);
            }

            for (var i = AddedJoints.Count - 1; i >= 0; i--)
            {
                var addedJoint = AddedJoints[i];
                if (addedJoint)
                    DestroyImmediate(addedJoint);
            }

            NonGrabbitColliders.Clear();
            AddedDynamicColliders.Clear();
            AddedStaticColliders.Clear();

            if (WasBodyAdded && Body)
                DestroyImmediate(Body);

            DestroyAllAddedJoints();

            //a joint the user deleted while Grabbit was running leaves a destroyed entry behind here, and the two
            //lists are only kept in step by DisableExistingJoints - so guard both. This threw a
            //MissingReferenceException straight out of Cleanup, which aborted the rest of the teardown and left
            //the generated colliders, joints and bodies below sitting on the object
            for (var i = 0; i < preExistingJoints.Count && i < jointConnectedBodies.Count; i++)
            {
                var joint = preExistingJoints[i];
                if (!joint)
                    continue;

                joint.connectedBody = jointConnectedBodies[i];
            }


            foreach (var data in CreatedSubDatas)
            {
                //same story: deleting a child mid-session used to abort the teardown here
                if (!data)
                    continue;

                data.Cleanup();
                DestroyImmediate(data);
            }
        }

        public void RemoveCollidersFromSet(HashSet<Collider> colliders, bool recursive = true)
        {
            foreach (var col in AddedStaticColliders)
            {
                colliders.Remove(col);
            }

            foreach (var col in AddedDynamicColliders)
            {
                colliders.Remove(col);
            }

            if (recursive)
            {
                foreach (var data in SubDatas)
                {
                    //deleting a child object while Grabbit runs leaves its entry behind destroyed, and every one
                    //of these walks happens per frame - so an unguarded one does not throw once, it throws for as
                    //long as the tool is open
                    if (!data)
                        continue;

                    data.RemoveCollidersFromSet(colliders, false);
                }
            }
        }

        public void Awake()
        {
            PrepareStatic();
        }

        public int NonGrabbitColliderCount
        {
            get
            {
                int count = NonGrabbitColliders.Count;
                foreach (var data in SubDatas)
                {
                    if (!data)
                        continue;

                    count += data.NonGrabbitColliders.Count;
                }

                return count;
            }
        }


        public void PrepareStatic()
        {
            if (isStaticConfigured)
                return;

            ExistingLayer = gameObject.layer;

            if (GrabbitEditor.CurrentSettings.ChangeLayerOfStaticObjects)
                gameObject.layer = GrabbitEditor.CurrentSettings.StaticObjectsLayer;

            DisableExistingJoints();
            CheckIfIgnorableLOD();
            RegisterRigidBody();
            SetBodiesAsStatic();
            RegisterExistingColliders();
            RegisterExistingMeshes();
            EncapsulateAllSubDatas();

            isStaticConfigured = true;
        }

        private void DisableExistingJoints()
        {
            var joints = GetComponents<Joint>();
            preExistingJoints.AddRange(joints);
            foreach (var joint in joints)
            {
                jointConnectedBodies.Add(joint.connectedBody);
            
                /*joint.connectedBody = null;
                joint.breakForce = 0;*/
            }
        }

        private void CheckIfIgnorableLOD()
        {
            var lodGroup = GetComponentInParent<LODGroup>();

            if (lodGroup)
            {
                var lods = lodGroup.GetLODs();
                int i = 0;

                foreach (var lod in lods)
                {
                    if (lod.renderers.Any(_ => _ && _.gameObject == gameObject))
                    {
                        if (i == 0)
                        {
                            IsIgnorableLOD = false;
                            return;
                        }

                        IsIgnorableLOD = true;
                        return;
                    }

                    i++;
                }

                IsIgnorableLOD = false;
            }
        }


        public void RegisterRigidBody()
        {
            var bodies = GetComponentsInChildren<Rigidbody>();

            Body = GetComponent<Rigidbody>();
            if (!Body)
            {
                Body = gameObject.AddComponent<Rigidbody>();

                WasBodyAdded = true;
            }

            foreach (Rigidbody body in bodies)
            {
                if (body.gameObject == gameObject)
                {
                    continue;
                }
                else
                {
                    var data = body.GetComponent<GrabbitData>();
                    if (!data)
                    {
                        data = body.gameObject.AddComponent<GrabbitData>();
                        CreatedSubDatas.Add(data);
                    }

                    if (!SubDatas.Contains(data))
                        SubDatas.Add(data);
                }
            }
        }

        public void RegisterExistingColliders()
        {
            var colliders = GetComponentsInChildren<Collider>();

            if (colliders.Length > 0)
            {
                bounds = colliders[0].bounds;
                isBoundInitialized = true;
            }

            foreach (var col in colliders)
            {
                if (!col.enabled || col.isTrigger)
                    continue;

                if (col.gameObject == gameObject)
                {
                    var meshCol = col as MeshCollider;

                    if (IsIgnorableLOD || (meshCol && meshCol.convex == false))
                    {
                        NonGrabbitCollidersStaticOnly.Add(col);
                        col.enabled = false;
                    }
                    else
                    {
                        NonGrabbitColliders.Add(col);
                    }
                }
                else
                {
                    var data = col.GetComponent<GrabbitData>();
                    if (!data)
                    {
                        data = col.gameObject.AddComponent<GrabbitData>();
                        CreatedSubDatas.Add(data);
                    }


                    if (!SubDatas.Contains(data))
                        SubDatas.Add(data);
                }
            }
        }

        public void RegisterExistingMeshes()
        {
            var meshes = GetComponentsInChildren<MeshFilter>();

            foreach (var mesh in meshes)
            {
                if (!mesh.sharedMesh || mesh.sharedMesh.triangles.Length <= 1)
                    continue;

                var go = mesh.gameObject;


                if (go == gameObject)
                {
                    if (IsIgnorableLOD)
                        continue;

                    var col = mesh.gameObject.AddComponent<MeshCollider>();
                    col.sharedMesh = mesh.sharedMesh;

                    AddedStaticColliders.Add(col);

                    if (!isBoundInitialized)
                    {
                        bounds = col.bounds;
                        isBoundInitialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(col.bounds);
                    }
                }
                else
                {
                    GrabbitData data = go.GetComponent<GrabbitData>();

                    if (!data)
                    {
                        data = mesh.gameObject.AddComponent<GrabbitData>();
                        CreatedSubDatas.Add(data);
                    }

                    if (!SubDatas.Contains(data))
                        SubDatas.Add(data);
                }
            }
        }

        public void EncapsulateAllSubDatas()
        {
            foreach (var subData in SubDatas)
            {
                if (!subData)
                    continue;

                bounds.Encapsulate(subData.bounds);
            }
        }

        public void PrepareDynamic()
        {
            if (isDynamicConfigured)
                return;

            var settings = GrabbitEditor.CurrentSettings;

            //resolved through the accessor rather than read off the window: the window's field is only filled when
            //Grabbit's own settings fetch runs, which it often does not, and a null container here means every
            //object silently degrades to one convex hull instead of its baked concave shape
            var colliderMeshContainer =
                GrabbitEditor.GetOrFetchColliderMeshContainer(!settings.UseDynamicNonConvexColliders);

            if (settings.UseDynamicNonConvexColliders && colliderMeshContainer)
            {
                //averaging the estimated added convex mesh colliders to 3
                AddedDynamicColliders = new List<MeshCollider>(AddedStaticColliders.Count * 3);

                foreach (var col in AddedStaticColliders)
                {
                    if (!colliderMeshContainer.IsMeshDefined(col.sharedMesh))
                        //then it needs to be generated first
                        colliderMeshContainer.RegisterCollidersFromSelection(col, settings);

                    var meshes = colliderMeshContainer.GetMeshListAndRegenerateIfNeeded(col.sharedMesh, settings);

                    if (meshes.Count > 0)
                    {
                        foreach (var mesh in meshes)
                        {
                            AddMeshColliderToDynamicColliders(settings, col, mesh);
                        }
                    }
                    else
                    {
                        AddMeshColliderToDynamicColliders(settings, col, col.sharedMesh);
                    }
                }
            }
            else
            {
                WarnAboutRoughHullsOnce(settings);

                foreach (var meshCollider in AddedStaticColliders)
                {
                    //same path as a failed decomposition: one convex hull off the source mesh. Going through the
                    //shared helper keeps the cooking options and the duplicate check that adding the component
                    //here by hand skipped - and makes sure the mesh is actually assigned rather than relying on
                    //AddComponent picking up the MeshFilter
                    AddMeshColliderToDynamicColliders(settings, meshCollider, meshCollider.sharedMesh);
                }
            }

            foreach (var subData in SubDatas)
            {
                if (!subData)
                    continue;

                subData.PrepareDynamic();
            }

            isDynamicConfigured = true;
        }

        private static bool warnedAboutRoughHulls;

        /// <summary>Explains the single rough convex hull once per session. Without it the only feedback is PhysX'
        /// own "the partial hull will be used" message, which names a mesh but never says that Grabbit chose the
        /// hull because concave generation is switched off - it reads like a bug rather than a setting.</summary>
        private static void WarnAboutRoughHullsOnce(GrabbitSettings settings)
        {
            //a missing container lands here too, but that case already reports itself when it is resolved
            if (warnedAboutRoughHulls || settings.UseDynamicNonConvexColliders)
                return;

            warnedAboutRoughHulls = true;
            Debug.LogWarning(
                "Grabbit: Generate Dynamic Concave Colliders is off, so grabbed objects are approximated with a " +
                "single convex hull instead of their real shape - concave geometry behaves like a solid blob, and " +
                "dense meshes go past the 256 polygon limit PhysX allows for one hull (its \"partial hull will be " +
                "used\" message). Turn the option on in the Grabbit settings to get the real shapes.");
        }

        private void AddMeshColliderToDynamicColliders(GrabbitSettings settings, MeshCollider col, Mesh mesh)
        {
            var existingColliders = col.gameObject.GetComponents<MeshCollider>();

            //check for existing colliders to not have to duplicate the ones of other handlers, if subobjects are
            //involved. Two exclusions matter once the source mesh itself is passed in (failed decomposition, or
            //concave mode off): the static obstacle collider carries that same mesh but stays concave, which is
            //illegal on the non-kinematic body a grab creates, and a collider the user authored must never be
            //adopted into the generated set - the cleanup pass destroys everything it holds
            var existing = existingColliders.FirstOrDefault(_ =>
                _ != col && _.convex && _.sharedMesh == mesh
                && !NonGrabbitColliders.Contains(_) && !NonGrabbitCollidersStaticOnly.Contains(_));

            var mc = existing ? existing : col.gameObject.AddComponent<MeshCollider>();

            if (!existing)
            {
                if (!settings.useLowQualityConvexCollidersOnSelection)
                    mc.cookingOptions &= ~MeshColliderCookingOptions.UseFastMidphase;
                mc.sharedMesh = mesh;
                mc.convex = true;
            }

            AddedDynamicColliders.Add(mc);
        }

        public void SetToSelectionLayer(bool recursive = true)
        {
            gameObject.layer = GrabbitEditor.CurrentSettings.ChangeLayerOfDynamicObjects
                ? GrabbitEditor.CurrentSettings.DynamicObjectsLayer
                : ExistingLayer;

            if (!recursive)
                return;

            foreach (var data in SubDatas)
            {
                if (!data)
                    continue;

                data.SetToSelectionLayer(false);
            }
        }

        public void SetToBackgroundLayer(bool recursive = true)
        {
            gameObject.layer = GrabbitEditor.CurrentSettings.ChangeLayerOfStaticObjects
                ? GrabbitEditor.CurrentSettings.StaticObjectsLayer
                : ExistingLayer;

            if (!recursive)
                return;

            foreach (var data in SubDatas)
            {
                if (!data)
                    continue;

                data.SetToBackgroundLayer(false);
            }
        }

        public void SetBodiesAsStatic()
        {
            RestoreRemovedBody();

            if (Body)
            {
                Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                Body.isKinematic = true;
                Body.useGravity = false;
                Body.Sleep();
            }

            foreach (var data in SubDatas)
            {
                if (!data)
                    continue;

                data.SetBodiesAsStatic();
            }
        }

        public void RestoreRemovedBody()
        {
            if (WasBodyRemoved)
            {
                Body = gameObject.AddComponent<Rigidbody>();
                WasBodyRemoved = false;
            }
        }

        public void SetBodiesAsDynamic(bool alsoSubs = true)
        {
            RestoreRemovedBody();

            if (Body)
            {
                Body.useGravity = false;
                Body.detectCollisions = true;
                Body.isKinematic = false;
                Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                Body.AngularDamping(99999);
                Body.WakeUp();
            }

            if (alsoSubs)
            {
                foreach (var data in SubDatas)
                {
                    if (!data)
                        continue;

                    if (data.WasBodyAdded)
                    {
                        if (data.Body)
                            DestroyImmediate(data.Body);

                        data.WasBodyRemoved = true;
                    }
                    else
                    {
                        //the body can be gone at this point: an earlier pass took it away (WasBodyRemoved) and
                        //only SetBodiesAsDynamic puts it back, which runs after the joint is wired up here. Put
                        //it back first, and skip a child that has no body at all rather than throwing on it
                        data.RestoreRemovedBody();

                        if (!data.Body)
                            continue;

                        FixedJoint join = gameObject.AddComponent<FixedJoint>();
                        join.connectedBody = data.Body;
                        join.enableCollision = false;
                        AddedJoints.Add(join);
                        data.Body.LinearDamping(9999);
                        data.Body.AngularDamping(9999);
                      //  join.connectedMassScale = 100f;
                        data.SetBodiesAsDynamic(false);
                    }
                }
            }
        }

        public void DisableColliders(bool alsoSubs = false)
        {
            foreach (var col in NonGrabbitColliders)
            {
                if (col)
                    col.enabled = false;
            }

            foreach (var col in AddedDynamicColliders)
            {
                if (col)
                    col.enabled = false;
            }

            foreach (var col in AddedStaticColliders)
            {
                if (col)
                    col.enabled = false;
            }

            if (alsoSubs)
            {
                foreach (var data in SubDatas)
                {
                    if (!data)
                        continue;

                    data.DisableColliders();
                }
            }
        }

        public void ActivateDynamicColliders(bool alsoSubs = true)
        {
            foreach (var col in AddedDynamicColliders)
            {
                if (col)
                    col.enabled = true;
            }

            if (alsoSubs)
                foreach (var data in SubDatas)
                {
                    if (!data)
                        continue;

                    data.ActivateDynamicColliders(false);
                }
        }

        public void ActivateStaticColliders(bool alsoSubs = true)
        {
            foreach (var col in AddedStaticColliders)
            {
                if (col)
                    col.enabled = true;
            }

            if (alsoSubs)
                foreach (var data in SubDatas)
                {
                    if (!data)
                        continue;

                    data.ActivateStaticColliders(false);
                }
        }

        public void ActivateNonGrabbitColliders(bool alsoSubs = true)
        {
            foreach (var col in NonGrabbitColliders)
            {
                if (col)
                    col.enabled = true;
            }

            if (alsoSubs)
                foreach (var data in SubDatas)
                {
                    if (!data)
                        continue;

                    data.ActivateNonGrabbitColliders(false);
                }
        }

        public void DeActivateDynamicColliders(bool alsoSubs = true)
        {
            foreach (var col in AddedDynamicColliders)
            {
                if (col)
                    col.enabled = false;
            }

            if (alsoSubs)
                foreach (var data in SubDatas)
                {
                    if (!data)
                        continue;

                    data.DeActivateDynamicColliders(false);
                }
        }

        public void DeActivateStaticColliders(bool alsoSubs = true)
        {
            foreach (var col in AddedStaticColliders)
            {
                if (col)
                    col.enabled = false;
            }

            if (alsoSubs)
                foreach (var data in SubDatas)
                {
                    if (!data)
                        continue;

                    data.DeActivateStaticColliders(false);
                }
        }

        public void DeActivateNonGrabbitColliders(bool alsoSubs = true)
        {
            foreach (var col in NonGrabbitColliders)
            {
                if (col)
                    col.enabled = false;
            }

            if (alsoSubs)
                foreach (var data in SubDatas)
                {
                    if (!data)
                        continue;

                    data.DeActivateNonGrabbitColliders(false);
                }
        }

        public void DestroyAllAddedJoints()
        {
            for (var i = AddedJoints.Count - 1; i >= 0; i--)
            {
                var addedJoint = AddedJoints[i];
                //the joint goes away with the object it was added to, so by the time a deleted child gets here
                //the entry is already destroyed - matches the guard the Cleanup pass above has always had
                if (addedJoint)
                    DestroyImmediate(addedJoint);
            }

            AddedJoints.Clear();
        }
    }
}
#endif