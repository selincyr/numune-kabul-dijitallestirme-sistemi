# Uçtan Uca Test Senaryoları

Bu doküman, Numune Kabul Dijitalleştirme Sistemi üzerinde gerçekleştirilen temel test senaryolarını içerir.

## Test Ortamı

- Uygulama: ASP.NET Core Razor Pages
- Veritabanı: SQLite
- OCR Motoru: Tesseract OCR
- PDF İşleme: PDF to Image dönüşümü
- Entegrasyon: Mock REST Integration Service

---

## Senaryo 1: PDF Yükleme

### Amaç

Kullanıcının sisteme PDF formatında numune kabul formu yükleyebilmesini test etmek.

### Adımlar

1. Uygulama başlatılır.
2. Üst menüden `PDF Yükle` sayfasına gidilir.
3. PDF formatında bir numune kabul formu seçilir.
4. Yükle butonuna basılır.

### Beklenen Sonuç

- PDF dosyası sisteme kaydedilmelidir.
- Belge veritabanına eklenmelidir.
- PDF sayfaları otomatik olarak PNG formatına dönüştürülmelidir.
- Kullanıcı belge detay sayfasına yönlendirilmelidir.
- Audit Log ekranında `PdfUpload` ve `PdfRender` kayıtları oluşmalıdır.

### Durum

Başarılı.

---

## Senaryo 2: PDF Önizleme

### Amaç

Yüklenen PDF belgesinin sistem üzerinde görüntülenebilmesini test etmek.

### Adımlar

1. Üst menüden `Belge Listesi` sayfasına gidilir.
2. Yüklenen belge seçilir.
3. Belge detay sayfası açılır.

### Beklenen Sonuç

- Belge bilgileri görüntülenmelidir.
- PDF önizleme alanında belge açılmalıdır.

### Durum

Başarılı.

---

## Senaryo 3: OCR İşlemi

### Amaç

PDF sayfalarından OCR ile metin çıkarılmasını test etmek.

### Adımlar

1. Belge detay sayfasına gidilir.
2. `OCR Başlat` butonuna basılır.
3. OCR işleminin tamamlanması beklenir.

### Beklenen Sonuç

- PDF sayfalarından OCR metni çıkarılmalıdır.
- OCR sonuçları veritabanına kaydedilmelidir.
- OCR sonucu belge detay sayfasında görüntülenmelidir.
- Audit Log ekranında `OCR` kaydı oluşmalıdır.

### Durum

Başarılı.

---

## Senaryo 4: Şablon Bazlı Alan Çıkarma

### Amaç

OCR metninin önceden tanımlanan form şablonuna göre yorumlanmasını ve beklenen alanların sınıflandırılmasını test etmek.

### Adımlar

1. OCR işlemi başlatılır.
2. Sistem varsayılan form şablonunu kontrol eder.
3. Şablona bağlı alanlar için regex/kural tabanlı alan çıkarma işlemi yapılır.

### Beklenen Sonuç

Aşağıdaki alanlar için çıkarım yapılmalıdır:

- T.C. Kimlik No
- Hasta Adı Soyadı
- Doğum Tarihi
- Cinsiyet
- Kurum
- Doktor
- Protokol No
- Numune Barkodu
- Numune Türü
- Test Adı
- Numune Kabul Tarihi
- Açıklama

Her alan için aşağıdaki bilgiler kaydedilmelidir:

- OCR değeri
- Düzeltilmiş değer
- Güven skoru
- Sayfa numarası
- Durum bilgisi

Durum bilgileri:

- Verified
- NeedsReview
- NotFound

### Durum

Başarılı.

---

## Senaryo 5: Manuel Doğrulama ve Düzeltme

### Amaç

OCR veya alan çıkarma sonucunda hatalı ya da eksik gelen değerlerin kullanıcı tarafından düzenlenebilmesini test etmek.

### Adımlar

1. Belge detay sayfasında `Çıkarılan Alanlar / Manuel Doğrulama` bölümü açılır.
2. Hatalı veya eksik alanlar düzenlenir.
3. `Düzeltmeleri Kaydet` butonuna basılır.

### Beklenen Sonuç

- Düzeltilmiş değerler veritabanına kaydedilmelidir.
- Değer girilen alanların durumu `Verified` olarak güncellenmelidir.
- Audit Log ekranında `ManualCorrection` kaydı oluşmalıdır.

### Durum

Başarılı.

---

## Senaryo 6: XML Üretimi

### Amaç

Doğrulanan alanların standart XML formatına dönüştürülmesini test etmek.

### Adımlar

1. OCR ve alan çıkarma işlemi tamamlanır.
2. Gerekli manuel düzeltmeler yapılır.
3. `XML Oluştur` butonuna basılır.

### Beklenen Sonuç

- XML içeriği oluşturulmalıdır.
- XML içeriği `XmlArchives` tablosuna kaydedilmelidir.
- XML çıktısı belge detay sayfasında görüntülenmelidir.
- Audit Log ekranında `XmlCreate` kaydı oluşmalıdır.

### Durum

Başarılı.

---

## Senaryo 7: XML Mapping

### Amaç

Çıkarılan alanların XML içinde standart bölümlere ayrılmış şekilde üretilmesini test etmek.

### Beklenen XML Bölümleri

- BelgeBilgileri
- HastaBilgileri
- KurumBilgileri
- NumuneBilgileri
- TestBilgileri
- AlanDetaylari

### Beklenen Sonuç

XML çıktısı, alanları yalnızca düz liste halinde değil; hasta, kurum, numune ve test bilgileri gibi anlamlı bölümler altında üretmelidir.

### Durum

Başarılı.

---

## Senaryo 8: Mock REST Entegrasyon

### Amaç

Oluşturulan XML içeriğinin mock entegrasyon servisine gönderilmesini test etmek.

### Adımlar

1. XML oluşturulur.
2. `Mock Servise Gönder` butonuna basılır.
3. Entegrasyon sonucu beklenir.

### Beklenen Sonuç

- Sistem son oluşturulan XML kaydını almalıdır.
- Mock REST servisine gönderim simüle edilmelidir.
- `IntegrationJobs` tablosuna kayıt atılmalıdır.
- Başarılı durumda kayıt durumu `Success` olmalıdır.
- Audit Log ekranında `IntegrationStart` ve `IntegrationSuccess` kayıtları oluşmalıdır.

### Durum

Başarılı.

---

## Senaryo 9: XML Oluşturmadan Mock Servise Gönderme

### Amaç

XML oluşturulmadan entegrasyon gönderimi yapılmak istendiğinde sistemin hata vermesini test etmek.

### Adımlar

1. XML kaydı olmayan bir belge detayına gidilir.
2. `Mock Servise Gönder` butonuna basılır.

### Beklenen Sonuç

- Sistem gönderimi engellemelidir.
- Kullanıcıya hata mesajı gösterilmelidir.
- Entegrasyon işlemi yapılmamalıdır.

### Durum

Başarılı.

---

## Senaryo 10: Audit Log Kontrolü

### Amaç

Sistemdeki önemli işlemlerin kayıt altına alındığını test etmek.

### Kontrol Edilen İşlemler

- PDF yükleme
- PDF sayfalarını PNG formatına dönüştürme
- OCR işlemi
- Manuel düzeltme
- XML oluşturma
- Mock entegrasyon gönderimi
- Belge silme

### Beklenen Sonuç

Audit Log ekranında işlem adı, açıklama, tarih ve kullanıcı bilgisi görüntülenmelidir.

### Durum

Başarılı.

---

## Genel Sonuç

Yapılan testler sonucunda sistemin temel MVP akışı başarıyla doğrulanmıştır.

Tamamlanan uçtan uca akış:

PDF yükleme → PDF görüntüleme → OCR → Şablon bazlı alan çıkarma → Manuel doğrulama → XML üretimi → XML arşivleme → Mock REST entegrasyon → Audit Log

Koordinat bazlı PDF üzerinde işaretleme ve rol bazlı kullanıcı yönetimi sonraki geliştirme aşaması olarak planlanmıştır.