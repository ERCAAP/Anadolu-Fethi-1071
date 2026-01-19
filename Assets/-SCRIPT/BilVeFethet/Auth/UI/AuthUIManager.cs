using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BilVeFethet.Utils;

namespace BilVeFethet.Auth.UI
{
    /// <summary>
    /// Kimlik Doğrulama UI Yöneticisi
    /// Login, Register, ForgotPassword panellerini yönetir
    /// </summary>
    public class AuthUIManager : SceneSingleton<AuthUIManager>
    {
        [Header("Paneller")]
        [SerializeField] private GameObject authContainer;
        [SerializeField] private GameObject loginPanel;
        [SerializeField] private GameObject registerPanel;
        [SerializeField] private GameObject forgotPasswordPanel;
        [SerializeField] private GameObject loadingOverlay;

        [Header("Login Panel")]
        [SerializeField] private TMP_InputField loginIdentifierInput;
        [SerializeField] private TMP_InputField loginPasswordInput;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button goToRegisterButton;
        [SerializeField] private Button forgotPasswordButton;
        [SerializeField] private Toggle rememberMeToggle;
        [SerializeField] private TextMeshProUGUI loginErrorText;

        [Header("Register Panel")]
        [SerializeField] private TMP_InputField registerEmailInput;
        [SerializeField] private TMP_InputField registerUsernameInput;
        [SerializeField] private TMP_InputField registerPasswordInput;
        [SerializeField] private TMP_InputField registerConfirmPasswordInput;
        [SerializeField] private TMP_InputField registerDisplayNameInput;
        [SerializeField] private Button registerButton;
        [SerializeField] private Button goToLoginButton;
        [SerializeField] private TextMeshProUGUI registerErrorText;
        [SerializeField] private TextMeshProUGUI passwordStrengthText;

        [Header("Forgot Password Panel")]
        [SerializeField] private TMP_InputField forgotEmailInput;
        [SerializeField] private Button sendResetButton;
        [SerializeField] private Button backToLoginButton;
        [SerializeField] private TextMeshProUGUI forgotMessageText;

        [Header("Animasyon")]
        [SerializeField] private float panelTransitionDuration = 0.3f;

        // Events
        public event Action OnLoginSuccess;
        public event Action OnRegisterSuccess;

        // State
        private CanvasGroup _loginCanvasGroup;
        private CanvasGroup _registerCanvasGroup;
        private CanvasGroup _forgotCanvasGroup;

        protected override void OnSingletonAwake()
        {
            SetupCanvasGroups();
        }

        private void Start()
        {
            SetupButtons();
            SetupInputValidation();
            SubscribeToAuthEvents();

            // Başlangıçta login panel
            ShowLoginPanel();

            // Zaten giriş yapmışsa gizle
            if (AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn)
            {
                HideAuthUI();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromAuthEvents();
        }

        #region Setup

        private void SetupCanvasGroups()
        {
            _loginCanvasGroup = GetOrAddCanvasGroup(loginPanel);
            _registerCanvasGroup = GetOrAddCanvasGroup(registerPanel);
            _forgotCanvasGroup = GetOrAddCanvasGroup(forgotPasswordPanel);
        }

        private CanvasGroup GetOrAddCanvasGroup(GameObject panel)
        {
            if (panel == null) return null;
            var cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.AddComponent<CanvasGroup>();
            return cg;
        }

        private void SetupButtons()
        {
            // Login Panel
            loginButton?.onClick.AddListener(OnLoginClicked);
            goToRegisterButton?.onClick.AddListener(ShowRegisterPanel);
            forgotPasswordButton?.onClick.AddListener(ShowForgotPasswordPanel);

            // Register Panel
            registerButton?.onClick.AddListener(OnRegisterClicked);
            goToLoginButton?.onClick.AddListener(ShowLoginPanel);

            // Forgot Password Panel
            sendResetButton?.onClick.AddListener(OnSendResetClicked);
            backToLoginButton?.onClick.AddListener(ShowLoginPanel);
        }

        private void SetupInputValidation()
        {
            // Şifre güçlülük kontrolü
            registerPasswordInput?.onValueChanged.AddListener(UpdatePasswordStrength);

            // Enter tuşu ile submit
            loginPasswordInput?.onSubmit.AddListener(_ => OnLoginClicked());
            registerConfirmPasswordInput?.onSubmit.AddListener(_ => OnRegisterClicked());
            forgotEmailInput?.onSubmit.AddListener(_ => OnSendResetClicked());
        }

        private void SubscribeToAuthEvents()
        {
            if (AuthManager.Instance == null) return;

            AuthManager.Instance.OnLoginCompleted += HandleLoginCompleted;
            AuthManager.Instance.OnRegisterCompleted += HandleRegisterCompleted;
            AuthManager.Instance.OnAuthStateChanged += HandleAuthStateChanged;
        }

        private void UnsubscribeFromAuthEvents()
        {
            if (AuthManager.Instance == null) return;

            AuthManager.Instance.OnLoginCompleted -= HandleLoginCompleted;
            AuthManager.Instance.OnRegisterCompleted -= HandleRegisterCompleted;
            AuthManager.Instance.OnAuthStateChanged -= HandleAuthStateChanged;
        }

        #endregion

        #region Panel Navigation

        public void ShowAuthUI()
        {
            if (authContainer != null)
                authContainer.SetActive(true);

            ShowLoginPanel();
        }

        public void HideAuthUI()
        {
            if (authContainer != null)
                authContainer.SetActive(false);
        }

        public void ShowLoginPanel()
        {
            StartCoroutine(TransitionToPanel(loginPanel, _loginCanvasGroup));
            ClearLoginInputs();
        }

        public void ShowRegisterPanel()
        {
            StartCoroutine(TransitionToPanel(registerPanel, _registerCanvasGroup));
            ClearRegisterInputs();
        }

        public void ShowForgotPasswordPanel()
        {
            StartCoroutine(TransitionToPanel(forgotPasswordPanel, _forgotCanvasGroup));
            ClearForgotInputs();
        }

        private IEnumerator TransitionToPanel(GameObject targetPanel, CanvasGroup targetGroup)
        {
            // Tüm panelleri gizle
            SetPanelActive(loginPanel, false);
            SetPanelActive(registerPanel, false);
            SetPanelActive(forgotPasswordPanel, false);

            // Hedef paneli göster
            if (targetPanel != null)
            {
                targetPanel.SetActive(true);

                if (targetGroup != null)
                {
                    targetGroup.alpha = 0;
                    float elapsed = 0;

                    while (elapsed < panelTransitionDuration)
                    {
                        elapsed += Time.deltaTime;
                        targetGroup.alpha = Mathf.Lerp(0, 1, elapsed / panelTransitionDuration);
                        yield return null;
                    }

                    targetGroup.alpha = 1;
                }
            }
        }

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }

        private void ShowLoading(bool show)
        {
            if (loadingOverlay != null)
                loadingOverlay.SetActive(show);

            // Butonları devre dışı bırak
            if (loginButton != null) loginButton.interactable = !show;
            if (registerButton != null) registerButton.interactable = !show;
            if (sendResetButton != null) sendResetButton.interactable = !show;
        }

        #endregion

        #region Button Handlers

        private async void OnLoginClicked()
        {
            // Validasyon
            var identifier = loginIdentifierInput?.text?.Trim();
            var password = loginPasswordInput?.text;

            if (string.IsNullOrEmpty(identifier))
            {
                ShowLoginError("E-posta veya kullanıcı adı giriniz");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowLoginError("Şifre giriniz");
                return;
            }

            ShowLoading(true);
            ClearLoginError();

            await AuthManager.Instance.LoginAsync(identifier, password);
        }

        private async void OnRegisterClicked()
        {
            // Validasyon
            var email = registerEmailInput?.text?.Trim();
            var username = registerUsernameInput?.text?.Trim();
            var password = registerPasswordInput?.text;
            var confirmPassword = registerConfirmPasswordInput?.text;
            var displayName = registerDisplayNameInput?.text?.Trim();

            if (string.IsNullOrEmpty(email))
            {
                ShowRegisterError("E-posta adresi giriniz");
                return;
            }

            if (!IsValidEmail(email))
            {
                ShowRegisterError("Geçerli bir e-posta adresi giriniz");
                return;
            }

            if (string.IsNullOrEmpty(username))
            {
                ShowRegisterError("Kullanıcı adı giriniz");
                return;
            }

            if (username.Length < 3 || username.Length > 20)
            {
                ShowRegisterError("Kullanıcı adı 3-20 karakter olmalıdır");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowRegisterError("Şifre giriniz");
                return;
            }

            if (password != confirmPassword)
            {
                ShowRegisterError("Şifreler eşleşmiyor");
                return;
            }

            if (!IsStrongPassword(password))
            {
                ShowRegisterError("Şifre en az 8 karakter, büyük/küçük harf ve rakam içermelidir");
                return;
            }

            ShowLoading(true);
            ClearRegisterError();

            await AuthManager.Instance.RegisterAsync(email, username, password, displayName);
        }

        private async void OnSendResetClicked()
        {
            var email = forgotEmailInput?.text?.Trim();

            if (string.IsNullOrEmpty(email))
            {
                ShowForgotMessage("E-posta adresi giriniz", true);
                return;
            }

            if (!IsValidEmail(email))
            {
                ShowForgotMessage("Geçerli bir e-posta adresi giriniz", true);
                return;
            }

            ShowLoading(true);

            var success = await AuthManager.Instance.ForgotPasswordAsync(email);

            ShowLoading(false);
            ShowForgotMessage("Eğer bu e-posta kayıtlıysa, şifre sıfırlama bağlantısı gönderildi.", false);
        }

        #endregion

        #region Event Handlers

        private void HandleLoginCompleted(object sender, LoginEventArgs e)
        {
            ShowLoading(false);

            if (e.Success)
            {
                HideAuthUI();
                OnLoginSuccess?.Invoke();
            }
            else
            {
                ShowLoginError(e.ErrorMessage ?? "Giriş başarısız");
            }
        }

        private void HandleRegisterCompleted(object sender, RegisterEventArgs e)
        {
            ShowLoading(false);

            if (e.Success)
            {
                HideAuthUI();
                OnRegisterSuccess?.Invoke();
            }
            else
            {
                ShowRegisterError(e.ErrorMessage ?? "Kayıt başarısız");
            }
        }

        private void HandleAuthStateChanged(object sender, AuthStateChangedEventArgs e)
        {
            if (e.NewState == AuthState.LoggedIn)
            {
                HideAuthUI();
            }
        }

        #endregion

        #region Validation & Helpers

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private bool IsStrongPassword(string password)
        {
            if (password.Length < 8) return false;
            if (!System.Text.RegularExpressions.Regex.IsMatch(password, "[a-z]")) return false;
            if (!System.Text.RegularExpressions.Regex.IsMatch(password, "[A-Z]")) return false;
            if (!System.Text.RegularExpressions.Regex.IsMatch(password, "[0-9]")) return false;
            return true;
        }

        private void UpdatePasswordStrength(string password)
        {
            if (passwordStrengthText == null) return;

            if (string.IsNullOrEmpty(password))
            {
                passwordStrengthText.text = "";
                return;
            }

            int score = 0;
            if (password.Length >= 8) score++;
            if (password.Length >= 12) score++;
            if (System.Text.RegularExpressions.Regex.IsMatch(password, "[a-z]")) score++;
            if (System.Text.RegularExpressions.Regex.IsMatch(password, "[A-Z]")) score++;
            if (System.Text.RegularExpressions.Regex.IsMatch(password, "[0-9]")) score++;
            if (System.Text.RegularExpressions.Regex.IsMatch(password, "[^a-zA-Z0-9]")) score++;

            switch (score)
            {
                case <= 2:
                    passwordStrengthText.text = "Zayıf";
                    passwordStrengthText.color = Color.red;
                    break;
                case <= 4:
                    passwordStrengthText.text = "Orta";
                    passwordStrengthText.color = Color.yellow;
                    break;
                default:
                    passwordStrengthText.text = "Güçlü";
                    passwordStrengthText.color = Color.green;
                    break;
            }
        }

        private void ShowLoginError(string message)
        {
            if (loginErrorText != null)
            {
                loginErrorText.text = message;
                loginErrorText.gameObject.SetActive(true);
            }
        }

        private void ClearLoginError()
        {
            if (loginErrorText != null)
            {
                loginErrorText.text = "";
                loginErrorText.gameObject.SetActive(false);
            }
        }

        private void ShowRegisterError(string message)
        {
            if (registerErrorText != null)
            {
                registerErrorText.text = message;
                registerErrorText.gameObject.SetActive(true);
            }
        }

        private void ClearRegisterError()
        {
            if (registerErrorText != null)
            {
                registerErrorText.text = "";
                registerErrorText.gameObject.SetActive(false);
            }
        }

        private void ShowForgotMessage(string message, bool isError)
        {
            if (forgotMessageText != null)
            {
                forgotMessageText.text = message;
                forgotMessageText.color = isError ? Color.red : Color.green;
                forgotMessageText.gameObject.SetActive(true);
            }
        }

        private void ClearLoginInputs()
        {
            if (loginIdentifierInput != null) loginIdentifierInput.text = "";
            if (loginPasswordInput != null) loginPasswordInput.text = "";
            ClearLoginError();
        }

        private void ClearRegisterInputs()
        {
            if (registerEmailInput != null) registerEmailInput.text = "";
            if (registerUsernameInput != null) registerUsernameInput.text = "";
            if (registerPasswordInput != null) registerPasswordInput.text = "";
            if (registerConfirmPasswordInput != null) registerConfirmPasswordInput.text = "";
            if (registerDisplayNameInput != null) registerDisplayNameInput.text = "";
            if (passwordStrengthText != null) passwordStrengthText.text = "";
            ClearRegisterError();
        }

        private void ClearForgotInputs()
        {
            if (forgotEmailInput != null) forgotEmailInput.text = "";
            if (forgotMessageText != null) forgotMessageText.gameObject.SetActive(false);
        }

        #endregion
    }
}
