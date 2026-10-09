# Yetenek UI dosyaları

Düzenlenebilir prefab: `Assets/Resources/UI/Skills/SkillUI.prefab`.

## Tab penceresi

Prefab düzenlerken `Panel` nesnesini aç. Oyunda Tab ile açılır, Tab veya Esc ile kapanır.

- `Panel/Body`: 1600 × 900 referans boyutunda açık kâğıt panel. Küçük ekranlarda bütünüyle ölçeklenir; üstte dış kapatma düğmesine alan bırakılır.
- `Panel/Close`: ekranın sol üstüne sabitlenmiş ESC / Geri düğmesi, panelin dışında. Etiket `pause.back` çevirisini kullanır.
- `Panel/Body/Title`, `Instructions`, `Points`: başlık, mevcut yükseltme açıklaması ve gerçek yükseltme hakkı. Yalnız sayısal puan değeri TMP rich text ile yeşildir.
- `Panel/Body/Nodes/Skill0` … `Skill4`: kalıcı yetenek kimlikleri değişmez (Assemble, Sort, Guide, Insight, Autoshelf). Görsel sıralama alt skill barı ile aynıdır: Sort, Guide, Insight, Autoshelf, Assemble.
- Her düğmede mevcut `Icon`, `Name`, `Level`, `Check` ve yalnız seçili yetenekte görünen altın `Selection` çerçevesi vardır.
- `Panel/Body/Progress/Title`, `Description`: mevcut sıradaki raf/TCG dolabı hedefi; yeni oyun mekaniği eklenmez.
- `Panel/Body/Details/IconFrame/Icon`: seçili yeteneğin mevcut simgesi.
- `Panel/Body/Details/Name`, `Level`, `Description`: gerçek yetenek adı, seviyesi ve açıklaması.
- `Panel/Body/Details/Next`, `Stats`: mevcut → sonraki süre/miktar. Sonraki değer yeşildir. Kilitliyken mevcut değer `—`, son seviyede yalnız mevcut değer gösterilir.
- `Panel/Body/Details/Upgrade`: yeşil, altın çerçeveli düğme; 1 hak harcar. Hak yokken veya son seviyedeyken pasiftir.

Yazılar PNG içine işlenmemiştir; TextMeshPro bileşenleri ve mevcut 12 dildeki localization anahtarları kullanılır. ESC tuş resmi mevcut oyun UI varlığıdır. Baloo 2 ve mevcut Noto fallback zinciri korunur. Panel metinleri kenarlıksız `Materials/SkillTextNoOutline.mat` kullanır; ortak font materyali değiştirilmez. Tasarım font boyutlarına %15 küçültme uygulanır ve uzun çeviriler için Auto Size açıktır.

## Görseller ve yeniden düzenleme

- `Assets/UI/Skills/Panel/ParchmentPanel.png`: yerleşik Imagegen ile üretilmiş yazısız, transparan kenarlı kâğıt/altın çerçeve. Unity Sprite olarak içe aktarılır.
- Aynı klasördeki `Inset`, `Badge`, `Level`, `Tile`, `Selection`, `Upgrade`: editör aracının ürettiği küçük nine-slice arayüz yüzeyleri. Metin veya skill simgesi içermez.
- Yetenek simgeleri doğrudan `Assets/UI/Skills/Hotbar/Skill*.png` dosyalarıdır; kopyalanmaz veya yeniden çizilmez.
- `TCG Card Chaos > UI > Apply Skill Panel Design`: yalnız bu prefab'ın Tab paneline tasarımı yeniden uygular. Task/Hotbar, sahneler, çeviri tablosu ve yetenek kayıtları değişmez. Prefab üzerinde elle yapılan panel konumlarını yeniden kuracağı için yalnız tasarımı sıfırlamak istediğinde çalıştır.

Kâğıt görselinin üretim istemi:

> Create a production game UI background asset ONLY, no interface content. Wide landscape rectangular popup panel, aspect 1.62:1, a single warm ivory aged paper surface with a fine double brass gold outline, gently rounded small corners, tasteful tiny understated antique corner flourishes. Close match to a cozy card-shop game abilities menu. Flat front-facing orthographic 2D sprite. Pale cream paper #F4E8CD with very subtle fibers, almost uniform clean bright center for dark readable overlay text, quiet warm weathering only near corners. Border slender and elegant, gold not bright yellow, slight bevel depth. Panel occupies almost the entire image with a small TRANSPARENT margin outside its rounded edges. Center entirely EMPTY uninterrupted paper, no text no numbers no letters no stars no icons no buttons no dividers no inset panels no logos no watermark. Genuine alpha transparency outside panel. Professional clean game UI asset. Use large high resolution landscape output.

## Kontroller

Editör menüsündeki `Validate Skill Panel`, 12 dil × 5 yetenek × 3 seviye durumunda TMP taşmasını kontrol eder. `Validate Skill Panel Behavior`, geçici ilerleme ile gerçek seçim/yükseltme/kapatma callback'lerini çalıştırır, tüm geçici durumu geri alır ve kayıt dosyalarına yazmaz. İki araç da Play Mode dışında kullanılır; raporlar `Temp/skill-panel-*.txt` altındadır.

`Render Skill Panel Preview`, gerçek prefab'ın Unity UI meshleriyle İngilizce/Türkçe önizleme üretir. Bu bağımsız editör önizlemesi oyun sahnesinin ekran görüntüsü değildir.

## Diğer HUD alanları

`Task` sol üst sayaçların altındaki mevcut hedef kutusu, `Hotbar` sol alttaki mevcut 1–5 yetenek göstergeleridir. Bu yeniden tasarım yalnız Tab penceresini değiştirir; bu iki alanın düzeni, tuşları ve cooldown/aktif süre davranışı korunur.

Yetenek kuralları: `Docs/SkillSystemPlan.md`. Steam adımları: `Docs/SteamSkillAchievements.md`.
