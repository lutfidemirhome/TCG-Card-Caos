/// <summary>
/// Groups cards that share one shelf row (e.g. normal_common_bloomini_01 … _10).
/// </summary>
public static class CardShelfSeries
{
    public static bool TryGetSeriesId(CardDefinition definition, out string seriesId)
    {
        seriesId = null;
        if (definition == null || string.IsNullOrWhiteSpace(definition.DefinitionId))
            return false;

        string id = definition.DefinitionId;
        int lastUnderscore = id.LastIndexOf('_');
        if (lastUnderscore <= 0)
        {
            seriesId = id;
            return true;
        }

        string suffix = id.Substring(lastUnderscore + 1);
        if (int.TryParse(suffix, out _))
            seriesId = id.Substring(0, lastUnderscore);
        else
            seriesId = id;

        return !string.IsNullOrWhiteSpace(seriesId);
    }
}
