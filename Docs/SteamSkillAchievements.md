# Skill görevleri ve Steam başarımları

11 Ekim 2026 tarihinde **48 başarım Steamworks'te kaydedildi ve yapılandırma yayını başarıyla tamamlandı** (App ID **5125130**). Eski kart/paket hedefleri oyundaki gerçek raf hedefleriyle değiştirildi. Her başarımın 256×256 renkli simgesi ve ortak gri kilit simgesi yüklendi. Oyunun 12 dili ve Steam tarafında zaten açık olan Portekiz Portekizcesi dahil 13 dilde ad/açıklama mevcut. Bu işlem oyunun çıkışını başlatmadı; build, depot ve çıkış tarihi değiştirilmedi.

Bağlantı **Steamworks.NET 2025.164.1** kullanır; Standalone için `TCG_STEAMWORKS_NET` zaten etkindir. Başarım kurulumunda oyun davranışı korunmuştur. Sonraki Windows test hazırlığında sağlayıcıya yalnız `TCG_STEAM_TEST` tanımlı build'de çalışan tanılama eklendi; ayrıntılar aşağıdadır. 11 Ekim 2026'da kullanıcı Windows laptopta gerçek Steam hesabıyla ilk başarımı kazandı; yeniden açılışta aynı başarım Steam'den açık olarak okundu. Hesap başarımları sıfırlanmadı.

## Hangi görev hangi başarımı açıyor?

- Normal kartlarda doğru tamamlanan yatay raf bir görevdir; aynı kart serisi tekrar tamamlanınca ek görev kazanılmaz. TCG tarafında (kodda PSA) doğru tamamlanan bütün dolap bir görevdir ve aynı dolap tekrar sayılmaz.
- Oyun içindeki hedeflerle aynı eşikler kullanılır: **2–18 arasındaki her sayı**, sonra **21, 24, 27, 31, 35, 39, 44, 49, 55, 61, 68, 75, 83, 91, 100, 109, 119, 129, 140, 152, 165, 179, 194, 214, 239, 269, 309, 359, 409, 459, 509**.
- Toplam **48 başarım** vardır. API adları `SKILL_ROWS_002` … `SKILL_ROWS_509` biçimindedir; yalnız listelenen eşikler tanımlanır. Tam liste ve önerilen Türkçe/İngilizce adlar [SteamSkillAchievements.csv](SteamSkillAchievements.csv) içindedir.
- 13, 15 ve 17 raf görevleri de başarım açar; bu üç hedef ayrıca yükseltme puanı vermez. Yükseltme puanları `SkillCatalog.Milestones` tablosunu izlemeye devam eder.
- Skill satın almak veya kullanmak tek başına başarım açmaz. Başarımın kaynağı kaydedilen gerçek raf/dolap tamamlamalarıdır.
- Yeni Oyun, Steam hesabında kazanılmış başarımları geri almaz. Oyunun yeni kaydındaki görev ilerlemesi yine sıfırdan başlar. Eski kayıt yüklenince tamamlanmış hedefler Steam'e tekrar bildirilir; Steam'de zaten açık olanlar tekrar ödül oluşturmaz.

## Projede tamamlanan bağlantı kurulumu

Paket, projeye aşağıdaki sabit sürüm adresiyle eklendi; `manifest.json` ve `packages-lock.json` içinde kayıtlıdır:

   `https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1`

- Mevcut Standalone sembolleri korunarak `TCG_STEAMWORKS_NET` eklendi. Windows/Mac/Linux build'lerinde sağlayıcı derlenir; ayrı bir platform ayarıyla sembolleri değiştirirsen bu sembolü koru.
- **Ekstra SteamManager oluşturma**; `SteamSkillAchievementsProvider` Steam bağlantısını ve callback'leri yönetir. Sahneye elle nesne eklenmez.
- Proje kökündeki `steam_appid.txt` paket importundan sonra da **5125130** içeriyor. Bu, projede tanımlı tam oyun App ID'si. Demo ID'si **5144220**; bu entegrasyon demoda kapalıdır.
- Paketle gelen macOS native kütüphane Intel ve Apple Silicon mimarilerini içeriyor. Windows 64 bit ve Linux native kütüphaneleri de pakette var. Kaynak/API ve import ayarlarını incelemek, her platformda gerçek build çalıştırma testinin yerine geçmez.

Bu kurulum, Steamworks.NET'in [resmî kurulum adımlarını](https://steamworks.github.io/installation/) ve doğrulanan [2025.164.1 sürümünü](https://github.com/rlabrecque/Steamworks.NET/releases/tag/2025.164.1) kullanır. Native Steam kütüphanelerini de paket getirir; ayrı bir Web API anahtarı oyun koduna konulmaz.

## Steamworks'te tamamlananlar

- 48 API adı oyun kataloğuyla eşleştirildi; `Set By = Client`, `Hidden = false`. Sayısal Steam stat bağlantısı kullanılmıyor.
- 48 renkli simge ve ortak kilit simgesi yüklendi; ilk/son hedefler dahil görsel eşleşmeleri kontrol edildi.
- Steam'in kendi VDF şablonundaki token'lar kullanıldı. 13 dilde toplam **624 ad + 624 açıklama** yüklendi. Geri indirilen ilk tam çeviri dosyası kaynakla birebir karşılaştırıldı; ardından Rusça sayı dilbilgisi düzeltmesi 48 kayıt için ayrıca başarıyla kaydedildi. Başarım sayfası bütün dilleri tamamen yerelleştirilmiş gösterdi.
- Yayın öncesi farklarda yalnız `common` bölümündeki Korece dil işareti ve `stats` bölümündeki başarım değişiklikleri vardı. Yayın sonucu: **Publish to steam OK — Publishing successful!**
- Mağaza sayfasındaki **Steam Başarımları** özelliği açıldı ve yayınlandı. Mağaza farkı yalnız `category_22 = true` eklemesiydi; yayın sonucu **Successfully published!**, revizyon **10**. Diğer mağaza alanları değiştirilmedi.
- Yerel görseller, çeviri VDF'leri, token eşlemesi ve önceki metin yedekleri `output/steam-achievements/` altında. CSV bir referans tablosudur; doğrudan Steam içe aktarma biçimi değildir.

İleride API adlarını değiştirme. Görünen metin ve ikon değişikliklerini kaydettikten sonra Steamworks yapılandırmasını ayrıca yayınlamak gerekir. Commit/push tek başına Steam ayarlarını yayınlamaz. [Valve başarım kurulum rehberi](https://partner.steamgames.com/doc/features/achievements/ach_guide)

## Build üzerinde kontrol

Steam masaüstü istemcisi açık ve kullandığın hesabın oyun erişimi olmalı. Gerçek başarım kontrolü için bağımsız bir **tam oyun build'i** kullan; Editor Play Steam hesabına başarım göndermez. Steam'deki test/beta dalından başlatman yeterli. Steam dışından yerel build deneyeceksen `steam_appid.txt` test dosyasını ilgili çalıştırılabilir dosyanın yanına koy; müşteriye yayınladığın build'e bu dosyayı dahil etme. İlk iki görevi tamamlayıp `SKILL_ROWS_002` başarımının Steam'de açıldığını doğrula, sonra kaydı yükleyerek mükerrer ödül oluşmadığını kontrol et.

## Kayıt, bağlantı ve performans davranışı

Oyun kayıtları gerçek tamamlanma sayısını tutar. Steam sonradan hazır olursa mevcut ilerleme gönderilir. Bağlantı sağlandığında ayrıca App ID + Steam kullanıcı kimliğiyle ayrılmış bir yerel ilerleme kaydı tutulur. Burada “Steam onayladı” bilgisi saklanmaz; Steam'in durumu her uygulama açılışında tekrar okunur. Yerel kayıt bulunmayan başka bir bilgisayarda oyun kaydını yüklemek gerekli hedefleri yeniden oluşturur.

Gönderim başarısız olursa hedefler silinmez. İki dakikalık aralıklarla yeniden denenir. `SetAchievement` sonucunun true olması yükleme onayı sayılmaz; isim içeren başarılı `UserAchievementStored_t` callback'i beklenir. Yanlış App ID'de gönderim kapanır. Yeni Oyun veya beceri seviyesi sıfırlamak Steam başarımlarını silmez. [Valve API açıklaması](https://partner.steamgames.com/doc/api/ISteamUserStats#StoreStats)

Steam bağlantısı açıkken her karede yalnız Steam'in gerekli callback pompası ve zaman kontrolü çalışır; raf/kart taraması veya reflection yapılmaz. Başarım listesi sadece hedef değişince ve seyrek yeniden denemelerde işlenir. Editor ve demo bu bağlantıyı başlatmaz. Bu sağlayıcı Steam Cloud kurmaz; kayıtların cihazlar arasında taşınması ayrı Steam Cloud yapılandırmasıdır.

## Doğrulama ve kalan gerçek build testi

Üretimde kullanılan `SkillCatalog`, `SteamSkillAchievements` ve `SteamSkillAchievementsProvider` kaynakları, sahte Unity/Steam SDK sınırıyla izole derlendi: **116 kontrol geçti**. Katalogdaki 48 eşik, artan ilerleme, demo engeli, toplu gönderim, 1.000 karede gereksiz sorgu olmaması, bağlantının geç başlaması, iki dakikalık yeniden deneme, callback kaybı, yanlış App ID, hesaplar arasında izolasyon ve yeniden açılışta bekleyen ilerlemenin korunması kapsandı. Sonuç `output/steam-achievements/tests/results.txt` içinde.

Bu test gerçek Steam hizmetine bağlanmaz ve kullanıcının kayıtlarına dokunmaz. İlk denetimde bu bilgisayarda çalışan Steam istemcisi ve güncel tam oyun build'i yoktu. Daha sonra aşağıdaki Windows test paketi oluşturuldu; kullanıcının Windows laptopta gönderdiği iki test görüntüsü ilk başarımın kazanılmasını ve yeniden açılışta korunmasını doğruladı. İki farklı normal kart serisi veya uygun TCG dolabı tamamlayıp `SKILL_ROWS_002` onayını kontrol et; üçüncü görevde `SKILL_ROWS_003` açılmalı. Editor Play bu bağlantıyı bilerek başlatmaz.

Çalışma sonunda Unity ana araç çubuğu kontrol edildi; Editor **Play modunda bırakılmadı**.

## Windows üzerinde yerel test hazırlığı — 11 Ekim 2026

`TCG Card Chaos > Steam > Build Windows Achievement Test` menüsü, `Builds/SteamAchievementTest-Windows` dizinine ayrı bir Windows x64 Mono Development build oluşturur. Teste özel `TCG_STEAM_TEST` sembolü sadece bu build çağrısına verilir; proje genelindeki sembollere eklenmez. Normal sürümde tanılama çağrıları, mesaj oluşturma ve ek Steam sorguları derlemeden çıkarılır.

Paketin `TESTI_BASLAT.cmd` dosyası çalışma klasörünü ayarlar ve Unity çıktısını `SteamAchievementTest.log` dosyasına yönlendirir. Önceki çalıştırmanın logu korunur. `SONUCU_GOSTER.cmd` bağlantı, ilerleme ve onay satırlarını gösterir. `steam_appid.txt` sadece bu yerel test paketindedir; müşteriye yayınlanan depota eklenmemelidir. Test tam oyuna erişebilen Steam hesabıyla, Steam çevrimiçiyken yapılır. Bu işlem Steam'e build yüklemez.

Başlangıçta ilk iki başarım, oyun henüz herhangi bir `SetAchievement` çağrısı yapmadan okunur. `QUEUED`/`STORE_SENT` kayıt onayı değildir; `STORE_RESULT k_EResultOK` ve ilgili `STEAM_CONFIRMED` satırı beklenir. Sonraki çalıştırmada `STARTUP_STATE ... available=True unlocked=True` satırı önceki kazanımın Steam tarafından geri okunabildiğini gösterir. Çıkış öncesi kütüphane/topluluk görünümü sınırlıdır; [Valve test açıklaması](https://partner.steamgames.com/doc/features/achievements#Testing) uyarınca tanılama çıktısıyla kontrol edilir.

Tanılama eklemesinden sonra test derlemesinde **120**, normal derlemede **117** izole kontrol geçti. Normal derlemede ek log/başlangıç sorgusu olmadığı ve kuyruklama işleminin yanlışlıkla kayıt onayı olarak gösterilmediği ayrıca doğrulandı. Windows'taki gerçek Steam hesabında `SKILL_ROWS_002` kazanımı ve yeniden açılışta kalıcılığı doğrulandı; diğer 47 başarım tek tek oynanarak denenmedi. Kullanıcı adımları: [SteamAchievementWindowsTest.txt](SteamAchievementWindowsTest.txt).

Windows x64 build'i Unity'de **Success** ile tamamlandı (203 saniye, yaklaşık 2,3 GiB). Paket içindeki test tanılama metinleri, Windows `steam_api64.dll`, tam oyun App ID dosyası ve kullanım talimatları doğrulandı. Taşıma paketi: `Builds/SteamAchievementTest-Windows.zip`. Unity Play modunda bırakılmadı. Build sırasında Unity'nin yeniden yazdığı, önceden temiz olan dört font/render ayarı dosyası eski içeriklerine geri alındı.

## Gerçek Windows / Steam testi sonucu — 11 Ekim 2026

Kullanıcının ilk Windows ekran görüntüsünde tam oyun App ID `5125130` bağlantısı, `EARNED_PROGRESS total=2`, `STORE_RESULT k_EResultOK` ve `STEAM_CONFIRMED SKILL_ROWS_002` görüldü. İlk sorgunun `available=False` olması sonradan gelen başarılı kayıt onayını geçersiz kılmıyor.

Oyunu kapatıp yeniden açtıktan sonra gönderilen `IMG_1732.HEIC` fotoğrafında:

- `STARTUP_STATE SKILL_ROWS_002 available=True unlocked=True`
- `STARTUP_STATE SKILL_ROWS_003 available=True unlocked=False`
- `ALREADY_UNLOCKED SKILL_ROWS_002 read from Steam before setting it this session.`

Bu kanıtlar ilk başarımın kazanıldığını ve ikinci çalıştırmada yeniden gönderilmeden Steam'den açık olarak okunduğunu doğruluyor. Üç görev başarımı henüz kilitli; bu durum iki tamamlanma ile uyumlu. Temel gerçek hesap testi başarılıdır. Tüm 48 başarımın oyunla tek tek testi, başka cihaz senkronizasyonu ve Steam bildiriminin görsel olarak çıkması bu görüntülerle doğrulanmış değildir.
