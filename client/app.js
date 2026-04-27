'use strict';

const API = window.IDADRS_API_URL
  || localStorage.getItem('idadrs_api_url')
  || 'http://localhost:5000/api';

let state = {
  token: localStorage.getItem('idadrs_token') || null,
  user: JSON.parse(localStorage.getItem('idadrs_user') || 'null'),
  currentPage: 'dashboard',
  documents: [],
  categories: [],
  searchResults: [],
  uploadStep: 1,
  uploadFile: null,
  selectedDocId: null,
  sortPref: 'newest',
  filterCategoryId: null,
  docSearchQuery: '',
  reportsPage: 1,
  reportsTotalPages: 1,
  searchDebounced: null
};

const refs = {};

function pick(...ids) {
  for (const id of ids) {
    const el = document.getElementById(id);
    if (el) return el;
  }
  return null;
}

function ensureToastContainer() {
  let c = document.getElementById('toast-container');
  if (!c) {
    c = document.createElement('div');
    c.id = 'toast-container';
    document.body.appendChild(c);
  }
}

function decodeJwt(token) {
  try {
    const payload = token.split('.')[1];
    const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
    const json = decodeURIComponent(
      atob(normalized)
        .split('')
        .map((ch) => `%${(`00${ch.charCodeAt(0).toString(16)}`).slice(-2)}`)
        .join('')
    );
    const parsed = JSON.parse(json);
    const role = parsed.role || parsed['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || 'User';
    const username = parsed.unique_name || parsed.sub || parsed.name || parsed.username || 'User';
    const id = parsed.nameid || parsed.sub || parsed.userId || parsed.id || null;
    return { id, username, role, raw: parsed };
  } catch (_) {
    return null;
  }
}

function getAuthUser() {
  return state.user || { id: null, username: 'User', role: 'User' };
}

function showElement(el, shouldShow) {
  if (!el) return;
  el.classList.toggle('hidden', !shouldShow);
}

function clearNode(el) {
  if (!el) return;
  while (el.firstChild) el.removeChild(el.firstChild);
}

function setText(el, text) {
  if (el) el.textContent = text;
}

function createEl(tag, className, text) {
  const el = document.createElement(tag);
  if (className) el.className = className;
  if (typeof text === 'string') el.textContent = text;
  return el;
}

function getDocGrid() {
  return pick('doc-grid', 'docs-grid');
}

function getUploadModal() {
  return pick('upload-modal', 'modal-upload');
}

function getCatModal() {
  return pick('cat-modal', 'modal-cat');
}

function getDocModal() {
  return pick('doc-modal', 'modal-doc');
}

function getOverlay() {
  return pick('overlay');
}

function normalizeCategory(cat) {
  return {
    id: cat.id ?? cat.categoryId ?? cat.categoryID,
    name: cat.categoryName ?? cat.name ?? '',
    description: cat.description ?? '',
    documentCount: cat.documentCount ?? cat.documentsCount ?? cat.count ?? 0
  };
}

function normalizeDocument(doc) {
  return {
    id: doc.id ?? doc.documentId,
    title: doc.title ?? doc.fileName ?? 'Untitled',
    description: doc.description ?? '',
    categoryId: doc.categoryId ?? doc.categoryID ?? null,
    categoryName: doc.categoryName ?? doc.category?.categoryName ?? doc.category?.name ?? 'Uncategorized',
    uploader: doc.uploaderUsername ?? doc.createdBy ?? doc.userName ?? 'Unknown',
    uploaderId: doc.uploaderId ?? doc.userId ?? doc.createdById ?? null,
    uploadDate: doc.uploadDate ?? doc.createdAt ?? doc.dateUploaded ?? null,
    fileSize: Number(doc.fileSize ?? doc.size ?? doc.fileLength ?? 0),
    fileName: doc.fileName ?? doc.filePath ?? doc.title ?? '',
    relevanceScore: Number(doc.relevanceScore ?? doc.score ?? 0),
    raw: doc
  };
}

function formatBytes(bytes) {
  const b = Number(bytes || 0);
  if (b < 1024) return `${b.toFixed(1)} B`;
  if (b < 1048576) return `${(b / 1024).toFixed(1)} KB`;
  if (b < 1073741824) return `${(b / 1048576).toFixed(1)} MB`;
  return `${(b / 1073741824).toFixed(1)} GB`;
}

function formatDate(isoString) {
  if (!isoString) return '—';
  const date = new Date(isoString);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', year: 'numeric' }).format(date);
}

function debounce(fn, delay) {
  let timer = null;
  return (...args) => {
    if (timer) clearTimeout(timer);
    timer = setTimeout(() => fn(...args), delay);
  };
}

function getExtension(filename) {
  const value = String(filename || '').toLowerCase().trim();
  const idx = value.lastIndexOf('.');
  if (idx < 0) return '';
  return value.slice(idx + 1);
}

function getFileIcon(filename) {
  const ext = getExtension(filename);
  if (ext === 'pdf') return '<span class="doc-type-icon pdf">📄</span>';
  if (ext === 'docx' || ext === 'doc') return '<span class="doc-type-icon docx">📝</span>';
  if (ext === 'xlsx' || ext === 'xls') return '<span class="doc-type-icon xlsx">📊</span>';
  if (['png', 'jpg', 'jpeg', 'gif', 'webp'].includes(ext)) return '<span class="doc-type-icon image">🖼️</span>';
  return '<span class="doc-type-icon other">📁</span>';
}

function toast(message, type = 'success') {
  ensureToastContainer();
  const container = document.getElementById('toast-container');
  const item = document.createElement('div');
  item.className = `toast toast-${type}`;
  item.textContent = message;
  item.style.opacity = '0';
  item.style.transform = 'translateX(20px)';
  item.style.transition = 'all 0.15s ease';
  container.appendChild(item);
  setTimeout(() => {
    item.classList.add('show');
    item.style.opacity = '1';
    item.style.transform = 'translateX(0)';
  }, 10);
  setTimeout(() => {
    item.classList.remove('show');
    item.style.opacity = '0';
    item.style.transform = 'translateX(20px)';
    setTimeout(() => item.remove(), 300);
  }, 3000);
}

function setAuthIdentityUI() {
  const user = getAuthUser();
  setText(document.getElementById('header-username'), user.username || 'User');
  setText(document.getElementById('sidebar-username'), user.username || 'User');
  setText(document.getElementById('sidebar-role'), user.role || 'User');
  const avatar = document.getElementById('sidebar-avatar');
  if (avatar) avatar.textContent = (user.username || 'U').charAt(0).toUpperCase();
}

function showLanding() {
  const landing = pick('landing-screen', 'landing');
  const auth = pick('auth-screen');
  const app = pick('app-shell', 'app');
  showElement(landing, true);
  showElement(auth, false);
  showElement(app, false);
  state.token = null;
  state.user = null;
  localStorage.removeItem('idadrs_token');
  localStorage.removeItem('idadrs_user');
}

function switchTab(tab) {
  const login = tab === 'login';
  document.getElementById('tab-login')?.classList.toggle('active', login);
  document.getElementById('tab-register')?.classList.toggle('active', !login);
  showElement(document.getElementById('form-login'), login);
  showElement(document.getElementById('form-register'), !login);
}

function showAuth(tab = 'login') {
  const landing = pick('landing-screen', 'landing');
  const auth = pick('auth-screen');
  const app = pick('app-shell', 'app');
  showElement(landing, false);
  showElement(auth, true);
  showElement(app, false);
  switchTab(tab);
  const first = tab === 'register' ? document.getElementById('reg-username') : document.getElementById('login-username');
  if (first) setTimeout(() => first.focus(), 30);
}

function isAdmin() {
  return getAuthUser().role === 'Admin';
}

function isArchivist() {
  const role = getAuthUser().role;
  return role === 'Admin' || role === 'Archivist';
}

function setRoleBasedUI() {
  const usersNav = document.getElementById('nav-users');
  if (usersNav) usersNav.style.display = isAdmin() ? '' : 'none';
  const uploaderButtons = [document.getElementById('dash-upload-btn'), document.getElementById('doc-upload-btn')];
  uploaderButtons.forEach((btn) => {
    if (btn) btn.style.display = isArchivist() ? '' : 'none';
  });
  const catAdd = document.getElementById('cat-add-btn');
  if (catAdd) catAdd.style.display = isArchivist() ? '' : 'none';
  const exports = document.getElementById('export-btns');
  if (exports) exports.style.display = isArchivist() ? 'flex' : 'none';
}

function showApp() {
  if (!state.token) {
    showAuth('login');
    return;
  }
  const landing = pick('landing-screen', 'landing');
  const auth = pick('auth-screen');
  const app = pick('app-shell', 'app');
  showElement(landing, false);
  showElement(auth, false);
  showElement(app, true);
  setAuthIdentityUI();
  setRoleBasedUI();
  goPage(state.currentPage);
}

async function api(method, path, body = null, isFormData = false) {
  try {
    if (!state.token && !path.startsWith('/auth/')) {
      showAuth('login');
      throw new Error('Please sign in to continue');
    }
    const headers = {};
    if (!isFormData) headers['Content-Type'] = 'application/json';
    if (state.token) headers.Authorization = `Bearer ${state.token}`;
    const options = { method, headers };
    if (body) options.body = isFormData ? body : JSON.stringify(body);
    const res = await fetch(`${API}${path}`, options);
    const text = await res.text();
    const data = text ? JSON.parse(text) : { success: res.ok, data: null, message: '' };
    if (res.status === 401) {
      showAuth('login');
      throw new Error(data.message || 'Session expired');
    }
    if (!res.ok) throw new Error(data.message || 'Request failed');
    return data;
  } catch (error) {
    if (error instanceof SyntaxError) throw new Error('Invalid server response');
    throw error;
  }
}

async function login(username, password) {
  try {
    const response = await api('POST', '/auth/login', { username, password });
    const token = response.data?.token || response.data?.accessToken || response.token || response.accessToken;
    if (!token) throw new Error('Login succeeded but token is missing');
    state.token = token;
    localStorage.setItem('idadrs_token', token);
    const jwtUser = decodeJwt(token);
    const responseUser = response.data?.user || {};
    state.user = {
      id: jwtUser?.id ?? responseUser.id ?? response.data?.userId ?? null,
      username: jwtUser?.username ?? responseUser.username ?? username,
      role: jwtUser?.role ?? responseUser.role ?? response.data?.role ?? 'User'
    };
    localStorage.setItem('idadrs_user', JSON.stringify(state.user));
    showApp();
    toast('Logged in successfully', 'success');
  } catch (err) {
    toast(err.message, 'error');
  }
}

async function register(username, email, password) {
  try {
    await api('POST', '/auth/register', { username, email, password });
    toast('Account created — please sign in', 'success');
    showAuth('login');
    const u = document.getElementById('login-username');
    if (u) u.value = username;
  } catch (err) {
    toast(err.message, 'error');
  }
}

function logout() {
  state.token = null;
  state.user = null;
  localStorage.removeItem('idadrs_token');
  localStorage.removeItem('idadrs_user');
  showLanding();
}

async function goPage(page) {
  state.currentPage = page;
  document.querySelectorAll('.page').forEach((p) => p.classList.add('hidden'));
  const pageEl = document.getElementById(`page-${page}`);
  if (pageEl) pageEl.classList.remove('hidden');
  document.querySelectorAll('.nav-item').forEach((n) => n.classList.remove('active'));
  const nav = document.querySelector(`.nav-item[data-page="${page}"]`);
  if (nav) nav.classList.add('active');
  try {
    if (page === 'dashboard') await loadDashboard();
    if (page === 'documents') await loadDocuments();
    if (page === 'search') await initSearch();
    if (page === 'categories') await loadCategories();
    if (page === 'reports') await loadReports();
    if (page === 'users') await loadUsers();
  } catch (err) {
    toast(err.message, 'error');
  }
}

function buildTable(headers, rows) {
  const table = createEl('table', 'data-table');
  const thead = createEl('thead');
  const trh = createEl('tr');
  headers.forEach((h) => trh.appendChild(createEl('th', '', h)));
  thead.appendChild(trh);
  const tbody = createEl('tbody');
  rows.forEach((cells) => {
    const tr = createEl('tr');
    cells.forEach((cell) => {
      const td = createEl('td');
      if (cell instanceof Node) td.appendChild(cell);
      else td.textContent = String(cell ?? '');
      tr.appendChild(td);
    });
    tbody.appendChild(tr);
  });
  table.appendChild(thead);
  table.appendChild(tbody);
  return table;
}

async function loadDashboard() {
  try {
    const statDocs = pick('stat-total-docs', 's-docs');
    const statCats = pick('stat-categories', 's-cats');
    const statRecent = pick('stat-recent', 's-users');
    const statStorage = pick('stat-storage', 's-events');
    [statDocs, statCats, statRecent, statStorage].forEach((el) => {
      if (!el) return;
      el.innerHTML = '<span class="skeleton" style="display:inline-block;width:42px;height:24px;"></span>';
    });
    const [docsRes, catsRes] = await Promise.all([
      api('GET', '/documents'),
      api('GET', '/categories')
    ]);
    const usageRes = await api('GET', '/reports/usage').catch(() => ({ data: null }));
    state.documents = (docsRes.data || []).map(normalizeDocument);
    state.categories = (catsRes.data || []).map(normalizeCategory);
    const docs = state.documents;
    const cats = state.categories;
    const totalDocs = docs.length;
    const totalCats = cats.length;
    const sevenDaysAgo = new Date();
    sevenDaysAgo.setDate(sevenDaysAgo.getDate() - 7);
    const recent = docs.filter((d) => d.uploadDate && new Date(d.uploadDate) >= sevenDaysAgo).length;
    const storage = docs.reduce((sum, d) => sum + (d.fileSize || 0), 0);
    setText(statDocs, String(totalDocs));
    setText(statCats, String(totalCats));
    setText(statRecent, String(recent));
    setText(statStorage, formatBytes(storage));
    if (usageRes?.data && state.user?.role === 'Admin') {
      const fallback = usageRes.data.totalUsers ?? usageRes.data.totalAccessEvents;
      if (statRecent && !Number.isFinite(Number(statRecent.textContent))) setText(statRecent, String(fallback ?? recent));
    }
    const recentContainer = document.getElementById('recent-list');
    if (!recentContainer) return;
    clearNode(recentContainer);
    const sorted = [...docs].sort((a, b) => new Date(b.uploadDate || 0) - new Date(a.uploadDate || 0)).slice(0, 10);
    if (!sorted.length) {
      recentContainer.appendChild(createEl('div', 'empty', 'No recent uploads'));
      return;
    }
    const rows = sorted.map((doc) => {
      const title = createEl('button', 'btn-ghost', doc.title);
      title.style.padding = '0';
      title.style.color = 'var(--text)';
      title.addEventListener('click', () => openDocModal(doc.id));
      return [title, doc.categoryName, doc.uploader, formatDate(doc.uploadDate)];
    });
    recentContainer.appendChild(buildTable(['Title', 'Category', 'Uploader', 'Date'], rows));
  } catch (err) {
    toast(err.message, 'error');
  }
}

function renderCategoryPills() {
  const holder = document.getElementById('cat-pills');
  if (!holder) return;
  clearNode(holder);
  const makePill = (label, value) => {
    const pill = createEl('button', `cat-pill${state.filterCategoryId === value ? ' active' : ''}`, label);
    pill.type = 'button';
    pill.addEventListener('click', () => {
      state.filterCategoryId = value;
      renderCategoryPills();
      renderDocCards();
    });
    holder.appendChild(pill);
  };
  makePill('All', null);
  state.categories.forEach((cat) => makePill(cat.name, cat.id));
}

function getFilteredSortedDocuments() {
  let docs = [...state.documents];
  if (state.filterCategoryId !== null) docs = docs.filter((d) => String(d.categoryId) === String(state.filterCategoryId));
  const q = state.docSearchQuery.trim().toLowerCase();
  if (q) docs = docs.filter((d) => d.title.toLowerCase().includes(q));
  if (state.sortPref === 'newest') docs.sort((a, b) => new Date(b.uploadDate || 0) - new Date(a.uploadDate || 0));
  if (state.sortPref === 'oldest') docs.sort((a, b) => new Date(a.uploadDate || 0) - new Date(b.uploadDate || 0));
  if (state.sortPref === 'az') docs.sort((a, b) => a.title.localeCompare(b.title));
  if (state.sortPref === 'za') docs.sort((a, b) => b.title.localeCompare(a.title));
  if (state.sortPref === 'cat') docs.sort((a, b) => a.categoryName.localeCompare(b.categoryName) || a.title.localeCompare(b.title));
  return docs;
}

function renderDocCards(docsInput = null) {
  const grid = getDocGrid();
  if (!grid) return;
  clearNode(grid);
  const docs = docsInput ?? getFilteredSortedDocuments();
  const countLabel = document.getElementById('doc-count-label');
  if (countLabel) countLabel.textContent = `${docs.length} document${docs.length === 1 ? '' : 's'}`;
  if (!docs.length) {
    grid.appendChild(createEl('div', 'empty', 'No documents yet — upload your first file'));
    return;
  }
  docs.forEach((doc) => {
    const card = createEl('article', 'doc-card');
    card.innerHTML = `${getFileIcon(doc.fileName)}
      <h3 class="doc-title"></h3>
      <div class="doc-meta"><span class="doc-cat"></span><span class="doc-date"></span></div>`;
    card.querySelector('.doc-title').textContent = doc.title;
    card.querySelector('.doc-cat').textContent = doc.categoryName;
    card.querySelector('.doc-date').textContent = formatDate(doc.uploadDate);
    card.addEventListener('click', () => openDocModal(doc.id));
    grid.appendChild(card);
  });
}

async function loadDocuments() {
  try {
    const [docsRes, catsRes] = await Promise.all([api('GET', '/documents'), api('GET', '/categories')]);
    state.documents = (docsRes.data || []).map(normalizeDocument);
    state.categories = (catsRes.data || []).map(normalizeCategory);
    renderCategoryPills();
    const sort = document.getElementById('doc-sort');
    if (sort) sort.value = state.sortPref;
    renderDocCards();
  } catch (err) {
    toast(err.message, 'error');
  }
}

function resetUploadModalForm() {
  state.uploadStep = 1;
  state.uploadFile = null;
  state.selectedDocId = null;
  ['up-title', 'up-desc', 'up-file', 'up-error'].forEach((id) => {
    const el = document.getElementById(id);
    if (!el) return;
    if ('value' in el) el.value = '';
    el.classList.add('hidden');
  });
  const upError = document.getElementById('up-error');
  if (upError) {
    upError.classList.add('hidden');
    upError.textContent = '';
  }
  const preview = document.getElementById('file-preview');
  if (preview) {
    clearNode(preview);
    preview.classList.add('hidden');
  }
  const progressWrap = document.getElementById('upload-progress-wrap');
  const progressFill = document.getElementById('progress-fill');
  const progressLabel = document.getElementById('progress-label');
  if (progressWrap) progressWrap.classList.add('hidden');
  if (progressFill) progressFill.style.width = '0%';
  if (progressLabel) progressLabel.textContent = 'Uploading…';
}

function setUploadNavState() {
  const back = document.getElementById('up-back');
  const next = document.getElementById('up-next');
  if (back) back.style.display = state.uploadStep === 1 ? 'none' : '';
  if (next) {
    if (state.uploadStep === 1) {
      next.textContent = 'Next →';
      next.disabled = !state.uploadFile;
    } else if (state.uploadStep === 2) {
      next.textContent = 'Next →';
      next.disabled = false;
    } else {
      next.textContent = 'Upload';
      next.disabled = false;
    }
  }
}

function renderStepIndicators() {
  for (let i = 1; i <= 3; i += 1) {
    const ind = document.getElementById(`step${i}-ind`);
    if (!ind) continue;
    ind.classList.remove('active', 'completed');
    const span = ind.querySelector('span');
    if (i < state.uploadStep) {
      ind.classList.add('completed');
      if (span) span.textContent = '✓';
    } else if (i === state.uploadStep) {
      ind.classList.add('active');
      if (span) span.textContent = String(i);
    } else {
      if (span) span.textContent = String(i);
    }
  }
}

function renderUploadStep() {
  ['up-step1', 'up-step2', 'up-step3'].forEach((id, idx) => {
    const panel = document.getElementById(id);
    if (!panel) return;
    panel.classList.toggle('hidden', idx + 1 !== state.uploadStep);
  });
  renderStepIndicators();
  setUploadNavState();
  if (state.uploadStep === 2) {
    const catSelect = document.getElementById('up-cat');
    if (catSelect) {
      const current = catSelect.value;
      clearNode(catSelect);
      const first = createEl('option', '', 'Select a category…');
      first.value = '';
      catSelect.appendChild(first);
      state.categories.forEach((cat) => {
        const opt = createEl('option', '', cat.name);
        opt.value = String(cat.id);
        catSelect.appendChild(opt);
      });
      if (current) catSelect.value = current;
    }
  }
  if (state.uploadStep === 3) {
    setText(document.getElementById('conf-file'), state.uploadFile?.name || '—');
    setText(document.getElementById('conf-size'), state.uploadFile ? formatBytes(state.uploadFile.size) : '—');
    const title = document.getElementById('up-title')?.value.trim() || '—';
    const categoryId = document.getElementById('up-cat')?.value;
    const catName = state.categories.find((c) => String(c.id) === String(categoryId))?.name || '—';
    setText(document.getElementById('conf-title'), title);
    setText(document.getElementById('conf-cat'), catName);
  }
}

function validateUploadFile(file) {
  if (!file) return 'Please select a file';
  const ext = getExtension(file.name);
  const allowed = ['pdf', 'doc', 'docx', 'xls', 'xlsx', 'png', 'jpg', 'jpeg'];
  if (!allowed.includes(ext)) return 'Unsupported file type';
  if (file.size > 50 * 1024 * 1024) return 'Max file size is 50MB';
  return null;
}

function renderFilePreview() {
  const preview = document.getElementById('file-preview');
  if (!preview) return;
  clearNode(preview);
  if (!state.uploadFile) {
    preview.classList.add('hidden');
    setUploadNavState();
    return;
  }
  preview.classList.remove('hidden');
  const name = createEl('div', '', `${state.uploadFile.name} (${formatBytes(state.uploadFile.size)})`);
  const remove = createEl('button', 'btn-ghost', '✕');
  remove.type = 'button';
  remove.addEventListener('click', (e) => {
    e.stopPropagation();
    state.uploadFile = null;
    const inp = document.getElementById('up-file');
    if (inp) inp.value = '';
    renderFilePreview();
  });
  preview.appendChild(name);
  preview.appendChild(remove);
  setUploadNavState();
}

function wireDropZone() {
  const dz = document.getElementById('drop-zone');
  const fileInput = document.getElementById('up-file');
  if (!dz || !fileInput || refs.uploadWired) return;
  refs.uploadWired = true;
  dz.addEventListener('dragover', (e) => {
    e.preventDefault();
    dz.classList.add('drag-over');
  });
  dz.addEventListener('dragleave', () => dz.classList.remove('drag-over'));
  dz.addEventListener('drop', (e) => {
    e.preventDefault();
    dz.classList.remove('drag-over');
    const file = e.dataTransfer?.files?.[0];
    if (!file) return;
    const error = validateUploadFile(file);
    if (error) {
      toast(error, 'error');
      return;
    }
    state.uploadFile = file;
    renderFilePreview();
  });
  fileInput.addEventListener('change', () => {
    const file = fileInput.files?.[0];
    if (!file) return;
    const error = validateUploadFile(file);
    if (error) {
      toast(error, 'error');
      fileInput.value = '';
      state.uploadFile = null;
      renderFilePreview();
      return;
    }
    state.uploadFile = file;
    renderFilePreview();
  });
}

function openUploadModal() {
  resetUploadModalForm();
  const modal = getUploadModal();
  const overlay = getOverlay();
  showElement(overlay, true);
  showElement(modal, true);
  wireDropZone();
  renderUploadStep();
}

function openUpload() {
  openUploadModal();
}

async function quickCreateCat() {
  try {
    let inline = document.getElementById('quick-cat-form');
    if (!inline) {
      inline = createEl('div');
      inline.id = 'quick-cat-form';
      inline.style.display = 'flex';
      inline.style.gap = '8px';
      inline.style.marginTop = '8px';
      const input = createEl('input');
      input.id = 'quick-cat-input';
      input.placeholder = 'New category name';
      const btn = createEl('button', 'btn-primary', 'Add');
      btn.type = 'button';
      btn.addEventListener('click', async () => {
        try {
          const name = input.value.trim();
          if (name.length < 2) throw new Error('Category name must be at least 2 characters');
          await api('POST', '/categories', { categoryName: name, description: '' });
          const catsRes = await api('GET', '/categories');
          state.categories = (catsRes.data || []).map(normalizeCategory);
          renderUploadStep();
          input.value = '';
          toast('Category created', 'success');
        } catch (err) {
          toast(err.message, 'error');
        }
      });
      inline.appendChild(input);
      inline.appendChild(btn);
      document.getElementById('up-step2')?.appendChild(inline);
      input.focus();
    } else {
      inline.remove();
    }
  } catch (err) {
    toast(err.message, 'error');
  }
}

async function executeUpload() {
  try {
    if (!state.uploadFile) throw new Error('Select a file first');
    const title = document.getElementById('up-title')?.value.trim() || '';
    const categoryId = document.getElementById('up-cat')?.value || '';
    const description = document.getElementById('up-desc')?.value.trim() || '';
    if (title.length < 3) throw new Error('Title must be at least 3 characters');
    if (!categoryId) throw new Error('Select a category');
    const formData = new FormData();
    formData.append('file', state.uploadFile);
    formData.append('title', title);
    formData.append('categoryId', categoryId);
    formData.append('description', description);
    const progressWrap = document.getElementById('upload-progress-wrap');
    const progressFill = document.getElementById('progress-fill');
    const progressLabel = document.getElementById('progress-label');
    if (progressWrap) progressWrap.classList.remove('hidden');
    const xhrResult = await new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      xhr.open('POST', `${API}/documents`);
      if (state.token) xhr.setRequestHeader('Authorization', `Bearer ${state.token}`);
      xhr.upload.onprogress = (evt) => {
        if (!evt.lengthComputable) return;
        const pct = Math.round((evt.loaded / evt.total) * 100);
        if (progressFill) progressFill.style.width = `${pct}%`;
        if (progressLabel) progressLabel.textContent = `Uploading... ${pct}%`;
      };
      xhr.onload = () => {
        try {
          const data = xhr.responseText ? JSON.parse(xhr.responseText) : { success: xhr.status < 300, data: null, message: '' };
          if (xhr.status >= 200 && xhr.status < 300) resolve(data);
          else reject(new Error(data.message || 'Upload failed'));
        } catch (_) {
          reject(new Error('Upload failed'));
        }
      };
      xhr.onerror = () => reject(new Error('Network error during upload'));
      xhr.send(formData);
    });
    if (!xhrResult.success && xhrResult.success !== undefined) throw new Error(xhrResult.message || 'Upload failed');
    closeModal('upload');
    toast('Document uploaded', 'success');
    await loadDocuments();
    if (state.currentPage !== 'documents') goPage('documents');
  } catch (err) {
    toast(err.message, 'error');
  }
}

async function wizardNav(direction) {
  try {
    if (direction === 'next') {
      if (state.uploadStep === 1) {
        if (!state.uploadFile) throw new Error('Choose a file before continuing');
      }
      if (state.uploadStep === 2) {
        const title = document.getElementById('up-title')?.value.trim() || '';
        if (title.length < 3) throw new Error('Title must be at least 3 characters');
        if (!document.getElementById('up-cat')?.value) throw new Error('Please select a category');
      }
      if (state.uploadStep === 3) {
        await executeUpload();
        return;
      }
      state.uploadStep += 1;
    } else {
      state.uploadStep = Math.max(1, state.uploadStep - 1);
    }
    renderUploadStep();
  } catch (err) {
    toast(err.message, 'error');
  }
}

function upNext() {
  wizardNav('next');
}

function upPrev() {
  wizardNav('prev');
}

function fileChosen(input) {
  try {
    const file = input?.files?.[0];
    if (!file) return;
    const error = validateUploadFile(file);
    if (error) {
      toast(error, 'error');
      input.value = '';
      return;
    }
    state.uploadFile = file;
    renderFilePreview();
  } catch (err) {
    toast(err.message, 'error');
  }
}

async function initSearch() {
  try {
    if (!state.categories.length) {
      const cats = await api('GET', '/categories');
      state.categories = (cats.data || []).map(normalizeCategory);
    }
    const select = document.getElementById('sf-cat');
    if (select) {
      const current = select.value;
      clearNode(select);
      const all = createEl('option', '', 'All Categories');
      all.value = '';
      select.appendChild(all);
      state.categories.forEach((cat) => {
        const opt = createEl('option', '', cat.name);
        opt.value = String(cat.id);
        select.appendChild(opt);
      });
      if (current) select.value = current;
    }
    const q = document.getElementById('sq');
    if (q && !refs.searchWired) {
      refs.searchWired = true;
      state.searchDebounced = debounce(() => doSearch(), 300);
      q.addEventListener('input', state.searchDebounced);
    }
  } catch (err) {
    toast(err.message, 'error');
  }
}

async function doSearch() {
  try {
    const q = document.getElementById('sq')?.value.trim() || '';
    const cat = document.getElementById('sf-cat')?.value || '';
    const from = document.getElementById('sf-from')?.value || '';
    const to = document.getElementById('sf-to')?.value || '';
    if (!q) {
      renderSearchResults([]);
      return;
    }
    const resultHolder = document.getElementById('search-results');
    if (resultHolder) {
      clearNode(resultHolder);
      for (let i = 0; i < 3; i += 1) {
        const s = createEl('div', 'skeleton');
        s.style.height = '72px';
        resultHolder.appendChild(s);
      }
    }
    const params = new URLSearchParams({ query: q });
    if (cat) params.set('categoryId', cat);
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    const res = await api('GET', `/documents/search?${params.toString()}`);
    state.searchResults = (res.data || []).map(normalizeDocument);
    renderSearchResults(state.searchResults);
  } catch (err) {
    toast(err.message, 'error');
  }
}

function runSearch() {
  doSearch();
}

function renderSearchResults(results) {
  const holder = document.getElementById('search-results');
  if (!holder) return;
  clearNode(holder);
  const sorted = [...results].sort((a, b) => b.relevanceScore - a.relevanceScore);
  if (!sorted.length) {
    holder.appendChild(createEl('div', 'empty', 'No documents match your search'));
    return;
  }
  sorted.forEach((doc) => {
    const row = createEl('article', 'result-item');
    row.innerHTML = `${getFileIcon(doc.fileName)}<div class="result-main"><h3 class="result-title"></h3><p class="result-meta"></p></div>`;
    row.querySelector('.result-title').textContent = doc.title;
    row.querySelector('.result-meta').textContent = `${doc.categoryName} • ${formatDate(doc.uploadDate)}`;
    const score = createEl('span', 'score-badge');
    const value = Number.isFinite(doc.relevanceScore) ? doc.relevanceScore : 0;
    score.textContent = value.toFixed(2);
    if (value > 0.7) score.classList.add('score-high');
    else if (value > 0.4) score.classList.add('score-medium');
    else score.classList.add('score-low');
    const btn = createEl('button', 'btn-outline', 'View');
    btn.type = 'button';
    btn.addEventListener('click', () => openDocModal(doc.id));
    row.appendChild(score);
    row.appendChild(btn);
    holder.appendChild(row);
  });
}

async function requestSignedDownloadUrl(docId) {
  try {
    const a = await api('GET', `/documents/${docId}/download`);
    return a.data?.url || a.data?.downloadUrl || a.url || a.downloadUrl || null;
  } catch (_) {
    return null;
  }
}

function canDeleteDocument(doc) {
  const user = getAuthUser();
  if (user.role === 'Admin') return true;
  return user.id !== null && String(doc.uploaderId) === String(user.id);
}

async function openDocModal(docId) {
  try {
    const res = await api('GET', `/documents/${docId}`);
    const doc = normalizeDocument(res.data || {});
    state.selectedDocId = doc.id;
    const modal = getDocModal();
    const body = document.getElementById('doc-detail-body');
    if (!modal || !body) return;
    clearNode(body);
    const title = document.getElementById('doc-modal-title');
    if (title) title.textContent = doc.title;
    const desc = createEl('p', '', doc.description || 'No description');
    desc.style.margin = '0';
    desc.style.color = 'var(--muted)';
    const meta = createEl('div', 'meta-grid');
    const items = [
      ['Category', doc.categoryName],
      ['Uploader', doc.uploader],
      ['Uploaded', formatDate(doc.uploadDate)],
      ['File size', formatBytes(doc.fileSize)]
    ];
    items.forEach(([k, v]) => {
      const item = createEl('div', 'meta-item');
      const key = createEl('label', '', k);
      const val = createEl('strong', '', String(v));
      item.appendChild(key);
      item.appendChild(val);
      meta.appendChild(item);
    });
    const actions = createEl('div');
    actions.style.display = 'flex';
    actions.style.gap = '8px';
    const download = createEl('button', 'btn-primary', 'Download');
    download.type = 'button';
    download.addEventListener('click', async () => {
      try {
        const url = await requestSignedDownloadUrl(doc.id);
        if (!url) throw new Error('Failed to get download URL');
        window.open(url, '_blank', 'noopener');
      } catch (err) {
        toast(err.message, 'error');
      }
    });
    actions.appendChild(download);
    if (canDeleteDocument(doc)) {
      const del = createEl('button', 'btn-danger', 'Delete');
      del.type = 'button';
      del.addEventListener('click', async () => {
        try {
          if (!confirm('Delete this document?')) return;
          await api('DELETE', `/documents/${doc.id}`);
          closeModal('doc');
          toast('Document deleted', 'success');
          await loadDocuments();
        } catch (err) {
          toast(err.message, 'error');
        }
      });
      actions.appendChild(del);
    }
    body.appendChild(desc);
    body.appendChild(meta);
    body.appendChild(actions);
    showElement(getOverlay(), true);
    showElement(modal, true);
  } catch (err) {
    toast(err.message, 'error');
  }
}

async function loadCategories() {
  try {
    const res = await api('GET', '/categories');
    state.categories = (res.data || []).map(normalizeCategory);
    const grid = document.getElementById('cats-grid');
    if (!grid) return;
    clearNode(grid);
    if (!state.categories.length) {
      grid.appendChild(createEl('div', 'empty', 'No categories yet'));
      return;
    }
    state.categories.forEach((cat) => {
      const card = createEl('article', 'cat-card');
      const name = createEl('h3', '', cat.name);
      const desc = createEl('p', '', cat.description || 'No description');
      desc.className = 'page-sub';
      const count = createEl('span', 'cat-pill', `${cat.documentCount} docs`);
      count.classList.remove('active');
      card.appendChild(name);
      card.appendChild(desc);
      card.appendChild(count);
      if (isArchivist()) {
        const actions = createEl('div');
        actions.style.display = 'flex';
        actions.style.gap = '8px';
        actions.style.marginTop = '10px';
        const edit = createEl('button', 'btn-outline', 'Edit');
        edit.type = 'button';
        edit.addEventListener('click', () => openCatModal(cat));
        const del = createEl('button', 'btn-danger', 'Delete');
        del.type = 'button';
        del.addEventListener('click', () => deleteCat(cat.id));
        actions.appendChild(edit);
        actions.appendChild(del);
        card.appendChild(actions);
      }
      grid.appendChild(card);
    });
  } catch (err) {
    toast(err.message, 'error');
  }
}

function openCatModal(cat = null) {
  const title = document.getElementById('cat-modal-title');
  const id = document.getElementById('cat-edit-id');
  const name = document.getElementById('cat-name');
  const desc = document.getElementById('cat-desc');
  const error = document.getElementById('cat-error');
  if (title) title.textContent = cat ? 'Edit category' : 'New category';
  if (id) id.value = cat?.id ? String(cat.id) : '';
  if (name) name.value = cat?.name || '';
  if (desc) desc.value = cat?.description || '';
  if (error) {
    error.textContent = '';
    error.classList.add('hidden');
  }
  showElement(getOverlay(), true);
  showElement(getCatModal(), true);
}

async function saveCat() {
  try {
    const id = document.getElementById('cat-edit-id')?.value || '';
    const name = document.getElementById('cat-name')?.value.trim() || '';
    const description = document.getElementById('cat-desc')?.value.trim() || '';
    if (name.length < 2) throw new Error('Category name must be at least 2 characters');
    if (id) await api('PUT', `/categories/${id}`, { categoryName: name, description });
    else await api('POST', '/categories', { categoryName: name, description });
    await loadCategories();
    closeModal('cat');
    toast(id ? 'Category updated' : 'Category created', 'success');
  } catch (err) {
    const errBox = document.getElementById('cat-error');
    if (errBox) {
      errBox.textContent = err.message;
      errBox.classList.remove('hidden');
    } else {
      toast(err.message, 'error');
    }
  }
}

async function deleteCat(id) {
  try {
    if (!confirm('Delete this category?')) return;
    await api('DELETE', `/categories/${id}`);
    await loadCategories();
    toast('Category deleted', 'success');
  } catch (err) {
    if (/document/i.test(err.message)) toast('Cannot delete — category has documents', 'error');
    else toast(err.message, 'error');
  }
}

async function loadReports(page = 1) {
  try {
    state.reportsPage = page;
    const body = document.getElementById('audit-body');
    const pages = document.getElementById('audit-pages');
    if (body) body.innerHTML = '<div class="skeleton" style="height:160px;"></div>';
    const res = await api('GET', `/reports/audit?page=${page}&pageSize=20`);
    const payload = res.data || {};
    const list = payload.items || payload.entries || payload.records || [];
    const total = payload.totalPages || Math.ceil((payload.totalCount || list.length || 1) / 20) || 1;
    state.reportsTotalPages = total;
    if (body) {
      clearNode(body);
      if (!list.length) {
        body.appendChild(createEl('div', 'empty', 'No audit data'));
      } else {
        const rows = list.map((it) => [
          it.userId ?? it.username ?? '—',
          it.documentId ?? '—',
          it.action ?? it.actionType ?? '—',
          formatDate(it.date ?? it.accessDate ?? it.createdAt)
        ]);
        body.appendChild(buildTable(['UserId', 'DocumentId', 'Action', 'Date'], rows));
      }
    }
    if (pages) {
      clearNode(pages);
      const prev = createEl('button', 'btn-outline', 'Prev');
      prev.type = 'button';
      prev.disabled = page <= 1;
      prev.addEventListener('click', () => loadReports(page - 1));
      const next = createEl('button', 'btn-outline', 'Next');
      next.type = 'button';
      next.disabled = page >= total;
      next.addEventListener('click', () => loadReports(page + 1));
      const label = createEl('span', '', `Page ${page} of ${total}`);
      pages.appendChild(prev);
      pages.appendChild(label);
      pages.appendChild(next);
    }
  } catch (err) {
    toast(err.message, 'error');
  }
}

async function exportReport(type) {
  try {
    if (!state.token) throw new Error('Please sign in');
    const res = await fetch(`${API}/reports/export/${type}`, {
      method: 'GET',
      headers: { Authorization: `Bearer ${state.token}` }
    });
    if (!res.ok) {
      let msg = 'Export failed';
      try {
        const data = await res.json();
        msg = data.message || msg;
      } catch (_) {}
      throw new Error(msg);
    }
    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `idadrs-report.${type === 'excel' ? 'csv' : 'pdf'}`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
    toast('Downloading report...', 'success');
  } catch (err) {
    toast(err.message, 'error');
  }
}

async function loadUsers() {
  try {
    const body = document.getElementById('users-body');
    if (!body) return;
    clearNode(body);
    if (!isAdmin()) {
      body.appendChild(createEl('div', 'empty', 'Access denied'));
      return;
    }
    const res = await api('GET', '/users');
    const users = res.data || [];
    if (!users.length) {
      body.appendChild(createEl('div', 'empty', 'No users found'));
      return;
    }
    const table = createEl('table', 'data-table');
    table.innerHTML = '<thead><tr><th>Username</th><th>Email</th><th>Role</th><th>Created Date</th><th>Actions</th></tr></thead>';
    const tbody = createEl('tbody');
    users.forEach((u) => {
      const tr = createEl('tr');
      [u.username, u.email, u.role, formatDate(u.createdDate || u.createdAt)].forEach((v) => tr.appendChild(createEl('td', '', String(v ?? ''))));
      const actionTd = createEl('td');
      const sel = createEl('select');
      ['User', 'Archivist', 'Admin'].forEach((role) => {
        const opt = createEl('option', '', role);
        opt.value = role;
        if (role === u.role) opt.selected = true;
        sel.appendChild(opt);
      });
      sel.addEventListener('change', () => changeRole(u.id, sel.value));
      const del = createEl('button', 'btn-danger', 'Delete');
      del.type = 'button';
      del.style.marginLeft = '8px';
      del.addEventListener('click', () => deleteUser(u.id));
      actionTd.appendChild(sel);
      actionTd.appendChild(del);
      tr.appendChild(actionTd);
      tbody.appendChild(tr);
    });
    table.appendChild(tbody);
    body.appendChild(table);
  } catch (err) {
    toast(err.message, 'error');
  }
}

async function changeRole(userId, newRole) {
  try {
    await api('PUT', `/users/${userId}`, { role: newRole });
    toast('Role updated', 'success');
    await loadUsers();
  } catch (err) {
    toast(err.message, 'error');
  }
}

async function deleteUser(userId) {
  try {
    const me = getAuthUser();
    if (String(me.id) === String(userId)) throw new Error('You cannot delete your own account');
    if (!confirm('Delete this user?')) return;
    await api('DELETE', `/users/${userId}`);
    toast('User deleted', 'success');
    await loadUsers();
  } catch (err) {
    toast(err.message, 'error');
  }
}

function closeModal(modalId) {
  const overlay = getOverlay();
  const upload = getUploadModal();
  const cat = getCatModal();
  const doc = getDocModal();
  if (!modalId) {
    [upload, cat, doc].forEach((m) => showElement(m, false));
  } else if (modalId === 'upload') showElement(upload, false);
  else if (modalId === 'cat') showElement(cat, false);
  else if (modalId === 'doc') showElement(doc, false);
  else {
    const byId = pick(modalId, `modal-${modalId}`);
    showElement(byId, false);
  }
  const anyVisible = [upload, cat, doc].some((m) => m && !m.classList.contains('hidden'));
  showElement(overlay, anyVisible);
  document.getElementById('drop-zone')?.classList.remove('drag-over');
}

function closeModals() {
  closeModal();
}

function applyDocFilters() {
  state.docSearchQuery = document.getElementById('doc-search')?.value || '';
  state.sortPref = document.getElementById('doc-sort')?.value || state.sortPref;
  renderDocCards();
}

function wireStaticHandlers() {
  document.querySelectorAll('.nav-item[data-page]').forEach((n) => {
    n.addEventListener('click', (e) => {
      e.preventDefault();
      const page = n.getAttribute('data-page');
      if (page) goPage(page);
    });
  });
  document.getElementById('login-btn')?.addEventListener('click', async (e) => {
    e.preventDefault();
    await login(
      document.getElementById('login-username')?.value.trim() || '',
      document.getElementById('login-password')?.value || ''
    );
  });
  document.getElementById('reg-btn')?.addEventListener('click', async (e) => {
    e.preventDefault();
    await register(
      document.getElementById('reg-username')?.value.trim() || '',
      document.getElementById('reg-email')?.value.trim() || '',
      document.getElementById('reg-password')?.value || ''
    );
  });
  document.getElementById('form-login')?.addEventListener('submit', async (e) => {
    e.preventDefault();
    await login(
      document.getElementById('login-username')?.value.trim() || '',
      document.getElementById('login-password')?.value || ''
    );
  });
  document.getElementById('form-register')?.addEventListener('submit', async (e) => {
    e.preventDefault();
    await register(
      document.getElementById('reg-username')?.value.trim() || '',
      document.getElementById('reg-email')?.value.trim() || '',
      document.getElementById('reg-password')?.value || ''
    );
  });
  document.getElementById('doc-search')?.addEventListener('input', applyDocFilters);
  document.getElementById('doc-sort')?.addEventListener('change', () => {
    state.sortPref = document.getElementById('doc-sort')?.value || 'newest';
    renderDocCards();
  });
  document.getElementById('up-back')?.addEventListener('click', () => upPrev());
  document.getElementById('up-next')?.addEventListener('click', () => upNext());
  document.getElementById('upload-btn')?.addEventListener('click', () => openUploadModal());
  document.getElementById('logout-btn')?.addEventListener('click', () => logout());
  document.querySelector('.auth-back')?.addEventListener('click', () => showLanding());
  document.querySelectorAll('[onclick*="showAuth(\'register\')"]').forEach((el) => {
    el.addEventListener('click', (e) => {
      e.preventDefault();
      showAuth('register');
    });
  });
  document.querySelectorAll('[onclick*="showAuth(\'login\')"]').forEach((el) => {
    el.addEventListener('click', (e) => {
      e.preventDefault();
      showAuth('login');
    });
  });
  document.getElementById('tab-login')?.addEventListener('click', () => switchTab('login'));
  document.getElementById('tab-register')?.addEventListener('click', () => switchTab('register'));
  document.getElementById('sq')?.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      doSearch();
    }
  });
  document.getElementById('sf-cat')?.addEventListener('change', () => doSearch());
  document.getElementById('sf-from')?.addEventListener('change', () => doSearch());
  document.getElementById('sf-to')?.addEventListener('change', () => doSearch());
  getOverlay()?.addEventListener('click', () => closeModal());
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') closeModal();
  });
}

function bootstrap() {
  ensureToastContainer();
  wireStaticHandlers();
  const catForm = getCatModal()?.querySelector('form');
  if (catForm) {
    catForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      await saveCat();
    });
  }
  const user = state.user || decodeJwt(state.token || '');
  if (user) {
    state.user = user;
    localStorage.setItem('idadrs_user', JSON.stringify(state.user));
  }
  if (state.token) showApp();
  else showLanding();
}

window.showLanding = showLanding;
window.showAuth = showAuth;
window.showApp = showApp;
window.switchTab = switchTab;
window.goPage = goPage;
window.api = api;
window.login = login;
window.register = register;
window.logout = logout;
window.loadDashboard = loadDashboard;
window.loadDocuments = loadDocuments;
window.renderDocCards = renderDocCards;
window.openUploadModal = openUploadModal;
window.openUpload = openUpload;
window.wizardNav = wizardNav;
window.upNext = upNext;
window.upPrev = upPrev;
window.executeUpload = executeUpload;
window.quickCreateCat = quickCreateCat;
window.fileChosen = fileChosen;
window.initSearch = initSearch;
window.doSearch = doSearch;
window.runSearch = runSearch;
window.renderSearchResults = renderSearchResults;
window.openDocModal = openDocModal;
window.loadCategories = loadCategories;
window.openCatModal = openCatModal;
window.saveCat = saveCat;
window.submitCat = async (e) => {
  if (e) e.preventDefault();
  await saveCat();
};
window.deleteCat = deleteCat;
window.loadReports = loadReports;
window.exportReport = exportReport;
window.loadUsers = loadUsers;
window.changeRole = changeRole;
window.deleteUser = deleteUser;
window.toast = toast;
window.formatBytes = formatBytes;
window.formatDate = formatDate;
window.debounce = debounce;
window.getFileIcon = getFileIcon;
window.closeModal = closeModal;
window.closeModals = closeModals;
window.openUpload = openUploadModal;
window.upNext = () => wizardNav('next');
window.upPrev = () => wizardNav('prev');

document.addEventListener('DOMContentLoaded', bootstrap);
