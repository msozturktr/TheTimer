# TheTimer  
**Zaman tutma, geri sayım ve gün içi planlama uygulaması**

## Ekran Görüntüleri  
![Zamanlayıcı](docs/screenshots/timer-running-pomodoro.png)  
![Plan](docs/screenshots/plan.png)  
![Bugün](docs/screenshots/today.png)  
![Rapor](docs/screenshots/report.png)

## Özellikler  
- Kronometre, geri sayım ve Pomodoro  
- Masaüstü bildirimleri (D‑Bus)  
- Tepsi ikonu (tray)  
- Çökme kurtarma  
- SQLite veri depolama (`~/.local/share/thetimer`)  
- Günlük planlama, yinelenen bloklar  
- Blok hatırlatıcıları  
- Plan vs. gerçekleşen karşılaştırması  
- Haftalık rapor  
- Astrolab koyu tasarım  

## Kurulum  

### Flatpak  
```bash
bash packaging/flatpak/build-flatpak.sh
flatpak run io.github.msozturktr.TheTimer
```

### AppImage  
```bash
bash packaging/appimage/build-appimage.sh
```

### Kaynak kodundan  
```bash
dotnet run --project src/TheTimer.App
```
*Gereksinim:* .NET 10 SDK  

## Geliştirme  

```bash
dotnet build          # Derleme
dotnet test           # Testler
dotnet run --project tools/TheTimer.Screenshots -- <outDir>   # Ekran görüntüsü aracı
```

## Teknoloji  
- **Dil/Çatı:** C# / .NET 10  
- **UI:** Avalonia 12  
- **MVVM:** CommunityToolkit.Mvvm  
- **Veri:** SQLite (`Microsoft.Data.Sqlite`)  
- **Bildirim:** Tmds.DBus.Protocol (freedesktop D‑Bus)  

## Lisans  
MIT lisansı. Kullanılan yazı tipleri (Bodoni Moda, Space Grotesk, JetBrains Mono) SIL OFL 1.1 altında dağıtılmaktadır.