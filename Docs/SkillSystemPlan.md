# Skill sistemi — güncel davranış (2026-09-26)

Bu belge önceki PDF aktarımını ve son geri bildirimlerle değişen kuralları birlikte açıklar. Son kullanıcı kararları önceki tercihlerin yerini alır. Çalışma branch'i: `full_release_card_1`.

## Başlangıç, zıplama ve arayüz

- Oyuna başlayıp welcome penceresini kapattığın anda skill görev kutusu, alt skill göstergeleri ve Tab paneli kullanılabilir. Tutorial aşaması beklenmez. Panel kendiliğinden açılmaz; Tab ile açılır.
- Space mevcut güçlendirilmiş zıplamayı sürekli kullanır. Önceki Double Jump düğmesini 1 ile açma/kapatma kaldırıldı. Bu, mevcut yüksek zıplamadır; yeni bir havada ikinci sıçrama mekaniği eklenmedi.
- Zıplama alt UI'da yer almaz. Beş kart skill'i sol alt köşeden sağa doğru sıralanır. Kullanım tuşları sırasıyla 1–5 (üst sayı tuşları ve numpad).
- Görev kutusu bir sonraki raf hedefi ile toplam ilerlemeyi gösterir. Harcanmamış yükseltme hakkı varsa ayrıca görünür; hedef metni kaybolmaz.
- Tab paneli tek sayfalık bir pop-up'tır: solda beş yetenek simgesi, seviyeler ve yükseltme hakları; sağda seçilen yeteneğin büyük simgesi, adı, seviyesi, açıklaması, mevcut → sonraki değerleri ve ayrı yükseltme düğmesi vardır. Minor Magic veya ikinci plan sekmesi yoktur. Seçim yapmak hak harcamaz. Simge dosyalarının tamamı ayrı kopyalardır; değiştirme rehberi `Assets/UI/Skills/README.md` içindedir.
- Yeni/yenilenen oyuncu metinleri mevcut 12 dilde güncellendi. Skill görselleri `Assets/UI/Skills/Art` altındaki bağımsız kopyalardır; orijinal UI görselleri değiştirilmedi. `Assets/Resources/UI/Skills/SkillUI.prefab` düzenlenebilir.

## Raf, PSA ve kalıcı tamamlanma

- Normal dolapta bir yatay sıra bir raftır. Sıranın tüm kartları aynı seriden, doğru numarada ve doğru kategoride yerleşmiş olmalıdır. Uçuşlar bitmeden tamamlanmış sayılmaz.
- Bir PSA dolabının **bütün** aktif tutucuları doğru PSA kartlarıyla dolunca 1 raf ilerlemesi verir. Tek tutucu, tek PSA kartı veya dolabın kısmi sırası ayrı ilerleme vermez.
- Doğru tamamlanan normal sıranın veya PSA dolabının kartları kilitlenir. Elle, Assemble ile veya başka normal alma yoluyla geri alınamaz; yerleri değiştirilemez. Yanlış ya da eksik raflar bu kilidi almaz.
- Her normal seri yalnız bir kez ilerleme verir. PSA'da da kullanılan içerik kimlikleri kaydedilir; aynı PSA içerikleri başka dolap üzerinden tekrar ilerleme kazandıramaz.
- Kilitler kayıttaki yerleşimlerden yüklemede yeniden kurulur; tamamlanan seri/PSA içerik kimlikleri ayrıca kaydedilir.
- Eski kayıtların mevcut puan ve seviyeleri korunur. Hâlen doğru dolu olan raflar incelenip kilitlenir ve seri bilgileri öğrenilir. Önceki sürümde tamamlanıp sonra boşaltılmış bir rafın eski kart serisi kayda yazılmadığı için bu geçmiş bilgi geri çıkarılamaz. Yeni tamamlamalarda bu açık yoktur.
- Sol üstteki mevcut genel dolap sayacı ile skill'in yatay raf sayacı farklı şeyleri ölçer; genel sayaç değiştirilmedi.

## Yetenekler ve seviyeler

Beşi de normal kart ve PSA kartlarını destekler. Pack'ler toplama, rehber, gösterme veya otomatik yerleştirme hedefi değildir. Sort, kartları sıraladıktan sonra pack'leri elin sonuna taşır.
Hepsi gerçek yeni oyunda seviye 0/kilitli başlar. Seviye 1 açar; yükseltmelerin her biri 1 hak tüketir. Diziler seviye 1'den başlayarak sıralıdır; süreler saniyedir.

| Yetenek | Maksimum seviye | Bekleme süreleri | Etki / miktar |
| --- | --- | --- | --- |
| Assemble - Kart toplama | 10 | 120,110,100,90,80,70,60,40,30,10 | 1,2,3,4,5,6,7,8,9,9 kart |
| Sort - Sıralama | 5 | 30,25,20,10,5 | Eldeki kartları numara sırasına dizer |
| Shelf Guide - Raf rehberi | 10 | 60,50,40,30,30,25,25,20,10,5 | 15,20,25,30,35,40,45,50,55,60 saniye |
| Insight - Yerdeki kartları gösterme | 10 | 60,55,50,50,40,40,30,30,20,20 | 7,10,12,17,20,23,27,30,35,40 saniye |
| Autoshelving - Otomatik yerleştirme | 10 | 100,90,80,70,60,50,40,30,20,10 | 10,15,20,25,30,35,40,45,50,55 saniye |


### 1 — Assemble

Elde seçili kartın grubuna ait yerdeki kartları yakından uzağa ele toplar. Normal kartlarda grup aynı kart serisidir. PSA'da grup aynı dil/set ve aynı PSA numarasıdır (örneğin İngilizce PSA 7); varyantlar bu grubun içindedir. El kapasitesi, eldeki paketlerin kapladığı yerler dahil korunur. Raflardaki, kilitli, açılma gösterimindeki veya hâlen hareketli kartlar toplanmaz. Toplama uçuşu tamamlandıktan sonra bekleme başlar. En üst seviye PDF'deki gibi 9 karttır.

### 2 — Sort

Normal ve PSA kartlarını küçük numaradan büyüğe sıralar. Kartların önceliği seri/kategori/nadirlik değil, numaradır. Paketler kendi aralarındaki sırayı koruyarak en sona gider. Seçili nesne korunur.

Örnek: `X8 – Paket A – Y2 – PSA7 – X1 – Paket B` → `X1 – Y2 – PSA7 – X8 – Paket A – Paket B`.

Aynı numarada normal/PSA türü, PSA dili-varyantı, kart içerik kimliği ve son olarak kalıcı nesne kimliği sabit bir sıra sağlar. Bu ek sıralama numara önceliğini değiştirmez; eşit numaralı kartların her kullanımda rastgele yer değiştirmesini önler. Test: farklı serilerden 8, 2, 5 numaralarıyla iki paket al, 2'ye bas; kartlar 2,5,8 ve ardından paketler olmalı. Yalnız paket varsa skill çalışmaz. Sıralama işlemi anlıktır, ardından bekleme başlar.

### 3 — Shelf Guide

Seçili normal veya PSA kartına uygun tüm dolapların yalnız dış gövdeleri yeşil outline ile gösterilir. İç raflar, tutucular, kartlar ve etiketler tek tek çerçevelenmez. Gerçek dolap dış gövdesinden önceden hazırlanmış ayrı mesh kullanılır; köşe normalleri düzeltilmiştir. Eski tel kutu görünümü kaldırıldı. Kaynak model ve importer ayarları değişmez. Yeniden hazırlama menüsü: TCG Card Chaos → Skills → Bake Cabinet Exterior Outlines. Kartın yeri dolu olsa da ait olduğu dolabı gösterebilir. Etki sırasında seçili kart değişirse rehber güncellenir.

### 4 — Insight

Seçili normal serinin veya PSA dil/numara grubunun yerdeki kartlarını yeşil işaret ve yukarı yükselen ışık/ışıltıyla gösterir. Etki sırasında yere düşen uygun kartlar da takip edilir. Pack'ler ve raflara yerleşmiş kartlar dahil değildir. Seçili kart değişirse gösterilen grup güncellenir.

### 5 — Autoshelving

Elde seçili X serisi kart ve nişangâhın hedeflediği uygun raf gerekir. Dolaba genel olarak bakmak yeterli değildir; mevcut hedefleme mesafesi içinde rafın slot bölgesine bakılmalıdır. 5 tuşu yeteneği etkinleştirir; yalnız bakmak kart taşımaz. Etki açıkken rafa bakıp E’ye basıldığında o anda seçili X serisi ve o fiziksel raf sabitlenir. Tek E basışı bu grubun sıralı yerleştirmesini başlatır; normal tek kart yerleştirme aynı basışta ayrıca çalışmaz. X kartları numara sırasıyla yalnız doğru, boş slotlara gider. Elde Y serisi olsa, X bitince seçim Y'ye geçse veya aynı dolabın başka rafına bakılsa Y otomatik yerleştirilmez.

PSA'da seçilen dil/numara grubu ve hedeflenen PSA dolabı sabitlenir; aynı gruptaki kartlar uygun boş tutuculara gider. Başka dolaba gönderilmez. Bakışı hedeften ayırmak yerleştirmeyi duraklatır; kalan etki süresi işlemeye devam eder. Yeniden aynı hedefe bakınca devam eder. Kartları 0,16 saniye arayla mevcut yerleştirme uçuşuna verir. Slot, uçuş başladığında rezerve edilir ve kayıt güncellenir.

Hedef raf/dolap ve seçilen grup yeni kayıtlarda saklanır. Yükleme sonrasında aktif süre korunur ancak yeniden E’ye basmadan taşıma başlamaz. Etki sürerken başka bir grup/raf için yeniden E’ye basarak yeni bir yerleştirme başlatılabilir; yalnız bakmak hedefi değiştirmez.

## Süreler, kayıt ve yeni oyun

- Etkili skill'lerde önce etki süresi biter, **sonra tam bekleme süresi** saymaya başlar. Örneğin 15 sn etki + 60 sn bekleme, yeniden kullanım için toplam 75 sn'dir.
- Assemble'ın kısa toplama uçuşu da etkidir; bekleme bundan sonra başlar. Sort'un sıralama işlemi anlıktır.
- Menü/pause sırasında süreler durur. Başarısız kullanım bekleme başlatmaz.
- Devam et/yükle: gerçek seviyeler, harcanmış/kullanılabilir haklar, tamamlanan raflar/seriler, kalan etki ve bekleme süreleri, Autoshelving hedefi geri gelir.
- Yeni Oyun: raf ilerlemesi, gerçek seviyeler, etkiler ve beklemeler sıfırdır. Başlangıçtaki boş raflar puan vermez; editörde hazırlanmış doluluk da başlangıç ödülü sayılmaz. Normal full-release sahnesi boş dolaplarla başlar.
- Yeni Oyun, Steam hesabında daha önce açılmış başarımları geri almaz.

## Ödül takvimi ve Steam

| Tamamlanan raf aralığı | Ödül eşikleri |
| --- | --- |
| 1-12 | 2,3,4,5,6,7,8,9,10,11,12 |
| 13-18 | 14,16,18 |
| 19-27 | 21,24,27 |
| 28-39 | 31,35,39 |
| 40-49 | 44,49 |
| 50-61 | 55,61 |
| 62-75 | 68,75 |
| 76-91 | 83,91 |
| 92-109 | 100,109 |
| 110-129 | 119,129 |
| 130-140 | 140 |
| 141-152 | 152 |
| 153-165 | 165 |
| 166-179 | 179 |
| 180-194 | 194 |
| 195-214 | 214 |
| 215-239 | 239 |
| 240-269 | 269 |
| 270-309 | 309 |
| 310-359 | 359 |
| 360-409 | 409 |
| 410-459 | 459 |
| 460-509 | 509 |


Toplam 45 yükseltme eşiği, toplam 45 skill seviyesi vardır. Her eşik 1 hak verir; hangi skill'in yükseleceğini oyuncu seçer.

Steam görevleri 2–18 arasındaki her tamamlanma sayısında, sonra tablodaki sonraki eşiklerde açılır. Böylece 13,15,17 hedefleri de başarım verir, fakat bu üçü ekstra skill hakkı vermez. Toplam 48 başarım vardır. Başarımlar skill satın almaya değil, gerçek raf/dolap hedeflerini tamamlamaya bağlıdır.

Steamworks.NET 2025.164.1 projeye eklenmiştir. Oyun bağlantısı, hesabın doğru App ID'sini ve sunucu yanıtlarını kontrol ederek başarısız gönderimleri yeniden dener. Editor ve demo gerçek Steam başarımı göndermez. Steamworks tanımlarını ve gerçek Steam build kontrolünü kullanıcı tamamlamalı: [adım adım Steam rehberi](SteamSkillAchievements.md), [48 başarım listesi](SteamSkillAchievements.csv).

Periyodik autosave korunur. 2–18 arasındaki yeni raf tamamlamalarında, sonraki yükseltme eşiklerinde ve yükseltme satın alırken ek otomatik kayıt istenir; mevcut kayıt kuyruğu istekleri birleştirir.

## P test modu

Yalnız Unity Editor/Development Build'de P beş skill'i geçici en yüksek seviyeye açar; yeniden P kapatır. Gerçek seviyeler, puan harcaması ve süreler test değerleriyle ezilmez. Yeni oyun/yükleme test modunu kapatır. P panel açıkken de kullanılabilir. P'ye basmak tek başına Steam başarımı açmaz. P açıkken gerçekten raf tamamlarsan bu dünya değişikliği ve ilerleme yine oyun kaydına girer; sonraki gerçek oyun yüklemesinde Steam'e eşleşebilir.

## O ile iki raf doldurma

Yalnız Unity Editor/Development Build'de oyun açık ve menüler kapalıyken O'ya bir kez basmak, tek bir normal dolabın iki boş rafını yerdeki mevcut kartlarla doğru doldurur. İki farklı, daha önce ilerleme kazandırmamış tam seri ve uygun raflar önceden bulunur; bulunamazsa hiçbir kart değiştirilmez. Kart üretilmez, eldeki veya paket içindeki kartlar alınmaz. Tamamlanan raflar normal şekilde kilitlenir, ilerleme ve kayıt güncellenir. Yeni oyunda ilk iki raf 1 yükseltme hakkı verir; devam eden oyunda mevcut hedef tablosu geçerlidir. P test modu yükseltme satın almayı kapattığından gerçek yükseltmeyi denemek için P modu kapalı olmalıdır. Bu kısayolun yaptığı gerçek raf değişiklikleri kayda ve normal başarım ilerlemesine dahildir.

## Kontrol durumu

Unity 6000.0.80f1'de oyun çekirdeği, Editor assembly'si ve gerçek Steamworks.NET bağımlılığıyla Steam sağlayıcısı derlendi. İlerleme/kayıt/süre/tekrar-ödül mantığı bağımsız C# kontrollerinden geçti; Steam tekrar-deneme akışı sahte servisle kontrol edildi. Gerçek Steam hesabında başarım açılmadı. Bu skill değişiklikleri için Play Mode veya tam build testi yapılmadı; oyun ve görsel kontrol kullanıcıya bırakıldı.

Kısa oyun kontrolü:

1. Yeni Oyun: görev kutusu hemen gelsin; Space güçlü zıplasın; Jump kutusu olmasın. Skill tuşları 1–5 ve Tab normal çalışsın.
2. Bir normal rafı, ardından tam bir PSA dolabını doğru doldur: toplam 2 ilerleme ve 1 hak gör; tamamlanan kartları geri almaya çalış, alınmamalı.
3. P ile skilleri dene: normal ve PSA'da toplama/ışıltı/dış dolap outline'ı; karışık elde Sort paketleri sona atsın. Autoshelving sadece seçilen X grubunu hedef rafa koysun.
4. Süreli bir skill kullan: önce etki, sonra bekleme. Etki sürerken kaydet/yükle; hedef, süre, dolu raf kilidi ve ilerleme korunsun.
5. Yeni Oyun aç: önceki seviyeler/puanlar/süreler gelmesin. Steam testi için ayrı rehberdeki gerçek build akışını izle.
