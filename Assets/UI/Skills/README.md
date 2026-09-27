# Yetenek UI dosyaları

Düzenlenebilir prefab: `Assets/Resources/UI/Skills/SkillUI.prefab`.

## Tek sayfalık skill pop-up

Prefab düzenlerken `Panel` nesnesini aç. Oyunda Tab ile açılır, Tab veya Esc ile kapanır.

- `Panel/Body/Title`, `Instructions`, `Points`: sol taraftaki başlık, ilerleme açıklaması ve yükseltme hakkı sayısı.
- `Panel/Body/Nodes/Skill0` … `Skill4`: soldan sağa beş seçim düğmesi. Her birinde `Icon`, `Name`, `Level` ve açılmış yetenek için `Check` bulunur.
- `Panel/Body/Details/IconFrame/Icon`: seçili yeteneğin büyük simgesi. Kod soldaki düğmenin simgesini buraya kopyalar; ayrı görsel atamana gerek yok.
- `Panel/Body/Details/Name`, `Level`, `Description`: seçilen yeteneğin başlığı, seviyesi ve açıklaması.
- `Panel/Body/Details/Next`, `Stats`: sonraki seviye başlığı ve **mevcut → sonraki** süre/miktar değerleri. Kilitli yetenekte mevcut değer `—`, en yüksek seviyede yalnız mevcut değer gösterilir.
- `Panel/Body/Details/Upgrade`: seçilen yeteneğe 1 hak harcayan yükseltme düğmesi.
- `Panel/Body/Close`: kapatma düğmesi.

Tek pop-up içinde iki bilgi alanı vardır; kitap sayfası, Minor Magic veya Paver Layout Plan bölümü yoktur. Panel küçük ekrana bütün olarak sığdırılır. Seçim yalnız açıklamayı değiştirir; yükseltme ayrı düğmeyle yapılır.

## Değiştirilebilir görseller

Tüm skill paneli görselleri `Assets/UI/Skills/Art` altındaki bağımsız kopyalardır. PNG dosyalarını aynı adla ezebilirsin; **`.meta` dosyalarını koru**. Asıl oyun UI görselleri değiştirilmez. Simgeler geçici olarak mevcut oyun görsellerinden kopyalandı; her yeteneğin ayrı dosyası ve GUID'i vardır.

| Dosya | Kullanım / kopyalanan kaynak |
| --- | --- |
| `skill_icon_assemble.png` | Assemble; HUD el/kart görseli |
| `skill_icon_sort.png` | Sort; HUD el/kart görselinin ayrı kopyası |
| `skill_icon_guide.png` | Shelf Guide; HUD dolap/kart görseli |
| `skill_icon_insight.png` | Insight; mevcut pack açılışındaki ışıldama görseli |
| `skill_icon_autoshelf.png` | Autoshelving; HUD dolap/kart görselinin ayrı kopyası |
| `skill_node.png` | Sol seçim düğmeleri ve büyük simge çerçevesi |
| `skill_details_panel.png` | Tek büyük pop-up'ın arka planı |
| `skill_panel_bg.png` | Sağ bilgi alanının arka planı |
| `skill_upgrade_button.png` | Yükseltme düğmesi |
| `skill_close_hint.png` | Kapatma düğmesi |
| `skill_unlocked_icon.png` | Açılmış yetenek işareti |
| `skill_task_panel.png` | Sol üst görev kutusu |

PNG değiştirmenin dışında boyut, renk ve konumları prefab'da düzenleyebilirsin. Kodun bulduğu nesne adlarını ve hiyerarşiyi koru. Yeni simgeler en fazla 256 px olarak içe aktarılır, en-boy oranı korunur.

`Materials/SkillTextNoOutline.mat` bu pop-up'ın yazılarına özel kenarlıksız materyaldir. Ortak font materyali değiştirilmez. Görev ve alt gösterge yazıları kendi mevcut materyallerini korur.

## Diğer HUD alanları

- `Task`: sol üst sayaçların altındaki görev kutusu; konumu mevcut sayaca bağlanır.
- `Hotbar`: sol alt köşeden sağa sıralanan 1–5 yetenek kutuları. Kutular 108×108, aralık 12 piksel; tuş numaraları `KeyHint` çocuklarıdır. Zıplama göstergesi yoktur.
- Görev kutusu ve Tab erişimi oyun hazır olup welcome penceresi kapanınca açılır; tutorial aşamasını beklemez.

Oyuncu metinleri prefab örnek metninden değil `LocalizationTable.asset` içindeki `skills.*` anahtarlarından gelir. Dil desteği için yazıları oradan değiştir. Yerleşim veya simge değişikliği yetenek seviyelerini/kayıtlarını değiştirmez.

Kurallar: `Docs/SkillSystemPlan.md`. Steam kurulum adımları: `Docs/SteamSkillAchievements.md`; başarım listesi: `Docs/SteamSkillAchievements.csv`.
