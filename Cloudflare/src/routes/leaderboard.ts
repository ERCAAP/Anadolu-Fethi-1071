import { Hono } from 'hono';
import { authMiddleware, optionalAuthMiddleware } from '../middleware/auth';

interface Env {
  DB: D1Database;
  SESSIONS: KVNamespace;
  JWT_SECRET: string;
}

export const leaderboardRoutes = new Hono<{ Bindings: Env }>();

// ==========================================
// GET /leaderboard/weekly - Haftalık sıralama
// ==========================================
leaderboardRoutes.get('/weekly', optionalAuthMiddleware, async (c) => {
  const limit = parseInt(c.req.query('limit') || '100');
  const offset = parseInt(c.req.query('offset') || '0');

  const leaderboard = await c.env.DB.prepare(`
    SELECT
      id, username, display_name, avatar_url, level,
      weekly_tp as score,
      ROW_NUMBER() OVER (ORDER BY weekly_tp DESC) as rank
    FROM users
    WHERE weekly_tp > 0
    ORDER BY weekly_tp DESC
    LIMIT ? OFFSET ?
  `).bind(Math.min(limit, 100), offset).all<any>();

  const user = c.get('user');
  let userRank = null;

  if (user) {
    const rankResult = await c.env.DB.prepare(`
      SELECT rank FROM (
        SELECT id, ROW_NUMBER() OVER (ORDER BY weekly_tp DESC) as rank
        FROM users WHERE weekly_tp > 0
      ) WHERE id = ?
    `).bind(user.userId).first<any>();

    userRank = rankResult?.rank;
  }

  return c.json({
    success: true,
    data: {
      entries: leaderboard.results,
      userRank,
      type: 'weekly'
    }
  });
});

// ==========================================
// GET /leaderboard/monthly - Aylık sıralama
// ==========================================
leaderboardRoutes.get('/monthly', optionalAuthMiddleware, async (c) => {
  const limit = parseInt(c.req.query('limit') || '100');
  const offset = parseInt(c.req.query('offset') || '0');

  // Not: Gerçek uygulamada aylık TP ayrı bir sütunda tutulmalı
  // veya game_history'den hesaplanmalı
  const leaderboard = await c.env.DB.prepare(`
    SELECT
      u.id, u.username, u.display_name, u.avatar_url, u.level,
      COALESCE(SUM(gh.tp_earned), 0) as score,
      ROW_NUMBER() OVER (ORDER BY COALESCE(SUM(gh.tp_earned), 0) DESC) as rank
    FROM users u
    LEFT JOIN game_history gh ON u.id = gh.user_id
      AND gh.played_at >= datetime('now', '-30 days')
    GROUP BY u.id
    HAVING score > 0
    ORDER BY score DESC
    LIMIT ? OFFSET ?
  `).bind(Math.min(limit, 100), offset).all<any>();

  return c.json({
    success: true,
    data: {
      entries: leaderboard.results,
      type: 'monthly'
    }
  });
});

// ==========================================
// GET /leaderboard/all-time - Tüm zamanlar
// ==========================================
leaderboardRoutes.get('/all-time', optionalAuthMiddleware, async (c) => {
  const limit = parseInt(c.req.query('limit') || '100');
  const offset = parseInt(c.req.query('offset') || '0');

  const leaderboard = await c.env.DB.prepare(`
    SELECT
      id, username, display_name, avatar_url, level,
      total_tp as score,
      ROW_NUMBER() OVER (ORDER BY total_tp DESC) as rank
    FROM users
    WHERE total_tp > 0
    ORDER BY total_tp DESC
    LIMIT ? OFFSET ?
  `).bind(Math.min(limit, 100), offset).all<any>();

  return c.json({
    success: true,
    data: {
      entries: leaderboard.results,
      type: 'all_time'
    }
  });
});

// ==========================================
// GET /leaderboard/friends - Arkadaş sıralaması
// ==========================================
leaderboardRoutes.get('/friends', authMiddleware, async (c) => {
  const user = c.get('user');

  const leaderboard = await c.env.DB.prepare(`
    SELECT
      u.id, u.username, u.display_name, u.avatar_url, u.level,
      u.weekly_tp as score
    FROM users u
    WHERE u.id = ?
    OR u.id IN (
      SELECT CASE WHEN f.user_id = ? THEN f.friend_id ELSE f.user_id END
      FROM friendships f
      WHERE (f.user_id = ? OR f.friend_id = ?)
      AND f.status = 'accepted'
    )
    ORDER BY u.weekly_tp DESC
  `).bind(user.userId, user.userId, user.userId, user.userId).all<any>();

  // Sıralama ekle
  const entries = leaderboard.results?.map((entry, index) => ({
    ...entry,
    rank: index + 1,
    isCurrentUser: entry.id === user.userId
  }));

  return c.json({
    success: true,
    data: {
      entries,
      type: 'friends'
    }
  });
});

// ==========================================
// GET /leaderboard/around-me - Etrafımdaki oyuncular
// ==========================================
leaderboardRoutes.get('/around-me', authMiddleware, async (c) => {
  const user = c.get('user');
  const type = c.req.query('type') || 'weekly';
  const range = parseInt(c.req.query('range') || '5');

  const scoreColumn = type === 'weekly' ? 'weekly_tp' : 'total_tp';

  // Önce kullanıcının sırasını bul
  const userRankResult = await c.env.DB.prepare(`
    SELECT rank, score FROM (
      SELECT
        id,
        ${scoreColumn} as score,
        ROW_NUMBER() OVER (ORDER BY ${scoreColumn} DESC) as rank
      FROM users
      WHERE ${scoreColumn} > 0
    ) WHERE id = ?
  `).bind(user.userId).first<any>();

  if (!userRankResult) {
    return c.json({
      success: true,
      data: {
        entries: [],
        userRank: null,
        type
      }
    });
  }

  const userRank = userRankResult.rank;
  const startRank = Math.max(1, userRank - range);
  const endRank = userRank + range;

  const leaderboard = await c.env.DB.prepare(`
    SELECT * FROM (
      SELECT
        id, username, display_name, avatar_url, level,
        ${scoreColumn} as score,
        ROW_NUMBER() OVER (ORDER BY ${scoreColumn} DESC) as rank
      FROM users
      WHERE ${scoreColumn} > 0
    ) WHERE rank BETWEEN ? AND ?
  `).bind(startRank, endRank).all<any>();

  const entries = leaderboard.results?.map(entry => ({
    ...entry,
    isCurrentUser: entry.id === user.userId
  }));

  return c.json({
    success: true,
    data: {
      entries,
      userRank,
      type
    }
  });
});

// ==========================================
// GET /leaderboard/top - En iyi 10
// ==========================================
leaderboardRoutes.get('/top', async (c) => {
  const type = c.req.query('type') || 'weekly';
  const scoreColumn = type === 'weekly' ? 'weekly_tp' : 'total_tp';

  const leaderboard = await c.env.DB.prepare(`
    SELECT
      id, username, display_name, avatar_url, level,
      ${scoreColumn} as score,
      ROW_NUMBER() OVER (ORDER BY ${scoreColumn} DESC) as rank
    FROM users
    WHERE ${scoreColumn} > 0
    ORDER BY ${scoreColumn} DESC
    LIMIT 10
  `).all<any>();

  return c.json({
    success: true,
    data: {
      entries: leaderboard.results,
      type
    }
  });
});
