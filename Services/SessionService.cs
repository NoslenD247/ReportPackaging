using System;
using System.Collections.Generic;
using System.Text;

namespace ReportPackaging.Services;
public static class SessionService
{
    public static async Task SaveSessionAsync(string token, string email)
    {
        // El Token se guarda encriptado (Keychain/Keystore)
        await SecureStorage.SetAsync("auth_token", token);

        // El email y datos no sensibles en Preferencias para acceso rápido
        Preferences.Set("user_email", email);
        Preferences.Set("is_logged_in", true);
    }
    public static void ClearSession()
    {
        SecureStorage.Remove("auth_token");
        Preferences.Clear();
    }
    public static string GetUserEmail() => Preferences.Get("user_email", string.Empty);
}
