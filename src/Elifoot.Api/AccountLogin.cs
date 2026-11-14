using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Elifoot.Api.Services;
using Elifoot.Application;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace Elifoot.Api;

public static class AccountLogin
{
    public const string Scheme = "AccountTest";
    public const string CommercialMode = "Account";
    public static bool Enabled(IConfiguration configuration) => configuration["Elifoot:AuthMode"] is Scheme or CommercialMode;
    public static Guid AccountId(HttpContext http) => Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static Guid CoachId(HttpContext http) => Guid.Parse(http.User.FindFirstValue("coach")!);
    public static string SessionToken(HttpContext http) => http.User.FindFirstValue("sid")!;

    public static bool TryOwner(HttpContext http, IConfiguration config, out Guid owner)
    {
        owner = Guid.Empty;
        if (Enabled(config)) return http.User.Identity?.IsAuthenticated == true && Guid.TryParse(http.User.FindFirstValue("coach"), out owner);
        if (config["Elifoot:AuthMode"] is not (null or "LocalDev")) return false;
        var auth = http.Request.Headers.Authorization.ToString();
        return auth.StartsWith("Bearer localdev-", StringComparison.Ordinal) && Guid.TryParseExact(auth[16..], "N", out owner) && owner != Guid.Empty;
    }

    public static void AddAccountLogin(this WebApplicationBuilder builder)
    {
        if (!Enabled(builder.Configuration)) return;
        bool commercial = builder.Configuration["Elifoot:AuthMode"] == CommercialMode;
        if (builder.Environment.IsProduction() && !commercial) throw new InvalidOperationException("AccountTest é exclusivo para ambiente local/de teste.");
        if (string.Equals(builder.Configuration["Elifoot:DatabaseProvider"], "InMemory", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A autenticação por conta requer persistência.");
        builder.Services.AddScoped<AccountSessionService>();
        builder.Services.AddSingleton<IPasswordHasher<AccountRecord>, PasswordHasher<AccountRecord>>();
        builder.Services.AddSingleton<AccountConnections>();
        var protection = builder.Services.AddDataProtection().SetApplicationName($"LigaClassica98.{(commercial ? CommercialMode : Scheme)}");
        if (commercial)
        {
            var origins = builder.Configuration.GetSection("Accounts:AllowedOrigins").Get<string[]>();
            if (origins is not { Length: > 0 } || origins.Any(value => !Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 || uri.GetLeftPart(UriPartial.Authority) != value))
                throw new InvalidOperationException("Configure Accounts:AllowedOrigins com origens HTTPS explícitas.");
            var keys = builder.Configuration["Accounts:KeyDirectory"];
            var certificate = builder.Configuration["Accounts:KeyCertificate"];
            if (string.IsNullOrWhiteSpace(keys) || !Path.IsPathFullyQualified(keys) || !Directory.Exists(keys)
                || string.IsNullOrWhiteSpace(certificate) || !Path.IsPathFullyQualified(certificate))
                throw new InvalidOperationException("Configure um diretório de chaves e um certificado de Data Protection por caminhos absolutos.");
            var privateKey = builder.Configuration["Accounts:KeyCertificatePemPrivateKey"];
            var keyCertificate = string.IsNullOrWhiteSpace(privateKey)
                ? X509CertificateLoader.LoadPkcs12FromFile(certificate,
                    builder.Configuration["Accounts:KeyCertificatePassword"], X509KeyStorageFlags.EphemeralKeySet)
                : X509Certificate2.CreateFromPemFile(certificate, privateKey);
            if (!keyCertificate.HasPrivateKey) throw new InvalidOperationException("O certificado de Data Protection precisa da chave privada.");
            protection.PersistKeysToFileSystem(new DirectoryInfo(keys)).ProtectKeysWithCertificate(keyCertificate);
        }
        else if (builder.Configuration["Accounts:KeyDirectory"] is { Length: > 0 } testKeys)
        {
            if (!Path.IsPathFullyQualified(testKeys) || !Directory.Exists(testKeys))
                throw new InvalidOperationException("Use um diretório de chaves de teste existente e absoluto.");
            protection.PersistKeysToFileSystem(new DirectoryInfo(testKeys));
        }
        var securePolicy = commercial ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = "lc98.csrf"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict; o.Cookie.SecurePolicy = securePolicy; });
        builder.Services.AddAuthentication(Scheme).AddCookie(Scheme, o =>
        {
            o.Cookie.Name = "lc98.account"; o.Cookie.HttpOnly = true; o.Cookie.SecurePolicy = securePolicy;
            o.Cookie.SameSite = SameSiteMode.Strict; o.ExpireTimeSpan = TimeSpan.FromHours(12); o.SlidingExpiration = false;
            o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
            o.Events.OnValidatePrincipal = async c =>
            {
                var service = c.HttpContext.RequestServices.GetRequiredService<AccountSessionService>();
                var token = c.Principal?.FindFirstValue("sid");
                var session = token == null ? null : await service.Session(token, c.HttpContext.RequestAborted);
                if (session == null || session.AccountId.ToString() != c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)) { c.RejectPrincipal(); return; }
                var identity = (ClaimsIdentity)c.Principal!.Identity!;
                foreach (var claim in identity.FindAll("coach").ToList()) identity.RemoveClaim(claim);
                identity.AddClaim(new("coach", session.CoachId.ToString()));
            };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.AddPolicy("world-invite", c => RateLimitPartition.GetFixedWindowLimiter(c.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous", _ =>
                new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                }));
            o.AddPolicy("account-auth", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
        });
    }

    public static void UseAccountLogin(this WebApplication app)
    {
        if (!Enabled(app.Configuration)) return;
        app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
        app.Use(async (http, next) =>
        {
            var path = (http.Request.Path.Value ?? "").TrimEnd('/');
            bool api = http.Request.Path.StartsWithSegments("/api");
            if (!api && !http.Request.Path.StartsWithSegments("/hubs")) { await next(); return; }
            if (app.Configuration["Elifoot:AuthMode"] == CommercialMode && !http.Request.IsHttps)
            { http.Response.StatusCode = 400; return; }
            http.Response.Headers.CacheControl = "no-store";
            var origin = http.Request.Headers.Origin.ToString();
            var allowed = app.Configuration.GetSection("Accounts:AllowedOrigins").Get<string[]>() ?? ["http://127.0.0.1:5242", "http://localhost:5242"];
            if (origin.Length > 0 && !allowed.Contains(origin, StringComparer.OrdinalIgnoreCase)) { http.Response.StatusCode = 403; return; }
            if (path.Equals("/api/auth/local-login", StringComparison.OrdinalIgnoreCase)) { http.Response.StatusCode = 404; return; }
            var anonymous = path is "/api/health" or "/api/auth/csrf" or "/api/auth/login" or "/api/auth/register";
            if (!anonymous && http.User.Identity?.IsAuthenticated != true) { http.Response.StatusCode = 401; return; }
            if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method) && !HttpMethods.IsOptions(http.Request.Method))
            {
                try { await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http); }
                catch (AntiforgeryValidationException) { http.Response.StatusCode = 400; await http.Response.WriteAsJsonAsync(new { error = "Sessão de formulário expirada. Atualize a página.", code = "CSRF_INVALID" }); return; }
            }
            if (!anonymous && api && !http.Request.Path.StartsWithSegments("/api/auth") && !http.Request.Path.StartsWithSegments("/api/accounts"))
            {
                if (!await AuthorizeRequest(http)) { http.Response.StatusCode = 403; await http.Response.WriteAsJsonAsync(new { error = "Esta conta não pode controlar esse treinador ou recurso." }); return; }
            }
            await next();
        });
    }

    private static async Task<bool> AuthorizeRequest(HttpContext http)
    {
        var service = http.RequestServices.GetRequiredService<AccountSessionService>();
        var account = AccountId(http); var actor = CoachId(http); var ct = http.RequestAborted;
        var requestedActors = new HashSet<Guid>();
        foreach (var pair in http.Request.Query.Where(x => x.Key.Equals("coachId", StringComparison.OrdinalIgnoreCase)))
            foreach (var value in pair.Value) { if (!Guid.TryParse(value, out var id)) return false; requestedActors.Add(id); }
        if (http.Request.RouteValues.TryGetValue("coachId", out var routeCoach))
        {
            if (!Guid.TryParse(routeCoach?.ToString(), out var id)) return false; requestedActors.Add(id);
        }
        if (http.Request.Query.TryGetValue("accountId", out var suppliedAccount) && suppliedAccount.ToString() != account.ToString()) return false;
        if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method) && http.Request.HasJsonContentType() && http.Request.ContentLength != 0)
        {
            http.Request.EnableBuffering();
            try
            {
                using var json = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: ct);
                if (json.RootElement.ValueKind == JsonValueKind.Object) foreach (var field in json.RootElement.EnumerateObject())
                {
                    if (field.Name.Equals("accountId", StringComparison.OrdinalIgnoreCase) &&
                        (field.Value.ValueKind != JsonValueKind.String || field.Value.GetString() != account.ToString())) return false;
                    if (field.Name.Equals("coachId", StringComparison.OrdinalIgnoreCase) || field.Name.Equals("hostCoachId", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Guid.TryParse(field.Value.ToString(), out var id)) return false; requestedActors.Add(id);
                    }
                }
            }
            catch (JsonException) { return false; }
            finally { http.Request.Body.Position = 0; }
        }
        if (requestedActors.Count > 1) return false;
        if (requestedActors.Count == 1)
        {
            actor = requestedActors.Single();
            if (!await service.Owns(account, actor, ct)) return false;
        }
        var path = http.Request.Path.Value!.TrimEnd('/');
        var query = http.Request.Query.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        if (!query.ContainsKey("coachId")) query["coachId"] = actor.ToString();
        // Identity comes from the authenticated session; legacy device values cannot establish ownership.
        query["deviceId"] = $"account:{account:N}";
        query["nickname"] = (await service.Account(account, ct)).DisplayName;
        http.Request.QueryString = QueryString.Create(query.SelectMany(x => x.Value.Select(v => new KeyValuePair<string, string?>(x.Key, v))));
        if (path.EndsWith("/tick", StringComparison.OrdinalIgnoreCase)) return false;
        var worldRoute = http.Request.RouteValues["worldId"];
        if (worldRoute == null && http.Request.Path.StartsWithSegments("/api/world-lobby-016b"))
            worldRoute = http.Request.RouteValues["id"];
        if (Guid.TryParse(worldRoute?.ToString(), out var worldId))
        {
            var repo = http.RequestServices.GetRequiredService<IWorldRepository>();
            var world = await repo.GetWorldForRoundCheckpointAsync(worldId, ct);
            if (world == null) return false;
            var profiles = await service.Profiles(account, ct); var owned = profiles.Select(x => x.CoachId).ToHashSet();
            var gate = MultiplayerRoundOrchestrator.GetRoomLock(worldId);
            await gate.WaitAsync(ct);
            try
            {
                if (path.EndsWith("/join", StringComparison.OrdinalIgnoreCase)) return true; // Admission is checked under the join lock.
                bool creator = world.CreatorCoachId is Guid host && owned.Contains(host);
                var memberships = world.HumanCoaches.Where(x => owned.Contains(x.Id)).ToList();
                if (path.Contains("/editor/", StringComparison.OrdinalIgnoreCase))
                    return creator || memberships.Any(x => x.IsHost);
                if (path.Contains("/finances", StringComparison.OrdinalIgnoreCase) && http.Request.RouteValues.TryGetValue("clubId", out var club))
                    return memberships.Any(x => x.Id == actor && x.CurrentClubId?.ToString() == club?.ToString());
                if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method))
                    return creator || memberships.Count > 0;
                return memberships.Any(x => x.Id == actor);
            }
            finally { gate.Release(); }
        }
        return true;
    }

    public static void MapAccountLogin(this WebApplication app)
    {
        if (!Enabled(app.Configuration)) return;
        app.MapGet("/api/auth/csrf", (HttpContext http, IAntiforgery antiforgery) => Results.Ok(new { csrfToken = antiforgery.GetAndStoreTokens(http).RequestToken }));
        app.MapPost("/api/auth/register", async (RegisterAccount request, HttpContext http, AccountSessionService service, CancellationToken ct) =>
        {
            if (!ValidUsername(request.Username) || string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length is < 2 or > 60 || request.Password == null || request.Password.Length is < 10 or > 128)
                return Results.BadRequest(new { error = "Informe usuário de 3 a 80 caracteres, nome de 2 a 60 e senha de 10 a 128 caracteres." });
            var account = await service.Register(request.Username, request.DisplayName, request.Password, ct);
            if (account == null) return Results.Conflict(new { error = "Usuário indisponível. Escolha outro." });
            return await SignIn(http, service, account, ct);
        }).RequireRateLimiting("account-auth");
        app.MapPost("/api/auth/login", async (LoginAccount request, HttpContext http, AccountSessionService service, CancellationToken ct) =>
        {
            if (!ValidUsername(request.Username) || request.Password == null || request.Password.Length is < 1 or > 128) return InvalidLogin();
            var account = await service.Login(request.Username, request.Password, ct);
            return account == null ? InvalidLogin() : await SignIn(http, service, account, ct);
        }).RequireRateLimiting("account-auth");
        app.MapGet("/api/auth/session", async (HttpContext http, AccountSessionService service, CancellationToken ct) => await View(http, service, ct));
        app.MapPost("/api/auth/logout", async (HttpContext http, AccountSessionService service, AccountConnections connections, CancellationToken ct) =>
        {
            var token = SessionToken(http); await service.Revoke(token, ct); connections.Revoke(AccountSessionService.Hash(token));
            await http.SignOutAsync(Scheme); return Results.Ok(new { success = true });
        });
        app.MapPost("/api/auth/profile", async (SelectAccountProfile request, HttpContext http, AccountSessionService service, CancellationToken ct) =>
        {
            if (!await service.SelectProfile(SessionToken(http), request.CoachId, ct)) return Results.StatusCode(403);
            ((ClaimsIdentity)http.User.Identity!).RemoveClaim(http.User.FindFirst("coach")!);
            ((ClaimsIdentity)http.User.Identity!).AddClaim(new("coach", request.CoachId.ToString()));
            return await View(http, service, ct);
        });
        app.MapPost("/api/accounts/legacy-link", async (ConsumeLegacyLink request, HttpContext http, AccountSessionService service, CancellationToken ct) =>
        {
            if (request.Code?.Length != 64 || !await service.ConsumeGrant(AccountId(http), request.Code, ct)) return Results.Conflict(new { error = "Código de vínculo inválido, expirado ou em conflito. Nenhuma vaga foi transferida." });
            return await View(http, service, ct);
        }).RequireRateLimiting("account-auth");
        app.MapPost("/api/accounts/legacy-link-grants", async (IssueLegacyLink request, HttpContext http, AccountSessionService service, IWorldRepository worlds, CancellationToken ct) =>
        {
            var configured = app.Configuration["Accounts:MigrationAdminToken"];
            var supplied = http.Request.Headers["X-Account-Migration-Token"].ToString();
            if (string.IsNullOrWhiteSpace(configured) || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(configured)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied)))) return Results.StatusCode(403);
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 300 || !ValidUsername(request.Username)) return Results.BadRequest(new { error = "Informe conta destinatária, treinador e justificativa da autorização." });
            var code = await service.IssueGrant(request.Username, request.CoachId, request.Reason, worlds, ct);
            return code == null ? Results.Conflict(new { error = "Vínculo não autorizado ou já existente." }) : Results.Ok(new { code, expiresInMinutes = 30 });
        });
    }

    private static bool ValidUsername(string? username) => username != null && username.Trim().Length is >= 3 and <= 80 && Regex.IsMatch(username.Trim(), @"\A[a-zA-Z0-9._@+\-]+\z");
    private static IResult InvalidLogin() => Results.Json(new { error = "Credenciais inválidas ou acesso temporariamente indisponível." }, statusCode: 401);
    private static async Task<IResult> SignIn(HttpContext http, AccountSessionService service, AccountRecord account, CancellationToken ct)
    {
        var (token, expires) = await service.CreateSession(account, ct);
        var profile = (await service.Profiles(account.Id, ct)).Single(x => x.IsPrimary);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, account.Id.ToString()), new(ClaimTypes.Name, account.DisplayName), new("coach", profile.CoachId.ToString()), new("sid", token)], Scheme));
        await http.SignInAsync(Scheme, principal, new AuthenticationProperties { IsPersistent = true, ExpiresUtc = expires }); http.User = principal;
        return await View(http, service, ct);
    }
    private static async Task<IResult> View(HttpContext http, AccountSessionService service, CancellationToken ct)
    {
        var account = await service.Account(AccountId(http), ct); var profiles = await service.Profiles(account.Id, ct);
        return Results.Ok(new
        {
            accountId = account.Id,
            username = account.Username,
            displayName = account.DisplayName,
            coachId = CoachId(http),
            profiles = profiles.Select(p => new { p.CoachId, p.DisplayName, p.IsPrimary }),
            authMode = http.RequestServices.GetRequiredService<IConfiguration>()["Elifoot:AuthMode"]
        });
    }
    public sealed record RegisterAccount(string Username, string DisplayName, string Password);
    public sealed record LoginAccount(string Username, string Password);
    public sealed record SelectAccountProfile(Guid CoachId);
    public sealed record ConsumeLegacyLink(string Code);
    public sealed record IssueLegacyLink(string Username, Guid CoachId, string Reason);
}
