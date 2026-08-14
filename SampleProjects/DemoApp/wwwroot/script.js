const API_BASE = '';

let currentSection = 'keys';
let currentTemplate = null;

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
    } else if (sectionId === 'templates') {
        loadTemplates();
    } else if (sectionId === 'queues') {
        loadQueueStats();
    } else if (sectionId === 'http-signature') {
        loadHttpSignatureSection();
    } else if (sectionId === 'federation') {
        loadFederationSection();
    } else if (sectionId === 'service-simulator') {
        loadServiceSimulatorSection();
    } else if (sectionId === 'protocol-debug') {
        loadProtocolDebugSection();
    } else if (sectionId === 'explorer') {
        loadExplorerSection();
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

document.getElementById('templateSelect').addEventListener('change', () => {
    const select = document.getElementById('templateSelect');
    const contentArea = document.getElementById('templateContent');
    
    if (select.value) {
        const templateName = select.options[select.selectedIndex].dataset.description || '';
        contentArea.value = `Description: ${templateName}\n\n[Click "Load Template" to load JSON]`;
    } else {
        contentArea.value = '';
        currentTemplate = null;
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
    loadTemplates();
    loadConfig();
});

document.getElementById('loadConfigBtn').addEventListener('click', loadConfig);
document.getElementById('saveConfigBtn').addEventListener('click', saveConfig);
document.getElementById('validateConfigBtn').addEventListener('click', validateConfig);
document.getElementById('refreshQueueBtn').addEventListener('click', loadQueueStats);
document.getElementById('retryQueueBtn').addEventListener('click', retryQueue);
document.getElementById('clearQueueBtn').addEventListener('click', clearQueue);
document.getElementById('generateTestSignatureBtn').addEventListener('click', generateTestSignature);
document.getElementById('signRequestBtn').addEventListener('click', signOutboundRequest);
document.getElementById('verifySignatureBtn').addEventListener('click', verifySignature);

async function loadConfig() {
    const resultDiv = document.getElementById('configResult');
    try {
        const data = await fetchJson(`${API_BASE}/demo/config`);
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
            return;
        }
        
        document.getElementById('domainInput').value = data.activityPub?.domain || '';
        document.getElementById('userPathInput').value = data.activityPub?.userPath || '';
        document.getElementById('portInput').value = data.activityPub?.port || 8080;
        resultDiv.textContent = 'Configuration loaded successfully';
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

async function saveConfig() {
    const resultDiv = document.getElementById('configResult');
    const config = {
        ActivityPub: {
            Domain: document.getElementById('domainInput').value.trim(),
            UserPath: document.getElementById('userPathInput').value.trim(),
            Port: parseInt(document.getElementById('portInput').value)
        },
        Logging: {
            LogLevel: {
                Default: 'Information',
                'Microsoft.AspNetCore': 'Warning'
            }
        }
    };
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/config`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(config)
        });
        
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
        } else {
            resultDiv.textContent = `Configuration saved: ${JSON.stringify(data, null, 2)}`;
        }
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

async function validateConfig() {
    const resultDiv = document.getElementById('configResult');
    try {
        const data = await fetchJson(`${API_BASE}/demo/config/validation`);
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
            return;
        }
        
        if (data.valid) {
            resultDiv.textContent = '✓ Configuration is valid';
        } else {
            resultDiv.textContent = `✗ Invalid configuration:\n${data.errors?.join('\n') || 'Unknown error'}`;
        }
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

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

async function loadTemplates() {
    const select = document.getElementById('templateSelect');
    select.innerHTML = '<option value="">-- Loading templates... --</option>';
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/templates`);
        if (data.error) {
            select.innerHTML = '<option value="">-- Error loading templates --</option>';
            return;
        }
        
        select.innerHTML = '<option value="">-- Select a template --</option>';
        
        if (data.templates && Array.isArray(data.templates)) {
            data.templates.forEach(template => {
                const option = document.createElement('option');
                option.value = template.id;
                option.textContent = `${template.name} (${template.category})`;
                option.dataset.description = template.description;
                select.appendChild(option);
            });
        }
    } catch (error) {
        select.innerHTML = '<option value="">-- Error loading templates --</option>';
    }
}

document.getElementById('loadTemplateBtn').addEventListener('click', async () => {
    const select = document.getElementById('templateSelect');
    const contentArea = document.getElementById('templateContent');
    const resultDiv = document.getElementById('templateResult');
    
    const selectedId = select.value;
    if (!selectedId) {
        alert('Please select a template');
        return;
    }
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/templates/${selectedId}`);
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
            return;
        }
        
        currentTemplate = data;
        contentArea.value = JSON.stringify(data, null, 2);
        resultDiv.textContent = 'Template loaded successfully';
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
});

document.getElementById('copyTemplateBtn').addEventListener('click', () => {
    const contentArea = document.getElementById('templateContent');
    
    contentArea.select();
    document.execCommand('copy');
    
    const resultDiv = document.getElementById('templateResult');
    resultDiv.textContent = 'Template copied to clipboard!';
});

document.getElementById('submitFromTemplateBtn').addEventListener('click', async () => {
    const resultDiv = document.getElementById('templateResult');
    
    if (!currentTemplate || !currentTemplate.id) {
        alert('Please load a template first');
        return;
    }
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/activities`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                activityId: currentTemplate.id || 'template-' + Date.now(),
                jsonData: JSON.stringify(currentTemplate)
            })
        });
        
        resultDiv.innerHTML = data.error
            ? `Error: ${data.error}`
            : `Activity submitted:\n${JSON.stringify(data, null, 2)}`;
        
        setTimeout(() => {
            loadActivities();
        }, 500);
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
});

async function loadQueueStats() {
    const resultDiv = document.getElementById('queueList');
    try {
        const data = await fetchJson(`${API_BASE}/demo/queues`);
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
            return;
        }
        
        document.getElementById('outboundTotal').textContent = data.outbound?.total || 0;
        document.getElementById('outboundPending').textContent = data.outbound?.pending || 0;
        document.getElementById('outboundProcessing').textContent = data.outbound?.processing || 0;
        document.getElementById('outboundCompleted').textContent = data.outbound?.completed || 0;
        document.getElementById('outboundFailed').textContent = data.outbound?.failed || 0;
        
        document.getElementById('inboundTotal').textContent = data.inbound?.total || 0;
        document.getElementById('inboundPending').textContent = data.inbound?.pending || 0;
        document.getElementById('inboundProcessing').textContent = data.inbound?.processing || 0;
        document.getElementById('inboundCompleted').textContent = data.inbound?.completed || 0;
        document.getElementById('inboundFailed').textContent = data.inbound?.failed || 0;
        
        if (data.items && data.items.length > 0) {
            resultDiv.innerHTML = data.items.slice(0, 10).map(item => 
                `<div class="queue-item">
                    <strong>Type:</strong> ${item.type} | 
                    <strong>Status:</strong> ${item.status} | 
                    <strong>Time:</strong> ${item.timestamp}
                 </div>`
            ).join('<hr>');
        } else {
            resultDiv.textContent = 'No queue items found';
        }
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

async function retryQueue() {
    const resultDiv = document.getElementById('queueList');
    try {
        const data = await fetchJson(`${API_BASE}/demo/queues/retry`, { method: 'POST' });
        resultDiv.innerHTML = data.error
            ? `Error: ${data.error}`
            : `Retry completed: ${JSON.stringify(data, null, 2)}`;
        setTimeout(loadQueueStats, 1000);
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

async function clearQueue() {
    const resultDiv = document.getElementById('queueList');
    if (!confirm('Are you sure you want to clear the queue?')) return;
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/queues/clear`, { method: 'POST' });
        resultDiv.innerHTML = data.error
            ? `Error: ${data.error}`
            : `Queue cleared: ${JSON.stringify(data, null, 2)}`;
        setTimeout(loadQueueStats, 1000);
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

async function generateTestSignature() {
    const resultDiv = document.getElementById('httpSignatureResult');
    try {
        const data = await fetchJson(`${API_BASE}/demo/http-signature/generate-test`);
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
            return;
        }
        
        document.getElementById('keyIdInput').value = data.keyId;
        document.getElementById('privateKeyInput').value = data.privateKey;
        
        resultDiv.innerHTML = `<strong>Test Key Pair Generated:</strong>\nKey ID: ${data.keyId}\n\nPrivate Key:\n${data.privateKey}\n\nExample Signature Headers:\n${JSON.stringify(data.exampleHeaders, null, 2)}`;
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

async function signOutboundRequest() {
    const resultDiv = document.getElementById('httpSignatureResult');
    const config = {
        keyId: document.getElementById('keyIdInput').value.trim(),
        privateKey: document.getElementById('privateKeyInput').value.trim(),
        url: document.getElementById('signUrlInput').value.trim(),
        method: document.getElementById('signMethodSelect').value
    };
    
    if (!config.keyId || !config.privateKey) {
        alert('Please generate or provide a key pair first');
        return;
    }
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/http-signature/sign`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(config)
        });
        
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
        } else {
            resultDiv.innerHTML = `<strong>Request Signed:</strong>\nKey ID: ${data.keyId}\nAlgorithm: ${data.algorithm}\nTimestamp: ${data.timestamp}\n\nSignature Headers:\n${JSON.stringify(data.headers, null, 2)}`;
        }
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

async function verifySignature() {
    const resultDiv = document.getElementById('httpSignatureResult');
    const config = {
        signature: document.getElementById('keyIdInput').value.trim(),
        signedHeaders: document.getElementById('signMethodSelect').value
    };
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/http-signature/verify`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(config)
        });
        
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
        } else {
            resultDiv.innerHTML = `<strong>Signature Verification:</strong>\nValid: ${data.valid}\nSignature: ${data.signature}\nSigned Headers: ${data.signedHeaders}\nTimestamp: ${data.timestamp}`;
        }
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

function loadHttpSignatureSection() {
    document.getElementById('httpSignatureResult').textContent = 'Ready. Generate a test key pair to begin.';
}

function loadFederationSection() {
    document.getElementById('federationResult').textContent = 'Ready. Enter an actor URL or WebFinger resource to discover endpoints.';
}

function loadServiceSimulatorSection() {
    document.getElementById('serviceSimulatorResult').textContent = 'Ready. Provide recipient and activity to simulate.';
}

function loadProtocolDebugSection() {
    document.getElementById('protocolDebugResult').textContent = 'Ready. Select an activity type and validate.';
}

document.getElementById('discoverActorBtn').addEventListener('click', discoverActorEndpoints);
document.getElementById('discoverWebfingerBtn').addEventListener('click', discoverWebfinger);
document.getElementById('simulateReceiveBtn').addEventListener('click', simulateReceiveActivity);
document.getElementById('simulateSendBtn').addEventListener('click', simulateSendActivity);
document.getElementById('validateProtocolBtn').addEventListener('click', validateProtocol);

async function discoverActorEndpoints() {
    const resultDiv = document.getElementById('federationResult');
    const actorUrl = document.getElementById('federationActorInput').value.trim() || 'http://localhost:8080/users/test';
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/federation/discover`);
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
            return;
        }
        
        resultDiv.innerHTML = `<strong>Actor Endpoints Discovered:</strong>\nURL: ${data.actorUrl}\nHealth: ${data.health}\n\nEndpoints:\n- Inbox: ${data.endpoints.inbox}\n- Outbox: ${data.endpoints.outbox}\n- Followers: ${data.endpoints.followers}\n- Following: ${data.endpoints.following}`;
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

async function discoverWebfinger() {
    const resultDiv = document.getElementById('federationResult');
    const resource = document.getElementById('webfingerResourceInput').value.trim();
    
    if (!resource) {
        resultDiv.textContent = 'Please enter a resource (e.g., user@domain.com)';
        return;
    }
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/federation/webfinger?resource=${encodeURIComponent(resource)}`);
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
            return;
        }
        
        resultDiv.innerHTML = `<strong>WebFinger Response:</strong>\nSubject: ${data.subject}\n\nLinks:\n${data.links.map(link => `- ${link.rel}: ${link.href} (type: ${link.type})`).join('\n')}`;
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

async function simulateReceiveActivity() {
    const resultDiv = document.getElementById('serviceSimulatorResult');
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/service/simulate-receive`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({})
        });
        
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
        } else {
            resultDiv.innerHTML = `<strong>Activity Received:</strong>\nSuccess: ${data.success}\nActivity ID: ${data.activityId}\nTimestamp: ${data.timestamp}`;
        }
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

async function simulateSendActivity() {
    const resultDiv = document.getElementById('serviceSimulatorResult');
    const recipient = document.getElementById('simulateRecipientInput').value.trim();
    const activity = document.getElementById('simulateActivityInput').value.trim();
    
    if (!recipient || !activity) {
        resultDiv.textContent = 'Please provide recipient actor URL and activity JSON';
        return;
    }
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/service/simulate-send`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ recipient, activity })
        });
        
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
        } else {
            resultDiv.innerHTML = `<strong>Activity Sent:</strong>\nSuccess: ${data.success}\nRecipient: ${data.recipient}\nActivity ID: ${data.activityId}\nTimestamp: ${data.timestamp}`;
        }
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

async function validateProtocol() {
    const resultDiv = document.getElementById('protocolDebugResult');
    const activityType = document.getElementById('protocolTypeInput').value;
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/protocol/validate?type=${encodeURIComponent(activityType)}`);
        
        resultDiv.innerHTML = `<strong>Protocol Validation:</strong>\nValid: ${data.valid}\nActivity Type: ${data.activityType}\nErrors: ${data.errors?.length || 0}\nWarnings: ${data.warnings?.length || 0}`;
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

document.getElementById('actorUrlInput').addEventListener('change', () => {
    const url = document.getElementById('actorUrlInput').value.trim();
    if (url) {
        document.getElementById('loadActorBtn').disabled = false;
        document.getElementById('loadActivitiesBtn').disabled = false;
    }
});

document.getElementById('loadActorBtn').addEventListener('click', async () => {
    const url = document.getElementById('actorUrlInput').value.trim();
    const resultDiv = document.getElementById('explorerResult');
    
    if (!url) {
        alert('Please enter an actor URL');
        return;
    }
    
    resultDiv.textContent = 'Loading actor profile...';
    
    try {
        const data = await fetchJson(url);
        
        if (data.error) {
            resultDiv.textContent = `Error fetching actor: ${data.error}`;
        } else {
            resultDiv.innerHTML = `<strong>Actor Profile:</strong>\n${JSON.stringify(data, null, 2)}`;
        }
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
});

document.getElementById('loadActivitiesBtn').addEventListener('click', async () => {
    const url = document.getElementById('actorUrlInput').value.trim();
    const resultDiv = document.getElementById('explorerActivities');
    
    if (!url) {
        alert('Please enter an actor URL');
        return;
    }
    
    resultDiv.textContent = 'Loading activity collection...';
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/explorer/activities?actorUrl=${encodeURIComponent(url)}`);
        
        if (data.error) {
            resultDiv.textContent = `Error fetching activities: ${data.error}`;
        } else {
            resultDiv.innerHTML = `<strong>Activity Collection:</strong>\n${JSON.stringify(data, null, 2)}`;
        }
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
});

document.getElementById('traceChainBtn').addEventListener('click', async () => {
    const url = document.getElementById('actorUrlInput').value.trim();
    const resultDiv = document.getElementById('explorerResult');
    
    if (!url) {
        alert('Please enter an actor URL');
        return;
    }
    
    resultDiv.textContent = 'Tracing activity chain...';
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/explorer/trace?actorUrl=${encodeURIComponent(url)}`);
        
        if (data.error) {
            resultDiv.textContent = `Error tracing chain: ${data.error}`;
        } else {
            resultDiv.innerHTML = `<strong>Activity Chain:</strong>\n${JSON.stringify(data, null, 2)}`;
        }
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
});

function loadExplorerSection() {
    document.getElementById('actorUrlInput').value = '';
    document.getElementById('explorerResult').textContent = '';
    document.getElementById('explorerActivities').textContent = '';
    document.getElementById('loadActorBtn').disabled = true;
    document.getElementById('loadActivitiesBtn').disabled = true;
    document.getElementById('traceChainBtn').disabled = true;
}
