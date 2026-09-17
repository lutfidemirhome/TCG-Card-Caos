using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Scene-owned, ordered pack contents. Draft edits never touch floor cards.</summary>
public sealed class PackAssignmentWindow : EditorWindow
{
    [Serializable]
    sealed class Draft
    {
        public WorldBoosterPack pack;
        public PackCardSet language;
        public string label;
        public List<CardDefinition> cards = new List<CardDefinition>();
        public List<CardDefinition> original = new List<CardDefinition>();
    }

    [Serializable] sealed class StoredDraft
    {
        public string scenePath;
        public List<StoredPack> packs = new List<StoredPack>();
    }
    [Serializable] sealed class StoredPack
    {
        public string id;
        public PackCardSet language;
        public string[] cards;
        public string[] original;
    }
    const string DraftPath = "Library/PackCardAssignments.draft.json";

    [SerializeField] List<Draft> drafts = new List<Draft>();
    [SerializeField] PhysicsLevelLayout layout;
    [SerializeField] int selected;
    [SerializeField] bool dirty;
    Vector2 packScroll, detailScroll;
    string search = "";
    int languageFilter;
    string notice;
    readonly List<string> errors = new List<string>();
    readonly Dictionary<string, string> owners = new Dictionary<string, string>(StringComparer.Ordinal);

    [MenuItem("TCG Card Chaos/Pack Kart Atamalari")]
    public static void Open()
    {
        var window = GetWindow<PackAssignmentWindow>("Pack Kart Atamaları");
        window.minSize = new Vector2(840f, 620f);
        var selectedPack = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<WorldBoosterPack>() : null;
        int selectedIndex = window.drafts.FindIndex(d => d.pack == selectedPack);
        if (selectedIndex >= 0) window.selected = selectedIndex;
        window.Show();
    }

    public static void OpenFor(WorldBoosterPack pack)
    {
        Open();
        var window = GetWindow<PackAssignmentWindow>();
        if (window.drafts.Count == 0)
            window.ReadScene();
        int index = window.drafts.FindIndex(d => d.pack == pack);
        if (index >= 0)
            window.selected = index;
    }

    void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndo;
        if (!EditorApplication.isPlayingOrWillChangePlaymode
            && (drafts.Count == 0 || layout == null || drafts.Any(d => d.pack == null)))
        {
            if (ReadScene())
                RestoreDraft();
        }
        else
            ValidateDrafts();
    }

    void OnDisable() => Undo.undoRedoPerformed -= OnUndo;
    void OnUndo()
    {
        if (!dirty)
            ReadScene();
        Repaint();
    }

    bool ReadScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
            return false;
        PhysicsLevelLayout found = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PhysicsLevelLayout>(true)).FirstOrDefault();
        if (found == null)
            return false;
        layout = found;
        drafts.Clear();
        WorldBoosterPack[] packs = layout.GetComponentsInChildren<WorldBoosterPack>(true);
        Array.Sort(packs, (a, b) =>
        {
            int language = a.PackSet.CompareTo(b.PackSet);
            if (language != 0) return language;
            int number = TrailingNumber(a.name).CompareTo(TrailingNumber(b.name));
            return number != 0 ? number : string.CompareOrdinal(a.name, b.name);
        });
        int english = 0, japanese = 0;
        foreach (WorldBoosterPack pack in packs)
        {
            var contents = pack.PeekPreRolledContents();
            int number = pack.PackSet == PackCardSet.Japanese ? ++japanese : ++english;
            string label = string.IsNullOrEmpty(pack.AssignmentLabel)
                ? (pack.PackSet == PackCardSet.Japanese ? "JP " : "EN ") + number.ToString("000")
                : pack.AssignmentLabel;
            var draft = new Draft { pack = pack, language = pack.PackSet, label = label };
            if (contents != null)
            {
                draft.cards.AddRange(contents);
                draft.original.AddRange(contents);
            }
            while (draft.cards.Count < CardDimensions.CardsPerBoosterPack)
                draft.cards.Add(null);
            drafts.Add(draft);
        }
        selected = Mathf.Clamp(selected, 0, Mathf.Max(0, drafts.Count - 1));
        dirty = false;
        ValidateDrafts();
        return true;
    }

    static int TrailingNumber(string value)
    {
        int start = value.LastIndexOf('_') + 1;
        return int.TryParse(value.Substring(start), out int number) ? number : int.MaxValue;
    }

    void OnGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Atama yapmak için Play modunu durdur.", MessageType.Info);
            return;
        }
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Sabit paketler · 100 İngilizce + 10 Japonca", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Sahneden yeniden oku", EditorStyles.toolbarButton))
            {
                bool discard = dirty && layout != null;
                if (!discard || EditorUtility.DisplayDialog("Taslak değişiklikler", "Uygulanmamış seçimler silinip sahnedeki atamalar okunacak.", "Yeniden oku", "Vazgeç"))
                {
                    if (ReadScene())
                    {
                        if (discard) DeleteDraft();
                        else RestoreDraft();
                    }
                    else notice = "Önce kaydedilmiş MainScene'i aktif sahne olarak aç. Taslak seçimler korunuyor.";
                }
            }
        }
        if (layout == null || drafts.Count == 0)
        {
            EditorGUILayout.HelpBox("MainScene'i aç, sonra Sahneden yeniden oku düğmesine bas.", MessageType.Info);
            return;
        }
        EditorGUILayout.HelpBox("Her pakete sırayla 5 kart seç. Kartlar her oyuncuda bu sırayla çıkar. Taslak bu bilgisayarda otomatik saklanır; Uygula düğmesi paketleri ve yerdeki kartları birlikte günceller.", MessageType.Info);
        if (!string.IsNullOrEmpty(notice))
            EditorGUILayout.HelpBox(notice, MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(210f)))
            {
                languageFilter = GUILayout.Toolbar(languageFilter, new[] { "Hepsi", "EN", "JP" });
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                packScroll = EditorGUILayout.BeginScrollView(packScroll);
                for (int i = 0; i < drafts.Count; i++)
                {
                    Draft draft = drafts[i];
                    if (languageFilter == 1 && draft.language != PackCardSet.English
                        || languageFilter == 2 && draft.language != PackCardSet.Japanese)
                        continue;
                    if (!string.IsNullOrWhiteSpace(search)
                        && draft.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                        && (draft.pack == null || draft.pack.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0))
                        continue;
                    bool invalid = errors.Any(e => e.StartsWith(draft.label + ":", StringComparison.Ordinal));
                    if (GUILayout.Toggle(selected == i, (invalid ? "⚠ " : "") + draft.label, "Button"))
                        selected = i;
                }
                EditorGUILayout.EndScrollView();
            }
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            DrawSelected();
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Kontrol: {errors.Count} sorun", EditorStyles.boldLabel);
            foreach (string error in errors.Take(15))
                EditorGUILayout.HelpBox(error, MessageType.Warning);
            if (errors.Count > 15)
                EditorGUILayout.LabelField($"… ve {errors.Count - 15} sorun daha.");
            EditorGUILayout.EndScrollView();
        }
        using (new EditorGUI.DisabledScope(errors.Count > 0))
        {
            if (GUILayout.Button("Atamaları uygula ve yerdeki kopyaları kaldır", GUILayout.Height(34f)))
                Apply();
        }
        EditorGUILayout.LabelField("Uyguladıktan sonra sahneyi Ctrl/Cmd+S ile kaydet. Mevcut oyun kayıtları kendi paket içeriklerini korur.", EditorStyles.wordWrappedMiniLabel);
    }

    void DrawSelected()
    {
        if (selected < 0 || selected >= drafts.Count)
            return;
        Draft draft = drafts[selected];
        EditorGUILayout.LabelField(draft.label + (draft.language == PackCardSet.Japanese ? " — Japonca" : " — İngilizce"), EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Sahnedeki paket", draft.pack, typeof(WorldBoosterPack), true);
        if (GUILayout.Button("Paketi sahnede seç") && draft.pack != null)
        {
            Selection.activeGameObject = draft.pack.gameObject;
            EditorGUIUtility.PingObject(draft.pack);
        }
        for (int i = 0; i < draft.cards.Count; i++)
        {
            int slot = i;
            CardDefinition card = draft.cards[i];
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(76f)))
            {
                Rect image = GUILayoutUtility.GetRect(50f, 70f, GUILayout.Width(50f));
                if (card != null && card.FrontTexture != null)
                    GUI.DrawTexture(image, card.FrontTexture, ScaleMode.ScaleToFit);
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    var next = (CardDefinition)EditorGUILayout.ObjectField($"Kart {i + 1}", card, typeof(CardDefinition), false);
                    if (EditorGUI.EndChangeCheck())
                        Assign(draft, slot, next);
                    if (card != null)
                        EditorGUILayout.LabelField(card.DefinitionId, EditorStyles.miniLabel);
                    if (GUILayout.Button("Kart ara / seç", GUILayout.Width(140f)))
                        PackAssignmentCardPicker.Open(draft.language, Ownership, chosen => Assign(draft, slot, chosen));
                }
            }
        }
        if (draft.cards.Count > CardDimensions.CardsPerBoosterPack && GUILayout.Button("Fazla alanları kaldır (ilk 5 kalsın)"))
        {
            draft.cards.RemoveRange(CardDimensions.CardsPerBoosterPack, draft.cards.Count - CardDimensions.CardsPerBoosterPack);
            dirty = true;
            ValidateDrafts();
            StoreDraft();
        }
    }

    string Ownership(CardDefinition card) => card != null && !string.IsNullOrEmpty(card.DefinitionId)
        && owners.TryGetValue(card.DefinitionId, out string owner) ? owner : null;

    void Assign(Draft draft, int slot, CardDefinition card)
    {
        if (this == null || EditorApplication.isPlayingOrWillChangePlaymode
            || !drafts.Contains(draft) || slot < 0 || slot >= draft.cards.Count)
            return;
        if (card != null)
        {
            if (!IsCatalogAsset(card))
            {
                notice = "Kart Assets/Resources/Cards/Definitions altında olmalı; aksi halde kayıt yüklenirken bulunamaz.";
                Repaint();
                return;
            }
            if (card.IsJapanese != (draft.language == PackCardSet.Japanese))
            {
                notice = "Bu kart paketin diliyle eşleşmiyor; seçim uygulanmadı.";
                Repaint();
                return;
            }
            foreach (Draft other in drafts)
                for (int i = 0; i < other.cards.Count; i++)
                    if (!(other == draft && i == slot) && other.cards[i] != null
                        && other.cards[i].DefinitionId == card.DefinitionId)
                    {
                        notice = $"Bu kart zaten {other.label}, Kart {i + 1} içinde. Önce oradaki atamayı kaldır; aynı kart iki kez kullanılamaz.";
                        ShowNotification(new GUIContent(notice));
                        Repaint();
                        return;
                    }
        }
        draft.cards[slot] = card;
        dirty = true;
        notice = null;
        ValidateDrafts();
        StoreDraft();
        Repaint();
    }

    void ValidateDrafts()
    {
        errors.Clear();
        owners.Clear();
        int english = drafts.Count(d => d.language == PackCardSet.English);
        int japanese = drafts.Count(d => d.language == PackCardSet.Japanese);
        if (english != PhysicsLevelLayout.MixPackCount || japanese != PhysicsLevelLayout.JapanPackCount)
            errors.Add($"Paket sayısı: EN {english}/100, JP {japanese}/10. Doğru sahneyi ve paketleri kontrol et.");
        foreach (Draft draft in drafts)
        {
            if (draft.pack == null)
                errors.Add(draft.label + ": Paket sahneden silinmiş; yeniden oku.");
            if (draft.cards.Count != CardDimensions.CardsPerBoosterPack)
                errors.Add(draft.label + ": Tam olarak 5 kart olmalı.");
            for (int i = 0; i < draft.cards.Count; i++)
            {
                CardDefinition card = draft.cards[i];
                string prefix = draft.label + $": Kart {i + 1} — ";
                if (card == null) { errors.Add(prefix + "boş."); continue; }
                if (card.IsJapanese != (draft.language == PackCardSet.Japanese))
                    errors.Add(prefix + "yanlış dil.");
                if (string.IsNullOrWhiteSpace(card.DefinitionId) || card.FrontTexture == null)
                    errors.Add(prefix + "kart kimliği veya görseli eksik.");
                if (!IsCatalogAsset(card))
                    errors.Add(prefix + "kart oyun kataloğunda değil; Resources/Cards/Definitions altında olmalı.");
                if (string.IsNullOrWhiteSpace(card.DefinitionId)) continue;
                if (owners.TryGetValue(card.DefinitionId, out string previous))
                    errors.Add(prefix + card.DefinitionId + " zaten " + previous + " içinde.");
                else
                    owners.Add(card.DefinitionId, draft.label + $" / Kart {i + 1}");
            }
        }
    }

    void Apply()
    {
        ValidateDrafts();
        if (errors.Count > 0 || layout == null || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        WorldBoosterPack[] current = layout.GetComponentsInChildren<WorldBoosterPack>(true);
        if (current.Length != drafts.Count || drafts.Any(d => d.pack == null || !current.Contains(d.pack)
            || d.pack.PackSet != d.language || !(d.pack.PeekPreRolledContents() ?? Array.Empty<CardDefinition>()).SequenceEqual(d.original)))
        {
            notice = "Sahnedeki paketler taslak hazırlanırken değişmiş. Sahneden yeniden oku ve atamaları kontrol et.";
            return;
        }
        var before = new HashSet<CardDefinition>(drafts.SelectMany(d => d.original).Where(c => c != null));
        var after = new HashSet<CardDefinition>(drafts.SelectMany(d => d.cards));
        string preview;
        try { preview = PackFloorReconciler.Describe(layout, before, after); }
        catch (Exception exception) { notice = exception.Message; return; }
        if (!EditorUtility.DisplayDialog("Paket atamalarını uygula", preview + "\n\nPaketler ve yerdeki kartlar birlikte değişecek. İşlem Undo ile geri alınabilir.", "Uygula", "Vazgeç"))
            return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Paket kart atamalarını uygula");
        try
        {
            foreach (Draft draft in drafts)
            {
                Undo.RecordObject(draft.pack, "Sabit paket içeriği");
                var serialized = new SerializedObject(draft.pack);
                var list = serialized.FindProperty("preRolledContents");
                list.arraySize = draft.cards.Count;
                for (int i = 0; i < draft.cards.Count; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = draft.cards[i];
                serialized.FindProperty("assignmentLabel").stringValue = draft.label;
                serialized.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(draft.pack);
                EditorUtility.SetDirty(draft.pack);
            }
            string report = PackFloorReconciler.Apply(layout, before, after);
            List<string> sceneProblems = PackAssignmentSceneValidation.FindProblems(layout);
            if (sceneProblems.Count > 0)
                throw new InvalidOperationException(string.Join("\n", sceneProblems.Take(8)));
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            Undo.CollapseUndoOperations(group);
            notice = report + " Sahneyi Ctrl/Cmd+S ile kaydet. Yeni atamalar yeni oyunda kullanılır.";
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            notice = "Atama uygulanamadı; işlem geri alındı: " + exception.Message;
            Debug.LogException(exception);
            return;
        }
        // Disk cleanup must never roll back an already validated scene edit.
        DeleteDraft();
        ReadScene();
    }

    static bool IsCatalogAsset(CardDefinition card) =>
        AssetDatabase.GetAssetPath(card).StartsWith("Assets/Resources/Cards/Definitions/", StringComparison.Ordinal);

    static string AssetId(CardDefinition card) => card == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(card));

    void StoreDraft()
    {
        if (layout == null) return;
        var stored = new StoredDraft { scenePath = layout.gameObject.scene.path };
        foreach (Draft draft in drafts)
        {
            if (draft.pack == null) continue;
            stored.packs.Add(new StoredPack
            {
                id = GlobalObjectId.GetGlobalObjectIdSlow(draft.pack).ToString(),
                language = draft.language,
                cards = draft.cards.Select(AssetId).ToArray(),
                original = draft.original.Select(AssetId).ToArray(),
            });
        }
        try
        {
            string tempPath = DraftPath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(stored, true));
            if (File.Exists(DraftPath))
                File.Replace(tempPath, DraftPath, null);
            else
                File.Move(tempPath, DraftPath);
        }
        catch (Exception exception) { notice = "Taslak diske yazılamadı: " + exception.Message; }
    }

    void RestoreDraft()
    {
        if (layout == null || !File.Exists(DraftPath)) return;
        try
        {
            var stored = JsonUtility.FromJson<StoredDraft>(File.ReadAllText(DraftPath));
            if (stored == null || stored.packs == null || stored.scenePath != layout.gameObject.scene.path || stored.packs.Count != drafts.Count) return;
            var entries = stored.packs.ToDictionary(p => p.id, StringComparer.Ordinal);
            var restored = new Dictionary<Draft, List<CardDefinition>>();
            // Only revive a draft against exactly the scene contents it was based on.
            foreach (Draft draft in drafts)
                if (!entries.TryGetValue(GlobalObjectId.GetGlobalObjectIdSlow(draft.pack).ToString(), out StoredPack entry)
                    || entry.language != draft.language || entry.original == null || entry.cards == null
                    || !entry.original.SequenceEqual(draft.original.Select(AssetId)))
                {
                    notice = "Sahne değiştiği için eski yerel taslak yüklenmedi.";
                    return;
                }
            foreach (Draft draft in drafts)
            {
                StoredPack entry = entries[GlobalObjectId.GetGlobalObjectIdSlow(draft.pack).ToString()];
                restored.Add(draft, entry.cards.Select(g => string.IsNullOrEmpty(g) ? null
                    : AssetDatabase.LoadAssetAtPath<CardDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToList());
            }
            foreach (var pair in restored)
                pair.Key.cards = pair.Value;
            dirty = true;
            notice = "Kaydedilmiş yerel taslak geri yüklendi.";
            ValidateDrafts();
        }
        catch (Exception exception) { notice = "Yerel taslak okunamadı: " + exception.Message; }
    }

    void DeleteDraft()
    {
        try { if (File.Exists(DraftPath)) File.Delete(DraftPath); }
        catch (Exception exception) { notice = "Sahne korunuyor; eski taslak dosyası silinemedi: " + exception.Message; }
    }
}

sealed class PackAssignmentCardPicker : EditorWindow
{
    List<CardDefinition> cards;
    Action<CardDefinition> choose;
    Func<CardDefinition, string> owner;
    string search = "";
    Vector2 scroll;

    public static void Open(PackCardSet language, Func<CardDefinition, string> owner, Action<CardDefinition> choose)
    {
        var window = CreateInstance<PackAssignmentCardPicker>();
        window.titleContent = new GUIContent(language == PackCardSet.Japanese ? "Japonca kart seç" : "İngilizce kart seç");
        window.choose = choose;
        window.owner = owner;
        window.cards = AssetDatabase.FindAssets("t:CardDefinition", new[] { "Assets/Resources/Cards/Definitions" })
            .Select(g => AssetDatabase.LoadAssetAtPath<CardDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null && c.FrontTexture != null && c.IsJapanese == (language == PackCardSet.Japanese))
            .OrderBy(c => c.DefinitionId, StringComparer.Ordinal).ToList();
        window.minSize = new Vector2(580f, 430f);
        window.ShowUtility();
    }

    void OnGUI()
    {
        if (cards == null) { Close(); return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Kart seçmeden önce Play modunu durdur.", MessageType.Info);
            return;
        }
        EditorGUILayout.LabelField("İsim, kart kimliği veya kategori ara", EditorStyles.boldLabel);
        search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        int shown = 0;
        CardDefinition picked = null;
        foreach (CardDefinition card in cards)
        {
            if (card == null) continue;
            if (!string.IsNullOrWhiteSpace(search) && (card.DefinitionId + " " + card.DisplayName + " " + card.ShelfCategoryId)
                .IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (++shown > 100) break;
            string used = owner?.Invoke(card);
            if (GUILayout.Button(card.DisplayName + "  ·  " + card.DefinitionId + (used == null ? "" : "\nKullanılıyor: " + used), GUILayout.MinHeight(34f)))
            {
                picked = card;
                break;
            }
        }
        EditorGUILayout.EndScrollView();
        if (picked != null)
        {
            choose?.Invoke(picked);
            Close();
            return;
        }
        if (shown > 100) EditorGUILayout.HelpBox("İlk 100 sonuç gösteriliyor. Aramayı daralt.", MessageType.Info);
    }
}
