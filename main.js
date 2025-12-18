const { app, BrowserWindow, ipcMain, dialog } = require('electron');
const path = require('path');
const fs = require('fs');
const { exec, spawn } = require('child_process');


const DATA_FILE = path.join(app.getPath('userData'), 'devices.json');


function ensureDataFile() {
if (!fs.existsSync(DATA_FILE)) {
fs.writeFileSync(DATA_FILE, JSON.stringify({ names: {} }, null, 2), 'utf8');
}
}


function readNames() {
ensureDataFile();
return JSON.parse(fs.readFileSync(DATA_FILE, 'utf8'));
}


function writeNames(obj) {
fs.writeFileSync(DATA_FILE, JSON.stringify(obj, null, 2), 'utf8');
}


function createWindow() {
const win = new BrowserWindow({
width: 900,
height: 700,
autoHideMenuBar: true,
frame: true,
backgroundColor: '#f5f5f5',
webPreferences: {
preload: path.join(__dirname, 'preload.js'),
contextIsolation: true,
nodeIntegration: false
}
});
win.setMenuBarVisibility(false);
win.loadFile(path.join(__dirname, 'renderer', 'index.html'));
}


app.whenReady().then(() => {
ensureDataFile();
createWindow();
});


// IPC handlers
ipcMain.handle('list-devices', async () => {

  const psScript = `
Get-PnpDevice -Class Bluetooth | 
  Where-Object { $_.HardwareID -match 'DEV_' } | 
  Select-Object FriendlyName, Status, Class, HardwareID,
    @{N='Address';E={[uInt64]('0x{0}' -f $_.HardwareID[0].Substring(12))}},
    @{N='MAC';E={
      $hex = $_.HardwareID[0].Substring(12)
      ($hex -split '(?<=\\G.{2})' -ne '') -join ':'
    }} | 
  ForEach-Object { 
    Write-Output ('{0}|{1}|{2}|{3}' -f $_.FriendlyName, $_.Status, $_.MAC, $_.Address) 
  }
`;

  const tempFile = path.join(app.getPath('temp'), 'bt-list.ps1');
  fs.writeFileSync(tempFile, psScript, 'utf8');

  return new Promise((resolve) => {
    require('child_process').exec(
      `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "${tempFile}"`,
      { maxBuffer: 1024 * 1024 * 10 },
      (err, stdout, stderr) => {

        fs.unlinkSync(tempFile);

        if (err) {
          resolve({ ok: false, error: stderr || err.message });
          return;
        }

        const lines = (stdout || '').trim().split(/\r?\n/).filter(Boolean);

        const devices = lines.map(line => {
          const [FriendlyName, Status, MAC, Address] = line.split('|');
          return { FriendlyName, Status, MAC, Address };
        });

        // Mezclar con nombres guardados localmente
        const data = readNames();
        const devicesWithNames = devices.map(d => ({
          ...d,
          DisplayName: data.names[d.MAC] || d.FriendlyName
        }));

        // Añadir dispositivos guardados que ya no aparecen en el sistema
        Object.keys(data.names || {}).forEach(macSaved => {
          if (!devicesWithNames.find(x => x.MAC === macSaved)) {
            devicesWithNames.push({
              FriendlyName: data.names[macSaved] || macSaved,
              Status: 'Guardado',
              MAC: macSaved,
              Address: null,
              DisplayName: data.names[macSaved] || macSaved,
              Known: true
            });
          }
        });

        resolve({ ok: true, devices: devicesWithNames });
      }
    );
  });
});

ipcMain.handle('save-name', async (event, mac, name) => {
  try {
    const data = readNames();
    if (!name || name.trim() === '') {
      delete data.names[mac];
    } else {
      data.names[mac] = name.trim();
    }
    writeNames(data);
    return { ok: true };
  } catch (err) {
    return { ok: false, error: err.message };
  }
});

ipcMain.handle('read-names', async () => {
  try {
    const data = readNames();
    return { ok: true, names: data.names };
  } catch (err) {
    return { ok: false, error: err.message };
  }
});

ipcMain.handle('unpair-by-mac', async (event, mac) => {
  const exe = path.join(__dirname, 'BluetoothDevicePairing.exe');
  
  // Intentar usar el ejecutable primero
  if (fs.existsSync(exe)) {
    return new Promise((resolve) => {
      exec(
        `"${exe}" unpair-by-mac --mac ${mac.toLowerCase()} --type Bluetooth`,
        (err, stdout, stderr) => {
          if (err) {
            resolve({ ok: false, error: stderr || err.message, stderr });
          } else {
            resolve({ ok: true, stdout, stderr });
          }
        }
      );
    });
  }

  // Fallback: usar PowerShell con pnputil
  const psScript = `
    $mac = '${mac}'
    Get-PnpDevice -Class Bluetooth | Where-Object { 
      $_.HardwareID -match 'DEV_' -and $_.HardwareID[0] -match ($mac -replace ':', '')
    } | ForEach-Object {
      pnputil /remove-device $_.InstanceId
    }
  `;

  return new Promise((resolve) => {
    exec(
      `powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "${psScript.replace(/"/g, '\\"')}"`,
      (err, stdout, stderr) => {
        if (err) {
          resolve({ ok: false, error: stderr || err.message, stderr });
        } else {
          resolve({ ok: true, stdout, stderr });
        }
      }
    );
  });
});

ipcMain.handle('pair-by-mac', async (event, mac) => {
  const exeName = 'BluetoothDevicePairing.exe';
  const candidates = [
    path.join(__dirname, exeName),
    path.join(process.resourcesPath, exeName)
  ];
  const exe = candidates.find(p => fs.existsSync(p));

  if (!exe) {
    console.error('BluetoothDevicePairing.exe no encontrado; colócalo en la carpeta raíz o en extraResources');
  }

  return new Promise((resolve) => {
    exec(
      `"${exe}" pair-by-mac --mac ${mac.toLowerCase()} --type Bluetooth`,
      (err, stdout, stderr) => {
        if (err) {
          resolve({ ok: false, error: stderr || err.message, stderr });
        } else {
          resolve({ ok: true, stdout, stderr });
        }
      }
    );
  });
});