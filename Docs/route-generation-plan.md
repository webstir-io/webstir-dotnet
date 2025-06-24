# API Route Generation from TypeScript Types

## Overview

This document outlines the implementation plan for automatic API route generation from TypeScript type definitions in webstir. The goal is to create a type-safe, end-to-end system where API contracts are defined once and used to generate both server-side handlers and client-side API calls.

### Initial Implementation Scope
- Type-safe API contract definitions
- Client-side API client generation
- Server-side route type helpers
- Compile-time type checking

### Future Enhancements (Not in Initial Release)
- Runtime validation
- Authentication/authorization types
- OpenAPI documentation generation
- Advanced middleware system

## Core Concepts

### Type-Driven Development
- Define API endpoints as TypeScript interfaces
- Generate server route handlers with full type safety
- Generate client-side API utilities automatically
- Ensure compile-time type checking across the entire stack

### Single Source of Truth
- API contracts live in `src/shared/api/`
- Both client and server derive their implementations from these contracts
- Changes to API types immediately reflect in both environments
- Breaking changes are caught at compile time

## Architecture

### Directory Structure
```
src/
├── shared/
│   ├── api/              # API endpoint definitions
│   │   ├── index.ts      # Main API type exports
│   │   ├── users.ts      # User endpoints
│   │   ├── posts.ts      # Post endpoints
│   │   └── auth.ts       # Auth endpoints
│   ├── types/            # Shared type definitions
│   └── validators/       # Runtime validation schemas
├── server/
│   ├── api/              # Generated + custom handlers
│   │   ├── router.ts     # Type-safe router
│   │   └── handlers/     # Implementation files
│   └── middleware/       # Server middleware
└── client/
    └── api/              # Generated API client
        └── client.ts     # Type-safe fetch wrapper
```

## API Contract Definition

### Basic Structure
```typescript
// src/shared/api/types.ts
export interface ApiEndpoint<TParams = {}, TQuery = {}, TBody = {}, TResponse = any> {
  params?: TParams;
  query?: TQuery;
  body?: TBody;
  response: TResponse;
}

export interface ApiContract {
  [path: string]: {
    GET?: ApiEndpoint;
    POST?: ApiEndpoint;
    PUT?: ApiEndpoint;
    DELETE?: ApiEndpoint;
    PATCH?: ApiEndpoint;
  };
}
```

### Example API Definition
```typescript
// src/shared/api/users.ts
import type { User, CreateUserDto, UpdateUserDto } from '@shared/types';

export interface UserEndpoints extends ApiContract {
  '/api/users': {
    GET: {
      query: {
        page?: number;
        limit?: number;
        sort?: 'name' | 'email' | 'created';
      };
      response: {
        users: User[];
        total: number;
        page: number;
      };
    };
    POST: {
      body: CreateUserDto;
      response: User;
    };
  };
  
  '/api/users/:id': {
    GET: {
      params: { id: string };
      response: User;
    };
    PUT: {
      params: { id: string };
      body: UpdateUserDto;
      response: User;
    };
    DELETE: {
      params: { id: string };
      response: { success: boolean };
    };
  };
  
  '/api/users/:id/avatar': {
    POST: {
      params: { id: string };
      body: FormData;
      response: { url: string };
    };
  };
}
```

## Server-Side Implementation

### Type-Safe Router
```typescript
// src/server/api/router.ts
import type { ApiContract, ApiEndpoint } from '@shared/api/types';

export class TypedRouter<T extends ApiContract> {
  private routes: Map<string, Map<string, Handler>> = new Map();
  
  get<Path extends keyof T & string>(
    path: Path,
    handler: Handler<T[Path]['GET']>
  ): this {
    this.addRoute('GET', path, handler);
    return this;
  }
  
  post<Path extends keyof T & string>(
    path: Path,
    handler: Handler<T[Path]['POST']>
  ): this {
    this.addRoute('POST', path, handler);
    return this;
  }
  
  // Similar methods for PUT, DELETE, PATCH
}

type Handler<T extends ApiEndpoint = any> = (
  req: TypedRequest<T>
) => Promise<T['response']> | T['response'];

interface TypedRequest<T extends ApiEndpoint> {
  params: T['params'];
  query: T['query'];
  body: T['body'];
}
```

### Handler Implementation
```typescript
// src/server/api/handlers/users.ts
import { TypedRouter } from '../router';
import type { UserEndpoints } from '@shared/api/users';
import { userService } from '../services/users';

export const userRouter = new TypedRouter<UserEndpoints>();

userRouter
  .get('/api/users', async (req) => {
    const { page = 1, limit = 10, sort = 'created' } = req.query;
    const users = await userService.list({ page, limit, sort });
    return {
      users,
      total: users.length,
      page
    };
  })
  .post('/api/users', async (req) => {
    const user = await userService.create(req.body);
    return user;
  })
  .get('/api/users/:id', async (req) => {
    const user = await userService.findById(req.params.id);
    if (!user) throw new NotFoundError('User not found');
    return user;
  });
```

## Client-Side Implementation

### Generated API Client
```typescript
// build/client/api/generated.ts (auto-generated)
import type { UserEndpoints } from '@shared/api/users';
import { apiClient } from './client';

export const api = {
  users: {
    list: (query?: UserEndpoints['/api/users']['GET']['query']) => 
      apiClient.get<UserEndpoints['/api/users']['GET']['response']>(
        '/api/users', 
        { query }
      ),
    
    create: (data: UserEndpoints['/api/users']['POST']['body']) =>
      apiClient.post<UserEndpoints['/api/users']['POST']['response']>(
        '/api/users',
        { body: data }
      ),
    
    get: (id: string) =>
      apiClient.get<UserEndpoints['/api/users/:id']['GET']['response']>(
        `/api/users/${id}`
      ),
    
    update: (id: string, data: UserEndpoints['/api/users/:id']['PUT']['body']) =>
      apiClient.put<UserEndpoints['/api/users/:id']['PUT']['response']>(
        `/api/users/${id}`,
        { body: data }
      ),
    
    delete: (id: string) =>
      apiClient.delete<UserEndpoints['/api/users/:id']['DELETE']['response']>(
        `/api/users/${id}`
      ),
    
    uploadAvatar: (id: string, file: File) => {
      const formData = new FormData();
      formData.append('avatar', file);
      return apiClient.post<UserEndpoints['/api/users/:id/avatar']['POST']['response']>(
        `/api/users/${id}/avatar`,
        { body: formData }
      );
    }
  }
};
```

### Base API Client
```typescript
// src/client/api/client.ts
export class ApiClient {
  private baseUrl = '';
  
  async get<T>(path: string, options?: { query?: any }): Promise<T> {
    const url = new URL(path, window.location.origin);
    if (options?.query) {
      Object.entries(options.query).forEach(([key, value]) => {
        if (value !== undefined) {
          url.searchParams.append(key, String(value));
        }
      });
    }
    
    const response = await fetch(url.toString());
    if (!response.ok) throw new ApiError(response);
    return response.json();
  }
  
  async post<T>(path: string, options?: { body?: any }): Promise<T> {
    const response = await fetch(path, {
      method: 'POST',
      headers: options?.body instanceof FormData 
        ? {} 
        : { 'Content-Type': 'application/json' },
      body: options?.body instanceof FormData 
        ? options.body 
        : JSON.stringify(options?.body)
    });
    
    if (!response.ok) throw new ApiError(response);
    return response.json();
  }
  
  // Similar methods for PUT, DELETE, PATCH
}

export const apiClient = new ApiClient();
```

## Code Generation

### ApiGeneratorWorker
```csharp
// CLI/Workers/ApiGeneratorWorker.cs
public class ApiGeneratorWorker : IWebFileWorker
{
    public int BuildOrder => 2; // After TypesWorker
    
    public void Build(bool releaseMode)
    {
        // Only run if src/shared/api directory exists
        if (!Directory.Exists("src/shared/api"))
            return;
        
        // 1. Parse TypeScript API definitions using ts-morph or similar
        var apiContracts = ParseApiContracts("src/shared/api");
        
        // 2. Generate client-side API utilities
        GenerateClientApi(apiContracts, "build/client/api/generated.ts");
        
        // 3. Generate server-side route type definitions
        GenerateServerTypes(apiContracts, "build/server/api/generated.ts");
        
        // 4. Future: Generate validation functions (not in initial release)
    }
    
    public string[] GetWatchPattern() => new[] { "src/shared/api/**/*.ts" };
}
```

### Generation Process
1. **Parse API Contracts**: Use TypeScript compiler API to extract type information
2. **Generate Client Code**: Create type-safe fetch wrappers for each endpoint
3. **Generate Server Types**: Create router type definitions and helpers

## Validation (Future Enhancement)

Runtime validation will be added as a later enhancement. The initial implementation will rely on TypeScript's compile-time type checking. When implemented, validation will use built-in JavaScript/TypeScript features without external dependencies.

### Planned Approach
```typescript
// Future: Generate simple type guards
export function isCreateUserDto(data: any): data is CreateUserDto {
  return (
    typeof data === 'object' &&
    typeof data.email === 'string' &&
    typeof data.name === 'string' &&
    typeof data.password === 'string'
  );
}

// Future: Basic validation middleware
export function validate<T>(validator: (data: any) => data is T) {
  return (req: Request, res: Response, next: NextFunction) => {
    if (!validator(req.body)) {
      return res.status(400).json({ error: 'Invalid request data' });
    }
    next();
  };
}
```

## Error Handling

### Type-Safe Errors
```typescript
// src/shared/api/errors.ts
export class ApiError extends Error {
  constructor(
    public statusCode: number,
    public code: string,
    message: string
  ) {
    super(message);
  }
}

export class NotFoundError extends ApiError {
  constructor(message: string) {
    super(404, 'NOT_FOUND', message);
  }
}

export class ValidationError extends ApiError {
  constructor(message: string, public errors: any[]) {
    super(400, 'VALIDATION_ERROR', message);
  }
}
```

## Development Workflow

### Step-by-Step Process
1. **Define API Contract**: Create interface in `src/shared/api/`
2. **Auto-Generation**: ApiGeneratorWorker creates client/server code
3. **Implement Handler**: Write server-side logic with full type safety
4. **Use in Client**: Import generated API client with autocomplete
5. **Hot Reload**: Changes trigger regeneration and reload

### Example Development Flow
```typescript
// 1. Define new endpoint
// src/shared/api/posts.ts
export interface PostEndpoints extends ApiContract {
  '/api/posts/:id/like': {
    POST: {
      params: { id: string };
      response: { likes: number };
    };
  };
}

// 2. Auto-generated client (immediate)
// build/client/api/generated.ts
api.posts.like(postId) // Full type safety and autocomplete

// 3. Implement server handler
postRouter.post('/api/posts/:id/like', async (req) => {
  const likes = await postService.addLike(req.params.id);
  return { likes }; // Type-checked return value
});
```

## Implementation Phases

### Phase 1: Basic Infrastructure (Week 1-2)
- [ ] Create ApiContract type system
- [ ] Implement basic TypedRouter for server
- [ ] Create manual client utilities
- [ ] Add to existing watch/build process

### Phase 2: Code Generation (Week 3-4)
- [ ] Implement ApiGeneratorWorker
- [ ] Parse TypeScript AST for API contracts
- [ ] Generate client-side API utilities
- [ ] Generate server-side type helpers

### Phase 3: Error Handling & Logging (Week 5)
- [ ] Create standardized error handling system
- [ ] Add request/response logging
- [ ] Implement error type definitions
- [ ] Add debugging utilities

### Phase 4: Documentation & Testing (Week 6)
- [ ] Generate OpenAPI documentation
- [ ] Create testing utilities
- [ ] Add request/response transformers
- [ ] Write comprehensive documentation

### Phase 5: Polish & Optimization (Week 7)
- [ ] Optimize generation performance
- [ ] Add configuration options
- [ ] Create migration guide
- [ ] Finalize API design

### Future Enhancements
- [ ] Runtime validation with type guards
- [ ] Authentication/authorization types
- [ ] Advanced middleware system
- [ ] Response caching strategies

## Configuration

No configuration needed! The API route generation follows webstir's philosophy of smart defaults:

- **Auto-detection**: If `src/shared/api/` exists, route generation is enabled
- **Fixed output paths**: 
  - Client: `build/client/api/generated.ts`
  - Server: `build/server/api/generated.ts`
- **Convention over configuration**: Standard locations and naming patterns
- **Zero config**: Works out of the box with sensible defaults

## Benefits

### Developer Experience
- **Type Safety**: Full end-to-end type checking
- **Autocomplete**: IDE knows all available endpoints
- **Refactoring**: Safe renaming and restructuring
- **Documentation**: Types serve as living documentation

### Code Quality
- **Consistency**: Uniform API structure
- **Type Safety**: Compile-time type checking
- **Error Handling**: Standardized error responses
- **Testing**: Easy to mock and test

### Maintenance
- **Single Source**: One place to update API contracts
- **Breaking Changes**: Caught at compile time
- **Versioning**: Easy to version APIs
- **Migration**: Clear upgrade paths

## Migration Guide

### From Manual Routes
```typescript
// Before: Manual route definition
app.get('/api/users/:id', async (req, res) => {
  const user = await getUser(req.params.id);
  res.json(user);
});

// After: Type-safe route
userRouter.get('/api/users/:id', async (req) => {
  return await getUser(req.params.id); // Return type enforced
});
```

### From Manual Fetch
```typescript
// Before: Manual fetch
const response = await fetch(`/api/users/${id}`);
const user = await response.json();

// After: Generated client
const user = await api.users.get(id); // Type-safe with autocomplete
```

## Future Enhancements

### Planned Features
- GraphQL schema generation
- WebSocket type safety
- Real-time subscriptions
- Batch operations
- File upload progress
- Request cancellation
- Offline support
- Response caching

### Potential Integrations
- tRPC compatibility layer
- OpenAPI import/export
- Postman collection generation
- Integration testing utilities
- Mock server generation
- Client SDK for other languages

## Conclusion

API route generation from TypeScript types represents a natural evolution of webstir's type-first approach. By leveraging TypeScript's powerful type system, we can eliminate boilerplate, prevent errors, and create a delightful developer experience while maintaining the simplicity and transparency that makes webstir unique.