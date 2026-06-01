using System.DirectoryServices;
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

    // Аналог Get-CNFromDN
    public static string GetCNFromDN(string dn)
    {
        if (string.IsNullOrEmpty(dn)) return "";
        var match = System.Text.RegularExpressions.Regex.Match(dn, @"^CN=([^,]+)");
        return match.Success ? match.Groups[1].Value : dn;
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
                Logger.Write($"Ошибка операции AD: {ex.Message}", LogType.Error);
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
                Logger.Write($"Ошибка с введёнными реквизитами: {ex2.Message}", LogType.Error);
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.Write($"Ошибка операции AD: {ex.Message}", LogType.Error);
            return false;
        }
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

    // Проверяет, является ли исключение ошибкой доступа (в т.ч. через TargetInvocationException от ADSI Invoke)
    private static bool IsAccessDenied(Exception ex)
    {
        if (ex is UnauthorizedAccessException) return true;

        if (ex is TargetInvocationException { InnerException: not null } tie)
        {
            return tie.InnerException is UnauthorizedAccessException ||
                   (tie.InnerException is COMException comEx &&
                    (uint)comEx.ErrorCode == 0x80070005);  // E_ACCESSDENIED
        }

        return false;
    }
}
