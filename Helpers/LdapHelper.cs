using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ADManager.Helpers;

public static class LdapHelper
{
    // Аналог Get-LdapSearcher
    public static DirectorySearcher? CreateSearcher(
        string domain,
        string filter,
        string[] properties,
        System.Net.NetworkCredential? credential = null)
    {
        try
        {
            DirectoryEntry entry = credential != null
                ? new DirectoryEntry(
                    $"LDAP://{domain}",
                    credential.UserName,
                    credential.Password,
                    AuthenticationTypes.Secure)
                : new DirectoryEntry($"LDAP://{domain}");

            var searcher = new DirectorySearcher(entry)
            {
                Filter    = filter,
                PageSize  = 1000,
                SizeLimit = 0
            };

            foreach (var prop in properties)
                searcher.PropertiesToLoad.Add(prop);

            return searcher;
        }
        catch (Exception ex)
        {
            Logger.Write($"Ошибка создания поисковика для {domain}: {ex.Message}", LogType.Error);
            return null;
        }
    }

    // Аналог Get-PropValue
    public static string GetProp(SearchResult result, string property, string defaultVal = "")
    {
        try
        {
            var col = result.Properties[property];
            if (col == null || col.Count == 0) return defaultVal;
            return col[0]?.ToString() ?? defaultVal;
        }
        catch { return defaultVal; }
    }

    public static long GetPropLong(SearchResult result, string property, long defaultVal = 0)
    {
        try
        {
            var col = result.Properties[property];
            if (col == null || col.Count == 0) return defaultVal;
            return Convert.ToInt64(col[0]);
        }
        catch { return defaultVal; }
    }

    // Аналог ConvertTo-DateTime
    public static DateTime? FileTimeToDateTime(long fileTime)
    {
        try
        {
            if (fileTime <= 0 || fileTime == long.MaxValue) return null;
            return DateTime.FromFileTime(fileTime);
        }
        catch { return null; }
    }

    // Экранирует спецсимволы LDAP-фильтра согласно RFC 4515
    public static string EscapeLdapFilter(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (char c in value)
        {
            sb.Append(c switch
            {
                '\\' => "\\5C",
                '*'  => "\\2A",
                '('  => "\\28",
                ')'  => "\\29",
                '\0' => "\\00",
                _    => c.ToString()
            });
        }
        return sb.ToString();
    }

    // Аналог Get-CNFromDN
    public static string GetCNFromDN(string dn)
    {
        if (string.IsNullOrEmpty(dn)) return "";
        var match = System.Text.RegularExpressions.Regex.Match(dn, @"^CN=([^,]+)");
        return match.Success ? match.Groups[1].Value : dn;
    }

    // Записывает LargeInteger-атрибут (accountExpires, lockoutTime и т.д.) через IADsLargeInteger COM-объект.
    // Прямое присвоение long вызывает E_FAIL (0x80004005), т.к. ADSI не конвертирует Int64 в IADsLargeInteger.
    public static void SetLargeInteger(DirectoryEntry entry, string attribute, long value)
    {
        var largeIntType = Type.GetTypeFromProgID("LargeInteger")
            ?? throw new COMException("Не удалось создать COM-тип LargeInteger (ActiveDs не зарегистрирован).");
        var obj = Activator.CreateInstance(largeIntType)!;
        largeIntType.InvokeMember("HighPart", BindingFlags.SetProperty, null, obj, [(int)(value >> 32)]);
        largeIntType.InvokeMember("LowPart",  BindingFlags.SetProperty, null, obj, [(int)(value & 0xFFFFFFFF)]);
        entry.Properties[attribute].Value = obj;
    }

    // Аналог Invoke-ADOperation (с авто-запросом прав и кэшем учётных данных)
    public static bool InvokeADOperation(
        string domain,
        string dn,
        Action<DirectoryEntry> operation,
        IWin32Window owner)
    {
        // Если есть кэшированные учётные данные — пробуем ими
        var cached = CredentialCache.Get(domain);
        if (cached != null)
        {
            try
            {
                using var entry = new DirectoryEntry(
                    $"LDAP://{domain}/{dn}",
                    cached.UserName, cached.Password,
                    AuthenticationTypes.Secure);
                operation(entry);
                return true;
            }
            catch (Exception ex) when (IsAccessDenied(ex))
            {
                CredentialCache.Invalidate(domain);
                // кэш устарел — продолжаем стандартным путём
            }
            catch (Exception ex)
            {
                Logger.Write(FormatAdError("Ошибка операции AD (кэш)", ex), LogType.Error);
                return false;
            }
        }

        // Стандартный путь: пробуем без повышенных прав
        try
        {
            using var entry = new DirectoryEntry($"LDAP://{domain}/{dn}");
            operation(entry);
            return true;
        }
        catch (Exception ex) when (IsAccessDenied(ex))
        {
            Logger.Write("Недостаточно прав. Введите учётные данные администратора.", LogType.Warning);
            using var credDlg = new Dialogs.AdminCredentialsDialog(domain);
            if (credDlg.ShowDialog(owner) != DialogResult.OK)
            {
                Logger.Write("Операция отменена пользователем.", LogType.Inactive);
                return false;
            }
            try
            {
                var cred = credDlg.Credential!;
                using var entry = new DirectoryEntry(
                    $"LDAP://{domain}/{dn}",
                    cred.UserName, cred.Password,
                    AuthenticationTypes.Secure);
                operation(entry);
                CredentialCache.Store(domain, cred);
                Logger.Write("Учётные данные сохранены до конца текущего дня.", LogType.Info);
                return true;
            }
            catch (Exception ex2)
            {
                Logger.Write(FormatAdError("Ошибка с введёнными реквизитами", ex2), LogType.Error);
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.Write(FormatAdError("Ошибка операции AD", ex), LogType.Error);
            return false;
        }
    }

    // Форматирует ошибку AD с HRESULT и цепочкой вложенных исключений
    public static string FormatAdError(string prefix, Exception ex)
    {
        var parts = new System.Text.StringBuilder();
        parts.Append(prefix).Append(": ");

        Exception? cur = ex;
        int depth = 0;
        while (cur != null && depth < 4)
        {
            if (depth > 0) parts.Append(" → ");
            parts.Append(cur.Message);
            if (cur is COMException com)
                parts.Append($" [0x{(uint)com.ErrorCode:X8}]");
            cur = cur.InnerException;
            depth++;
        }
        return parts.ToString();
    }

    // Смена пароля через WinNT-провайдер — работает без LDAPS.
    // LDAP-провайдер требует SSL (порт 636) или Kerberos (порт 464);
    // при их отсутствии entry.Invoke("SetPassword") бросает E_ADS_BAD_PATHNAME (0x80005000).
    public static bool InvokeSetPassword(
        string domain, string sam, string dn,
        string newPassword, bool mustChange,
        IWin32Window owner)
    {
        var cached = CredentialCache.Get(domain);
        if (cached != null)
        {
            try   { SetPasswordCore(domain, sam, dn, newPassword, mustChange, cached); return true; }
            catch (Exception ex) when (IsAccessDenied(ex)) { CredentialCache.Invalidate(domain); }
            catch (Exception ex)
            {
                Logger.Write(FormatAdError("Ошибка задания пароля (кэш)", ex), LogType.Error);
                return false;
            }
        }

        try
        {
            SetPasswordCore(domain, sam, dn, newPassword, mustChange, null);
            return true;
        }
        catch (Exception ex) when (IsAccessDenied(ex))
        {
            Logger.Write("Недостаточно прав. Введите учётные данные администратора.", LogType.Warning);
            using var credDlg = new Dialogs.AdminCredentialsDialog(domain);
            if (credDlg.ShowDialog(owner) != DialogResult.OK)
            {
                Logger.Write("Операция отменена пользователем.", LogType.Inactive);
                return false;
            }
            try
            {
                var cred = credDlg.Credential!;
                SetPasswordCore(domain, sam, dn, newPassword, mustChange, cred);
                CredentialCache.Store(domain, cred);
                Logger.Write("Учётные данные сохранены до конца текущего дня.", LogType.Info);
                return true;
            }
            catch (Exception ex2)
            {
                Logger.Write(FormatPasswordError("Ошибка с введёнными реквизитами", ex2), LogType.Error);
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.Write(FormatPasswordError("Ошибка смены пароля", ex), LogType.Error);
            return false;
        }
    }

    private static void SetPasswordCore(
        string domain, string sam, string dn,
        string newPassword, bool mustChange,
        NetworkCredential? cred)
    {
        // PrincipalContext с Negotiate+Sealing+Signing — рекомендованный способ
        // смены паролей в AD без LDAPS: использует Kerberos GSSAPI с шифрованием.
        var opts = ContextOptions.Negotiate | ContextOptions.Sealing | ContextOptions.Signing;

        using var ctx = cred != null
            ? new PrincipalContext(ContextType.Domain, domain, null, opts, cred.UserName, cred.Password)
            : new PrincipalContext(ContextType.Domain, domain, null, opts, null, null);

        using var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, sam)
            ?? throw new InvalidOperationException($"Пользователь {sam} не найден в {domain}.");

        user.SetPassword(newPassword);
        if (mustChange) user.ExpirePasswordNow();
        user.Save();
    }

    // Получает DN, должность, отдел, руководителя и статус активности пользователя
    public static (string DN, string Title, string Department, string Manager, bool IsEnabled) FetchUserProps(
        string domain, string sam)
    {
        try
        {
            var searcher = CreateSearcher(domain,
                $"(&(objectClass=user)(sAMAccountName={sam}))",
                new[] { "distinguishedName", "title", "department", "manager", "userAccountControl" });
            var r = searcher?.FindOne();
            if (r == null) return ("", "", "", "", false);

            string dn  = GetProp(r, "distinguishedName");
            int    uac = (int)GetPropLong(r, "userAccountControl");
            bool enabled = (uac & 2) == 0 &&
                           dn.IndexOf("OU=DisabledAccounts", StringComparison.OrdinalIgnoreCase) < 0;

            return (dn, GetProp(r, "title"), GetProp(r, "department"), GetCNFromDN(GetProp(r, "manager")), enabled);
        }
        catch { return ("", "", "", "", false); }
    }

    private static string FormatPasswordError(string prefix, Exception ex)
    {
        // Разворачиваем TargetInvocationException и показываем внутреннее исключение
        var inner = ex.InnerException ?? ex;
        string msg = inner.Message;
        string hr  = inner is COMException c ? $" (0x{(uint)c.ErrorCode:X8})" : "";
        return ex.InnerException != null
            ? $"{prefix}: {ex.Message} → {msg}{hr}"
            : $"{prefix}: {msg}{hr}";
    }

    private static readonly uint[] _accessDeniedCodes =
    {
        0x80070005,  // E_ACCESSDENIED (Win32)
        0x80072032,  // LDAP_INSUFFICIENT_ACCESS_RIGHTS (LDAP error 50)
        0x8007052E,  // ERROR_LOGON_FAILURE
        0x80005000,  // E_ADS_BAD_PATHNAME — возвращается ADSI при SetPassword без прав
    };

    // Проверяет, является ли исключение ошибкой доступа (в т.ч. через TargetInvocationException от ADSI Invoke)
    private static bool IsAccessDenied(Exception ex)
    {
        if (ex is UnauthorizedAccessException) return true;
        if (ex is COMException direct && Array.IndexOf(_accessDeniedCodes, (uint)direct.ErrorCode) >= 0) return true;

        if (ex is TargetInvocationException { InnerException: not null } tie)
        {
            return tie.InnerException is UnauthorizedAccessException ||
                   (tie.InnerException is COMException comEx &&
                    Array.IndexOf(_accessDeniedCodes, (uint)comEx.ErrorCode) >= 0);
        }

        return false;
    }
}
