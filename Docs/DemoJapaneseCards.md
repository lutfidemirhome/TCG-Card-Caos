# Demo için Japon kartları ve dolapları

Kaynak: `full_release_card_1`, `987618fc928ab37b935c83f0bd80641759571e4c`.
Yalnız normal Japon kartları ve bunların dolapları aktarıldı. Japon paketleri,
Japon PSA kartları/dolapları ve kaynak sahne yerleşimleri aktarılmadı.

## İçerik

| Kategori | Kart sayısı | Satır başına yuva |
| --- | ---: | ---: |
| Dragon Epic Japan | 50 | 5 |
| Fire Mythic Gold Japan | 30 | 3 |
| Ground Master Art Japan | 30 | 3 |
| Ice Prism Elite Japan | 50 | 5 |
| Psychic Elite Japan | 100 | 10 |

Dolap prefabları: `Assets/Prefabs/Cabinets/Cabinets_Japanese/`.
260 kart tanımı: `Assets/Resources/Cards/Definitions/Japanese/`.
Kart ön yüzleri: `Assets/Art/Cards/Japanese/`.
Ortak Japon arka yüzü: `Assets/Resources/Cards/JapaneseCardBack/`.
Özgün dosyalar ve `.meta` kimlikleri korunmuştur.

Normal kartların katalog, yuva/seri ve kayıt kimliği sistemi kullanılır.
Japon arka yüzleri hem elde/rafta hem yerdeki toplu çizimde ayrı materyal
kullanır. Mevcut İngilizce paket havuzları ve İngilizce Mix üretimi Japon
kartlarını seçmez. Kartların mevcut ışıklandırması korunmuştur.

## Dört dolap için fizik yerleşimi

Kullanıcı Dragon, Fire, Ground ve Ice dolaplarını demo alanına yerleştirdi.
100 kartlık Psychic dolabı demo hedefine dahil değildir. Diğer dört dolabın
160 normal kartı `MainScene` içinde şu grupta hazırlanmıştır:

`Physics_Card_Level / Demo_Area / Demo_Cards / Demo_Japanese_Cards`

Kartlar `AreaKind.Demo` ve benzersiz kayıt kimlikleriyle oluşturulur.
Yeni grup camların içinde, dolaplardan ve balkondan uzaktaki
X=`6.5..10.5`, Z=`-10.5..-5`, Y=`1.2..2.6` hacminde yukarıda bekler.
Konumlar ve yatay dönüşler rastgeledir; kartlar bir ızgara üzerinde dizilmez.
Başlangıçta tam 144 kartın önü, 16 kartın arkası yukarı bakar. Kartların
başlangıçta iç içe girmemesi için üç boyutlu mesafe korunur; düşerken üst üste
gelebilirler. Küçük başlangıç eğimleri yüz yönünü korumaya yardımcı olur,
ancak fizik çarpışmaları bazı kartları çevirebilir.
Mevcut demo kartlarının yerleşimi korunur; fizik düşürme ve son Bake
kullanıcı tarafından yapılır.

1. Play kapalıyken `TCG Card Chaos > Demo Japanese Cards > Grabbit Fall Japanese Only` seç.
2. Fare Scene penceresindeyken **sol Shift** basılı tut. Kartlar yere oturana kadar bekle; bırakınca fizik duraklar.
3. Grabbit penceresinde **Exit Grabbit** seç.
4. `TCG Card Chaos > Demo Japanese Cards > Bake Japanese Only` seç.
5. Sahneyi `Cmd+S` / `Ctrl+S` ile kaydet.

Aynı kontroller Card Physics Level Builder içindeki **Demo Japanese Cards —
160 Normal Cards** bölümünde de bulunur. `Create 160 Normal Cards` yalnız eksik
kartları ekler; tekrar çalıştırmak mevcut kartları çoğaltmaz veya taşımaz.
Yalnız Japon grubunu yeniden yukarı alıp karıştırmak için aynı menüdeki
`Reshuffle Above Demo - 90 Percent Face Up` kullanılabilir. Bu işlem mevcut
160 kartın kimliklerini korur; yeni kopyalar üretmez. Sonrasında yine Fall,
Exit Grabbit, Bake ve sahneyi kaydetme adımlarını uygula.
`Generate Demo Cards` / `Delete Demo Cards` bu işlem için kullanılmamalıdır;
bunlar mevcut demo içeriğini yeniden üretir/siler.

İlerleme hedefi artık 10 dolap ve 424 karttır: 235 İngilizce yer kartı +
160 Japonca yer kartı + 4 PSA + mevcut 5 paketin içindeki 25 kart.
Japonca kart sayısı `PhysicsLevelLayout.DemoJapaneseRegularCount` alanında
ayrı tutulur; İngilizce kart/paket üretim havuzu değişmez.
