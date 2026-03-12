using System;
using System.Collections.Generic;
using System.Text;

namespace ReportPackaging.Config
{
    public static class AuthConfig
    {
        // Credenciales de Clerk (Public Client)
        public const string ClientId = "LiP7OOFeag52xKMm";
        public const string Domain = "clerk.apparellinks.app";
        public const string RedirectUri = "packingreportscan://callback";
        // Endpoints
        public const string AuthorizeUrl = $"https://{Domain}/oauth/authorize";
        public const string TokenUrl = $"https://{Domain}/oauth/token";
        public const string UserInfoUrl = $"https://{Domain}/oauth/userinfo";
    }
}
