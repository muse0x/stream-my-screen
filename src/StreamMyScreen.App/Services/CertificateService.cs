using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace StreamMyScreen.App.Services;

public static class CertificateService
{
    public static X509Certificate2 GenerateSelfSignedCertificate(string localIp)
    {
        using var rsa = RSA.Create(2048);
        var certRequest = new CertificateRequest(
            "CN=StreamMyScreen",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName("StreamMyScreen");
        sanBuilder.AddIpAddress(IPAddress.Loopback);

        if (IPAddress.TryParse(localIp, out var parsedIp))
        {
            sanBuilder.AddIpAddress(parsedIp);
        }

        certRequest.CertificateExtensions.Add(sanBuilder.Build());
        certRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        certRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        certRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false)); // Server Authentication

        var cert = certRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(5));

        // Export and re-import with exportable private key for Kestrel compatibility
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }
}
