# Independent Android / Flutter control

This project contains a Flutter animation, an increment button and a frame-timing heartbeat. It imports no Elifoot code and uses no REST, SignalR, storage or native plugins. It is an isolation control, not a replacement game client.

Build from this directory with the same Flutter/JDK/SDK used by Elifoot:

```powershell
flutter pub get
flutter build apk --profile --target-platform android-x64
```

From the repository root, with an online Android emulator:

```powershell
node scripts/run-android-isolation-case.mjs C-reference reference
```

The software-rendering launch uses both `enable-software-rendering=true` and `enable-impeller=false`. Successful compilation alone does not validate Flutter runtime. Require a visible reference screen and `QA004_REFERENCE frames=...` log messages with advancing counts; inspect ANR traces and system state separately.
