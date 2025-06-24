# Minimal Shared Types Implementation

## Overview
This document outlines a minimal approach to adding shared TypeScript types between client and server in Webstir. Start simple, expand only when needed.

## Current State
- TypeScript already configured with `@shared/*` path mappings
- Client/server separation exists, but no shared directory
- ServerWorker and ScriptsWorker handle separate compilation

## Implementation Steps

### Step 1: Create Minimal Structure
Create a single directory with one file:
```
src/shared/
└── types/
    └── index.ts
```

### Step 2: Add One Example Type
```typescript
// src/shared/types/index.ts
export interface ApiResponse<T> {
  data?: T;
  error?: string;
}
```

### Step 3: Create SharedWorker
Create a new `SharedWorker` class that implements `IFileWorker` to handle initialization of the shared directory structure and initial types file.

### Step 4: Update Workers
Modify ScriptsWorker.cs to include the shared directory in compilation paths (ServerWorker already includes it).

### Step 5: Validate with Real Usage

**Server example:**
```typescript
// src/server/api/health.ts
import { ApiResponse } from '@shared/types';

export function getHealth(): ApiResponse<{ status: string }> {
  return { data: { status: 'ok' } };
}
```

**Client example:**
```typescript
// src/client/pages/home/index.ts
import { ApiResponse } from '@shared/types';

async function checkHealth() {
  const response = await fetch('/api/health');
  const result: ApiResponse<{ status: string }> = await response.json();
  
  if (result.data) {
    console.log('Server status:', result.data.status);
  }
}
```

### Step 6: Expand As Needed
Add types only when you actually need them. Keep everything in index.ts until it becomes unwieldy (>200 lines), then split.

## What NOT to Do (Yet)

- ❌ Complex directory structures
- ❌ Validation schemas
- ❌ Code generation
- ❌ Runtime type checking
- ❌ Separate files for each type
- ❌ Abstract base types
- ❌ Build optimizations

## Benefits of This Approach

1. **Quick Setup**: Working in 30 minutes
2. **Low Risk**: Minimal changes to existing code
3. **Easy to Test**: One type, two usages
4. **Future Proof**: Easy to expand without breaking changes

## When to Expand

Consider adding complexity only when you experience these pain points:
- index.ts becomes too large (>200 lines)
- Multiple developers creating conflicting types
- Need runtime validation for external APIs
- Want to generate documentation from types

## Quick Checklist

- [ ] Create SharedWorker.cs implementing IFileWorker
- [ ] Register SharedWorker in Program.cs DI container
- [ ] Run `webstir init` to create src/shared/types/index.ts
- [ ] Update ScriptsWorker.cs to include shared directory
- [ ] Create one server endpoint using the type
- [ ] Create one client function using the type
- [ ] Verify builds work