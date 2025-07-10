// API Demo page - Demonstrates API calls and shared types
import type { ApiResponse } from '@shared/types/index.js';
import type { User, Feature } from '@shared/types/demo.js';

// Helper function to make API calls
async function apiCall<T>(url: string, options?: RequestInit): Promise<T> {
    const response = await fetch(url, {
        headers: {
            'Content-Type': 'application/json',
            ...options?.headers
        },
        ...options
    });
    
    if (!response.ok) {
        throw new Error(`API call failed: ${response.statusText}`);
    }
    
    return response.json();
}

// Set up page interactions
document.addEventListener('DOMContentLoaded', () => {
    // Load users
    const loadUsersBtn = document.getElementById('load-users');
    const usersList = document.getElementById('users-list');
    
    if (loadUsersBtn && usersList) {
        loadUsersBtn.addEventListener('click', async () => {
            usersList.innerHTML = '<div class="loading">Loading users...</div>';
            
            try {
                const response = await apiCall<ApiResponse<User[]>>('/api/users');
                
                if (response.data) {
                    usersList.innerHTML = response.data
                        .map(user => `
                            <div class="user-item">
                                <strong>${user.name}</strong> - ${user.email}
                                <br><small>ID: ${user.id}</small>
                            </div>
                        `)
                        .join('');
                }
            } catch (error) {
                usersList.innerHTML = `<div class="error">Error: ${error}</div>`;
            }
        });
    }
    
    // Echo test
    const echoInput = document.getElementById('echo-input') as HTMLInputElement;
    const sendEchoBtn = document.getElementById('send-echo');
    const echoResult = document.getElementById('echo-result');
    
    if (sendEchoBtn && echoInput && echoResult) {
        sendEchoBtn.addEventListener('click', async () => {
            const text = echoInput.value.trim();
            if (!text) return;
            
            echoResult.innerHTML = '<div class="loading">Sending...</div>';
            
            try {
                const response = await apiCall<ApiResponse<{ echo: string }>>('/api/data', {
                    method: 'POST',
                    body: JSON.stringify({ text })
                });
                
                if (response.data) {
                    echoResult.innerHTML = `<strong>Server response:</strong> ${response.data.echo}`;
                }
            } catch (error) {
                echoResult.innerHTML = `<div class="error">Error: ${error}</div>`;
            }
        });
    }
    
    // Load features
    const loadFeaturesBtn = document.getElementById('load-features');
    const featuresList = document.getElementById('features-list');
    
    if (loadFeaturesBtn && featuresList) {
        loadFeaturesBtn.addEventListener('click', async () => {
            featuresList.innerHTML = '<div class="loading">Loading features...</div>';
            
            try {
                const response = await apiCall<ApiResponse<Feature[]>>('/api/features');
                
                if (response.data) {
                    featuresList.innerHTML = '<ul>' + 
                        response.data
                            .map(feature => `<li><strong>${feature.name}:</strong> ${feature.description}</li>`)
                            .join('') +
                        '</ul>';
                }
            } catch (error) {
                featuresList.innerHTML = `<div class="error">Error: ${error}</div>`;
            }
        });
    }
});