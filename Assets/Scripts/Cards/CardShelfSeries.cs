using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Groups cards that share one shelf row (e.g. normal_common_bloomini_01 … _10).
/// </summary>
public static class CardShelfSeries
{
    // Row validation compares the same definitions many times as cabinets fill.
    // Key by the source id, so changing a definition never reuses an old series.
    static readonly Dictionary<string, string> SeriesByDefinitionId =
        new Dictionary<string, string>(System.StringComparer.Ordinal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache()
    {
        SeriesByDefinitionId.Clear();
    }

    public static bool TryGetSeriesId(CardDefinition definition, out string seriesId)
    {
        seriesId = null;
        if (definition == null || string.IsNullOrWhiteSpace(definition.DefinitionId))
            return false;

        string id = definition.DefinitionId;
        if (Application.isPlaying && SeriesByDefinitionId.TryGetValue(id, out seriesId))
            return true;

        int lastUnderscore = id.LastIndexOf('_');
        if (lastUnderscore <= 0)
        {
            seriesId = id;
        }
        else
        {
            string suffix = id.Substring(lastUnderscore + 1);
            if (int.TryParse(suffix, out _))
                seriesId = id.Substring(0, lastUnderscore);
            else
                seriesId = id;
        }

        if (string.IsNullOrWhiteSpace(seriesId))
            return false;

        if (Application.isPlaying)
            SeriesByDefinitionId[id] = seriesId;

        return true;
    }
}
