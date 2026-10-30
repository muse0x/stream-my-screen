# stream-my-screen 📺

minimalist screen mirroring receiver for windows because lonelyscreen is bloated and airserver costs money.

built with .net 9 + direct3d11. pure lan, no cloud, no bullshit.

---

### features

- **zero install on phone**: works natively with ios airplay. no random apks or third-party client apps required.
- **hardware accelerated**: direct3d11 decoding at smooth 60 fps with < 1% cpu usage.
- **lightweight**: ~45 mb ram. no electron bloatware eating 400 mb idle.
- **100% offline & local**: runs strictly on your local wi-fi subnet. zero cloud servers, zero external latency.
- **clean controls**: f11 fullscreen toggle, 90° rotation, hd screenshot capture, audio pass-through.

---

### install / run

#### option 1: windows installer (.exe)
grab the latest `StreamMyScreen-Setup-v1.0.0.exe` from the [releases](../../releases) tab. double-click, install, run.

#### option 2: portable / lazy way
clone this repo and double-click `runapp.bat`. it downloads the engine automatically and launches.

#### option 3: terminal way
```bash
dotnet run --project src/StreamMyScreen.App
```

---

### how to use (ios)

1. make sure your iphone/ipad and pc are on the same wi-fi network.
2. swipe down **Control Center** -> tap **Screen Mirroring**.
3. select `StreamMyScreen`.
4. done. instant 60 fps screen mirroring on your pc.

---

### license
mit. do whatever you want.
