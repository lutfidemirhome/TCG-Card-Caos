using System.Collections;
using UnityEngine;

/// <summary>Shared static poses at rest; samples the imported rig only while opening.</summary>
public sealed class MinorChestModelView : MonoBehaviour
{
    [SerializeField] GameObject animatedRoot;
    [SerializeField] MeshFilter staticMesh;
    [SerializeField] Mesh closedMesh, openMesh;
    [SerializeField] AnimationClip openingClip;
    float _sampleTime;

    public void Configure(GameObject rig, MeshFilter mesh, Mesh closed, Mesh open, AnimationClip clip)
    {
        animatedRoot = rig; staticMesh = mesh; closedMesh = closed; openMesh = open; openingClip = clip;
        SetOpen(false);
    }

    public void SetOpen(bool open)
    {
        _sampleTime = open ? openingClip.length : 0f;
        animatedRoot.SetActive(false);
        staticMesh.sharedMesh = open ? openMesh : closedMesh;
        staticMesh.gameObject.SetActive(true);
    }

    public IEnumerator Open() => Animate(true);
    public IEnumerator Close() => Animate(false);

    IEnumerator Animate(bool open)
    {
        staticMesh.gameObject.SetActive(false);
        animatedRoot.SetActive(true);
        float target = open ? openingClip.length : 0f;
        openingClip.SampleAnimation(animatedRoot, _sampleTime);
        while (!Mathf.Approximately(_sampleTime, target))
        {
            yield return null;
            _sampleTime = Mathf.MoveTowards(_sampleTime, target, Time.unscaledDeltaTime);
            openingClip.SampleAnimation(animatedRoot, _sampleTime);
        }
        SetOpen(open);
    }
}
