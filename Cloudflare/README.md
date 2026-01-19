# BilVeFethet Backend API

Cloudflare Workers + D1 Database + KV Storage ile oluşturulmuş backend API.

## 🚀 Kurulum

### 1. Gereksinimler

- Node.js 18+
- npm veya yarn
- Cloudflare hesabı (ücretsiz)

### 2. Cloudflare CLI Kurulumu

```bash
npm install -g wrangler
```

### 3. Cloudflare'a Giriş

```bash
wrangler login
```

Bu komut tarayıcı açar ve Cloudflare hesabınıza bağlanmanızı sağlar.

### 4. Proje Bağımlılıklarını Yükle

```bash
cd Cloudflare
npm install
```

### 5. D1 Veritabanı Oluştur

```bash
# Veritabanı oluştur
wrangler d1 create bilvefethet-db

# Çıktıdaki database_id'yi wrangler.toml'a yapıştır
```

**wrangler.toml** dosyasını düzenle:
```toml
[[d1_databases]]
binding = "DB"
database_name = "bilvefethet-db"
database_id = "BURAYA_DATABASE_ID_YAPISTIR"
```

### 6. KV Namespace Oluştur

```bash
# KV namespace oluştur
wrangler kv:namespace create "SESSIONS"

# Çıktıdaki id'yi wrangler.toml'a yapıştır
```

**wrangler.toml** dosyasını düzenle:
```toml
[[kv_namespaces]]
binding = "SESSIONS"
id = "BURAYA_KV_ID_YAPISTIR"
```

### 7. Veritabanı Şemasını Uygula

```bash
# Lokal test için
npm run db:migrate:local

# Production için
npm run db:migrate
```

### 8. Secrets Ayarla

```bash
# JWT Secret (güçlü bir key oluştur)
wrangler secret put JWT_SECRET
# Girdi: rastgele-guclu-bir-anahtar-32-karakter

# Turnstile Secret (opsiyonel - Cloudflare dashboard'dan al)
wrangler secret put TURNSTILE_SECRET
```

### 9. Deploy Et

```bash
# Geliştirme (lokal)
npm run dev

# Production'a deploy
npm run deploy
```

Deploy sonrası URL'iniz: `https://bilvefethet-api.YOUR_SUBDOMAIN.workers.dev`

---

## 📡 API Endpoints

### Auth

| Endpoint | Method | Açıklama |
|----------|--------|----------|
| `/auth/register` | POST | Yeni kullanıcı kaydı |
| `/auth/login` | POST | Giriş yap |
| `/auth/logout` | POST | Çıkış yap |
| `/auth/refresh` | POST | Token yenile |
| `/auth/me` | GET | Mevcut kullanıcı bilgisi |
| `/auth/forgot-password` | POST | Şifre sıfırlama isteği |
| `/auth/reset-password` | POST | Şifre sıfırla |
| `/auth/verify-email` | GET | E-posta doğrula |

### Profile

| Endpoint | Method | Açıklama |
|----------|--------|----------|
| `/profile` | GET | Kendi profilini al |
| `/profile/:userId` | GET | Başka kullanıcının profili |
| `/profile` | PUT | Profili güncelle |
| `/profile/settings` | PUT | Ayarları güncelle |
| `/profile/game-right/use` | POST | Oyun hakkı kullan |
| `/profile/joker/use` | POST | Joker kullan |
| `/profile/stats/detailed` | GET | Detaylı istatistikler |

### Questions

| Endpoint | Method | Açıklama |
|----------|--------|----------|
| `/questions/random` | GET | Rastgele soru al |
| `/questions/answer` | POST | Cevap gönder |
| `/questions/categories` | GET | Kategorileri listele |
| `/questions` | POST | Soru ekle (admin) |
| `/questions/bulk` | POST | Toplu soru ekle (admin) |

### Chat

| Endpoint | Method | Açıklama |
|----------|--------|----------|
| `/chat/conversations` | GET | Konuşma listesi |
| `/chat/:userId` | GET | Mesajları al |
| `/chat/:userId` | POST | Mesaj gönder |
| `/chat/:userId/:messageId` | DELETE | Mesaj sil |
| `/chat/unread/count` | GET | Okunmamış sayısı |

### Friends

| Endpoint | Method | Açıklama |
|----------|--------|----------|
| `/friends` | GET | Arkadaş listesi |
| `/friends/requests` | GET | Arkadaşlık istekleri |
| `/friends/request/:userId` | POST | İstek gönder |
| `/friends/accept/:userId` | POST | İsteği kabul et |
| `/friends/reject/:userId` | POST | İsteği reddet |
| `/friends/:userId` | DELETE | Arkadaşlıktan çıkar |
| `/friends/block/:userId` | POST | Engelle |
| `/friends/search` | GET | Kullanıcı ara |

### Leaderboard

| Endpoint | Method | Açıklama |
|----------|--------|----------|
| `/leaderboard/weekly` | GET | Haftalık sıralama |
| `/leaderboard/monthly` | GET | Aylık sıralama |
| `/leaderboard/all-time` | GET | Tüm zamanlar |
| `/leaderboard/friends` | GET | Arkadaş sıralaması |
| `/leaderboard/around-me` | GET | Etrafımdaki oyuncular |
| `/leaderboard/top` | GET | En iyi 10 |

### Notifications

| Endpoint | Method | Açıklama |
|----------|--------|----------|
| `/notifications` | GET | Bildirimleri listele |
| `/notifications/count` | GET | Okunmamış sayısı |
| `/notifications/:id/read` | POST | Okundu işaretle |
| `/notifications/read-all` | POST | Tümünü okundu işaretle |
| `/notifications/:id` | DELETE | Bildirimi sil |
| `/notifications` | DELETE | Tümünü sil |

---

## 🔐 Kimlik Doğrulama

Tüm korumalı endpoint'ler için `Authorization` header'ı gereklidir:

```
Authorization: Bearer YOUR_JWT_TOKEN
```

---

## 📝 Örnek İstekler

### Kayıt

```bash
curl -X POST https://bilvefethet-api.xxx.workers.dev/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "username": "testuser",
    "password": "Test1234",
    "displayName": "Test Kullanıcı"
  }'
```

### Giriş

```bash
curl -X POST https://bilvefethet-api.xxx.workers.dev/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "identifier": "testuser",
    "password": "Test1234"
  }'
```

### Rastgele Soru Al

```bash
curl -X GET "https://bilvefethet-api.xxx.workers.dev/questions/random?count=5&category=Tarih" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

## 🛠️ Geliştirme

```bash
# Lokal sunucu başlat
npm run dev

# Logları izle
npm run tail
```

---

## 📊 Maliyet

Cloudflare ücretsiz tier limitleri:
- Workers: 100,000 istek/gün
- D1: 5GB depolama, 5M satır okuma/gün
- KV: 100,000 okuma/gün

Çoğu küçük-orta ölçekli oyun için ücretsiz tier yeterlidir.
