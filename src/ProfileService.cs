using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EqualizerProfileSwitcher
{
    public sealed class ProfileService
    {
        public const string ManagedName = "equalizer-profile-switcher.txt";
        private const string Marker = "# Managed by Equalizer Profile Switcher";
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public string[] Discover(string directory)
        {
            if (!Directory.Exists(directory)) return new string[0];
            return Directory.GetFiles(directory, "*.txt")
                .Where(p => !string.Equals(Path.GetFileName(p), ManagedName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(Path.GetFileName(p), "config.txt", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => Path.GetFileName(p), StringComparer.CurrentCultureIgnoreCase).ToArray();
        }

        public async Task ApplyAsync(string configPath, string profilePath)
        {
            configPath = Path.GetFullPath(configPath);
            profilePath = Path.GetFullPath(profilePath);
            string managedPath = Path.Combine(Path.GetDirectoryName(configPath), ManagedName);
            if (profilePath.IndexOfAny(new[] { '\r', '\n', '"' }) >= 0)
                throw new InvalidOperationException("The profile path contains unsupported characters.");
            if (string.Equals(profilePath, configPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(profilePath, managedPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The configuration cannot include itself.");
            if (!File.Exists(profilePath)) throw new FileNotFoundException("Profile no longer exists.", profilePath);
            if (!File.Exists(configPath)) throw new FileNotFoundException("Select an existing Equalizer APO configuration.", configPath);

            // Acquire exclusive access to other instances of this tool. APO only reads the files.
            using (var gate = new FileStream(managedPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                byte[] original = File.ReadAllBytes(configPath);
                string content = Decode(original);
                string include = "Include: " + ManagedName;
                bool installed = content.Split('\n').Any(line => string.Equals(line.Trim(), include, StringComparison.OrdinalIgnoreCase));
                string previous = File.Exists(managedPath) ? Decode(File.ReadAllBytes(managedPath)) : null;
                if (previous != null && !previous.StartsWith(Marker + "\r\n", StringComparison.Ordinal))
                    throw new InvalidOperationException("The managed filename already exists and is not owned by this tool. Rename that file before continuing.");
                await WriteAsync(managedPath, Marker + "\r\nInclude: " + profilePath + "\r\n");
                if (!installed)
                {
                    Exception failure = null;
                    try
                    {
                        // Preserve the original bytes, BOM and line endings; append only the integration line.
                        if (!original.SequenceEqual(File.ReadAllBytes(configPath)))
                            throw new IOException("Configuration changed while applying. Please try again.");
                        Encoding encoding = DetectEncoding(original);
                        string newline = content.Contains("\r\n") ? "\r\n" : "\n";
                        byte[] addition = encoding.GetBytes((content.Length > 0 && !content.EndsWith("\n") ? newline : "")
                            + "# Equalizer Profile Switcher" + newline + include + newline);
                        await WriteBytesAsync(configPath, original.Concat(addition).ToArray());
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                    if (failure != null)
                    {
                        if (previous != null) await WriteAsync(managedPath, previous);
                        else File.Delete(managedPath);
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
                    }
                }
            }
        }

        public string CurrentProfile(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath)) return null;
            if (!File.Exists(configPath) || !Decode(File.ReadAllBytes(configPath)).Split('\n').Any(line =>
                string.Equals(line.Trim(), "Include: " + ManagedName, StringComparison.OrdinalIgnoreCase))) return null;
            string managed = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configPath)), ManagedName);
            if (!File.Exists(managed)) return null;
            return File.ReadAllLines(managed).Where(l => l.StartsWith("Include: ", StringComparison.Ordinal))
                .Select(l => l.Substring(9)).FirstOrDefault();
        }

        private static Encoding DetectEncoding(byte[] data)
        {
            if (data.Length >= 2 && data[0] == 255 && data[1] == 254) return Encoding.Unicode;
            if (data.Length >= 2 && data[0] == 254 && data[1] == 255) return Encoding.BigEndianUnicode;
            return Utf8;
        }

        private static string Decode(byte[] data)
        {
            using (var reader = new StreamReader(new MemoryStream(data), DetectEncoding(data), true))
                return reader.ReadToEnd();
        }

        private static Task WriteAsync(string path, string content)
        {
            return WriteBytesAsync(path, Utf8.GetBytes(content));
        }

        private static async Task WriteBytesAsync(string path, byte[] content)
        {
            for (int attempt = 0; ; attempt++)
            {
                string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllBytes(temp, content);
                    if (File.Exists(path))
                    {
                        string backups = Path.Combine(Path.GetDirectoryName(path), "profile-switcher-backups");
                        Directory.CreateDirectory(backups);
                        string backup = Path.Combine(backups, Path.GetFileName(path) + "." + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "." + Guid.NewGuid().ToString("N") + ".bak");
                        File.Replace(temp, path, backup);
                    }
                    else File.Move(temp, path);
                    return;
                }
                catch (IOException)
                {
                    if (attempt >= 2) throw;
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
                await Task.Delay(250 * (attempt + 1));
            }
        }
    }
}
