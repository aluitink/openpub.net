// FederationApp JavaScript

const apiBase = '/api/federation';
let instances = [];

async function fetchInstances() {
    try {
        const response = await fetch(`${apiBase}/instances`);
        if (response.ok) {
            instances = await response.json();
            renderInstances();
        }
    } catch (error) {
        console.error('Error fetching instances:', error);
    }
}

async function discoverInstance() {
    const domain = document.getElementById('domainInput').value.trim();
    if (!domain) {
        showNotification('Please enter a domain', 'error');
        return;
    }

    try {
        const response = await fetch(`${apiBase}/discover`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(domain)
        });

        const success = await response.json();
        if (success) {
            showNotification(`Successfully discovered ${domain}`, 'success');
            fetchInstances();
            document.getElementById('domainInput').value = '';
        } else {
            showNotification(`Failed to discover ${domain}`, 'error');
        }
    } catch (error) {
        showNotification(`Error: ${error.message}`, 'error');
    }
}

async function followInstance(domain) {
    try {
        const response = await fetch(`${apiBase}/follow`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(domain)
        });

        const success = await response.json();
        showNotification(success ? `Following ${domain}` : `Failed to follow ${domain}`, success ? 'success' : 'error');
        fetchInstances();
    } catch (error) {
        showNotification(`Error: ${error.message}`, 'error');
    }
}

async function unfollowInstance(domain) {
    try {
        const response = await fetch(`${apiBase}/unfollow`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(domain)
        });

        const success = await response.json();
        showNotification(success ? `Unfollowed ${domain}` : `Failed to unfollow ${domain}`, success ? 'success' : 'error');
        fetchInstances();
    } catch (error) {
        showNotification(`Error: ${error.message}`, 'error');
    }
}

async function createNote() {
    const content = document.getElementById('noteContent').value.trim();
    const domainsStr = document.getElementById('noteDomains').value.trim();

    if (!content) {
        showNotification('Please enter note content', 'error');
        return;
    }

    const domains = domainsStr.split(',').map(d => d.trim()).filter(d => d);

    if (domains.length === 0) {
        showNotification('Please specify at least one destination domain', 'error');
        return;
    }

    try {
        const response = await fetch(`${apiBase}/note`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ content, toDomains: domains })
        });

        const success = await response.json();
        showNotification(success ? 'Note created successfully!' : 'Failed to create note', success ? 'success' : 'error');
        document.getElementById('noteContent').value = '';
        document.getElementById('noteDomains').value = '';
    } catch (error) {
        showNotification(`Error: ${error.message}`, 'error');
    }
}

async function broadcastNote() {
    const content = document.getElementById('noteContent').value.trim();

    if (!content) {
        showNotification('Please enter note content', 'error');
        return;
    }

    try {
        const response = await fetch(`${apiBase}/broadcast`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ content })
        });

        const count = await response.json();
        showNotification(`Note broadcast to ${count} instances!`, 'success');
        document.getElementById('noteContent').value = '';
    } catch (error) {
        showNotification(`Error: ${error.message}`, 'error');
    }
}

function renderInstances() {
    const list = document.getElementById('instanceList');
    list.innerHTML = '<li class="instance-item"><div class="instance-info"><span class="status-indicator status-connected"></span><h3>This Instance</h3><p>localhost:5001</p></div></li>';

    let successCount = 0;
    let failedCount = 0;

    instances.forEach(instance => {
        const statusClass = instance.isConnected ? 'status-connected' : 'status-disconnected';
        const statusText = instance.isConnected ? 'Connected' : 'Disconnected';

        list.innerHTML += `
            <li class="instance-item">
                <div class="instance-info">
                    <span class="status-indicator ${statusClass}"></span>
                    <h3>${instance.domain}</h3>
                    <p>${instance.actorId}</p>
                    <p style="font-size: 0.8rem; margin-top: 5px;">
                        Delivered: ${instance.successfulDeliveries} | Failed: ${instance.failedDeliveries}
                    </p>
                </div>
                <div>
                    ${instance.isConnected 
                        ? `<button class="btn-sm btn-danger" onclick="unfollowInstance('${instance.domain}')">Unfollow</button>`
                        : `<button class="btn-sm btn-primary" onclick="followInstance('${instance.domain}')">Follow</button>`
                    }
                </div>
            </li>
        `;

        successCount += instance.successfulDeliveries;
        failedCount += instance.failedDeliveries;
    });

    document.getElementById('instanceCount').textContent = instances.length;
    document.getElementById('successCount').textContent = successCount;
    document.getElementById('failedCount').textContent = failedCount;
}

function showNotification(message, type = 'success') {
    const notification = document.getElementById('notification');
    notification.textContent = message;
    notification.className = `notification ${type} show`;

    setTimeout(() => {
        notification.className = 'notification';
    }, 3000);
}

fetchInstances();
