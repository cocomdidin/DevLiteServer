using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DevLiteServer.Core;

public static class SslCertificateManager
{
    /// <summary>
    /// Generates or returns an existing self-signed SSL certificate and private key in PEM format for the specified domain.
    /// Returns (certFilePath, keyFilePath) using forward slashes for Nginx compatibility.
    /// </summary>
    public static (string CertPath, string KeyPath) EnsureCertificate(string appRoot, string domain)
    {
        string sslDir = Path.Combine(appRoot, "ssl");
        if (!Directory.Exists(sslDir))
        {
            Directory.CreateDirectory(sslDir);
        }

        string cleanDomain = domain.Trim().ToLowerInvariant();
        string certFile = Path.Combine(sslDir, $"{cleanDomain}.crt");
        string keyFile = Path.Combine(sslDir, $"{cleanDomain}.key");

        if (File.Exists(certFile) && File.Exists(keyFile))
        {
            return (certFile.Replace('\\', '/'), keyFile.Replace('\\', '/'));
        }

        try
        {
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest(
                $"CN={cleanDomain}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // SAN extension
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(cleanDomain);
            if (!cleanDomain.StartsWith("*."))
            {
                sanBuilder.AddDnsName($"*.{cleanDomain}");
            }
            req.CertificateExtensions.Add(sanBuilder.Build());

            // Basic Constraints (End Entity, not CA)
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

            // Key Usage
            req.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));

            // Enhanced Key Usage: Server Authentication
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

            using var cert = req.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(5));

            string certPem = cert.ExportCertificatePem();
            string keyPem = rsa.ExportPkcs8PrivateKeyPem();

            File.WriteAllText(certFile, certPem);
            File.WriteAllText(keyFile, keyPem);
        }
        catch (Exception ex)
        {
            // If generation fails, throw descriptive exception
            throw new InvalidOperationException($"Failed to generate SSL certificate for {cleanDomain}: {ex.Message}", ex);
        }

        return (certFile.Replace('\\', '/'), keyFile.Replace('\\', '/'));
    }
}
