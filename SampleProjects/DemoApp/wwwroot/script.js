const API_BASE = '';

let currentSection = 'keys';

async function fetchJson(url, options = {}) {
    try {
        const response = await fetch(url, options);
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
        }
        return await response.json();
    } catch (error) {
        return { error: error.message };
    }
}

async function showSection(sectionId) {
    if (currentSection === sectionId) return;
    
    document.querySelectorAll('section').forEach(sec => sec.classList.add('hidden'));
    document.getElementById(sectionId).classList.remove('hidden');
    
    document.querySelectorAll('nav button').forEach(btn => {
        btn.style.background = btn.style.background === 'rgb(52, 152, 219)' ? '#3498db' : '#3498db';
    });
    
    currentSection = sectionId;
    
    if (sectionId === 'actors') {
        loadActors();
    } else if (sectionId === 'status') {
        checkStatus();
    } else if (sectionId === 'activities') {
        loadActivities();
    }
}

document.getElementById('generateKeysBtn').addEventListener('click', async () => {
    const btn = document.getElementById('generateKeysBtn');
    const resultDiv = document.getElementById('keysResult');
    
    btn.disabled = true;
    resultDiv.textContent = 'Generating keys...';
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/keys`);
        resultDiv.innerHTML = `<strong>Private Key:</strong>\n${data.privateKey}\n\n<strong>Public Key:</strong>\n${data.publicKey}`;
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    } finally {
        btn.disabled = false;
    }
});

document.getElementById('createActorBtn').addEventListener('click', async () => {
    const usernameInput = document.getElementById('username');
    const resultDiv = document.getElementById('actorsList');
    const username = usernameInput.value.trim();
    
    if (!username) {
        alert('Please enter a username');
        return;
    }
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/actors`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(username)
        });
        
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
        } else {
            resultDiv.innerHTML = `Actor created:\n${JSON.stringify(data, null, 2)}`;
            usernameInput.value = '';
            await loadActors();
        }
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
});

async function loadActors() {
    const resultDiv = document.getElementById('actorsList');
    try {
        const data = await fetchJson(`${API_BASE}/demo/actors`);
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
        } else if (Array.isArray(data)) {
            resultDiv.innerHTML = data.length
                ? data.map(a => `ID: ${a.id}, Username: ${a.username}`).join('\n')
                : 'No actors found. Create one above!';
        } else {
            resultDiv.textContent = JSON.stringify(data, null, 2);
        }
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

document.getElementById('submitActivityBtn').addEventListener('click', async () => {
    const activityId = document.getElementById('activityId').value.trim();
    const jsonData = document.getElementById('jsonData').value.trim();
    const resultDiv = document.getElementById('activitiesResult');
    
    if (!activityId || !jsonData) {
        alert('Please fill in both fields');
        return;
    }
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/activities`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ activityId, jsonData })
        });
        
        resultDiv.innerHTML = data.error
            ? `Error: ${data.error}`
            : `Activity submitted:\n${JSON.stringify(data, null, 2)}`;
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
});

async function checkStatus() {
    const resultDiv = document.getElementById('statusResult');
    try {
        const data = await fetchJson(`${API_BASE}/demo/status`);
        resultDiv.innerHTML = data.error
            ? `Error: ${data.error}`
            : JSON.stringify(data, null, 2);
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

document.getElementById('statusBtn').addEventListener('click', checkStatus);

document.addEventListener('DOMContentLoaded', () => {
    loadActors();
    loadActivities();
});

let currentPage = 1;
const pageSize = 5;

async function loadActivities() {
    const resultDiv = document.getElementById('activityStream');
    const pageInfo = document.getElementById('pageInfo');
    const prevBtn = document.getElementById('prevPageBtn');
    const nextBtn = document.getElementById('nextPageBtn');
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/activities/paginated?page=${currentPage}&pageSize=${pageSize}`);
        
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
            return;
        }
        
        pageInfo.textContent = `Page ${data.page} of ${data.totalPages}`;
        prevBtn.disabled = data.page <= 1;
        nextBtn.disabled = data.page >= data.totalPages;
        
        if (!data.data || data.data.length === 0) {
            resultDiv.textContent = 'No activities found. Submit one above!';
            return;
        }
        
        resultDiv.innerHTML = data.data.map(a => 
            `<div class="activity-item"><strong>ID:</strong> ${a.activityId}<br><pre>${a.jsonData}</pre></div>`
        ).join('<hr>');
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

document.getElementById('prevPageBtn').addEventListener('click', () => {
    if (currentPage > 1) {
        currentPage--;
        loadActivities();
    }
});

document.getElementById('nextPageBtn').addEventListener('click', () => {
    currentPage++;
    loadActivities();
});
