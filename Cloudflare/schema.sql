-- =============================================
-- BilVeFethet Veritabanı Şeması
-- Cloudflare D1 (SQLite)
-- =============================================

-- Kullanıcılar Tablosu
CREATE TABLE IF NOT EXISTS users (
    id TEXT PRIMARY KEY,
    email TEXT UNIQUE NOT NULL,
    username TEXT UNIQUE NOT NULL,
    password_hash TEXT NOT NULL,
    display_name TEXT NOT NULL,
    avatar_url TEXT DEFAULT '',

    -- Oyun İstatistikleri
    level INTEGER DEFAULT 1,
    total_tp INTEGER DEFAULT 0,
    weekly_tp INTEGER DEFAULT 0,
    gold_coins INTEGER DEFAULT 100,
    game_rights INTEGER DEFAULT 5,

    -- Seri Bilgileri
    current_win_streak INTEGER DEFAULT 0,
    current_undefeated_streak INTEGER DEFAULT 0,
    max_win_streak INTEGER DEFAULT 0,
    max_undefeated_streak INTEGER DEFAULT 0,

    -- Oyun İstatistikleri
    total_games_played INTEGER DEFAULT 0,
    total_wins INTEGER DEFAULT 0,
    total_correct_answers INTEGER DEFAULT 0,
    total_wrong_answers INTEGER DEFAULT 0,

    -- Hesap Durumu
    email_verified INTEGER DEFAULT 0,
    is_banned INTEGER DEFAULT 0,
    ban_reason TEXT DEFAULT '',

    -- Zaman Bilgileri
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    last_login DATETIME,
    next_free_game_time DATETIME,

    -- Ayarlar (JSON olarak saklanır)
    settings TEXT DEFAULT '{}'
);

-- Oturumlar Tablosu
CREATE TABLE IF NOT EXISTS sessions (
    id TEXT PRIMARY KEY,
    user_id TEXT NOT NULL,
    token TEXT NOT NULL,
    device_info TEXT DEFAULT '',
    ip_address TEXT DEFAULT '',
    expires_at DATETIME NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- Jokerler Tablosu
CREATE TABLE IF NOT EXISTS user_jokers (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id TEXT NOT NULL,
    joker_type TEXT NOT NULL, -- 'Yuzde50', 'OyuncularaSor', 'Papagan', 'Teleskop'
    count INTEGER DEFAULT 0,
    UNIQUE(user_id, joker_type),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- Muhafızlar Tablosu
CREATE TABLE IF NOT EXISTS user_guardians (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id TEXT NOT NULL,
    guardian_type TEXT NOT NULL, -- 'Guardian1', 'Guardian2', 'Guardian3'
    count INTEGER DEFAULT 0,
    UNIQUE(user_id, guardian_type),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- Arkadaşlık Tablosu
CREATE TABLE IF NOT EXISTS friendships (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id TEXT NOT NULL,
    friend_id TEXT NOT NULL,
    status TEXT DEFAULT 'pending', -- 'pending', 'accepted', 'blocked'
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    accepted_at DATETIME,
    UNIQUE(user_id, friend_id),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (friend_id) REFERENCES users(id) ON DELETE CASCADE
);

-- Mesajlar Tablosu
CREATE TABLE IF NOT EXISTS messages (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sender_id TEXT NOT NULL,
    receiver_id TEXT NOT NULL,
    message TEXT NOT NULL,
    is_read INTEGER DEFAULT 0,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (sender_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (receiver_id) REFERENCES users(id) ON DELETE CASCADE
);

-- Sorular Tablosu
CREATE TABLE IF NOT EXISTS questions (
    id TEXT PRIMARY KEY,
    question_text TEXT NOT NULL,
    question_type INTEGER DEFAULT 0, -- 0: CoktanSecmeli, 1: Tahmin
    category TEXT NOT NULL,
    difficulty_level INTEGER DEFAULT 5,

    -- Çoktan Seçmeli için
    options TEXT DEFAULT '[]', -- JSON array
    correct_answer_index INTEGER DEFAULT 0,

    -- Tahmin için
    correct_value REAL DEFAULT 0,
    tolerance_percent REAL DEFAULT 10,
    value_unit TEXT DEFAULT '',

    -- Meta
    time_limit INTEGER DEFAULT 30,
    is_active INTEGER DEFAULT 1,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Sıralama Tablosu (Haftalık/Aylık cache)
CREATE TABLE IF NOT EXISTS leaderboard_cache (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id TEXT NOT NULL,
    username TEXT NOT NULL,
    display_name TEXT NOT NULL,
    avatar_url TEXT DEFAULT '',
    score INTEGER NOT NULL,
    rank INTEGER NOT NULL,
    leaderboard_type TEXT NOT NULL, -- 'weekly', 'monthly', 'all_time'
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- Bildirimler Tablosu
CREATE TABLE IF NOT EXISTS notifications (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id TEXT NOT NULL,
    type TEXT NOT NULL, -- 'friend_request', 'game_invite', 'reward', 'system'
    title TEXT NOT NULL,
    message TEXT NOT NULL,
    data TEXT DEFAULT '{}', -- JSON
    is_read INTEGER DEFAULT 0,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- E-posta Doğrulama Tablosu
CREATE TABLE IF NOT EXISTS email_verifications (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id TEXT NOT NULL,
    token TEXT UNIQUE NOT NULL,
    expires_at DATETIME NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- Şifre Sıfırlama Tablosu
CREATE TABLE IF NOT EXISTS password_resets (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id TEXT NOT NULL,
    token TEXT UNIQUE NOT NULL,
    expires_at DATETIME NOT NULL,
    used INTEGER DEFAULT 0,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- Oyun Geçmişi Tablosu
CREATE TABLE IF NOT EXISTS game_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id TEXT NOT NULL,
    game_id TEXT NOT NULL,
    game_mode TEXT NOT NULL, -- 'single_player', 'quick_match', 'custom_lobby'
    final_rank INTEGER NOT NULL,
    final_score INTEGER NOT NULL,
    correct_answers INTEGER NOT NULL,
    wrong_answers INTEGER NOT NULL,
    tp_earned INTEGER NOT NULL,
    played_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- İndeksler
CREATE INDEX IF NOT EXISTS idx_users_email ON users(email);
CREATE INDEX IF NOT EXISTS idx_users_username ON users(username);
CREATE INDEX IF NOT EXISTS idx_sessions_user_id ON sessions(user_id);
CREATE INDEX IF NOT EXISTS idx_sessions_token ON sessions(token);
CREATE INDEX IF NOT EXISTS idx_messages_sender ON messages(sender_id);
CREATE INDEX IF NOT EXISTS idx_messages_receiver ON messages(receiver_id);
CREATE INDEX IF NOT EXISTS idx_friendships_user ON friendships(user_id);
CREATE INDEX IF NOT EXISTS idx_questions_category ON questions(category);
CREATE INDEX IF NOT EXISTS idx_leaderboard_type ON leaderboard_cache(leaderboard_type);
CREATE INDEX IF NOT EXISTS idx_notifications_user ON notifications(user_id);
CREATE INDEX IF NOT EXISTS idx_game_history_user ON game_history(user_id);
