using System;
using System.Collections.Generic;

namespace BilVeFethet.Auth
{
    /// <summary>
    /// Kimlik doğrulama veri modelleri
    /// </summary>

    #region Request Models

    [Serializable]
    public class RegisterRequest
    {
        public string email;
        public string username;
        public string password;
        public string displayName;
        public string turnstileToken;
    }

    [Serializable]
    public class LoginRequest
    {
        public string identifier; // email veya username
        public string password;
        public string turnstileToken;
    }

    [Serializable]
    public class RefreshTokenRequest
    {
        public string refreshToken;
        public string sessionId;
    }

    [Serializable]
    public class ForgotPasswordRequest
    {
        public string email;
    }

    [Serializable]
    public class ResetPasswordRequest
    {
        public string token;
        public string newPassword;
    }

    #endregion

    #region Response Models

    [Serializable]
    public class ApiResponse<T>
    {
        public bool success;
        public string message;
        public string error;
        public T data;
    }

    [Serializable]
    public class AuthResponseData
    {
        public string userId;
        public string email;
        public string username;
        public string displayName;
        public string avatarUrl;
        public int level;
        public int totalTP;
        public int weeklyTP;
        public int goldCoins;
        public int gameRights;
        public bool emailVerified;
        public Dictionary<string, int> jokerCounts;
        public string token;
        public string refreshToken;
        public string sessionId;
    }

    [Serializable]
    public class TokenRefreshData
    {
        public string token;
    }

    [Serializable]
    public class ProfileData
    {
        public string id;
        public string email;
        public string username;
        public string display_name;
        public string avatar_url;
        public int level;
        public int total_tp;
        public int weekly_tp;
        public int gold_coins;
        public int game_rights;
        public int current_win_streak;
        public int current_undefeated_streak;
        public int max_win_streak;
        public int max_undefeated_streak;
        public int total_games_played;
        public int total_wins;
        public int total_correct_answers;
        public int total_wrong_answers;
        public bool emailVerified;
        public string created_at;
        public string last_login;
        public Dictionary<string, int> jokerCounts;
        public Dictionary<string, int> guardianCounts;
        public ProfileSettings settings;
    }

    [Serializable]
    public class ProfileSettings
    {
        public bool soundEnabled = true;
        public bool musicEnabled = true;
        public bool notificationsEnabled = true;
        public bool vibrationEnabled = true;
        public string language = "tr";
    }

    #endregion

    #region Storage Models

    [Serializable]
    public class StoredAuthData
    {
        public string userId;
        public string email;
        public string username;
        public string displayName;
        public string token;
        public string refreshToken;
        public string sessionId;
        public long tokenExpiry; // Unix timestamp
        public long lastLogin;
    }

    #endregion

    #region Enums

    public enum AuthState
    {
        Unknown,
        NotLoggedIn,
        LoggingIn,
        LoggedIn,
        Refreshing,
        Error
    }

    public enum AuthError
    {
        None,
        NetworkError,
        InvalidCredentials,
        UserNotFound,
        EmailAlreadyExists,
        UsernameAlreadyExists,
        WeakPassword,
        TokenExpired,
        Banned,
        ServerError,
        Unknown
    }

    #endregion

    #region Event Args

    public class AuthStateChangedEventArgs : EventArgs
    {
        public AuthState OldState { get; }
        public AuthState NewState { get; }
        public AuthError Error { get; }
        public string ErrorMessage { get; }

        public AuthStateChangedEventArgs(AuthState oldState, AuthState newState, AuthError error = AuthError.None, string errorMessage = null)
        {
            OldState = oldState;
            NewState = newState;
            Error = error;
            ErrorMessage = errorMessage;
        }
    }

    public class LoginEventArgs : EventArgs
    {
        public bool Success { get; }
        public AuthResponseData Data { get; }
        public AuthError Error { get; }
        public string ErrorMessage { get; }

        public LoginEventArgs(bool success, AuthResponseData data = null, AuthError error = AuthError.None, string errorMessage = null)
        {
            Success = success;
            Data = data;
            Error = error;
            ErrorMessage = errorMessage;
        }
    }

    public class RegisterEventArgs : EventArgs
    {
        public bool Success { get; }
        public AuthResponseData Data { get; }
        public AuthError Error { get; }
        public string ErrorMessage { get; }

        public RegisterEventArgs(bool success, AuthResponseData data = null, AuthError error = AuthError.None, string errorMessage = null)
        {
            Success = success;
            Data = data;
            Error = error;
            ErrorMessage = errorMessage;
        }
    }

    #endregion
}
