# 🎮 BilVeFethet - Tam Kurulum Rehberi

Bu rehber, Cloudflare backend ve Unity entegrasyonunu adım adım açıklar.

---

## 📋 Genel Bakış

```
┌─────────────────────────────────────────────────────────────┐
│                      UNITY OYUN                              │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐          │
│  │ AuthManager │  │ AuthUIManager│ │MainMenuManager│         │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘          │
│         │                │                │                  │
│         └────────────────┴────────────────┘                  │
│                          │                                   │
│              CloudflareConfig.cs                             │
└──────────────────────────┼───────────────────────────────────┘
                           │ HTTPS
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                  CLOUDFLARE WORKERS                          │
│  ┌─────────────────────────────────────────────────────┐    │
│  │  Auth | Profile | Questions | Chat | Leaderboard    │    │
│  └─────────────────────────────────────────────────────┘    │
│                          │                                   │
│         ┌────────────────┼────────────────┐                  │
│         ▼                ▼                ▼                  │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐          │
│  │  D1 (SQL)   │  │  KV Store   │  │  Turnstile  │          │
│  └─────────────┘  └─────────────┘  └─────────────┘          │
└─────────────────────────────────────────────────────────────┘
```

---

## 🔧 BÖLÜM 1: Cloudflare Kurulumu

### Adım 1.1: Cloudflare Hesabı

1. https://dash.cloudflare.com adresine git
2. Ücretsiz hesap oluştur
3. E-posta doğrula

### Adım 1.2: Wrangler CLI Kurulumu

Terminal'de:
```bash
# Node.js 18+ gerekli
npm install -g wrangler

# Cloudflare'a giriş
wrangler login
```

### Adım 1.3: Proje Kurulumu

```bash
cd "Anadolu fethi 1071/Cloudflare"
npm install
```

### Adım 1.4: D1 Veritabanı Oluştur

```bash
wrangler d1 create bilvefethet-db
```

Çıktı şöyle olacak:
```
✅ Successfully created DB 'bilvefethet-db'
database_id = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
```

**Bu ID'yi kopyala!**

### Adım 1.5: KV Namespace Oluştur

```bash
wrangler kv:namespace create "SESSIONS"
```

Çıktı şöyle olacak:
```
✅ Successfully created KV namespace
id = "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy"
```

**Bu ID'yi de kopyala!**

### Adım 1.6: wrangler.toml Güncelle

`Cloudflare/wrangler.toml` dosyasını aç ve ID'leri yapıştır:

```toml
name = "bilvefethet-api"
main = "src/index.ts"
compatibility_date = "2024-01-01"

[[d1_databases]]
binding = "DB"
database_name = "bilvefethet-db"
database_id = "BURAYA_D1_ID_YAPISTIR"  # ← Değiştir

[[kv_namespaces]]
binding = "SESSIONS"
id = "BURAYA_KV_ID_YAPISTIR"  # ← Değiştir
```

### Adım 1.7: Secrets Ayarla

```bash
# JWT için güçlü bir key oluştur (32+ karakter)
wrangler secret put JWT_SECRET
# Girdi: my-super-secret-jwt-key-change-this-123

# Turnstile (opsiyonel)
# wrangler secret put TURNSTILE_SECRET
```

### Adım 1.8: Veritabanı Şemasını Uygula

```bash
# Production
wrangler d1 execute bilvefethet-db --file=./schema.sql
```

### Adım 1.9: Deploy Et

```bash
npm run deploy
```

Çıktı:
```
✅ Deployed bilvefethet-api
https://bilvefethet-api.YOUR_SUBDOMAIN.workers.dev
```

**Bu URL'yi not al!** Unity'de kullanacaksın.

---

## 🎮 BÖLÜM 2: Unity Kurulumu

### Adım 2.1: CloudflareConfig Oluştur

1. Unity'de `Project` penceresinde:
   - Sağ tık → Create → Folder → "Resources" adında klasör oluştur

2. Resources klasöründe:
   - Sağ tık → Create → BilVeFethet → Cloudflare Config

3. Inspector'da ayarları doldur:
   - **Api Base Url**: `https://bilvefethet-api.YOUR_SUBDOMAIN.workers.dev`
   - **Timeout Seconds**: 30
   - **Enable Debug Logs**: ✓ (geliştirme için)

### Adım 2.2: Auth Scene Hierarchy

MainMenu sahnesinde şu yapıyı oluştur:

```
Canvas
├── AuthContainer
│   ├── LoginPanel
│   │   ├── TitleText (TMP) - "Giriş Yap"
│   │   ├── IdentifierInput (TMP_InputField) - placeholder: "E-posta veya Kullanıcı Adı"
│   │   ├── PasswordInput (TMP_InputField) - placeholder: "Şifre"
│   │   ├── RememberMeToggle (Toggle)
│   │   ├── LoginButton (Button) - "Giriş Yap"
│   │   ├── ForgotPasswordButton (Button) - "Şifremi Unuttum"
│   │   ├── GoToRegisterButton (Button) - "Hesap Oluştur"
│   │   └── ErrorText (TMP) - kırmızı, gizli
│   │
│   ├── RegisterPanel (gizli)
│   │   ├── TitleText (TMP) - "Hesap Oluştur"
│   │   ├── EmailInput (TMP_InputField)
│   │   ├── UsernameInput (TMP_InputField)
│   │   ├── DisplayNameInput (TMP_InputField)
│   │   ├── PasswordInput (TMP_InputField)
│   │   ├── ConfirmPasswordInput (TMP_InputField)
│   │   ├── PasswordStrengthText (TMP)
│   │   ├── RegisterButton (Button)
│   │   ├── GoToLoginButton (Button)
│   │   └── ErrorText (TMP)
│   │
│   ├── ForgotPasswordPanel (gizli)
│   │   ├── TitleText (TMP)
│   │   ├── EmailInput (TMP_InputField)
│   │   ├── SendButton (Button)
│   │   ├── BackButton (Button)
│   │   └── MessageText (TMP)
│   │
│   └── LoadingOverlay (gizli)
│       └── SpinnerImage
```

### Adım 2.3: Managers GameObject

```
[MANAGERS]
├── AuthManager (script: AuthManager.cs)
├── PlayerManager (script: PlayerManager.cs)
├── GameModeManager (script: GameModeManager.cs)
└── ... diğer manager'lar
```

### Adım 2.4: AuthUIManager Bağlantıları

AuthUIManager'ı Canvas'a veya ayrı bir GameObject'e ekle:

| SerializeField | Bağlanacak |
|----------------|------------|
| authContainer | AuthContainer |
| loginPanel | LoginPanel |
| registerPanel | RegisterPanel |
| forgotPasswordPanel | ForgotPasswordPanel |
| loadingOverlay | LoadingOverlay |
| loginIdentifierInput | LoginPanel/IdentifierInput |
| loginPasswordInput | LoginPanel/PasswordInput |
| loginButton | LoginPanel/LoginButton |
| goToRegisterButton | LoginPanel/GoToRegisterButton |
| forgotPasswordButton | LoginPanel/ForgotPasswordButton |
| loginErrorText | LoginPanel/ErrorText |
| registerEmailInput | RegisterPanel/EmailInput |
| registerUsernameInput | RegisterPanel/UsernameInput |
| registerPasswordInput | RegisterPanel/PasswordInput |
| registerConfirmPasswordInput | RegisterPanel/ConfirmPasswordInput |
| registerDisplayNameInput | RegisterPanel/DisplayNameInput |
| registerButton | RegisterPanel/RegisterButton |
| goToLoginButton | RegisterPanel/GoToLoginButton |
| registerErrorText | RegisterPanel/ErrorText |
| passwordStrengthText | RegisterPanel/PasswordStrengthText |
| forgotEmailInput | ForgotPasswordPanel/EmailInput |
| sendResetButton | ForgotPasswordPanel/SendButton |
| backToLoginButton | ForgotPasswordPanel/BackButton |
| forgotMessageText | ForgotPasswordPanel/MessageText |

### Adım 2.5: Input Field Ayarları

**Password Input'lar için:**
- Content Type: Password
- Character Limit: 128

**Email Input için:**
- Content Type: Email Address

---

## ✅ BÖLÜM 3: Test Et

### 3.1: Cloudflare API Test

Terminal'de:
```bash
# Health check
curl https://bilvefethet-api.YOUR_SUBDOMAIN.workers.dev/

# Kayıt test
curl -X POST https://bilvefethet-api.YOUR_SUBDOMAIN.workers.dev/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@test.com","username":"testuser","password":"Test1234"}'
```

### 3.2: Unity Test

1. Play Mode'a gir
2. Register panelinde yeni hesap oluştur
3. Console'da logları kontrol et
4. Giriş yap

---

## 🔄 Akış Diyagramı

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│   Oyuncu     │────▶│  Login UI    │────▶│ AuthManager  │
│  bilgi girer │     │              │     │              │
└──────────────┘     └──────────────┘     └──────┬───────┘
                                                  │
                                                  ▼
                                          ┌──────────────┐
                                          │  Cloudflare  │
                                          │   Workers    │
                                          └──────┬───────┘
                                                  │
                                                  ▼
                                          ┌──────────────┐
                                          │  D1 Database │
                                          │  (SQLite)    │
                                          └──────┬───────┘
                                                  │
                                                  ▼
                                          ┌──────────────┐
                                          │   JWT Token  │◀──┐
                                          │   Döndür     │   │
                                          └──────┬───────┘   │
                                                  │          │
                                                  ▼          │
                                          ┌──────────────┐   │
                                          │ AuthManager  │   │
                                          │ Token Kaydet │   │
                                          └──────┬───────┘   │
                                                  │          │
                                                  ▼          │
                                          ┌──────────────┐   │
                                          │PlayerManager │   │
                                          │ Veri Yükle   │───┘
                                          └──────────────┘
```

---

## 🐛 Sorun Giderme

### "Network Error" Hatası
- CloudflareConfig'deki API URL'yi kontrol et
- Cloudflare Worker'ın çalıştığını doğrula
- CORS ayarlarını kontrol et

### "Invalid Token" Hatası
- Token süresi dolmuş olabilir
- Çıkış yap ve tekrar giriş yap

### Database Hatası
- Schema'nın doğru uygulandığını kontrol et:
  ```bash
  wrangler d1 execute bilvefethet-db --command="SELECT name FROM sqlite_master WHERE type='table';"
  ```

### Deploy Hatası
- `wrangler.toml`'daki ID'lerin doğru olduğunu kontrol et
- Secrets'ların ayarlandığını kontrol et:
  ```bash
  wrangler secret list
  ```

---

## 📚 Dosya Yapısı

```
Anadolu fethi 1071/
├── Cloudflare/
│   ├── src/
│   │   ├── index.ts           # Ana router
│   │   ├── routes/
│   │   │   ├── auth.ts        # Kimlik doğrulama
│   │   │   ├── profile.ts     # Profil yönetimi
│   │   │   ├── questions.ts   # Soru API
│   │   │   ├── chat.ts        # Mesajlaşma
│   │   │   ├── friends.ts     # Arkadaşlık
│   │   │   ├── leaderboard.ts # Sıralama
│   │   │   └── notifications.ts
│   │   ├── middleware/
│   │   │   └── auth.ts        # JWT middleware
│   │   └── utils/
│   │       ├── password.ts    # Şifre hashleme
│   │       └── jwt.ts         # Token işlemleri
│   ├── schema.sql             # Veritabanı şeması
│   ├── wrangler.toml          # Cloudflare config
│   ├── package.json
│   └── README.md
│
├── Assets/-SCRIPT/BilVeFethet/
│   ├── Auth/
│   │   ├── AuthManager.cs     # Ana auth yöneticisi
│   │   ├── AuthData.cs        # Veri modelleri
│   │   ├── CloudflareConfig.cs # API ayarları
│   │   └── UI/
│   │       └── AuthUIManager.cs # Login/Register UI
│   └── ...
│
└── KURULUM_REHBERI.md         # Bu dosya
```

---

## 🎉 Tebrikler!

Kurulum tamamlandı. Artık:
- ✅ Kullanıcı kaydı ve girişi çalışıyor
- ✅ Profil yönetimi hazır
- ✅ Soru API'si hazır
- ✅ Mesajlaşma sistemi hazır
- ✅ Arkadaşlık sistemi hazır
- ✅ Sıralama sistemi hazır

Sorularınız için issue açabilirsiniz!
