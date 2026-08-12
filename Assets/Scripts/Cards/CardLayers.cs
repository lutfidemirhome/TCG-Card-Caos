using UnityEngine;

public static class CardLayers
{
    public const string WorldCardLayerName = "WorldCard";

    static int _worldCardLayer = int.MinValue;

    public static int WorldCard
    {
        get
        {
            EnsureInitialized();
            return _worldCardLayer;
        }
    }

    public static LayerMask WorldCardMask
    {
        get
        {
            EnsureInitialized();
            return _worldCardLayer >= 0 ? 1 << _worldCardLayer : ~0;
        }
    }

    public static void EnsureInitialized()
    {
        if (_worldCardLayer != int.MinValue)
            return;

        _worldCardLayer = LayerMask.NameToLayer(WorldCardLayerName);
        if (_worldCardLayer < 0)
            _worldCardLayer = 0;
    }

    public static void ApplyToGameObject(GameObject target)
    {
        if (target == null)
            return;

        EnsureInitialized();
        if (_worldCardLayer >= 0)
            target.layer = _worldCardLayer;
    }
}
