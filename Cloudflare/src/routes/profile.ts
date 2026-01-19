import { Hono } from 'hono';
import { authMiddleware } from '../middleware/auth';

interface Env {
  DB: D1Database;
  SESSIONS: KVNamespace;
  JWT_SECRET: string;
}

export const profileRoutes = new Hono<{ Bindings: Env }>();

// Tüm profile route'ları auth gerektiriyor
profileRoutes.use('*', authMiddleware);

// ==========================================
// GET /profile - Kendi profilini al
// ==========================================
profileRoutes.get('/', async (c) => {
  const user = c.get('user');

  const profile = await c.env.DB.prepare(`
    SELECT
      id, email, username, display_name, avatar_url,
      level, total_tp, weekly_tp, gold_coins, game_rights,
      current_win_streak, current_undefeated_streak,
      max_win_streak, max_undefeated_streak,
      total_games_played, total_wins, total_correct_answers, total_wrong_answers,
      email_verified, created_at, last_login, settings
    FROM users WHERE id = ?
  `).bind(user.userId).first<any>();

  if (!profile) {
    return c.json({ success: false, error: 'Profil bulunamadı' }, 404);
  }

  // Jokerleri al
  const jokers = await c.env.DB.prepare(
    'SELECT joker_type, count FROM user_jokers WHERE user_id = ?'
  ).bind(user.userId).all<{ joker_type: string; count: number }>();

  // Muhafızları al
  const guardians = await c.env.DB.prepare(
    'SELECT guardian_type, count FROM user_guardians WHERE user_id = ?'
  ).bind(user.userId).all<{ guardian_type: string; count: number }>();

  const jokerCounts: Record<string, number> = {};
  jokers.results?.forEach(j => jokerCounts[j.joker_type] = j.count);

  const guardianCounts: Record<string, number> = {};
  guardians.results?.forEach(g => guardianCounts[g.guardian_type] = g.count);

  return c.json({
    success: true,
    data: {
      ...profile,
      settings: JSON.parse(profile.settings || '{}'),
      emailVerified: profile.email_verified === 1,
      jokerCounts,
      guardianCounts
    }
  });
});

// ==========================================
// GET /profile/:userId - Başka kullanıcının profilini al
// ==========================================
profileRoutes.get('/:userId', async (c) => {
  const userId = c.req.param('userId');

  const profile = await c.env.DB.prepare(`
    SELECT
      id, username, display_name, avatar_url,
      level, total_tp, weekly_tp,
      max_win_streak, max_undefeated_streak,
      total_games_played, total_wins,
      created_at
    FROM users WHERE id = ?
  `).bind(userId).first<any>();

  if (!profile) {
    return c.json({ success: false, error: 'Profil bulunamadı' }, 404);
  }

  return c.json({ success: true, data: profile });
});

// ==========================================
// PUT /profile - Profili güncelle
// ==========================================
profileRoutes.put('/', async (c) => {
  const user = c.get('user');
  const body = await c.req.json();
  const { displayName, avatarUrl } = body;

  const updates: string[] = [];
  const values: any[] = [];

  if (displayName !== undefined) {
    if (displayName.length < 2 || displayName.length > 30) {
      return c.json({ success: false, error: 'Görüntü adı 2-30 karakter olmalıdır' }, 400);
    }
    updates.push('display_name = ?');
    values.push(displayName);
  }

  if (avatarUrl !== undefined) {
    updates.push('avatar_url = ?');
    values.push(avatarUrl);
  }

  if (updates.length === 0) {
    return c.json({ success: false, error: 'Güncellenecek alan belirtilmedi' }, 400);
  }

  values.push(user.userId);

  await c.env.DB.prepare(`
    UPDATE users SET ${updates.join(', ')} WHERE id = ?
  `).bind(...values).run();

  return c.json({ success: true, message: 'Profil güncellendi' });
});

// ==========================================
// PUT /profile/settings - Ayarları güncelle
// ==========================================
profileRoutes.put('/settings', async (c) => {
  const user = c.get('user');
  const body = await c.req.json();

  // Mevcut ayarları al
  const current = await c.env.DB.prepare(
    'SELECT settings FROM users WHERE id = ?'
  ).bind(user.userId).first<any>();

  const currentSettings = JSON.parse(current?.settings || '{}');
  const newSettings = { ...currentSettings, ...body };

  await c.env.DB.prepare(
    'UPDATE users SET settings = ? WHERE id = ?'
  ).bind(JSON.stringify(newSettings), user.userId).run();

  return c.json({ success: true, data: newSettings });
});

// ==========================================
// POST /profile/game-right - Oyun hakkı kullan
// ==========================================
profileRoutes.post('/game-right/use', async (c) => {
  const user = c.get('user');

  const current = await c.env.DB.prepare(
    'SELECT game_rights FROM users WHERE id = ?'
  ).bind(user.userId).first<any>();

  if (!current || current.game_rights <= 0) {
    return c.json({ success: false, error: 'Oyun hakkınız kalmadı' }, 400);
  }

  await c.env.DB.prepare(
    'UPDATE users SET game_rights = game_rights - 1 WHERE id = ?'
  ).bind(user.userId).run();

  return c.json({
    success: true,
    data: { remainingRights: current.game_rights - 1 }
  });
});

// ==========================================
// POST /profile/joker/use - Joker kullan
// ==========================================
profileRoutes.post('/joker/use', async (c) => {
  const user = c.get('user');
  const body = await c.req.json();
  const { jokerType } = body;

  if (!jokerType) {
    return c.json({ success: false, error: 'Joker tipi belirtilmedi' }, 400);
  }

  const joker = await c.env.DB.prepare(
    'SELECT count FROM user_jokers WHERE user_id = ? AND joker_type = ?'
  ).bind(user.userId, jokerType).first<any>();

  if (!joker || joker.count <= 0) {
    return c.json({ success: false, error: 'Bu joker hakkınız kalmadı' }, 400);
  }

  await c.env.DB.prepare(
    'UPDATE user_jokers SET count = count - 1 WHERE user_id = ? AND joker_type = ?'
  ).bind(user.userId, jokerType).run();

  return c.json({
    success: true,
    data: { jokerType, remainingCount: joker.count - 1 }
  });
});

// ==========================================
// GET /profile/stats - Detaylı istatistikler
// ==========================================
profileRoutes.get('/stats/detailed', async (c) => {
  const user = c.get('user');

  // Temel istatistikler
  const stats = await c.env.DB.prepare(`
    SELECT
      total_games_played, total_wins, total_correct_answers, total_wrong_answers,
      current_win_streak, max_win_streak, current_undefeated_streak, max_undefeated_streak
    FROM users WHERE id = ?
  `).bind(user.userId).first<any>();

  // Son 10 oyun
  const recentGames = await c.env.DB.prepare(`
    SELECT game_mode, final_rank, final_score, correct_answers, wrong_answers, tp_earned, played_at
    FROM game_history
    WHERE user_id = ?
    ORDER BY played_at DESC
    LIMIT 10
  `).bind(user.userId).all<any>();

  // Mod bazlı istatistikler
  const modeStats = await c.env.DB.prepare(`
    SELECT
      game_mode,
      COUNT(*) as games_played,
      SUM(CASE WHEN final_rank = 1 THEN 1 ELSE 0 END) as wins,
      AVG(final_score) as avg_score
    FROM game_history
    WHERE user_id = ?
    GROUP BY game_mode
  `).bind(user.userId).all<any>();

  return c.json({
    success: true,
    data: {
      overview: stats,
      recentGames: recentGames.results,
      modeStats: modeStats.results,
      winRate: stats.total_games_played > 0
        ? ((stats.total_wins / stats.total_games_played) * 100).toFixed(1)
        : 0,
      accuracy: (stats.total_correct_answers + stats.total_wrong_answers) > 0
        ? ((stats.total_correct_answers / (stats.total_correct_answers + stats.total_wrong_answers)) * 100).toFixed(1)
        : 0
    }
  });
});
