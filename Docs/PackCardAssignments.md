# Sabit paket kartları

Unity'de `MainScene` açıkken **TCG Card Chaos > Pack Kart Atamalari** menüsünü aç.

1. Soldan `EN 001–100` veya `JP 001–010` paketini seç. **Paketi sahnede seç** düğmesi ilgili fiziksel paketi gösterir. Paketlerin sahnedeki nesne adları ve kayıt kimlikleri değişmez.
2. Beş alana kartları istediğin çıkış sırasıyla ata. **Kart ara / seç** diline uygun kartları ad, kimlik veya kategoriyle arar. `CardDefinition` dosyalarını alanlara sürüklemek de mümkündür.
3. Aynı kart başka bir alanda veya pakette kullanılıyorsa seçim reddedilir ve mevcut yeri gösterilir. Kartı taşımak için önce eski alanı boşalt. Dil uyumsuzluğu, eksik kart ve katalog dışında kart da uygulanamaz.
4. Tüm sorunlar giderildiğinde **Atamaları uygula ve yerdeki kopyaları kaldır** düğmesine bas. Değişecek yerdeki kart sayıları gösterilir. Seçilen kartların yerdeki örnekleri kaldırılır. Önceden yalnız pakette olan ve artık hiçbir pakete atanmayan kartlar boşalan zemin konumlarına geri konur. İşlem Undo ile bir bütün olarak geri alınabilir.
5. Sahneyi **Ctrl/Cmd+S** ile kaydet. Bu sahneyi içeren build tüm oyunculara aynı fiziksel paketten aynı beş kartı aynı sırada verir.

Taslak seçimler bu bilgisayarda `Library/PackCardAssignments.draft.json` dosyasına otomatik saklanır. Taslaklar build'e veya Git'e girmez. **Uygula** ve sahneyi kaydet adımları kalıcı oyun içeriğini oluşturur. Başlangıçtaki sahne içeriği değişmişse eski taslak otomatik uygulanmaz.

Mevcut sahnenin ilk kontrolünde 550 paket alanında 521 farklı kart ve 29 tekrar bulundu. Pencere bunları gösterir; yerine hangi kartların geleceğini tasarımcı seçer. Rastgele düzeltme yapılmaz. Tam sürüm kontrolü eksik/kapalı paketleri, tekrarlı kartları, eksik katalog kartlarını ve çakışan kayıt kimliklerini bildirir; bu sorunlar kalırsa build'i durdurur. Demo build'leri tam sürümün 110 paket şartından ayrıdır.

## Kayıt davranışı

- Kaydedilen paket; kalıcı kimliği, etiketi, dili ve sıralı kart kimlikleriyle geri yüklenir. Eksik kayıt rastgele kartlarla tamamlanmaz.
- Açılışın ortasında, beş kartın tamamı oluşmadan kayıt alınırsa orijinal paket kaydedilir. Beş kart oluştuktan sonra kayıt, kartların beşini de elde tutulan kartlar olarak içerir.
- Arka plandaki bir kayıt başladıktan sonra oluşan değişiklikler ayrıca kaydedilmeyi bekler. Çıkışta devam eden dosya yazımı tamamlanır, ardından güncel durum kaydedilir; eski kayıt işlemi yeni değişiklikleri kaydedilmiş saymaz.
- Yeni editör atamaları **yeni oyunlara** uygulanır. Eski save dosyaları kendi paket içeriklerini korur; geçmişte kaybolan veya çoğalan kartlar bu işlemle otomatik düzeltilmez. Eski bir kayıttaki beş geçerli kartın kendi içinde tekrarı varsa kayıt içeriği değiştirilmeden açılabilir; yeni atamalarda tekrar yasaktır.

## Elle kontrol

- Aynı kartı iki pakete, Japon kartını İngiliz paketine ve boş alan bırakılmış bir paketi uygulamaya çalış: uyarı verilmeli.
- Geçerli atamayı uygula, sahneyi kaydet, yeni oyunda seçilen paketleri aç: tam beş kart ve belirlediğin sıra gelmeli; aynı kart yerde bulunmamalı.
- Açılmamış paketi eldeyken kaydet/yükle, sonra aç. Açılış sırasında autosave/çıkış kaydı ve kartlar açıldıktan sonraki kaydı da kontrol et: paket veya beş kart korunmalı.
- Bir kartın paket atamasını başka bir kartla değiştirip yeniden uygula: eski kart yere dönmeli, yeni kartın yerdeki örneği kaldırılmalı.
