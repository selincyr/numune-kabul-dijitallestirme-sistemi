# Numune Kabul Dijitalleştirme Sistemi

## Proje Hakkında

Numune Kabul Dijitalleştirme Sistemi, PDF formatındaki numune kabul formlarının dijital ortama aktarılması amacıyla geliştirilmiş bir web uygulamasıdır.

Sistem; PDF yükleme, PDF sayfalarını görüntüye dönüştürme, OCR işlemi, şablon bazlı alan çıkarımı, manuel doğrulama, XML üretimi ve mock LIS/HBYS entegrasyonu süreçlerini kapsamaktadır.

Bu proje MVP olarak geliştirilmiştir. Amaç, farklı kurumlara ait numune kabul formlarının dijitalleştirilmesi ve doğrulanmış verilerin XML formatında entegrasyona hazır hale getirilmesidir.

## Temel Özellikler

- PDF yükleme
- PDF listeleme, görüntüleme ve silme
- PDF sayfalarını PNG formatına dönüştürme
- Tesseract OCR ile metin çıkarma
- Ham OCR metnini veritabanında saklama
- Kurum yönetimi
- Form şablonu yönetimi
- Şablona özel alan ve regex kuralı tanımlama
- Şablon bazlı alan çıkarımı
- Güven skoru hesaplama
- Alan koordinatlarını saklama
- PDF üzerinde renkli alan işaretleme
- İşaretli alana tıklayarak manuel düzeltme alanına yönlenme
- Manuel alan düzeltme
- AuditLog kaydı
- XML oluşturma
- XML arşivleme
- XML içinde OCR metni, düzeltilmiş alanlar, güven skorları ve koordinatlar
- Mock LIS/HBYS REST entegrasyonu
- IntegrationJobs ile entegrasyon takibi
- Başarısız entegrasyonlar için yeniden gönderim
- REST API desteği
- JWT Authentication
- Role Based Authorization
- MSSQL provider desteği
- Serilog dosya loglama
- Uçtan uca test dokümanı

## Kullanılan Teknolojiler

- ASP.NET Core Razor Pages
- ASP.NET Core Web API
- Entity Framework Core
- SQLite
- MSSQL Provider
- Tesseract OCR
- PDF render / PNG dönüştürme
- XML
- JWT Authentication
- Role Based Authorization
- Serilog
- Git / GitHub

## Proje Yapısı

```text
src/
  NumuneKabul.Domain
  NumuneKabul.Application
  NumuneKabul.Infrastructure
  NumuneKabul.Web

docs/
  uctan-uca-test.md
```

## Veritabanı Modelleri

Projede kullanılan temel tablolar:

- Users
- AppUsers
- Institutions
- FormTemplates
- TemplateFields
- PdfDocuments
- OcrResults
- ExtractedFields
- XmlArchives
- IntegrationJobs
- AuditLogs

## Kullanıcı Rolleri

Sistemde üç temel rol bulunmaktadır.

### Admin

Admin rolü sistem yönetimi için kullanılır.

Yetkileri:

- Kurum yönetimi
- Şablon yönetimi
- PDF/OCR/XML işlemleri
- Entegrasyon API erişimi
- Kullanıcı ve rol bazlı API erişimi

### Personnel

Numune kabul personeli rolüdür.

Yetkileri:

- PDF işlemleri
- OCR işlemleri
- Alan doğrulama
- XML oluşturma

Bu rol entegrasyon API’sine erişemez.

### IntegrationService

Entegrasyon servisi rolüdür.

Yetkileri:

- Entegrasyon durumu görüntüleme
- XML entegrasyon gönderimi

Bu rol PDF/OCR/Fields/XML API’lerine erişemez.

## Test Kullanıcıları

Geliştirme ortamında test amacıyla aşağıdaki kullanıcılar oluşturulabilir.

Test kullanıcılarını oluşturmak için:

```bash
curl -X POST http://localhost:5195/api/auth/seed-test-users
```

### Admin

```text
Kullanıcı adı: admin
Şifre: admin123
Rol: Admin
```

### Personel

```text
Kullanıcı adı: personel
Şifre: personel123
Rol: Personnel
```

### Entegrasyon Servisi

```text
Kullanıcı adı: entegrasyon
Şifre: entegrasyon123
Rol: IntegrationService
```

> Bu kullanıcılar yalnızca geliştirme ve test ortamı içindir. Canlı ortamda değiştirilmelidir.

## Kurulum

Projeyi klonlayın:

```bash
git clone https://github.com/selincyr/numune-kabul-dijitallestirme-sistemi.git
```

Proje klasörüne girin:

```bash
cd numune-kabul-dijitallestirme-sistemi
```

Bağımlılıkları yükleyin:

```bash
dotnet restore
```

Projeyi derleyin:

```bash
dotnet build
```

## Veritabanını Oluşturma

SQLite varsayılan veritabanı olarak kullanılmaktadır.

Migration uygulamak için:

```bash
dotnet ef database update \
  --project src/NumuneKabul.Infrastructure \
  --startup-project src/NumuneKabul.Web \
  --context AppDbContext
```

## Uygulamayı Çalıştırma

```bash
dotnet run --project src/NumuneKabul.Web
```

Geliştirme ortamında uygulama aşağıdaki adresten açılabilir:

```text
http://localhost:5195
```

## Veritabanı Provider Seçimi

`appsettings.json` içinde veritabanı provider seçimi yapılabilir.

Varsayılan ayar:

```json
"DatabaseProvider": "Sqlite"
```

SQLite bağlantısı:

```json
"DefaultConnection": "Data Source=|DataDirectory|/numunekabul.db"
```

MSSQL bağlantısı:

```json
"SqlServerConnection": "Server=localhost,1433;Database=NumuneKabulDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;"
```

MSSQL kullanmak için:

```json
"DatabaseProvider": "SqlServer"
```

olarak değiştirilmelidir.

## JWT Authentication

Login endpointi:

```bash
curl -X POST http://localhost:5195/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"admin","password":"admin123"}'
```

Başarılı giriş sonucunda JWT token döner.

Token ile API isteği örneği:

```bash
curl -i http://localhost:5195/api/pdf/5 \
  -H "Authorization: Bearer TOKEN"
```

## API Endpointleri

### Auth API

```text
POST /api/auth/seed-test-users
POST /api/auth/login
```

### PDF API

```text
POST /api/pdf/upload
GET /api/pdf/{id}
DELETE /api/pdf/{id}
```

Yetki:

```text
Admin
Personnel
```

### OCR API

```text
POST /api/ocr/start/{id}
GET /api/ocr/result/{id}
```

Yetki:

```text
Admin
Personnel
```

### Fields API

```text
GET /api/fields/{id}
PUT /api/fields/{id}
```

Yetki:

```text
Admin
Personnel
```

### XML API

```text
POST /api/xml/create/{id}
GET /api/xml/{id}
```

Yetki:

```text
Admin
Personnel
```

### Integration API

```text
POST /api/integration/send/{id}
GET /api/integration/status/{id}
```

Yetki:

```text
Admin
IntegrationService
```

## Role Based Authorization Testleri

Tokensız API isteği:

```bash
curl -i http://localhost:5195/api/pdf/5
```

Beklenen sonuç:

```text
HTTP/1.1 401 Unauthorized
```

Admin token ile PDF API isteği:

```bash
curl -i http://localhost:5195/api/pdf/5 \
  -H "Authorization: Bearer ADMIN_TOKEN"
```

Beklenen sonuç:

```text
HTTP/1.1 200 OK
```

Personel token ile entegrasyon API isteği:

```bash
curl -i http://localhost:5195/api/integration/status/5 \
  -H "Authorization: Bearer PERSONEL_TOKEN"
```

Beklenen sonuç:

```text
HTTP/1.1 403 Forbidden
```

Entegrasyon servisi token ile entegrasyon API isteği:

```bash
curl -i http://localhost:5195/api/integration/status/5 \
  -H "Authorization: Bearer ENT_TOKEN"
```

Beklenen sonuç:

```text
HTTP/1.1 200 OK
```

## Uygulama İş Akışı

Sistem aşağıdaki uçtan uca akışla çalışmaktadır:

1. Kullanıcı PDF dosyasını yükler.
2. PDF bir kuruma bağlanır.
3. PDF için form şablonu seçilir.
4. PDF sayfaları PNG formatına dönüştürülür.
5. OCR işlemi çalıştırılır.
6. Ham OCR metni veritabanına kaydedilir.
7. Seçili şablonun regex kuralları uygulanır.
8. Alanlar otomatik olarak çıkarılır.
9. Güven skorları hesaplanır.
10. Alan koordinatları saklanır.
11. Alanlar PDF üzerinde renkli kutularla gösterilir.
12. Kullanıcı gerekli alanları manuel olarak düzeltebilir.
13. Manuel düzeltmeler AuditLog ile kaydedilir.
14. XML oluşturulur.
15. XML XmlArchives tablosuna kaydedilir.
16. XML mock LIS/HBYS servisine gönderilir.
17. Entegrasyon sonucu IntegrationJobs tablosunda takip edilir.
18. Gerekirse yeniden gönderim yapılır.

## Kurum Yönetimi

Kurum yönetimi ekranında aşağıdaki işlemler yapılabilir:

- Kurum listeleme
- Yeni kurum oluşturma
- Kurum düzenleme
- Kurum silme
- Bağlı belge veya şablon bulunan kurumun silinmesini engelleme

## Şablon Yönetimi

Şablon yönetimi ekranında aşağıdaki işlemler yapılabilir:

- Şablon listeleme
- Yeni şablon oluşturma
- Şablon oluştururken kurum seçme
- Şablon detay görüntüleme
- Şablon silme
- Şablona alan ekleme
- Şablon alanı düzenleme
- Şablon alanı silme

Şablon alanları aşağıdaki bilgileri içermektedir:

- Alan adı
- Anahtar kelime
- Regex kuralı
- Zorunlu alan bilgisi
- Veri tipi
- Sıra numarası

## PDF Üzerinde Alan İşaretleme

OCR sonrası çıkarılan alanlar PDF görüntüsü üzerinde renkli kutular ile gösterilir.

Renk anlamları:

```text
Yeşil: Bulunan / doğrulanan alan
Sarı: Kontrol gerektiren alan
Kırmızı: Bulunamayan alan
```

Kullanıcı işaretli alana tıkladığında ilgili manuel düzeltme alanına yönlendirilir.

## XML Üretimi

XML çıktısı aşağıdaki bilgileri içerir:

- Belge bilgileri
- Kurum bilgileri
- Ham OCR metni
- Çıkarılan alanlar
- Düzeltilmiş alan değerleri
- Güven skorları
- Sayfa numarası
- Koordinat bilgileri

Oluşturulan XML kayıtları `XmlArchives` tablosunda saklanır.

## Mock LIS/HBYS Entegrasyonu

Mock entegrasyon akışı aşağıdaki işlemleri kapsar:

- XML gönderimi
- Entegrasyon sonucu kaydı
- Başarılı/başarısız durum takibi
- Hata mesajı saklama
- Retry / yeniden gönderim

Entegrasyon kayıtları `IntegrationJobs` tablosunda tutulur.

## Serilog Dosya Loglama

Projede Serilog dosya loglama desteği eklenmiştir.

Log dosyaları aşağıdaki klasörde oluşur:

```text
src/NumuneKabul.Web/Logs
```

Log dosyaları günlük olarak oluşturulur:

```text
numunekabul-YYYYMMDD.log
```

Log dosyaları Git takibine alınmamalıdır.

`.gitignore` içinde aşağıdaki kayıtlar bulunmalıdır:

```gitignore
Logs/
src/NumuneKabul.Web/Logs/
*.log
```

## Uçtan Uca Test Dokümanı

Uçtan uca test adımları aşağıdaki dosyada açıklanmıştır:

```text
docs/uctan-uca-test.md
```

Bu dokümanda aşağıdaki işlemler test edilmiştir:

- JWT login
- Role Based Authorization
- Kurum yönetimi
- Şablon yönetimi
- PDF yükleme
- OCR
- Alan çıkarma
- Manuel düzeltme
- XML oluşturma
- Mock entegrasyon
- MSSQL provider desteği
- Serilog dosya loglama

## Bilinen Sınırlamalar

MVP sürümünde aşağıdaki sınırlamalar bulunmaktadır:

- El yazısı tanıma bulunmamaktadır.
- Yapay zekâ veya LLM tabanlı alan çıkarımı kullanılmamaktadır.
- Gerçek LIS/HBYS bağlantısı yerine mock REST servis kullanılmaktadır.
- HL7/FHIR desteği bulunmamaktadır.
- OCR başarısı PDF görüntü kalitesine bağlıdır.
- Regex kurallarının farklı form yapıları için şablon bazında tanımlanması gerekmektedir.
- Gerçek canlı ortam güvenlik yapılandırması ayrıca yapılmalıdır.

## Gelecek Geliştirmeler

İlerleyen sürümlerde aşağıdaki özellikler eklenebilir:

- Gerçek LIS/HBYS entegrasyonu
- HL7/FHIR desteği
- Çok sayfalı gelişmiş OCR işleme
- Otomatik form tipi tanıma
- Barkod ve QR kod okuma
- El yazısı tanıma
- LLM tabanlı alan çıkarımı
- Şablonsuz belge analizi
- Gelişmiş raporlama
- Dashboard
- Kullanıcı yönetim ekranı
- Merkezi hata ve log yönetimi

## Sonuç

Numune Kabul Dijitalleştirme Sistemi, PDF formatındaki numune kabul formlarını dijitalleştirmek için geliştirilmiş bir MVP uygulamasıdır.

Sistem; PDF yükleme, OCR, şablon bazlı alan çıkarımı, manuel doğrulama, XML üretimi, mock LIS/HBYS entegrasyonu, JWT Authentication, Role Based Authorization, MSSQL provider desteği ve Serilog dosya loglama özelliklerini içermektedir.

Uçtan uca testler sonucunda sistemin temel MVP gereksinimlerini karşıladığı doğrulanmıştır.