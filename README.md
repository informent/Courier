# Courier

Courier sends files directly between devices on the same network. It has no account, cloud upload, or required third-party service.

## Current workflow

1. On the receiving device, choose a folder and start receiving.
2. Select and copy one complete pairing code from the receiving panel. Share it privately with the sender.
3. On the sending device, choose a file and paste that pairing code.
4. The receiver reviews the filename, size, and destination, then accepts or declines within 20 seconds. No file is written before acceptance.
5. Courier transfers the file directly and verifies it with SHA-256 before reporting success. Use Cancel sending or Stop receiving to interrupt a transfer.

This Windows preview requires both devices to connect over the local network. Windows Firewall may ask for permission. Transfers use TLS with the receiver's SHA-256 certificate fingerprint pinned in the pairing code. A random session secret is checked before any file metadata is accepted. There is no plaintext fallback and no certificate is installed in your trust store. Browser and phone support are not implemented yet.

Keep pairing codes private. Anyone with the current code can send one file; this does not prove their personal identity. Each receiving session creates a fresh certificate and secret. When several network addresses are listed, use the code for the network shared by both devices. A loopback address (`127.0.0.1`) works only on the same PC. Both peers must run this updated protocol; old `IP:port` codes are rejected.

Version 1.0.0 adds cryptographically anchored transfer receipts and bounded local history. After confirmed delivery, Courier records direction, UTC completion time, filename, byte count, and SHA-256. Pairing codes, secrets, certificates, file contents, and peer addresses are never written to receipt history. The newest 100 receipts are retained under the current user's local application data.

Validation includes encrypted loopback transfers, wrong certificate and secret rejection, malformed codes, unsafe filenames, tampered/truncated data, overwrite protection, decline without disk writes, and cancellation cleanup. This is not an independent security audit or proof of two-device network compatibility. Browser/phone support and a full UI interaction review remain future work.

Incoming files are staged until their hash is verified. Existing files are never overwritten, and incomplete transfers are removed. The sender reports success only after the receiver confirms verification and saving. This protocol requires both peers to use the updated version.

Courier is not affiliated with Apple, Microsoft, Google, or any cloud provider.

## Diagnostics and validation

Startup and failure diagnostics are written to `Downloads\GITHUB\Courier\Logs` under your Windows user profile. Logs record a timestamp, operation, exception type, and error code, not pairing secrets, filenames, exception messages, or file contents. Logging failures do not prevent the app from opening.

The test suite includes a 256 MiB encrypted transfer to a separate receiver process, Unicode filenames, locked sources, blocked destinations, and safety regressions. `tests/UiSmoke.ps1` operates two packaged app instances and checks acceptance, decline, and approval expiry without manual input. It creates uniquely named test files and removes only those fixtures afterward. See [VALIDATION.md](VALIDATION.md) for the verification scope and remaining limits.

## Build and test

```powershell
dotnet run --project tests/TransferTests.csproj
dotnet build Courier.csproj -c Release
dotnet publish Courier.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

After publishing, run the packaged UI tests from an unlocked Windows desktop:

```powershell
foreach ($decision in @('Accept', 'Decline', 'Expire')) {
    powershell -NoProfile -File tests/UiSmoke.ps1 -Exe <absolute-path-to-Courier.exe> -Decision $decision
    if ($LASTEXITCODE -ne 0) { throw 'UI test failed' }
}
```

## Name change

Courier replaces OpenShare. New builds use Courier.exe and Courier.csproj. Existing releases remain available under their original names.

Existing settings and data locations retain the OpenShare folder name for compatibility. Keep these folders when upgrading; no automatic move or data format change is required.

The legacy TLS name remains unchanged so the rename does not alter the transfer protocol.

## Automatic setup

Windows setup packages install Courier for your current account, bundle the .NET runtime, create a Start menu shortcut and register an uninstaller. A desktop shortcut is optional. No administrator password or manual file copying is required. Existing settings and saved data stay in their current locations. The portable ZIP remains available.

Download `Courier-v1.2.0-setup.exe` from Releases and run it.

For unattended deployment:

```powershell
Start-Process .\Courier-v1.2.0-setup.exe -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-' -Wait -PassThru
```

Build from source with the [open-source Inno Setup compiler](https://github.com/jrsoftware/issrc) installed:

```powershell
pwsh -File scripts/Build-Setup.ps1 -Verify
```

The verification option installs into a temporary test folder, checks reinstall and launch, then uninstalls. It runs on the hosted Windows runner during CI. It does not remove existing user settings or backups.

## Export and verify transfer receipts

Version 1.2 adds headless receipt reports. Export the current account's last 100 receipts, or replace `default` with a saved history file:

```powershell
Start-Process .\Courier.exe -ArgumentList '--export-history default --report "D:\Reports\receipts.json"' -WindowStyle Hidden -Wait -PassThru
Start-Process .\Courier.exe -ArgumentList '--verify-receipt "D:\Reports\receipts.json" 1 "D:\Received\file.zip" --report "D:\Reports\verification.json"' -WindowStyle Hidden -Wait -PassThru
```

Receipt numbers start at 1 in report order, oldest first. Verification streams the selected local file and compares its byte count and SHA-256 with the receipt. Renamed files can still match. Exit code 0 means success, 3 means the file differs, and 2 means invalid input or an operation failure. Reports never replace an existing file; their parent folder must already exist. Receipt reports contain filenames and completion times, so share them intentionally. They contain no pairing codes, secrets, peer addresses or file contents. A receipt checks content integrity; it does not establish who sent a file and is not a digital signature.

History reads are bounded to 1 MiB and 100 validated entries. Corrupt history is preserved and reported, and simultaneous Courier processes serialize history updates. A history write failure after delivery now reports the confirmed transfer with a separate history warning. Receipt export and verification run without opening a window or contacting the network.
