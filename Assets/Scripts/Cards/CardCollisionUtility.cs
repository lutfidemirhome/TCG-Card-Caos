using UnityEngine;

/// <summary>
/// Keeps flat world cards from resting inside static geometry (wall bases, shelf lips, etc.).
/// </summary>
public static class CardCollisionUtility
{
    const int MaxResolveIterations = 10;
    const float SeparationSkin = 0.003f;
    const int OverlapBufferSize = 32;

    static PhysicsMaterial _sharedPhysicMaterial;
    static readonly Collider[] OverlapBuffer = new Collider[OverlapBufferSize];

    public static PhysicsMaterial SharedPhysicMaterial
    {
        get
        {
            if (_sharedPhysicMaterial == null)
            {
                _sharedPhysicMaterial = new PhysicsMaterial("CardSurface")
                {
                    dynamicFriction = 0.65f,
                    staticFriction = 0.75f,
                    bounciness = 0.04f,
                    frictionCombine = PhysicsMaterialCombine.Maximum,
                    bounceCombine = PhysicsMaterialCombine.Minimum,
                };
            }

            return _sharedPhysicMaterial;
        }
    }

    public static void ApplyToCollider(BoxCollider collider)
    {
        if (collider == null)
            return;

        collider.material = SharedPhysicMaterial;
    }

    public static void ApplyFlatWorldSize(BoxCollider collider)
    {
        if (collider == null)
            return;

        collider.size = new Vector3(CardDimensions.Width, CardDimensions.Thickness, CardDimensions.Height);
        collider.center = Vector3.zero;
        ApplyToCollider(collider);
    }

    public static void ApplyUprightShelfSize(BoxCollider collider)
    {
        if (collider == null)
            return;

        collider.size = new Vector3(CardDimensions.Width, CardDimensions.Height, CardDimensions.Thickness);
        collider.center = Vector3.zero;
    }

    public static void ResolveStaticPenetration(
        Transform cardTransform,
        BoxCollider cardCollider,
        WorldCard self,
        Rigidbody body = null)
    {
        if (cardTransform == null || cardCollider == null || !cardCollider.enabled)
            return;

        for (int iteration = 0; iteration < MaxResolveIterations; iteration++)
        {
            if (!TryResolveSinglePass(cardTransform, cardCollider, self, body))
                break;
        }
    }

    static bool TryResolveSinglePass(
        Transform cardTransform,
        BoxCollider cardCollider,
        WorldCard self,
        Rigidbody body)
    {
        Vector3 center = cardTransform.TransformPoint(cardCollider.center);
        Vector3 halfExtents = Vector3.Scale(cardCollider.size * 0.5f, cardTransform.lossyScale);
        halfExtents.x = Mathf.Max(halfExtents.x - SeparationSkin, 0.001f);
        halfExtents.y = Mathf.Max(halfExtents.y - SeparationSkin, 0.001f);
        halfExtents.z = Mathf.Max(halfExtents.z - SeparationSkin, 0.001f);

        int overlapCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            OverlapBuffer,
            cardTransform.rotation,
            ~0,
            QueryTriggerInteraction.Ignore);

        bool moved = false;
        for (int i = 0; i < overlapCount; i++)
        {
            Collider other = OverlapBuffer[i];
            if (ShouldIgnoreCollider(other, cardCollider, self))
                continue;

            if (!Physics.ComputePenetration(
                    cardCollider,
                    cardTransform.position,
                    cardTransform.rotation,
                    other,
                    other.transform.position,
                    other.transform.rotation,
                    out Vector3 direction,
                    out float distance))
            {
                continue;
            }

            Vector3 separation = direction * (distance + SeparationSkin);
            if (body != null && body.isKinematic == false)
                body.MovePosition(body.position + separation);
            else
                cardTransform.position += separation;

            moved = true;
        }

        return moved;
    }

    static bool ShouldIgnoreCollider(Collider other, Collider selfCollider, WorldCard self)
    {
        if (other == null || other == selfCollider)
            return true;
        if (other.isTrigger)
            return true;

        WorldCard otherCard = other.GetComponentInParent<WorldCard>();
        if (otherCard != null && otherCard != self)
            return true;

        if (other.GetComponentInParent<WorldBoosterPack>() != null)
            return true;

        if (other.GetComponentInParent<FirstPersonController>() != null)
            return true;

        Rigidbody otherBody = other.attachedRigidbody;
        if (otherBody != null && !otherBody.isKinematic)
            return true;

        return false;
    }
}
