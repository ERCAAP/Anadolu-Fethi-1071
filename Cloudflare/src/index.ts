import { Hono } from 'hono';
import { cors } from 'hono/cors';
import { authRoutes } from './routes/auth';
import { profileRoutes } from './routes/profile';
import { questionRoutes } from './routes/questions';
import { chatRoutes } from './routes/chat';
import { friendRoutes } from './routes/friends';
import { leaderboardRoutes } from './routes/leaderboard';
import { notificationRoutes } from './routes/notifications';

export interface Env {
  DB: D1Database;
  SESSIONS: KVNamespace;
  JWT_SECRET: string;
  TURNSTILE_SECRET: string;
  ENVIRONMENT: string;
}

const app = new Hono<{ Bindings: Env }>();

// CORS ayarları
app.use('*', cors({
  origin: '*', // Production'da kendi domain'inizi yazın
  allowMethods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'],
  allowHeaders: ['Content-Type', 'Authorization'],
  exposeHeaders: ['Content-Length'],
  maxAge: 86400,
  credentials: true,
}));

// Health check
app.get('/', (c) => {
  return c.json({
    status: 'ok',
    message: 'BilVeFethet API v1.0',
    timestamp: new Date().toISOString()
  });
});

// API Routes
app.route('/auth', authRoutes);
app.route('/profile', profileRoutes);
app.route('/questions', questionRoutes);
app.route('/chat', chatRoutes);
app.route('/friends', friendRoutes);
app.route('/leaderboard', leaderboardRoutes);
app.route('/notifications', notificationRoutes);

// 404 Handler
app.notFound((c) => {
  return c.json({ error: 'Endpoint bulunamadı' }, 404);
});

// Error Handler
app.onError((err, c) => {
  console.error('API Error:', err);
  return c.json({
    error: 'Sunucu hatası',
    message: c.env.ENVIRONMENT === 'development' ? err.message : 'Bir hata oluştu'
  }, 500);
});

export default app;
