const API_URL = '/api';

// --- State ---
let authToken = localStorage.getItem('token');
let userRole = localStorage.getItem('role');
let activeGame = null;

// --- DOM Elements ---
const views = {
    auth: document.getElementById('auth-view'),
    admin: document.getElementById('admin-view'),
    student: document.getElementById('student-view'),
    game: document.getElementById('game-ui-view')
};

// Toggle Views
function showView(viewName) {
    Object.values(views).forEach(v => v.classList.remove('active'));
    if(views[viewName]) views[viewName].classList.add('active');
}

// --- Init ---
function init() {
    if (authToken) {
        if (userRole === 'Admin') initAdmin();
        else initStudent();
    } else {
        showView('auth');
    }
}

// --- Auth Flow UI Toggles ---
let isStudentLogin = true;
let isAdminLogin = true;

document.getElementById('btn-student-register-toggle').onclick = () => {
    isStudentLogin = !isStudentLogin;
    document.getElementById('btn-student-login').innerText = isStudentLogin ? "Sign In" : "Register Student";
    document.getElementById('student-auth-mode-text').innerText = isStudentLogin ? "Log in to enter the game" : "Create a student account";
    document.getElementById('btn-student-register-toggle').innerText = isStudentLogin ? "Sign up" : "Sign in";
};

document.getElementById('btn-admin-register-toggle').onclick = () => {
    isAdminLogin = !isAdminLogin;
    document.getElementById('btn-admin-login').innerText = isAdminLogin ? "Authenticate" : "Register Admin";
    document.getElementById('admin-auth-mode-text').innerText = isAdminLogin ? "Admin Access Only" : "Create Administrator Identity";
    document.getElementById('btn-admin-register-toggle').innerText = isAdminLogin ? "Register" : "Login";
    document.getElementById('admin-reg-secret').style.display = isAdminLogin ? "none" : "block";
};

document.getElementById('btn-switch-to-admin').onclick = () => {
    document.getElementById('student-auth-card').style.display = 'none';
    document.getElementById('admin-auth-card').style.display = 'block';
};

document.getElementById('btn-switch-to-student').onclick = () => {
    document.getElementById('admin-auth-card').style.display = 'none';
    document.getElementById('student-auth-card').style.display = 'block';
};

// --- Auth Calls ---
document.getElementById('btn-student-login').onclick = () => authFlow('student', isStudentLogin ? 'login' : 'register');
document.getElementById('btn-admin-login').onclick = () => authFlow('admin', isAdminLogin ? 'login' : 'register');

async function authFlow(roleType, actionType) {
    const errorEl = document.getElementById(`${roleType}-auth-error`);
    errorEl.style.display = 'none';
    const email = document.getElementById(`${roleType}-email`).value;
    const password = document.getElementById(`${roleType}-password`).value;

    let url = `${API_URL}/auth/${actionType}`;
    let body = { email, password };
    if (actionType === 'register') {
        body.role = roleType === 'admin' ? 'Admin' : 'Student';
        if (roleType === 'admin') body.adminSecret = document.getElementById('admin-reg-secret').value;
    }

    const res = await fetch(url, { method: 'POST', headers: { 'Content-Type':'application/json' }, body: JSON.stringify(body) });
    const data = await res.json();

    if (!res.ok) {
        errorEl.style.color = "var(--danger)";
        errorEl.innerText = data.message;
        errorEl.style.display = 'block';
        return;
    }

    if (actionType === 'login') {
        localStorage.setItem('token', data.token);
        localStorage.setItem('role', data.role);
        authToken = data.token;
        userRole = data.role;
        init();
    } else {
        errorEl.style.color = "var(--success)";
        errorEl.innerText = "Registered successfully. Please log in.";
        errorEl.style.display = 'block';
        // Auto swap back to login mode
        if(roleType === 'student' && !isStudentLogin) document.getElementById('btn-student-register-toggle').click();
        if(roleType === 'admin' && !isAdminLogin) document.getElementById('btn-admin-register-toggle').click();
    }
}

function logout() {
    localStorage.clear();
    authToken = null;
    userRole = null;
    showView('auth');
}
document.getElementById('btn-logout-admin').onclick = logout;
document.getElementById('btn-logout-student').onclick = logout;

async function authFetch(url, options = {}) {
    options.headers = { ...options.headers, 'Authorization': `Bearer ${authToken}` };
    const res = await fetch(url, options);
    if(res.status === 401) logout();
    return res;
}

// --- Admin Flow ---
async function initAdmin() {
    showView('admin');
    
    try {
        const qs = await authFetch(`${API_URL}/admin/questionnaires`).then(r => r.json());
        document.getElementById('stat-quests').innerText = qs.length || 0;
        
        const users = await authFetch(`${API_URL}/admin/users`).then(r => r.json());
        document.getElementById('stat-users').innerText = users.length || 0;

        const sessions = await authFetch(`${API_URL}/admin/sessions`).then(r => r.json());
        const sessContainer = document.getElementById('session-list');
        sessContainer.innerHTML = sessions.map(s => `
            <div style="background:rgba(255,255,255,0.05); padding:15px; margin-top:10px; border-radius:8px; border: 1px solid var(--border);">
                <h4>${s.name}</h4>
                <p style="color:var(--text-muted); font-size:0.9rem; margin-top:5px;">Users Assigned: ${s.assignedUsers.length} | Questionnaires: ${s.assignedQuestionnaires.length}</p>
            </div>
        `).join('');

        // Catalog render
        const areas = await authFetch(`${API_URL}/admin/areas`).then(r => r.json());
        const catContainer = document.getElementById('catalog-list');
        catContainer.innerHTML = `
            <h3>Existing Questionnaires</h3>
            <div style="display:flex; flex-wrap:wrap; gap:10px; margin-bottom:20px;">
                ${qs.map(q => `<div style="background:rgba(255,255,255,0.1); padding:10px 15px; border-radius:4px;">${q.title} (${q.questions.length} questions)</div>`).join('')}
            </div>
            <h3>Areas & SubAreas</h3>
            ${areas.map(a => `
                <div style="background:rgba(255,255,255,0.05); padding:10px; margin-top:5px; border-radius:4px;">
                    <strong>${a.name}</strong> 
                    <span style="color:var(--text-muted); font-size:0.85rem">(${a.subAreas.length} subareas)</span>
                    <div style="padding-left:20px; font-size:0.9rem; margin-top:5px; color:var(--primary)">
                         ${a.subAreas.map(sa => sa.name).join(', ')}
                    </div>
                </div>
            `).join('')}
        `;
        
        // Reference Tables for Session Mapping
        document.getElementById('maphelp-users').innerHTML = `<strong>Users List (ID map)</strong><br>` + users.map(u => `[${u.id}] ${u.email}`).join('<br>');
        document.getElementById('maphelp-quests').innerHTML = `<strong>Quests List (ID map)</strong><br>` + qs.map(q => `[${q.id}] ${q.title}`).join('<br>');
        
        await fetchRankings();
    } catch(e) {
        console.error("Failed to load admin stats:", e);
    }
}

const fetchRankings = async () => {
    try {
        const res = await authFetch(`${API_URL}/admin/rankings`);
        if(!res) return;
        const ranks = await res.json();
        const container = document.getElementById('rankings-list');
        container.innerHTML = ranks.map((r, i) => `
            <div style="background:rgba(0,0,0,0.3); padding:15px; border-radius:8px; margin-bottom:10px;">
                <h3 style="margin:0; color:var(--primary);">#${i+1} : ${r.subCriteriaName} (Score: ${r.totalScore})</h3>
                <ul style="margin-top:10px; font-size:0.9rem;">
                    ${r.sessionBreakdown.map(s => `<li>Session '${s.sessionName}': ${s.totalSessionScore} points</li>`).join('')}
                </ul>
            </div>
        `).join('');
    } catch(e) { console.error(e); }
};

document.getElementById('btn-create-area').onclick = async () => {
    const input = document.getElementById('in-area-name');
    const name = input.value;
    if (!name) return;
    await authFetch(`${API_URL}/admin/area`, { method: 'POST', body: JSON.stringify({ name }), headers: {'Content-Type': 'application/json'} });
    input.value = '';
    initAdmin();
};

document.getElementById('btn-create-subarea').onclick = async () => {
    const inputName = document.getElementById('in-subarea-name');
    const inputId = document.getElementById('in-subarea-areaid');
    const name = inputName.value;
    const areaId = parseInt(inputId.value);
    if (!name || isNaN(areaId)) return;
    await authFetch(`${API_URL}/admin/subarea`, { method: 'POST', body: JSON.stringify({ name, areaId }), headers: {'Content-Type': 'application/json'} });
    inputName.value = ''; inputId.value = '';
    initAdmin();
};

document.getElementById('btn-create-quest').onclick = async () => {
    const input = document.getElementById('in-quest-title');
    const title = input.value;
    if (!title) return;
    await authFetch(`${API_URL}/admin/questionnaire`, { method: 'POST', body: JSON.stringify({ title, themeColor: "#ec4899", backgroundColor: "#3b82f6" }), headers: {'Content-Type': 'application/json'} });
    input.value = '';
    initAdmin();
};

document.getElementById('btn-add-question').onclick = async () => {
    const qId = parseInt(document.getElementById('in-q-questid').value);
    const subId = parseInt(document.getElementById('in-q-subid').value);
    const text = document.getElementById('in-q-text').value;
    const optsStr = document.getElementById('in-q-opts').value;
    const correct = document.getElementById('in-q-correct').value;
    
    if(isNaN(qId) || isNaN(subId) || !text || !optsStr || !correct) return alert("Fill all fields properly!");
    const optsJson = JSON.stringify(optsStr.split(',').map(s=>s.trim()));
    
    await authFetch(`${API_URL}/admin/question`, { 
        method: 'POST', 
        body: JSON.stringify({ questionnaireId: qId, subAreaId: subId, text: text, options: optsJson, correctAnswer: correct, points: 10 }), 
        headers: {'Content-Type': 'application/json'} 
    });
    
    document.getElementById('in-q-text').value = '';
    initAdmin();
};

document.getElementById('btn-create-session').onclick = async () => {
    const input = document.getElementById('in-session-name');
    const name = input.value;
    if (!name) return;
    await authFetch(`${API_URL}/admin/session`, { method: 'POST', body: JSON.stringify({ name }), headers: {'Content-Type': 'application/json'} });
    input.value = '';
    initAdmin();
};

document.getElementById('btn-map-user').onclick = async () => {
    const sId = parseInt(document.getElementById('in-map-sessid').value);
    const uId = parseInt(document.getElementById('in-map-userid').value);
    if(isNaN(sId) || isNaN(uId)) return;
    await authFetch(`${API_URL}/admin/session/${sId}/assign-user/${uId}`, { method: 'POST' });
    initAdmin();
};

document.getElementById('btn-map-quest').onclick = async () => {
    const sId = parseInt(document.getElementById('in-mapq-sessid').value);
    const qId = parseInt(document.getElementById('in-mapq-questid').value);
    if(isNaN(sId) || isNaN(qId)) return;
    await authFetch(`${API_URL}/admin/session/${sId}/assign-quest/${qId}`, { method: 'POST' });
    initAdmin();
};



document.querySelectorAll('.nav-btn[data-target]').forEach(btn => {
    btn.onclick = (e) => {
        document.querySelectorAll('.nav-btn').forEach(b => b.classList.remove('active'));
        e.target.classList.add('active');
        document.querySelectorAll('.admin-section').forEach(s => s.classList.remove('active'));
        document.getElementById(`admin-${e.target.dataset.target}`).classList.add('active');
    };
});

document.getElementById('btn-download-excel').onclick = async () => {
    const res = await authFetch(`${API_URL}/adminexport/export`);
    const blob = await res.blob();
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `QuizResults_${new Date().getTime()}.xlsx`;
    document.body.appendChild(a);
    a.click();
    a.remove();
};


// --- Student Flow & Game ---
async function initStudent() {
    showView('student');
    const res = await authFetch(`${API_URL}/game/my-sessions`);
    const sessions = await res.json();
    const container = document.getElementById('student-sessions-list');
    container.innerHTML = '';

    if (!sessions || sessions.length === 0) {
        container.innerHTML = `<p style="color:var(--text-muted)">You have no assigned quests right now.</p>`;
        return;
    }

    sessions.forEach(s => {
        s.questionnaires.forEach(q => {
            const div = document.createElement('div');
            div.className = 'quest-card';
            div.style.borderColor = q.themeColor;
            div.innerHTML = `<h3 style="color:${q.themeColor}; font-size:1.5rem; margin-bottom:10px">${q.title}</h3><p>Session: ${s.name}</p>`;
            div.onclick = () => startGame(q.id);
            container.appendChild(div);
        });
    });
}

// Game Core execution
async function startGame(qId) {
    const res = await authFetch(`${API_URL}/game/questionnaire/${qId}`);
    const qData = await res.json();
    activeGame = {
        data: qData,
        currentQIndex: 0,
        score: 0,
        timeSeconds: 0,
        timerInterval: null
    };
    
    // Apply theme mapping
    document.getElementById('game-ui-view').style.background = qData.backgroundColor;
    document.querySelector('.progress-bar').style.background = qData.themeColor;

    showView('game');
    renderQuestion();
}

function clearTimer() { if(activeGame.timerInterval) clearInterval(activeGame.timerInterval); }
function startTimer() {
    activeGame.timeSeconds = 0;
    activeGame.timerInterval = setInterval(() => {
        activeGame.timeSeconds++;
        const mins = String(Math.floor(activeGame.timeSeconds / 60)).padStart(2, '0');
        const secs = String(activeGame.timeSeconds % 60).padStart(2, '0');
        document.getElementById('game-time').innerText = `${mins}:${secs}`;
    }, 1000);
}

function renderQuestion() {
    clearTimer();
    const q = activeGame.data.questions[activeGame.currentQIndex];
    if (!q) {
        // Game over map
        document.getElementById('q-text').innerHTML = `Quest Complete!<br>Final Score: <span style="color:var(--success)">${activeGame.score}</span>`;
        document.getElementById('q-options').innerHTML = `<button class="btn-primary" onclick="initStudent()">Back to Dashboard</button>`;
        document.getElementById('btn-skip').style.display = 'none';
        document.querySelector('.progress-bar').style.width = `100%`;
        clearTimer();
        return;
    }

    document.getElementById('btn-skip').style.display = 'block';
    
    // Progress Calculation
    let pct = (activeGame.currentQIndex / activeGame.data.questions.length) * 100;
    document.querySelector('.progress-bar').style.width = `${pct}%`;

    // Render text and options
    document.getElementById('q-text').innerText = q.text;
    document.getElementById('q-score').innerText = activeGame.score;

    const optionsContainer = document.getElementById('q-options');
    optionsContainer.innerHTML = '';
    
    let opts = [];
    try { opts = JSON.parse(q.options); } catch(e) { opts = q.options.split(','); }

    opts.forEach(opt => {
        const btn = document.createElement('button');
        btn.className = 'option-btn';
        btn.innerText = opt;
        btn.onclick = () => submitAnswer(btn, opt, false);
        optionsContainer.appendChild(btn);
    });

    startTimer();
}

document.getElementById('btn-skip').onclick = () => {
    submitAnswer(null, "", true);
};

async function submitAnswer(btnEl, choice, isSkipped) {
    clearTimer();
    const q = activeGame.data.questions[activeGame.currentQIndex];
    
    // Disable buttons
    document.querySelectorAll('.option-btn').forEach(b => b.disabled = true);
    document.getElementById('btn-skip').style.display = 'none';

    // Submit API
    const res = await authFetch(`${API_URL}/game/submit-answer`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            questionId: q.id,
            choice: choice,
            isSkipped: isSkipped,
            timeTakenSeconds: activeGame.timeSeconds,
            overrideSubAreaId: null // User logic can be implemented in a dialog box
        })
    });
    
    const outcome = await res.json();
    
    if (btnEl) {
        if (outcome.isCorrect) btnEl.classList.add('correct');
        else btnEl.classList.add('wrong');
    }

    activeGame.score += outcome.pointsEarned;

    // Small delay micro-animation logic before moving onto the next
    setTimeout(() => {
        activeGame.currentQIndex++;
        // Play out transition animation
        const cd = document.getElementById('question-card');
        cd.style.animation = 'none';
        cd.offsetHeight; // trigger reflow
        cd.style.animation = 'fadeScale 0.5s ease forwards';
        renderQuestion();
    }, 1200);
}

// Start
init();
