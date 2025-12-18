const $ = (sel) => document.querySelector(sel);
const tbody = document.querySelector('#devices tbody');
const status = document.getElementById('status');

let currentDevices = [];

const SAVE_DEBOUNCE = 800;
const saveTimers = {};
function setRowSaveStatus(mac, text, isError = false) {
  const span = tbody.querySelector(`.save-status[data-mac="${mac}"]`);
  if (span) {
    span.textContent = text;
    span.style.color = isError ? 'darkred' : 'green';
  }
}

function setStatus(text, isError = false) {
  status.textContent = text;
  status.style.color = isError ? 'darkred' : 'black';
}

function escapeHtml(s) {
  return (s || '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

// Renderizar tabla
function renderDevices(devices) {
  tbody.innerHTML = '';
  currentDevices = devices;

  if (!devices.length) {
    const tr = document.createElement('tr');
    tr.innerHTML = `<td colspan="5">No se encontraron dispositivos Bluetooth</td>`;
    tbody.appendChild(tr);
    return;
  }

  devices.forEach((d, i) => {
    const tr = document.createElement('tr');

    tr.innerHTML = `
      <td>${i + 1}</td>
      <td>
        <input 
          class="name" 
          data-mac="${d.MAC}" 
          value="${escapeHtml(d.DisplayName || '')}" 
          placeholder="Nombre local" 
        />
        <button class="forget-local" data-mac="${d.MAC}">Olvidar</button>
        <span class="save-status" data-mac="${d.MAC}" style="margin-left:8px"></span>
      </td>
      <td>${d.MAC}</td>
      <td>${d.Status || ''}</td>
      <td>
        <button class="unpair" data-mac="${d.MAC}">Desemparejar</button>
        <button class="pair" data-mac="${d.MAC}">Emparejar</button>
      </td>
    `;

    tbody.appendChild(tr);
  });
}

// Cargar dispositivos desde backend
async function loadDevices() {
  setStatus('Cargando dispositivos…');

  const res = await window.api.listDevices();

  if (!res.ok) {
    setStatus('Error al listar dispositivos: ' + (res.error || 'desconocido'), true);
    return;
  }

  renderDevices(res.devices || []);
  setStatus(`Dispositivos encontrados: ${(res.devices || []).length}`);
}

// Acciones de botones

// Auto-guardar nombre al editar con debounce
tbody.addEventListener('input', (e) => {
  const t = e.target;
  if (!t.matches('input.name')) return;
  const mac = t.dataset.mac;
  const name = t.value.trim();

  // mostrar estado pendiente
  setRowSaveStatus(mac, 'Guardando...');

  if (saveTimers[mac]) clearTimeout(saveTimers[mac]);

  saveTimers[mac] = setTimeout(async () => {
    const r = await window.api.saveName(mac, name);
    if (r.ok) {
      setRowSaveStatus(mac, 'Guardado');
      const dev = currentDevices.find(d => d.MAC === mac);
      if (dev) dev.DisplayName = name || dev.FriendlyName || mac;
      setTimeout(() => setRowSaveStatus(mac, ''), 2000);
    } else {
      setRowSaveStatus(mac, 'Error', true);
    }
    delete saveTimers[mac];
  }, SAVE_DEBOUNCE);
});

tbody.addEventListener('click', async (e) => {
  const t = e.target;

  // OLVIDAR
  if (t.matches('.unpair')) {
    const mac = t.dataset.mac;

    const device = currentDevices.find(d => d.MAC === mac);
    const name = device?.DisplayName || mac;

    if (!confirm(`¿Deseas olvidar el dispositivo?\n\n${name} (${mac})`)) return;

    setStatus(`Olvidando ${name}…`);

    const r = await window.api.unpairByMac(mac);

    if (r.ok) {
      setStatus(`Dispositivo "${name}" olvidado correctamente.`);
    } else {
      setStatus(`Error al olvidar: ${r.error || r.stderr || 'desconocido'}`, true);
    }

    await loadDevices();
  }

  // EMPAREJAR
  if (t.matches('.pair')) {
    const mac = t.dataset.mac;

    const device = currentDevices.find(d => d.MAC === mac);
    const name = device?.DisplayName || mac;

    setStatus(`Emparejando ${name}…`);

    const r = await window.api.pairByMac(mac);

    if (r.ok) {
      setStatus(`Dispositivo "${name}" emparejado correctamente.`);
    } else {
      setStatus(`Error al emparejar: ${r.error || r.stderr || 'desconocido'}`, true);
    }

    await loadDevices();
  }

  // GUARDAR NOMBRE LOCAL
  if (t.matches('.save')) {
    const mac = t.dataset.mac;
    const input = tbody.querySelector(`input.name[data-mac="${mac}"]`);
    const name = input.value.trim();

    if (!name.length) {
      if (!confirm('El nombre está vacío. ¿Deseas eliminar el nombre personalizado?')) {
        return;
      }
    }

    setStatus(`Guardando nombre para ${mac}…`);

    const r = await window.api.saveName(mac, name);

    if (r.ok) {
      setStatus('Nombre guardado correctamente.');
    } else {
      setStatus('Error al guardar: ' + (r.error || 'desconocido'), true);
    }

    await loadDevices();
  }

  // ELIMINAR RECUERDO LOCAL
  if (t.matches('.forget-local')) {
    const mac = t.dataset.mac;
    if (!confirm(`¿Deseas eliminar el recuerdo local del dispositivo ${mac}?`)) return;

    setStatus(`Eliminando recuerdo de ${mac}…`);

    const r = await window.api.saveName(mac, '');

    if (r.ok) {
      setStatus('Recuerdo eliminado correctamente.');
    } else {
      setStatus('Error al eliminar: ' + (r.error || 'desconocido'), true);
    }

    await loadDevices();
  }
});

// Botón refrescar
document.getElementById('refresh').addEventListener('click', loadDevices);

// Carga inicial automática
loadDevices();
