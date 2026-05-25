using System.DirectoryServices;

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
                Filter = filter,
                PageSize = 1000,
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

    // Аналог Invoke-ADOperation (с авто-запросом прав)
    public static bool InvokeADOperation(
        string domain,
        string dn,
        Action<DirectoryEntry> operation,
        IWin32Window owner)
    {
        try
        {
            using var entry = new DirectoryEntry($"LDAP://{domain}/{dn}");
            operation(entry);
            return true;
        }
        catch (UnauthorizedAccessException)
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
                    cred.UserName,
                    cred.Password,
                    AuthenticationTypes.Secure);
                operation(entry);
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
}