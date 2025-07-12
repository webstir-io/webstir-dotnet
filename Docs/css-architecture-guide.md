# Webstir CSS Architecture Guide

## Overview

Webstir uses a simple yet powerful CSS bundling strategy that supports both global and page-specific styles with predictable cascading order. This guide explains how to effectively organize and write CSS in your webstir projects.

## Core Concepts

### 1. CSS File Organization

Webstir organizes CSS into two main categories:

- **App-level CSS**: Global styles that apply to all pages
- **Page-level CSS**: Styles specific to individual pages

### 2. Bundling Process

For each page, webstir creates a single CSS file containing:
1. All app-level CSS (from `app/app.css` and subdirectories)
2. The main page CSS file (e.g., `about/about.css`)
3. Additional page CSS files (from subdirectories, sorted by number)

## Directory Structure

```
src/client/
├── app/
│   ├── app.css                    # Main app styles
│   └── styles/                    # App-level style modules
│       ├── 00-tokens.css          # Design tokens/CSS variables
│       ├── 01-reset.css           # CSS reset/normalize
│       ├── 02-base.css            # Base element styles
│       ├── 03-layout.css          # Layout utilities
│       ├── 04-components.css      # Shared components
│       └── 05-utilities.css       # Utility classes
└── pages/
    ├── home/
    │   ├── home.css               # Home page base styles
    │   └── styles/
    │       ├── 768-tablet.css     # Tablet breakpoint
    │       └── 1024-desktop.css   # Desktop breakpoint
    └── about/
        ├── about.css              # About page base styles
        └── styles/
            ├── components/        # Page-specific components
            │   └── team-grid.css
            └── responsive/
                ├── 768-tablet.css
                └── 1024-desktop.css
```

## Numbering System

The numbering system controls the order in which CSS files are concatenated:

### App-Level Numbering
```css
/* 00-tokens.css - Loaded first */
:root {
  --color-primary: #0066cc;
  --color-text: #333;
  --spacing-unit: 0.25rem;
}

/* 01-reset.css - Loaded second */
*, *::before, *::after {
  box-sizing: border-box;
}

/* 02-base.css - Loaded third */
body {
  font-family: system-ui, sans-serif;
  color: var(--color-text);
}
```

### Responsive Design Numbering
```css
/* 320-mobile.css - Mobile-first base */
.container {
  padding: 1rem;
}

/* 768-tablet.css - Tablet and up */
@media (min-width: 768px) {
  .container {
    padding: 2rem;
    max-width: 750px;
    margin: 0 auto;
  }
}

/* 1024-desktop.css - Desktop and up */
@media (min-width: 1024px) {
  .container {
    max-width: 1200px;
  }
}
```

## Best Practices

### 1. Design Tokens

Create a central token file for consistent design values:

```css
/* app/styles/00-tokens.css */
:root {
  /* Colors */
  --color-primary: #0066cc;
  --color-primary-light: #3385d6;
  --color-primary-dark: #004c99;
  --color-text: #333;
  --color-text-muted: #666;
  --color-background: #f5f5f5;
  
  /* Typography */
  --font-family-base: system-ui, -apple-system, sans-serif;
  --font-family-mono: 'Consolas', 'Monaco', monospace;
  --font-size-base: 1rem;
  --font-size-lg: 1.25rem;
  --font-size-sm: 0.875rem;
  
  /* Spacing */
  --spacing-xs: 0.25rem;
  --spacing-sm: 0.5rem;
  --spacing-md: 1rem;
  --spacing-lg: 2rem;
  --spacing-xl: 4rem;
  
  /* Breakpoints (for reference) */
  --breakpoint-tablet: 768px;
  --breakpoint-desktop: 1024px;
  --breakpoint-wide: 1440px;
}
```

### 2. Component Styles

Organize reusable components in app-level CSS:

```css
/* app/styles/04-components.css */

/* Button Component */
.btn {
  display: inline-flex;
  align-items: center;
  padding: var(--spacing-sm) var(--spacing-md);
  font-size: var(--font-size-base);
  font-weight: 500;
  text-decoration: none;
  border: none;
  border-radius: 0.375rem;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn--primary {
  background-color: var(--color-primary);
  color: white;
}

.btn--primary:hover {
  background-color: var(--color-primary-dark);
}

.btn--secondary {
  background-color: transparent;
  color: var(--color-primary);
  border: 2px solid var(--color-primary);
}

/* Card Component */
.card {
  background: white;
  border-radius: 0.5rem;
  padding: var(--spacing-lg);
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.card__title {
  font-size: var(--font-size-lg);
  margin-bottom: var(--spacing-md);
}
```

### 3. Page-Specific Styles

Keep page-specific styles scoped to their pages:

```css
/* pages/about/about.css */

/* Page-specific layout */
.about-hero {
  background: linear-gradient(135deg, var(--color-primary), var(--color-primary-dark));
  color: white;
  padding: var(--spacing-xl) var(--spacing-lg);
  text-align: center;
}

.about-hero__title {
  font-size: 2.5rem;
  margin-bottom: var(--spacing-md);
}

/* Team section */
.team-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: var(--spacing-lg);
  margin-top: var(--spacing-xl);
}
```

### 4. Responsive Design Pattern

Use a mobile-first approach with numbered files:

```css
/* pages/products/styles/base.css - Mobile first */
.product-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: var(--spacing-md);
}

/* pages/products/styles/768-tablet.css */
@media (min-width: 768px) {
  .product-grid {
    grid-template-columns: repeat(2, 1fr);
    gap: var(--spacing-lg);
  }
}

/* pages/products/styles/1024-desktop.css */
@media (min-width: 1024px) {
  .product-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}

/* pages/products/styles/1440-wide.css */
@media (min-width: 1440px) {
  .product-grid {
    grid-template-columns: repeat(4, 1fr);
    max-width: 1400px;
    margin: 0 auto;
  }
}
```

### 5. Utility Classes

Create utility classes for common patterns:

```css
/* app/styles/05-utilities.css */

/* Display utilities */
.hidden { display: none !important; }
.block { display: block !important; }
.inline-block { display: inline-block !important; }
.flex { display: flex !important; }
.inline-flex { display: inline-flex !important; }
.grid { display: grid !important; }

/* Flexbox utilities */
.items-center { align-items: center !important; }
.justify-center { justify-content: center !important; }
.justify-between { justify-content: space-between !important; }
.flex-wrap { flex-wrap: wrap !important; }
.gap-sm { gap: var(--spacing-sm) !important; }
.gap-md { gap: var(--spacing-md) !important; }
.gap-lg { gap: var(--spacing-lg) !important; }

/* Spacing utilities */
.mt-0 { margin-top: 0 !important; }
.mt-sm { margin-top: var(--spacing-sm) !important; }
.mt-md { margin-top: var(--spacing-md) !important; }
.mt-lg { margin-top: var(--spacing-lg) !important; }
.mt-xl { margin-top: var(--spacing-xl) !important; }

/* Text utilities */
.text-center { text-align: center !important; }
.text-right { text-align: right !important; }
.font-bold { font-weight: 700 !important; }
.text-sm { font-size: var(--font-size-sm) !important; }
.text-lg { font-size: var(--font-size-lg) !important; }

/* Responsive utilities */
@media (min-width: 768px) {
  .tablet\:hidden { display: none !important; }
  .tablet\:block { display: block !important; }
  .tablet\:flex { display: flex !important; }
}
```

## Advanced Patterns

### 1. Container Queries (Future Enhancement)

When browser support improves, use container queries for component-based responsive design:

```css
/* Component responds to its container, not viewport */
.card-container {
  container-type: inline-size;
}

.card {
  padding: var(--spacing-md);
}

@container (min-width: 400px) {
  .card {
    display: grid;
    grid-template-columns: 200px 1fr;
    gap: var(--spacing-md);
  }
}
```

### 2. CSS Custom Properties for Theming

Use CSS variables for easy theming:

```css
/* app/styles/00-tokens.css */
:root {
  --theme-primary: #0066cc;
  --theme-background: #ffffff;
  --theme-text: #333333;
}

/* Dark theme */
[data-theme="dark"] {
  --theme-primary: #4d94ff;
  --theme-background: #1a1a1a;
  --theme-text: #e0e0e0;
}

/* Usage */
body {
  background-color: var(--theme-background);
  color: var(--theme-text);
}
```

### 3. Print Styles

Include print-specific styles:

```css
/* app/styles/99-print.css */
@media print {
  /* Hide navigation and non-essential elements */
  .nav,
  .footer,
  .sidebar,
  .no-print {
    display: none !important;
  }
  
  /* Optimize for print */
  body {
    font-size: 12pt;
    line-height: 1.5;
    color: black;
    background: white;
  }
  
  /* Ensure links are visible */
  a[href]:after {
    content: " (" attr(href) ")";
  }
}
```

## Performance Tips

### 1. Minimize Specificity

Keep specificity low for easier overrides:

```css
/* Good - Low specificity */
.card { }
.card__title { }

/* Avoid - High specificity */
div.content .card h2.title { }
```

### 2. Use CSS Variables for Dynamic Values

```css
/* Define once, use everywhere */
.dynamic-spacing {
  padding: var(--component-padding, var(--spacing-md));
}

/* Override locally */
.special-section {
  --component-padding: var(--spacing-xl);
}
```

### 3. Optimize Media Queries

Group related styles in media query files:

```css
/* 768-tablet.css - All tablet styles together */
@media (min-width: 768px) {
  .container { max-width: 750px; }
  .grid { grid-template-columns: repeat(2, 1fr); }
  .hero { padding: var(--spacing-xl); }
  /* More tablet styles... */
}
```

## Common Patterns

### 1. Hero Section
```css
.hero {
  min-height: 50vh;
  display: flex;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: var(--spacing-xl) var(--spacing-lg);
  background: linear-gradient(135deg, var(--color-primary), var(--color-primary-dark));
  color: white;
}

.hero__title {
  font-size: clamp(2rem, 5vw, 3rem);
  margin-bottom: var(--spacing-md);
}
```

### 2. Responsive Grid
```css
.auto-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(250px, 100%), 1fr));
  gap: var(--spacing-lg);
}
```

### 3. Aspect Ratio Box
```css
.aspect-ratio {
  position: relative;
  width: 100%;
}

.aspect-ratio--16-9 {
  padding-bottom: 56.25%; /* 9/16 */
}

.aspect-ratio__content {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
}
```

## Testing Your CSS

### 1. Browser Testing
- Test in multiple browsers (Chrome, Firefox, Safari, Edge)
- Use browser DevTools to debug styles
- Check responsive breakpoints

### 2. Performance Testing
- Keep total CSS under 100KB (minified)
- Use CSS analyzers to find unused styles
- Monitor paint and layout performance

### 3. Accessibility Testing
- Ensure sufficient color contrast
- Test with keyboard navigation
- Verify focus indicators are visible

## Troubleshooting

### Common Issues

1. **Styles not applying**: Check file naming and directory structure
2. **Wrong cascade order**: Verify numbering in filenames
3. **Media queries not working**: Ensure proper number prefixes (768-, 1024-)
4. **Specificity conflicts**: Use consistent naming patterns (BEM)

### Debug Mode

In development, webstir adds comments showing source files:

```css
/* app.css */
.container { }

/* 768-tablet.css */
@media (min-width: 768px) { }
```

## Migration Guide

### From Traditional CSS
1. Move global styles to `app/app.css`
2. Create numbered files in `app/styles/` for modules
3. Move page styles to respective page directories
4. Add number prefixes for responsive styles

### From CSS-in-JS
1. Extract component styles to `app/styles/04-components.css`
2. Convert dynamic styles to CSS variables
3. Move page-specific styles to page directories

## Summary

Webstir's CSS architecture provides:
- **Simplicity**: No complex configuration needed
- **Flexibility**: Organize styles however you prefer
- **Performance**: Single CSS file per page
- **Maintainability**: Clear file organization
- **Scalability**: Works for small and large projects

By following these patterns and best practices, you'll create maintainable, performant CSS that scales with your project.