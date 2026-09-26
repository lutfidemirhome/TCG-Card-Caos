# Skill sistemi - PDF aktarımı

Kaynaklar: `Skill plan.pdf` (3 dolu, 1 boş sayfa), `Skilller.pdf` (3 sayfa).
Bu belge PDF aktarımını, kabul edilen kararları ve uygulamanın düzenini kaydeder.

## Panel ve dosyalar

- Örnek görseldeki büyük skill paneli ve kilitli/açılmış seviye mantığı kullanıldı.
- Paver Layout Plan sekmesi yok.
- Mevcut oyunun görselleri bağımsız GUID'lerle `Assets/UI/Skills/Art` klasörüne kopyalandı.
- Orijinal UI dosyaları değiştirilmeyecek. Kopyalar aynı dosya ve .meta korunarak değiştirilebilir.
- Prefab, skill tanımları, UI kodu ve oyun mantığı ayrı klasörlerde tutuluyor.
- Seviye, mevcut bekleme/etki süreleri ve kazanılmış ödüller save kapsamına alındı.
- Oyun testlerini kullanıcı yapacak.

## Yetenekler

Hepsi seviye 0'da kilitli, seviye 1'de kullanıma açılıyor.
Tablodaki diziler seviye 1'den başlayarak sıralıdır, süreler saniyedir.

| Yetenek | Maksimum seviye | Bekleme süreleri | Etki / miktar |
| --- | --- | --- | --- |
| Assemble - Kart toplama | 10 | 120,110,100,90,80,70,60,40,30,10 | 1,2,3,4,5,6,7,8,9,9 kart |
| Sort - Sıralama | 5 | 30,25,20,10,5 | Eldeki kartları numara sırasına dizer |
| Shelf Guide - Raf rehberi | 10 | 60,50,40,30,30,25,25,20,10,5 | 15,20,25,30,35,40,45,50,55,60 saniye |
| Insight - Yerdeki kartları gösterme | 10 | 60,55,50,50,40,40,30,30,20,20 | 7,10,12,17,20,23,27,30,35,40 saniye |
| Autoshelving - Otomatik yerleştirme | 10 | 100,90,80,70,60,50,40,30,20,10 | 10,15,20,25,30,35,40,45,50,55 saniye |

- Assemble: Seçili eldeki kartın serisine ait yer kartlarını ele toplar. El kapasitesi korunur. Bekleme aktivasyonda başlar.
- Sort: Elde bulunan kartları sayısal sıraya koyar. Bekleme aktivasyonda başlar.
- Shelf Guide: Seçili kartın doğru dolabını vurgular, etki süresince yönlendirir.
- Insight: Seçili kartın serisindeki yerdeki diğer kartları etki süresince parlatır.
- Insight işareti: yeşil çerçeveye ek olarak karttan yaklaşık 1,2 metre yukarı uzanan, incelerek kaybolan ışık huzmesi ve 8 yükselen ışıltı gösterir. Ortak mesh/materyal kullanır; animasyon shader'da hesaplanır. Gerçek zamanlı ışık, collider veya kart başına Update yoktur. Son işaret kaldırılınca üretilen mesh de temizlenir.
- Autoshelving: Yalnız nişangâhın hedeflediği dolaba uygun eldeki kartları doğru slotlarına yerleştirir. Uygun kart yoksa etkinleşmez.
- Assemble seviye 10 miktarı PDF'deki gibi 9'dur; 10'a çıkarılmayacak.
- Jump bu PDF'lerde yok; mevcut jump davranışı korunmalı, yeni kısayollarla çakışmamalı.

## Ödül takvimi

Yazılı aralık kuralına göre eşikler:

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

Toplam: 45 ödül eşiği, 45 yetenek seviyesi. 509 sonrası PDF'de belirtilmiyor.
Bazı çizimlerde sıra numaraları atlanmış/tekrarlanmış; eşikler yazılı aralıklardan türetildi.

İlk autosave ve başarım 2. rafta. 2-12 arası her rafta yükseltme/autosave/başarım.
13-18 arasında her rafta autosave/başarım; yükseltme iki rafta bir.
19 sonrası skill ve başarım tablodaki eşiklerde; autosave sözcüğü üzeri çizili.
Mevcut periyodik autosave korunur. 2–18 arasındaki yeni tamamlanan raflarda, sonraki ödül eşiklerinde ve yükseltme satın alırken ek otomatik kayıt istenir. Mevcut kayıt sırası bu istekleri birleştirir.

## Kabul edilen kararlar ve uygulama tercihleri

- Raf, dolabın tek yatay sırasıdır; sol üstteki mevcut dolap sayacı değiştirilmedi.
- Her eşik 1 yükseltme puanı verir. Oyuncu istediği yeteneği bir seviye yükseltir; ücret 1 puandır.
- Görev kutusu sol üst sayaçların altında hedef/ilerlemeyi ve varsa kullanılabilir yükseltme puanını birlikte gösterir. 2–18 arasında her yeni raf ayrı görev hedefidir; puanlar yalnız ödül takvimindeki eşiklerde verilir. Platform başarımı eklenmedi.
- Her fiziksel yatay raf ilk tamamlandığında bir kez ilerleme verir. Aynı kart serisi başka bir fiziksel rafı ilk kez tamamlıyorsa onun ilerlemesi de kazanılır; kullanıcı bu kuralın korunmasını seçti. Aynı rafı boşaltıp tekrar doldurmak ikinci ilerleme vermez.
- Yeni oyunda sol görev kutusu ve Tab erişimi, sağ tutorial `Arrange` ("Find the remaining cards...") aşamasına gelince açılır ve açık kalır. Devam et/kayıt yükle akışında tutorial atlandığı için mevcut erişim korunur.
- Eski kayıtlardaki tamamlanmış normal kart rafları başlangıçta sayılır. Zaten kazanılmış raf kimlikleri ayrıca korunur.
- Bekleme aktivasyonda başlar. Etkisi devam eden bir yetenek beklemesi bitse bile tekrar başlatılamaz. Menü açıkken süreler donar.
- Seri ve normal raf odaklı dört yetenek normal kartlara uygulanır. Sort eldeki PSA kartlarını da numarasıyla sıralar. Paketler el sıralamasında aynı yerlerinde kalır.
- Yeni UI'daki bütün oyuncu metinleri mevcut 12 dilde `skills.*` anahtarlarıyla kayıtlıdır; dil değişikliği dinlenir. Mevcut font/fallback sistemi kullanılır.

## Kontroller ve dosyalar

- Tab: yetenek panelini aç/kapat. Esc: paneli kapat. Yetenek kutusunu seçip yükseltme düğmesine basılır.
- 2: Assemble; 3: Sort; 4: Shelf Guide; 5: Insight; 6: Autoshelving.
- 2–6 kutuları sol altta Jump göstergesinin hemen sağına aynı boyut/boşlukla sıralanır. Tuş numarası üstte, yerelleştirilmiş ad ve durum içeridedir. Aktif etki kutusu Jump ile aynı yeşil tonu kullanır.
- 1: mevcut Double Jump. Shift+6 mevcut geliştirme kayıt kısayoludur; yetenek tetiklemez.
- P (yalnız Unity Editor / Development Build): beş yeteneği geçici olarak en yüksek seviyede denemeyi aç/kapat. Tab paneli tutorial aşamasını beklemeden kullanılabilir. Gerçek seviyeler, puanlar ve süreler değişmez; test seviyeleri/süreleri kayda yazılmaz. Test sırasında yapılan kart yerleştirme gibi normal oyun işlemleri yine kaydedilir. Yeni oyun veya kayıt yükleme test modunu kapatır. P skill paneli açıkken de çalışır; diğer menülerde çalışmaz.
- `Assets/Resources/UI/Skills/SkillUI.prefab`: görev, alt yetenek göstergeleri ve modalın düzenlenebilir prefabı.
- `Assets/UI/Skills/Art`: özgün UI'lardan bağımsız kopyalar. Aynı dosyanın üstüne yazıp `.meta` dosyasını koruyarak değiştirilebilir.
- `Assets/Scripts/Skills`: seviye tabloları, ilerleme, yetenek işlemleri, geçici işaretçiler ve panel davranışı.
- `Assets/Resources/Localization/LocalizationTable.asset` ve `Assets/Scripts/Core/Localization/LocalizationKeys.cs`: çeviriler ve anahtarlar.
- Prefab oyun HUD'ına bir defa eklenir. Ana panel başta kapalıdır; prefab düzenlerken `Panel` nesnesini geçici olarak açarak düzen görülebilir.

## Uygulama güvenliği

- Tekrar ödül kazanmayı engellemek için tamamlanmış sıra kimlikleri kaydedilmeli.
- Otomatik yerleştirmede önce boş ve doğru slot doğrulanır, sonra kart elden çıkarılıp slot hemen rezerve edilir. Mevcut raf uçuşu kullanılır.
- Otomatik yerleştirme mevcut uçuş/yerleştirme ve save dirty akışını kullanmalı.
- Yeni oyun skill durumunu sıfırlamalı, kayıt yükleme mevcut verileri geri getirmeli.
- Eski kayıtların eksik skill alanları güvenli varsayılanlarla okunmalı.
- Rehber/parlama için her kare sahne taraması yapılmamalı; sınırlı güncelleme ve temizlenen görsel kaynaklar kullanılmalı.
- Insight, yer kartlarının eklenme/çıkarılma sayacını izler; etki açıkken sonradan yere düşen eşleşen kartı da bulur. Seri karşılaştırması, hiyerarşi sorgularından önce yapılır. Hedefi değişmeyen işaretler tekrar üretilmez; dolap işareti kart işaretlerinden bağımsız tutulur.
- Shelf Guide doğru slot dolu olsa da seçili kartın ait olduğu dolabı gösterir; Autoshelving yalnız doğru ve boş slota yerleştirir.
- Raf uçuşu başladığında kayıt değişikliği işaretlenir; varış beklenmeden çıkış kaydı alınabilir. Seviye artırırken süren bekleme yüklemede kısaltılmaz; kilitli yeteneğin kayıt süresi sıfırlanır.
- Skill paneli açıkken oyun girdileri engellenmeli; ESC mevcut pause menüsüyle aynı karede iki panel açmamalı.


## Kullanıcının oyun içinde kontrol edeceği akış

1. Yeni oyunda sol görev yazısı ve Tab erişimi ilk tutorial adımlarında kapalı olmalı; "Find the remaining cards..." aşamasında açılmalı. Ardından Tab/Esc ile paneli aç/kapat, ayarlardan dil değiştirince yeni yazıları kontrol et.
2. İki yatay rafı doğru tamamla; 1 puan kazan, istediğin yeteneği aç. Eski dolu kayıtta daha fazla puan görünmesi beklenir.
3. Açtığın yeteneği ilgili 2–6 tuşuyla kullan; uygun olmayan kart/boş el durumunda beklemenin başlamadığını kontrol et.
4. Kaydet/yeniden yükle: seviyeler, kalan süreler, puanlar ve elde sıralanan kart/paketlerin düzeni korunmalı. Yeni oyun ayrı, kilitli seviyelerle başlamalı.

Play Mode, build veya otomatik oyun testi çalıştırılmadı. Dosya bağlantıları, çeviri sayıları, prefab yapısı ve entegrasyon kod üzerinden incelendi.
