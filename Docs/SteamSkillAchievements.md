# Skill görevleri ve Steam başarımları

Oyun içindeki bağlantı kodu eklendi, **Steamworks.NET 2025.164.1 kuruldu ve Standalone için `TCG_STEAMWORKS_NET` etkinleştirildi.** Unity'de yeniden paket kurman veya sahneye bağlantı nesnesi eklemen gerekmiyor. Sende kalan işler aşağıdaki Steamworks başarım tanımlarını oluşturup yayınlamak ve Steam üzerinden tam oyun build'ini denemek. Steam hesabında gerçek açma/sıfırlama işlemi yapılmadı.

## Hangi görev hangi başarımı açıyor?

- Normal kartlarda doğru tamamlanan yatay raf bir görevdir. PSA tarafında doğru tamamlanan bütün dolap bir görevdir.
- Oyun içindeki hedeflerle aynı eşikler kullanılır: **2–18 arasındaki her sayı**, sonra **21, 24, 27, 31, 35, 39, 44, 49, 55, 61, 68, 75, 83, 91, 100, 109, 119, 129, 140, 152, 165, 179, 194, 214, 239, 269, 309, 359, 409, 459, 509**.
- Toplam **48 başarım** vardır. API adları `SKILL_ROWS_002` … `SKILL_ROWS_509` biçimindedir; yalnız listelenen eşikler tanımlanır. Tam liste ve önerilen Türkçe/İngilizce adlar [SteamSkillAchievements.csv](SteamSkillAchievements.csv) içindedir.
- 13, 15 ve 17 raf görevleri de başarım açar; bu üç hedef ayrıca yükseltme puanı vermez. Yükseltme puanları `SkillCatalog.Milestones` tablosunu izlemeye devam eder.
- Skill satın almak, kullanmak veya P ile tüm seviyeleri önizlemek tek başına başarım açmaz. Başarımın kaynağı kaydedilen gerçek raf/dolap tamamlamalarıdır. Test skill'leriyle gerçek raf tamamlarsan bu normal oyun ilerlemesidir.
- Yeni Oyun, Steam hesabında kazanılmış başarımları geri almaz. Oyunun yeni kaydındaki görev ilerlemesi yine sıfırdan başlar. Eski kayıt yüklenince tamamlanmış hedefler Steam'e tekrar bildirilir; Steam'de zaten açık olanlar tekrar ödül oluşturmaz.

## Projede tamamlanan bağlantı kurulumu

Paket, projeye aşağıdaki sabit sürüm adresiyle eklendi; `manifest.json` ve `packages-lock.json` içinde kayıtlıdır:

   `https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1`

- Mevcut Standalone sembolleri korunarak `TCG_STEAMWORKS_NET` eklendi. Windows/Mac/Linux build'lerinde sağlayıcı derlenir; ayrı bir platform ayarıyla sembolleri değiştirirsen bu sembolü koru.
- **Ekstra SteamManager oluşturma**; `SteamSkillAchievementsProvider` Steam bağlantısını ve callback'leri yönetir. Sahneye elle nesne eklenmez.
- Proje kökündeki `steam_appid.txt` paket importundan sonra da **5125130** içeriyor. Bu, projede tanımlı tam oyun App ID'si. Demo ID'si **5144220**; bu entegrasyon demoda kapalıdır.
- Paketle gelen macOS native kütüphane Intel ve Apple Silicon mimarilerini içeriyor. Windows 64 bit ve Linux native kütüphaneleri de pakette var. Kaynak/API ve import ayarlarını incelemek, her platformda gerçek build çalıştırma testinin yerine geçmez.

Bu kurulum, Steamworks.NET'in [resmî kurulum adımlarını](https://steamworks.github.io/installation/) ve doğrulanan [2025.164.1 sürümünü](https://github.com/rlabrecque/Steamworks.NET/releases/tag/2025.164.1) kullanır. Native Steam kütüphanelerini de paket getirir; ayrı bir Web API anahtarı oyun koduna konulmaz.

## Steamworks panelinde yapacakların

1. Steamworks'e giriş yap, **TCG Card Chaos / 5125130** uygulamasını seç. Demo uygulamasını seçme.
2. Uygulama yönetiminden **Stats & Achievements → Achievements** sayfasını aç.
3. CSV'deki 48 satır için başarım tanımları oluştur. **API Name** alanlarını büyük/küçük harf dahil birebir kopyala. CSV bir giriş/referans tablosudur; Valve'ın doğrudan içe aktarma şeması olduğu iddia edilmez.
4. Her biri için **Set By = Client**, **Hidden = false** kullan. Bu uygulama sayısal bir Steam stat'ı gerektirmeden hedef tamamlanınca ilgili başarımı doğrudan açar; bir progress-stat bağlantısı kurman gerekmez.
5. Görünen İngilizce ad/açıklamaları gir; Türkçe ve destekleyeceğin diğer Steam dilleri için yerelleştirmeleri ekle. CSV'deki görünen adları değiştirebilirsin; kodla eşleşen **API Name** değişmemeli. Başarımların ikonlarını da Steamworks'ün istediği alanlara yükle.
6. Değişiklikleri kaydet; ardından uygulamanın **Publish** bölümünde Steamworks yapılandırma değişikliklerini yayınla. Yalnız taslakta kalan tanımlar istemciye ulaşmaz.
7. Tam oyun build'ini bir test/beta dalına yükle, o dalı Steam kütüphanesinden seçip çalıştır. İki raf görevi tamamlandığında `SKILL_ROWS_002`, üçüncüde `SKILL_ROWS_003` açılmalı. Aynı eşikte normal raf ve tam PSA dolabı toplamı kullanılır.
8. Farklı bir Steam hesabına geçerken oyunu tamamen kapatıp yeniden aç. Yerel bekleyen ilerleme her Steam kullanıcı kimliği için ayrı tutulur.

Valve'ın [başarım kurulum rehberi](https://partner.steamgames.com/doc/features/achievements/ach_guide), uygulamaya ait başarımların backend'de tanımlanmasını ve istemci API'siyle açılmasını anlatır. Gerçek tanımların yayınlanması yalnız Steamworks hesabında yapılabilir; bu dosyaları commit/push etmek Steam yapılandırmasını yayınlamaz.

## Build üzerinde kontrol

Steam masaüstü istemcisi açık ve kullandığın hesabın oyun erişimi olmalı. Gerçek başarım kontrolü için bağımsız bir **tam oyun build'i** kullan; Editor Play Steam hesabına başarım göndermez. Steam'deki test/beta dalından başlatman yeterli. Steam dışından yerel build deneyeceksen `steam_appid.txt` test dosyasını ilgili çalıştırılabilir dosyanın yanına koy; müşteriye yayınladığın build'e bu dosyayı dahil etme. İlk iki görevi tamamlayıp `SKILL_ROWS_002` başarımının Steam'de açıldığını doğrula, sonra kaydı yükleyerek mükerrer ödül oluşmadığını kontrol et.

## Kayıt, bağlantı ve performans davranışı

Oyun kayıtları gerçek tamamlanma sayısını tutar. Steam sonradan hazır olursa mevcut ilerleme gönderilir. Bağlantı sağlandığında ayrıca App ID + Steam kullanıcı kimliğiyle ayrılmış bir yerel ilerleme kaydı tutulur. Burada “Steam onayladı” bilgisi saklanmaz; Steam'in durumu her uygulama açılışında tekrar okunur. Yerel kayıt bulunmayan başka bir bilgisayarda oyun kaydını yüklemek gerekli hedefleri yeniden oluşturur.

Gönderim başarısız olursa hedefler silinmez. İki dakikalık aralıklarla yeniden denenir. `SetAchievement` sonucunun true olması yükleme onayı sayılmaz; isim içeren başarılı `UserAchievementStored_t` callback'i beklenir. Yanlış App ID'de gönderim kapanır. Yeni Oyun veya beceri seviyesi sıfırlamak Steam başarımlarını silmez. [Valve API açıklaması](https://partner.steamgames.com/doc/api/ISteamUserStats#StoreStats)

Steam bağlantısı açıkken her karede yalnız Steam'in gerekli callback pompası ve zaman kontrolü çalışır; raf/kart taraması veya reflection yapılmaz. Başarım listesi sadece hedef değişince ve seyrek yeniden denemelerde işlenir. Editor ve demo bu bağlantıyı başlatmaz. Bu sağlayıcı Steam Cloud kurmaz; kayıtların cihazlar arasında taşınması ayrı Steam Cloud yapılandırmasıdır.

## Doğrulama sınırı

Paket kurulumu tamamlandı; kullanılan API'ler, assembly bağımlılıkları ve native dosyalar kurulan sürümün kaynaklarından kontrol edildi. Ayrı bir sahte Steam sağlayıcısıyla hata, bağlantı gecikmesi, callback kaybı ve yeniden deneme senaryoları geçti. Gerçek hesap erişimi, yayınlanmış 48 tanım ve Steam üzerinden uçtan uca açılma testi henüz yapılmadı. Başarımları yayında saymak için Steamworks tanımları ve gerçek build denemesi de tamamlanmalı.
