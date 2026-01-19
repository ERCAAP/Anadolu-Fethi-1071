import { Hono } from 'hono';
import { authMiddleware } from '../middleware/auth';

interface Env {
  DB: D1Database;
  SESSIONS: KVNamespace;
  JWT_SECRET: string;
}

export const notificationRoutes = new Hono<{ Bindings: Env }>();

notificationRoutes.use('*', authMiddleware);

// ==========================================
// GET /notifications - Bildirimleri listele
// ==========================================
notificationRoutes.get('/', async (c) => {
  const user = c.get('user');
  const limit = parseInt(c.req.query('limit') || '50');
  const offset = parseInt(c.req.query('offset') || '0');
  const unreadOnly = c.req.query('unread') === 'true';

  let query = `
    SELECT id, type, title, message, data, is_read, created_at
    FROM notifications
    WHERE user_id = ?
  `;

  if (unreadOnly) {
    query += ' AND is_read = 0';
  }

  query += ' ORDER BY created_at DESC LIMIT ? OFFSET ?';

  const notifications = await c.env.DB.prepare(query)
    .bind(user.userId, limit, offset)
    .all<any>();

  // Data JSON'ı parse et
  const parsed = notifications.results?.map(n => ({
    ...n,
    data: JSON.parse(n.data || '{}'),
    isRead: n.is_read === 1
  }));

  return c.json({ success: true, data: parsed });
});

// ==========================================
// GET /notifications/count - Okunmamış sayısı
// ==========================================
notificationRoutes.get('/count', async (c) => {
  const user = c.get('user');

  const result = await c.env.DB.prepare(`
    SELECT COUNT(*) as count FROM notifications
    WHERE user_id = ? AND is_read = 0
  `).bind(user.userId).first<any>();

  return c.json({
    success: true,
    data: { unreadCount: result?.count || 0 }
  });
});

// ==========================================
// POST /notifications/:id/read - Okundu işaretle
// ==========================================
notificationRoutes.post('/:id/read', async (c) => {
  const user = c.get('user');
  const notificationId = c.req.param('id');

  await c.env.DB.prepare(`
    UPDATE notifications SET is_read = 1
    WHERE id = ? AND user_id = ?
  `).bind(notificationId, user.userId).run();

  return c.json({ success: true });
});

// ==========================================
// POST /notifications/read-all - Tümünü okundu işaretle
// ==========================================
notificationRoutes.post('/read-all', async (c) => {
  const user = c.get('user');

  await c.env.DB.prepare(`
    UPDATE notifications SET is_read = 1
    WHERE user_id = ? AND is_read = 0
  `).bind(user.userId).run();

  return c.json({ success: true, message: 'Tüm bildirimler okundu olarak işaretlendi' });
});

// ==========================================
// DELETE /notifications/:id - Bildirimi sil
// ==========================================
notificationRoutes.delete('/:id', async (c) => {
  const user = c.get('user');
  const notificationId = c.req.param('id');

  await c.env.DB.prepare(`
    DELETE FROM notifications WHERE id = ? AND user_id = ?
  `).bind(notificationId, user.userId).run();

  return c.json({ success: true, message: 'Bildirim silindi' });
});

// ==========================================
// DELETE /notifications - Tüm bildirimleri sil
// ==========================================
notificationRoutes.delete('/', async (c) => {
  const user = c.get('user');

  await c.env.DB.prepare(`
    DELETE FROM notifications WHERE user_id = ?
  `).bind(user.userId).run();

  return c.json({ success: true, message: 'Tüm bildirimler silindi' });
});
