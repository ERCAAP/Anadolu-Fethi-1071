import { Hono } from 'hono';
import { authMiddleware } from '../middleware/auth';
import { generateId } from '../utils/jwt';

interface Env {
  DB: D1Database;
  SESSIONS: KVNamespace;
  JWT_SECRET: string;
}

export const friendRoutes = new Hono<{ Bindings: Env }>();

friendRoutes.use('*', authMiddleware);

// ==========================================
// GET /friends - Arkadaş listesi
// ==========================================
friendRoutes.get('/', async (c) => {
  const user = c.get('user');

  const friends = await c.env.DB.prepare(`
    SELECT
      u.id, u.username, u.display_name, u.avatar_url, u.level, u.total_tp,
      f.status, f.created_at as friendship_date
    FROM friendships f
    JOIN users u ON (
      CASE WHEN f.user_id = ? THEN f.friend_id ELSE f.user_id END = u.id
    )
    WHERE (f.user_id = ? OR f.friend_id = ?)
    AND f.status = 'accepted'
    ORDER BY u.display_name
  `).bind(user.userId, user.userId, user.userId).all<any>();

  return c.json({ success: true, data: friends.results });
});

// ==========================================
// GET /friends/requests - Arkadaşlık istekleri
// ==========================================
friendRoutes.get('/requests', async (c) => {
  const user = c.get('user');

  // Gelen istekler
  const incoming = await c.env.DB.prepare(`
    SELECT
      u.id, u.username, u.display_name, u.avatar_url, u.level,
      f.id as request_id, f.created_at
    FROM friendships f
    JOIN users u ON f.user_id = u.id
    WHERE f.friend_id = ? AND f.status = 'pending'
    ORDER BY f.created_at DESC
  `).bind(user.userId).all<any>();

  // Giden istekler
  const outgoing = await c.env.DB.prepare(`
    SELECT
      u.id, u.username, u.display_name, u.avatar_url, u.level,
      f.id as request_id, f.created_at
    FROM friendships f
    JOIN users u ON f.friend_id = u.id
    WHERE f.user_id = ? AND f.status = 'pending'
    ORDER BY f.created_at DESC
  `).bind(user.userId).all<any>();

  return c.json({
    success: true,
    data: {
      incoming: incoming.results,
      outgoing: outgoing.results
    }
  });
});

// ==========================================
// POST /friends/request/:userId - Arkadaşlık isteği gönder
// ==========================================
friendRoutes.post('/request/:userId', async (c) => {
  const user = c.get('user');
  const friendId = c.req.param('userId');

  if (friendId === user.userId) {
    return c.json({ success: false, error: 'Kendinize istek gönderemezsiniz' }, 400);
  }

  // Kullanıcı var mı?
  const friend = await c.env.DB.prepare(
    'SELECT id FROM users WHERE id = ?'
  ).bind(friendId).first();

  if (!friend) {
    return c.json({ success: false, error: 'Kullanıcı bulunamadı' }, 404);
  }

  // Zaten arkadaş mı veya istek var mı?
  const existing = await c.env.DB.prepare(`
    SELECT status FROM friendships
    WHERE (user_id = ? AND friend_id = ?) OR (user_id = ? AND friend_id = ?)
  `).bind(user.userId, friendId, friendId, user.userId).first<any>();

  if (existing) {
    if (existing.status === 'accepted') {
      return c.json({ success: false, error: 'Zaten arkadaşsınız' }, 400);
    }
    if (existing.status === 'pending') {
      return c.json({ success: false, error: 'Zaten bekleyen bir istek var' }, 400);
    }
    if (existing.status === 'blocked') {
      return c.json({ success: false, error: 'Bu kullanıcıya istek gönderemezsiniz' }, 403);
    }
  }

  await c.env.DB.prepare(`
    INSERT INTO friendships (user_id, friend_id, status, created_at)
    VALUES (?, ?, 'pending', datetime('now'))
  `).bind(user.userId, friendId).run();

  // Bildirim oluştur
  await c.env.DB.prepare(`
    INSERT INTO notifications (user_id, type, title, message, data, created_at)
    VALUES (?, 'friend_request', 'Arkadaşlık İsteği', ?, ?, datetime('now'))
  `).bind(
    friendId,
    `${user.username} size arkadaşlık isteği gönderdi`,
    JSON.stringify({ fromUserId: user.userId })
  ).run();

  return c.json({ success: true, message: 'Arkadaşlık isteği gönderildi' });
});

// ==========================================
// POST /friends/accept/:userId - İsteği kabul et
// ==========================================
friendRoutes.post('/accept/:userId', async (c) => {
  const user = c.get('user');
  const fromUserId = c.req.param('userId');

  const result = await c.env.DB.prepare(`
    UPDATE friendships
    SET status = 'accepted', accepted_at = datetime('now')
    WHERE user_id = ? AND friend_id = ? AND status = 'pending'
  `).bind(fromUserId, user.userId).run();

  if (result.meta.changes === 0) {
    return c.json({ success: false, error: 'İstek bulunamadı' }, 404);
  }

  // Bildirim oluştur
  await c.env.DB.prepare(`
    INSERT INTO notifications (user_id, type, title, message, data, created_at)
    VALUES (?, 'friend_accepted', 'Arkadaşlık Kabul Edildi', ?, ?, datetime('now'))
  `).bind(
    fromUserId,
    `${user.username} arkadaşlık isteğinizi kabul etti`,
    JSON.stringify({ userId: user.userId })
  ).run();

  return c.json({ success: true, message: 'Arkadaşlık isteği kabul edildi' });
});

// ==========================================
// POST /friends/reject/:userId - İsteği reddet
// ==========================================
friendRoutes.post('/reject/:userId', async (c) => {
  const user = c.get('user');
  const fromUserId = c.req.param('userId');

  const result = await c.env.DB.prepare(`
    DELETE FROM friendships
    WHERE user_id = ? AND friend_id = ? AND status = 'pending'
  `).bind(fromUserId, user.userId).run();

  if (result.meta.changes === 0) {
    return c.json({ success: false, error: 'İstek bulunamadı' }, 404);
  }

  return c.json({ success: true, message: 'Arkadaşlık isteği reddedildi' });
});

// ==========================================
// DELETE /friends/:userId - Arkadaşlıktan çıkar
// ==========================================
friendRoutes.delete('/:userId', async (c) => {
  const user = c.get('user');
  const friendId = c.req.param('userId');

  const result = await c.env.DB.prepare(`
    DELETE FROM friendships
    WHERE ((user_id = ? AND friend_id = ?) OR (user_id = ? AND friend_id = ?))
    AND status = 'accepted'
  `).bind(user.userId, friendId, friendId, user.userId).run();

  if (result.meta.changes === 0) {
    return c.json({ success: false, error: 'Arkadaşlık bulunamadı' }, 404);
  }

  return c.json({ success: true, message: 'Arkadaşlıktan çıkarıldı' });
});

// ==========================================
// POST /friends/block/:userId - Kullanıcıyı engelle
// ==========================================
friendRoutes.post('/block/:userId', async (c) => {
  const user = c.get('user');
  const blockUserId = c.req.param('userId');

  // Mevcut arkadaşlığı sil veya güncelle
  await c.env.DB.prepare(`
    DELETE FROM friendships
    WHERE (user_id = ? AND friend_id = ?) OR (user_id = ? AND friend_id = ?)
  `).bind(user.userId, blockUserId, blockUserId, user.userId).run();

  // Engelleme kaydı ekle
  await c.env.DB.prepare(`
    INSERT INTO friendships (user_id, friend_id, status, created_at)
    VALUES (?, ?, 'blocked', datetime('now'))
  `).bind(user.userId, blockUserId).run();

  return c.json({ success: true, message: 'Kullanıcı engellendi' });
});

// ==========================================
// GET /friends/search - Kullanıcı ara
// ==========================================
friendRoutes.get('/search', async (c) => {
  const user = c.get('user');
  const query = c.req.query('q');

  if (!query || query.length < 2) {
    return c.json({ success: false, error: 'En az 2 karakter girin' }, 400);
  }

  const users = await c.env.DB.prepare(`
    SELECT id, username, display_name, avatar_url, level
    FROM users
    WHERE id != ?
    AND (username LIKE ? OR display_name LIKE ?)
    LIMIT 20
  `).bind(user.userId, `%${query}%`, `%${query}%`).all<any>();

  return c.json({ success: true, data: users.results });
});
