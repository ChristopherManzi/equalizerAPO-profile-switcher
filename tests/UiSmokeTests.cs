using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using EqualizerProfileSwitcher;

internal static class UiSmokeTests
{
    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "Cinema.txt"), "# Example");
            var settings = new Settings { ProfilesDirectory = root, ConfigPath = "" };
            using (var form = new MainForm(settings))
            {
                form.Show();
                Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-default.png"));
                }
                if (!Find(form, "Cinema")) throw new Exception("Profile button missing.");
                File.WriteAllText(Path.Combine(root, "Music.txt"), "# Example");
                var deadline = DateTime.UtcNow.AddSeconds(3);
                while (!Find(form, "Music") && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(20); }
                if (!Find(form, "Music")) throw new Exception("Dynamic refresh failed.");
                form.Size = form.MinimumSize;
                Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-compact.png"));
                }
                form.Close();
            }
            Console.WriteLine("PASS: Desktop window renders at default and minimum sizes; profile buttons refresh dynamically.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Directory.Delete(root, true); }
    }

    private static bool Find(Control root, string text)
    {
        return root.Controls.Cast<Control>().Any(c => c.Text == text || Find(c, text));
    }
}
