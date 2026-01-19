import { Context, Next } from 'hono';
import { verifyToken, extractTokenFromHeader, JWTPayload } from '../utils/jwt';

// Context'e user bilgisi eklemek için type extension
declare module 'hono' {
  interface ContextVariableMap {
    user: JWTPayload;
  }
}

/**
 * Auth middleware - token doğrulama
 */
export async function authMiddleware(c: Context, next: Next) {
  const authHeader = c.req.header('Authorization');
  const token = extractTokenFromHeader(authHeader);

  if (!token) {
    return c.json({ error: 'Yetkilendirme token\'ı bulunamadı' }, 401);
  }

  const payload = await verifyToken(token, c.env.JWT_SECRET);

  if (!payload) {
    return c.json({ error: 'Geçersiz veya süresi dolmuş token' }, 401);
  }

  // User bilgisini context'e ekle
  c.set('user', payload);

  await next();
}

/**
 * Opsiyonel auth middleware - token varsa doğrula, yoksa devam et
 */
export async function optionalAuthMiddleware(c: Context, next: Next) {
  const authHeader = c.req.header('Authorization');
  const token = extractTokenFromHeader(authHeader);

  if (token) {
    const payload = await verifyToken(token, c.env.JWT_SECRET);
    if (payload) {
      c.set('user', payload);
    }
  }

  await next();
}

/**
 * Rate limiting helper (basit implementasyon)
 */
export async function checkRateLimit(
  kv: KVNamespace,
  key: string,
  limit: number,
  windowSeconds: number
): Promise<{ allowed: boolean; remaining: number; resetIn: number }> {
  const now = Math.floor(Date.now() / 1000);
  const windowKey = `ratelimit:${key}:${Math.floor(now / windowSeconds)}`;

  const current = await kv.get(windowKey);
  const count = current ? parseInt(current) : 0;

  if (count >= limit) {
    const resetIn = windowSeconds - (now % windowSeconds);
    return { allowed: false, remaining: 0, resetIn };
  }

  await kv.put(windowKey, (count + 1).toString(), { expirationTtl: windowSeconds });

  return { allowed: true, remaining: limit - count - 1, resetIn: windowSeconds - (now % windowSeconds) };
}
