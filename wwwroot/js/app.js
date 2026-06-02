const { createApp } = Vue;

createApp({
    data() {
        return {
            API_URL: '/api',
            authToken: localStorage.getItem('token'),
            userRole: localStorage.getItem('role'),
            currentView: null,
            authMode: 'student',
            studentEmail: '',
            studentPassword: '',
            studentIsLogin: true,
            adminEmail: '',
            adminPassword: '',
            adminSecret: '',
            adminIsLogin: true,
            authError: '',
            authErrorIsSuccess: false,
            adminSection: 'dashboard',
            statUsers: 0,
            statQuests: 0,
            questionnaires: [],
            areas: [],
            catalogView: 'questionnaires',
            newAreaName: '',
            newSubAreaName: '',
            newSubAreaAreaId: '',
            newQuestTitle: '',
            aiSubAreaOptions: [],
            indexSubAreaId: '',
            indexLoading: false,
            indexStatus: null,
            genSubAreaId: '',
            genLoading: false,
            genStatus: null,
            pendingQuestions: [],
            pendingLoading: false,
            sessions: [],
            users: [],
            newSessionName: '',
            mapSessId: '',
            mapUserId: '',
            mapQSessId: '',
            mapQQuestId: '',
            rankings: [],
            editModal: {
                show: false,
                title: '',
                fields: [],
                values: {},
                showDelete: false,
                _onSave: null,
                _onDelete: null
            },
            studentSessions: [],
            game: null,
            activeGuideAction: 'taxonomy'
        };
    },

    computed: {
        subAreaLookup() {
            const lookup = {};
            (this.areas || []).forEach(area => {
                (area.subAreas || []).forEach(sub => {
                    lookup[sub.id] = `${area.name} -> ${sub.name}`;
                });
            });
            return lookup;
        },
        currentQuestion() {
            if (!this.game || !this.game.data) return null;
            return this.game.data.questions[this.game.currentQIndex] || null;
        },
        currentQuestionOptions() {
            if (!this.currentQuestion) return [];
            try { return JSON.parse(this.currentQuestion.options); }
            catch (e) { return (this.currentQuestion.options || '').split(',').map(s => s.trim()); }
        },
        gameProgress() {
            if (!this.game || !this.game.data) return 0;
            return (this.game.currentQIndex / this.game.data.questions.length) * 100;
        },
        gameTimeFormatted() {
            if (!this.game) return '00:00';
            const mins = String(Math.floor(this.game.timeSeconds / 60)).padStart(2, '0');
            const secs = String(this.game.timeSeconds % 60).padStart(2, '0');
            return `${mins}:${secs}`;
        },
        rankingsView() {
            return (this.rankings || []).map(r => {
                const subCriteriaName = r.subCriteriaName ?? r.SubCriteriaName ?? 'Uncategorized';
                const totalScore = Number(r.totalScore ?? r.TotalScore ?? 0);
                const rawBreakdown = r.sessionBreakdown ?? r.SessionBreakdown ?? [];
                const sessionBreakdown = (rawBreakdown || []).map(s => ({
                    sessionName: s.sessionName ?? s.SessionName ?? 'Unknown Session',
                    totalSessionScore: Number(s.totalSessionScore ?? s.TotalSessionScore ?? s.score ?? s.Score ?? 0)
                }));
                return { subCriteriaName, totalScore, sessionBreakdown };
            }).sort((a, b) => b.totalScore - a.totalScore);
        }
    },

    methods: {
        init() {
            if (this.authToken) {
                if (this.userRole === 'Admin') this.initAdmin();
                else this.initStudent();
            } else {
                this.currentView = 'auth';
            }
        },

        async authFlow(roleType, actionType) {
            this.authError = '';
            const email = roleType === 'student' ? this.studentEmail : this.adminEmail;
            const password = roleType === 'student' ? this.studentPassword : this.adminPassword;
            const url = `${this.API_URL}/auth/${actionType}`;
            const body = { email, password };
            if (actionType === 'register') {
                body.role = roleType === 'admin' ? 'Admin' : 'Student';
                if (roleType === 'admin') body.adminSecret = this.adminSecret;
            }

            const res = await fetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });
            const data = await res.json();

            if (!res.ok) {
                this.authErrorIsSuccess = false;
                this.authError = data.message || 'Error occurred';
                return;
            }

            if (actionType === 'login') {
                const expectedRole = roleType === 'admin' ? 'Admin' : 'Student';
                if (data.role !== expectedRole) {
                    this.authErrorIsSuccess = false;
                    this.authError = roleType === 'student'
                        ? 'This account is an Admin account. Please use Admin Access.'
                        : 'This account is a Student account. Please use the Student portal.';
                    return;
                }

                localStorage.setItem('token', data.token);
                localStorage.setItem('role', data.role);
                this.authToken = data.token;
                this.userRole = data.role;
                this.init();
            } else {
                this.authErrorIsSuccess = true;
                this.authError = 'Registered successfully. Please log in.';
                if (roleType === 'student') this.studentIsLogin = true;
                if (roleType === 'admin') this.adminIsLogin = true;
            }
        },

        logout() {
            localStorage.clear();
            this.authToken = null;
            this.userRole = null;
            this.currentView = 'auth';
            this.game = null;
            this.clearTimer();
        },

        async authFetch(url, options = {}) {
            options.headers = { ...options.headers, Authorization: `Bearer ${this.authToken}` };
            const res = await fetch(url, options);
            if (res.status === 401) this.logout();
            return res;
        },

        async initAdmin() {
            this.currentView = 'admin';
            this.adminSection = 'dashboard';
            try {
                const [qs, users, sessions, areas] = await Promise.all([
                    this.authFetch(`${this.API_URL}/admin/questionnaires`).then(r => r.json()),
                    this.authFetch(`${this.API_URL}/admin/users`).then(r => r.json()),
                    this.authFetch(`${this.API_URL}/admin/sessions`).then(r => r.json()),
                    this.authFetch(`${this.API_URL}/admin/areas`).then(r => r.json())
                ]);

                this.questionnaires = qs || [];
                this.users = users || [];
                this.sessions = sessions || [];
                this.areas = areas || [];
                this.statQuests = this.questionnaires.length;
                this.statUsers = this.users.length;

                this.aiSubAreaOptions = [];
                this.areas.forEach(area => {
                    (area.subAreas || []).forEach(sub => {
                        this.aiSubAreaOptions.push({
                            value: String(sub.id),
                            label: `${area.name} -> ${sub.name} (ID: ${sub.id})`
                        });
                    });
                });

                await this.fetchRankings();
                this.$nextTick(() => this.setGuideAction('taxonomy'));
            } catch (e) {
                console.error('Failed to load admin data:', e);
            }
        },

        async createArea() {
            if (!this.newAreaName) return;
            await this.authFetch(`${this.API_URL}/admin/area`, {
                method: 'POST',
                body: JSON.stringify({ name: this.newAreaName }),
                headers: { 'Content-Type': 'application/json' }
            });
            this.newAreaName = '';
            await this.initAdmin();
        },

        async createSubArea() {
            const areaId = parseInt(this.newSubAreaAreaId);
            if (!this.newSubAreaName || isNaN(areaId)) return;
            await this.authFetch(`${this.API_URL}/admin/subarea`, {
                method: 'POST',
                body: JSON.stringify({ name: this.newSubAreaName, areaId }),
                headers: { 'Content-Type': 'application/json' }
            });
            this.newSubAreaName = '';
            this.newSubAreaAreaId = '';
            await this.initAdmin();
        },

        async createQuestionnaire() {
            if (!this.newQuestTitle) return;
            await this.authFetch(`${this.API_URL}/admin/questionnaire`, {
                method: 'POST',
                body: JSON.stringify({ title: this.newQuestTitle, themeColor: '#ec4899', backgroundColor: '#3b82f6' }),
                headers: { 'Content-Type': 'application/json' }
            });
            this.newQuestTitle = '';
            await this.initAdmin();
        },

        async createSession() {
            if (!this.newSessionName) return;
            await this.authFetch(`${this.API_URL}/admin/session`, {
                method: 'POST',
                body: JSON.stringify({ name: this.newSessionName }),
                headers: { 'Content-Type': 'application/json' }
            });
            this.newSessionName = '';
            await this.initAdmin();
        },

        async mapUserToSession() {
            const sId = parseInt(this.mapSessId);
            const uId = parseInt(this.mapUserId);
            if (isNaN(sId) || isNaN(uId)) return;
            const res = await this.authFetch(`${this.API_URL}/admin/session/${sId}/assign-user/${uId}`, { method: 'POST' });
            if (!res.ok) {
                const d = await res.json().catch(() => ({}));
                alert(d.error || d.message || 'Failed');
                return;
            }
            await this.initAdmin();
        },

        async mapQuestToSession() {
            const sId = parseInt(this.mapQSessId);
            const qId = parseInt(this.mapQQuestId);
            if (isNaN(sId) || isNaN(qId)) return;
            const res = await this.authFetch(`${this.API_URL}/admin/session/${sId}/assign-questionnaire/${qId}`, { method: 'POST' });
            if (!res.ok) {
                const d = await res.json().catch(() => ({}));
                alert(d.error || d.message || 'Failed');
                return;
            }
            await this.initAdmin();
        },

        async fetchRankings() {
            try {
                const res = await this.authFetch(`${this.API_URL}/admin/rankings`);
                if (!res) return;
                const data = await res.json();
                this.rankings = Array.isArray(data) ? data : [];
            } catch (e) {
                console.error(e);
                this.rankings = [];
            }
        },

        async downloadExcel() {
            const res = await this.authFetch(`${this.API_URL}/adminexport/export`);
            const blob = await res.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `QuizResults_${Date.now()}.xlsx`;
            document.body.appendChild(a);
            a.click();
            a.remove();
        },

        async indexQuestions() {
            this.indexLoading = true;
            this.indexStatus = null;
            try {
                const url = this.indexSubAreaId
                    ? `${this.API_URL}/admin/index-questions/${this.indexSubAreaId}`
                    : `${this.API_URL}/admin/index-questions`;
                const res = await this.authFetch(url, { method: 'POST' });
                const data = await res.json();
                this.indexStatus = { ok: res.ok, message: data.message };
            } catch (e) {
                this.indexStatus = { ok: false, message: `Error: ${e.message}` };
            } finally {
                this.indexLoading = false;
            }
        },

        async generateQuestions() {
            if (!this.genSubAreaId) {
                alert('Please select a SubArea first!');
                return;
            }
            this.genLoading = true;
            this.genStatus = null;
            try {
                const res = await this.authFetch(`${this.API_URL}/admin/generate-questions/${this.genSubAreaId}?useRAG=true`, { method: 'POST' });
                const data = await res.json();
                this.genStatus = {
                    ok: res.ok,
                    message: data.message,
                    generatedCount: data.generatedCount,
                    duplicatesFiltered: data.duplicatesFiltered
                };
                if (res.ok) await this.loadPendingQuestions();
            } catch (e) {
                this.genStatus = { ok: false, message: `Error: ${e.message}` };
            } finally {
                this.genLoading = false;
            }
        },

        async loadPendingQuestions() {
            this.pendingLoading = true;
            try {
                const res = await this.authFetch(`${this.API_URL}/admin/pending-questions`);
                this.pendingQuestions = await res.json() || [];
            } catch (e) {
                this.pendingQuestions = [];
            } finally {
                this.pendingLoading = false;
            }
        },

        async approveQuestion(questionId) {
            if (!confirm('Approve this AI-generated question?')) return;
            const res = await this.authFetch(`${this.API_URL}/admin/approve-question/${questionId}`, { method: 'POST' });
            const data = await res.json();
            if (res.ok) {
                await this.loadPendingQuestions();
                await this.initAdmin();
            } else {
                alert(`Error: ${data.message || 'Approval failed'}`);
            }
        },

        async rejectQuestion(questionId) {
            if (!confirm('Reject and delete this AI-generated question?')) return;
            const res = await this.authFetch(`${this.API_URL}/admin/reject-question/${questionId}`, { method: 'DELETE' });
            const data = await res.json();
            if (res.ok) await this.loadPendingQuestions();
            else alert(`Error: ${data.message || 'Rejection failed'}`);
        },

        formatSubAreaLabel(subAreaId) {
            const subId = Number(subAreaId);
            if (!subId) return 'Unassigned';
            const name = this.subAreaLookup[subId];
            return name ? `${name} (ID: ${subId})` : `SubArea ID: ${subId}`;
        },

        parsePendingOptions(optionsStr) {
            try { return JSON.parse(optionsStr); }
            catch (e) { return (optionsStr || '').split(',').map(s => s.trim()); }
        },

        async initStudent() {
            this.currentView = 'student';
            const res = await this.authFetch(`${this.API_URL}/game/my-sessions`);
            this.studentSessions = await res.json() || [];
        },

        async startGame(qId) {
            const res = await this.authFetch(`${this.API_URL}/game/questionnaire/${qId}`);
            const qData = await res.json();
            this.game = {
                data: qData,
                currentQIndex: 0,
                score: 0,
                timeSeconds: 0,
                timerInterval: null,
                gameOver: false,
                answerState: null
            };
            this.currentView = 'game';
            this.startTimer();
        },

        clearTimer() {
            if (this.game && this.game.timerInterval) {
                clearInterval(this.game.timerInterval);
                this.game.timerInterval = null;
            }
        },

        startTimer() {
            this.clearTimer();
            if (!this.game) return;
            this.game.timeSeconds = 0;
            this.game.timerInterval = setInterval(() => {
                if (this.game) this.game.timeSeconds++;
            }, 1000);
        },

        async submitAnswer(choice, isSkipped) {
            if (!this.game || !this.currentQuestion || this.game.answerState) return;
            this.clearTimer();
            const q = this.currentQuestion;
            const res = await this.authFetch(`${this.API_URL}/game/submit-answer`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    questionId: q.id,
                    choice,
                    isSkipped,
                    timeTakenSeconds: this.game.timeSeconds,
                    overrideSubAreaId: null
                })
            });
            const outcome = await res.json();
            this.game.score += outcome.pointsEarned;
            this.game.answerState = {
                chosenAnswer: choice,
                correctAnswer: outcome.correctAnswer || q.correctAnswer
            };
            setTimeout(() => {
                if (!this.game) return;
                this.game.currentQIndex++;
                this.game.answerState = null;
                if (this.game.currentQIndex >= this.game.data.questions.length) {
                    this.game.gameOver = true;
                    this.clearTimer();
                } else {
                    this.$nextTick(() => {
                        const cd = document.getElementById('question-card');
                        if (cd) {
                            cd.style.animation = 'none';
                            cd.offsetHeight;
                            cd.style.animation = 'fadeScale 0.5s ease forwards';
                        }
                    });
                    this.startTimer();
                }
            }, 1200);
        },

        setGuideAction(action) {
            this.activeGuideAction = action;
            this.$nextTick(() => this.updateActionWalkthrough(action));
        },

        updateActionWalkthrough(action) {
            const titleEl = document.getElementById('walkthrough-title');
            const descEl = document.getElementById('walkthrough-desc');
            const consoleBox = document.getElementById('mock-console-box');
            if (!titleEl || !descEl || !consoleBox) return;

            if (action === 'taxonomy') {
                titleEl.innerText = 'Taxonomy Builder Walkthrough';
                descEl.innerHTML = 'Learn how to configure your system curriculum catalog. To group tests correctly, administrators set up a two-tier nested taxonomy. You must add the high-level <strong>Area</strong> first, and then nest the detailed <strong>Sub-Areas</strong> inside it.';
                consoleBox.innerHTML = `
                    <div class="mock-console-wrapper">
                        <div style="font-weight:bold; font-size:0.8rem; color:var(--primary); margin-bottom:12px; text-transform:uppercase; border-bottom:1px solid rgba(255,255,255,0.05); padding-bottom:6px;">Mock Setup Console</div>
                        <div style="display:grid; grid-template-columns:1fr 1fr; gap:15px;">
                            <div style="background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.04); padding:12px; border-radius:10px;">
                                <h5 style="color:#fff; margin-bottom:10px;">Add New Area</h5>
                                <div class="mock-input-row">
                                    <label>New Area Name</label>
                                    <input type="text" id="mock-area-name" class="mock-input" placeholder="e.g. Physics" />
                                </div>
                                <button class="btn-secondary" id="btn-mock-add-area" style="width:100%; font-size:0.8rem; padding:8px;">Add Area</button>
                            </div>
                            <div style="background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.04); padding:12px; border-radius:10px;">
                                <h5 style="color:#fff; margin-bottom:10px;">Add New Sub-Area</h5>
                                <div class="mock-input-row">
                                    <label>New SubArea Name</label>
                                    <input type="text" id="mock-sub-name" class="mock-input" placeholder="e.g. Quantum Mechanics" />
                                </div>
                                <div class="mock-input-row">
                                    <label>Parent Area ID</label>
                                    <input type="number" id="mock-sub-parent-id" class="mock-input" placeholder="e.g. 1" />
                                </div>
                                <button class="btn-secondary" id="btn-mock-add-sub" style="width:100%; font-size:0.8rem; padding:8px;">Add SubArea</button>
                            </div>
                        </div>
                        <div id="mock-taxonomy-notify"></div>
                        <div class="mock-db-preview">
                            <div class="mock-db-title">Database State Mock View</div>
                            <div class="mock-db-card" id="mock-taxonomy-db">[Areas Table]:<br>- [ID: 1] Computer Science<br>- [ID: 2] Mathematics<br><br>[SubAreas Table]:<br>- [ID: 1] Web Development (Parent Area ID: 1)<br>- [ID: 2] Calculus (Parent Area ID: 2)</div>
                        </div>
                    </div>
                `;
                document.getElementById('btn-mock-add-area').onclick = () => {
                    const val = document.getElementById('mock-area-name').value || 'Physics';
                    document.getElementById('mock-taxonomy-notify').innerHTML = `<div class="mock-notification">Simulated Save: Created Area "${val}" in SQLite with generated ID = 3.</div>`;
                };
                document.getElementById('btn-mock-add-sub').onclick = () => {
                    const subName = document.getElementById('mock-sub-name').value || 'Quantum Mechanics';
                    const parentId = parseInt(document.getElementById('mock-sub-parent-id').value) || 3;
                    document.getElementById('mock-taxonomy-notify').innerHTML = `<div class="mock-notification">Simulated Save: Created Sub-Area "${subName}" mapping to Parent Area ID = ${parentId}.</div>`;
                };
            } else if (action === 'content') {
                titleEl.innerText = 'Quiz & Question Generator Walkthrough';
                descEl.innerHTML = 'Learn how to package quiz content. First, you add a <strong>Questionnaire</strong> (which houses the exam/rating), and then you add specific <strong>Questions</strong> by targeting the Questionnaire and Sub-Area IDs.';
                consoleBox.innerHTML = '<div class="mock-console-wrapper"><div style="font-size:0.9rem; color:var(--text-muted);">Use Catalog to create questionnaires and questions linked by IDs.</div></div>';
            } else if (action === 'sessions') {
                titleEl.innerText = 'Cohort Management Walkthrough';
                descEl.innerHTML = 'Learn how to activate questionnaires for students by creating sessions and mapping users/questionnaires into those cohorts.';
                consoleBox.innerHTML = '<div class="mock-console-wrapper"><div style="font-size:0.9rem; color:var(--text-muted);">Use Sessions panel to map Session ID, User ID, and Questionnaire ID.</div></div>';
            } else if (action === 'reports') {
                titleEl.innerText = 'Reporting & Export Engine Walkthrough';
                descEl.innerHTML = 'Student results aggregate in Evaluation Rankings and can be exported to Excel.';
                consoleBox.innerHTML = '<div class="mock-console-wrapper"><div style="font-size:0.9rem; color:var(--text-muted);">Open Export and click Download Excel Report.</div></div>';
            }
        },

        showEditModal(title, fields, onSave, onDelete) {
            const values = {};
            fields.forEach(f => { values[f.name] = f.value !== undefined ? String(f.value) : ''; });
            this.editModal = {
                show: true,
                title,
                fields,
                values,
                showDelete: !!onDelete,
                _onSave: onSave,
                _onDelete: onDelete
            };
        },

        closeEditModal() {
            this.editModal.show = false;
        },

        async handleModalSave() {
            if (this.editModal._onSave) await this.editModal._onSave(this.editModal.values);
        },

        async handleModalDelete() {
            if (this.editModal._onDelete) await this.editModal._onDelete();
        },

        async openEditArea(id) {
            const area = this.areas.find(a => a.id === id);
            if (!area) return;
            this.showEditModal('Edit Area', [
                { name: 'name', label: 'Area Name', value: area.name, type: 'text' }
            ], async (values) => {
                const res = await this.authFetch(`${this.API_URL}/admin/area/${id}`, {
                    method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name: values.name })
                });
                if (res.ok) { this.closeEditModal(); await this.initAdmin(); }
                else { const d = await res.json(); alert(`Error: ${d.error || 'Update failed'}`); }
            }, async () => {
                if (!confirm('Delete this area? All subareas must be deleted first.')) return;
                const res = await this.authFetch(`${this.API_URL}/admin/area/${id}`, { method: 'DELETE' });
                if (res.ok) { this.closeEditModal(); await this.initAdmin(); }
                else { const d = await res.json(); alert(`Error: ${d.error || 'Deletion failed'}`); }
            });
        },

        async openEditSubArea(id) {
            const allSubAreas = this.areas.flatMap(a => (a.subAreas || []).map(sa => ({ ...sa, areaId: a.id })));
            const subArea = allSubAreas.find(sa => sa.id === id);
            if (!subArea) return;
            this.showEditModal('Edit SubArea', [
                { name: 'name', label: 'SubArea Name', value: subArea.name, type: 'text' },
                { name: 'areaId', label: 'Parent Area', value: String(subArea.areaId), type: 'select', options: this.areas.map(a => ({ value: String(a.id), label: a.name })) }
            ], async (values) => {
                const res = await this.authFetch(`${this.API_URL}/admin/subarea/${id}`, {
                    method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name: values.name, areaId: parseInt(values.areaId) })
                });
                if (res.ok) { this.closeEditModal(); await this.initAdmin(); }
                else { const d = await res.json(); alert(`Error: ${d.error || 'Update failed'}`); }
            }, async () => {
                if (!confirm('Delete this subarea? All questions must be deleted first.')) return;
                const res = await this.authFetch(`${this.API_URL}/admin/subarea/${id}`, { method: 'DELETE' });
                if (res.ok) { this.closeEditModal(); await this.initAdmin(); }
                else { const d = await res.json(); alert(`Error: ${d.error || 'Deletion failed'}`); }
            });
        },

        async openEditQuestionnaire(id) {
            const quest = this.questionnaires.find(q => q.id === id);
            if (!quest) return;
            this.showEditModal('Edit Questionnaire', [
                { name: 'title', label: 'Title', value: quest.title, type: 'text' },
                { name: 'themeColor', label: 'Theme Color', value: quest.themeColor, type: 'text' },
                { name: 'backgroundColor', label: 'Background Color', value: quest.backgroundColor, type: 'text' }
            ], async (values) => {
                const res = await this.authFetch(`${this.API_URL}/admin/questionnaire/${id}`, {
                    method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(values)
                });
                if (res.ok) { this.closeEditModal(); await this.initAdmin(); }
                else { const d = await res.json(); alert(`Error: ${d.error || 'Update failed'}`); }
            }, async () => {
                if (!confirm('Delete this questionnaire? All questions must be deleted first.')) return;
                const res = await this.authFetch(`${this.API_URL}/admin/questionnaire/${id}`, { method: 'DELETE' });
                if (res.ok) { this.closeEditModal(); await this.initAdmin(); }
                else { const d = await res.json(); alert(`Error: ${d.error || 'Deletion failed'}`); }
            });
        },

        async openConfigureQuestionnaire(id) {
            this.showEditModal('Configure Questionnaire: Add Question', [
                { name: 'questionnaireId', label: 'Questionnaire ID', value: String(id), type: 'number' },
                { name: 'subAreaId', label: 'SubArea ID', value: '', type: 'number' },
                { name: 'text', label: 'Question Text', value: '', type: 'textarea' },
                { name: 'options', label: 'Options (comma-separated)', value: '', type: 'text' },
                { name: 'correctAnswer', label: 'Correct Answer', value: '', type: 'text' }
            ], async (values) => {
                const questionnaireId = parseInt(values.questionnaireId);
                const subAreaId = parseInt(values.subAreaId);
                const text = (values.text || '').trim();
                const options = (values.options || '').trim();
                const correctAnswer = (values.correctAnswer || '').trim();
                if (isNaN(questionnaireId) || isNaN(subAreaId) || !text || !options || !correctAnswer) {
                    alert('Please fill all required fields.');
                    return;
                }
                const res = await this.authFetch(`${this.API_URL}/admin/question`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ questionnaireId, subAreaId, text, options: JSON.stringify(options.split(',').map(s => s.trim()).filter(Boolean)), correctAnswer, points: 10 })
                });
                if (res.ok) { this.closeEditModal(); await this.initAdmin(); }
                else { const d = await res.json(); alert(`Error: ${d.error || 'Question creation failed'}`); }
            });
        },

        async openEditQuestion(id) {
            let question = null;
            for (const q of this.questionnaires) {
                const found = (q.questions || []).find(x => x.id === id);
                if (found) { question = { ...found, questionnaireId: q.id }; break; }
            }
            if (!question) return;
            let parsedOpts = question.options;
            try { parsedOpts = JSON.parse(question.options).join(', '); } catch (e) {}
            this.showEditModal('Edit Question', [
                { name: 'text', label: 'Question Text', value: question.text, type: 'textarea' },
                { name: 'options', label: 'Options (comma-separated)', value: parsedOpts, type: 'text' },
                { name: 'correctAnswer', label: 'Correct Answer', value: question.correctAnswer, type: 'text' },
                { name: 'points', label: 'Points', value: String(question.points), type: 'number' }
            ], async (values) => {
                const optsArray = values.options.split(',').map(s => s.trim()).filter(Boolean);
                const res = await this.authFetch(`${this.API_URL}/admin/question/${id}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        questionnaireId: question.questionnaireId,
                        subAreaId: question.subAreaId || 1,
                        text: values.text,
                        options: JSON.stringify(optsArray),
                        correctAnswer: values.correctAnswer,
                        points: parseInt(values.points) || 10
                    })
                });
                if (res.ok) { this.closeEditModal(); await this.initAdmin(); }
                else { const d = await res.json(); alert(`Error: ${d.error || 'Update failed'}`); }
            }, async () => {
                if (!confirm('Delete this question?')) return;
                const res = await this.authFetch(`${this.API_URL}/admin/question/${id}`, { method: 'DELETE' });
                if (res.ok) { this.closeEditModal(); await this.initAdmin(); }
                else { const d = await res.json(); alert(`Error: ${d.error || 'Deletion failed'}`); }
            });
        }
    },

    mounted() {
        this.init();
    }
}).mount('#app');
