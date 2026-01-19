import { Hono } from 'hono';
import { authMiddleware } from '../middleware/auth';
import { generateId } from '../utils/jwt';

interface Env {
  DB: D1Database;
  SESSIONS: KVNamespace;
  JWT_SECRET: string;
}

export const chatRoutes = new Hono<{ Bindings: Env }>();

// Tüm chat route'ları auth gerektiriyor
chatRoutes.use('*', authMiddleware);

// Küfür filtresi (basit liste)
const badWords = [
  // Türkçe küfürler buraya eklenecek
  // Gerçek uygulamada daha kapsamlı bir liste kullanılmalı
];

function filterBadWords(text: string): string {
  let filtered = text;
  badWords.forEach(word => {
    const regex = new RegExp(word, 'gi');
    filtered = filtered.replace(regex, '*'.repeat(word.length));
  });
  return filtered;
}

// ==========================================
// GET /chat/conversations - Konuşma listesi
// ==========================================
chatRoutes.get('/conversations', async (c) => {
  const user = c.get('user');

  const conversations = await c.env.DB.prepare(`
    SELECT
      CASE
        WHEN sender_id = ? THEN receiver_id
        ELSE sender_id
      END as other_user_id,
      MAX(created_at) as last_message_time,
      (SELECT message FROM messages m2
       WHERE (m2.sender_id = ? OR m2.receiver_id = ?)
       AND (m2.sender_id = other_user_id OR m2.receiver_id = other_user_id)
       ORDER BY created_at DESC LIMIT 1) as last_message,
      SUM(CASE WHEN receiver_id = ? AND is_read = 0 THEN 1 ELSE 0 END) as unread_count
    FROM messages
    WHERE sender_id = ? OR receiver_id = ?
    GROUP BY other_user_id
    ORDER BY last_message_time DESC
    LIMIT 50
  `).bind(
    user.userId, user.userId, user.userId,
    user.userId, user.userId, user.userId
  ).all<any>();

  // Kullanıcı bilgilerini al
  const userIds = conversations.results?.map(c => c.other_user_id) || [];
  if (userIds.length === 0) {
    return c.json({ success: true, data: [] });
  }

  const users = await c.env.DB.prepare(`
    SELECT id, username, display_name, avatar_url, level
    FROM users WHERE id IN (${userIds.map(() => '?').join(',')})
  `).bind(...userIds).all<any>();

  const userMap = new Map(users.results?.map(u => [u.id, u]) || []);

  const enrichedConversations = conversations.results?.map(conv => ({
    ...conv,
    user: userMap.get(conv.other_user_id)
  }));

  return c.json({ success: true, data: enrichedConversations });
});

// ==========================================
// GET /chat/:userId - Belirli kullanıcıyla mesajlar
// ==========================================
chatRoutes.get('/:userId', async (c) => {
  const user = c.get('user');
  const otherUserId = c.req.param('userId');
  const offset = parseInt(c.req.query('offset') || '0');
  const limit = parseInt(c.req.query('limit') || '50');

  const messages = await c.env.DB.prepare(`
    SELECT id, sender_id, receiver_id, message, is_read, created_at
    FROM messages
    WHERE (sender_id = ? AND receiver_id = ?)
       OR (sender_id = ? AND receiver_id = ?)
    ORDER BY created_at DESC
    LIMIT ? OFFSET ?
  `).bind(
    user.userId, otherUserId,
    otherUserId, user.userId,
    limit, offset
  ).all<any>();

  // Okunmamışları okundu olarak işaretle
  await c.env.DB.prepare(`
    UPDATE messages SET is_read = 1
    WHERE sender_id = ? AND receiver_id = ? AND is_read = 0
  `).bind(otherUserId, user.userId).run();

  return c.json({
    success: true,
    data: messages.results?.reverse() // Kronolojik sıra
  });
});

// ==========================================
// POST /chat/:userId - Mesaj gönder
// ==========================================
chatRoutes.post('/:userId', async (c) => {
  const user = c.get('user');
  const receiverId = c.req.param('userId');
  const body = await c.req.json();
  let { message } = body;

  if (!message || message.trim().length === 0) {
    return c.json({ success: false, error: 'Mesaj boş olamaz' }, 400);
  }

  if (message.length > 1000) {
    return c.json({ success: false, error: 'Mesaj çok uzun (max 1000 karakter)' }, 400);
  }

  // Kendine mesaj gönderemez
  if (receiverId === user.userId) {
    return c.json({ success: false, error: 'Kendinize mesaj gönderemezsiniz' }, 400);
  }

  // Alıcı var mı?
  const receiver = await c.env.DB.prepare(
    'SELECT id FROM users WHERE id = ?'
  ).bind(receiverId).first();

  if (!receiver) {
    return c.json({ success: false, error: 'Kullanıcı bulunamadı' }, 404);
  }

  // Engelli mi? (arkadaşlık tablosunda blocked status)
  const blocked = await c.env.DB.prepare(`
    SELECT id FROM friendships
    WHERE ((user_id = ? AND friend_id = ?) OR (user_id = ? AND friend_id = ?))
    AND status = 'blocked'
  `).bind(user.userId, receiverId, receiverId, user.userId).first();

  if (blocked) {
    return c.json({ success: false, error: 'Bu kullanıcıya mesaj gönderemezsiniz' }, 403);
  }

  // Küfür filtrele
  message = filterBadWords(message.trim());

  const messageId = generateId();

  await c.env.DB.prepare(`
    INSERT INTO messages (id, sender_id, receiver_id, message, created_at)
    VALUES (?, ?, ?, ?, datetime('now'))
  `).bind(messageId, user.userId, receiverId, message).run();

  return c.json({
    success: true,
    data: {
      id: messageId,
      senderId: user.userId,
      receiverId,
      message,
      createdAt: new Date().toISOString()
    }
  });
});

// ==========================================
// DELETE /chat/:userId/:messageId - Mesaj sil
// ==========================================
chatRoutes.delete('/:userId/:messageId', async (c) => {
  const user = c.get('user');
  const messageId = c.req.param('messageId');

  // Sadece kendi mesajını silebilir
  const result = await c.env.DB.prepare(
    'DELETE FROM messages WHERE id = ? AND sender_id = ?'
  ).bind(messageId, user.userId).run();

  if (result.meta.changes === 0) {
    return c.json({ success: false, error: 'Mesaj bulunamadı veya silme yetkiniz yok' }, 404);
  }

  return c.json({ success: true, message: 'Mesaj silindi' });
});

// ==========================================
// GET /chat/unread/count - Okunmamış mesaj sayısı
// ==========================================
chatRoutes.get('/unread/count', async (c) => {
  const user = c.get('user');

  const result = await c.env.DB.prepare(`
    SELECT COUNT(*) as count FROM messages
    WHERE receiver_id = ? AND is_read = 0
  `).bind(user.userId).first<any>();

  return c.json({
    success: true,
    data: { unreadCount: result?.count || 0 }
  });
});
