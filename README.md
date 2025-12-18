# Bluetooth Manager (.NET 8 WPF)

Esta es una reimplementación de la aplicación original en Electron, ahora usando .NET 8 y WPF.

Características principales:
- Listado de dispositivos Bluetooth usando PowerShell `Get-PnpDevice` (igual que la versión previa)
- Guardado local de nombres en JSON en `%APPDATA%\BluetoothManager\devices.json`
- Emparejar / desemparejar por MAC usando `BluetoothDevicePairing.exe` si está disponible o `pnputil` (fallback)

Cómo compilar:
1. Asegúrate de tener .NET 8 SDK instalado.
2. Desde la carpeta `dotnet` ejecuta:

   dotnet build
   dotnet run --project .

Notas:
- Para emparejar/desemparejar la app intentará usar `BluetoothDevicePairing.exe` si está junto a la aplicación.
- Si necesitas usar APIs WinRT nativas más seguras, podemos migrar la enumeración para usar `Windows.Devices.Bluetooth`.
