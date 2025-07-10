import type { RouteHandler } from '@shared/router-types.js';

let navigationCount = 0;

export const routeHandler: RouteHandler = {
  onEnter: (params) => {
    console.log('Entering About page', params);
    
    // Update route info
    const routeEl = document.getElementById('current-route');
    if (routeEl) {
      routeEl.textContent = window.location.pathname;
    }
    
    // Set load time
    const timeEl = document.getElementById('load-time');
    if (timeEl) {
      timeEl.textContent = new Date().toLocaleTimeString();
    }
    
    // Update navigation count
    navigationCount++;
    const countEl = document.getElementById('nav-count');
    if (countEl) {
      countEl.textContent = navigationCount.toString();
    }
    
    // Add animation class
    const page = document.querySelector('.about-page');
    if (page) {
      page.classList.add('page-enter');
      setTimeout(() => page.classList.remove('page-enter'), 300);
    }
  },
  
  onLeave: () => {
    console.log('Leaving About page');
  },
  
  onUpdate: (params) => {
    console.log('About page updated with params:', params);
    
    // Update current route display
    const routeEl = document.getElementById('current-route');
    if (routeEl) {
      const queryString = window.location.search;
      routeEl.textContent = window.location.pathname + queryString;
    }
  }
};

// Add page enter animation
const style = document.createElement('style');
style.textContent = `
  .page-enter {
    animation: fadeIn 0.3s ease-out;
  }
  
  @keyframes fadeIn {
    from {
      opacity: 0;
      transform: translateY(10px);
    }
    to {
      opacity: 1;
      transform: translateY(0);
    }
  }
`;
document.head.appendChild(style);