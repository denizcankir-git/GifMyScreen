# GifMyScreen — Developer Guide

## Proje Tanımı
Windows işletim sisteminde masaüstü arka planını video, GIF ve interaktif web sayfalarıyla değiştiren açık kaynaklı masaüstü motoru.

## Mimari ve Temel Kurallar
- **Çatı:** .NET 10 WPF (`net10.0-windows`, C# 12+)
- **Windows Forms Entegrasyonu:** System Tray (`NotifyIcon`) desteği için aktif (`<UseWindowsForms>true</UseWindowsForms>`).
- **Masaüstü Enjeksiyonu:** `DesktopManager` iki Explorer düzenini destekler. Her iki durumda da stil `SetParent`'tan önce `WS_CHILD` yapılır (`WS_POPUP` kesinlikle kaldırılmalıdır), konum fiziksel piksel olarak `SetWindowPos` ile verilir.
  - Klasik (Win10 / Win11 23H2-): Simgeleri barındıran üst düzey pencerenin arkasındaki kardeş `WorkerW`'ya bağlanır.
  - Yükseltilmiş masaüstü (Win11 24H2+): `SHELLDLL_DefView` ve `WorkerW` doğrudan `Progman`'ın çocuklarıdır. Pencere `Progman`'a bağlanır, `WS_EX_LAYERED` olur ve Z-sırasında `SHELLDLL_DefView` ile `WorkerW` arasına konur. Katmanlı çocuk pencere için `app.manifest` içindeki `supportedOS` bildirimi gereklidir. WPF'in `HwndTarget` katmanı `WS_EX_LAYERED`'ı her stil değişiminde temizlediği için `WallpaperWindow` içindeki `WM_STYLECHANGING` kancası bu mesajı sonuçlandırır (`handled = true`).
- **DPI:** Uygulama `app.manifest` ile Per-Monitor V2 DPI farkındadır. Explorer ile aynı fiziksel piksel uzayında çalışmak zorunludur; aksi halde farklı ölçekli monitörlerde koordinatlar sanallaştırılır ve gömülü pencere kayar.
- **WebView2 başlatma:** WebView2 kontrolü `Visibility="Hidden"` ile başlatılmamalıdır; kapsayıcı penceresi 0x0 kalır ve içerik görünmez.
- **Render Motoru:** `Microsoft.Web.WebView2` (Chromium).
  - Görseller ve GIF'ler CORS ve dosya yolu hatalarını önlemek için `base64` veri URI'si üzerinden yüklenir.
  - Videolar sanal ana bilgisayar eşlemesi (`SetVirtualHostNameToFolderMapping`) ile yürütülür.
- **Performans:** `PerformanceWatchdog` 500 ms aralıklarla ön plandaki pencerenin ana monitörü (fiziksel `rcMonitor` sınırları) tamamen kaplayıp kaplamadığını kontrol eder. Tam ekranda WebView2 önce gizlenir, sonra `TrySuspendAsync()` çağrılır; çıkışta `Resume()` ve yeniden görünür yapılır. `TrySuspendAsync()` görünür bir WebView2'de `ERROR_INVALID_STATE` (`0x8007139F`) ile başarısız olur, gizleme adımı zorunludur.
- **Çıkış:** Gömülü pencere kapanınca `DesktopManager.RefreshSystemWallpaper()` sistem duvar kağıdını kalıcı ayarlara dokunmadan yeniden uygular; aksi halde masaüstü siyah/son karede kalır.
- **Yapılandırma:** `%AppData%/GifMyScreen/config.json` dosyasında saklanır.

## Derleme ve Çalıştırma Komutları
- **Hata Ayıklama Derlemesi:** `dotnet build`
- **Uygulamayı Çalıştırma:** `dotnet run --project GifMyScreen`
- **Yayın Paketi (.exe):** 
  `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true`

## Kodlama Standartları
- P/Invoke bildirimleri `Core/Native/Win32.cs` altında tutulur.
- WebView2 geçiş anlarındaki COM istisnaları `try-catch` ile izole edilir; ancak bir istisnayı yutmadan önce nedeninin gerçekten geçici olduğu doğrulanmalıdır (örn. `0x8007139F` görünür WebView2'yi askıya almaya çalışmaktan kaynaklanır).
- Win32 değişiklikleri çalışma anında doğrulanmalıdır: pencere hiyerarşisi/stilleri (`GetWindowLong`, `GetParent`) ve ekran görüntüsü ile. Yalnızca derlemenin başarılı olması yeterli değildir.
- Kod açıklamaları ve commit mesajları sade ve açıklayıcı olmalıdır.