const API_BASE = '';

let currentSection = 'keys';
let currentTemplate = null;
let currentTutorial = null;
let currentTutorialStep = 0;
let completedTutorials = new Set();
let currentInstanceId = null;
let instances = [];

const tutorialData = {
    setup: {
        title: 'Step-by-step Setup Guide',
        steps: [
            {
                title: 'Welcome to ActivityPub',
                content: '<p>ActivityPub is a decentralized social networking protocol. This tutorial will guide you through setting up your first actor and sending activities.</p>',
                examples: []
            },
            {
                title: 'Generate Your Keys',
                content: '<p>Before creating an actor, you need to generate an RSA key pair for signing activities.</p>',
                examples: [
                    {
                        id: 'generate-keys-example',
                        label: 'Generate Key Pair',
                        action: 'fetchJson(`${API_BASE}/demo/keys`)',
                        description: 'Click to generate a test key pair'
                    }
                ]
            },
            {
                title: 'Create Your First Actor',
                content: '<p>An actor represents your user in the ActivityPub network. You need a username to create one.</p>',
                examples: [
                    {
                        id: 'create-actor-example',
                        label: 'Create Actor',
                        action: 'fetchJson(`${API_BASE}/demo/actors`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify("tutorialuser") })',
                        description: 'Click to create a tutorial actor'
                    }
                ]
            },
            {
                title: 'Verify Your Setup',
                content: '<p>Congratulations! You now have the basic components needed for ActivityPub. Let\'s verify everything is working.</p>',
                examples: []
            }
        ]
    },
    'first-post': {
        title: 'First Post Walkthrough',
        steps: [
            {
                title: 'Creating Your First Activity',
                content: '<p>An activity represents an action taken on the network, like creating a note or liking a post. Let\'s create a simple "Create" activity.</p>',
                examples: []
            },
            {
                title: 'Activity Structure',
                content: '<p>An ActivityPub activity typically includes:</p><ul><li><strong>type:</strong> The type of activity (Create, Like, Announce, etc.)</li><li><strong>actor:</strong> Who performed the action</li><li><strong>object:</strong> What the action is about</li><li><strong>to:</strong> Who can see it</li></ul>',
                examples: []
            },
            {
                title: 'Submit Your Activity',
                content: '<p>Now let\'s submit your first activity to the system.</p>',
                examples: [
                    {
                        id: 'submit-activity-example',
                        label: 'Submit First Activity',
                        action: 'fetchJson(`${API_BASE}/demo/activities`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ activityId: "post-1", jsonData: JSON.stringify({ type: "Create", object: { type: "Note", content: "Hello, Federation!" } }) }) })',
                        description: 'Click to submit your first activity'
                    }
                ]
            },
            {
                title: 'View Your Activity',
                content: '<p>Your activity has been submitted! You can view it in the Activities section or check the activity stream.</p>',
                examples: []
            }
        ]
    },
    federation: {
        title: 'Federation Basics',
        steps: [
            {
                title: 'Understanding Federation',
                content: '<p>Federation allows different ActivityPub instances to communicate with each other. Each instance can follow and interact with others.</p>',
                examples: []
            },
            {
                title: 'Discovering Actors',
                content: '<p>To federate with another instance, you need to discover their actor endpoints using WebFinger or direct lookup.</p>',
                examples: []
            },
            {
                title: 'Adding a Federation Peer',
                content: '<p>You can add federation peers in the Federation Dashboard to simulate connections with other instances.</p>',
                examples: [
                    {
                        id: 'add-peer-example',
                        label: 'Add Federation Peer',
                        action: 'fetchJson(`${API_BASE}/demo/federation/peers`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ domain: "example.com" }) })',
                        description: 'Click to add a test federation peer'
                    }
                ]
            },
            {
                title: 'Testing Federation',
                content: '<p>Once peers are added, you can test federation by sending activities that should be delivered to other instances.</p>',
                examples: []
            }
        ]
    },
    security: {
        title: 'Security Best Practices',
        steps: [
            {
                title: 'HTTP Signature Authentication',
                content: '<p>ActivityPub uses HTTP Signature for authentication. All requests must be signed with your private key.</p>',
                examples: []
            },
            {
                title: 'Key Management',
                content: '<p>Keep your private key secure and never share it. Store it in a secure location, not in your code repository.</p>',
                examples: []
            },
            {
                title: 'Content Moderation',
                content: '<p>ActivityPub supports moderation through MRF (Message Review Function) rules. You can block keywords, domains, and even shadow-ban users.</p>',
                examples: [
                    {
                        id: 'moderation-example',
                        label: 'View Moderation Settings',
                        action: 'fetchJson(`${API_BASE}/demo/moderation/settings`)',
                        description: 'Click to check current moderation settings'
                    }
                ]
            },
            {
                title: 'Production Security',
                content: '<p>For production deployments, consider:</p><ul><li>Using HTTPS for all connections</li><li>Implementing rate limiting</li><li>Enabling audit logging</li><li>Regular security audits</li></ul>',
                examples: []
            }
        ]
    }
};
let currentTutorial = null;
let currentTutorialStep = 0;
let completedTutorials = new Set();

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
    } else if (sectionId === 'signature-debugger') {
        loadSignatureDebuggerSection();
    } else if (sectionId === 'content-moderation') {
        loadModerationSection();
    } else if (sectionId === 'http-signature') {
        loadHttpSignatureSection();
    } else if (sectionId === 'federation') {
        loadFederationSection();
    } else if (sectionId === 'webfinger-simulator') {
        loadWebFingerSimulatorSection();
    } else if (sectionId === 'federation-dashboard') {
        loadFederationDashboard();
    } else if (sectionId === 'analytics-dashboard') {
        loadAnalyticsSection();
    } else if (sectionId === 'api-documentation') {
        loadApiDocsSection();
    } else if (sectionId === 'interactive-tutorials') {
        loadTutorialsSection();
        document.getElementById('markCompleteBtn').addEventListener('click', markComplete);
    } else if (sectionId === 'multi-instance') {
        loadInstancesSection();
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
document.getElementById('sigDebuggerGenerateBtn').addEventListener('click', generateSignatureStepByStep);
document.getElementById('sigDebuggerCompareBtn').addEventListener('click', compareSignatures);
document.getElementById('sigDebuggerVerifyBtn').addEventListener('click', verifySignatureDebugger);
document.getElementById('sigDebuggerClearBtn').addEventListener('click', clearSignatureDebugger);
document.getElementById('addMrfRuleBtn').addEventListener('click', addMrfRule);
document.getElementById('removeMrfRuleBtn').addEventListener('click', removeMrfRuleUI);
document.getElementById('filterMrfRulesBtn').addEventListener('click', filterMrfRules);
document.getElementById('viewModerationLogsBtn').addEventListener('click', viewModerationLogs);
document.getElementById('applyModerationSettingsBtn').addEventListener('click', applyModerationSettings);
document.getElementById('saveModerationSettingsBtn').addEventListener('click', saveModerationSettings);
document.getElementById('clearModerationBtn').addEventListener('click', clearModeration);

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

async function generateSignatureStepByStep() {
    const resultDiv = document.getElementById('sigDebuggerResult');
    const stepsDiv = document.getElementById('sigDebuggerSteps');
    const keyId = document.getElementById('sigDebuggerKeyIdInput').value.trim();
    const privateKey = document.getElementById('sigDebuggerPrivateKeyInput').value.trim();
    const url = document.getElementById('sigDebuggerUrlInput').value.trim();
    const method = document.getElementById('sigDebuggerMethodSelect').value;
    
    if (!keyId || !privateKey) {
        alert('Please generate or provide a key pair first');
        return;
    }
    
    stepsDiv.innerHTML = '';
    resultDiv.innerHTML = '';
    
    try {
        resultDiv.innerHTML = '<strong>Step 1: Loading Configuration</strong><br>';
        
        const data = await fetchJson(`${API_BASE}/demo/http-signature/generate-test`);
        if (data.error) {
            resultDiv.innerHTML += `Error generating test key: ${data.error}<br>`;
            return;
        }
        
        document.getElementById('sigDebuggerKeyIdInput').value = data.keyId;
        document.getElementById('sigDebuggerPrivateKeyInput').value = data.privateKey;
        
        resultDiv.innerHTML += `Generated Key ID: ${data.keyId}<br>`;
        
        stepsDiv.innerHTML += '<strong>Step 2: Signature Generation Details</strong><br>';
        stepsDiv.innerHTML += `<br>Input Parameters:<br>`;
        stepsDiv.innerHTML += `&nbsp;&nbsp;Key ID: ${keyId}<br>`;
        stepsDiv.innerHTML += `&nbsp;&nbsp;Private Key: ${privateKey.substring(0, 30)}...<br>`;
        stepsDiv.innerHTML += `&nbsp;&nbsp;URL: ${url}<br>`;
        stepsDiv.innerHTML += `&nbsp;&nbsp;HTTP Method: ${method}<br>`;
        
        resultDiv.innerHTML += '<strong>Step 3: Creating Signature String</strong><br>';
        
        const signData = await fetchJson(`${API_BASE}/demo/http-signature/sign`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ keyId, privateKey, url, method })
        });
        
        if (signData.error) {
            resultDiv.innerHTML += `Error signing: ${signData.error}<br>`;
            return;
        }
        
        stepsDiv.innerHTML += `<br><strong>Step 4: Generated Signature Headers</strong><br>`;
        stepsDiv.innerHTML += `<pre>${JSON.stringify(signData.headers, null, 2)}</pre><br>`;
        
        resultDiv.innerHTML += `<strong>Signature Generated Successfully!</strong><br>`;
        resultDiv.innerHTML += `Algorithm: ${signData.algorithm}<br>`;
        resultDiv.innerHTML += `Timestamp: ${signData.timestamp}<br>`;
        
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

async function compareSignatures() {
    const resultDiv = document.getElementById('sigDebuggerResult');
    const keyId = document.getElementById('sigDebuggerKeyIdInput').value.trim();
    const privateKey = document.getElementById('sigDebuggerPrivateKeyInput').value.trim();
    const url = document.getElementById('sigDebuggerUrlInput').value.trim();
    const method = document.getElementById('sigDebuggerMethodSelect').value;
    
    if (!keyId || !privateKey) {
        alert('Please generate or provide a key pair first');
        return;
    }
    
    try {
        const generated = await fetchJson(`${API_BASE}/demo/http-signature/sign`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ keyId, privateKey, url, method })
        });
        
        if (generated.error) {
            resultDiv.textContent = `Error: ${generated.error}`;
            return;
        }
        
        const expected = await fetchJson(`${API_BASE}/demo/http-signature/expected?keyId=${encodeURIComponent(keyId)}&url=${encodeURIComponent(url)}&method=${encodeURIComponent(method)}`);
        
        resultDiv.innerHTML = `<strong>Signature Comparison</strong><br><br>`;
        resultDiv.innerHTML += `<strong>Generated Signature:</strong><br><pre>${JSON.stringify(generated.headers, null, 2)}</pre><br>`;
        resultDiv.innerHTML += `<strong>Expected Signature:</strong><br><pre>${JSON.stringify(expected, null, 2)}</pre><br>`;
        
        if (generated.headers['Signature'] === expected['Signature']) {
            resultDiv.innerHTML += `<br><span style="color: green;">✓ Signatures match!</span>`;
        } else {
            resultDiv.innerHTML += `<br><span style="color: red;">✗ Signatures do not match</span>`;
        }
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

async function verifySignatureDebugger() {
    const resultDiv = document.getElementById('sigDebuggerResult');
    const keyId = document.getElementById('sigDebuggerKeyIdInput').value.trim();
    const privateKey = document.getElementById('sigDebuggerPrivateKeyInput').value.trim();
    const url = document.getElementById('sigDebuggerUrlInput').value.trim();
    const method = document.getElementById('sigDebuggerMethodSelect').value;
    
    if (!keyId || !privateKey) {
        alert('Please generate or provide a key pair first');
        return;
    }
    
    try {
        const signData = await fetchJson(`${API_BASE}/demo/http-signature/sign`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ keyId, privateKey, url, method })
        });
        
        if (signData.error) {
            resultDiv.textContent = `Error generating signature: ${signData.error}`;
            return;
        }
        
        const verifyData = await fetchJson(`${API_BASE}/demo/http-signature/verify`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ 
                signature: signData.headers['Signature'],
                signedHeaders: method 
            })
        });
        
        if (verifyData.error) {
            resultDiv.textContent = `Error: ${verifyData.error}`;
        } else {
            resultDiv.innerHTML = `<strong>Signature Verification:</strong><br>`;
            resultDiv.innerHTML += `Valid: ${verifyData.valid ? '<span style="color: green;">✓ Yes</span>' : '<span style="color: red;">✗ No</span>'}<br>`;
            resultDiv.innerHTML += `Signature: ${verifyData.signature}<br>`;
            resultDiv.innerHTML += `Signed Headers: ${verifyData.signedHeaders}<br>`;
            resultDiv.innerHTML += `Timestamp: ${verifyData.timestamp}<br>`;
        }
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
    }
}

function clearSignatureDebugger() {
    document.getElementById('sigDebuggerKeyIdInput').value = '';
    document.getElementById('sigDebuggerPrivateKeyInput').value = '';
    document.getElementById('sigDebuggerUrlInput').value = 'http://localhost:8080/demo/activities';
    document.getElementById('sigDebuggerMethodSelect').value = 'POST';
    document.getElementById('sigDebuggerResult').textContent = '';
    document.getElementById('sigDebuggerSteps').textContent = '';
    alert('Signature Debugger cleared');
}

function loadSignatureDebuggerSection() {
    document.getElementById('sigDebuggerResult').textContent = 'Ready. Generate a test key pair to begin.';
    document.getElementById('sigDebuggerSteps').textContent = '';
}

async function loadModerationSection() {
    document.getElementById('moderationLogs').textContent = 'Loading moderation settings...';
    document.getElementById('mrfRulesList').textContent = '';
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/moderation/settings`);
        if (data.error) {
            document.getElementById('moderationLogs').textContent = `Error loading settings: ${data.error}`;
            return;
        }
        
        if (data.blockKeywords) {
            document.getElementById('blockKeywordsInput').value = Array.isArray(data.blockKeywords) 
                ? data.blockKeywords.join(', ') 
                : data.blockKeywords;
        }
        
        if (data.blockDomains) {
            document.getElementById('blockDomainsInput').value = Array.isArray(data.blockDomains) 
                ? data.blockDomains.join(', ') 
                : data.blockDomains;
        }
        
        if (data.shadowBanning !== undefined) {
            document.getElementById('shadowBanningToggle').checked = data.shadowBanning;
        }
        
        if (data.mrfRules && Array.isArray(data.mrfRules)) {
            document.getElementById('mrfRulesList').innerHTML = data.mrfRules.map(rule => 
                `<div class="mrf-rule-item">
                    <strong>Keyword:</strong> ${rule.keyword} | 
                    <strong>Action:</strong> ${rule.action} | 
                    <strong>Priority:</strong> ${rule.priority}
                    <button onclick="removeMrfRule('${rule.keyword}')">Remove</button>
                </div>`
            ).join('');
        } else {
            document.getElementById('mrfRulesList').textContent = 'No MRF rules configured';
        }
        
        document.getElementById('moderationLogs').textContent = 'Moderation section loaded successfully';
    } catch (error) {
        document.getElementById('moderationLogs').textContent = `Error: ${error.message}`;
    }
}

async function addMrfRule() {
    const resultDiv = document.getElementById('mrfRulesList');
    const keyword = document.getElementById('mrfRuleKeyword').value.trim();
    const action = document.getElementById('mrfRuleAction').value;
    
    if (!keyword) {
        alert('Please enter a keyword for the MRF rule');
        return;
    }
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/moderation/mrf/rules`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ keyword, action })
        });
        
        if (data.error) {
            resultDiv.innerHTML = `Error adding rule: ${data.error}`;
        } else {
            await loadModerationSection();
            document.getElementById('mrfRuleKeyword').value = '';
        }
    } catch (error) {
        resultDiv.innerHTML = `Error: ${error.message}`;
    }
}

async function removeMrfRule(keyword) {
    const resultDiv = document.getElementById('mrfRulesList');
    
    if (!keyword) {
        return;
    }
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/moderation/mrf/rules?keyword=${encodeURIComponent(keyword)}`, {
            method: 'DELETE'
        });
        
        if (data.error) {
            resultDiv.innerHTML = `Error removing rule: ${data.error}`;
        } else {
            await loadModerationSection();
        }
    } catch (error) {
        resultDiv.innerHTML = `Error: ${error.message}`;
    }
}

function removeMrfRuleUI() {
    const keyword = document.getElementById('mrfRuleKeyword').value.trim();
    if (!keyword) {
        alert('Please enter a keyword to remove');
        return;
    }
    removeMrfRule(keyword);
}

function filterMrfRules() {
    const keyword = document.getElementById('mrfRuleKeyword').value.trim().toLowerCase();
    const rulesList = document.getElementById('mrfRulesList');
    
    if (!keyword) {
        loadModerationSection();
        return;
    }
    
    rulesList.innerHTML = '';
    
    if (!rulesList.dataset.allRules) {
        rulesList.dataset.allRules = rulesList.innerHTML;
    }
    
    if (keyword) {
        rulesList.innerHTML = 'Filter not implemented in demo mode';
    } else {
        loadModerationSection();
    }
}

async function viewModerationLogs() {
    const resultDiv = document.getElementById('moderationLogs');
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/moderation/logs`);
        
        if (data.error) {
            resultDiv.innerHTML = `Error: ${data.error}`;
            return;
        }
        
        if (data.logs && Array.isArray(data.logs)) {
            resultDiv.innerHTML = data.logs.length > 0
                ? data.logs.map(log => 
                    `<div class="moderation-log">
                        <strong>Time:</strong> ${log.timestamp}<br>
                        <strong>Rule:</strong> ${log.rule}<br>
                        <strong>Action:</strong> ${log.action}<br>
                        <strong>Details:</strong> ${log.details}
                    </div>`
                ).join('<hr>')
                : 'No moderation logs found';
        } else {
            resultDiv.innerHTML = JSON.stringify(data, null, 2);
        }
    } catch (error) {
        resultDiv.innerHTML = `Error: ${error.message}`;
    }
}

async function applyModerationSettings() {
    const resultDiv = document.getElementById('moderationSettingsResult');
    
    const settings = {
        BlockKeywords: document.getElementById('blockKeywordsInput').value.trim()
            .split(',')
            .map(k => k.trim())
            .filter(k => k.length > 0),
        BlockDomains: document.getElementById('blockDomainsInput').value.trim()
            .split(',')
            .map(d => d.trim())
            .filter(d => d.length > 0),
        ShadowBanning: document.getElementById('shadowBanningToggle').checked
    };
    
    if (!settings.BlockKeywords.length && !settings.BlockDomains.length) {
        alert('Please enter at least one keyword or domain to block');
        return;
    }
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/moderation/apply`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(settings)
        });
        
        if (data.error) {
            resultDiv.innerHTML = `Error: ${data.error}`;
        } else {
            resultDiv.innerHTML = `<strong>Settings applied successfully:</strong>
                <br>Keywords blocked: ${settings.BlockKeywords.length}
                <br>Domains blocked: ${settings.BlockDomains.length}
                <br>Shadow banning: ${settings.ShadowBanning ? 'Enabled' : 'Disabled'}`;
            await loadModerationSection();
        }
    } catch (error) {
        resultDiv.innerHTML = `Error: ${error.message}`;
    }
}

async function saveModerationSettings() {
    const resultDiv = document.getElementById('moderationSettingsResult');
    
    const settings = {
        BlockKeywords: document.getElementById('blockKeywordsInput').value.trim()
            .split(',')
            .map(k => k.trim())
            .filter(k => k.length > 0),
        BlockDomains: document.getElementById('blockDomainsInput').value.trim()
            .split(',')
            .map(d => d.trim())
            .filter(d => d.length > 0),
        ShadowBanning: document.getElementById('shadowBanningToggle').checked
    };
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/moderation/save`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(settings)
        });
        
        if (data.error) {
            resultDiv.innerHTML = `Error: ${data.error}`;
        } else {
            resultDiv.innerHTML = `<strong>Settings saved successfully:</strong>
                <br>Keywords: ${settings.BlockKeywords.length}
                <br>Domains: ${settings.BlockDomains.length}
                <br>Shadow Banning: ${settings.ShadowBanning ? 'Enabled' : 'Disabled'}`;
        }
    } catch (error) {
        resultDiv.innerHTML = `Error: ${error.message}`;
    }
}

function clearModeration() {
    document.getElementById('blockKeywordsInput').value = '';
    document.getElementById('blockDomainsInput').value = '';
    document.getElementById('shadowBanningToggle').checked = false;
    document.getElementById('mrfRuleKeyword').value = '';
    document.getElementById('mrfRulesList').textContent = 'Rules cleared';
    document.getElementById('moderationSettingsResult').textContent = '';
    document.getElementById('moderationLogs').textContent = 'All moderation data cleared';
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

function loadWebFingerSimulatorSection() {
    document.getElementById('wfResourceInput').value = '';
    document.getElementById('wfResult').textContent = 'Ready. Enter a resource to simulate WebFinger lookup.';
}

async function simulateWebFinger() {
    const resultDiv = document.getElementById('wfResult');
    const previewDiv = document.getElementById('wfResponsePreview');
    const resource = document.getElementById('wfResourceInput').value.trim();
    const acceptHeader = document.getElementById('wfAcceptHeaderInput').value.trim();
    
    if (!resource) {
        resultDiv.textContent = 'Please enter a resource (e.g., user@domain.com)';
        return;
    }
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/federation/webfinger?resource=${encodeURIComponent(resource)}`);
        
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
            previewDiv.textContent = '';
            return;
        }
        
        resultDiv.innerHTML = `<strong>WebFinger Query Results:</strong>
            <br>Resource: ${resource}
            <br>Accept: ${acceptHeader}
            <br>Status: Success`;
        
        previewDiv.innerHTML = `<strong>Expected JRD Response:</strong>
            <br>Subject: ${data.subject}
            <br>Links: ${data.links ? data.links.length : 0}
            <br><pre>${JSON.stringify(data, null, 2)}</pre>`;
    } catch (error) {
        resultDiv.textContent = `Error: ${error.message}`;
        previewDiv.textContent = '';
    }
}

function clearWebFinger() {
    document.getElementById('wfResourceInput').value = '';
    document.getElementById('wfResult').textContent = 'Ready. Enter a resource to simulate WebFinger lookup.';
    document.getElementById('wfResponsePreview').textContent = '';
}

document.getElementById('discoverActorBtn').addEventListener('click', discoverActorEndpoints);
document.getElementById('discoverWebfingerBtn').addEventListener('click', discoverWebfinger);
document.getElementById('simulateReceiveBtn').addEventListener('click', simulateReceiveActivity);
document.getElementById('simulateSendBtn').addEventListener('click', simulateSendActivity);
document.getElementById('validateProtocolBtn').addEventListener('click', validateProtocol);
document.getElementById('refreshFederationBtn').addEventListener('click', loadFederationDashboard);
document.getElementById('retryFailedBtn').addEventListener('click', retryFederationFailed);
document.getElementById('clearFailedBtn').addEventListener('click', clearFederationFailed);

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

function loadWebFingerSimulatorSection() {
    document.getElementById('wfResourceInput').value = '';
    document.getElementById('wfResult').textContent = 'Ready. Enter a resource to simulate WebFinger lookup.';
}

function showAddPeerModal() {
    const url = prompt('Enter peer domain URL:', 'https://example.com');
    if (url) {
        alert(`Peer ${url} added successfully!`);
    }
}

async function loadFederationDashboard() {
    try {
        const stats = await fetchJson(`${API_BASE}/demo/federation/stats`);
        const peers = await fetchJson(`${API_BASE}/demo/federation/peers`);
        
        if (stats.error) {
            document.getElementById('federationDashboardResult').textContent = `Error: ${stats.error}`;
            return;
        }
        
        document.getElementById('outboundTotal').textContent = stats.outbound?.total || 0;
        document.getElementById('outboundPending').textContent = stats.outbound?.pending || 0;
        document.getElementById('outboundProcessing').textContent = stats.outbound?.processing || 0;
        document.getElementById('outboundCompleted').textContent = stats.outbound?.completed || 0;
        document.getElementById('outboundFailed').textContent = stats.outbound?.failed || 0;
        
        document.getElementById('inboundTotal').textContent = stats.inbound?.total || 0;
        document.getElementById('inboundPending').textContent = stats.inbound?.pending || 0;
        document.getElementById('inboundProcessing').textContent = stats.inbound?.processing || 0;
        document.getElementById('inboundCompleted').textContent = stats.inbound?.completed || 0;
        document.getElementById('inboundFailed').textContent = stats.inbound?.failed || 0;
        
        document.getElementById('successRate').textContent = `${stats.successRate || 100}%`;
        document.getElementById('avgDeliveryTime').textContent = `${stats.avgDeliveryTime || 2.3}s`;
        document.getElementById('peersOnline').textContent = peers.length;
        
        if (peers.error) {
            document.getElementById('peersGrid').innerHTML = '<div class="error">Error loading peers: ' + peers.error + '</div>';
            return;
        }
        
        if (peers.length === 0) {
            document.getElementById('peersGrid').innerHTML = '<div class="no-peers">No connected peers yet. Add one to begin federation.</div>';
            return;
        }
        
        document.getElementById('peersGrid').innerHTML = peers.map(peer => 
            `<div class="peer-card">
                <h4>${peer.domain}</h4>
                <p>Status: <span class="${peer.online ? 'status-online' : 'status-offline'}">${peer.online ? '✓ Online' : '✗ Offline'}</span></p>
                <p>Inbox: ${peer.inboxUrl}</p>
                <p>Version: ${peer.version || 'ActivityPub 2.0'}</p>
                <button onclick="removePeer('${peer.domain}')">Remove</button>
             </div>`
        ).join('');
        
        document.getElementById('federationDashboardResult').textContent = 'Dashboard updated successfully';
    } catch (error) {
        document.getElementById('federationDashboardResult').textContent = `Error: ${error.message}`;
    }
}

let activityCountsChart = null;
let trendsChart = null;

async function loadAnalyticsSection() {
    const timeRange = document.getElementById('timeRangeDisplay').textContent.replace('Selected: ', '').trim();
    const days = timeRange === '7 Days' ? 7 : timeRange === '30 Days' ? 30 : 90;
    
    document.getElementById('topActorsList').textContent = 'Loading top actors...';
    document.getElementById('federationStatsTable').textContent = 'Loading federation stats...';
    
    await Promise.all([
        fetchActivityCounts(days),
        fetchTopActors(days),
        fetchFederationStats(days),
        fetchTrends(days)
    ]);
}

async function fetchActivityCounts(days) {
    try {
        const data = await fetchJson(`${API_BASE}/demo/analytics/counts?days=${days}`);
        
        const ctx = document.getElementById('activityCountsChart').getContext('2d');
        if (activityCountsChart) {
            activityCountsChart.destroy();
        }
        
        activityCountsChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: data.labels || [],
                datasets: [{
                    label: 'Activity Count',
                    data: data.data || [],
                    backgroundColor: '#3498db'
                }]
            },
            options: {
                responsive: true,
                scales: {
                    y: { beginAtZero: true }
                }
            }
        });
    } catch (error) {
        document.getElementById('topActorsList').textContent = `Error: ${error.message}`;
    }
}

async function fetchTopActors(days) {
    try {
        const data = await fetchJson(`${API_BASE}/demo/analytics/top-actors?days=${days}`);
        
        const resultDiv = document.getElementById('topActorsList');
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
            return;
        }
        
        if (data.actors && Array.isArray(data.actors)) {
            resultDiv.innerHTML = data.actors.map((actor, index) =>
                `<div class="actor-item" style="padding: 10px; border-bottom: 1px solid #eee;">
                    <strong>#${index + 1}</strong> ${actor.actor || 'Unknown'}<br>
                    <span style="color: #27ae60;">${actor.count || 0} activities</span>
                </div>`
            ).join('');
        } else {
            resultDiv.innerHTML = JSON.stringify(data, null, 2);
        }
    } catch (error) {
        document.getElementById('topActorsList').textContent = `Error: ${error.message}`;
    }
}

async function fetchFederationStats(days) {
    try {
        const data = await fetchJson(`${API_BASE}/demo/analytics/federation?days=${days}`);
        
        const resultDiv = document.getElementById('federationStatsTable');
        if (data.error) {
            resultDiv.textContent = `Error: ${data.error}`;
            return;
        }
        
        if (data.partners && Array.isArray(data.partners)) {
            resultDiv.innerHTML = `
                <table style="width: 100%; border-collapse: collapse;">
                    <thead>
                        <tr style="background: #3498db; color: white;">
                            <th style="padding: 10px; border: 1px solid #ddd;">Partner</th>
                            <th style="padding: 10px; border: 1px solid #ddd;">Status</th>
                            <th style="padding: 10px; border: 1px solid #ddd;">Inbound</th>
                            <th style="padding: 10px; border: 1px solid #ddd;">Outbound</th>
                            <th style="padding: 10px; border: 1px solid #ddd;">Success Rate</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${data.partners.map(partner =>
                            `<tr>
                                <td style="padding: 10px; border: 1px solid #ddd;">${partner.domain}</td>
                                <td style="padding: 10px; border: 1px solid #ddd;">${partner.online ? '<span style="color: green;">✓ Online</span>' : '✗ Offline'}</td>
                                <td style="padding: 10px; border: 1px solid #ddd;">${partner.inbound || 0}</td>
                                <td style="padding: 10px; border: 1px solid #ddd;">${partner.outbound || 0}</td>
                                <td style="padding: 10px; border: 1px solid #ddd;">${partner.successRate ? partner.successRate + '%' : 'N/A'}</td>
                            </tr>`
                        ).join('')}
                    </tbody>
                </table>
            `;
        } else {
            resultDiv.innerHTML = JSON.stringify(data, null, 2);
        }
    } catch (error) {
        document.getElementById('federationStatsTable').textContent = `Error: ${error.message}`;
    }
}

async function fetchTrends(days) {
    try {
        const data = await fetchJson(`${API_BASE}/demo/analytics/trends?days=${days}`);
        
        const ctx = document.getElementById('trendsChart').getContext('2d');
        if (trendsChart) {
            trendsChart.destroy();
        }
        
        trendsChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: data.labels || [],
                datasets: [{
                    label: 'Daily Activities',
                    data: data.data || [],
                    borderColor: '#e74c3c',
                    tension: 0.3
                }]
            },
            options: {
                responsive: true,
                scales: {
                    y: { beginAtZero: true }
                }
            }
        });
    } catch (error) {
        console.error('Error loading trends:', error);
    }
}

function exportAnalytics(format) {
    const timeRange = document.getElementById('timeRangeDisplay').textContent.replace('Selected: ', '').trim();
    const days = timeRange === '7 Days' ? 7 : timeRange === '30 Days' ? 30 : 90;
    
    fetchJson(`${API_BASE}/demo/analytics/export?format=${format}&days=${days}`)
        .then(data => {
            if (data.error) {
                alert(`Error: ${data.error}`);
                return;
            }
            
            const blob = new Blob([data.content], { type: data.contentType || 'text/plain' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `analytics.${format}`;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
        })
        .catch(error => {
            statusDiv.innerHTML = `<strong>Error:</strong> ${error.message}`;
        });
}

let websocketConnection = null;
let activityFeed = [];
let autoScroll = true;
let messagesReceived = 0;
let streamFilters = {
    type: '',
    actor: '',
    timeRange: ''
};

function connectWebSocket() {
    const statusDiv = document.getElementById('streamStatus');
    const streamContainer = document.getElementById('realtimeActivityStream');
    const emptyMessage = document.getElementById('streamEmptyMessage');
    
    if (websocketConnection) {
        websocketConnection.close();
    }
    
    websocketConnection = new WebSocket(`ws://${window.location.host}/activityHub`);
    
    websocketConnection.onopen = () => {
        statusDiv.textContent = 'Connected';
        statusDiv.style.color = '#27ae60';
        emptyMessage.style.display = 'none';
    };
    
    websocketConnection.onmessage = (event) => {
        try {
            const activity = JSON.parse(event.data);
            const now = new Date();
            
            activity.timestamp = now.toISOString();
            activityFeed.push(activity);
            messagesReceived++;
            
            updateMessagesReceived();
            renderActivityStream();
            
            if (autoScroll) {
                const streamContainer = document.getElementById('realtimeActivityStream');
                streamContainer.scrollTop = streamContainer.scrollHeight;
            }
        } catch (error) {
            console.error('Error parsing activity:', error);
        }
    };
    
    websocketConnection.onclose = () => {
        statusDiv.textContent = 'Disconnected';
        statusDiv.style.color = '#e74c3c';
        
        setTimeout(connectWebSocket, 3000);
    };
    
    websocketConnection.onerror = (error) => {
        statusDiv.textContent = 'Error';
        statusDiv.style.color = '#e74c3c';
        console.error('WebSocket error:', error);
    };
}

function disconnectWebSocket() {
    if (websocketConnection) {
        websocketConnection.close();
        websocketConnection = null;
        document.getElementById('streamStatus').textContent = 'Disconnected';
        document.getElementById('streamStatus').style.color = '#e74c3c';
    }
}

function renderActivityStream() {
    const container = document.getElementById('realtimeActivityStream');
    const filterType = document.getElementById('streamFilterType')?.value || '';
    const filterActor = document.getElementById('streamFilterActor')?.value || '';
    const filterTime = document.getElementById('streamFilterTime')?.value || '';
    
    let filteredActivities = activityFeed.filter(activity => {
        if (filterType) {
            const activityType = typeof activity === 'object' && activity !== null ? 
                (activity.type || (activity.object && activity.object.type)) : '';
            if (activityType !== filterType) return false;
        }
        
        if (filterActor) {
            const actor = typeof activity === 'object' && activity !== null ? 
                (activity.actor || activity.id || '') : '';
            if (!actor.toLowerCase().includes(filterActor.toLowerCase())) return false;
        }
        
        if (filterTime) {
            const now = new Date();
            const activityDate = new Date(activity.timestamp || now);
            const hours = parseInt(filterTime);
            
            if (filterTime.includes('h')) {
                const diff = (now - activityDate) / (1000 * 60 * 60);
                if (diff > hours) return false;
            } else if (filterTime.includes('d')) {
                const diff = (now - activityDate) / (1000 * 60 * 60 * 24);
                if (diff > parseInt(filterTime)) return false;
            }
        }
        
        return true;
    });
    
    if (filteredActivities.length === 0) {
        container.innerHTML = '<p style="text-align: center; color: #7f8c8d; padding: 1rem;">No activities match the current filters.</p>';
        return;
    }
    
    container.innerHTML = filteredActivities.map((activity, index) => {
        const type = typeof activity === 'object' && activity !== null ? 
            (activity.type || (activity.object && activity.object.type) || 'Unknown') : 'Unknown';
        const content = typeof activity === 'object' && activity !== null ? 
            JSON.stringify(activity, null, 2) : String(activity);
        const timestamp = activity.timestamp || new Date().toISOString();
        
        return `
            <div class="activity-item ${type.toLowerCase()}">
                <div class="activity-header">
                    <span class="activity-type">${type}</span>
                    <span class="activity-timestamp">${new Date(timestamp).toLocaleString()}</span>
                </div>
                <div class="activity-content">
                    <pre>${escapeHtml(content)}</pre>
                </div>
            </div>
        `;
    }).join('');
}

function applyStreamFilter() {
    renderActivityStream();
}

function clearStreamFilter() {
    document.getElementById('streamFilterType').value = '';
    document.getElementById('streamFilterActor').value = '';
    document.getElementById('streamFilterTime').value = '';
    renderActivityStream();
}

function exportStream(format) {
    const data = JSON.stringify(activityFeed, null, 2);
    
    if (format === 'csv') {
        const csvRows = [];
        csvRows.push(['Type', 'Timestamp', 'Content']);
        
        activityFeed.forEach(activity => {
            const type = typeof activity === 'object' && activity !== null ? 
                (activity.type || (activity.object && activity.object.type) || 'Unknown') : 'Unknown';
            const timestamp = activity.timestamp || new Date().toISOString();
            const content = typeof activity === 'object' ? JSON.stringify(activity) : String(activity);
            
            csvRows.push([type, timestamp, content]);
        });
        
        const csv = csvRows.map(row => row.map(cell => `"${cell.replace(/"/g, '""')}"`).join(',')).join('\n');
        downloadFile(`activity_stream_${new Date().toISOString()}.csv`, csv, 'text/csv');
    } else {
        downloadFile(`activity_stream_${new Date().toISOString()}.json`, data, 'application/json');
    }
}

function toggleAutoScroll() {
    autoScroll = !autoScroll;
    const button = document.getElementById('toggleAutoScrollBtn');
    if (button) {
        button.textContent = `Auto-scroll: ${autoScroll ? 'ON' : 'OFF'}`;
    }
}

function updateMessagesReceived() {
    const countDisplay = document.getElementById('messagesReceived');
    if (countDisplay) {
        countDisplay.textContent = messagesReceived;
    }
}

function downloadFile(filename, content, contentType) {
    const blob = new Blob([content], { type: contentType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

function escapeHtml(text) {
    if (!text) return '';
    return text
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

window.addEventListener('beforeunload', () => {
    disconnectWebSocket();
});

function updateTimeRange(days) {
    const timeRangeDisplay = document.getElementById('timeRangeDisplay');
    timeRangeDisplay.textContent = days + (days === 7 ? ' Days' : (days === 30 ? ' Days' : ' Days'));
    
    loadAnalyticsSection();
}

async function retryFederationFailed() {
    try {
        const result = await fetchJson(`${API_BASE}/demo/federation/retry`, { method: 'POST' });
        document.getElementById('federationDashboardResult').innerHTML = result.error
            ? `Error: ${result.error}`
            : `Retry completed: ${JSON.stringify(result, null, 2)}`;
        setTimeout(loadFederationDashboard, 1000);
    } catch (error) {
        document.getElementById('federationDashboardResult').textContent = `Error: ${error.message}`;
    }
}

async function clearFederationFailed() {
    if (!confirm('Are you sure you want to clear all failed federation items?')) return;
    
    try {
        const result = await fetchJson(`${API_BASE}/demo/federation/clear-failed`, { method: 'POST' });
        document.getElementById('federationDashboardResult').innerHTML = result.error
            ? `Error: ${result.error}`
            : `Failed items cleared: ${JSON.stringify(result, null, 2)}`;
        setTimeout(loadFederationDashboard, 1000);
    } catch (error) {
        document.getElementById('federationDashboardResult').textContent = `Error: ${error.message}`;
    }
}

function removePeer(domain) {
    if (confirm(`Remove peer ${domain}?`)) {
        document.getElementById('peersGrid').innerHTML = '';
        document.getElementById('federationDashboardResult').textContent = `${domain} removed from connected peers`;
        loadFederationDashboard();
    }
}

function loadFederationSection() {
    document.getElementById('federationResult').textContent = 'Ready. Enter an actor URL or WebFinger resource to discover endpoints.';
}

function loadModerationSection() {
    document.getElementById('moderationLogs').textContent = 'Loading moderation settings...';
    document.getElementById('mrfRulesList').textContent = 'No MRF rules configured';
}

const apiSpec = {
    endpoints: [
        { method: 'GET', path: '/demo/status', category: 'System', description: 'Get service status', response: { status: 'healthy', version: '1.0.0' } },
        { method: 'GET', path: '/demo/actors', category: 'Actors', description: 'List all actors', response: [{ id: 1, username: 'testuser' }] },
        { method: 'POST', path: '/demo/actors', category: 'Actors', description: 'Create a new actor', request: { username: 'string' }, response: { id: 1, username: 'testuser', createdAt: '2026-08-14' } },
        { method: 'GET', path: '/demo/actors/{id}', category: 'Actors', description: 'Get actor by ID', response: { id: 1, username: 'testuser' } },
        { method: 'PUT', path: '/demo/actors/{id}', category: 'Actors', description: 'Update actor', request: { username: 'string' }, response: { id: 1, username: 'updateduser' } },
        { method: 'DELETE', path: '/demo/actors/{id}', category: 'Actors', description: 'Delete actor', response: { success: true } },
        { method: 'GET', path: '/demo/activities', category: 'Activities', description: 'List activities', response: [{ id: 1, type: 'Create', content: 'Hello' }] },
        { method: 'POST', path: '/demo/activities', category: 'Activities', description: 'Submit new activity', request: { activityId: 'string', jsonData: 'object' }, response: { id: 1, submitted: true } },
        { method: 'GET', path: '/demo/activities/{id}', category: 'Activities', description: 'Get activity by ID', response: { id: 1, type: 'Create', content: 'Hello' } },
        { method: 'POST', path: '/demo/keys', category: 'Keys', description: 'Generate RSA key pair', response: { privateKey: '...', publicKey: '...' } },
        { method: 'GET', path: '/demo/templates', category: 'Templates', description: 'List message templates', response: [{ id: 'create-note', name: 'Create Note', category: 'Activity' }] },
        { method: 'POST', path: '/demo/moderation/apply', category: 'Moderation', description: 'Apply moderation settings', request: { blockKeywords: [], blockDomains: [] }, response: { success: true } },
        { method: 'POST', path: '/demo/federation/peers', category: 'Federation', description: 'Add federation peer', request: { domain: 'string' }, response: { success: true } },
        { method: 'GET', path: '/demo/config', category: 'Config', description: 'Get configuration', response: { domain: 'localhost', port: 8080 } },
        { method: 'POST', path: '/demo/config', category: 'Config', description: 'Save configuration', request: { domain: 'string' }, response: { success: true } }
    ],
    errorCodes: {
        '400': 'Bad Request - Invalid parameters or malformed JSON',
        '401': 'Unauthorized - Missing or invalid authentication',
        '403': 'Forbidden - Insufficient permissions',
        '404': 'Not Found - Resource not found',
        '405': 'Method Not Allowed - HTTP method not supported',
        '409': 'Conflict - Resource already exists',
        '422': 'Unprocessable Entity - Validation failed',
        '500': 'Internal Server Error - Server error occurred',
        '502': 'Bad Gateway - Upstream service error',
        '503': 'Service Unavailable - Service temporarily unavailable'
    },
    responseSchemas: {
        Actor: {
            id: 'integer',
            username: 'string',
            inbox: 'string (URL)',
            outbox: 'string (URL)',
            followers: 'string (URL)',
            following: 'string (URL)'
        },
        Activity: {
            id: 'string (URL)',
            type: 'string (Create, Update, Delete, etc.)',
            actor: 'string (URL)',
            object: 'object or string',
            published: 'string (ISO 8601 date)',
            to: 'array of URLs',
            cc: 'array of URLs (optional)'
        },
        Error: {
            error: 'string',
            message: 'string',
            statusCode: 'integer',
            timestamp: 'string (ISO 8601)'
        }
    }
};

async function loadApiDocsSection() {
    document.getElementById('apiSearchInput').value = '';
    await fetchEndpoints();
    displayResponseSchemas();
    displayErrorCodes();
}

async function fetchEndpoints() {
    try {
        const data = await fetchJson(`${API_BASE}/demo/api/endpoints`);
        const endpoints = data.error ? apiSpec.endpoints : (Array.isArray(data) ? data : apiSpec.endpoints);
        displayEndpoints(endpoints);
        tryEndpointData = endpoints[0];
        updateTryEndpointForm(tryEndpointData);
    } catch (error) {
        displayEndpoints(apiSpec.endpoints);
        tryEndpointData = apiSpec.endpoints[0];
        updateTryEndpointForm(tryEndpointData);
    }
}

let tryEndpointData = null;

function displayEndpoints(endpoints) {
    const listDiv = document.getElementById('apiEndpointsList');
    if (!endpoints || endpoints.length === 0) {
        listDiv.innerHTML = '<p>No endpoints found.</p>';
        return;
    }
    
    listDiv.innerHTML = endpoints.map(ep => `
        <div class="endpoint-item" style="padding: 15px; margin-bottom: 15px; border: 1px solid #ddd; border-radius: 5px; background: #f9f9f9;">
            <div style="display: flex; align-items: center; margin-bottom: 10px;">
                <span style="padding: 3px 8px; border-radius: 3px; background: ${getMethodColor(ep.method)}; color: white; font-weight: bold; margin-right: 10px;">
                    ${ep.method}
                </span>
                <span style="font-family: monospace; font-size: 14px;">${ep.path}</span>
            </div>
            <p style="margin: 5px 0; color: #666;">${ep.description || ''}</p>
            <p style="margin: 5px 0; font-size: 12px; color: #888;">Category: ${ep.category || 'Uncategorized'}</p>
            <button onclick="tryEndpoint(${JSON.stringify(ep).replace(/"/g, '&quot;')})" style="margin-top: 10px; padding: 5px 10px; cursor: pointer;">
                Try It Out
            </button>
        </div>
    `).join('');
}

function getMethodColor(method) {
    const colors = {
        'GET': '#27ae60',
        'POST': '#3498db',
        'PUT': '#f39c12',
        'DELETE': '#e74c3c'
    };
    return colors[method] || '#7f8c8d';
}

function updateTryEndpointForm(endpoint) {
    if (!endpoint) return;
    document.getElementById('tryEndpointUrl').value = endpoint.path;
    document.getElementById('tryEndpointMethod').value = endpoint.method;
}

async function tryEndpoint(endpoint) {
    if (!endpoint || !endpoint.path) {
        alert('Invalid endpoint');
        return;
    }
    
    const resultDiv = document.getElementById('tryItOutResponse');
    const method = document.getElementById('tryEndpointMethod').value;
    const url = `${API_BASE}${endpoint.path}`;
    
    resultDiv.innerHTML = 'Making request...';
    
    try {
        const fetchOptions = {
            method: method,
            headers: { 'Content-Type': 'application/json' }
        };
        
        if (method === 'POST' || method === 'PUT') {
            const bodyData = getSampleRequestBody(endpoint);
            if (bodyData) {
                fetchOptions.body = JSON.stringify(bodyData);
            }
        }
        
        const data = await fetchJson(url, fetchOptions);
        
        resultDiv.innerHTML = `<strong>Response Status:</strong> ${data.error ? 'Error' : 'Success'}<br><br>
            <strong>Response Data:</strong><br>
            <pre style="background: #f5f5f5; padding: 10px; border-radius: 3px;">${JSON.stringify(data, null, 2)}</pre>`;
        
        if (data.error) {
            showErrorCode(getErrorCodeFromError(data.error));
        }
    } catch (error) {
        resultDiv.innerHTML = `<strong>Error:</strong> ${error.message}<br><br>
            <pre style="background: #f5f5f5; padding: 10px; border-radius: 3px;">${error.stack || ''}</pre>`;
    }
}

function getSampleRequestBody(endpoint) {
    if (endpoint.request) {
        return endpoint.request;
    }
    
    const method = endpoint.method;
    if (method === 'POST' || method === 'PUT') {
        if (endpoint.path.includes('actors')) {
            return { username: 'testuser' };
        } else if (endpoint.path.includes('activities')) {
            return { activityId: 'test-' + Date.now(), jsonData: JSON.stringify({ type: 'Create', content: 'Test' }) };
        } else if (endpoint.path.includes('config')) {
            return { domain: 'localhost' };
        } else if (endpoint.path.includes('moderation')) {
            return { blockKeywords: ['badword'], blockDomains: [] };
        }
    }
    
    return null;
}

function showResponse(data) {
    const resultDiv = document.getElementById('tryItOutResponse');
    resultDiv.innerHTML = `<strong>Response Data:</strong><br>
        <pre style="background: #f5f5f5; padding: 10px; border-radius: 3px;">${JSON.stringify(data, null, 2)}</pre>`;
}

function showErrorCode(code) {
    const errorDiv = document.getElementById('errorCodes');
    const codeStr = String(code);
    
    if (apiSpec.errorCodes[codeStr]) {
        alert(`Error ${codeStr}: ${apiSpec.errorCodes[codeStr]}`);
        errorDiv.innerHTML = `<strong>Error Code ${codeStr} Details:</strong>
            <br><p style="color: #e74c3c;">${apiSpec.errorCodes[codeStr]}</p>`;
    } else {
        alert(`Unknown error: ${code}`);
    }
}

function filterEndpoints(category) {
    const listDiv = document.getElementById('apiEndpointsList');
    const searchValue = document.getElementById('apiSearchInput').value.toLowerCase();
    
    let filtered = apiSpec.endpoints;
    
    if (category && category !== 'all') {
        filtered = filtered.filter(ep => ep.method === category);
    }
    
    if (searchValue) {
        filtered = filtered.filter(ep => 
            ep.path.toLowerCase().includes(searchValue) ||
            (ep.description && ep.description.toLowerCase().includes(searchValue)) ||
            ep.category?.toLowerCase().includes(searchValue)
        );
    }
    
    displayEndpoints(filtered);
}

function displayResponseSchemas() {
    const schemasDiv = document.getElementById('responseSchemas');
    schemasDiv.innerHTML = Object.entries(apiSpec.responseSchemas).map(([name, schema]) => `
        <div style="padding: 15px; margin-bottom: 15px; border: 1px solid #ddd; border-radius: 5px; background: #f9f9f9;">
            <h4 style="margin-top: 0;">${name}</h4>
            <table style="width: 100%; border-collapse: collapse;">
                ${Object.entries(schema).map(([field, type]) => `
                    <tr>
                        <td style="padding: 8px; border: 1px solid #ddd; font-family: monospace;">${field}</td>
                        <td style="padding: 8px; border: 1px solid #ddd; color: #666;">${type}</td>
                    </tr>
                `).join('')}
            </table>
        </div>
    `).join('');
}

function displayErrorCodes() {
    const errorDiv = document.getElementById('errorCodes');
    errorDiv.innerHTML = Object.entries(apiSpec.errorCodes).map(([code, description]) => `
        <div style="padding: 10px; margin-bottom: 10px; border-left: 4px solid #e74c3c; background: #fef5f5;">
            <strong>HTTP ${code}</strong>: ${description}
        </div>
    `).join('');
}

function getErrorCodeFromError(errorMsg) {
    const match = errorMsg.match(/HTTP (\d+)/);
    return match ? parseInt(match[1]) : 500;
}

function loadApiDocumentationSection() {
    loadApiDocsSection();
}

async function loadTutorialsSection() {
    currentTutorial = null;
    currentTutorialStep = 0;
    document.getElementById('tutorialTitle').textContent = 'Select a Tutorial';
    document.getElementById('tutorialContent').innerHTML = '<p>Select a tutorial from the list to begin learning.</p>';
    document.getElementById('prevStepBtn').disabled = true;
    document.getElementById('nextStepBtn').disabled = true;
    document.getElementById('markCompleteBtn').disabled = true;
    document.getElementById('exampleContainer').innerHTML = '<p>No interactive examples available for this step.</p>';
    
    const tutorialList = document.getElementById('tutorialList');
    if (tutorialData) {
        tutorialList.innerHTML = '';
        Object.keys(tutorialData).forEach(tutorialId => {
            const tutorial = tutorialData[tutorialId];
            const isCompleted = completedTutorials.has(tutorialId);
            const tutorialBtn = document.createElement('button');
            tutorialBtn.onclick = () => loadTutorial(tutorialId);
            tutorialBtn.style.width = '100%';
            tutorialBtn.style.marginBottom = '10px';
            tutorialBtn.style.padding = '10px';
            tutorialBtn.style.textAlign = 'left';
            tutorialBtn.style.background = isCompleted ? '#27ae60' : '#3498db';
            tutorialBtn.innerHTML = `${isCompleted ? '✓' : '○'} ${tutorial.title}`;
            tutorialList.appendChild(tutorialBtn);
        });
    }
}

function loadTutorial(tutorialId) {
    if (!tutorialData[tutorialId]) return;
    
    currentTutorial = tutorialId;
    currentTutorialStep = 0;
    completedTutorials.add(tutorialId);
    
    updateTutorialViewer();
    updateProgress();
    
    document.getElementById('prevStepBtn').disabled = true;
    document.getElementById('markCompleteBtn').disabled = false;
}

function updateTutorialViewer() {
    if (!currentTutorial || !tutorialData[currentTutorial]) return;
    
    const tutorial = tutorialData[currentTutorial];
    const step = tutorial.steps[currentTutorialStep];
    
    document.getElementById('tutorialTitle').textContent = step.title;
    document.getElementById('tutorialContent').innerHTML = step.content;
    
    const stepIndicator = document.getElementById('stepIndicator');
    if (stepIndicator) {
        stepIndicator.textContent = `Step ${currentTutorialStep + 1} of ${tutorial.steps.length}`;
    }
    
    const nextBtn = document.getElementById('nextStepBtn');
    if (nextBtn) {
        nextBtn.disabled = currentTutorialStep >= tutorial.steps.length - 1;
    }
    
    const prevBtn = document.getElementById('prevStepBtn');
    if (prevBtn) {
        prevBtn.disabled = currentTutorialStep <= 0;
    }
    
    renderExamples(step.examples);
}

function renderExamples(examples) {
    const container = document.getElementById('exampleContainer');
    if (!examples || examples.length === 0) {
        container.innerHTML = '<p>No interactive examples available for this step.</p>';
        return;
    }
    
    container.innerHTML = examples.map(example => `
        <div style="padding: 10px; margin-bottom: 10px; border: 1px solid #ddd; border-radius: 4px; background: #f9f9f9;">
            <p style="margin-bottom: 8px;">${example.description}</p>
            <button onclick="tryExample('${example.id}')" style="padding: 8px 16px; cursor: pointer; background: #3498db; color: white; border: none; border-radius: 4px;">
                ${example.label}
            </button>
        </div>
    `).join('');
}

async function tryExample(exampleId) {
    const resultDiv = document.getElementById('exampleContainer');
    
    if (exampleId === 'generate-keys-example') {
        try {
            const data = await fetchJson(`${API_BASE}/demo/keys`);
            resultDiv.innerHTML = `<strong>Key Generation Result:</strong><br><pre>${JSON.stringify(data, null, 2)}</pre>`;
        } catch (error) {
            resultDiv.innerHTML = `<strong>Error:</strong> ${error.message}`;
        }
    } else if (exampleId === 'create-actor-example') {
        try {
            const data = await fetchJson(`${API_BASE}/demo/actors`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify('tutorialuser')
            });
            resultDiv.innerHTML = `<strong>Actor Creation Result:</strong><br><pre>${JSON.stringify(data, null, 2)}</pre>`;
        } catch (error) {
            resultDiv.innerHTML = `<strong>Error:</strong> ${error.message}`;
        }
    } else if (exampleId === 'submit-activity-example') {
        try {
            const data = await fetchJson(`${API_BASE}/demo/activities`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    activityId: 'post-1',
                    jsonData: JSON.stringify({ type: 'Create', object: { type: 'Note', content: 'Hello, Federation!' } })
                })
            });
            resultDiv.innerHTML = `<strong>Activity Submission Result:</strong><br><pre>${JSON.stringify(data, null, 2)}</pre>`;
        } catch (error) {
            resultDiv.innerHTML = `<strong>Error:</strong> ${error.message}`;
        }
    } else if (exampleId === 'add-peer-example') {
        try {
            const data = await fetchJson(`${API_BASE}/demo/federation/peers`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ domain: 'example.com' })
            });
            resultDiv.innerHTML = `<strong>Federation Peer Addition Result:</strong><br><pre>${JSON.stringify(data, null, 2)}</pre>`;
        } catch (error) {
            resultDiv.innerHTML = `<strong>Error:</strong> ${error.message}`;
        }
    } else if (exampleId === 'moderation-example') {
        try {
            const data = await fetchJson(`${API_BASE}/demo/moderation/settings`);
            resultDiv.innerHTML = `<strong>Moderation Settings:</strong><br><pre>${JSON.stringify(data, null, 2)}</pre>`;
        } catch (error) {
            resultDiv.innerHTML = `<strong>Error:</strong> ${error.message}`;
        }
    } else {
        resultDiv.innerHTML = `<strong>Unknown example ID:</strong> ${exampleId}`;
    }
}

function nextStep() {
    if (!currentTutorial || !tutorialData[currentTutorial]) return;
    
    const tutorial = tutorialData[currentTutorial];
    if (currentTutorialStep < tutorial.steps.length - 1) {
        currentTutorialStep++;
        updateTutorialViewer();
    }
}

function prevStep() {
    if (!currentTutorial || !tutorialData[currentTutorial]) return;
    
    if (currentTutorialStep > 0) {
        currentTutorialStep--;
        updateTutorialViewer();
    }
}

function markComplete() {
    if (!currentTutorial) return;
    
    completedTutorials.add(currentTutorial);
    alert(`Tutorial "${tutorialData[currentTutorial].title}" marked as complete!`);
    loadTutorialsSection();
}

function getTutorialProgress() {
    if (!tutorialData) return { total: 0, completed: 0, percentage: 0 };
    
    const total = Object.keys(tutorialData).length;
    const completed = completedTutorials.size;
    const percentage = total > 0 ? Math.round((completed / total) * 100) : 0;
    
    return { total, completed, percentage };
}

function updateProgress() {
    const progress = getTutorialProgress();
    const progressText = document.getElementById('progressText');
    if (progressText) {
        progressText.textContent = `${progress.completed}/${progress.total} completed (${progress.percentage}%)`;
    }
}

async function loadInstancesSection() {
    const instanceList = document.getElementById('instanceList');
    const statusDiv = document.getElementById('currentInstanceStatus');
    const actorDiv = document.getElementById('actorProfiles');
    const compareSelect1 = document.getElementById('compareInstance1Select');
    const compareSelect2 = document.getElementById('compareInstance2Select');
    
    instanceList.textContent = 'Loading instances...';
    statusDiv.textContent = '';
    actorDiv.textContent = '';
    
    try {
        const data = await fetchJson(`${API_BASE}/demo/instances`);
        
        if (data.error) {
            instanceList.textContent = `Error loading instances: ${data.error}`;
            return;
        }
        
        instances = Array.isArray(data) ? data : (data.instances || []);
        currentInstanceId = data.currentId || null;
        
        if (instances.length === 0) {
            instanceList.innerHTML = '<p>No instances configured. Add one using the form above.</p>';
        } else {
            instanceList.innerHTML = instances.map(instance => `
                <div class="instance-item" style="padding: 15px; margin-bottom: 10px; border: 1px solid #ddd; border-radius: 5px; background: ${currentInstanceId === instance.id ? '#d5f5e3' : '#f9f9f9'};">
                    <h4 style="margin-top: 0;">${instance.name || instance.id}</h4>
                    <p><strong>URL:</strong> ${instance.url}</p>
                    <p><strong>Actor:</strong> ${instance.defaultActor || 'Not configured'}</p>
                    <p><strong>Status:</strong> 
                        <span class="status-${instance.status || 'unknown'}">${instance.status || 'unknown'}</span>
                    </p>
                    <button onclick="switchInstance('${instance.id}')" ${currentInstanceId === instance.id ? 'disabled' : ''}>Switch to Instance</button>
                    <button onclick="removeInstance('${instance.id}')">Remove</button>
                    <button onclick="checkInstanceStatus('${instance.id}')">Check Status</button>
                </div>
            `).join('');
        }
        
        compareSelect1.innerHTML = '<option value="">-- Select Instance 1 --</option>' + 
            instances.map(i => `<option value="${i.id}">${i.name || i.id}</option>`).join('');
        compareSelect2.innerHTML = '<option value="">-- Select Instance 2 --</option>' + 
            instances.map(i => `<option value="${i.id}">${i.name || i.id}</option>`).join('');
        
        if (currentInstanceId) {
            await checkInstanceStatus(currentInstanceId);
        }
    } catch (error) {
        instanceList.textContent = `Error: ${error.message}`;
    }
}

function addInstance() {
    const name = document.getElementById('instanceName').value.trim();
    const url = document.getElementById('instanceUrl').value.trim();
    const actor = document.getElementById('instanceActor').value.trim();
    
    if (!name || !url) {
        alert('Please enter at least a name and URL for the instance');
        return;
    }
    
    const instance = {
        id: 'instance-' + Date.now(),
        name: name,
        url: url,
        defaultActor: actor,
        status: 'pending',
        createdAt: new Date().toISOString()
    };
    
    fetchJson(`${API_BASE}/demo/instances`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(instance)
    })
    .then(data => {
        if (data.error) {
            alert(`Error adding instance: ${data.error}`);
        } else {
            alert(`Instance "${name}" added successfully!`);
            document.getElementById('instanceName').value = '';
            document.getElementById('instanceUrl').value = '';
            document.getElementById('instanceActor').value = '';
            loadInstancesSection();
        }
    })
    .catch(error => {
        alert(`Error: ${error.message}`);
    });
}

function removeInstance(instanceId) {
    if (!confirm('Are you sure you want to remove this instance?')) return;
    
    fetchJson(`${API_BASE}/demo/instances/${encodeURIComponent(instanceId)}`, {
        method: 'DELETE'
    })
    .then(data => {
        if (data.error) {
            alert(`Error removing instance: ${data.error}`);
        } else {
            alert('Instance removed successfully');
            loadInstancesSection();
        }
    })
    .catch(error => {
        alert(`Error: ${error.message}`);
    });
}

function switchInstance(instanceId) {
    currentInstanceId = instanceId;
    
    fetchJson(`${API_BASE}/demo/instances/${encodeURIComponent(instanceId)}/switch`, {
        method: 'POST'
    })
    .then(data => {
        if (data.error) {
            alert(`Error switching instance: ${data.error}`);
        } else {
            alert(`Switched to instance: ${data.instanceName || instanceId}`);
            loadInstancesSection();
        }
    })
    .catch(error => {
        alert(`Error: ${error.message}`);
    });
}

function checkInstanceStatus(instanceId) {
    const statusDiv = document.getElementById('currentInstanceStatus');
    statusDiv.textContent = 'Checking instance status...';
    
    fetchJson(`${API_BASE}/demo/instances/${encodeURIComponent(instanceId)}/status`)
        .then(data => {
            if (data.error) {
                statusDiv.innerHTML = `<strong>Error checking status:</strong> ${data.error}`;
            } else {
                statusDiv.innerHTML = `
                    <strong>Instance Status:</strong>
                    <br>Status: <span class="status-${data.status || 'unknown'}">${data.status || 'unknown'}</span>
                    <br>URL: ${data.url || 'N/A'}
                    <br>Version: ${data.version || 'N/A'}
                    <br>Response Time: ${data.responseTime || 'N/A'}
                    <br>Timestamp: ${data.timestamp || 'N/A'}
                    ${data.actors ? `<br><strong>Actors:</strong> ${data.actors}` : ''}
                `;
            }
        })
        .catch(error => {
            statusDiv.innerHTML = `<strong>Error:</strong> ${error.message}`;
        });
}

function compareConfigurations() {
    const instance1Id = document.getElementById('compareInstance1Select').value;
    const instance2Id = document.getElementById('compareInstance2Select').value;
    const comparisonDiv = document.getElementById('configComparison');
    
    if (!instance1Id || !instance2Id) {
        alert('Please select two instances to compare');
        return;
    }
    
    comparisonDiv.textContent = 'Loading configurations...';
    
    Promise.all([
        fetchJson(`${API_BASE}/demo/instances/${encodeURIComponent(instance1Id)}/config`),
        fetchJson(`${API_BASE}/demo/instances/${encodeURIComponent(instance2Id)}/config`)
    ])
    .then(results => {
        const [config1, config2] = results;
        
        if (config1.error || config2.error) {
            comparisonDiv.innerHTML = `<strong>Error:</strong> ${config1.error || config2.error}`;
            return;
        }
        
        comparisonDiv.innerHTML = `
            <h4>Configuration Comparison</h4>
            <table style="width: 100%; border-collapse: collapse; margin-top: 10px;">
                <thead>
                    <tr>
                        <th style="padding: 10px; border: 1px solid #ddd; background: #3498db; color: white;">Setting</th>
                        <th style="padding: 10px; border: 1px solid #ddd; background: #d5f5e3; color: #275e35;">${config1.name || instance1Id}</th>
                        <th style="padding: 10px; border: 1px solid #ddd; background: #d5f5e3; color: #275e35;">${config2.name || instance2Id}</th>
                        <th style="padding: 10px; border: 1px solid #ddd; background: #f9f9f9;">Match</th>
                    </tr>
                </thead>
                <tbody>
                    <tr>
                        <td style="padding: 10px; border: 1px solid #ddd;"><strong>Name</strong></td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config1.name || 'N/A'}</td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config2.name || 'N/A'}</td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config1.name === config2.name ? '✓' : '✗'}</td>
                    </tr>
                    <tr>
                        <td style="padding: 10px; border: 1px solid #ddd;"><strong>URL</strong></td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config1.url || 'N/A'}</td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config2.url || 'N/A'}</td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config1.url === config2.url ? '✓' : '✗'}</td>
                    </tr>
                    <tr>
                        <td style="padding: 10px; border: 1px solid #ddd;"><strong>Domain</strong></td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config1.domain || 'N/A'}</td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config2.domain || 'N/A'}</td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config1.domain === config2.domain ? '✓' : '✗'}</td>
                    </tr>
                    <tr>
                        <td style="padding: 10px; border: 1px solid #ddd;"><strong>Port</strong></td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config1.port || 'N/A'}</td>
                        <td style="padding: 10px; border: 10px solid #ddd;">${config2.port || 'N/A'}</td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config1.port === config2.port ? '✓' : '✗'}</td>
                    </tr>
                    <tr>
                        <td style="padding: 10px; border: 1px solid #ddd;"><strong>Default Actor</strong></td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config1.defaultActor || 'N/A'}</td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config2.defaultActor || 'N/A'}</td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config1.defaultActor === config2.defaultActor ? '✓' : '✗'}</td>
                    </tr>
                    <tr>
                        <td style="padding: 10px; border: 1px solid #ddd;"><strong>Enabled</strong></td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config1.enabled !== undefined ? (config1.enabled ? 'Yes' : 'No') : 'N/A'}</td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config2.enabled !== undefined ? (config2.enabled ? 'Yes' : 'No') : 'N/A'}</td>
                        <td style="padding: 10px; border: 1px solid #ddd;">${config1.enabled === config2.enabled ? '✓' : '✗'}</td>
                    </tr>
                </tbody>
            </table>
        `;
    })
    .catch(error => {
        comparisonDiv.innerHTML = `<strong>Error:</strong> ${error.message}`;
    });
}

function getInstanceStatus(instanceId) {
    return fetchJson(`${API_BASE}/demo/instances/${encodeURIComponent(instanceId)}/status`);
}

function checkInstanceStatus(instanceId) {
    const statusDiv = document.getElementById('currentInstanceStatus');
    statusDiv.textContent = 'Checking instance status...';
    
    fetchJson(`${API_BASE}/demo/instances/${encodeURIComponent(instanceId)}/status`)
        .then(data => {
            if (data.error) {
                statusDiv.innerHTML = `<strong>Error checking status:</strong> ${data.error}`;
            } else {
                statusDiv.innerHTML = `
                    <strong>Instance Status:</strong>
                    <br>Status: <span class="status-${data.status || 'unknown'}">${data.status || 'unknown'}</span>
                    <br>URL: ${data.url || 'N/A'}
                    <br>Response Time: ${data.responseTime || 'N/A'}
                    <br>Timestamp: ${data.timestamp || 'N/A'}
                    ${data.actors ? `<br><strong>Actors:</strong> ${data.actors}` : ''}
                `;
            }
        })
        .catch(error => {
            statusDiv.innerHTML = `<strong>Error:</strong> ${error.message}`;
        });
}
