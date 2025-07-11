import type { RouteHandler } from '@shared/router-types.js';

export const routeHandler: RouteHandler = {
  onEnter: (params) => {
    console.log('Entering Features page', params);
    
    // Add entrance animation to feature cards
    const cards = document.querySelectorAll('.feature-card');
    cards.forEach((card, index) => {
      setTimeout(() => {
        card.classList.add('active');
        setTimeout(() => card.classList.remove('active'), 300);
      }, index * 100);
    });
    
    // Add click handlers to feature cards
    setupFeatureCardInteractions();
    
    // Check if specific feature is requested via query params
    if (params.feature) {
      highlightFeature(params.feature);
    }
  },
  
  onLeave: () => {
    console.log('Leaving Features page');
    
    // Clean up event listeners
    const cards = document.querySelectorAll('.feature-card');
    cards.forEach(card => {
      card.replaceWith(card.cloneNode(true));
    });
  },
  
  onUpdate: (params) => {
    console.log('Features page updated with params:', params);
    
    // Handle feature highlighting from URL params
    if (params.feature) {
      highlightFeature(params.feature);
    } else {
      clearHighlights();
    }
  }
};

function setupFeatureCardInteractions() {
  const cards = document.querySelectorAll('.feature-card');
  
  cards.forEach(card => {
    card.addEventListener('click', () => {
      const featureName = card.getAttribute('data-feature');
      if (featureName) {
        // Update URL with feature parameter
        const url = new URL(window.location.href);
        url.searchParams.set('feature', featureName);
        window.history.pushState({}, '', url.toString());
        
        // Highlight the clicked feature
        highlightFeature(featureName);
      }
    });
    
    // Add hover sound effect (visual feedback)
    card.addEventListener('mouseenter', () => {
      const icon = card.querySelector('.feature-icon') as HTMLElement;
      if (icon) {
        icon.style.transform = 'scale(1.1) rotate(5deg)';
      }
    });
    
    card.addEventListener('mouseleave', () => {
      const icon = card.querySelector('.feature-icon') as HTMLElement;
      if (icon) {
        icon.style.transform = 'scale(1) rotate(0deg)';
      }
    });
  });
}

function highlightFeature(featureName: string) {
  // Clear previous highlights
  clearHighlights();
  
  // Highlight the selected feature
  const targetCard = document.querySelector(`[data-feature="${featureName}"]`);
  if (targetCard) {
    targetCard.classList.add('highlighted');
    targetCard.scrollIntoView({ behavior: 'smooth', block: 'center' });
    
    // Add a temporary glow effect
    targetCard.classList.add('glow');
    setTimeout(() => targetCard.classList.remove('glow'), 1000);
  }
}

function clearHighlights() {
  document.querySelectorAll('.feature-card').forEach(card => {
    card.classList.remove('highlighted', 'glow');
  });
}

// Add dynamic styles for highlighting
const style = document.createElement('style');
style.textContent = `
  .feature-icon {
    transition: transform 0.3s ease;
  }
  
  .feature-card.highlighted {
    background: linear-gradient(135deg, #e3f2fd 0%, #f3e5f5 100%);
    border: 2px solid #1976d2;
  }
  
  .feature-card.glow {
    animation: glow 1s ease-out;
  }
  
  @keyframes glow {
    0%, 100% {
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
    }
    50% {
      box-shadow: 0 0 20px rgba(25, 118, 210, 0.6);
    }
  }
`;
document.head.appendChild(style);