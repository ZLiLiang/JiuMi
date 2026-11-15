using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Installs, reuses and removes the capture root certificate in the current-user stores.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsWeChatCertificateStore
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsWeChatCertificateStore"/> class.
    /// </summary>
    /// <param name="timeProvider">Supplies the current time when judging expiry.</param>
    public WindowsWeChatCertificateStore(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets a value indicating whether a usable, trusted capture certificate already exists.
    /// </summary>
    public bool IsInstalled()
    {
        using X509Certificate2? certificate = FindUsablePersonalCertificate();
        return certificate is not null && IsTrusted(certificate);
    }

    /// <summary>
    /// Returns the existing unexpired certificate or creates and trusts a new one.
    /// Adding to the root store shows the Windows trust prompt.
    /// </summary>
    /// <returns>The certificate with its private key.</returns>
    public X509Certificate2 EnsureInstalled()
    {
        X509Certificate2? existing = FindUsablePersonalCertificate();
        if (existing is not null)
        {
            try
            {
                EnsureTrusted(existing);
                return existing;
            }
            catch
            {
                existing.Dispose();
                throw;
            }
        }

        RemoveAll();
        X509Certificate2 created = WeChatRootCertificateFactory.Create(_timeProvider.GetUtcNow());
        try
        {
            AddPersonal(created);
            AddTrusted(created);
            return created;
        }
        catch
        {
            created.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Removes every capture certificate from the root and personal stores.
    /// </summary>
    public static void Uninstall() => RemoveAll();

    private X509Certificate2? FindUsablePersonalCertificate()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        DateTimeOffset now = _timeProvider.GetLocalNow();
        X509Certificate2? best = null;
        foreach (X509Certificate2 candidate in store.Certificates)
        {
            bool usable = IsCaptureCertificate(candidate) &&
                candidate.HasPrivateKey &&
                candidate.NotAfter > now.DateTime;
            if (!usable)
            {
                candidate.Dispose();
                continue;
            }

            if (best is null || candidate.NotAfter > best.NotAfter)
            {
                best?.Dispose();
                best = candidate;
            }
            else
            {
                candidate.Dispose();
            }
        }

        return best;
    }

    private static bool IsTrusted(X509Certificate2 certificate)
    {
        using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        return store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, validOnly: false).Count > 0;
    }

    private static void AddPersonal(X509Certificate2 certificate)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        if (store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, validOnly: false).Count == 0)
        {
            store.Add(certificate);
        }
    }

    private static void EnsureTrusted(X509Certificate2 certificate)
    {
        if (IsTrusted(certificate))
        {
            return;
        }

        AddTrusted(certificate);
    }

    private static void AddTrusted(X509Certificate2 certificate)
    {
        using var publicCopy = X509CertificateLoader.LoadCertificate(certificate.RawData);
        using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        if (store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, validOnly: false).Count == 0)
        {
            store.Add(publicCopy);
        }
    }

    private static void RemoveAll()
    {
        RemoveFrom(StoreName.Root);
        RemoveFrom(StoreName.My);
    }

    private static void RemoveFrom(StoreName storeName)
    {
        using var store = new X509Store(storeName, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        var matches = new List<X509Certificate2>();
        foreach (X509Certificate2 candidate in store.Certificates)
        {
            if (IsCaptureCertificate(candidate))
            {
                matches.Add(candidate);
            }
            else
            {
                candidate.Dispose();
            }
        }

        foreach (X509Certificate2 certificate in matches)
        {
            store.Remove(certificate);
            certificate.Dispose();
        }
    }

    private static bool IsCaptureCertificate(X509Certificate2 certificate)
    {
        return string.Equals(
            certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false),
            WeChatRootCertificateFactory.CommonName,
            StringComparison.OrdinalIgnoreCase);
    }
}
