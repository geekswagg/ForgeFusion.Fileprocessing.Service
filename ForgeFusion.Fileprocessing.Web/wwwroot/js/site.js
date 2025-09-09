window.downloadFile = (url, filename) => {
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

// Enhanced Blazor connection handling
window.blazorConnectionHandler = {
    maxRetries: 8,
    retryInterval: 2000,
    
    onConnectionDown: () => {
        console.log('Blazor connection lost, attempting to reconnect...');
        // Show a subtle notification instead of the modal
        window.blazorConnectionHandler.showConnectionStatus(false);
    },
    
    onConnectionUp: () => {
        console.log('Blazor connection restored');
        window.blazorConnectionHandler.showConnectionStatus(true);
        // Hide notification after a short delay
        setTimeout(() => {
            window.blazorConnectionHandler.hideConnectionStatus();
        }, 2000);
    },
    
    onReconnectFailed: () => {
        console.log('Failed to reconnect to Blazor server');
        // Show a more prominent error message
        window.blazorConnectionHandler.showReconnectFailed();
    },
    
    showConnectionStatus: (isConnected) => {
        // Remove existing status
        const existing = document.getElementById('blazor-connection-status');
        if (existing) {
            existing.remove();
        }
        
        // Create status indicator
        const status = document.createElement('div');
        status.id = 'blazor-connection-status';
        status.style.cssText = `
            position: fixed;
            top: 10px;
            right: 10px;
            z-index: 9999;
            padding: 8px 16px;
            border-radius: 4px;
            font-size: 14px;
            font-weight: 500;
            box-shadow: 0 2px 8px rgba(0,0,0,0.15);
            transition: all 0.3s ease;
        `;
        
        if (isConnected) {
            status.innerHTML = '<i class="bi bi-check-circle"></i> Connected';
            status.style.backgroundColor = '#d4edda';
            status.style.color = '#155724';
            status.style.border = '1px solid #c3e6cb';
        } else {
            status.innerHTML = '<i class="bi bi-exclamation-triangle"></i> Reconnecting...';
            status.style.backgroundColor = '#fff3cd';
            status.style.color = '#856404';
            status.style.border = '1px solid #ffeaa7';
        }
        
        document.body.appendChild(status);
    },
    
    hideConnectionStatus: () => {
        const status = document.getElementById('blazor-connection-status');
        if (status) {
            status.style.opacity = '0';
            setTimeout(() => status.remove(), 300);
        }
    },
    
    showReconnectFailed: () => {
        const modal = document.createElement('div');
        modal.innerHTML = `
            <div style="position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.5); z-index: 10000; display: flex; align-items: center; justify-content: center;">
                <div style="background: white; padding: 20px; border-radius: 8px; max-width: 400px; text-align: center;">
                    <h5 style="margin-bottom: 15px; color: #dc3545;">Connection Lost</h5>
                    <p style="margin-bottom: 20px;">The connection to the server has been lost. Please refresh the page to continue.</p>
                    <button onclick="window.location.reload()" style="background: #007bff; color: white; border: none; padding: 10px 20px; border-radius: 4px; cursor: pointer;">Refresh Page</button>
                </div>
            </div>
        `;
        document.body.appendChild(modal);
    }
};

// Apply the custom handler when Blazor starts
document.addEventListener('DOMContentLoaded', () => {
    if (window.Blazor) {
        window.Blazor.defaultReconnectionHandler = window.blazorConnectionHandler;
    }
});