# Demo köpeği

`MainScene` içindeki `Shop Dog Area`, `full_release_card_1` köpeğinin modelini,
animasyonlarını ve davranışlarını kullanır. Yürüme, koşma, bekleme, yatma/uyuma,
uyanma ve oyuncuya yol verme davranışları korunmuştur. Köpek kayıt sistemine
girmez; her açılışta başlangıç noktasına yerleşir.

## Demo rotası

Köpek alt katta, camla kapalı demo alanının batı ve kuzey koridorlarında dolaşır.
Başlangıcı `(1.5, 0.06, -6)` konumudur. Yedi hedefin hepsi camların dışındadır.
Üst kat ve merdivenler bu demo rotasına dahil değildir.

Camların içindeki alan ayrıca yalnız köpeğin gezinme yüzeyinde engellenir:
`Navigation Exclusions` kutusu x=`3.88..14.3`, y=`-1..6`, z=`-14.3..-2.59`.
Bu kutu hem normal yolları hem oyuncudan uzaklaşma yollarını sınırlar. Oyuncu
ve kartların fizik katmanlarına veya cam collider'larına müdahale etmez.
İçindeki zemin doğrudan gezinme haritasından çıkarılır; sınıra köpeğin gövdesi
ve hesaplama hassasiyeti için ayrıca mesafe eklenir.

Gezinme yüzeyi yükleme tamamlandıktan sonra bir kez, köpeğe özel `Shop Dog`
agent türüyle hazırlanır. Yalnız geçerli ve birbirine bağlı zemin noktaları
kullanılır; yeterli güvenli nokta bulunamazsa köpek hareket ettirilmez.

## Sahneyi düzenlerken

`TCG Card Chaos > Shop Dog > Select Walking Area` menüsü alanı seçer.
Yeşil nokta başlangıcı, mavi noktalar hedefleri, kırmızı kutu yasak alanı gösterir.
Inspector'daki `Scene görünümünde noktaları taşı` ile hedefler taşınabilir.
Camların konumu değişirse yasak alan kutusu da yeni demo alanını kaplamalıdır.
Yasak kutunun üst yüzeyini gezinme sınırının üstünde tut; kutu gezilebilir bir
zemin değildir.

Yeni dolapların sabit, etkin ve trigger olmayan collider'ları köpeğin yol
hesabında engel sayılır. Sahne düzenlemeleri bir sonraki Play/açılışta hesaplanır;
oyun sırasında dolap taşıma için sürekli yeniden hesaplama yapılmaz.
