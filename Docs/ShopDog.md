# Dükkân köpeği

MainScene içindeki **Shop Dog Area**, alt katı, merdiveni ve üst kattaki U biçimli koridoru kapsar. Alanı görmek için **TCG Card Chaos > Shop Dog > Select Walking Area** menüsünü kullan. Mavi noktalar alt kat, turuncular üst kat, yeşil nokta başlangıçtır. Inspector'daki seçenekle noktalar Scene görünümünde taşınabilir; Undo desteklenir. Koordinatlar dünya koordinatıdır; alan kökünü taşımak yerine noktaları ve sınırları düzenle.

Köpek alt katta bir turdan sonra üst kata çıkar, orada bir tur atar ve alt kata döner. Her beş yolculuğun dördünde düz ve yeterince uzun bölümlerde koşmayı tercih eder; birinde düz zeminde de sakin yürür. İlk sakin yürüyüş üçüncü yolculuktadır; beşli sıra yürüyüşü katlar arasında da dağıtır. Bu oran yolculuk tercihidir, toplam hareket süresinin oranı değildir. Kat değiştirirken de düz yaklaşma koridorunda koşabilir, merdiven ve keskin dönüşlerde yürür. Başarıyla varılan hedefler sayılır; yarım kalan yolculuklar kat sırasını değiştirmez. Bekler, yatar, uyur ve kalkar. Aynı durakta tekrar tekrar uyumaz. Zıplama kaldırılmıştır. Ara sıra Voice hareketini yapar. Paket ses içermediğinden havlama sesi çalmaz. Oturma animasyonu da yoktur; Idle1 ve Idle2 ayakta bekleme pozlarıdır.

Görsel ölçek 4'tür; ilk eklenen köpeğin (ölçek 2) iki katı büyüklüğündedir. Köpeğin gezinme yarıçapı ve yüksekliği de bu boyuta göre artırıldı. Ölçek yalnız oluşturulurken atanır; yatarken tekrar değiştirilmez. Orijinal `Idle_sleep` ve `Sleep_loop` kliplerinde omurganın birbirine bağlı beş kemiği ölçeklenir; yatarken görülen gövde küçülmesi paketin kendi animasyonudur ve korunmuştur.

## Paket animasyonları

- Idle1, Idle2: iki bekleme hareketi.
- Walk, run: yürüme ve koşma.
- Idle_sleep, Sleep_loop: yatmaya geçiş ve uyuma. Kalkarken yatma geçişi ters oynatılır.
- Voice: ses çıkarma/havlama hareketi.
- Agro, Attack_1: saldırgan duruş ve saldırı.
- jump, jump_loop: zıplama ve havada hareket.
- Edle_eat, Eat_loop: yemek yemeye geçiş ve yemek yeme. `Edle_eat`, paketin kendi klip adıdır.

Agro, Attack_1, jump, jump_loop, Edle_eat ve Eat_loop dükkân davranışına bağlı değildir. Saldırı ve zıplama kullanılmaz; zıplama klibi sahnedeki köpeğin animasyon listesinden de çıkarılmıştır. Animasyonlara uygun hiyerarşiye sahip Dog_idle prefabı kullanılıyor.

Orijinal yürüyüş/koşu kliplerinin ilk ve son pozları uyuşmadığı için `ShopDogAnimationPostprocessor` yalnız `Dog_Walk.fbx` ve `Dog_Run.fbx` içe aktarılırken döngüyü kapatır. Gerçek içe aktarılmış eğri zamanları okunur; Walk'ın 18, run'ın 8 pozuna ilk poz eklenir, başlangıç/bitiş teğetleri ve quaternion sürekliliği düzenlenir. Süreler 0.600 ve 0.267 saniyedir. Walk'ın içe aktarma aralığı kapanış karesini kapsayacak şekilde uzatılmıştır. Orijinal FBX dosyaları, klip adları ve GUID referansları korunur; oyun sırasında klip kopyalama veya uyarıyla eski döngüye geri dönme yoktur.

Sakin yürüyüş 0.7 m/s, koşu 3.6 m/s'dir; önceki 2.4 m/s koşuya göre %50 artmıştır. Hızlanma/yavaşlama 5 m/s²'dir; koşarken köşeye 1.8 m kala yürüyüşe geçilir. Kaynak animasyonda, görsel ölçek 4 iken basılı ayağın hareketinden hesaplanan yaklaşık referans hızlar yürüyüş için 0.95 m/s, koşu için 4.6 m/s'dir. Oynatma hızı gerçek hareket hızının bu referansa oranını izler; bu nedenle koşu 3.6 m/s'de yaklaşık 0.78 hızında oynar. Köpek yavaşladığında ayaklar da yavaşlar, durduğunda zorunlu bir minimum döngü hızı uygulanmaz. Bu oranlar kaynak pozlardan hesaplanmıştır; görsel uyum kullanıcı tarafından kontrol edilmelidir.

## Alan ve performans

- Bağımsız Shop Dog gezinme türü kullanılır; varsayılan Humanoid ayarları korunur.
- Görsel hiyerarşi sahne yüklenirken hazırlanır. Oyunun yüklemesi tamamlandıktan sonra sabit çarpışma şekilleri bir kez toplanır, iş küçük gruplara bölünür ve gezinme yüzeyi en fazla iki iş parçacığıyla asenkron hazırlanır. Oyun yükleme ekranı köpeği beklemez.
- WorldCard katmanındaki binlerce kart/paket ilk sorguda hariç tutulur. Oyuncu, tetikleyiciler ve kartlara özel bitki şekilleri de kullanılmaz. Sahne collider'ları değiştirilmez.
- Zeminler, üst kat destekleri ve mevcut okunabilir merdiven mesh'i yürünebilir. Diğer sabit eşyalar engeldir. Okunamayan mesh'ler yalnız gezinme hesabında kendi yönlerini koruyan kutularla temsil edilir.
- Başlangıçta her noktanın aynı yüzeye kesintisiz bağlandığı kontrol edilir. Üst kat bağlı değilse ışınlanma yapılmaz; Console'da uyarı çıkar. Köpek güvenli noktalar arasında tam rotaları kullanır. Yolda takılırsa sürekli rota aramak yerine bekleyip daha sonra yeniden dener.
- Normal oyunda sahne taraması veya yüzey yenilemesi yoktur. Yeni hedef seçerken en fazla üç rota kontrolü yapılır ve hesaplanan rota doğrudan kullanılır; ikinci bir rota hesabı istenmez.
- Kamera döndüğünde animasyonu yeniden başlatan `Play/Sample` kaldırıldı. Bu tek köpeğin animasyon saati ekran dışında da devam eder; mesh çizimi ve skinning ekran dışında hâlâ cull edilir. Bu seçim ekran dışında kemik animasyonu maliyetini korur, ancak görünürlük değişince kesilen geçişlerin yeniden başlatılmasını önler.
- Ayrı bir görsel pivot, ön ve arka zemin yüksekliğine göre merdivenin eğimini izler. Ölçüm en fazla saniyede on kez iki NavMesh sorgusu yapar; dururken sorgu tekrarlanmaz. Açı yumuşatılır, sabitse hiyerarşiye tekrar yazılmaz. Gezinme kökü dik kalır; kemik animasyonları değiştirilmez.
- Yürüme/koşma seçimi saniyede on kez mevcut rota bilgisi ve önceden ölçülmüş geçerli eğimle yapılır. Ek fizik/NavMesh sorgusu yoktur. Farklı başlama/bırakma eşikleri ve kısa bekleme süresi, sınırda sürekli animasyon değişmesini önler. Zıplamanın tüm uygunluk sorguları kaldırılmıştır.
- Mevcut geliştirme performans kaydındaki `dogCPU`, `peakDog` ve `scriptAllocDogKB` köpeğin davranış/eğim kodunu ölçer. Bunlar Unity'nin kendi animasyon, skinning veya GPU süreleri değildir. Kayıtlardaki yüksek toplam çizim sayısı yalnız köpeğe bağlanmamalı: sahne kartları hâlihazırda ayrı, çoğunlukla iki materyalli renderer kullanır. Bu değişiklik kart render sistemini değiştirmez.
- Paylaşılan kayıtlarda otomatik kayıtların dosya hazırlığı da takılmalara denk geliyor. `GameSaveManager` kayıt görüntüsünü ana iş parçacığında tek seferde alır; değişmez veri kopyasının JSON'a çevrilmesi ve dosyaya yazılması mevcut arka plan görevinde yapılır. Dosya biçimi ve çıkışta devam eden kaydı bekleme korunur. Dünya verisini toplama süresi hâlâ ana iş parçacığındadır; bu değişiklik bütün kayıt takılmalarını ortadan kaldırdığı anlamına gelmez.
- Oyuncu köpeğin gövdesine çarpabilir. Sabit biçimli kapsül collider ve kinematik Rigidbody kullanılır; hareketi NavMeshAgent yönetir. Kapsül animasyon kemiklerine bağlanmaz, her kare mesh pişirilmez ve sahnenin fizik dönüşümleri topluca eşitlenmez. Kart/paket/PSA katmanı çarpışmadan dışlanır; collider Ignore Raycast katmanında olduğundan normal etkileşim ışınlarını engellemez. Oyuncuyla aynı Default katmanındaki sabit dekorlar fizik filtresinde yer alsa da köpeğin rotasını gezinme yüzeyi belirler.
- Köpek oyuncunun önüne doğru ilerliyorsa, mevcut oyuncu konumuna göre durup yolun açılmasını bekler; aynı rotayla devam eder. Kontrol saniyede on kez yalnız önbellekteki referanslar ve mesafe hesabıyla yapılır. Durma mesafesi koşu hızını, frenlemeyi ve iki kontrol arasında alınan yolu içerir. Beklerken takılı kalma süresi işlemez; dur/kalk eşikleri farklıdır. Oyuncunun katmanı veya sahnenin genel çarpışma matrisi değiştirilmez.
- Pause ve loading sırasında davranışlar durur. Yalnız bu alana ait gezinme verisi sahne kapanışında temizlenir. Köpek mevcut kart/kayıt sistemine dahil edilmez; her oyun oturumunda başlangıç noktasından yeniden başlar.

## Kullanıcının yapacağı kontrol

Unity, derleme veya otomatik test çalıştırılmadı.

1. MainScene'i aç, Play'e bas. Console'daki `[Shop Dog] Hazır` satırında üst kat nokta sayısının sıfırdan büyük olduğunu kontrol et. Uyarı varsa mesajı paylaş.
2. Birkaç dakika takip et: yürüyüşte ön bacağı, koşarken ayakların ilerleme hızıyla uyumunu, merdivende yürüyüşe dönüşü ve alt/üst kat sırasını kontrol et. Artık zıplamamalı. Uyku sırasındaki orijinal gövde deformasyonu korunur.
3. Köpeğe önden/yandan yürü; içinden geçmemeli. Önünde durup kenara çekil; köpek bekleyip yoluna devam etmeli. Yanında kart/paket bırak; eşyaları itmemeli. Yakınında sağa/sola bakarken takılmayı kontrol et.
4. ESC ile durdur/devam et. Kayıt yap, çık ve tekrar gir. Yeni `[Performance]` ve `[Save]` satırlarını paylaş; mümkünse Game penceresi tek görünürken aynı yerde dene.
