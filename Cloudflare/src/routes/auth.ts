import { Hono } from 'hono';
import { hashPassword, verifyPassword, validatePasswordStrength } from '../utils/password';
import { createToken, createRefreshToken, verifyToken, generateId, generateSecureToken } from '../utils/jwt';
import { authMiddleware, checkRateLimit } from '../middleware/auth';

interface Env {
  DB: D1Database;
  SESSIONS: KVNamespace;
  JWT_SECRET: string;
  TURNSTILE_SECRET: string;
}

export const authRoutes = new Hono<{ Bindings: Env }>();

// ==========================================
// POST /auth/register - Yeni kullanıcı kaydı
// ==========================================
authRoutes.post('/register', async (c) => {
  try {
    const body = await c.req.json();
    const { email, username, password, displayName, turnstileToken } = body;

    // Validasyonlar
    if (!email || !username || !password) {
      return c.json({ success: false, error: 'E-posta, kullanıcı adı ve şifre gereklidir' }, 400);
    }

    // E-posta formatı kontrolü
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(email)) {
      return c.json({ success: false, error: 'Geçersiz e-posta formatı' }, 400);
    }

    // Kullanıcı adı kontrolü
    const usernameRegex = /^[a-zA-Z0-9_]{3,20}$/;
    if (!usernameRegex.test(username)) {
      return c.json({
        success: false,
        error: 'Kullanıcı adı 3-20 karakter olmalı ve sadece harf, rakam, alt çizgi içermelidir'
      }, 400);
    }

    // Şifre güçlülük kontrolü
    const passwordCheck = validatePasswordStrength(password);
    if (!passwordCheck.valid) {
      return c.json({ success: false, error: passwordCheck.message }, 400);
    }

    // Turnstile doğrulama (bot koruması)
    if (turnstileToken && c.env.TURNSTILE_SECRET) {
      const turnstileValid = await verifyTurnstile(turnstileToken, c.env.TURNSTILE_SECRET);
      if (!turnstileValid) {
        return c.json({ success: false, error: 'Bot doğrulaması başarısız' }, 400);
      }
    }

    // E-posta veya kullanıcı adı zaten var mı?
    const existing = await c.env.DB.prepare(
      'SELECT id FROM users WHERE email = ? OR username = ?'
    ).bind(email.toLowerCase(), username.toLowerCase()).first();

    if (existing) {
      return c.json({ success: false, error: 'Bu e-posta veya kullanıcı adı zaten kullanılıyor' }, 409);
    }

    // Şifreyi hashle
    const passwordHash = await hashPassword(password);

    // Kullanıcı oluştur
    const userId = generateId();
    const finalDisplayName = displayName || username;

    await c.env.DB.prepare(`
      INSERT INTO users (id, email, username, password_hash, display_name, created_at)
      VALUES (?, ?, ?, ?, ?, datetime('now'))
    `).bind(userId, email.toLowerCase(), username.toLowerCase(), passwordHash, finalDisplayName).run();

    // Başlangıç jokerleri ekle
    await c.env.DB.batch([
      c.env.DB.prepare('INSERT INTO user_jokers (user_id, joker_type, count) VALUES (?, ?, ?)').bind(userId, 'Yuzde50', 3),
      c.env.DB.prepare('INSERT INTO user_jokers (user_id, joker_type, count) VALUES (?, ?, ?)').bind(userId, 'OyuncularaSor', 3),
      c.env.DB.prepare('INSERT INTO user_jokers (user_id, joker_type, count) VALUES (?, ?, ?)').bind(userId, 'Papagan', 2),
      c.env.DB.prepare('INSERT INTO user_jokers (user_id, joker_type, count) VALUES (?, ?, ?)').bind(userId, 'Teleskop', 2),
    ]);

    // Token oluştur
    const token = await createToken(
      { userId, email: email.toLowerCase(), username: username.toLowerCase() },
      c.env.JWT_SECRET
    );
    const refreshToken = await createRefreshToken(userId, c.env.JWT_SECRET);

    // Session kaydet
    const sessionId = generateId();
    await c.env.SESSIONS.put(`session:${sessionId}`, JSON.stringify({
      userId,
      refreshToken,
      createdAt: Date.now()
    }), { expirationTtl: 30 * 24 * 60 * 60 }); // 30 gün

    // E-posta doğrulama token'ı oluştur
    const verificationToken = generateSecureToken();
    await c.env.DB.prepare(`
      INSERT INTO email_verifications (user_id, token, expires_at)
      VALUES (?, ?, datetime('now', '+24 hours'))
    `).bind(userId, verificationToken).run();

    // TODO: E-posta gönder (Cloudflare Email Workers veya harici servis)

    return c.json({
      success: true,
      message: 'Kayıt başarılı! E-posta adresinizi doğrulayın.',
      data: {
        userId,
        username: username.toLowerCase(),
        displayName: finalDisplayName,
        token,
        refreshToken,
        sessionId
      }
    });

  } catch (error) {
    console.error('Register error:', error);
    return c.json({ success: false, error: 'Kayıt işlemi başarısız' }, 500);
  }
});

// ==========================================
// POST /auth/login - Giriş yap
// ==========================================
authRoutes.post('/login', async (c) => {
  try {
    const body = await c.req.json();
    const { identifier, password, turnstileToken } = body; // identifier = email veya username

    if (!identifier || !password) {
      return c.json({ success: false, error: 'E-posta/kullanıcı adı ve şifre gereklidir' }, 400);
    }

    // Rate limiting kontrolü
    const clientIP = c.req.header('CF-Connecting-IP') || 'unknown';
    const rateLimit = await checkRateLimit(c.env.SESSIONS, `login:${clientIP}`, 10, 60);
    if (!rateLimit.allowed) {
      return c.json({
        success: false,
        error: `Çok fazla deneme. ${rateLimit.resetIn} saniye sonra tekrar deneyin.`
      }, 429);
    }

    // Turnstile doğrulama
    if (turnstileToken && c.env.TURNSTILE_SECRET) {
      const turnstileValid = await verifyTurnstile(turnstileToken, c.env.TURNSTILE_SECRET);
      if (!turnstileValid) {
        return c.json({ success: false, error: 'Bot doğrulaması başarısız' }, 400);
      }
    }

    // Kullanıcıyı bul
    const user = await c.env.DB.prepare(`
      SELECT id, email, username, password_hash, display_name, avatar_url,
             level, total_tp, weekly_tp, gold_coins, game_rights,
             email_verified, is_banned, ban_reason
      FROM users
      WHERE email = ? OR username = ?
    `).bind(identifier.toLowerCase(), identifier.toLowerCase()).first<any>();

    if (!user) {
      return c.json({ success: false, error: 'Kullanıcı bulunamadı' }, 401);
    }

    // Yasaklı mı?
    if (user.is_banned) {
      return c.json({
        success: false,
        error: `Hesabınız yasaklandı. Sebep: ${user.ban_reason || 'Belirtilmedi'}`
      }, 403);
    }

    // Şifre doğrulama
    const passwordValid = await verifyPassword(password, user.password_hash);
    if (!passwordValid) {
      return c.json({ success: false, error: 'Yanlış şifre' }, 401);
    }

    // Son giriş zamanını güncelle
    await c.env.DB.prepare(
      "UPDATE users SET last_login = datetime('now') WHERE id = ?"
    ).bind(user.id).run();

    // Token oluştur
    const token = await createToken(
      { userId: user.id, email: user.email, username: user.username },
      c.env.JWT_SECRET
    );
    const refreshToken = await createRefreshToken(user.id, c.env.JWT_SECRET);

    // Session kaydet
    const sessionId = generateId();
    await c.env.SESSIONS.put(`session:${sessionId}`, JSON.stringify({
      userId: user.id,
      refreshToken,
      createdAt: Date.now()
    }), { expirationTtl: 30 * 24 * 60 * 60 });

    // Jokerleri al
    const jokers = await c.env.DB.prepare(
      'SELECT joker_type, count FROM user_jokers WHERE user_id = ?'
    ).bind(user.id).all<{ joker_type: string; count: number }>();

    const jokerCounts: Record<string, number> = {};
    jokers.results?.forEach(j => {
      jokerCounts[j.joker_type] = j.count;
    });

    return c.json({
      success: true,
      message: 'Giriş başarılı!',
      data: {
        userId: user.id,
        email: user.email,
        username: user.username,
        displayName: user.display_name,
        avatarUrl: user.avatar_url,
        level: user.level,
        totalTP: user.total_tp,
        weeklyTP: user.weekly_tp,
        goldCoins: user.gold_coins,
        gameRights: user.game_rights,
        emailVerified: user.email_verified === 1,
        jokerCounts,
        token,
        refreshToken,
        sessionId
      }
    });

  } catch (error) {
    console.error('Login error:', error);
    return c.json({ success: false, error: 'Giriş işlemi başarısız' }, 500);
  }
});

// ==========================================
// POST /auth/logout - Çıkış yap
// ==========================================
authRoutes.post('/logout', authMiddleware, async (c) => {
  try {
    const body = await c.req.json();
    const { sessionId } = body;

    if (sessionId) {
      await c.env.SESSIONS.delete(`session:${sessionId}`);
    }

    return c.json({ success: true, message: 'Çıkış başarılı' });

  } catch (error) {
    return c.json({ success: false, error: 'Çıkış işlemi başarısız' }, 500);
  }
});

// ==========================================
// POST /auth/refresh - Token yenile
// ==========================================
authRoutes.post('/refresh', async (c) => {
  try {
    const body = await c.req.json();
    const { refreshToken, sessionId } = body;

    if (!refreshToken || !sessionId) {
      return c.json({ success: false, error: 'Refresh token ve session ID gereklidir' }, 400);
    }

    // Session kontrolü
    const sessionData = await c.env.SESSIONS.get(`session:${sessionId}`);
    if (!sessionData) {
      return c.json({ success: false, error: 'Geçersiz session' }, 401);
    }

    const session = JSON.parse(sessionData);
    if (session.refreshToken !== refreshToken) {
      return c.json({ success: false, error: 'Geçersiz refresh token' }, 401);
    }

    // Token doğrula
    const payload = await verifyToken(refreshToken, c.env.JWT_SECRET);
    if (!payload || payload.userId !== session.userId) {
      await c.env.SESSIONS.delete(`session:${sessionId}`);
      return c.json({ success: false, error: 'Token süresi dolmuş, tekrar giriş yapın' }, 401);
    }

    // Kullanıcı bilgilerini al
    const user = await c.env.DB.prepare(
      'SELECT id, email, username FROM users WHERE id = ?'
    ).bind(session.userId).first<any>();

    if (!user) {
      return c.json({ success: false, error: 'Kullanıcı bulunamadı' }, 401);
    }

    // Yeni token oluştur
    const newToken = await createToken(
      { userId: user.id, email: user.email, username: user.username },
      c.env.JWT_SECRET
    );

    return c.json({
      success: true,
      data: { token: newToken }
    });

  } catch (error) {
    return c.json({ success: false, error: 'Token yenileme başarısız' }, 500);
  }
});

// ==========================================
// POST /auth/forgot-password - Şifre sıfırlama isteği
// ==========================================
authRoutes.post('/forgot-password', async (c) => {
  try {
    const body = await c.req.json();
    const { email } = body;

    if (!email) {
      return c.json({ success: false, error: 'E-posta adresi gereklidir' }, 400);
    }

    const user = await c.env.DB.prepare(
      'SELECT id FROM users WHERE email = ?'
    ).bind(email.toLowerCase()).first<any>();

    // Güvenlik için her zaman başarılı dön
    if (!user) {
      return c.json({
        success: true,
        message: 'Eğer bu e-posta kayıtlıysa, şifre sıfırlama bağlantısı gönderildi.'
      });
    }

    // Sıfırlama token'ı oluştur
    const resetToken = generateSecureToken();
    await c.env.DB.prepare(`
      INSERT INTO password_resets (user_id, token, expires_at)
      VALUES (?, ?, datetime('now', '+1 hour'))
    `).bind(user.id, resetToken).run();

    // TODO: E-posta gönder

    return c.json({
      success: true,
      message: 'Eğer bu e-posta kayıtlıysa, şifre sıfırlama bağlantısı gönderildi.'
    });

  } catch (error) {
    return c.json({ success: false, error: 'İşlem başarısız' }, 500);
  }
});

// ==========================================
// POST /auth/reset-password - Şifre sıfırla
// ==========================================
authRoutes.post('/reset-password', async (c) => {
  try {
    const body = await c.req.json();
    const { token, newPassword } = body;

    if (!token || !newPassword) {
      return c.json({ success: false, error: 'Token ve yeni şifre gereklidir' }, 400);
    }

    // Şifre güçlülük kontrolü
    const passwordCheck = validatePasswordStrength(newPassword);
    if (!passwordCheck.valid) {
      return c.json({ success: false, error: passwordCheck.message }, 400);
    }

    // Token kontrolü
    const resetData = await c.env.DB.prepare(`
      SELECT user_id FROM password_resets
      WHERE token = ? AND used = 0 AND expires_at > datetime('now')
    `).bind(token).first<any>();

    if (!resetData) {
      return c.json({ success: false, error: 'Geçersiz veya süresi dolmuş token' }, 400);
    }

    // Şifreyi güncelle
    const passwordHash = await hashPassword(newPassword);
    await c.env.DB.batch([
      c.env.DB.prepare('UPDATE users SET password_hash = ? WHERE id = ?').bind(passwordHash, resetData.user_id),
      c.env.DB.prepare('UPDATE password_resets SET used = 1 WHERE token = ?').bind(token)
    ]);

    return c.json({ success: true, message: 'Şifreniz başarıyla güncellendi' });

  } catch (error) {
    return c.json({ success: false, error: 'Şifre sıfırlama başarısız' }, 500);
  }
});

// ==========================================
// GET /auth/verify-email - E-posta doğrula
// ==========================================
authRoutes.get('/verify-email', async (c) => {
  try {
    const token = c.req.query('token');

    if (!token) {
      return c.json({ success: false, error: 'Token gereklidir' }, 400);
    }

    const verification = await c.env.DB.prepare(`
      SELECT user_id FROM email_verifications
      WHERE token = ? AND expires_at > datetime('now')
    `).bind(token).first<any>();

    if (!verification) {
      return c.json({ success: false, error: 'Geçersiz veya süresi dolmuş token' }, 400);
    }

    await c.env.DB.batch([
      c.env.DB.prepare('UPDATE users SET email_verified = 1 WHERE id = ?').bind(verification.user_id),
      c.env.DB.prepare('DELETE FROM email_verifications WHERE token = ?').bind(token)
    ]);

    return c.json({ success: true, message: 'E-posta adresiniz doğrulandı!' });

  } catch (error) {
    return c.json({ success: false, error: 'Doğrulama başarısız' }, 500);
  }
});

// ==========================================
// GET /auth/me - Mevcut kullanıcı bilgisi
// ==========================================
authRoutes.get('/me', authMiddleware, async (c) => {
  try {
    const user = c.get('user');

    const userData = await c.env.DB.prepare(`
      SELECT id, email, username, display_name, avatar_url,
             level, total_tp, weekly_tp, gold_coins, game_rights,
             current_win_streak, max_win_streak,
             total_games_played, total_wins, total_correct_answers,
             email_verified, created_at, last_login
      FROM users WHERE id = ?
    `).bind(user.userId).first<any>();

    if (!userData) {
      return c.json({ success: false, error: 'Kullanıcı bulunamadı' }, 404);
    }

    // Jokerleri al
    const jokers = await c.env.DB.prepare(
      'SELECT joker_type, count FROM user_jokers WHERE user_id = ?'
    ).bind(user.userId).all<{ joker_type: string; count: number }>();

    const jokerCounts: Record<string, number> = {};
    jokers.results?.forEach(j => {
      jokerCounts[j.joker_type] = j.count;
    });

    return c.json({
      success: true,
      data: {
        ...userData,
        jokerCounts,
        emailVerified: userData.email_verified === 1
      }
    });

  } catch (error) {
    return c.json({ success: false, error: 'Kullanıcı bilgisi alınamadı' }, 500);
  }
});

// ==========================================
// Turnstile doğrulama helper
// ==========================================
async function verifyTurnstile(token: string, secret: string): Promise<boolean> {
  try {
    const response = await fetch('https://challenges.cloudflare.com/turnstile/v0/siteverify', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ secret, response: token })
    });
    const data = await response.json() as { success: boolean };
    return data.success;
  } catch {
    return false;
  }
}
