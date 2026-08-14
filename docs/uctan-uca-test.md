# Uçtan Uca Test Dokümanı

## Proje Adı

Numune Kabul Dijitalleştirme Sistemi

## Test Amacı

Bu testin amacı, PDF tabanlı numune kabul formlarının sisteme yüklenmesinden başlayarak OCR işlemi, alan çıkarımı, manuel doğrulama, XML üretimi ve mock LIS/HBYS entegrasyonuna kadar olan sürecin uçtan uca çalıştığını doğrulamaktır.

## Test Ortamı

- İşletim Sistemi: Ubuntu
- Geliştirme Platformu: .NET
- Web Teknolojisi: ASP.NET Core Razor Pages
- Veritabanı: SQLite
- Alternatif Veritabanı Desteği: MSSQL Provider
- OCR Motoru: Tesseract OCR
- PDF Görüntüleme: PDF render / PNG dönüştürme
- Entegrasyon: Mock REST LIS/HBYS servisi
- Güvenlik: JWT Authentication ve Role Based Authorization
- Loglama: AuditLog ve Serilog dosya loglama

## Test Kullanıcıları

### Admin

Kullanıcı adı:

```text
admin