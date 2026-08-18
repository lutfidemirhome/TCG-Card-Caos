#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Grabbit
{
    public class ColliderMeshDeletionPostProcessor : UnityEditor.AssetModificationProcessor
    {
        public static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (!asset)
                return AssetDeleteResult.DidNotDelete;

            //never opens the Grabbit window and stays quiet when there is no container: a mesh being deleted in a
            //project that does not use generated colliders is not something to report
            var container = GrabbitEditor.GetOrFetchColliderMeshContainer(true);

            if (container)
                container.RemoveMesh(asset);
            
            return AssetDeleteResult.DidNotDelete;
        }
    }
}
#endif