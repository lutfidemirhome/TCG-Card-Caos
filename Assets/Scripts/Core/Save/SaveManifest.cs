using System;

[Serializable]
public class SaveManifest
{
    public int saveVersion = GameSaveSettings.CurrentSaveVersion;
    public int nextAutosaveIndex;
}
