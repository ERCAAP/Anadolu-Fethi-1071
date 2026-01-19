import { Hono } from 'hono';
import { authMiddleware, optionalAuthMiddleware } from '../middleware/auth';
import { generateId } from '../utils/jwt';

interface Env {
  DB: D1Database;
  SESSIONS: KVNamespace;
  JWT_SECRET: string;
}

export const questionRoutes = new Hono<{ Bindings: Env }>();

// ==========================================
// GET /questions/random - Rastgele soru al
// ==========================================
questionRoutes.get('/random', optionalAuthMiddleware, async (c) => {
  const category = c.req.query('category');
  const difficulty = c.req.query('difficulty');
  const count = parseInt(c.req.query('count') || '1');
  const excludeIds = c.req.query('exclude')?.split(',') || [];

  let query = 'SELECT * FROM questions WHERE is_active = 1';
  const params: any[] = [];

  if (category) {
    query += ' AND category = ?';
    params.push(category);
  }

  if (difficulty) {
    const diffMap: Record<string, [number, number]> = {
      'Kolay': [1, 3],
      'Orta': [4, 6],
      'Zor': [7, 10]
    };
    const range = diffMap[difficulty];
    if (range) {
      query += ' AND difficulty_level BETWEEN ? AND ?';
      params.push(range[0], range[1]);
    }
  }

  if (excludeIds.length > 0) {
    query += ` AND id NOT IN (${excludeIds.map(() => '?').join(',')})`;
    params.push(...excludeIds);
  }

  query += ' ORDER BY RANDOM() LIMIT ?';
  params.push(Math.min(count, 20)); // Max 20 soru

  const questions = await c.env.DB.prepare(query).bind(...params).all<any>();

  // Cevapları gizle (client tarafında gösterilmemeli)
  const sanitizedQuestions = questions.results?.map(q => ({
    id: q.id,
    questionText: q.question_text,
    questionType: q.question_type,
    category: q.category,
    difficultyLevel: q.difficulty_level,
    options: JSON.parse(q.options || '[]'),
    timeLimit: q.time_limit,
    valueUnit: q.value_unit,
    // correctAnswerIndex ve correctValue gönderilmiyor!
  }));

  return c.json({
    success: true,
    data: sanitizedQuestions
  });
});

// ==========================================
// POST /questions/answer - Cevap gönder
// ==========================================
questionRoutes.post('/answer', authMiddleware, async (c) => {
  const user = c.get('user');
  const body = await c.req.json();
  const { questionId, answer, answerTime } = body;

  if (!questionId) {
    return c.json({ success: false, error: 'Soru ID gereklidir' }, 400);
  }

  const question = await c.env.DB.prepare(
    'SELECT * FROM questions WHERE id = ?'
  ).bind(questionId).first<any>();

  if (!question) {
    return c.json({ success: false, error: 'Soru bulunamadı' }, 404);
  }

  let isCorrect = false;
  let earnedPoints = 0;
  let correctAnswer: any;

  if (question.question_type === 0) {
    // Çoktan seçmeli
    isCorrect = answer === question.correct_answer_index;
    correctAnswer = question.correct_answer_index;
  } else {
    // Tahmin sorusu
    const guessedValue = parseFloat(answer);
    const correctValue = question.correct_value;
    const tolerance = question.tolerance_percent / 100;

    const lowerBound = correctValue * (1 - tolerance);
    const upperBound = correctValue * (1 + tolerance);

    isCorrect = guessedValue >= lowerBound && guessedValue <= upperBound;
    correctAnswer = correctValue;
  }

  // Puan hesaplama
  if (isCorrect) {
    const basePoints = question.difficulty_level * 10;
    const timeBonus = Math.max(0, (question.time_limit - answerTime) * 2);
    earnedPoints = Math.round(basePoints + timeBonus);
  }

  // İstatistikleri güncelle
  if (isCorrect) {
    await c.env.DB.prepare(
      'UPDATE users SET total_correct_answers = total_correct_answers + 1 WHERE id = ?'
    ).bind(user.userId).run();
  } else {
    await c.env.DB.prepare(
      'UPDATE users SET total_wrong_answers = total_wrong_answers + 1 WHERE id = ?'
    ).bind(user.userId).run();
  }

  return c.json({
    success: true,
    data: {
      isCorrect,
      earnedPoints,
      correctAnswer,
      explanation: question.explanation || null
    }
  });
});

// ==========================================
// GET /questions/categories - Kategorileri listele
// ==========================================
questionRoutes.get('/categories', async (c) => {
  const categories = await c.env.DB.prepare(`
    SELECT category, COUNT(*) as count
    FROM questions
    WHERE is_active = 1
    GROUP BY category
    ORDER BY count DESC
  `).all<any>();

  return c.json({
    success: true,
    data: categories.results
  });
});

// ==========================================
// Admin: POST /questions - Soru ekle
// ==========================================
questionRoutes.post('/', authMiddleware, async (c) => {
  // TODO: Admin kontrolü ekle
  const body = await c.req.json();
  const {
    questionText,
    questionType = 0,
    category,
    difficultyLevel = 5,
    options = [],
    correctAnswerIndex = 0,
    correctValue = 0,
    tolerancePercent = 10,
    valueUnit = '',
    timeLimit = 30
  } = body;

  if (!questionText || !category) {
    return c.json({ success: false, error: 'Soru metni ve kategori gereklidir' }, 400);
  }

  const questionId = generateId();

  await c.env.DB.prepare(`
    INSERT INTO questions (
      id, question_text, question_type, category, difficulty_level,
      options, correct_answer_index, correct_value, tolerance_percent,
      value_unit, time_limit
    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
  `).bind(
    questionId,
    questionText,
    questionType,
    category,
    difficultyLevel,
    JSON.stringify(options),
    correctAnswerIndex,
    correctValue,
    tolerancePercent,
    valueUnit,
    timeLimit
  ).run();

  return c.json({
    success: true,
    data: { questionId }
  });
});

// ==========================================
// Admin: POST /questions/bulk - Toplu soru ekle
// ==========================================
questionRoutes.post('/bulk', authMiddleware, async (c) => {
  // TODO: Admin kontrolü ekle
  const body = await c.req.json();
  const { questions } = body;

  if (!Array.isArray(questions) || questions.length === 0) {
    return c.json({ success: false, error: 'Soru listesi gereklidir' }, 400);
  }

  const statements = questions.map((q: any) => {
    const questionId = generateId();
    return c.env.DB.prepare(`
      INSERT INTO questions (
        id, question_text, question_type, category, difficulty_level,
        options, correct_answer_index, correct_value, tolerance_percent,
        value_unit, time_limit
      ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    `).bind(
      questionId,
      q.questionText,
      q.questionType || 0,
      q.category,
      q.difficultyLevel || 5,
      JSON.stringify(q.options || []),
      q.correctAnswerIndex || 0,
      q.correctValue || 0,
      q.tolerancePercent || 10,
      q.valueUnit || '',
      q.timeLimit || 30
    );
  });

  await c.env.DB.batch(statements);

  return c.json({
    success: true,
    message: `${questions.length} soru eklendi`
  });
});
