using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Creates the self-signed root the capture proxy signs page certificates with.
/// </summary>
internal static class WeChatRootCertificateFactory
{
    /// <summary>The common name shown in the Windows trust prompt.</summary>
    public const string CommonName = "JiuMi 微信视频下载";

    /// <summary>How long a newly created root stays valid.</summary>
    public const int ValidYears = 5;

    /// <summary>
    /// Creates a five-year self-signed certificate authority with a persisted private key.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <returns>A root certificate with its private key.</returns>
    public static X509Certificate2 Create(DateTimeOffset now)
    {
        using RSA key = RSA.Create(2048);
        var nameBuilder = new X500DistinguishedNameBuilder();
        nameBuilder.AddCommonName(CommonName);

        var request = new CertificateRequest(
            nameBuilder.Build(),
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: true,
            hasPathLengthConstraint: false,
            pathLengthConstraint: 0,
            critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature,
            critical: true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        using X509Certificate2 created = request.CreateSelfSigned(now.AddDays(-1), now.AddYears(ValidYears));
        byte[] pfx = created.Export(X509ContentType.Pfx);

        // Round-tripping with PersistKeySet keeps the private key usable after the store import.
        return X509CertificateLoader.LoadPkcs12(
            pfx,
            password: null,
            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
    }
}
