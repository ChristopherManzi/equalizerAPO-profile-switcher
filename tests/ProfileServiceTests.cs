using System;
using System.IO;
using System.Linq;
using System.Text;
using EqualizerProfileSwitcher;

internal static class ProfileServiceTests
{
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }

    private static int Main()
    {
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profile-switcher-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string config = Path.Combine(root, "config.txt");
            string profiles = Path.Combine(root, "Profiles with spaces ü");
            Directory.CreateDirectory(profiles);
            string first = Path.Combine(profiles, "Cinema.txt");
            string second = Path.Combine(profiles, "Music.txt");
            File.WriteAllText(first, "Preamp: -3 dB");
            File.WriteAllText(second, "# No additional processing");
            var service = new ProfileService();
            Assert(service.Discover(profiles).Length == 2, "Discovers profiles dynamically");
            File.WriteAllText(Path.Combine(profiles, "ignored.md"), "text");
            File.WriteAllText(Path.Combine(profiles, "config.txt"), "text");
            Assert(service.Discover(profiles).Length == 2, "Excludes unrelated files and config.txt");
            byte[] original = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("# Existing settings ü\r\nPreamp: -6 dB")).ToArray();
            File.WriteAllBytes(config, original);
            service.ApplyAsync(config, first).GetAwaiter().GetResult();
            Assert(File.ReadAllBytes(config).Take(original.Length).SequenceEqual(original), "Preserves existing configuration bytes including BOM");
            Assert(service.CurrentProfile(config) == first, "Supports Unicode and spaces in profile paths");
            string integrated = File.ReadAllText(config);
            service.ApplyAsync(config, second).GetAwaiter().GetResult();
            Assert(File.ReadAllText(config) == integrated, "Subsequent switches do not modify the main configuration");
            Assert(service.CurrentProfile(config) == second, "Switches to the selected profile");
            string backups = Path.Combine(root, "profile-switcher-backups");
            Assert(Directory.GetFiles(backups).Any(p => File.ReadAllBytes(p).SequenceEqual(original)), "Backs up the exact original configuration");
            Assert(Directory.GetFiles(backups).Any(p => File.ReadAllText(p).Contains(first)), "Backs up the previous profile pointer");
            bool rejected = false;
            try { service.ApplyAsync(config, config).GetAwaiter().GetResult(); } catch (InvalidOperationException) { rejected = true; }
            Assert(rejected, "Rejects self inclusion");
            rejected = false;
            try { service.ApplyAsync(config, Path.Combine(profiles, "Missing.txt")).GetAwaiter().GetResult(); } catch (FileNotFoundException) { rejected = true; }
            Assert(rejected && service.CurrentProfile(config) == second, "Missing profile leaves selected profile unchanged");
            using (var locked = new FileStream(Path.Combine(root, ProfileService.ManagedName), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                rejected = false;
                try { service.ApplyAsync(config, first).GetAwaiter().GetResult(); } catch (IOException) { rejected = true; }
                Assert(rejected, "Locked files fail safely");
            }
            Assert(service.CurrentProfile(config) == second, "Failed write preserves the previous profile");
            Assert(Directory.GetFiles(root, "*.tmp").Length == 0, "Cleans up temporary files");
            File.WriteAllText(config, "# UTF16 settings\r\n", Encoding.Unicode);
            service.ApplyAsync(config, first).GetAwaiter().GetResult();
            Assert(File.ReadAllText(config).Contains("Include: " + ProfileService.ManagedName), "Preserves UTF16 encoding when integrating");
            File.Delete(first);
            Assert(service.Discover(profiles).Length == 1, "Detects removed profiles");
            File.WriteAllText(config, "# No integration yet\r\n");
            Assert(service.CurrentProfile(config) == null, "Does not show a disconnected managed file as active");
            using (var lockedConfig = new FileStream(config, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                rejected = false;
                try { service.ApplyAsync(config, second).GetAwaiter().GetResult(); } catch (IOException) { rejected = true; }
                Assert(rejected, "Reports failure when the main configuration cannot be replaced");
            }
            string managed = Path.Combine(root, ProfileService.ManagedName);
            Assert(File.ReadAllText(managed).Contains(first), "Restores the previous pointer when integration fails");
            File.Delete(managed);
            using (var lockedConfig = new FileStream(config, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                rejected = false;
                try { service.ApplyAsync(config, second).GetAwaiter().GetResult(); } catch (IOException) { rejected = true; }
                Assert(rejected && !File.Exists(managed), "Removes a newly created pointer when first integration fails");
            }
            File.WriteAllText(managed, "# User-owned configuration");
            rejected = false;
            try { service.ApplyAsync(config, second).GetAwaiter().GetResult(); } catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && File.ReadAllText(managed) == "# User-owned configuration", "Protects an existing file using the reserved name");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Directory.Delete(root, true); }
    }
}
