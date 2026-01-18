using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BilVeFethet.Editor
{
    /// <summary>
    /// UI Setup Helper - Ana menü UI elementlerini oluşturur
    /// </summary>
    public static class UISetupHelper
    {
#if UNITY_EDITOR
        [MenuItem("BilVeFethet/Setup Main Menu UI")]
        public static void SetupMainMenuUI()
        {
            var canvas = GameObject.Find("MainCanvas");
            if (canvas == null)
            {
                Debug.LogError("MainCanvas bulunamadı!");
                return;
            }

            SetupMainMenuPanel(canvas.transform);
            SetupPlayModePanel(canvas.transform);
            SetupMatchmakingPanel(canvas.transform);
            SetupCreateLobbyPanel(canvas.transform);
            SetupJoinLobbyPanel(canvas.transform);
            SetupLobbyRoomPanel(canvas.transform);
            SetupLeaderboardPanel(canvas.transform);
            SetupProfilePanel(canvas.transform);
            SetupSettingsPanel(canvas.transform);
            SetupShopPanel(canvas.transform);

            Debug.Log("Ana Menü UI kurulumu tamamlandı!");
        }

        private static void SetupMainMenuPanel(Transform canvas)
        {
            var panel = canvas.Find("MainMenuPanel");
            if (panel == null) return;

            SetupPanelRectTransform(panel, true);

            // Üst Bar - Oyuncu Bilgileri
            var topBar = CreateUIElement("TopBar", panel, typeof(Image), typeof(HorizontalLayoutGroup));
            SetupRectTransform(topBar.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -100), new Vector2(0, 100));

            // Avatar
            var avatar = CreateUIElement("Avatar", topBar.transform, typeof(Image));
            SetupRectTransform(avatar.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(80, 80));

            // Oyuncu Bilgisi Container
            var playerInfoContainer = CreateUIElement("PlayerInfoContainer", topBar.transform, typeof(VerticalLayoutGroup));

            var playerNameText = CreateTextElement("PlayerNameText", playerInfoContainer.transform, "Oyuncu Adı", 24, TextAlignmentOptions.Left);
            var playerLevelText = CreateTextElement("PlayerLevelText", playerInfoContainer.transform, "Seviye 1", 18, TextAlignmentOptions.Left);

            // Sağ üst - Para ve Haklar
            var currencyContainer = CreateUIElement("CurrencyContainer", topBar.transform, typeof(HorizontalLayoutGroup));

            var tpText = CreateTextElement("TPText", currencyContainer.transform, "0 TP", 20, TextAlignmentOptions.Right);
            var goldText = CreateTextElement("GoldText", currencyContainer.transform, "0 Altın", 20, TextAlignmentOptions.Right);
            var gameRightsText = CreateTextElement("GameRightsText", currencyContainer.transform, "5 Hak", 20, TextAlignmentOptions.Right);

            // Orta - Ana Butonlar
            var centerContainer = CreateUIElement("CenterContainer", panel, typeof(VerticalLayoutGroup));
            SetupRectTransform(centerContainer.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 500));

            CreateButton("PlayButton", centerContainer.transform, "OYNA", new Color(0.2f, 0.6f, 0.2f));
            CreateButton("LeaderboardButton", centerContainer.transform, "Sıralama", new Color(0.3f, 0.3f, 0.7f));
            CreateButton("ProfileButton", centerContainer.transform, "Profil", new Color(0.5f, 0.3f, 0.6f));
            CreateButton("ShopButton", centerContainer.transform, "Mağaza", new Color(0.7f, 0.5f, 0.2f));
            CreateButton("SettingsButton", centerContainer.transform, "Ayarlar", new Color(0.4f, 0.4f, 0.4f));

            // Leaderboard Preview
            var leaderboardPreviewContainer = CreateUIElement("LeaderboardPreviewContainer", panel, typeof(VerticalLayoutGroup));
            SetupRectTransform(leaderboardPreviewContainer.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-150, 0), new Vector2(280, 300));

            CreateTextElement("LeaderboardTitle", leaderboardPreviewContainer.transform, "En İyiler", 20, TextAlignmentOptions.Center);

            var playerRankText = CreateTextElement("PlayerRankText", panel, "Sıralamanız: #--", 18, TextAlignmentOptions.Left);
            SetupRectTransform(playerRankText.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), new Vector2(200, 30));

            var onlinePlayersText = CreateTextElement("OnlinePlayersText", panel, "Çevrimiçi: 0", 16, TextAlignmentOptions.Right);
            SetupRectTransform(onlinePlayersText.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 20), new Vector2(150, 30));

            // Level Progress
            var levelProgressSlider = CreateSlider("LevelProgressSlider", panel);
            SetupRectTransform(levelProgressSlider.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -150), new Vector2(-200, 20));
        }

        private static void SetupPlayModePanel(Transform canvas)
        {
            var panel = canvas.Find("PlayModePanel");
            if (panel == null) return;

            SetupPanelRectTransform(panel, false);

            // Başlık
            CreateTextElement("TitleText", panel, "Oyun Modu Seç", 32, TextAlignmentOptions.Center);

            // Butonlar Container
            var buttonsContainer = CreateUIElement("ButtonsContainer", panel, typeof(VerticalLayoutGroup));
            SetupRectTransform(buttonsContainer.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 400));

            CreateButton("QuickMatchButton", buttonsContainer.transform, "Hızlı Eşleşme", new Color(0.2f, 0.6f, 0.3f));
            CreateButton("CreateLobbyButton", buttonsContainer.transform, "Lobi Oluştur", new Color(0.3f, 0.5f, 0.7f));
            CreateButton("JoinLobbyButton", buttonsContainer.transform, "Lobiye Katıl", new Color(0.5f, 0.4f, 0.6f));
            CreateButton("BotGameButton", buttonsContainer.transform, "Botlara Karşı", new Color(0.6f, 0.4f, 0.2f));

            // Geri Butonu
            var backButton = CreateButton("BackButton", panel, "Geri", new Color(0.5f, 0.5f, 0.5f));
            SetupRectTransform(backButton.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(100, 50), new Vector2(150, 50));
        }

        private static void SetupMatchmakingPanel(Transform canvas)
        {
            var panel = canvas.Find("MatchmakingPanel");
            if (panel == null) return;

            SetupPanelRectTransform(panel, false);

            // Merkez Container
            var centerContainer = CreateUIElement("CenterContainer", panel, typeof(VerticalLayoutGroup));
            SetupRectTransform(centerContainer.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 300));

            CreateTextElement("StatusText", centerContainer.transform, "Oyun aranıyor...", 28, TextAlignmentOptions.Center);
            CreateTextElement("PlayersText", centerContainer.transform, "0/3 Oyuncu", 24, TextAlignmentOptions.Center);

            // Spinner (basit dönen görsel)
            var spinner = CreateUIElement("Spinner", centerContainer.transform, typeof(Image));
            SetupRectTransform(spinner.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));

            // İptal Butonu
            var cancelButton = CreateButton("CancelButton", panel, "İptal", new Color(0.7f, 0.3f, 0.3f));
            SetupRectTransform(cancelButton.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(200, 60));
        }

        private static void SetupCreateLobbyPanel(Transform canvas)
        {
            var panel = canvas.Find("CreateLobbyPanel");
            if (panel == null) return;

            SetupPanelRectTransform(panel, false);

            CreateTextElement("TitleText", panel, "Lobi Oluştur", 28, TextAlignmentOptions.Center);

            var formContainer = CreateUIElement("FormContainer", panel, typeof(VerticalLayoutGroup));
            SetupRectTransform(formContainer.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 300));

            CreateTextElement("LobbyNameLabel", formContainer.transform, "Lobi Adı:", 18, TextAlignmentOptions.Left);
            CreateInputField("LobbyNameInput", formContainer.transform, "Lobi adı girin...");

            CreateToggle("PrivateLobbyToggle", formContainer.transform, "Özel Lobi");

            CreateButton("CreateButton", formContainer.transform, "Oluştur", new Color(0.2f, 0.6f, 0.3f));

            var backButton = CreateButton("BackButton", panel, "Geri", new Color(0.5f, 0.5f, 0.5f));
            SetupRectTransform(backButton.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(100, 50), new Vector2(150, 50));
        }

        private static void SetupJoinLobbyPanel(Transform canvas)
        {
            var panel = canvas.Find("JoinLobbyPanel");
            if (panel == null) return;

            SetupPanelRectTransform(panel, false);

            CreateTextElement("TitleText", panel, "Lobiye Katıl", 28, TextAlignmentOptions.Center);

            var formContainer = CreateUIElement("FormContainer", panel, typeof(VerticalLayoutGroup));
            SetupRectTransform(formContainer.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 200));

            CreateTextElement("CodeLabel", formContainer.transform, "Lobi Kodu:", 18, TextAlignmentOptions.Left);
            CreateInputField("LobbyCodeInput", formContainer.transform, "ABC123");

            CreateButton("JoinButton", formContainer.transform, "Katıl", new Color(0.2f, 0.5f, 0.7f));

            var backButton = CreateButton("BackButton", panel, "Geri", new Color(0.5f, 0.5f, 0.5f));
            SetupRectTransform(backButton.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(100, 50), new Vector2(150, 50));
        }

        private static void SetupLobbyRoomPanel(Transform canvas)
        {
            var panel = canvas.Find("LobbyRoomPanel");
            if (panel == null) return;

            SetupPanelRectTransform(panel, false);

            // Üst kısım - Lobi Kodu
            var lobbyCodeText = CreateTextElement("LobbyCodeText", panel, "Kod: ABC123", 24, TextAlignmentOptions.Center);
            SetupRectTransform(lobbyCodeText.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(300, 40));

            // Oyuncu Listesi
            var playerListContainer = CreateUIElement("PlayerListContainer", panel, typeof(VerticalLayoutGroup));
            SetupRectTransform(playerListContainer.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 300));

            // Alt Butonlar
            var buttonsContainer = CreateUIElement("ButtonsContainer", panel, typeof(HorizontalLayoutGroup));
            SetupRectTransform(buttonsContainer.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(400, 60));

            CreateButton("ReadyButton", buttonsContainer.transform, "Hazır", new Color(0.2f, 0.6f, 0.3f));
            CreateButton("StartGameButton", buttonsContainer.transform, "Başlat", new Color(0.3f, 0.5f, 0.7f));
            CreateButton("LeaveButton", buttonsContainer.transform, "Ayrıl", new Color(0.6f, 0.3f, 0.3f));
        }

        private static void SetupLeaderboardPanel(Transform canvas)
        {
            var panel = canvas.Find("LeaderboardPanel");
            if (panel == null) return;

            SetupPanelRectTransform(panel, false);

            CreateTextElement("TitleText", panel, "Sıralama", 32, TextAlignmentOptions.Center);

            // Tab Butonları
            var tabsContainer = CreateUIElement("TabsContainer", panel, typeof(HorizontalLayoutGroup));
            SetupRectTransform(tabsContainer.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(500, 50));

            CreateButton("WeeklyTab", tabsContainer.transform, "Haftalık", new Color(0.3f, 0.5f, 0.7f));
            CreateButton("MonthlyTab", tabsContainer.transform, "Aylık", new Color(0.4f, 0.4f, 0.5f));
            CreateButton("AllTimeTab", tabsContainer.transform, "Tüm Zamanlar", new Color(0.4f, 0.4f, 0.5f));

            // Liste Container
            var leaderboardListContainer = CreateUIElement("LeaderboardListContainer", panel, typeof(VerticalLayoutGroup));
            SetupRectTransform(leaderboardListContainer.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 400));

            var backButton = CreateButton("BackButton", panel, "Geri", new Color(0.5f, 0.5f, 0.5f));
            SetupRectTransform(backButton.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(100, 50), new Vector2(150, 50));
        }

        private static void SetupProfilePanel(Transform canvas)
        {
            var panel = canvas.Find("ProfilePanel");
            if (panel == null) return;

            SetupPanelRectTransform(panel, false);

            CreateTextElement("TitleText", panel, "Profil", 32, TextAlignmentOptions.Center);

            // Profil Bilgileri
            var profileContainer = CreateUIElement("ProfileContainer", panel, typeof(VerticalLayoutGroup));
            SetupRectTransform(profileContainer.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 400));

            CreateTextElement("UsernameText", profileContainer.transform, "Kullanıcı Adı", 24, TextAlignmentOptions.Center);
            CreateTextElement("LevelText", profileContainer.transform, "Seviye: 1", 20, TextAlignmentOptions.Center);
            CreateTextElement("TPText", profileContainer.transform, "Toplam TP: 0", 20, TextAlignmentOptions.Center);
            CreateTextElement("WinsText", profileContainer.transform, "Kazanılan: 0", 18, TextAlignmentOptions.Center);
            CreateTextElement("LossesText", profileContainer.transform, "Kaybedilen: 0", 18, TextAlignmentOptions.Center);

            var backButton = CreateButton("BackButton", panel, "Geri", new Color(0.5f, 0.5f, 0.5f));
            SetupRectTransform(backButton.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(100, 50), new Vector2(150, 50));
        }

        private static void SetupSettingsPanel(Transform canvas)
        {
            var panel = canvas.Find("SettingsPanel");
            if (panel == null) return;

            SetupPanelRectTransform(panel, false);

            CreateTextElement("TitleText", panel, "Ayarlar", 32, TextAlignmentOptions.Center);

            var settingsContainer = CreateUIElement("SettingsContainer", panel, typeof(VerticalLayoutGroup));
            SetupRectTransform(settingsContainer.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 300));

            CreateToggle("MusicToggle", settingsContainer.transform, "Müzik");
            CreateToggle("SoundToggle", settingsContainer.transform, "Ses Efektleri");
            CreateToggle("NotificationsToggle", settingsContainer.transform, "Bildirimler");

            var backButton = CreateButton("BackButton", panel, "Geri", new Color(0.5f, 0.5f, 0.5f));
            SetupRectTransform(backButton.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(100, 50), new Vector2(150, 50));
        }

        private static void SetupShopPanel(Transform canvas)
        {
            var panel = canvas.Find("ShopPanel");
            if (panel == null) return;

            SetupPanelRectTransform(panel, false);

            CreateTextElement("TitleText", panel, "Mağaza", 32, TextAlignmentOptions.Center);

            var shopContainer = CreateUIElement("ShopContainer", panel, typeof(GridLayoutGroup));
            SetupRectTransform(shopContainer.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 400));

            var backButton = CreateButton("BackButton", panel, "Geri", new Color(0.5f, 0.5f, 0.5f));
            SetupRectTransform(backButton.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(100, 50), new Vector2(150, 50));
        }

        #region Helper Methods

        private static void SetupPanelRectTransform(Transform panel, bool isActive)
        {
            var rt = panel.GetComponent<RectTransform>();
            if (rt == null) rt = panel.gameObject.AddComponent<RectTransform>();

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var image = panel.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            }

            panel.gameObject.SetActive(isActive);
        }

        private static void SetupRectTransform(Transform t, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var rt = t.GetComponent<RectTransform>();
            if (rt == null) rt = t.gameObject.AddComponent<RectTransform>();

            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        private static GameObject CreateUIElement(string name, Transform parent, params System.Type[] components)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            go.AddComponent<RectTransform>();
            foreach (var comp in components)
            {
                go.AddComponent(comp);
            }

            return go;
        }

        private static TextMeshProUGUI CreateTextElement(string name, Transform parent, string text, int fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 40);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;

            return tmp;
        }

        private static Button CreateButton(string name, Transform parent, string buttonText, Color bgColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 60);

            var image = go.AddComponent<Image>();
            image.color = bgColor;

            var button = go.AddComponent<Button>();

            // Text
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);

            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = buttonText;
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return button;
        }

        private static TMP_InputField CreateInputField(string name, Transform parent, string placeholder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(350, 50);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.25f);

            // Text Area
            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(go.transform, false);
            var textAreaRt = textArea.AddComponent<RectTransform>();
            textAreaRt.anchorMin = Vector2.zero;
            textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = new Vector2(10, 5);
            textAreaRt.offsetMax = new Vector2(-10, -5);

            // Placeholder
            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(textArea.transform, false);
            var placeholderRt = placeholderGo.AddComponent<RectTransform>();
            placeholderRt.anchorMin = Vector2.zero;
            placeholderRt.anchorMax = Vector2.one;
            placeholderRt.offsetMin = Vector2.zero;
            placeholderRt.offsetMax = Vector2.zero;
            var placeholderTmp = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholderTmp.text = placeholder;
            placeholderTmp.fontSize = 18;
            placeholderTmp.color = new Color(0.5f, 0.5f, 0.5f);
            placeholderTmp.alignment = TextAlignmentOptions.Left;

            // Text
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(textArea.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var textTmp = textGo.AddComponent<TextMeshProUGUI>();
            textTmp.fontSize = 18;
            textTmp.color = Color.white;
            textTmp.alignment = TextAlignmentOptions.Left;

            var inputField = go.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRt;
            inputField.textComponent = textTmp;
            inputField.placeholder = placeholderTmp;

            return inputField;
        }

        private static Toggle CreateToggle(string name, Transform parent, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 40);

            var toggle = go.AddComponent<Toggle>();

            // Background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(go.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.5f);
            bgRt.anchorMax = new Vector2(0, 0.5f);
            bgRt.anchoredPosition = new Vector2(15, 0);
            bgRt.sizeDelta = new Vector2(30, 30);
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.35f);

            // Checkmark
            var checkGo = new GameObject("Checkmark");
            checkGo.transform.SetParent(bgGo.transform, false);
            var checkRt = checkGo.AddComponent<RectTransform>();
            checkRt.anchorMin = Vector2.zero;
            checkRt.anchorMax = Vector2.one;
            checkRt.offsetMin = new Vector2(5, 5);
            checkRt.offsetMax = new Vector2(-5, -5);
            var checkImage = checkGo.AddComponent<Image>();
            checkImage.color = new Color(0.2f, 0.7f, 0.3f);

            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            toggle.isOn = true;

            // Label
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0, 0);
            labelRt.anchorMax = new Vector2(1, 1);
            labelRt.offsetMin = new Vector2(45, 0);
            labelRt.offsetMax = Vector2.zero;
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = 18;
            labelTmp.color = Color.white;
            labelTmp.alignment = TextAlignmentOptions.Left;

            return toggle;
        }

        private static Slider CreateSlider(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 20);

            // Background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(go.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.25f);

            // Fill Area
            var fillAreaGo = new GameObject("Fill Area");
            fillAreaGo.transform.SetParent(go.transform, false);
            var fillAreaRt = fillAreaGo.AddComponent<RectTransform>();
            fillAreaRt.anchorMin = Vector2.zero;
            fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = Vector2.zero;
            fillAreaRt.offsetMax = Vector2.zero;

            // Fill
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImage = fillGo.AddComponent<Image>();
            fillImage.color = new Color(0.3f, 0.6f, 0.8f);

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.interactable = false;
            slider.value = 0.5f;

            return slider;
        }

        [MenuItem("BilVeFethet/Create UI Prefabs")]
        public static void CreateUIPrefabs()
        {
            CreateLeaderboardEntryPrefab();
            CreateLobbyPlayerPrefab();
            Debug.Log("UI Prefab'ları oluşturuldu!");
        }

        private static void CreateLeaderboardEntryPrefab()
        {
            var go = new GameObject("LeaderboardEntryPrefab");

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(550, 50);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

            var layoutGroup = go.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.padding = new RectOffset(10, 10, 5, 5);
            layoutGroup.spacing = 20;
            layoutGroup.childAlignment = TextAnchor.MiddleLeft;

            // Rank Text
            var rankText = CreateTextElement("RankText", go.transform, "#1", 20, TextAlignmentOptions.Center);
            var rankRt = rankText.GetComponent<RectTransform>();
            rankRt.sizeDelta = new Vector2(50, 40);

            // Player Name
            var nameText = CreateTextElement("PlayerNameText", go.transform, "Oyuncu Adı", 18, TextAlignmentOptions.Left);
            var nameRt = nameText.GetComponent<RectTransform>();
            nameRt.sizeDelta = new Vector2(300, 40);

            // Score
            var scoreText = CreateTextElement("ScoreText", go.transform, "1000 TP", 18, TextAlignmentOptions.Right);
            var scoreRt = scoreText.GetComponent<RectTransform>();
            scoreRt.sizeDelta = new Vector2(120, 40);

            // Save as prefab
            string path = "Assets/-PREFAB/UI";
            if (!AssetDatabase.IsValidFolder(path))
            {
                if (!AssetDatabase.IsValidFolder("Assets/-PREFAB"))
                {
                    AssetDatabase.CreateFolder("Assets", "-PREFAB");
                }
                AssetDatabase.CreateFolder("Assets/-PREFAB", "UI");
            }

            PrefabUtility.SaveAsPrefabAsset(go, path + "/LeaderboardEntryPrefab.prefab");
            Object.DestroyImmediate(go);
        }

        private static void CreateLobbyPlayerPrefab()
        {
            var go = new GameObject("LobbyPlayerPrefab");

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(350, 60);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);

            var layoutGroup = go.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.padding = new RectOffset(15, 15, 10, 10);
            layoutGroup.spacing = 15;
            layoutGroup.childAlignment = TextAnchor.MiddleLeft;

            // Player Name
            var nameText = CreateTextElement("PlayerNameText", go.transform, "Oyuncu (Host)", 20, TextAlignmentOptions.Left);
            var nameRt = nameText.GetComponent<RectTransform>();
            nameRt.sizeDelta = new Vector2(200, 40);

            // Ready Status
            var statusText = CreateTextElement("StatusText", go.transform, "Hazır", 18, TextAlignmentOptions.Right);
            var statusRt = statusText.GetComponent<RectTransform>();
            statusRt.sizeDelta = new Vector2(100, 40);
            statusText.color = new Color(0.3f, 0.8f, 0.3f);

            // Save as prefab
            string path = "Assets/-PREFAB/UI";
            if (!AssetDatabase.IsValidFolder(path))
            {
                if (!AssetDatabase.IsValidFolder("Assets/-PREFAB"))
                {
                    AssetDatabase.CreateFolder("Assets", "-PREFAB");
                }
                AssetDatabase.CreateFolder("Assets/-PREFAB", "UI");
            }

            PrefabUtility.SaveAsPrefabAsset(go, path + "/LobbyPlayerPrefab.prefab");
            Object.DestroyImmediate(go);
        }

        #endregion
#endif
    }
}
