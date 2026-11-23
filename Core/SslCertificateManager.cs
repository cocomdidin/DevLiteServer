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

    private const string CaBundleUrl = "https://curl.se/ca/cacert.pem";
    private static readonly SemaphoreSlim CaBundleLock = new(1, 1);

    /// <summary>
    /// Gets the expected path for the Mozilla CA bundle (cacert.pem) used by PHP cURL/OpenSSL.
    /// </summary>
    public static string GetCaBundlePath(string appRoot)
    {
        return Path.Combine(appRoot, "ssl", "cacert.pem");
    }

    /// <summary>
    /// Ensures ssl/cacert.pem exists and is reasonably fresh (under 90 days old).
    /// Downloads from curl.se if missing or stale; falls back to existing file if offline.
    /// </summary>
    public static async Task<string?> EnsureCaBundleAsync(string appRoot, bool forceRefresh = false, CancellationToken ct = default)
    {
        string caPath = GetCaBundlePath(appRoot);
        if (!forceRefresh && IsValidCaBundle(caPath, TimeSpan.FromDays(90)))
        {
            return caPath;
        }

        await CaBundleLock.WaitAsync(ct);
        try
        {
            if (!forceRefresh && IsValidCaBundle(caPath, TimeSpan.FromDays(90)))
            {
                return caPath;
            }

            string? sslDir = Path.GetDirectoryName(caPath);
            if (!string.IsNullOrEmpty(sslDir) && !Directory.Exists(sslDir))
            {
                Directory.CreateDirectory(sslDir);
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.Add("User-Agent", "DevLiteServer/1.0");

            using var response = await http.GetAsync(CaBundleUrl, ct);
            response.EnsureSuccessStatusCode();

            byte[] data = await response.Content.ReadAsByteArrayAsync(ct);
            if (data.Length > 50_000)
            {
                string tempPath = caPath + ".tmp";
                await File.WriteAllBytesAsync(tempPath, data, ct);
                File.Move(tempPath, caPath, overwrite: true);
                return caPath;
            }
        }
        catch
        {
            // Fallback to existing bundle even if older than 90 days when offline
        }
        finally
        {
            CaBundleLock.Release();
        }

        return IsValidCaBundle(caPath, TimeSpan.MaxValue) ? caPath : null;
    }

    private static bool IsValidCaBundle(string caPath, TimeSpan maxAge)
    {
        try
        {
            var info = new FileInfo(caPath);
            if (!info.Exists || info.Length <= 50_000) return false;
            if (maxAge != TimeSpan.MaxValue && (DateTime.UtcNow - info.LastWriteTimeUtc) > maxAge) return false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Synchronizes LiteServer SSL certificates into Valet/Herd-compatible directories
    /// so tools like laravel-vite-plugin automatically enable HTTPS and WSS HMR.
    /// Non-blocking and never modifies or deletes certificates not issued by LiteServer.
    /// </summary>
    public static void SyncViteTlsCertificates(string appRoot, IEnumerable<SiteItem> sites, string? userProfileOverride = null)
    {
        string userHome = string.IsNullOrWhiteSpace(userProfileOverride)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : userProfileOverride;

        if (string.IsNullOrWhiteSpace(userHome))
        {
            return;
        }

        var activeSslDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var site in sites)
        {
            if (site.SslEnabled && !string.IsNullOrWhiteSpace(site.Domain))
            {
                activeSslDomains.Add(site.Domain.Trim().ToLowerInvariant());
            }
        }

        var targetConfigDirs = new List<string>();
        var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddTargetDir(string path)
        {
            if (seenDirs.Add(path))
            {
                targetConfigDirs.Add(path);
            }
        }

        AddTargetDir(Path.Combine(userHome, ".config", "valet"));

        if (userProfileOverride == null)
        {
            string? herdHome = Environment.GetEnvironmentVariable("HERD_HOME");
            if (!string.IsNullOrWhiteSpace(herdHome))
            {
                string herdValetDir = Path.Combine(herdHome, "config", "valet");
                if (Directory.Exists(herdValetDir))
                {
                    AddTargetDir(herdValetDir);
                }
            }
        }

        string userHerdValetDir = Path.Combine(userHome, ".config", "herd", "config", "valet");
        if (Directory.Exists(userHerdValetDir))
        {
            AddTargetDir(userHerdValetDir);
        }

        foreach (string configDir in targetConfigDirs)
        {
            try
            {
                string certDir = Path.Combine(configDir, "Certificates");
                string configJsonPath = Path.Combine(configDir, "config.json");

                if (activeSslDomains.Count > 0)
                {
                    Directory.CreateDirectory(certDir);

                    if (!File.Exists(configJsonPath))
                    {
                        File.WriteAllText(configJsonPath, "{\n  \"tld\": \"test\"\n}\n");
                    }

                    foreach (string domain in activeSslDomains)
                    {
                        string srcCert = Path.Combine(appRoot, "ssl", $"{domain}.crt");
                        string srcKey = Path.Combine(appRoot, "ssl", $"{domain}.key");

                        if (File.Exists(srcCert) && File.Exists(srcKey))
                        {
                            CopyFileIfChanged(srcCert, Path.Combine(certDir, $"{domain}.crt"));
                            CopyFileIfChanged(srcKey, Path.Combine(certDir, $"{domain}.key"));
                        }
                    }
                }

                if (Directory.Exists(certDir))
                {
                    foreach (string crtPath in Directory.EnumerateFiles(certDir, "*.crt"))
                    {
                        string domain = Path.GetFileNameWithoutExtension(crtPath).ToLowerInvariant();
                        if (!activeSslDomains.Contains(domain) && IsLiteServerIssuedCert(crtPath))
                        {
                            File.Delete(crtPath);
                            string keyPath = Path.Combine(certDir, $"{domain}.key");
                            if (File.Exists(keyPath))
                            {
                                File.Delete(keyPath);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Non-blocking: Vite TLS certificate sync must never prevent vhost generation
            }
        }
    }

    private static bool IsLiteServerIssuedCert(string certPath)
    {
        try
        {
            using var cert = X509CertificateLoader.LoadCertificateFromFile(certPath);
            return cert.Issuer.Contains("LiteServer", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void CopyFileIfChanged(string sourcePath, string destPath)
    {
        if (File.Exists(destPath))
        {
            var srcInfo = new FileInfo(sourcePath);
            var dstInfo = new FileInfo(destPath);
            if (srcInfo.Length == dstInfo.Length &&
                File.ReadAllBytes(sourcePath).AsSpan().SequenceEqual(File.ReadAllBytes(destPath)))
            {
                return;
            }
        }

        File.Copy(sourcePath, destPath, overwrite: true);
    }
}
