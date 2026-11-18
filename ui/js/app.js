// app.js — EmuClean frontend. Talks to the engine via window.pywebview.api
// (wrapped from the WebView2 host bridge) or the MOCK backend in a browser.

'use strict';

// ------------------------------------------------------------- host bridge
// Inside the WebView2 host, window.chrome.webview exists. Wrap it into a
// pywebview-like api so the rest of this file stays unchanged. In a plain
// browser neither exists and the MOCK backend below is used.
(function buildBridge() {
  const wv = window.chrome && window.chrome.webview;
  if (!wv || window.pywebview) return;
  const pending = {};
  let seq = 1;
  wv.addEventListener('message', (ev) => {
    const d = ev.data || {};
    if (d.__emu !== 2) return;
    const p = pending[d.id];
    if (!p) return;
    delete pending[d.id];
    if (d.ok) p.resolve(d.data); else p.reject(new Error(d.error || 'host error'));
  });
  window.pywebview = {
    api: new Proxy({}, {
      get: (t, method) => (...args) => new Promise((resolve, reject) => {
        const id = seq++;
        pending[id] = { resolve, reject };
        wv.postMessage({ __emu: 1, id, method: String(method), args });
      }),
    }),
  };
  // forward the HTML titlebar drag to the native frameless window
  document.addEventListener('DOMContentLoaded', () => {
    const tb = document.getElementById('titlebar');
    if (!tb) return;
    tb.addEventListener('mousedown', (e) => {
      if (e.target.closest('button') || e.button !== 0) return;
      wv.postMessage({ type: 'drag' });
    });
  });
})();

const $ = (sel) => document.querySelector(sel);
const $$ = (sel) => [...document.querySelectorAll(sel)];

// ---------------------------------------------------------------- mock data
const MOCK = {
  detect: () => ({
    version: '1.0.0',
    emulators: [
      {
        id: 'bluestacks5', name: 'BlueStacks 5', kind: 'android', emoji: '🔷',
        installed: true, running: true,
        install_root: 'C:\\Program Files\\BlueStacks_nxt',
        data_root: 'C:\\ProgramData\\BlueStacks_nxt',
        junk_human: '412.3 MB',
        rules: [
          { category: 'junk', label: 'Service & engine logs', note: 'BlueStacks service logs', size: 81200000, files: 342, auto: true, locked: false, paths: ['C:\\ProgramData\\BlueStacks_nxt\\Logs'] },
          { category: 'junk', label: 'Crash dumps', note: 'crash reports', size: 12400000, files: 12, auto: true, locked: false, paths: ['C:\\ProgramData\\BlueStacks_nxt\\CrashDumps'] },
          { category: 'opt', label: 'Shared folder', note: 'files you moved in/out', size: 900000, files: 6, auto: false, locked: false, paths: ['C:\\ProgramData\\BlueStacks_nxt\\BstkSharedFolder'] },
          { category: 'lock', label: 'Instances (apps & data)', note: 'your Android instances — games, apps, accounts', size: 12400000000, files: 8120, auto: false, locked: true, paths: ['C:\\ProgramData\\BlueStacks_nxt\\Engine'] },
          { category: 'lock', label: 'Player configs', note: 'controls, resolution, preferences', size: 400000, files: 18, auto: false, locked: true, paths: ['C:\\ProgramData\\BlueStacks_nxt\\Config'] },
        ],
      },
      {
        id: 'dolphin', name: 'Dolphin (Wii/GameCube)', kind: 'desktop', emoji: '🐬',
        installed: true, running: false,
        install_root: 'C:\\Program Files\\Dolphin',
        data_root: 'C:\\Users\\you\\Documents\\Dolphin Emulator',
        junk_human: '63.8 MB',
        rules: [
          { category: 'junk', label: 'Cache', note: 'shader cache — regenerates', size: 58000000, files: 210, auto: true, locked: false, paths: ['...\\Cache'] },
          { category: 'junk', label: 'Logs', note: 'emulator logs', size: 5800000, files: 22, auto: true, locked: false, paths: ['...\\Logs'] },
          { category: 'lock', label: 'GameCube saves', note: 'memory card saves', size: 1200000, files: 8, auto: false, locked: true, paths: ['...\\GC'] },
          { category: 'lock', label: 'Wii saves & NAND', note: 'Wii saves, Mii data, NAND', size: 340000000, files: 90, auto: false, locked: true, paths: ['...\\Wii'] },
        ],
      },
    ],
    leftovers: [
      { id: 'nox', name: 'NoxPlayer', emoji: '🌑', paths: ['C:\\Users\\you\\AppData\\Local\\Nox'], size: 890000000 },
    ],
  }),
};

// ------------------------------------------------------------- api bridge
function api() {
  if (window.pywebview && window.pywebview.api) return window.pywebview.api;
  return {
    __mock: true,
    detect: MOCK.detect,
    execute_clean: async (selections, permanent) => ({
      deleted: selections.reduce((a, s) => a + s.paths.length, 0),
      freed: 0, failed: 0, errors: [],
      log: ['[mock] would send targets to the Recycle Bin'],
    }),
    adb_status: async () => ({ ok: true, adb_path: 'C:\\Program Files\\BlueStacks_nxt\\HD-Adb.exe', ports: { RabbitIce: '5555' }, message: '1 instance port(s) found' }),
    adb_connect: async () => ({ ok: true, message: 'connected: 127.0.0.1:5555' }),
    adb_packages: async () => ({
      ok: true, packages: [
        { name: 'com.bluestacks.sapp', core: false, safe: true, balanced: true, aggressive: true },
        { name: 'com.bluestacks.settings', core: false, safe: false, balanced: false, aggressive: false },
        { name: 'com.android.settings', core: true, safe: false, balanced: false, aggressive: false },
        { name: 'com.king.candycrush.saga', core: false, safe: false, balanced: false, aggressive: true },
        { name: 'com.facebook.katana', core: false, safe: false, balanced: false, aggressive: true },
        { name: 'com.google.android.gms', core: true, safe: false, balanced: false, aggressive: false },
      ],
    }),
    adb_uninstall: async (emu, pkgs) => ({
      ok: true,
      results: Object.fromEntries(pkgs.map((p) => [p, 'ok'])),
      log: pkgs.map((p) => `uninstalled ${p}`),
    }),
    optimizer_catalog: async () => ({
      android: [
        { name: 'cached_apps_freezer', label: 'Enable cached apps freezer', note: 'frees RAM in background apps' },
        { name: 'activity_logging', label: 'Disable activity starts logging', note: 'less overhead' },
        { name: 'package_verifier', label: 'Disable package verifier', note: 'less install lag' },
        { name: 'show_processes', label: 'Hide background process count', note: 'cleaner, less work' },
        { name: 'dirty_regions', label: 'Disable dirty-region debug drawing', note: '' },
        { name: 'wifi_display', label: 'Disable Wi-Fi display service', note: '' },
        { name: 'fstrim', label: 'More aggressive fstrim', note: 'keeps storage snappy' },
      ],
      pc: [
        { name: 'power_high', label: 'High Performance power plan', note: 'switch from Balanced' },
        { name: 'gamedvr_off', label: 'Turn off Game Bar background recording', note: 'steals CPU/GPU from emulators' },
        { name: 'priority_high', label: 'Raise emulator process priority', note: 'running emulator processes to High' },
      ],
    }),
    apply_optimizer: async (android, pc) => ({ android: {}, pc: {}, log: ['[mock] applied'] }),
    device_status: async () => ({
      adb: true, adb_path: 'C:\\…\\EmuClean\\platform-tools\\adb.exe',
      devices: [{ serial: 'R58N123ABC', state: 'device', usb: true }],
      serial: 'R58N123ABC', message: 'connected: R58N123ABC',
    }),
    device_connect: async (s) => ({ ok: true, message: 'connected: ' + s }),
    device_reconnect: async () => ({ ok: true, serial: 'R58N123ABC', devices: [{ serial: 'R58N123ABC', state: 'device', usb: true }] }),
    device_info: async () => ({
      ok: true, brand: 'Google', model: 'Pixel 7', device: 'p7', android: '14', sdk: '34',
      patch: '2024-10-05', build: 'AP2A.241005.004', abi: 'arm64-v8a', serial: 'R58N123ABC',
      screen: '1080x2400', density: '420', battery_level: '86', battery_status: 'discharging',
      battery_temp: '29.4 °C', storage_total: '117 GB', storage_free: '44.2 GB', ram: '7.7 GB',
    }),
    device_packages: async () => ({
      ok: true,
      packages: [
        { name: 'com.facebook.katana', system: false, nuke: true, core: false },
        { name: 'com.supercell.clashofclans', system: false, nuke: true, core: false },
        { name: 'com.instagram.barcelona', system: false, nuke: true, core: false },
        { name: 'com.whatsapp.w4b', system: false, nuke: false, core: false },
        { name: 'com.google.android.apps.tachyon', system: true, nuke: true, core: false },
        { name: 'com.android.settings', system: true, nuke: false, core: true },
      ],
    }),
    device_uninstall: async (e, pkgs) => ({ ok: true, results: Object.fromEntries(pkgs.map((p) => [p, 'ok'])), log: pkgs.map((p) => 'uninstalled ' + p) }),
    device_restore: async (e, pkg) => ({ ok: true, message: 'restored ' + pkg }),
    device_force_stop_all: async () => ({ ok: true, stopped: 42, failed: 0, log: ['[mock] force-stopped'] }),
    device_deny_notifications: async () => ({ ok: true, denied: 40, failed: 2, sdk_ok: true, message: '', log: ['[mock] denied'] }),
    device_ls: async (e, path) => ({ ok: true, path, entries: [{ name: 'DCIM', dir: true }, { name: 'Download', dir: true }, { name: 'Pictures', dir: true }, { name: 'Android', dir: true }, { name: 'notes.txt', dir: false }] }),
    device_pull: async () => ({ ok: true, message: 'pulled 12 file(s) → C:\\Users\\you\\Downloads\\EmuClean' }),
    device_quick_pull: async () => ({ ok: true, message: 'pulled 12 file(s) → C:\\Users\\you\\Downloads\\EmuClean' }),
    device_push: async () => ({ ok: true, message: 'pushed 1 file(s)' }),
    device_reboot: async () => ({ ok: true, message: 'rebooting…' }),
    get_log: async () => ['[mock] log line 1', '[mock] log line 2'],
  };
}

function toast(msg, err) {
  const t = $('#toast');
  t.textContent = msg;
  t.classList.toggle('err', !!err);
  t.classList.remove('hidden');
  clearTimeout(t._h);
  t._h = setTimeout(() => t.classList.add('hidden'), 3200);
}

function fmtSize(n) {
  if (n == null) return '—';
  if (n < 1024) return `${n} B`;
  if (n < 1024 ** 2) return `${(n / 1024).toFixed(1)} KB`;
  if (n < 1024 ** 3) return `${(n / 1024 ** 2).toFixed(1)} MB`;
  return `${(n / 1024 ** 3).toFixed(2)} GB`;
}

// ---------------------------------------------------------------- navigation
$$('.nav-item').forEach((el) => {
  el.addEventListener('click', () => {
    $$('.nav-item').forEach((x) => x.classList.remove('active'));
    el.classList.add('active');
    const page = el.dataset.page;
    $$('.page').forEach((p) => p.classList.remove('active'));
    $(`#page-${page}`).classList.add('active');
    if (page === 'log') refreshLog();
    if (page === 'adb') initAdb();
    if (page === 'optimize') initOptimize();
    if (page === 'device') initDevice();
  });
});

$('#btn-min').addEventListener('click', () => {
  if (window.pywebview) window.pywebview.api.__window_min && window.pywebview.api.__window_min();
});
$('#btn-close').addEventListener('click', () => {
  if (window.pywebview) window.pywebview.api.close_window();
});

// ---------------------------------------------------------------- modal
let modalOk = null;
function showModal(title, bodyHtml, onOk, okLabel) {
  $('#modal-title').textContent = title;
  $('#modal-body').innerHTML = bodyHtml;
  $('#modal-ok').textContent = okLabel || 'Confirm';
  modalOk = onOk;
  $('#modal').classList.remove('hidden');
}
$('#modal-cancel').addEventListener('click', () => $('#modal').classList.add('hidden'));
$('#modal-ok').addEventListener('click', () => {
  $('#modal').classList.add('hidden');
  if (modalOk) modalOk();
});

// ---------------------------------------------------------------- state
const state = {
  detected: null,
  selectedEmu: null,
  selections: {},      // key `${emu}:${label}` -> {emu,label,category,paths,checked}
  adbEmu: null,
  adbPackages: null,
};

// ---------------------------------------------------------------- clean page
async function loadDetection() {
  const box = $('#detect-status');
  box.classList.add('hidden');
  try {
    state.detected = await api().detect();
  } catch (e) {
    toast('Detection failed: ' + e, true);
    return;
  }
  $('#foot-version').textContent = `v${state.detected.version || '1.0.0'}`;
  renderEmuGrid();
  renderLeftovers();
  if (!state.detected.emulators.length) {
    box.textContent = 'No emulators detected on this PC — nothing to clean here.';
    box.classList.remove('hidden');
  }
  $('#adb-status').innerHTML = '<div class="muted">Run a scan on the Clean page, pick an emulator there, then use this page.</div>';
  $('#adb-panel').classList.add('hidden');
}

function renderEmuGrid() {
  const grid = $('#emu-grid');
  grid.innerHTML = '';
  state.detected.emulators.forEach((emu) => {
    const el = document.createElement('div');
    el.className = 'card' + (state.selectedEmu === emu.id ? ' selected' : '');
    el.innerHTML = `
      <div class="badges">
        ${emu.running ? '<span class="badge run">running</span>' : ''}
        <span class="badge">${emu.kind}</span>
      </div>
      <div class="emoji">${emu.emoji}</div>
      <h3>${emu.name}</h3>
      <div class="root">${emu.install_root || emu.data_root || ''}</div>
      <div class="junk">junk found: <b>${emu.junk_human || fmtSize(emu.junk_size)}</b></div>`;
    el.addEventListener('click', () => selectEmu(emu.id));
    grid.appendChild(el);
  });
}

function selectEmu(id) {
  state.selectedEmu = id;
  renderEmuGrid();
  const emu = state.detected.emulators.find((e) => e.id === id);
  if (!emu) return;
  $('#rules-wrap').classList.remove('hidden');
  $('#rules-title').textContent = `${emu.emoji} ${emu.name} — cleanup rules`;
  renderRules(emu);
  // auto-select safe items
  state.selections = {};
  emu.rules.forEach((r) => {
    state.selections[`${id}:${r.label}`] = {
      emu: id, label: r.label, category: r.category,
      paths: r.paths, checked: r.auto,
    };
  });
  syncRuleChecks(emu);
}

function renderRules(emu) {
  const list = $('#rules-list');
  list.innerHTML = '';
  emu.rules.forEach((r) => {
    const el = document.createElement('div');
    el.className = 'rule' + (r.locked ? ' lock' : '');
    el.innerHTML = `
      <input type="checkbox" data-key="${emu.id}:${r.label}">
      <div class="lbl"><b>${r.locked ? '🔒 ' : ''}${r.label}</b><i>${r.note}</i></div>
      <span class="cat cat-${r.category}">${r.category === 'junk' ? 'junk' : r.category === 'opt' ? 'settings' : 'user data'}</span>
      <span class="size">${fmtSize(r.size)}</span>`;
    list.appendChild(el);
    el.querySelector('input').addEventListener('change', (ev) => {
      const key = `${emu.id}:${r.label}`;
      if (state.selections[key]) state.selections[key].checked = ev.target.checked;
      if (r.locked && ev.target.checked) {
        toast(`"${r.label}" is user data — double-check before cleaning!`, true);
      }
    });
  });
}

function syncRuleChecks(emu) {
  emu.rules.forEach((r) => {
    const box = $(`input[data-key="${emu.id}:${r.label}"]`);
    const sel = state.selections[`${emu.id}:${r.label}`];
    if (box && sel) box.checked = !!sel.checked;
  });
}

function selectionList() {
  return Object.values(state.selections).filter((s) => s.checked && s.paths.length);
}

$('#btn-safe').addEventListener('click', () => {
  const emu = currentEmu();
  if (!emu) return;
  emu.rules.forEach((r) => {
    if (state.selections[`${emu.id}:${r.label}`])
      state.selections[`${emu.id}:${r.label}`].checked = r.auto;
  });
  syncRuleChecks(emu);
  toast('Safe items selected (junk only)');
});

$('#btn-all').addEventListener('click', () => {
  const emu = currentEmu();
  if (!emu) return;
  showModal('Select everything?', `
    <p class="modal-body-list">This includes <b>locked user-data items</b> — saves, memory cards,
    instance disks. Cleaning those deletes your game progress. EmuClean sends everything to the
    Recycle Bin (reversible), but be sure.</p>`, () => {
    emu.rules.forEach((r) => {
      if (state.selections[`${emu.id}:${r.label}`])
        state.selections[`${emu.id}:${r.label}`].checked = true;
    });
    syncRuleChecks(emu);
  }, 'Select all');
});

$('#btn-clear').addEventListener('click', () => {
  const emu = currentEmu();
  if (!emu) return;
  emu.rules.forEach((r) => {
    if (state.selections[`${emu.id}:${r.label}`])
      state.selections[`${emu.id}:${r.label}`].checked = false;
  });
  syncRuleChecks(emu);
});

function currentEmu() {
  return state.detected && state.detected.emulators.find((e) => e.id === state.selectedEmu);
}

$('#btn-clean').addEventListener('click', () => {
  const sel = selectionList();
  if (!sel.length) { toast('Nothing selected', true); return; }
  const emu = currentEmu();
  const totalPaths = sel.reduce((a, s) => a + s.paths.length, 0);
  const locked = sel.filter((s) => s.category === 'lock').length;
  showModal(`Clean ${sel.length} item(s) on ${emu.name}?`, `
    <p class="modal-body-list">${totalPaths} folder/file target(s)
    ${locked ? ` · <b style="color:#ffb454">${locked} user-data item(s) selected</b>` : ''}<br>
    Everything goes to the <b>Recycle Bin</b> — reversible until you empty it.<br>
    Tip: close the emulator first so files are not locked.</p>`,
    async () => {
      toast('Cleaning…');
      const res = await api().execute_clean(sel, false);
      toast(`Done — ${res.deleted} target(s) recycled${res.failed ? `, ${res.failed} failed` : ''}`,
            !!res.failed);
      refreshLog();
      setTimeout(loadDetection, 600);
    }, '🧹 Clean now');
});

$('#btn-rescan').addEventListener('click', () => { toast('Scanning…'); loadDetection(); });

function renderLeftovers() {
  const wrap = $('#leftover-wrap');
  const list = $('#leftover-list');
  if (!state.detected.leftovers.length) { wrap.classList.add('hidden'); return; }
  wrap.classList.remove('hidden');
  list.innerHTML = '';
  state.detected.leftovers.forEach((lo) => {
    const el = document.createElement('div');
    el.className = 'rule';
    el.innerHTML = `
      <input type="checkbox" data-lo="${lo.id}">
      <div class="lbl"><b>${lo.emoji} ${lo.name}</b><i>leftover data from an uninstalled emulator</i></div>
      <span class="size">${fmtSize(lo.size)}</span>`;
    list.appendChild(el);
  });
}

// ---------------------------------------------------------------- adb page
async function initAdb() {
  if (!state.detected) await loadDetection();
  const emu = state.detected.emulators.find((e) => e.kind === 'android');
  if (!emu) {
    $('#adb-status').innerHTML = '<div class="muted">No Android emulator detected — BlueStacks needed for ADB deep clean.</div>';
    $('#adb-panel').classList.add('hidden');
    return;
  }
  state.adbEmu = emu.id;
  $('#adb-panel').classList.remove('hidden');
  $('#adb-title').textContent = `📦 ${emu.name} — packages`;
  const st = await api().adb_status(emu.id);
  $('#adb-status').innerHTML = st.ok
    ? `✅ <b>HD-Adb found:</b> <span class="mono">${st.adb_path}</span> · ${st.message}
       ${Object.keys(st.ports || {}).map((k) => `<span class="badge">${k}:${st.ports[k]}</span>`).join(' ')}`
    : `⚠️ ${st.message}`;
  if (st.ok) await refreshPackages();
}

async function refreshPackages() {
  const res = await api().adb_packages(state.adbEmu);
  state.adbPackages = res.packages || [];
  renderPackages();
}

function renderPackages() {
  const filter = ($('#adb-filter').value || '').toLowerCase();
  const list = $('#adb-list');
  list.innerHTML = '';
  (state.adbPackages || []).forEach((p) => {
    if (filter && !p.name.toLowerCase().includes(filter)) return;
    const el = document.createElement('div');
    const prof = p.aggressive ? 'aggressive' : p.balanced ? 'balanced' : p.safe ? 'safe' : '';
    el.className = 'pkg' + (p.core ? ' core' : '') + (prof ? ' ' + prof : '');
    el.innerHTML = `
      <input type="checkbox" data-pkg="${p.name}" ${p.core ? 'disabled' : ''}>
      <span>${p.name}</span>
      <span class="tag">${p.core ? 'CORE — protected' : prof || ''}</span>`;
    list.appendChild(el);
  });
}

function pkgSelections() {
  return $$('input[data-pkg]').filter((c) => c.checked).map((c) => c.dataset.pkg);
}

function selectProfile(profile) {
  $$('input[data-pkg]').forEach((c) => {
    const p = state.adbPackages.find((x) => x.name === c.dataset.pkg);
    if (p && !p.core) c.checked = !!p[profile];
  });
  toast(`${profile} profile packages selected`);
}

$('#adb-filter').addEventListener('input', renderPackages);
$('#btn-adb-connect').addEventListener('click', async () => {
  const res = await api().adb_connect(state.adbEmu);
  toast(res.message, !res.ok);
  if (res.ok) await refreshPackages();
});
$('#btn-adb-safe').addEventListener('click', () => selectProfile('safe'));
$('#btn-adb-balanced').addEventListener('click', () => selectProfile('balanced'));
$('#btn-adb-aggro').addEventListener('click', () => selectProfile('aggressive'));
$('#btn-adb-clear').addEventListener('click', () => $$('input[data-pkg]').forEach((c) => (c.checked = false)));
$('#btn-adb-uninstall').addEventListener('click', async () => {
  const pkgs = pkgSelections();
  if (!pkgs.length) { toast('No packages selected', true); return; }
  showModal(`Uninstall ${pkgs.length} package(s)?`, `
    <p class="modal-body-list">Packages are removed from the <b>current user</b> only
    (pm uninstall --user 0) — a BlueStacks factory reset brings them back.</p>`,
  async () => {
    const res = await api().adb_uninstall(state.adbEmu, pkgs);
    toast(`Uninstalled ${Object.values(res.results).filter((r) => r === 'ok').length} package(s)`);
    refreshPackages(); refreshLog();
  }, 'Uninstall');
});

// ---------------------------------------------------------------- optimize
let optCatalog = null;
async function initOptimize() {
  if (!optCatalog) optCatalog = await api().optimizer_catalog();
  renderTweaks('#android-tweaks', optCatalog.android);
  renderTweaks('#pc-tweaks', optCatalog.pc);
}

function renderTweaks(sel, items) {
  const box = $(sel);
  box.innerHTML = '';
  items.forEach((t) => {
    const el = document.createElement('div');
    el.className = 'tweak';
    el.innerHTML = `
      <input type="checkbox" data-tweak="${t.name}">
      <div><b>${t.label}</b><i>${t.note || ''}</i></div>
      <span class="res" data-res="${t.name}"></span>`;
    box.appendChild(el);
  });
}

$('#btn-opt-android').addEventListener('click', async () => {
  const names = $$('input[data-tweak]').filter((c) => c.checked && optCatalog.android.some((t) => t.name === c.dataset.tweak)).map((c) => c.dataset.tweak);
  if (!names.length) { toast('Select android tweaks first', true); return; }
  const res = await api().apply_optimizer(names, [], state.adbEmu);
  Object.entries(res.android || {}).forEach(([k, v]) => {
    const el = $(`[data-res="${k}"]`);
    if (el) { el.textContent = v === 'ok' ? '✅' : '⚠️ ' + v; el.style.color = v === 'ok' ? 'var(--ok)' : 'var(--warn)'; }
  });
  refreshLog();
  toast('Android tweaks applied' + (res.android && Object.values(res.android).some((v) => v !== 'ok') ? '' : ' (check results)'));
});

$('#btn-opt-pc').addEventListener('click', async () => {
  const names = $$('input[data-tweak]').filter((c) => c.checked && optCatalog.pc.some((t) => t.name === c.dataset.tweak)).map((c) => c.dataset.tweak);
  if (!names.length) { toast('Select PC tweaks first', true); return; }
  const res = await api().apply_optimizer([], names, null);
  Object.keys(res.pc || {}).forEach((k) => {
    const el = $(`[data-res="${k}"]`);
    if (el) { el.textContent = '✅'; el.style.color = 'var(--ok)'; }
  });
  refreshLog();
  toast('PC tweaks applied');
});

// ---------------------------------------------------------------- device page
const devState = { serial: null, packages: null, cwd: '/sdcard' };

async function initDevice() {
  let st;
  try {
    st = await api().device_status();
  } catch (e) {
    $('#dev-status').innerHTML = `<div class="muted">device api unavailable: ${e}</div>`;
    return;
  }
  devState.serial = st.serial || null;
  renderDevStatus(st);
  if (devState.serial) {
    await loadDevInfo();
    await loadDevPackages();
    browseDev('/sdcard');
  }
}

function renderDevStatus(st) {
  const chips = (st.devices || []).map((d) =>
    `<span class="badge chip ${d.state === 'device' ? 'run' : ''}" data-serial="${d.serial}">${d.serial} · ${d.state}</span>`
  ).join(' ') || '<span class="badge">no devices — plug in a phone with USB debugging</span>';
  $('#dev-status').innerHTML = `
    <div class="muted" style="margin-bottom:8px">adb: <span class="mono">${st.adb_path || 'not found'}</span></div>
    <div>${st.message}</div>
    <div style="margin-top:10px">${chips}</div>`;
  $$('#dev-status .chip').forEach((c) => c.addEventListener('click', async () => {
    const res = await api().device_connect(c.dataset.serial);
    toast(res.message, !res.ok);
    if (res.ok) { devState.serial = c.dataset.serial; initDevice(); }
  }));
}

$('#btn-dev-reconnect').addEventListener('click', async () => {
  toast('Reconnecting…');
  const res = await api().device_reconnect();
  toast(res.ok ? `Reconnected — ${devState.serial || 'device ready'}` : 'No device found', !res.ok);
  initDevice();
});

const INFO_ROWS = [
  ['brand', 'Brand'], ['model', 'Model'], ['device', 'Device'], ['android', 'Android'],
  ['sdk', 'SDK'], ['patch', 'Security patch'], ['build', 'Build'], ['abi', 'ABI'],
  ['screen', 'Screen'], ['density', 'Density'], ['serial', 'Serial'],
];
function renderDevInfo(info) {
  const rows = INFO_ROWS.filter(([k]) => info[k] != null).map(([k, label]) =>
    `<div class="row"><b>${label}</b><span>${info[k]}</span></div>`);
  rows.push(`<div class="row"><b>Battery</b><span>${info.battery_level || '?'}% · ${info.battery_status || '?'} · ${info.battery_temp || '?'}</span></div>`);
  rows.push(`<div class="row"><b>Storage</b><span>${info.storage_free || '?'} free / ${info.storage_total || '?'}</span></div>`);
  rows.push(`<div class="row"><b>RAM</b><span>${info.ram || '?'}</span></div>`);
  $('#dev-info').innerHTML = rows.join('');
}

async function loadDevInfo() {
  const info = await api().device_info();
  if (info.ok !== false) renderDevInfo(info);
  else $('#dev-info').innerHTML = `<div class="muted">${info.message || 'no device'}</div>`;
}
$('#btn-dev-info').addEventListener('click', () => devState.serial && loadDevInfo());

// ---- files transfer
async function browseDev(path) {
  const res = await api().device_ls(path);
  if (!res.ok) { toast('Cannot read ' + path, true); return; }
  devState.cwd = path;
  $('#dev-pathbar').textContent = path;
  const grid = $('#dev-files');
  grid.innerHTML = '';
  if (path !== '/sdcard' && path !== '/') {
    const up = document.createElement('div');
    up.className = 'frow';
    up.innerHTML = '↩️ ..';
    up.addEventListener('click', () => browseDev(path.split('/').slice(0, -1).join('/') || '/'));
    grid.appendChild(up);
  }
  (res.entries || []).forEach((e) => {
    const el = document.createElement('div');
    el.className = 'frow' + (e.dir ? '' : ' f');
    el.innerHTML = `${e.dir ? '📁' : '📄'} ${e.name}`;
    if (e.dir) el.addEventListener('click', () => browseDev(path === '/' ? '/' + e.name : path + '/' + e.name));
    grid.appendChild(el);
  });
}
function quickPull(kind, label) {
  return async () => {
    if (!devState.serial) { toast('No device connected', true); return; }
    toast(`Pulling ${label}…`);
    const res = await api().device_quick_pull(kind);
    toast(res.message, !res.ok);
    refreshLog();
  };
}
$('#btn-pull-shots').addEventListener('click', quickPull('screenshots', 'Screenshots'));
$('#btn-pull-cam').addEventListener('click', quickPull('camera', 'Camera photos'));
$('#btn-pull-dl').addEventListener('click', quickPull('downloads', 'Download folder'));
$('#btn-dev-push').addEventListener('click', async () => {
  const local = $('#dev-push-local').value.trim();
  const remote = $('#dev-push-remote').value.trim();
  if (!local || !remote) { toast('Fill both paths', true); return; }
  const res = await api().device_push(local, remote);
  toast(res.message, !res.ok);
  refreshLog();
});

// ---- BloatDroid
async function loadDevPackages() {
  const res = await api().device_packages(false); // include system so preinstalled bloat shows
  devState.packages = res.ok ? res.packages || [] : [];
  renderDevPackages();
}

function renderDevPackages() {
  const filter = ($('#dev-filter').value || '').toLowerCase();
  const list = $('#dev-list');
  list.innerHTML = '';
  (devState.packages || []).forEach((p) => {
    if (filter && !p.name.toLowerCase().includes(filter)) return;
    const el = document.createElement('div');
    el.className = 'pkg' + (p.core ? ' core' : '') + (p.nuke ? ' nuke' : '');
    const tag = p.core ? 'CORE — protected' : p.system ? 'system' + (p.nuke ? ' · ☢️ preinstalled bloat' : '')
      : p.nuke ? '☢️ bloat' : '';
    el.innerHTML = `
      <input type="checkbox" data-devpkg="${p.name}" ${p.core ? 'disabled' : ''}>
      <span>${p.name}</span>
      <span class="tag">${tag}</span>`;
    list.appendChild(el);
  });
}

function devPkgSelections() {
  return $$('input[data-devpkg]').filter((c) => c.checked).map((c) => c.dataset.devpkg);
}
function selectNukable() {
  $$('input[data-devpkg]').forEach((c) => {
    const p = devState.packages.find((x) => x.name === c.dataset.devpkg);
    if (p && p.nuke && !p.core) c.checked = true;
  });
}

$('#dev-filter').addEventListener('input', renderDevPackages);
$('#btn-dev-refresh').addEventListener('click', () => devState.serial && loadDevPackages());
$('#btn-dev-sel3').addEventListener('click', () => {
  $$('input[data-devpkg]').forEach((c) => {
    const p = devState.packages.find((x) => x.name === c.dataset.devpkg);
    if (p && !p.core) c.checked = true;
  });
});
$('#btn-dev-nukesel').addEventListener('click', () => { selectNukable(); toast('Preinstalled bloat selected'); });

$('#btn-dev-uninstall').addEventListener('click', async () => {
  const pkgs = devPkgSelections();
  if (!pkgs.length) { toast('No packages selected', true); return; }
  devUninstallFlow(pkgs, `Uninstall ${pkgs.length} package(s)?`);
});

$('#btn-dev-nuke').addEventListener('click', () => {
  const nukable = (devState.packages || []).filter((p) => p.nuke && !p.core).map((p) => p.name);
  if (!nukable.length) { toast('No known bloat found on this device 🙂', true); return; }
  devUninstallFlow(nukable,
    `☢️ Full Nuke — remove ${nukable.length} known-bloat package(s)?`,
    'They are removed for the current user only. Restore any of them later with the Restore box below.');
});

async function devUninstallFlow(pkgs, title, note) {
  showModal(title, `
    <p class="modal-body-list">${note || ''}${pkgs.slice(0, 12).map((p) => `<br>• <span class="mono">${p}</span>`).join('')}
    ${pkgs.length > 12 ? `<br>… and ${pkgs.length - 12} more` : ''}<br><br>
    Removed with <b>pm uninstall --user 0</b> — reversible for system apps, a factory reset brings everything back.</p>`,
  async () => {
    toast('Uninstalling…');
    const res = await api().device_uninstall(devState.serial, pkgs);
    const okN = Object.values(res.results || {}).filter((r) => r === 'ok').length;
    toast(`Removed ${okN}/${pkgs.length} package(s)`);
    refreshLog();
    loadDevPackages();
  }, 'Uninstall');
}

// ---- quick actions
$('#btn-dev-forcestop').addEventListener('click', () => {
  if (!devState.serial) { toast('No device connected', true); return; }
  showModal('Force Stop ALL 3rd-party apps?', `
    <p class="modal-body-list">Every user-installed app is stopped (<b>am force-stop</b>).
    Apps will relaunch normally when you open them — nothing is uninstalled.</p>`,
  async () => {
    const res = await api().device_force_stop_all();
    toast(`Stopped ${res.stopped} app(s)` + (res.failed ? `, ${res.failed} failed` : ''), !!res.failed);
    refreshLog();
  }, 'Force Stop');
});

$('#btn-dev-nonotif').addEventListener('click', () => {
  if (!devState.serial) { toast('No device connected', true); return; }
  showModal('Disable notifications for ALL apps?', `
    <p class="modal-body-list">Sets <b>appops POST_NOTIFICATION deny</b> for every user-installed app
    (Android 9+). Silent apps, same apps. Re-enable any time from Android settings → Notifications.</p>`,
  async () => {
    const res = await api().device_deny_notifications();
    if (res.sdk_ok) toast(`Notifications denied for ${res.denied} app(s)` + (res.failed ? `, ${res.failed} failed` : ''), !!res.failed);
    else toast(res.message || 'Not supported on this Android version', true);
    refreshLog();
  }, 'Silence all');
});

function rebootFlow(mode, label) {
  return () => {
    if (!devState.serial) { toast('No device connected', true); return; }
    showModal(`${label}?`, `<p class="modal-body-list">The device will restart now.</p>`,
      async () => {
        const res = await api().device_reboot(mode);
        toast(res.message || 'Reboot sent');
      }, label);
  };
}
$('#btn-dev-reboot').addEventListener('click', rebootFlow('system', 'Reboot device'));
$('#btn-dev-recovery').addEventListener('click', rebootFlow('recovery', 'Reboot to recovery'));

$('#btn-dev-restore').addEventListener('click', async () => {
  const pkg = $('#dev-restore-pkg').value.trim();
  if (!pkg) { toast('Type a package name first', true); return; }
  const res = await api().device_restore(devState.serial, pkg);
  toast(res.message, !res.ok);
  refreshLog();
});

// ---------------------------------------------------------------- log page
async function refreshLog() {
  try {
    const lines = await api().get_log();
    $('#log-box').textContent = (lines || []).join('\n') || '(empty)';
  } catch (e) {
    $('#log-box').textContent = String(e);
  }
}
$('#btn-log-copy').addEventListener('click', () => {
  navigator.clipboard.writeText($('#log-box').textContent);
  toast('Log copied');
});

// ---------------------------------------------------------------- boot
window.addEventListener('pywebviewready', loadDetection);
if (!window.pywebview) setTimeout(loadDetection, 150); // browser preview
