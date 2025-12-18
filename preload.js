const { contextBridge, ipcRenderer } = require('electron');


contextBridge.exposeInMainWorld('api', {
listDevices: () => ipcRenderer.invoke('list-devices'),
unpairByMac: (mac) => ipcRenderer.invoke('unpair-by-mac', mac),
pairByMac: (mac) => ipcRenderer.invoke('pair-by-mac', mac),
saveName: (mac, name) => ipcRenderer.invoke('save-name', mac, name),
readNames: () => ipcRenderer.invoke('read-names')
});