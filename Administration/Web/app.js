document.addEventListener('DOMContentLoaded', () => {
    const TWENTY_FOUR_HOURS = 24 * 60 * 60 * 1000; // 24 heures en millisecondes

    function getValidAuthToken() {
        const token = localStorage.getItem('admin_auth_token') || '';
        const savedTime = parseInt(localStorage.getItem('admin_auth_token_time') || '0', 10);
        if (token && savedTime && (Date.now() - savedTime < TWENTY_FOUR_HOURS)) {
            return token;
        }
        localStorage.removeItem('admin_auth_token');
        localStorage.removeItem('admin_auth_token_time');
        return '';
    }

    let authToken = getValidAuthToken();

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

    function formatParisDate(dateStr) {
        if (!dateStr) return '';
        const d = new Date(dateStr);
        if (isNaN(d.getTime())) return dateStr;
        const pad = n => n.toString().padStart(2, '0');
        return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
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

    function escapeHtml(str) {
        if (str === null || str === undefined) return '';
        return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    window.redirectToUser = (userId) => {
        window.switchTab('users');
        const searchInput = document.getElementById('user-search-input');
        if (searchInput) {
            searchInput.value = userId;
            searchInput.dispatchEvent(new Event('input'));
        }
    };

    window.filterTransactionsByUser = (userId) => {
        window.switchTab('transactions');
        const searchInput = document.getElementById('tx-search-input');
        if (searchInput) {
            searchInput.value = String(userId);
            searchInput.dispatchEvent(new Event('input'));
        }
        showToast(`Transactions filtrées sur l'utilisateur ${userId}`, 'info');
    };

    window.filterPaymentsByUser = (userId) => {
        window.switchTab('payments');
        const searchInput = document.getElementById('payments-search-input');
        if (searchInput) {
            searchInput.value = String(userId);
            searchInput.dispatchEvent(new Event('input'));
        }
        showToast(`Rechargements filtrés sur l'utilisateur ${userId}`, 'info');
    };

    window.filterPayments = () => {
        const filterValue = document.getElementById('filter-payments-method').value.toUpperCase();
        const trs = document.querySelectorAll('#all-payments-table tr');
        trs.forEach(tr => {
            const methodCell = tr.cells[2].innerText.toUpperCase();
            if (filterValue === '' || methodCell.includes(filterValue)) {
                tr.style.display = '';
            } else {
                tr.style.display = 'none';
            }
        });
    };

    window.sortTable = (tableId, colIndex, isNumber = false) => {
        const table = document.getElementById(tableId);
        let rows = Array.from(table.rows);
        let ascending = table.getAttribute('data-sort-asc') === 'true';
        table.setAttribute('data-sort-asc', !ascending);
        
        rows.sort((a, b) => {
            let cellA = a.cells[colIndex].innerText.trim().replace(/€/g, '').replace(/#/g, '');
            let cellB = b.cells[colIndex].innerText.trim().replace(/€/g, '').replace(/#/g, '');
            
            if (isNumber) {
                return ascending ? parseFloat(cellA) - parseFloat(cellB) : parseFloat(cellB) - parseFloat(cellA);
            } else {
                return ascending ? cellA.localeCompare(cellB) : cellB.localeCompare(cellA);
            }
        });
        
        table.innerHTML = '';
        rows.forEach(row => table.appendChild(row));
    };

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
            localStorage.setItem('admin_auth_token_time', Date.now().toString());
            showToast('Connexion réussie (Session 24h) !', 'success');
            initApp();
        } else {
            authToken = '';
            showToast('Mot de passe ou token incorrect.', 'danger');
        }
    });

    function logout() {
        authToken = '';
        localStorage.removeItem('admin_auth_token');
        localStorage.removeItem('admin_auth_token_time');
        appView.style.display = 'none';
        loginView.style.display = 'flex';
    }

    logoutBtn.addEventListener('click', logout);

    function initApp() {
        loginView.style.display = 'none';
        appView.style.display = 'flex';

        const hash = (window.location.hash || '').replace('#', '').trim();
        const savedTab = localStorage.getItem('admin_active_tab') || 'dashboard';
        const validTabs = ['dashboard', 'metrics', 'users', 'stock', 'payments', 'transactions', 'settings'];
        const activeTab = validTabs.includes(hash) ? hash : (validTabs.includes(savedTab) ? savedTab : 'dashboard');

        window.switchTab(activeTab);
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
                'metrics': '⚡ Métriques & Trafic Système',
                'users': 'Gestion des Utilisateurs',
                'stock': 'Gestion du Stock Carrefour',
                'payments': 'Rechargements (CB & Crypto)',
                'transactions': 'Historique des Achats',
                'settings': 'Configuration du Bot'
            };
            pageTitleHeading.innerText = titleMap[tab] || 'Administration';

            if (tab === 'dashboard') loadDashboardData();
            else if (tab === 'metrics') startMetricsLivePolling();
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

    // Mobile Menu Toggle Logic
    const mobileMenuBtn = document.getElementById('mobile-menu-btn');
    const sidebar = document.querySelector('.sidebar');
    const sidebarOverlay = document.getElementById('sidebar-overlay');

    function toggleMobileMenu() {
        if (!sidebar) return;
        const isActive = sidebar.classList.toggle('active');
        if (sidebarOverlay) {
            if (isActive) sidebarOverlay.classList.add('active');
            else sidebarOverlay.classList.remove('active');
        }
    }

    function closeMobileMenu() {
        if (!sidebar || window.innerWidth > 768) return;
        sidebar.classList.remove('active');
        if (sidebarOverlay) sidebarOverlay.classList.remove('active');
    }

    if (mobileMenuBtn) {
        mobileMenuBtn.addEventListener('click', toggleMobileMenu);
    }

    if (sidebarOverlay) {
        sidebarOverlay.addEventListener('click', closeMobileMenu);
    }

    navItems.forEach(item => {
        item.addEventListener('click', closeMobileMenu);
    });
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

        const tbody = document.getElementById('recent-sales-table');
        tbody.innerHTML = '';
        (stats.recentSales || []).forEach(tx => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td>#${tx.id}</td>
                <td style="cursor: pointer; color: var(--accent-primary);" onclick="window.redirectToUser('${tx.userId}')"><code>${tx.userId}</code></td>
                <td>${escapeHtml(tx.brand)}</td>
                <td><strong>${tx.price} €</strong></td>
                <td>${formatParisDate(tx.createdAt)}</td>
            `;
            tbody.appendChild(tr);
        });
    }

    let metricsLiveInterval = null;

    function startMetricsLivePolling() {
        if (metricsLiveInterval) clearInterval(metricsLiveInterval);
        loadMetricsData();
        metricsLiveInterval = setInterval(() => {
            const activeTab = localStorage.getItem('admin_active_tab');
            if (activeTab === 'metrics') {
                loadMetricsData();
            } else {
                clearInterval(metricsLiveInterval);
                metricsLiveInterval = null;
            }
        }, 2000);
    }

    function updateCounter(elementId, value) {
        const el = document.getElementById(elementId);
        if (!el) return;
        const newVal = String(value || 0);
        if (el.innerText !== newVal) {
            el.innerText = newVal;
            el.classList.add('counter-pulse');
            setTimeout(() => el.classList.remove('counter-pulse'), 400);
        }
    }

    // [ ADVANCED METRICS CHARTS SYSTEM ] =====================================
    let chartGlobalVolume = null;
    let chartTelegramActivity = null;
    let chartGatewaysActivity = null;
    let chartHealthActivity = null;

    const metricsHistory = {
        labels: [],
        totalTraffic: [],
        tgRec: [],
        tgSent: [],
        cmdExec: [],
        sumupRec: [],
        oxapaySent: [],
        errors: []
    };
    const MAX_HISTORY_POINTS = 15;

    function initMetricsViewMode() {
        const btnCards = document.getElementById('btn-mode-cards');
        const btnCharts = document.getElementById('btn-mode-charts');
        const cardsView = document.getElementById('metrics-cards-view');
        const chartsView = document.getElementById('metrics-charts-view');

        if (!btnCards || !btnCharts || !cardsView || !chartsView) return;

        function setViewMode(mode) {
            localStorage.setItem('metrics_view_mode', mode);
            if (mode === 'cards') {
                cardsView.style.display = 'block';
                chartsView.style.display = 'none';
                btnCards.style.background = 'var(--accent-primary)';
                btnCards.style.color = '#ffffff';
                btnCharts.style.background = 'transparent';
                btnCharts.style.color = 'var(--text-secondary)';
            } else {
                cardsView.style.display = 'none';
                chartsView.style.display = 'grid';
                btnCharts.style.background = 'var(--accent-primary)';
                btnCharts.style.color = '#ffffff';
                btnCards.style.background = 'transparent';
                btnCards.style.color = 'var(--text-secondary)';
            }
        }

        btnCards.addEventListener('click', () => setViewMode('cards'));
        btnCharts.addEventListener('click', () => setViewMode('charts'));

        const savedMode = localStorage.getItem('metrics_view_mode') || 'charts';
        setViewMode(savedMode);
    }

    let selectedTimeframe = 'live';

    function initMetricsCharts() {
        if (typeof Chart === 'undefined') return;

        Chart.defaults.color = '#94a3b8';
        Chart.defaults.font.family = 'system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';

        const ctxGlobal = document.getElementById('chart-global-volume')?.getContext('2d');
        if (ctxGlobal && !chartGlobalVolume) {
            chartGlobalVolume = new Chart(ctxGlobal, {
                type: 'line',
                data: {
                    labels: metricsHistory.labels,
                    datasets: [
                        { 
                            label: 'Volume Réseau Global', 
                            data: metricsHistory.totalTraffic, 
                            borderColor: '#6366f1', 
                            backgroundColor: 'rgba(99, 102, 241, 0.18)', 
                            fill: true, 
                            tension: 0.2, 
                            borderWidth: 3,
                            pointBackgroundColor: '#818cf8',
                            pointRadius: 3
                        }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: false,
                    plugins: {
                        legend: { position: 'top', labels: { boxWidth: 12, padding: 16 } },
                        tooltip: { mode: 'index', intersect: false }
                    },
                    scales: {
                        x: { grid: { color: 'rgba(255,255,255,0.05)' } },
                        y: { grid: { color: 'rgba(255,255,255,0.05)' }, beginAtZero: true }
                    }
                }
            });
        }

        const ctxTg = document.getElementById('chart-telegram-activity')?.getContext('2d');
        if (ctxTg && !chartTelegramActivity) {
            chartTelegramActivity = new Chart(ctxTg, {
                type: 'doughnut',
                data: {
                    labels: ['Requêtes Entrantes', 'Requêtes Sortantes'],
                    datasets: [{
                        data: [0, 0],
                        backgroundColor: ['#0088cc', '#a855f7'],
                        borderWidth: 0
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: false,
                    plugins: { legend: { position: 'bottom' } },
                    cutout: '70%'
                }
            });
        }

        const ctxGateways = document.getElementById('chart-gateways-activity')?.getContext('2d');
        if (ctxGateways && !chartGatewaysActivity) {
            chartGatewaysActivity = new Chart(ctxGateways, {
                type: 'bar',
                data: {
                    labels: ['SumUp Reçus', 'SumUp Envoyés', 'OxaPay API'],
                    datasets: [{
                        label: 'Nombre de requêtes',
                        data: [0, 0, 0],
                        backgroundColor: ['#3b82f6', '#60a5fa', '#f59e0b'],
                        borderRadius: 6
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: false,
                    plugins: { legend: { display: false } },
                    scales: {
                        x: { grid: { display: false } },
                        y: { grid: { color: 'rgba(255,255,255,0.05)' }, beginAtZero: true }
                    }
                }
            });
        }

        const ctxHealth = document.getElementById('chart-health-activity')?.getContext('2d');
        if (ctxHealth && !chartHealthActivity) {
            chartHealthActivity = new Chart(ctxHealth, {
                type: 'bar',
                data: {
                    labels: ['Processus Traités', 'Accès Admin', 'Erreurs'],
                    datasets: [{
                        label: 'Volume',
                        data: [0, 0, 0],
                        backgroundColor: ['#10b981', '#a855f7', '#ef4444'],
                        borderRadius: 6
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: false,
                    plugins: { legend: { display: false } },
                    scales: {
                        x: { grid: { display: false } },
                        y: { grid: { color: 'rgba(255,255,255,0.05)' }, beginAtZero: true }
                    }
                }
            });
        }

        const selectTimeframe = document.getElementById('chart-timeframe-select');
        if (selectTimeframe && !selectTimeframe.dataset.initialized) {
            selectTimeframe.dataset.initialized = 'true';
            selectTimeframe.addEventListener('change', (e) => {
                selectedTimeframe = e.target.value;
                if (lastStatsResponse) {
                    renderMainVolumeChart(lastStatsResponse);
                }
            });
        }
    }

    let lastStatsResponse = null;

    function renderMainVolumeChart(stats) {
        if (!chartGlobalVolume) return;

        if (selectedTimeframe === 'live') {
            chartGlobalVolume.data.labels = metricsHistory.labels;
            chartGlobalVolume.data.datasets[0].data = metricsHistory.totalTraffic;
            chartGlobalVolume.data.datasets[0].label = 'Volume Réseau Global (En Direct)';
        } else if (stats && stats.history) {
            let hData = [];
            if (selectedTimeframe === 'today') {
                hData = stats.history.today || [];
                chartGlobalVolume.data.datasets[0].label = 'Volume des Achats & Rechargements (Aujourd\'hui)';
            } else if (selectedTimeframe === '7d') {
                hData = stats.history.days7 || [];
                chartGlobalVolume.data.datasets[0].label = 'Volume des Achats & Rechargements (7 Derniers Jours)';
            } else if (selectedTimeframe === '30d') {
                hData = stats.history.days30 || [];
                chartGlobalVolume.data.datasets[0].label = 'Volume des Achats & Rechargements (30 Derniers Jours)';
            }
            chartGlobalVolume.data.labels = hData.map(x => x.label);
            chartGlobalVolume.data.datasets[0].data = hData.map(x => x.volume);
        }

        chartGlobalVolume.update('none');
    }

    function updateChartsWithMetrics(m, totalTraffic, stats) {
        initMetricsCharts();
        initMetricsViewMode();
        lastStatsResponse = stats;

        const nowTime = new Date().toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
        
        // Initialiser 10 points si le buffer est vide pour tracer immédiatement une ligne complète
        if (metricsHistory.labels.length === 0) {
            const now = new Date();
            for (let i = 9; i >= 0; i--) {
                const pastTime = new Date(now.getTime() - i * 2000).toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
                metricsHistory.labels.push(pastTime);
                metricsHistory.totalTraffic.push(totalTraffic || 0);
            }
        } else {
            if (metricsHistory.labels.length >= 10) {
                metricsHistory.labels.shift();
                metricsHistory.totalTraffic.shift();
            }
            metricsHistory.labels.push(nowTime);
            metricsHistory.totalTraffic.push(totalTraffic || 0);
        }

        renderMainVolumeChart(stats);

        if (chartTelegramActivity) {
            chartTelegramActivity.data.datasets[0].data = [m.telegramReceived || 0, m.telegramSent || 0];
            chartTelegramActivity.update('none');
        }
        if (chartGatewaysActivity) {
            chartGatewaysActivity.data.datasets[0].data = [m.sumupReceived || 0, m.sumupSent || 0, m.oxapaySent || 0];
            chartGatewaysActivity.update('none');
        }
        if (chartHealthActivity) {
            chartHealthActivity.data.datasets[0].data = [m.commandsExecuted || 0, m.adminLogins || 0, m.errorsCount || 0];
            chartHealthActivity.update('none');
        }
    }

    async function loadMetricsData() {
        const stats = await apiRequest('/stats');
        if (!stats || !stats.metrics) return;

        const m = stats.metrics;
        updateCounter('metric-tg-rec', m.telegramReceived);
        updateCounter('metric-tg-sent', m.telegramSent);
        updateCounter('metric-sumup-rec', m.sumupReceived);
        updateCounter('metric-sumup-sent', m.sumupSent);
        updateCounter('metric-oxapay-sent', m.oxapaySent);
        updateCounter('metric-commands-exec', m.commandsExecuted);
        updateCounter('metric-errors-count', m.errorsCount);
        updateCounter('metric-admin-logins', m.adminLogins);

        const totalTraffic = (m.telegramReceived || 0) + (m.telegramSent || 0) + (m.sumupReceived || 0) + (m.sumupSent || 0) + (m.oxapayReceived || 0) + (m.oxapaySent || 0);
        updateCounter('metric-total-traffic', totalTraffic);

        updateChartsWithMetrics(m, totalTraffic, stats);
    }

    const resetMetricsBtn = document.getElementById('reset-metrics-btn');
    if (resetMetricsBtn) {
        resetMetricsBtn.addEventListener('click', () => {
            openModal(
                '🧹 Réinitialiser les compteurs',
                '<p>Êtes-vous sûr de vouloir réinitialiser l\'intégralité des compteurs de métriques et de statistiques à 0 ?</p><p style="color: var(--text-secondary); font-size: 13px; margin-top: 8px;">Cette action effacera l\'historique enregistré en BDD et remettra les valeurs à zéro.</p>',
                async () => {
                    closeModal();
                    const res = await apiRequest('/metrics/reset', 'POST');
                    if (res && res.success) {
                        showToast('Compteurs réinitialisés à 0', 'success');
                        metricsHistory.labels.length = 0;
                        metricsHistory.tgRec.length = 0;
                        metricsHistory.tgSent.length = 0;
                        metricsHistory.cmdExec.length = 0;
                        metricsHistory.sumupRec.length = 0;
                        metricsHistory.oxapaySent.length = 0;
                        metricsHistory.errors.length = 0;
                        await loadMetricsData();
                    } else {
                        showToast('Erreur lors de la réinitialisation', 'error');
                    }
                }
            );
        });
    }

    let allUsers = [];
    let userSortField = 'userNumber';
    let userSortDir = 'asc';
    let usersCurrentPage = 1;
    let usersPerPage = 10;

    async function loadUsersData() {
        const data = await apiRequest('/users');
        if (!data) return;

        allUsers = data.users || [];
        applyUsersFilterAndSort();
    }

    function applyUsersFilterAndSort() {
        const searchInput = document.getElementById('user-search-input');
        const clearBtn = document.getElementById('user-search-clear');
        const q = searchInput ? searchInput.value.trim().toLowerCase() : '';

        if (clearBtn) {
            clearBtn.style.display = q ? 'block' : 'none';
        }

        let filtered = allUsers.filter(u => {
            if (!q) return true;
            return String(u.id).includes(q) ||
                   String(u.userNumber).includes(q) ||
                   (u.username && u.username.toLowerCase().includes(q)) ||
                   String(u.solde).includes(q) ||
                   (u.banReason && u.banReason.toLowerCase().includes(q));
        });

        filtered.sort((a, b) => {
            let valA = a[userSortField];
            let valB = b[userSortField];

            if (typeof valA === 'string') valA = valA.toLowerCase();
            if (typeof valB === 'string') valB = valB.toLowerCase();

            if (valA < valB) return userSortDir === 'asc' ? -1 : 1;
            if (valA > valB) return userSortDir === 'asc' ? 1 : -1;
            return 0;
        });

        // Update Sort Arrow Indicators
        document.querySelectorAll('.sortable-th').forEach(th => {
            const field = th.getAttribute('data-sort');
            const arrowSpan = th.querySelector('.sort-arrow');
            if (arrowSpan) {
                if (field === userSortField) {
                    arrowSpan.innerText = userSortDir === 'asc' ? '▲' : '▼';
                    th.style.color = 'var(--accent-primary)';
                } else {
                    arrowSpan.innerText = '↕';
                    th.style.color = '';
                }
            }
        });

        // Pagination Slice
        const totalItems = filtered.length;
        let perPage = usersPerPage === 'all' ? totalItems : parseInt(usersPerPage) || 10;
        if (perPage <= 0) perPage = 10;

        const totalPages = Math.ceil(totalItems / perPage) || 1;
        if (usersCurrentPage > totalPages) usersCurrentPage = totalPages;
        if (usersCurrentPage < 1) usersCurrentPage = 1;

        const startIdx = (usersCurrentPage - 1) * perPage;
        const endIdx = usersPerPage === 'all' ? totalItems : Math.min(startIdx + perPage, totalItems);
        const pageItems = filtered.slice(startIdx, endIdx);

        // Update Pagination Info UI
        const infoElem = document.getElementById('users-pagination-info');
        if (infoElem) {
            infoElem.innerText = totalItems > 0 
                ? `Affichage ${startIdx + 1}-${endIdx} sur ${totalItems} utilisateur(s)`
                : `Aucun utilisateur trouvé`;
        }

        const pageIndicator = document.getElementById('users-page-indicator');
        if (pageIndicator) {
            pageIndicator.innerText = `Page ${usersCurrentPage} / ${totalPages}`;
        }

        const btnPrev = document.getElementById('btn-users-prev');
        const btnNext = document.getElementById('btn-users-next');
        if (btnPrev) btnPrev.disabled = usersCurrentPage <= 1;
        if (btnNext) btnNext.disabled = usersCurrentPage >= totalPages;

        renderUsersTable(pageItems);
    }

    function renderUsersTable(users) {
        const tbody = document.getElementById('users-table');
        tbody.innerHTML = '';
        if (users.length === 0) {
            tbody.innerHTML = `<tr><td colspan="8" style="text-align: center; color: var(--text-secondary); padding: 24px;">Aucun utilisateur trouvé.</td></tr>`;
            return;
        }

        users.forEach(user => {
            const tr = document.createElement('tr');
            tr.style.cursor = 'context-menu';
            const statusBadge = user.isBanned 
                ? `<span class="badge badge-danger">Banni</span>` 
                : `<span class="badge badge-success">Actif</span>`;
            
            const unameBadge = user.username 
                ? `<span style="color: #6366f1; font-weight: 600;">${escapeHtml(user.username)}</span>` 
                : `<span style="color: var(--text-secondary); font-style: italic; opacity: 0.6;">-</span>`;

            tr.innerHTML = `
                <td><span class="badge badge-info" style="font-weight: 700; background: rgba(99, 102, 241, 0.15); color: #818cf8; border: 1px solid rgba(99, 102, 241, 0.3);">#${user.userNumber || '?'}</span></td>
                <td><code>${user.id}</code></td>
                <td>${unameBadge}</td>
                <td><strong>${user.solde.toFixed(2)} €</strong></td>
                <td>${user.achats}</td>
                <td>${statusBadge}</td>
                <td>${user.banReason ? escapeHtml(user.banReason) : '-'}</td>
                <td>
                    <button class="action-btn" onclick="btnEditSolde('${user.id}', ${user.solde})">💳 Solde</button>
                    ${user.isBanned 
                        ? `<button class="action-btn action-btn-danger" onclick="btnDebanUser('${user.id}')">Débannir</button>` 
                        : `<button class="action-btn action-btn-danger" onclick="btnBanUser('${user.id}')">Bannir</button>`}
                    <button class="action-btn action-btn-danger" style="background: rgba(239,68,68,0.15); color: #ef4444; border: 1px solid rgba(239,68,68,0.3);" onclick="btnDeleteUser('${user.id}')">🗑️ Supprimer</button>
                </td>
            `;

            tr.addEventListener('contextmenu', (e) => {
                showDynamicContextMenu(e, [
                    { label: '💳 Modifier le Solde', action: () => btnEditSolde(user.id, user.solde) },
                    { label: user.isBanned ? '🔓 Débannir l\'Utilisateur' : '🚫 Bannir l\'Utilisateur', action: () => user.isBanned ? btnDebanUser(user.id) : btnBanUser(user.id) },
                    { divider: true },
                    { label: '📋 Copier l\'ID Telegram', action: () => { navigator.clipboard.writeText(String(user.id)); showToast(`ID ${user.id} copié !`, 'info'); } },
                    { label: '💬 Copier le Username', action: () => { if (user.username) { navigator.clipboard.writeText(user.username); showToast(`@${user.username} copié !`, 'info'); } else showToast('Aucun username à copier', 'warning'); } },
                    { divider: true },
                    { label: '🛒 Historique des Achats', action: () => window.filterTransactionsByUser(user.id) },
                    { label: '💰 Historique des Rechargements', action: () => window.filterPaymentsByUser(user.id) },
                    { divider: true },
                    { label: '🗑️ Supprimer l\'Utilisateur', danger: true, action: () => btnDeleteUser(user.id) }
                ]);
            });
            tbody.appendChild(tr);
        });
    }

    // [ DYNAMIC CONTEXT MENU SYSTEM ] ========================================
    const ctxMenu = document.getElementById('custom-context-menu');

    function showDynamicContextMenu(e, items) {
        e.preventDefault();
        e.stopPropagation();
        if (!ctxMenu) return;

        ctxMenu.innerHTML = '';
        items.forEach(item => {
            if (item.divider) {
                const div = document.createElement('div');
                div.className = 'context-menu-divider';
                ctxMenu.appendChild(div);
            } else {
                const div = document.createElement('div');
                div.className = `context-menu-item ${item.danger ? 'danger' : ''}`;
                div.innerHTML = item.label;
                div.addEventListener('click', (evt) => {
                    evt.stopPropagation();
                    hideContextMenu();
                    if (item.action) item.action();
                });
                ctxMenu.appendChild(div);
            }
        });

        ctxMenu.style.display = 'block';

        let x = e.pageX;
        let y = e.pageY;
        const menuWidth = 240;
        const menuHeight = ctxMenu.offsetHeight || 260;

        if (x + menuWidth > window.innerWidth + window.scrollX) {
            x = window.innerWidth + window.scrollX - menuWidth - 10;
        }
        if (y + menuHeight > window.innerHeight + window.scrollY) {
            y = window.innerHeight + window.scrollY - menuHeight - 10;
        }

        ctxMenu.style.left = `${Math.max(10, x)}px`;
        ctxMenu.style.top = `${Math.max(10, y)}px`;
    }

    function hideContextMenu() {
        if (ctxMenu) ctxMenu.style.display = 'none';
    }

    document.addEventListener('click', hideContextMenu);
    document.addEventListener('scroll', hideContextMenu);
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') hideContextMenu();
    });

    // Attach Event Listeners for Search, Clear, Sorting & Pagination
    const searchInputElem = document.getElementById('user-search-input');
    if (searchInputElem) {
        searchInputElem.addEventListener('input', () => {
            usersCurrentPage = 1;
            applyUsersFilterAndSort();
        });
    }

    const clearBtnElem = document.getElementById('user-search-clear');
    if (clearBtnElem) {
        clearBtnElem.addEventListener('click', () => {
            if (searchInputElem) searchInputElem.value = '';
            usersCurrentPage = 1;
            applyUsersFilterAndSort();
        });
    }

    document.querySelectorAll('.sortable-th').forEach(th => {
        th.addEventListener('click', () => {
            const field = th.getAttribute('data-sort');
            if (userSortField === field) {
                userSortDir = userSortDir === 'asc' ? 'desc' : 'asc';
            } else {
                userSortField = field;
                userSortDir = 'asc';
            }
            applyUsersFilterAndSort();
        });
    });

    const perPageSelect = document.getElementById('users-per-page-select');
    if (perPageSelect) {
        perPageSelect.addEventListener('change', (e) => {
            usersPerPage = e.target.value;
            usersCurrentPage = 1;
            applyUsersFilterAndSort();
        });
    }

    const btnPrevElem = document.getElementById('btn-users-prev');
    if (btnPrevElem) {
        btnPrevElem.addEventListener('click', () => {
            if (usersCurrentPage > 1) {
                usersCurrentPage--;
                applyUsersFilterAndSort();
            }
        });
    }

    const btnNextElem = document.getElementById('btn-users-next');
    if (btnNextElem) {
        btnNextElem.addEventListener('click', () => {
            usersCurrentPage++;
            applyUsersFilterAndSort();
        });
    }

    const btnSyncElem = document.getElementById('btn-sync-usernames');
    if (btnSyncElem) {
        btnSyncElem.addEventListener('click', async () => {
            btnSyncElem.disabled = true;
            btnSyncElem.innerHTML = '<span>🔄</span> Synchro en cours...';
            const res = await apiRequest('/users/sync-usernames', 'POST', {});
            btnSyncElem.disabled = false;
            btnSyncElem.innerHTML = '<span>🔄</span> Synchro Pseudos Telegram';
            if (res && res.success) {
                showToast('Pseudos Telegram synchronisés en direct !', 'success');
                loadUsersData();
            } else {
                showToast('Erreur lors de la synchronisation', 'danger');
            }
        });
    }

    window.btnDeleteUser = (userId) => {
        openModal(`Supprimer ${userId}`, `<p style="color: var(--text-secondary);">Êtes-vous sûr de vouloir <strong>supprimer définitivement</strong> l'utilisateur <code>${userId}</code> ?<br><br><span style="color:#ef4444; font-size:13px; font-weight:600;">⚠️ Cette action effacera toutes ses données comme s'il n'avait jamais rejoint le bot.</span></p>`, async () => {
            const res = await apiRequest('/users/delete', 'POST', { userId: parseInt(userId) });
            if (res && res.success) {
                showToast('Utilisateur supprimé définitivement', 'success');
                loadUsersData();
            } else {
                showToast('Erreur lors de la suppression', 'danger');
            }
        });
    };

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

    // [ STOCK PAGINATION LOGIC ] =============================================
    let rawStockData = [];
    let stockCurrentPage = 1;
    let stockPerPage = '10';

    async function loadStockData() {
        const data = await apiRequest('/stock');
        if (!data) return;
        rawStockData = data.stock || [];
        stockCurrentPage = 1;
        applyStockPagination();
    }

    function applyStockPagination() {
        const totalItems = rawStockData.length;
        let perPage = stockPerPage === 'all' ? totalItems : parseInt(stockPerPage) || 10;
        if (perPage <= 0) perPage = 10;

        const totalPages = Math.ceil(totalItems / perPage) || 1;
        if (stockCurrentPage > totalPages) stockCurrentPage = totalPages;
        if (stockCurrentPage < 1) stockCurrentPage = 1;

        const startIdx = (stockCurrentPage - 1) * perPage;
        const endIdx = stockPerPage === 'all' ? totalItems : Math.min(startIdx + perPage, totalItems);
        const pageItems = rawStockData.slice(startIdx, endIdx);

        const infoElem = document.getElementById('stock-pagination-info');
        if (infoElem) {
            infoElem.innerText = totalItems > 0 
                ? `Affichage ${startIdx + 1}-${endIdx} sur ${totalItems} carte(s)`
                : `Aucune carte disponible`;
        }

        const pageIndicator = document.getElementById('stock-page-indicator');
        if (pageIndicator) {
            pageIndicator.innerText = `Page ${stockCurrentPage} / ${totalPages}`;
        }

        const btnPrev = document.getElementById('btn-stock-prev');
        const btnNext = document.getElementById('btn-stock-next');
        if (btnPrev) btnPrev.disabled = stockCurrentPage <= 1;
        if (btnNext) btnNext.disabled = stockCurrentPage >= totalPages;

        renderStockTable(pageItems);
    }

    function renderStockTable(items) {
        const tbody = document.getElementById('stock-table');
        tbody.innerHTML = '';
        if (items.length === 0) {
            tbody.innerHTML = `<tr><td colspan="6" style="text-align: center; color: var(--text-secondary); padding: 24px;">Aucun stock disponible.</td></tr>`;
            return;
        }

        items.forEach(item => {
            const tr = document.createElement('tr');
            tr.style.cursor = 'context-menu';
            const valDisplay = (item.value != null && item.value > 0) ? `${item.value} €` : '-';
            const pinDisplay = item.pin ? `<code>${escapeHtml(item.pin)}</code>` : `<span style="color: var(--text-secondary); font-style: italic; opacity: 0.5;">-</span>`;
            tr.innerHTML = `
                <td>#${item.id}</td>
                <td><code>${item.code}</code></td>
                <td>${pinDisplay}</td>
                <td><strong>${valDisplay}</strong></td>
                <td><strong>${item.price} €</strong></td>
                <td>
                    <button class="action-btn action-btn-danger" onclick="btnDeleteStock(${item.id})">Supprimer</button>
                </td>
            `;
            tr.addEventListener('contextmenu', (e) => {
                showDynamicContextMenu(e, [
                    { label: '📋 Copier le Code Carte', action: () => { navigator.clipboard.writeText(item.code); showToast(`Code ${item.code} copié !`, 'info'); } },
                    { label: '🔑 Copier le PIN', action: () => { if (item.pin) { navigator.clipboard.writeText(item.pin); showToast(`PIN ${item.pin} copié !`, 'info'); } else showToast('Aucun PIN sur cette carte', 'warning'); } },
                    { label: '📌 Copier Ligne Complète (Code|PIN|Solde|Prix)', action: () => {
                        const line = item.pin ? `${item.code}|${item.pin}|${item.value || 0}|${item.price || 0}` : `${item.code}|${item.value || 0}|${item.price || 0}`;
                        navigator.clipboard.writeText(line);
                        showToast('Ligne complète copiée !', 'info');
                    } },
                    { divider: true },
                    { label: `💶 Solde Carte: ${valDisplay}`, action: () => {} },
                    { label: `💰 Prix Vente: ${item.price} €`, action: () => {} },
                    { divider: true },
                    { label: '🗑️ Supprimer cette Carte', danger: true, action: () => btnDeleteStock(item.id) }
                ]);
            });
            tbody.appendChild(tr);
        });
    }

    const stockPerPageSelect = document.getElementById('stock-per-page-select');
    if (stockPerPageSelect) {
        stockPerPageSelect.addEventListener('change', (e) => {
            stockPerPage = e.target.value;
            stockCurrentPage = 1;
            applyStockPagination();
        });
    }
    const btnStockPrev = document.getElementById('btn-stock-prev');
    if (btnStockPrev) {
        btnStockPrev.addEventListener('click', () => {
            if (stockCurrentPage > 1) {
                stockCurrentPage--;
                applyStockPagination();
            }
        });
    }
    const btnStockNext = document.getElementById('btn-stock-next');
    if (btnStockNext) {
        btnStockNext.addEventListener('click', () => {
            stockCurrentPage++;
            applyStockPagination();
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

    // [ TRANSACTIONS PAGINATION LOGIC ] =======================================
    let rawTransactionsData = [];
    let transactionsCurrentPage = 1;
    let transactionsPerPage = '10';

    async function loadTransactionsData() {
        const data = await apiRequest('/transactions');
        if (!data) return;
        rawTransactionsData = data.transactions || [];
        transactionsCurrentPage = 1;
        applyTransactionsPagination();
    }

    function applyTransactionsPagination() {
        const query = (document.getElementById('tx-search-input')?.value || '').trim().toLowerCase();
        const clearBtn = document.getElementById('tx-search-clear');
        if (clearBtn) clearBtn.style.display = query ? 'block' : 'none';

        let filtered = rawTransactionsData;
        if (query) {
            filtered = rawTransactionsData.filter(t => 
                String(t.userId || '').toLowerCase().includes(query) ||
                String(t.brand || '').toLowerCase().includes(query) ||
                String(t.code || '').toLowerCase().includes(query) ||
                String(t.id || '').toLowerCase().includes(query)
            );
        }

        const totalItems = filtered.length;
        let perPage = transactionsPerPage === 'all' ? totalItems : parseInt(transactionsPerPage) || 10;
        if (perPage <= 0) perPage = 10;

        const totalPages = Math.ceil(totalItems / perPage) || 1;
        if (transactionsCurrentPage > totalPages) transactionsCurrentPage = totalPages;
        if (transactionsCurrentPage < 1) transactionsCurrentPage = 1;

        const startIdx = (transactionsCurrentPage - 1) * perPage;
        const endIdx = transactionsPerPage === 'all' ? totalItems : Math.min(startIdx + perPage, totalItems);
        const pageItems = filtered.slice(startIdx, endIdx);

        const infoElem = document.getElementById('transactions-pagination-info');
        if (infoElem) {
            infoElem.innerText = totalItems > 0 
                ? `Affichage ${startIdx + 1}-${endIdx} sur ${totalItems} achat(s)`
                : `Aucun achat trouvé`;
        }

        const pageIndicator = document.getElementById('transactions-page-indicator');
        if (pageIndicator) {
            pageIndicator.innerText = `Page ${transactionsCurrentPage} / ${totalPages}`;
        }

        const btnPrev = document.getElementById('btn-transactions-prev');
        const btnNext = document.getElementById('btn-transactions-next');
        if (btnPrev) btnPrev.disabled = transactionsCurrentPage <= 1;
        if (btnNext) btnNext.disabled = transactionsCurrentPage >= totalPages;

        renderTransactionsTable(pageItems);
    }

    function renderTransactionsTable(transactions) {
        const tbody = document.getElementById('all-transactions-table');
        tbody.innerHTML = '';
        if (transactions.length === 0) {
            tbody.innerHTML = `<tr><td colspan="7" style="text-align: center; color: var(--text-secondary); padding: 24px;">Aucune transaction enregistrée.</td></tr>`;
            return;
        }

        transactions.forEach(tx => {
            const tr = document.createElement('tr');
            tr.style.cursor = 'context-menu';
            const isIptv = (tx.brand || '').toLowerCase() === 'iptv';
            const valueFormatted = (!isIptv && tx.value != null && tx.value > 0) ? `${tx.value} €` : '-';
            tr.innerHTML = `
                <td>#${tx.id}</td>
                <td style="cursor: pointer; color: var(--accent-primary);" onclick="window.redirectToUser('${tx.userId}')"><code>${tx.userId}</code></td>
                <td>${escapeHtml(tx.brand)}</td>
                <td><code>${escapeHtml(tx.code)}</code></td>
                <td>${valueFormatted}</td>
                <td><strong>${tx.price} €</strong></td>
                <td>${formatParisDate(tx.createdAt)}</td>
            `;
            tr.addEventListener('contextmenu', (e) => {
                showDynamicContextMenu(e, [
                    { label: '👤 Inspecter cet Utilisateur', action: () => window.redirectToUser(tx.userId) },
                    { label: '💰 Voir ses Rechargements', action: () => window.filterPaymentsByUser(tx.userId) },
                    { divider: true },
                    { label: '📦 Copier Code / Info', action: () => { navigator.clipboard.writeText(tx.code); showToast(`Code ${tx.code} copié !`, 'info'); } },
                    { label: '🏷️ Copier la Marque', action: () => { navigator.clipboard.writeText(tx.brand); showToast(`Marque ${tx.brand} copiée !`, 'info'); } },
                    { label: '📋 Copier ID Telegram', action: () => { navigator.clipboard.writeText(String(tx.userId)); showToast(`ID ${tx.userId} copié !`, 'info'); } }
                ]);
            });
            tbody.appendChild(tr);
        });
    }

    const txSearchInput = document.getElementById('tx-search-input');
    if (txSearchInput) {
        txSearchInput.addEventListener('input', () => {
            transactionsCurrentPage = 1;
            applyTransactionsPagination();
        });
    }
    const txSearchClear = document.getElementById('tx-search-clear');
    if (txSearchClear) {
        txSearchClear.addEventListener('click', () => {
            if (txSearchInput) txSearchInput.value = '';
            transactionsCurrentPage = 1;
            applyTransactionsPagination();
        });
    }

    const txPerPageSelect = document.getElementById('transactions-per-page-select');
    if (txPerPageSelect) {
        txPerPageSelect.addEventListener('change', (e) => {
            transactionsPerPage = e.target.value;
            transactionsCurrentPage = 1;
            applyTransactionsPagination();
        });
    }
    const btnTxPrev = document.getElementById('btn-transactions-prev');
    if (btnTxPrev) {
        btnTxPrev.addEventListener('click', () => {
            if (transactionsCurrentPage > 1) {
                transactionsCurrentPage--;
                applyTransactionsPagination();
            }
        });
    }
    const btnTxNext = document.getElementById('btn-transactions-next');
    if (btnTxNext) {
        btnTxNext.addEventListener('click', () => {
            transactionsCurrentPage++;
            applyTransactionsPagination();
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

    // [ PAYMENTS PAGINATION LOGIC ] ==========================================
    let rawPaymentsData = [];
    let paymentsCurrentPage = 1;
    let paymentsPerPage = '10';

    async function loadPaymentsData() {
        const data = await apiRequest('/payments');
        if (!data) return;
        rawPaymentsData = data.payments || [];
        paymentsCurrentPage = 1;
        applyPaymentsPagination();
    }

    window.filterPayments = function() {
        paymentsCurrentPage = 1;
        applyPaymentsPagination();
    };

    function applyPaymentsPagination() {
        const query = (document.getElementById('payments-search-input')?.value || '').trim().toLowerCase();
        const filterValue = (document.getElementById('filter-payments-method')?.value || '').toUpperCase();
        
        const clearBtn = document.getElementById('payments-search-clear');
        if (clearBtn) clearBtn.style.display = query ? 'block' : 'none';

        let filtered = rawPaymentsData;
        if (filterValue) {
            filtered = filtered.filter(p => (p.method || '').toUpperCase() === filterValue);
        }
        if (query) {
            filtered = filtered.filter(p => 
                String(p.chatId || '').toLowerCase().includes(query) ||
                String(p.trackId || '').toLowerCase().includes(query) ||
                String(p.method || '').toLowerCase().includes(query) ||
                String(p.status || '').toLowerCase().includes(query) ||
                String(p.id || '').toLowerCase().includes(query)
            );
        }

        const totalItems = filtered.length;
        let perPage = paymentsPerPage === 'all' ? totalItems : parseInt(paymentsPerPage) || 10;
        if (perPage <= 0) perPage = 10;

        const totalPages = Math.ceil(totalItems / perPage) || 1;
        if (paymentsCurrentPage > totalPages) paymentsCurrentPage = totalPages;
        if (paymentsCurrentPage < 1) paymentsCurrentPage = 1;

        const startIdx = (paymentsCurrentPage - 1) * perPage;
        const endIdx = paymentsPerPage === 'all' ? totalItems : Math.min(startIdx + perPage, totalItems);
        const pageItems = filtered.slice(startIdx, endIdx);

        const infoElem = document.getElementById('payments-pagination-info');
        if (infoElem) {
            infoElem.innerText = totalItems > 0 
                ? `Affichage ${startIdx + 1}-${endIdx} sur ${totalItems} rechargement(s)`
                : `Aucun rechargement trouvé`;
        }

        const pageIndicator = document.getElementById('payments-page-indicator');
        if (pageIndicator) {
            pageIndicator.innerText = `Page ${paymentsCurrentPage} / ${totalPages}`;
        }

        const btnPrev = document.getElementById('btn-payments-prev');
        const btnNext = document.getElementById('btn-payments-next');
        if (btnPrev) btnPrev.disabled = paymentsCurrentPage <= 1;
        if (btnNext) btnNext.disabled = paymentsCurrentPage >= totalPages;

        renderPaymentsTable(pageItems);
    }

    const pmSearchInput = document.getElementById('payments-search-input');
    if (pmSearchInput) {
        pmSearchInput.addEventListener('input', () => {
            paymentsCurrentPage = 1;
            applyPaymentsPagination();
        });
    }
    const pmSearchClear = document.getElementById('payments-search-clear');
    if (pmSearchClear) {
        pmSearchClear.addEventListener('click', () => {
            if (pmSearchInput) pmSearchInput.value = '';
            paymentsCurrentPage = 1;
            applyPaymentsPagination();
        });
    }

    function renderPaymentsTable(payments) {
        const tbody = document.getElementById('all-payments-table');
        tbody.innerHTML = '';
        if (payments.length === 0) {
            tbody.innerHTML = `<tr><td colspan="7" style="text-align: center; color: var(--text-secondary); padding: 24px;">Aucun rechargement trouvé.</td></tr>`;
            return;
        }

        payments.forEach(p => {
            const tr = document.createElement('tr');
            tr.style.cursor = 'context-menu';
            const statusStr = p.status ? p.status.toUpperCase() : 'INCONNU';
            let statusBadge = `<span class="badge badge-warning">${p.status || 'INCONNU'}</span>`;
            if (statusStr === 'PAID') statusBadge = `<span class="badge badge-success">PAYÉ</span>`;
            else if (statusStr === 'FAILED' || statusStr === 'CANCELED') statusBadge = `<span class="badge badge-danger">ÉCHOUÉ</span>`;
            else if (statusStr === 'EXPIRED') statusBadge = `<span class="badge badge-muted">EXPIRÉ</span>`;

            const amountSafe = Number(p.amount || 0).toFixed(2);
            
            tr.innerHTML = `
                <td>#${p.id}</td>
                <td style="cursor: pointer; color: var(--accent-primary);" onclick="window.redirectToUser('${p.chatId}')"><code>${p.chatId || 'N/A'}</code></td>
                <td><strong>${p.method || 'N/A'}</strong></td>
                <td><strong>${amountSafe} €</strong></td>
                <td>${statusBadge}</td>
                <td><code>${p.trackId || 'N/A'}</code></td>
                <td>${p.createdAt ? formatParisDate(p.createdAt) : 'N/A'}</td>
            `;
            tr.addEventListener('contextmenu', (e) => {
                showDynamicContextMenu(e, [
                    { label: '👤 Inspecter cet Utilisateur', action: () => window.redirectToUser(p.chatId) },
                    { label: '🛒 Voir ses Achats', action: () => window.filterTransactionsByUser(p.chatId) },
                    { divider: true },
                    { label: '📋 Copier ID Telegram', action: () => { if (p.chatId) { navigator.clipboard.writeText(String(p.chatId)); showToast(`ID ${p.chatId} copié !`, 'info'); } } },
                    { label: '💳 Copier le Track ID', action: () => { if (p.trackId) { navigator.clipboard.writeText(p.trackId); showToast(`Track ID ${p.trackId} copié !`, 'info'); } } },
                    { label: '💰 Copier le Montant', action: () => { navigator.clipboard.writeText(`${amountSafe} €`); showToast(`Montant ${amountSafe} € copié !`, 'info'); } }
                ]);
            });
            tbody.appendChild(tr);
        });
    }

    const pmPerPageSelect = document.getElementById('payments-per-page-select');
    if (pmPerPageSelect) {
        pmPerPageSelect.addEventListener('change', (e) => {
            paymentsPerPage = e.target.value;
            paymentsCurrentPage = 1;
            applyPaymentsPagination();
        });
    }
    const btnPmPrev = document.getElementById('btn-payments-prev');
    if (btnPmPrev) {
        btnPmPrev.addEventListener('click', () => {
            if (paymentsCurrentPage > 1) {
                paymentsCurrentPage--;
                applyPaymentsPagination();
            }
        });
    }
    const btnPmNext = document.getElementById('btn-payments-next');
    if (btnPmNext) {
        btnPmNext.addEventListener('click', () => {
            paymentsCurrentPage++;
            applyPaymentsPagination();
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

    const pwdForm = document.getElementById('settings-password-form');
    if (pwdForm) {
        pwdForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const pass = document.getElementById('setting-admin-password').value;
            const confirmPass = document.getElementById('setting-admin-password-confirm').value;

            if (pass !== confirmPass) {
                showToast('Les mots de passe ne correspondent pas', 'danger');
                return;
            }

            const res = await apiRequest('/settings/password', 'POST', { password: pass });
            if (res && res.success) {
                authToken = pass;
                localStorage.setItem('admin_auth_token', pass);
                localStorage.setItem('admin_auth_token_time', Date.now().toString());
                showToast('Mot de passe administrateur mis à jour avec succès !', 'success');
                document.getElementById('setting-admin-password').value = '';
                document.getElementById('setting-admin-password-confirm').value = '';
            } else {
                showToast(res ? res.message : 'Erreur lors du changement de mot de passe', 'danger');
            }
        });
    }

    document.querySelectorAll('.toggle-pwd-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const targetId = btn.getAttribute('data-target');
            if (!targetId) return;
            const input = document.getElementById(targetId);
            if (!input) return;

            if (input.type === 'password') {
                input.type = 'text';
                btn.innerText = '🙈';
            } else {
                input.type = 'password';
                btn.innerText = '👁️';
            }
        });
    });

    if (authToken) {
        initApp();
    }
});
