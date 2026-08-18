#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Grabbit.VHACD;
using UnityEditor;
using UnityEngine;

namespace Grabbit
{
    [Serializable]
    public class MeshList
    {
        public int maxMeshCount;
        public List<Mesh> Meshes;
        public int resolution;
    }

    [CreateAssetMenu(fileName = "Grabbit Settings", menuName = "Tools/Grabbit/Create New Collider Container",
        order = 1)]
    public class ColliderMeshContainer : ScriptableObject
    {
        [SerializeField] [HideInInspector] private MeshMeshListDictionary colliderMeshes = new MeshMeshListDictionary();

        public List<Mesh> GetMeshListAndRegenerateIfNeeded(Mesh mesh, GrabbitSettings settings)
        {
            var list = colliderMeshes[mesh];
            if (list.resolution != settings.ColliderResolution || list.maxMeshCount != settings.MaxMeshCollidersCreated)
                RegenerateFromMesh(mesh, settings);

            return list.Meshes;
        }

        public bool IsMeshDefined(Mesh mesh)
        {
            if (colliderMeshes.ContainsKey(mesh))
            {
                if (colliderMeshes[mesh].Meshes.Contains(null))
                {
                    colliderMeshes[mesh].Meshes.RemoveAll(_ => !_);
                    EditorUtility.SetDirty(this);
                }
                
                return colliderMeshes[mesh].Meshes.Count > 0;
            }

            return false;
        }

        public void GenerateAllColliders(GrabbitSettings settings)
        {
            var ids = AssetDatabase.FindAssets("t:Mesh", new[] {"Assets"});
            Debug.LogFormat("Grabbit Analysis: {0} meshes found. Generating Colliders...", ids.Length);

            var vhacdGenerator = CreateAndConfigureGenerator(settings);
            var i = 0;

            foreach (var id in ids)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(id));

                if (colliderMeshes.ContainsKey(mesh))
                    continue;

                if (EditorUtility.DisplayCancelableProgressBar(
                    "Grabbit Is Generating Colliders To Be Used In The Scene",
                    $"Analyzing {mesh.name} ({i + 1} out of {ids.Length})",
                    (float) i / ids.Length))
                    break;

                try
                {
                    if (!mesh)
                        continue;
                    var meshes = vhacdGenerator.GenerateConvexMeshes(mesh);

                    foreach (var collidingMesh in meshes) AssetDatabase.AddObjectToAsset(collidingMesh, this);

                    colliderMeshes.Add(mesh,
                        new MeshList
                        {
                            maxMeshCount = settings.MaxMeshCollidersCreated, resolution = settings.ColliderResolution,
                            Meshes = meshes
                        });
                }
                catch (Exception)
                {
                    // ignored
                }


                i++;
            }

            if (vhacdGenerator.NativeLibraryMissing)
                WarnNativeLibraryUnavailableOnce(vhacdGenerator.NativeLibraryError);

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(this);
            Debug.LogFormat("Grabbit Colliders Generated!", ids.Length);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Asset path of the native decomposition library, or null when it is not in the project.</summary>
        private static string ResolveVhacdDllPath()
        {
            foreach (var guid in AssetDatabase.FindAssets("libvhacd"))
            {
                var candidate = AssetDatabase.GUIDToAssetPath(guid);
                if (candidate.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            return null;
        }

        /// <summary>Tells the generator where the native libraries live and lets it pre-load them, on the main
        /// thread, before any decomposition (threaded or not) reaches a P/Invoke.</summary>
        private static void PrimeNativeLibrary()
        {
            if (VhacdGenerator.NativeSearchDirectory != null)
                return;

            var dllPath = ResolveVhacdDllPath();
            if (dllPath == null)
                return;

            //asset paths are relative to the project folder, which is the parent of Application.dataPath
            var projectFolder = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectFolder))
                return;

            var absolute = Path.Combine(projectFolder, dllPath.Replace('/', Path.DirectorySeparatorChar));
            VhacdGenerator.NativeSearchDirectory = Path.GetDirectoryName(absolute);
            VhacdGenerator.EnsureNativeDependenciesLoaded();
        }

        private static VhacdGenerator CreateAndConfigureGenerator(GrabbitSettings settings)
        {
            PrimeNativeLibrary();

            var vhacdGenerator = new VhacdGenerator();
            vhacdGenerator.parameters.m_maxConvexHulls =
                (uint) settings.MaxMeshCollidersCreated;
            vhacdGenerator.parameters.m_resolution = (uint) settings.ColliderResolution;
            return vhacdGenerator;
        }

        public void RegisterCollidersFromMeshFiltersInScene(GrabbitSettings settings, params MeshFilter[] filters)
        {
            var vhacdGenerator = CreateAndConfigureGenerator(settings);
            var i = 0;
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;

                if (EditorUtility.DisplayCancelableProgressBar(
                    "Grabbit Is Generating Colliders In The Scene",
                    $"Analyzing {mesh.name} ({i + 1} out of {filters.Length})",
                    (float) i / filters.Length))
                    break;

                //TODO: check for hidden assets and so on
                if (colliderMeshes.ContainsKey(mesh))
                    continue;

                var meshes = vhacdGenerator.GenerateConvexMeshes(mesh);

                foreach (var collidingMesh in meshes) AssetDatabase.AddObjectToAsset(collidingMesh, this);

                colliderMeshes.Add(mesh,
                    new MeshList
                    {
                        maxMeshCount = settings.MaxMeshCollidersCreated, resolution = settings.ColliderResolution,
                        Meshes = meshes
                    });
                i++;
            }

            if (vhacdGenerator.NativeLibraryMissing)
                WarnNativeLibraryUnavailableOnce(vhacdGenerator.NativeLibraryError);

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        public bool ShouldRegenerate(MeshCollider collider, GrabbitSettings settings)
        {
            var mesh = collider.sharedMesh;
            if (!colliderMeshes.ContainsKey(mesh))
                return true;

            return colliderMeshes[mesh].resolution != settings.ColliderResolution;
        }

        public void RegenerateFromMesh(Mesh mesh, GrabbitSettings settings)
        {
            if (colliderMeshes.ContainsKey(mesh))
                RemoveMesh(mesh);
            GenerateFromMesh(mesh, settings);
        }

        public void RegenerateFromCollider(MeshCollider collider, GrabbitSettings settings)
        {
            var mesh = collider.sharedMesh;
            if (colliderMeshes.ContainsKey(mesh))
                RemoveMesh(mesh);

            RegisterCollidersFromSelection(collider, settings);
        }

        public void RegisterCollidersFromSelection(MeshCollider collider, GrabbitSettings settings)
        {
            var mesh = collider.sharedMesh;

            GenerateFromMesh(mesh, settings);
        }

        private static int MaxEstimatedTickForGeneration = 60 * 60;

        private void GenerateFromMesh(Mesh mesh, GrabbitSettings settings)
        {
            if (IsMeshDefined(mesh))
                return;

            var vhacdGenerator = CreateAndConfigureGenerator(settings);

            vhacdGenerator.ThreadedGenerateConvexMeshes(mesh);

            bool shouldGenerate = true;
            int i = 0;
            while (!vhacdGenerator.IsThreadedMeshGenerationDone())
            {
                if (EditorUtility.DisplayCancelableProgressBar(
                    $"Grabbit is generating colliders for {mesh.name}",
                    "Generating...",
                    (float) i / MaxEstimatedTickForGeneration))
                {
                    vhacdGenerator.AbortThread();
                    shouldGenerate = false;
                    break;
                }

                i++;
            }

            if (shouldGenerate)
                vhacdGenerator.GenerateMeshesAfterThreadCompletion();
            List<Mesh> meshes = shouldGenerate ? vhacdGenerator.RetrieveThreadMesh() : new List<Mesh>();
            //NonThreadedMeshGeneration(mesh, settings, vhacdGenerator);

            if (meshes.Count == 0 && mesh.vertexCount > 0)
            {
                if (shouldGenerate)
                {
                    Debug.LogWarning(
                        $"Grabbit Warning: The collider generation failed for {mesh.name}, only convex collider available.");

                    if (vhacdGenerator.NativeLibraryMissing)
                        WarnNativeLibraryUnavailableOnce(vhacdGenerator.NativeLibraryError);
                }
                else
                {
                    Debug.Log(
                        $"Grabbit Warning: The collider generation was cancelled for {mesh.name}, only convex collider available.");
                }

                EditorUtility.ClearProgressBar();
            }

            foreach (var collidingMesh in meshes) AssetDatabase.AddObjectToAsset(collidingMesh, this);

            if (colliderMeshes.ContainsKey(mesh))
            {
                colliderMeshes[mesh] = new MeshList
                {
                    maxMeshCount = settings.MaxMeshCollidersCreated, resolution = settings.ColliderResolution,
                    Meshes = meshes
                };
            }
            else
            {
                colliderMeshes.Add(mesh,
                    new MeshList
                    {
                        maxMeshCount = settings.MaxMeshCollidersCreated, resolution = settings.ColliderResolution,
                        Meshes = meshes
                    });
            }


            EditorUtility.DisplayProgressBar(
                $"Grabbit is generating colliders for {mesh.name}",
                "Generating...",
                1);
            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(this);
        }

        private static bool warnedNativeLibraryUnavailable;

        /// <summary>Reports why the native decomposition library could not be loaded, once per session, from the
        /// plugin's actual import state rather than from a guess.</summary>
        /// <remarks>
        /// The old advice — install the VC++ redistributable, then copy vcomp140 into System32 — could not fix
        /// the actual failure: libvhacd.dll linked the *debug* OpenMP runtime, which no redistributable installs
        /// (see <see cref="VhacdGenerator.EnsureNativeDependenciesLoaded"/>). It now links the release one, so
        /// the redistributable really is the answer in the one case where the runtime is what is missing — and
        /// every other cause is package-side and names itself.
        /// </remarks>
        private static void WarnNativeLibraryUnavailableOnce(string loaderError)
        {
            if (warnedNativeLibraryUnavailable)
                return;

            warnedNativeLibraryUnavailable = true;

            var path = ResolveVhacdDllPath();

            string diagnosis;
            if (path == null)
            {
                diagnosis = "libvhacd.dll is not in this project at all — the Grabbit package did not bring it " +
                            "in. Reimport Grabbit and make sure every folder is ticked in the import dialog.";
            }
            else if (AssetImporter.GetAtPath(path) is PluginImporter importer &&
                     !importer.GetCompatibleWithEditor() && !importer.GetCompatibleWithAnyPlatform())
            {
                diagnosis = $"{path} is excluded from the Editor in its import settings, so Unity never loads " +
                            "it. Select it in the Project window and tick Editor under Platform settings " +
                            "(Include Platforms), then Apply.";
            }
            else
            {
                var sibling = Path.Combine(Path.GetDirectoryName(path) ?? "", "vcomp140.dll");
                diagnosis = $"{path} is enabled for the Editor but Windows could not load it. It needs the " +
                            "OpenMP runtime vcomp140.dll, which comes with the Microsoft Visual C++ " +
                            "Redistributable 2015-2022 x64 — install that. If it is already installed, drop an " +
                            $"x64 vcomp140.dll at {sibling} and Grabbit will load it from there.";
            }

            Debug.LogWarning(
                $"Grabbit: concave collider generation is unavailable — {diagnosis}\nLoader error: {loaderError}");
        }

        private List<Mesh> NonThreadedMeshGeneration(Mesh mesh, GrabbitSettings settings, VhacdGenerator vhacdGenerator)
        {
            //TODO: check for hidden assets and so on
            return vhacdGenerator.GenerateConvexMeshes(mesh);
        }


        public void ClearColliders()
        {
            foreach (var pair in colliderMeshes)
            foreach (var mesh in pair.Value.Meshes)
            {
                if (!mesh)
                    continue;
                AssetDatabase.RemoveObjectFromAsset(mesh);
                DestroyImmediate(mesh);
            }

            colliderMeshes.Clear();

            string path = AssetDatabase.GetAssetPath(this);
            var objs = AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (var obj in objs)
            {
                if (obj == this)
                    continue;

                AssetDatabase.RemoveObjectFromAsset(obj);
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        public void RemoveMesh(Mesh mesh)
        {
            if (!colliderMeshes.ContainsKey(mesh))
                return;

            var list = colliderMeshes[mesh];
            foreach (var createdMesh in list.Meshes)
            {
                if (!createdMesh)
                    return;

                AssetDatabase.RemoveObjectFromAsset(createdMesh);
                DestroyImmediate(createdMesh);
            }

            colliderMeshes.Remove(mesh);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif