# Yetenek UI dosyaları

Düzenlenebilir prefab: `Assets/Resources/UI/Skills/SkillUI.prefab`.

- `Task`: sol üst sayaçların altındaki görev kutusu.
- `Hotbar`: sol altta zıplama göstergesinin hemen sağından başlayan 2–6 yetenek kutuları. Jump ile aynı 108×108 boyut, 12 piksel aralık ve koyu arka plan; tuş numaraları ayrı `KeyHint` çocuklarıdır.
- `Panel/Body`: Tab ile açılan büyük yetenek paneli. Prefab düzenlerken `Panel` nesnesini aç; oyunda başlangıçta otomatik kapanır.
- `Art`: mevcut oyun UI görsellerinin bağımsız kopyaları. Görselleri aynı adla ezebilirsin; `.meta` dosyalarını koru.
- `Materials/SkillTextNoOutline.mat`: bu paneldeki koyu renkli başlık, seviye ve düğme yazılarına özel kenarlıksız materyal. Ortak font materyali değiştirilmez.

Boyutları, renkleri ve görselleri düzenleyebilirsin. Kodun bulduğu nesne adlarını ve hiyerarşiyi koru. Modal küçük ekrana otomatik sığdırılır. Görev kutusunun konumu mevcut üst sol sayacın altına bağlanır.

Yeni oyunda görev kutusu ve Tab ile panel erişimi, sağ tutorial'ın aynı başlıktaki kalan kartları bulup aynı rafa dizme aşamasında açılır. Sonraki aşamalarda açık kalır. Devam et/kayıt yükle akışında tutorial tekrar oynatılmadığı için panel erişimi korunur.

Oyuncu yazıları prefab örnek metninden değil `LocalizationTable.asset` içindeki `skills.*` anahtarlarından gelir. Dil desteğini korumak için yazıları oradan değiştir. Kullanılmayan Paver Layout Plan sekmesi yoktur.

Kurallar ve PDF özeti: `Docs/SkillSystemPlan.md`.
