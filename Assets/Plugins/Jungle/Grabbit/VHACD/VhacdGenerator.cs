using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using ThreadPriority = System.Threading.ThreadPriority;

namespace Grabbit.VHACD
{
    public class VhacdGenerator
    {
        public Parameters parameters;

        /// <summary>True when the last run could not load the native VHACD library at all. The caller decides
        /// what to tell the user: only editor-side code can see whether the plugin is excluded from the Editor
        /// in its import settings, missing from the project, or present but unloadable by Windows.</summary>
        public bool NativeLibraryMissing { get; private set; }

        /// <summary>The loader's own message for <see cref="NativeLibraryMissing"/>, kept for the log.</summary>
        public string NativeLibraryError { get; private set; }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryW(string lpFileName);

        /// <summary>Absolute path of the folder holding the native libraries. Set by the editor-side caller,
        /// which is the only side that can ask the AssetDatabase where the plugin ended up.</summary>
        public static string NativeSearchDirectory;

        private static bool nativeDependenciesLoaded;

        /// <summary>
        /// Loads the native library's own dependencies by absolute path, before anything P/Invokes into it.
        /// </summary>
        /// <remarks>
        /// Windows resolves a DLL's imports through the <em>process</em> search path — Unity.exe's folder,
        /// System32, PATH — and never through the folder the DLL itself was loaded from. So the copy of the
        /// OpenMP runtime sitting next to libvhacd.dll is never found on its own, and loading it here by
        /// absolute path puts the module in the process under the name the import table asks for, which is what
        /// "copy the vcomp140 dlls into System32" was really working around.
        /// <para>
        /// libvhacd.dll used to import VCOMP140D.DLL, the <em>debug</em> OpenMP runtime, which no Visual C++
        /// Redistributable installs (debug runtimes are not redistributable and ship only with Visual Studio) —
        /// so the first call threw DllNotFoundException on every machine without VS, reported to the user as a
        /// missing redistributable that installing could not fix. It now links the release VCOMP140.DLL, which
        /// the redistributable does install and which Unity itself requires, so Grabbit no longer ships a copy.
        /// </para>
        /// <para>
        /// This pre-load therefore has nothing to do on a normal install. It stays as the escape hatch for a
        /// machine whose redistributable is missing or broken: dropping an x64 vcomp140.dll next to
        /// libvhacd.dll makes it load, which plain Windows resolution would never manage on its own.
        /// </para>
        /// <para>
        /// Must be called from the main thread: it is the caller's job to have resolved
        /// <see cref="NativeSearchDirectory"/> through the AssetDatabase first.
        /// </para>
        /// </remarks>
        public static void EnsureNativeDependenciesLoaded()
        {
            if (nativeDependenciesLoaded)
                return;

            nativeDependenciesLoaded = true;

            if (string.IsNullOrEmpty(NativeSearchDirectory) || Path.DirectorySeparatorChar != '\\')
                return;

            //a failure to load is not worth reporting here: the P/Invoke itself reports far more precisely if
            //the dependency is really absent
            var path = Path.Combine(NativeSearchDirectory, "vcomp140.dll");
            if (File.Exists(path))
                LoadLibraryW(path);
        }

        public VhacdGenerator()
        {
            parameters.Init();
        }

        [DllImport("libvhacd")]
        private static extern unsafe void* CreateVHACD();

        [DllImport("libvhacd")]
        private static extern unsafe void DestroyVHACD(void* pVHACD);

        [DllImport("libvhacd")]
        private static extern unsafe bool ComputeFloat(
            void* pVHACD,
            float* points,
            uint countPoints,
            uint* triangles,
            uint countTriangles,
            Parameters* parameters);

        [DllImport("libvhacd")]
        private static extern unsafe bool ComputeDouble(
            void* pVHACD,
            double* points,
            uint countPoints,
            uint* triangles,
            uint countTriangles,
            Parameters* parameters);

        [DllImport("libvhacd")]
        private static extern unsafe uint GetNConvexHulls(void* pVHACD);

        [DllImport("libvhacd")]
        private static extern unsafe void GetConvexHull(
            void* pVHACD,
            uint index,
            ConvexHull* ch);

        public unsafe List<Mesh> GenerateConvexMeshes(Mesh mesh)
        {
            try
            {
                var vhacd = CreateVHACD();
                var parameters = this.parameters;

                var verts = mesh.vertices;
                var tris = mesh.triangles;
                fixed (Vector3* pVerts = verts)
                fixed (int* pTris = tris)
                {
                    ComputeFloat(
                        vhacd,
                        (float*) pVerts, (uint) verts.Length,
                        (uint*) pTris, (uint) tris.Length / 3,
                        &parameters);
                }

                var numHulls = GetNConvexHulls(vhacd);
                var generatedCollisions = new List<Mesh>();

                foreach (var index in Enumerable.Range(0, (int) numHulls))
                {
                    ConvexHull hull;
                    GetConvexHull(vhacd, (uint) index, &hull);

                    var hullMesh = new Mesh();
                    var hullVerts = new Vector3[hull.m_nPoints];
                    fixed (Vector3* pHullVerts = hullVerts)
                    {
                        var pComponents = hull.m_points;
                        var pVerts = pHullVerts;

                        for (var pointCount = hull.m_nPoints; pointCount != 0; --pointCount)
                        {
                            pVerts->x = (float) pComponents[0];
                            pVerts->y = (float) pComponents[1];
                            pVerts->z = (float) pComponents[2];

                            pVerts += 1;
                            pComponents += 3;
                        }
                    }

                    hullMesh.SetVertices(hullVerts);

                    var indices = new int[hull.m_nTriangles * 3];
                    Marshal.Copy((IntPtr) hull.m_triangles, indices, 0, indices.Length);
                    hullMesh.SetTriangles(indices, 0);

                    generatedCollisions.Add(hullMesh);
                }

                DestroyVHACD(vhacd);
                return generatedCollisions;
            }
            catch (DllNotFoundException e)
            {
                //same as the threaded path: record it and let the editor-side caller name the actual cause
                NativeLibraryMissing = true;
                NativeLibraryError = e.Message;
                return new List<Mesh>();
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "Grabbit Warning : An Exception was raised when attempting to generate colliders for the mesh");
                Debug.Log(e);
                return new List<Mesh>();
            }
        }

        public unsafe void GenerateConvexMeshesThreaded(Vector3[] verts, int[] tris)
        {
            try
            {
                var vhacd = CreateVHACD();
                var parameters = this.parameters;

                fixed (Vector3* pVerts = verts)
                fixed (int* pTris = tris)
                {
                    ComputeFloat(
                        vhacd,
                        (float*) pVerts, (uint) verts.Length,
                        (uint*) pTris, (uint) tris.Length / 3,
                        &parameters);
                }

                var numHulls = GetNConvexHulls(vhacd);

                tempOutTris.Clear();
                tempOutVerts.Clear();

                foreach (var index in Enumerable.Range(0, (int) numHulls))
                {
                    ConvexHull hull;
                    GetConvexHull(vhacd, (uint) index, &hull);

                    var hullVerts = new Vector3[hull.m_nPoints];
                    fixed (Vector3* pHullVerts = hullVerts)
                    {
                        var pComponents = hull.m_points;
                        var pVerts = pHullVerts;

                        for (var pointCount = hull.m_nPoints; pointCount != 0; --pointCount)
                        {
                            pVerts->x = (float) pComponents[0];
                            pVerts->y = (float) pComponents[1];
                            pVerts->z = (float) pComponents[2];

                            pVerts += 1;
                            pComponents += 3;
                        }
                    }

                    tempOutVerts.Add(hullVerts);

                    var indices = new int[hull.m_nTriangles * 3];
                    Marshal.Copy((IntPtr) hull.m_triangles, indices, 0, indices.Length);
                    tempOutTris.Add(indices);
                }

                DestroyVHACD(vhacd);
            }
            catch (DllNotFoundException e)
            {
                //this runs on a worker thread and in an assembly with no UnityEditor reference, so it cannot tell
                //an excluded plugin from a genuinely missing dependency. It records the failure instead and lets
                //the editor-side caller read the plugin's real import state - the advice used to be four lines of
                //"install the VC++ redistributable", which sends people to fix a machine that was never at fault
                NativeLibraryMissing = true;
                NativeLibraryError = e.Message;
                tempOutTris.Clear();
                tempOutVerts.Clear();
            }
            catch (ThreadAbortException)
            {
                tempOutTris.Clear();
                tempOutVerts.Clear();
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "Grabbit Warning : An Exception was raised when attempting to generate colliders for the mesh");
                Debug.Log(e);
                tempOutTris.Clear();
                tempOutVerts.Clear();
            }
        }


        private List<Mesh> tempMeshes = new List<Mesh>();

        private List<Vector3[]> tempOutVerts = new List<Vector3[]>();
        private List<int[]> tempOutTris = new List<int[]>();

        private Vector3[] tempVerts;
        private int[] tempTris;
        private Thread meshingThread;

        public void ThreadedGenerateConvexMeshes(Mesh mesh)
        {
            tempVerts = mesh.vertices;
            tempTris = mesh.triangles;
            tempMeshes.Clear();
            meshingThread = new Thread(FindAndUpdateMeshesInThread)
            {
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };
            meshingThread.Start();
        }

        public bool IsThreadedMeshGenerationDone()
        {
            return !meshingThread.IsAlive;
        }

        public void AbortThread()
        {
            meshingThread.Abort();
        }

        public List<Mesh> RetrieveThreadMesh()
        {
            return tempMeshes;
        }

        private void FindAndUpdateMeshesInThread()
        {
            tempMeshes.Clear();
            GenerateConvexMeshesThreaded(tempVerts, tempTris);
        }

        public void GenerateMeshesAfterThreadCompletion()
        {
            tempMeshes = new List<Mesh>(tempOutTris.Count);
            for (int i = 0; i < tempOutTris.Count; i++)
            {
                var hullMesh=new Mesh();
                hullMesh.SetVertices(tempOutVerts[i]);
                hullMesh.SetTriangles(tempOutTris[i], 0);
                tempMeshes.Add(hullMesh);
            }
        }
        
        [Serializable]
        public unsafe struct Parameters
        {
            public void Init()
            {
                m_resolution = 100000;
                m_concavity = 0.001;
                m_planeDownsampling = 4;
                m_convexhullDownsampling = 4;
                m_alpha = 0.05;
                m_beta = 0.05;
                m_pca = 0;
                m_mode = 0; // 0: voxel-based (recommended), 1: tetrahedron-based
                m_maxNumVerticesPerCH = 64;
                m_minVolumePerCH = 0.0001;
                m_callback = null;
                m_logger = null;
                m_convexhullApproximation = 1;
                m_oclAcceleration = 0;
                m_maxConvexHulls = 1024;
                m_projectHullVertices =
                    true; // This will project the output convex hull vertices onto the original source mesh to increase the floating point accuracy of the results
            }

            [Tooltip("maximum concavity")] [Range(0, 1)]
            public double m_concavity;

            [Tooltip("controls the bias toward clipping along symmetry planes")] [Range(0, 1)]
            public double m_alpha;

            [Tooltip("controls the bias toward clipping along revolution axes")] [Range(0, 1)]
            public double m_beta;

            [Tooltip("controls the adaptive sampling of the generated convex-hulls")] [Range(0, 0.01f)]
            public double m_minVolumePerCH;

            public void* m_callback;
            public void* m_logger;

            [Tooltip("maximum number of voxels generated during the voxelization stage")] [Range(100, 64000000)]
            public uint m_resolution;

            [Tooltip("controls the maximum number of triangles per convex-hull")] [Range(4, 1024)]
            public uint m_maxNumVerticesPerCH;

            [Tooltip("controls the granularity of the search for the \"best\" clipping plane")] [Range(1, 16)]
            public uint m_planeDownsampling;

            [Tooltip(
                "controls the precision of the convex-hull generation process during the clipping plane selection stage")]
            [Range(1, 16)]
            public uint m_convexhullDownsampling;

            [Tooltip("enable/disable normalizing the mesh before applying the convex decomposition")] [Range(0, 1)]
            public uint m_pca;

            [Tooltip("0: voxel-based (recommended), 1: tetrahedron-based")] [Range(0, 1)]
            public uint m_mode;

            [Range(0, 1)] public uint m_convexhullApproximation;

            [Range(0, 1)] public uint m_oclAcceleration;

            public uint m_maxConvexHulls;

            [Tooltip(
                "This will project the output convex hull vertices onto the original source mesh to increase the floating point accuracy of the results")]
            public bool m_projectHullVertices;
        }

        private unsafe struct ConvexHull
        {
            public double* m_points;
            public uint* m_triangles;
            public uint m_nPoints;
            public uint m_nTriangles;
            public double m_volume;
            public fixed double m_center[3];
        }
    }
}