# GifMyScreen — Developer Guide

## Proje Tanımı
Windows işletim sisteminde masaüstü arka planını video, GIF ve interaktif web sayfalarıyla değiştiren açık kaynaklı masaüstü motoru.

## Mimari ve Temel Kurallar
- **Çatı:** .NET 8.0 / 9.0 WPF (C# 12+)
- **Windows Forms Entegrasyonu:** System Tray (`NotifyIcon`) desteği için aktif (`<UseWindowsForms>true</UseWindowsForms>`).
- **Masaüstü Enjeksiyonu:** `WorkerW` katmanına `SetParent` ile enjekte edilir. Pencere stili `WS_CHILD` olmalıdır (`WS_POPUP` kesinlikle kaldırılmalıdır).
- **Render Motoru:** `Microsoft.Web.WebView2` (Chromium).
  - Görseller ve GIF'ler CORS ve dosya yolu hatalarını önlemek için `base64` veri URI'si üzerinden yüklenir.
  - Videolar sanal ana bilgisayar eşlemesi (`SetVirtualHostNameToFolderMapping`) ile yürütülür.
- **Performans:** `PerformanceWatchdog` 500 ms aralıklarla tam ekran pencereleri kontrol eder; tam ekranda `TrySuspendAsync()`, çıkışta `Resume()` çağrılır.
- **Yapılandırma:** `%AppData%/GifMyScreen/config.json` dosyasında saklanır.

## Derleme ve Çalıştırma Komutları
- **Hata Ayıklama Derlemesi:** `dotnet build`
- **Uygulamayı Çalıştırma:** `dotnet run --project GifMyScreen`
- **Yayın Paketi (.exe):** 
  `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true`

## Kodlama Standartları
- P/Invoke bildirimleri `Core/Native/Win32.cs` altında tutulur.
- COM istisnaları (`COMException 0x8007139F`) WebView2 geçiş anlarında `try-catch` ile izole edilmelidir.
- Kod açıklamaları ve commit mesajları sade ve açıklayıcı olmalıdır.