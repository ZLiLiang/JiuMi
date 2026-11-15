using System.Security.Cryptography.X509Certificates;
using JiuMi.Infrastructure.WeChatVideo;

namespace JiuMi.Infrastructure.Tests;

public sealed class WeChatRootCertificateFactoryTests
{
    [Fact]
    public void Create_ReturnsFiveYearCertificateAuthorityWithPrivateKey()
    {
        var now = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

        using X509Certificate2 certificate = WeChatRootCertificateFactory.Create(now);

        Assert.True(certificate.HasPrivateKey);
        Assert.Equal(
            WeChatRootCertificateFactory.CommonName,
            certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false));
        Assert.Equal(now.AddYears(5).Date, certificate.NotAfter.ToUniversalTime().Date);
        Assert.True(certificate.Extensions.OfType<X509BasicConstraintsExtension>().Single().CertificateAuthority);
    }

    [Fact]
    public void Create_ReturnsCertificateThatCanSignLeafCertificates()
    {
        using X509Certificate2 root = WeChatRootCertificateFactory.Create(DateTimeOffset.UtcNow);
        using var key = root.GetRSAPrivateKey();

        Assert.NotNull(key);
        Assert.InRange(key!.KeySize, 2048, 4096);
    }
}
