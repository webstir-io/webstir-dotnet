import crypto from 'node:crypto';
import type http from 'node:http';

import type { AuthSecrets } from '../env.js';

export interface AuthContext {
  source: 'jwt' | 'service-token';
  token: string;
  userId?: string;
  email?: string;
  name?: string;
  scopes: readonly string[];
  roles: readonly string[];
  claims: Record<string, unknown>;
}

export function resolveRequestAuth(
  req: http.IncomingMessage,
  secrets: AuthSecrets,
  logger?: { warn?(message: string, metadata?: Record<string, unknown>): void }
): AuthContext | undefined {
  const bearer = getHeader(req, 'authorization');
  if (bearer?.startsWith('Bearer ')) {
    if (!secrets.jwtSecret) {
      logger?.warn?.('Authorization header provided but AUTH_JWT_SECRET is not configured.');
    } else {
      const token = bearer.slice(7).trim();
      const context = verifyJwtToken(token, secrets);
      if (context) {
        return context;
      }
      logger?.warn?.('Bearer token validation failed', { reason: 'invalid_token' });
    }
  }

  const serviceToken = getHeader(req, 'x-service-token') ?? getHeader(req, 'x-api-key');
  if (serviceToken && secrets.serviceTokens.length > 0) {
    if (secrets.serviceTokens.includes(serviceToken)) {
      return {
        source: 'service-token',
        token: serviceToken,
        scopes: ['service'],
        roles: ['service'],
        claims: {},
        userId: undefined,
        email: undefined,
        name: undefined
      };
    }
    logger?.warn?.('Service token did not match any allowed AUTH_SERVICE_TOKENS entry');
  }

  return undefined;
}

function verifyJwtToken(token: string, secrets: AuthSecrets): AuthContext | undefined {
  if (!secrets.jwtSecret) return undefined;
  const parts = token.split('.');
  if (parts.length !== 3) return undefined;
  const [encodedHeader, encodedPayload, signature] = parts;

  const header = decodeSegment(encodedHeader);
  if (!header || header.alg !== 'HS256') {
    return undefined;
  }

  const payload = decodeSegment(encodedPayload);
  if (!payload) {
    return undefined;
  }

  const signedContent = `${encodedHeader}.${encodedPayload}`;
  const expectedSignature = crypto.createHmac('sha256', secrets.jwtSecret).update(signedContent).digest('base64url');
  if (!timingSafeEqual(signature, expectedSignature)) {
    return undefined;
  }

  if (secrets.jwtIssuer && payload.iss !== secrets.jwtIssuer) {
    return undefined;
  }

  if (secrets.jwtAudience && !audienceMatches(payload.aud, secrets.jwtAudience)) {
    return undefined;
  }

  const scopes = normalizeScopes(payload.scope);
  const roles = normalizeRoles(payload.roles ?? payload.role ?? payload['https://schemas.webstir.dev/roles']);

  return {
    source: 'jwt',
    token,
    userId: typeof payload.sub === 'string' ? payload.sub : undefined,
    email: typeof payload.email === 'string' ? payload.email : undefined,
    name: typeof payload.name === 'string' ? payload.name : undefined,
    scopes,
    roles,
    claims: payload as Record<string, unknown>
  };
}

function decodeSegment(segment: string): Record<string, any> | undefined {
  try {
    const json = Buffer.from(segment, 'base64url').toString('utf8');
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return undefined;
  }
}

function timingSafeEqual(left: string, right: string): boolean {
  const leftBuffer = Buffer.from(left);
  const rightBuffer = Buffer.from(right);
  if (leftBuffer.length !== rightBuffer.length) {
    return false;
  }
  return crypto.timingSafeEqual(leftBuffer, rightBuffer);
}

function audienceMatches(value: unknown, expected: string): boolean {
  if (Array.isArray(value)) {
    return value.includes(expected);
  }
  if (typeof value === 'string') {
    return value === expected;
  }
  return false;
}

function normalizeScopes(value: unknown): string[] {
  if (!value) return [];
  if (Array.isArray(value)) {
    return value.map((scope) => String(scope));
  }
  if (typeof value === 'string') {
    return value.split(' ').map((scope) => scope.trim()).filter((scope) => scope.length > 0);
  }
  return [];
}

function normalizeRoles(value: unknown): string[] {
  if (!value) return [];
  if (Array.isArray(value)) {
    return value.map((role) => String(role));
  }
  if (typeof value === 'string') {
    return value.split(',').map((role) => role.trim()).filter((role) => role.length > 0);
  }
  return [];
}

function getHeader(req: http.IncomingMessage, name: string): string | undefined {
  const value = req.headers[name] ?? req.headers[name.toLowerCase()];
  if (typeof value === 'string') {
    return value;
  }
  if (Array.isArray(value)) {
    return value[0];
  }
  return undefined;
}
