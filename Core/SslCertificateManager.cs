using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DevLiteServer.Core;

public static class SslCertificateManager
{
    private const string RootCaSubject = "CN=LiteServer Development Root CA, O=LiteServer, OU=Local Development";

    /// <summary>
    /// Generates or returns an existing SSL certificate and private key for the specified domain,
    /// signed by a trusted LiteServer Development Root CA.
    /// Also ensures the Root CA is registered in the current user's Trusted Root Certification Authorities store.
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

        // Ensure Root CA exists and is trusted
        using var caCert = EnsureRootCertificateAuthority(sslDir);
        EnsureRootCaTrusted(caCert);

        // Check if certificate already exists and was issued by LiteServer Root CA
        if (File.Exists(certFile) && File.Exists(keyFile))
        {
            try
            {
                using var existingCert = X509CertificateLoader.LoadCertificateFromFile(certFile);
                if (existingCert.Issuer.Contains("LiteServer") && existingCert.NotAfter > DateTime.UtcNow.AddDays(14))
                {
                    return (certFile.Replace('\\', '/'), keyFile.Replace('\\', '/'));
                }
            }
            catch
            {
                // If existing cert is unreadable or old self-signed cert, fall through to regenerate
            }
        }

        try
        {
            using var leafRsa = RSA.Create(2048);
            var req = new CertificateRequest(
                $"CN={cleanDomain}",
                leafRsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Subject Alternative Names (SAN)
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(cleanDomain);
            if (!cleanDomain.StartsWith("*.") && !IPAddress.TryParse(cleanDomain, out _))
            {
                sanBuilder.AddDnsName($"*.{cleanDomain}");
            }
            if (cleanDomain.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                sanBuilder.AddIpAddress(IPAddress.Loopback);
                sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
            }
            req.CertificateExtensions.Add(sanBuilder.Build());

            // Basic Constraints (End Entity, not CA)
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

            // Key Usage
            req.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));

            // Enhanced Key Usage: Server Authentication + Client Authentication
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection
                {
                    new Oid("1.3.6.1.5.5.7.3.1"), // Server Authentication
                    new Oid("1.3.6.1.5.5.7.3.2")  // Client Authentication
                }, false));

            // Authority Key Identifier linking to Root CA
            req.CertificateExtensions.Add(
                X509AuthorityKeyIdentifierExtension.CreateFromCertificate(caCert, true, false));

            // Random serial number
            byte[] serialNumber = new byte[16];
            RandomNumberGenerator.Fill(serialNumber);

            // Sign leaf certificate using Root CA
            using var leafCert = req.Create(
                caCert,
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(2),
                serialNumber);

            string leafCertPem = leafCert.ExportCertificatePem();
            string caCertPem = caCert.ExportCertificatePem();
            string fullChainPem = $"{leafCertPem}\n{caCertPem}";
            string leafKeyPem = leafRsa.ExportPkcs8PrivateKeyPem();

            File.WriteAllText(certFile, fullChainPem);
            File.WriteAllText(keyFile, leafKeyPem);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to generate SSL certificate for {cleanDomain}: {ex.Message}", ex);
        }

        return (certFile.Replace('\\', '/'), keyFile.Replace('\\', '/'));
    }

    /// <summary>
    /// Loads or creates the local Root CA certificate and private key.
    /// </summary>
    public static X509Certificate2 EnsureRootCertificateAuthority(string sslDir)
    {
        string caDir = Path.Combine(sslDir, "ca");
        if (!Directory.Exists(caDir))
        {
            Directory.CreateDirectory(caDir);
        }

        string caCertPath = Path.Combine(caDir, "rootCA.crt");
        string caKeyPath = Path.Combine(caDir, "rootCA.key");

        if (File.Exists(caCertPath) && File.Exists(caKeyPath))
        {
            try
            {
                string certPem = File.ReadAllText(caCertPath);
                string keyPem = File.ReadAllText(caKeyPath);
                return X509Certificate2.CreateFromPem(certPem, keyPem);
            }
            catch
            {
                // Corrupted, regenerate below
            }
        }

        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            RootCaSubject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Mark as CA
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 1, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature, true));
        req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

        using var caCert = req.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(10));

        string certPemOut = caCert.ExportCertificatePem();
        string keyPemOut = rsa.ExportPkcs8PrivateKeyPem();

        File.WriteAllText(caCertPath, certPemOut);
        File.WriteAllText(caKeyPath, keyPemOut);

        return X509Certificate2.CreateFromPem(certPemOut, keyPemOut);
    }

    /// <summary>
    /// Ensures the Root CA certificate is installed in the current user's Trusted Root store.
    /// Prompts a one-time standard Windows security confirmation dialog on first install.
    /// </summary>
    public static bool EnsureRootCaTrusted(X509Certificate2 caCert)
    {
        try
        {
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);

            var existing = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                caCert.Thumbprint,
                false);

            if (existing.Count > 0)
            {
                return true;
            }

            store.Add(caCert);
            return true;
        }
        catch
        {
            // If user declines the Windows confirmation prompt or permissions are restricted
            return false;
        }
    }

    /// <summary>
    /// Checks whether the LiteServer Root CA is currently trusted in the current user's store.
    /// </summary>
    public static bool IsRootCaTrusted(string appRoot)
    {
        try
        {
            string caCertPath = Path.Combine(appRoot, "ssl", "ca", "rootCA.crt");
            if (!File.Exists(caCertPath)) return false;

            using var cert = X509CertificateLoader.LoadCertificateFromFile(caCertPath);
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            return store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false).Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the absolute path to the Root CA certificate file.
    /// </summary>
    public static string GetRootCaCertPath(string appRoot)
    {
        return Path.Combine(appRoot, "ssl", "ca", "rootCA.crt");
    }
}
