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
        loadDashboardData();
    }

    if (authToken) {
        initApp();
    }

    navItems.forEach(item => {
        item.addEventListener('click', (e) => {
            e.preventDefault();
            const tab = item.getAttribute('data-tab');
            
            navItems.forEach(i => i.classList.remove('active'));
            item.classList.add('active');

            tabContents.forEach(content => content.style.display = 'none');
            const target = document.getElementById(`tab-${tab}`);
            if (target) target.style.display = 'block';

            const titleMap = {
                'dashboard': 'Vue d\'Ensemble',
                'users': 'Gestion des Utilisateurs',
                'stock': 'Gestion du Stock Carrefour',
                'transactions': 'Historique des Achats',
                'settings': 'Configuration du Bot'
            };
            pageTitleHeading.innerText = titleMap[tab] || 'Administration';

            if (tab === 'dashboard') loadDashboardData();
            else if (tab === 'users') loadUsersData();
            else if (tab === 'stock') loadStockData();
            else if (tab === 'transactions') loadTransactionsData();
            else if (tab === 'settings') loadSettingsData();
        });
    });

    async function loadDashboardData() {
        const stats = await apiRequest('/stats');
        if (!stats) return;

        document.getElementById('stat-total-ca').innerText = `${stats.totalCa.toFixed(2)} €`;
        document.getElementById('stat-total-sales').innerText = stats.totalSales;
        document.getElementById('stat-total-users').innerText = stats.totalUsers;
        document.getElementById('stat-total-stock').innerText = stats.totalStock;

        const tbody = document.getElementById('recent-sales-table');
        tbody.innerHTML = '';
        (stats.recentSales || []).forEach(tx => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td>#${tx.id}</td>
                <td><code>${tx.userId}</code></td>
                <td>${tx.brand}</td>
                <td><strong>${tx.price} €</strong></td>
                <td>${new Date(tx.createdAt).toLocaleString('fr-FR')}</td>
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
        const code = document.getElementById('stock-code-input').value.trim();
        const value = parseInt(document.getElementById('stock-value-input').value);
        const price = parseFloat(document.getElementById('stock-price-input').value);

        if (!code || isNaN(value) || isNaN(price)) return;

        const res = await apiRequest('/stock/add', 'POST', { brand: 'carr', code, pin: '', value, price });
        if (res && res.success) {
            showToast('Carte Carrefour ajoutée au stock !', 'success');
            document.getElementById('stock-code-input').value = '';
            document.getElementById('stock-value-input').value = '';
            document.getElementById('stock-price-input').value = '';
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
                <td>${new Date(tx.createdAt).toLocaleString('fr-FR')}</td>
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
