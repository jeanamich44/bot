document.addEventListener('DOMContentLoaded', () => {
    let authToken = localStorage.getItem('admin_auth_token') || '';

    const loginView = document.getElementById('login-view');
    const appView = document.getElementById('app-view');
    const loginForm = document.getElementById('login-form');
    const tokenInput = document.getElementById('token-input');
    const logoutBtn = document.getElementById('logout-btn');
    const navItems = document.querySelectorAll('.nav-item');
    const tabContents = document.querySelectorAll('.tab-content');
    const pageTitleHeading = document.getElementById('page-title-heading');

    const modalBackdrop = document.getElementById('custom-modal-backdrop');
    const modalTitle = document.getElementById('modal-title');
    const modalBodyContent = document.getElementById('modal-body-content');
    const modalCloseBtn = document.getElementById('modal-close-btn');
    const modalCancelBtn = document.getElementById('modal-cancel-btn');
    const modalConfirmBtn = document.getElementById('modal-confirm-btn');
    let modalOnConfirmCallback = null;

    function showToast(message, type = 'success') {
        const container = document.getElementById('toast-container');
        const toast = document.createElement('div');
        toast.className = `toast toast-${type}`;
        toast.innerText = message;
        container.appendChild(toast);
        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transform = 'translateX(30px)';
            toast.style.transition = 'all 0.3s ease';
            setTimeout(() => toast.remove(), 300);
        }, 3500);
    }

    function openModal(title, htmlContent, onConfirm) {
        modalTitle.innerText = title;
        modalBodyContent.innerHTML = htmlContent;
        modalOnConfirmCallback = onConfirm;
        modalBackdrop.classList.add('active');
    }

    function closeModal() {
        modalBackdrop.classList.remove('active');
        modalOnConfirmCallback = null;
    }

    modalCloseBtn.addEventListener('click', closeModal);
    modalCancelBtn.addEventListener('click', closeModal);
    modalConfirmBtn.addEventListener('click', async () => {
        if (modalOnConfirmCallback) {
            await modalOnConfirmCallback();
        }
        closeModal();
    });

    async function apiRequest(endpoint, method = 'GET', body = null) {
        const headers = {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${authToken}`
        };
        const options = { method, headers };
        if (body) options.body = JSON.stringify(body);

        try {
            const response = await fetch(`/api/admin${endpoint}`, options);
            if (response.status === 401) {
                logout();
                showToast('Session expirée ou clé invalide.', 'danger');
                return null;
            }
            return await response.json();
        } catch (err) {
            showToast('Erreur de connexion au serveur API', 'danger');
            return null;
        }
    }

    loginForm.addEventListener('submit', async (e) => {
        e.preventDefault();
        const pwd = tokenInput.value.trim();
        if (!pwd) return;

        authToken = pwd;
        const res = await apiRequest('/login', 'POST', { password: pwd });
        if (res && res.success) {
            localStorage.setItem('admin_auth_token', pwd);
            showToast('Connexion réussie !', 'success');
            initApp();
        } else {
            authToken = '';
            showToast('Mot de passe ou token incorrect.', 'danger');
        }
    });

    function logout() {
        authToken = '';
        localStorage.removeItem('admin_auth_token');
        appView.style.display = 'none';
        loginView.style.display = 'flex';
    }

    logoutBtn.addEventListener('click', logout);

    function initApp() {
        loginView.style.display = 'none';
        appView.style.display = 'flex';

        const hash = (window.location.hash || '').replace('#', '').trim();
        const savedTab = localStorage.getItem('admin_active_tab') || 'dashboard';
        const validTabs = ['dashboard', 'users', 'stock', 'payments', 'transactions', 'settings'];
        const activeTab = validTabs.includes(hash) ? hash : (validTabs.includes(savedTab) ? savedTab : 'dashboard');

        window.switchTab(activeTab);
    }

    if (authToken) {
        initApp();
    }

    navItems.forEach(item => {
        item.addEventListener('click', (e) => {
            e.preventDefault();
            const tab = item.getAttribute('data-tab');

            localStorage.setItem('admin_active_tab', tab);
            window.location.hash = tab;
            
            navItems.forEach(i => i.classList.remove('active'));
            item.classList.add('active');

            tabContents.forEach(content => content.style.display = 'none');
            const target = document.getElementById(`tab-${tab}`);
            if (target) target.style.display = 'block';

            const titleMap = {
                'dashboard': 'Vue d\'Ensemble',
                'users': 'Gestion des Utilisateurs',
                'stock': 'Gestion du Stock Carrefour',
                'payments': 'Rechargements (CB & Crypto)',
                'transactions': 'Historique des Achats',
                'settings': 'Configuration du Bot'
            };
            pageTitleHeading.innerText = titleMap[tab] || 'Administration';

            if (tab === 'dashboard') loadDashboardData();
            else if (tab === 'users') loadUsersData();
            else if (tab === 'stock') loadStockData();
            else if (tab === 'payments') loadPaymentsData();
            else if (tab === 'transactions') loadTransactionsData();
            else if (tab === 'settings') loadSettingsData();
        });
    });

    window.switchTab = function(tabName) {
        const targetNav = document.querySelector(`.nav-item[data-tab="${tabName}"]`);
        if (targetNav) {
            targetNav.click();
        }
    };

    let currentMaintenanceState = false;
    const maintenanceBadge = document.getElementById('maintenance-badge');
    const toggleMaintenanceBtn = document.getElementById('toggle-maintenance-btn');

    function updateMaintenanceUI(isMtn) {
        currentMaintenanceState = isMtn;
        if (!maintenanceBadge) return;
        if (isMtn) {
            maintenanceBadge.className = 'badge badge-danger';
            maintenanceBadge.innerText = '🛠️ Maintenance Active';
        } else {
            maintenanceBadge.className = 'badge badge-success';
            maintenanceBadge.innerText = '🟢 Mode Normal';
        }
    }

    if (toggleMaintenanceBtn) {
        toggleMaintenanceBtn.addEventListener('click', () => {
            const nextState = !currentMaintenanceState;
            openModal('Mode Maintenance', `<p>Voulez-vous <strong>${nextState ? 'ACTIVER' : 'DÉSACTIVER'}</strong> le mode maintenance ?<br><br><small style="color: var(--text-secondary);">Toutes les actions Telegram non-admin seront immédiatement bloquées.</small></p>`, async () => {
                const res = await apiRequest('/maintenance', 'POST', { maintenance: nextState });
                if (res && res.success) {
                    updateMaintenanceUI(res.maintenance);
                    showToast(`Mode maintenance ${res.maintenance ? 'activé 🔴' : 'désactivé 🟢'} !`, res.maintenance ? 'danger' : 'success');
                }
            });
        });
    }

    async function loadDashboardData() {
        const stats = await apiRequest('/stats');
        if (!stats) return;

        updateMaintenanceUI(stats.maintenance);

        document.getElementById('stat-total-ca').innerText = `${stats.totalCa.toFixed(2)} €`;
        document.getElementById('stat-total-sales').innerText = stats.totalSales;
        document.getElementById('stat-total-users').innerText = stats.totalUsers;
        document.getElementById('stat-total-stock').innerText = stats.totalStock;

        function formatParisDate(dateStr) {
            if (!dateStr) return '';
            const d = new Date(dateStr);
            if (isNaN(d.getTime())) return dateStr;
            const pad = n => n.toString().padStart(2, '0');
            return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
        }

        const tbody = document.getElementById('recent-sales-table');
        tbody.innerHTML = '';
        (stats.recentSales || []).forEach(tx => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td>#${tx.id}</td>
                <td><code>${tx.userId}</code></td>
                <td>${tx.brand}</td>
                <td><strong>${tx.price} €</strong></td>
                <td>${formatParisDate(tx.createdAt)}</td>
            `;
            tbody.appendChild(tr);
        });
    }

    let allUsers = [];
    async function loadUsersData() {
        const data = await apiRequest('/users');
        if (!data) return;

        allUsers = data.users || [];
        renderUsersTable(allUsers);
    }

    function renderUsersTable(users) {
        const tbody = document.getElementById('users-table');
        tbody.innerHTML = '';
        users.forEach(user => {
            const tr = document.createElement('tr');
            const statusBadge = user.isBanned 
                ? `<span class="badge badge-danger">Banni</span>` 
                : `<span class="badge badge-success">Actif</span>`;
            
            tr.innerHTML = `
                <td><code>${user.id}</code></td>
                <td><strong>${user.solde.toFixed(2)} €</strong></td>
                <td>${user.achats}</td>
                <td>${statusBadge}</td>
                <td>${user.banReason || '-'}</td>
                <td>
                    <button class="action-btn" onclick="btnEditSolde('${user.id}', ${user.solde})">💳 Solde</button>
                    ${user.isBanned 
                        ? `<button class="action-btn action-btn-danger" onclick="btnDebanUser('${user.id}')">Débannir</button>` 
                        : `<button class="action-btn action-btn-danger" onclick="btnBanUser('${user.id}')">Bannir</button>`}
                </td>
            `;
            tbody.appendChild(tr);
        });
    }

    document.getElementById('user-search-input').addEventListener('input', (e) => {
        const q = e.target.value.trim().toLowerCase();
        if (!q) {
            renderUsersTable(allUsers);
        } else {
            const filtered = allUsers.filter(u => String(u.id).includes(q));
            renderUsersTable(filtered);
        }
    });

    window.btnEditSolde = (userId, currentSolde) => {
        const html = `
            <p style="margin-bottom: 12px; color: var(--text-secondary);">Modifier le solde de l'utilisateur <code>${userId}</code> (Solde actuel: ${currentSolde}€) :</p>
            <div class="form-group">
                <label class="form-label">Action</label>
                <select id="modal-solde-action" class="form-input">
                    <option value="add">Ajouter (+)</option>
                    <option value="remove">Retirer (-)</option>
                </select>
            </div>
            <div class="form-group">
                <label class="form-label">Montant (€)</label>
                <input type="number" step="0.1" id="modal-solde-amount" class="form-input" placeholder="Ex: 10" required>
            </div>
        `;
        openModal(`Gestion Solde ${userId}`, html, async () => {
            const act = document.getElementById('modal-solde-action').value;
            const amt = parseFloat(document.getElementById('modal-solde-amount').value);
            if (isNaN(amt) || amt <= 0) {
                showToast('Montant invalide', 'danger');
                return;
            }
            const res = await apiRequest('/users/solde', 'POST', { userId, action: act, amount: amt });
            if (res && res.success) {
                showToast('Solde mis à jour avec succès', 'success');
                loadUsersData();
            }
        });
    };

    window.btnBanUser = (userId) => {
        const html = `
            <p style="margin-bottom: 12px; color: var(--text-secondary);">Bannir l'utilisateur <code>${userId}</code> :</p>
            <div class="form-group">
                <label class="form-label">Raison du bannissement (Optionnel)</label>
                <input type="text" id="modal-ban-reason" class="form-input" placeholder="Ex: Spam / Arnaque">
            </div>
        `;
        openModal(`Bannir ${userId}`, html, async () => {
            const reason = document.getElementById('modal-ban-reason').value.trim();
            const res = await apiRequest('/users/ban', 'POST', { userId, ban: true, reason });
            if (res && res.success) {
                showToast('Utilisateur banni', 'success');
                loadUsersData();
            }
        });
    };

    window.btnDebanUser = (userId) => {
        openModal(`Débannir ${userId}`, `<p>Confirmer le débannissement de l'utilisateur <code>${userId}</code> ?</p>`, async () => {
            const res = await apiRequest('/users/ban', 'POST', { userId, ban: false });
            if (res && res.success) {
                showToast('Utilisateur débanni', 'success');
                loadUsersData();
            }
        });
    };

    async function loadStockData() {
        const data = await apiRequest('/stock');
        if (!data) return;

        const tbody = document.getElementById('stock-table');
        tbody.innerHTML = '';
        (data.stock || []).forEach(item => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td>#${item.id}</td>
                <td><code>${item.code}</code></td>
                <td><strong>${item.value} €</strong></td>
                <td><strong>${item.price} €</strong></td>
                <td>
                    <button class="action-btn action-btn-danger" onclick="btnDeleteStock(${item.id})">Supprimer</button>
                </td>
            `;
            tbody.appendChild(tr);
        });
    }

    document.getElementById('add-stock-form').addEventListener('submit', async (e) => {
        e.preventDefault();
        const text = document.getElementById('stock-bulk-textarea').value.trim();

        if (!text) {
            showToast('Veuillez saisir au moins un code carte.', 'danger');
            return;
        }

        const lines = text.split('\n');
        const items = [];

        lines.forEach(line => {
            const l = line.trim();
            if (!l) return;

            const parts = l.split('|');
            if (parts.length >= 4) {
                items.push({
                    brand: 'carr',
                    code: parts[0].trim(),
                    pin: parts[1].trim(),
                    value: parseInt(parts[2].trim()) || 0,
                    price: parseFloat(parts[3].trim()) || 0.0
                });
            } else if (parts.length === 3) {
                items.push({
                    brand: 'carr',
                    code: parts[0].trim(),
                    pin: '',
                    value: parseInt(parts[1].trim()) || 0,
                    price: parseFloat(parts[2].trim()) || 0.0
                });
            } else if (parts.length === 2) {
                items.push({
                    brand: 'carr',
                    code: parts[0].trim(),
                    pin: '',
                    value: parseInt(parts[1].trim()) || 0,
                    price: 0.0
                });
            } else {
                items.push({
                    brand: 'carr',
                    code: l,
                    pin: '',
                    value: 0,
                    price: 0.0
                });
            }
        });

        if (items.length === 0) {
            showToast('Aucun code valide trouvé.', 'danger');
            return;
        }

        const res = await apiRequest('/stock/add', 'POST', { items });
        if (res && res.success) {
            showToast(`✅ ${res.count || items.length} carte(s) Carrefour ajoutée(s) au stock !`, 'success');
            document.getElementById('stock-bulk-textarea').value = '';
            loadStockData();
        }
    });

    window.btnDeleteStock = (id) => {
        openModal('Supprimer Carte', `<p>Supprimer la carte #${id} du stock ?</p>`, async () => {
            const res = await apiRequest('/stock/delete', 'POST', { id });
            if (res && res.success) {
                showToast('Carte supprimée', 'success');
                loadStockData();
            }
        });
    };

    async function loadTransactionsData() {
        const data = await apiRequest('/transactions');
        if (!data) return;

        const tbody = document.getElementById('all-transactions-table');
        tbody.innerHTML = '';
        (data.transactions || []).forEach(tx => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td>#${tx.id}</td>
                <td><code>${tx.userId}</code></td>
                <td>${tx.brand}</td>
                <td><code>${tx.code}</code></td>
                <td>${tx.value} €</td>
                <td><strong>${tx.price} €</strong></td>
                <td>${formatParisDate(tx.createdAt)}</td>
            `;
            tbody.appendChild(tr);
        });
    }

    async function loadSettingsData() {
        const data = await apiRequest('/settings');
        if (!data) return;

        const iptv = data.iptv || {};
        document.getElementById('setting-iptv-1m').value = iptv.price_1m || '5';
        document.getElementById('setting-iptv-3m').value = iptv.price_3m || '10';
        document.getElementById('setting-iptv-6m').value = iptv.price_6m || '15';
        document.getElementById('setting-iptv-12m').value = iptv.price_12m || '30';

        if (data.telegramMode) {
            document.getElementById('setting-telegram-mode').value = data.telegramMode;
        }
        if (data.sumupMode) {
            document.getElementById('setting-sumup-mode').value = data.sumupMode;
        }
    }

    async function loadPaymentsData() {
        const data = await apiRequest('/payments');
        if (!data) return;

        const tbody = document.getElementById('all-payments-table');
        tbody.innerHTML = '';
        (data.payments || []).forEach(p => {
            const tr = document.createElement('tr');
            let statusBadge = `<span class="badge badge-warning">${p.status}</span>`;
            if (p.status.toUpperCase() === 'PAID') statusBadge = `<span class="badge badge-success">PAYÉ</span>`;
            else if (p.status.toUpperCase() === 'FAILED' || p.status.toUpperCase() === 'CANCELED') statusBadge = `<span class="badge badge-danger">ÉCHOUÉ</span>`;
            else if (p.status.toUpperCase() === 'EXPIRED') statusBadge = `<span class="badge badge-muted">EXPIRÉ</span>`;

            tr.innerHTML = `
                <td>#${p.id}</td>
                <td><code>${p.chatId}</code></td>
                <td><strong>${p.method}</strong></td>
                <td><strong>${p.amount.toFixed(2)} €</strong></td>
                <td>${statusBadge}</td>
                <td><code>${p.trackId}</code></td>
                <td>${formatParisDate(p.createdAt)}</td>
            `;
            tbody.appendChild(tr);
        });
    }

    const tgModeForm = document.getElementById('settings-telegram-mode-form');
    if (tgModeForm) {
        tgModeForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const mode = document.getElementById('setting-telegram-mode').value;
            const res = await apiRequest('/settings/telegram', 'POST', { mode });
            if (res && res.success) {
                showToast(`Mode Telegram basculé sur ${res.mode === 'webhook' ? 'Webhook ⚡' : 'Long Polling 🔄'} avec succès !`, 'success');
            }
        });
    }

    const sumupModeForm = document.getElementById('settings-sumup-mode-form');
    if (sumupModeForm) {
        sumupModeForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const mode = document.getElementById('setting-sumup-mode').value;
            const res = await apiRequest('/settings/sumup/mode', 'POST', { mode });
            if (res && res.success) {
                showToast(`Mode SumUp basculé sur ${res.mode === 'webhook' ? 'Webhook ⚡' : 'Long Polling 🔄'} avec succès !`, 'success');
            }
        });
    }

    document.getElementById('settings-iptv-form').addEventListener('submit', async (e) => {
        e.preventDefault();
        const p1 = document.getElementById('setting-iptv-1m').value.trim();
        const p3 = document.getElementById('setting-iptv-3m').value.trim();
        const p6 = document.getElementById('setting-iptv-6m').value.trim();
        const p12 = document.getElementById('setting-iptv-12m').value.trim();

        const res = await apiRequest('/settings/iptv', 'POST', { price_1m: p1, price_3m: p3, price_6m: p6, price_12m: p12 });
        if (res && res.success) {
            showToast('Tarifs IPTV enregistrés !', 'success');
        }
    });
});
