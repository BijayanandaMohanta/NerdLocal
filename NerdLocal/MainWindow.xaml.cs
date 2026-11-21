
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;

namespace NerdLocal
{
    public partial class MainWindow : Window
    {
        private const string DefaultXamppPath = @"C:\xampp";

        private readonly ObservableCollection<DomainItem> Domains =
            new ObservableCollection<DomainItem>();

        private ICollectionView _domainsView;
        private DomainItem editingDomain;
        private string currentEditingFile = "";
        private bool _isLoadingEditor;
        private bool _editorHasUnsavedChanges;
        private bool _isDarkTheme = true;
        private bool _syncingNavigation;

        private static readonly string ThemeSettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NerdLocal",
            "ui-theme.txt");

        private string HOST_FILE =
            @"C:\Windows\System32\drivers\etc\hosts";

        private string VHOST_FILE =
            @"C:\xampp\apache\conf\extra\httpd-vhosts.conf";

        private string HTTPD_FILE =
            @"C:\xampp\apache\conf\httpd.conf";

        public MainWindow()
        {
            InitializeComponent();
            ApplyTheme(LoadSavedThemePreference(), savePreference: false);

            txtHostsFile.Text = HOST_FILE;
            txtVhostFile.Text = VHOST_FILE;
            txtHttpdFile.Text = HTTPD_FILE;

            if (!IsAdministrator())
            {
                System.Windows.MessageBox.Show(
                    "Please run NerdLocal as Administrator.",
                    "Administrator Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                System.Windows.Application.Current.Shutdown();
                return;
            }

            _domainsView = CollectionViewSource.GetDefaultView(Domains);
            DomainGrid.ItemsSource = _domainsView;

            cmbPort.SelectedIndex = 0;

            txtSearch.TextChanged += TxtSearch_TextChanged;
            txtConfigEditor.TextChanged += ConfigEditor_TextChanged;

            LoadDomains();
            UpdateDomainCount();
        }

        private bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);

                return principal.IsInRole(
                    WindowsBuiltInRole.Administrator);
            }
        }

        private string HostsFilePath => txtHostsFile.Text.Trim();
        private string VHostsFilePath => txtVhostFile.Text.Trim();
        private string HttpdFilePath => txtHttpdFile.Text.Trim();

        private string GetXamppPath()
        {
            string httpd = HttpdFilePath;

            if (string.IsNullOrWhiteSpace(httpd))
                return DefaultXamppPath;

            // C:\xampp\apache\conf\httpd.conf
            DirectoryInfo directory = Directory.GetParent(httpd);

            // conf -> apache -> xampp
            if (directory != null)
                directory = directory.Parent;

            if (directory != null)
                directory = directory.Parent;

            return directory == null
                ? DefaultXamppPath
                : directory.FullName;
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Project Folder";
                dialog.ShowNewFolderButton = false;

                if (dialog.ShowDialog() ==
                    System.Windows.Forms.DialogResult.OK)
                {
                    txtPath.Text = dialog.SelectedPath;
                }
            }
        }

        private bool ValidateDomain(string domain, string path, string port)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                ShowError("Enter a domain.");
                return false;
            }

            domain = domain.Trim();

            if (domain.Length > 253 ||
                !Regex.IsMatch(
                    domain,
                    @"^(?=.{1,253}$)(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,63}$"))
            {
                ShowError("Enter a valid domain name.");
                return false;
            }

            if (!domain.EndsWith(
                    ".local", StringComparison.OrdinalIgnoreCase) &&
                !domain.EndsWith(
                    ".test", StringComparison.OrdinalIgnoreCase))
            {
                ShowError("Domain must end with .local or .test.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                ShowError("Select an existing project folder.");
                return false;
            }

            if (!int.TryParse(port, out int portNumber) ||
                portNumber < 1 || portNumber > 65535)
            {
                ShowError("Select a valid port between 1 and 65535.");
                return false;
            }

            if (!File.Exists(HostsFilePath) ||
                !File.Exists(HttpdFilePath) ||
                !File.Exists(VHostsFilePath))
            {
                ShowError(
                    "One or more configuration files do not exist.\n\n" +
                    "Check the paths in the Config Files tab.");
                return false;
            }

            return true;
        }

        private void btnAddDomain_Click(object sender, RoutedEventArgs e)
        {
            string domain = txtDomain.Text.Trim();
            string path = txtPath.Text.Trim();

            ComboBoxItem selectedPort = cmbPort.SelectedItem as ComboBoxItem;

            if (selectedPort == null)
            {
                ShowError("Select a port.");
                return;
            }

            string port = selectedPort.Content.ToString();

            if (editingDomain != null)
            {
                UpdateDomain(domain, path, port);
                return;
            }

            try
            {
                if (!ValidateDomain(domain, path, port))
                    return;

                string hosts = File.ReadAllText(HostsFilePath);
                string vhosts = File.ReadAllText(VHostsFilePath);
                string httpd = File.ReadAllText(HttpdFilePath);

                if (HasServerName(vhosts, domain))
                {
                    ShowError(
                        "This domain already exists in the Apache " +
                        "virtual-host configuration.");
                    return;
                }

                if (HasHostsMapping(hosts, domain))
                {
                    ShowError(
                        "This domain already has an entry in the Hosts file. " +
                        "Review the existing configuration first.");
                    return;
                }

                string updatedHosts = AddHostsMapping(hosts, domain);
                string updatedHttpd = AddListenPort(httpd, port);
                string updatedVhosts =
                    AddVHostToContent(vhosts, domain, path, port);

                // Back up all files before modifying any.
                string hostsBackup = CreateBackup(HostsFilePath);
                string vhostsBackup = CreateBackup(VHostsFilePath);
                string httpdBackup = CreateBackup(HttpdFilePath);

                try
                {
                    File.WriteAllText(HostsFilePath, updatedHosts);
                    File.WriteAllText(HttpdFilePath, updatedHttpd);
                    File.WriteAllText(VHostsFilePath, updatedVhosts);

                    string validationError = TestApacheConfig();

                    if (!string.IsNullOrEmpty(validationError))
                    {
                        throw new InvalidOperationException(
                            "Apache configuration validation failed:\n\n" +
                            validationError);
                    }
                }
                catch (Exception writeException)
                {
                    try
                    {
                        RestoreBackup(hostsBackup, HostsFilePath);
                        RestoreBackup(httpdBackup, HttpdFilePath);
                        RestoreBackup(vhostsBackup, VHostsFilePath);
                    }
                    catch (Exception restoreException)
                    {
                        throw new InvalidOperationException(
                            "The operation failed and rollback was incomplete.\n" +
                            "Backups:\n" +
                            hostsBackup + "\n" +
                            httpdBackup + "\n" +
                            vhostsBackup + "\n\n" +
                            "Rollback error: " + restoreException.Message,
                            writeException);
                    }

                    throw;
                }

                LoadDomains();
                UpdateDomainCount();

                txtDomain.Clear();
                txtPath.Clear();

                System.Windows.MessageBox.Show(
                    "Domain added successfully.\n\n" +
                    "Backups were created alongside the original files.\n" +
                    "Restart Apache to apply the changes.",
                    "NerdLocal",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("Could not add the domain.\n\n" + ex.Message);
            }
        }

        private void CopyDomain_Click(object sender, RoutedEventArgs e)
        {
            DomainItem domain = (sender as Button)?.Tag as DomainItem;

            if (domain == null)
                return;

            string url = "http://" + domain.Domain;

            if (!string.IsNullOrWhiteSpace(domain.Port) &&
                domain.Port != "80")
            {
                url += ":" + domain.Port;
            }

            try
            {
                Clipboard.SetText(url);
            }
            catch (Exception ex)
            {
                ShowError("Could not copy the domain URL:\n" + ex.Message);
            }
        }

        private void EditDomain_Click(object sender, RoutedEventArgs e)
        {
            editingDomain = (sender as Button)?.Tag as DomainItem;

            if (editingDomain == null)
                return;

            txtDomain.Text = editingDomain.Domain;
            txtPath.Text = editingDomain.Path;
            ComboBoxItem portItem = cmbPort.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item =>
                    item.Content.ToString() == editingDomain.Port);

            if (portItem == null)
            {
                portItem = new ComboBoxItem { Content = editingDomain.Port };
                cmbPort.Items.Add(portItem);
            }

            cmbPort.SelectedItem = portItem;
            txtDomainFormTitle.Text = "Edit Domain";
            btnAddDomain.Content = "Save Changes";
            btnCancelEdit.Visibility = Visibility.Visible;
            SetStatusBar("Editing domain: " + editingDomain.Domain);
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            ClearDomainEdit();
            SetStatusBar("Edit cancelled  •  XAMPP Local Stack");
        }

        private void DeleteDomain_Click(object sender, RoutedEventArgs e)
        {
            DomainItem domain = (sender as Button)?.Tag as DomainItem;

            if (domain == null ||
                System.Windows.MessageBox.Show(
                    "Delete " + domain.Domain + " from the Hosts and Apache " +
                    "virtual-host files?",
                    "Delete Domain",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                string hosts = RemoveHostsMapping(
                    File.ReadAllText(HostsFilePath), domain.Domain);
                string vhosts = RemoveVHost(
                    File.ReadAllText(VHostsFilePath), domain.Domain);
                string httpd = File.ReadAllText(HttpdFilePath);

                SaveDomainConfiguration(hosts, vhosts, httpd);
                LoadDomains();
                UpdateDomainCount();
                ClearDomainEdit();
            }
            catch (Exception ex)
            {
                ShowError("Could not delete the domain.\n\n" + ex.Message);
            }
        }

        private void UpdateDomain(string domain, string path, string port)
        {
            if (!ValidateDomain(domain, path, port))
                return;

            try
            {
                string hosts = File.ReadAllText(HostsFilePath);
                string vhosts = File.ReadAllText(VHostsFilePath);
                string httpd = File.ReadAllText(HttpdFilePath);

                if (!domain.Equals(editingDomain.Domain,
                        StringComparison.OrdinalIgnoreCase) &&
                    (HasHostsMapping(hosts, domain) ||
                     HasServerName(vhosts, domain)))
                {
                    ShowError("The selected domain already exists in the configuration files.");
                    return;
                }

                hosts = ReplaceHostsMapping(
                    hosts, editingDomain.Domain, domain);
                vhosts = UpdateVHost(
                    vhosts, editingDomain.Domain, domain, path, port);
                httpd = AddListenPort(httpd, port);

                SaveDomainConfiguration(hosts, vhosts, httpd);
                LoadDomains();
                UpdateDomainCount();
                ClearDomainEdit();

                System.Windows.MessageBox.Show(
                    "Domain updated successfully. Backups were created alongside the configuration files.",
                    "NerdLocal",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("Could not update the domain.\n\n" + ex.Message);
            }
        }

        private void ClearDomainEdit()
        {
            editingDomain = null;
            txtDomain.Clear();
            txtPath.Clear();
            cmbPort.SelectedIndex = 0;
            txtDomainFormTitle.Text = "Add New Domain";
            btnAddDomain.Content = "+   Add Domain";
            btnCancelEdit.Visibility = Visibility.Collapsed;
        }

        private string ReplaceHostsMapping(
            string content, string oldDomain, string newDomain)
        {
            string newline = content.Contains("\r\n") ? "\r\n" : "\n";
            string[] lines = content.Split(new[] { "\r\n", "\n" },
                StringSplitOptions.None);

            for (int index = 0; index < lines.Length; index++)
            {
                string[] parts = Regex.Split(lines[index].Trim(), @"\s+");

                if (parts.Length < 2 || lines[index].TrimStart().StartsWith("#"))
                    continue;

                bool changed = false;
                for (int part = 1; part < parts.Length; part++)
                {
                    if (parts[part].Equals(oldDomain,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        parts[part] = newDomain;
                        changed = true;
                    }
                }

                if (changed)
                    lines[index] = parts[0] + "\t" + string.Join("\t", parts.Skip(1));
            }

            return string.Join(newline, lines);
        }

        private string RemoveHostsMapping(string content, string domain)
        {
            string newline = content.Contains("\r\n") ? "\r\n" : "\n";
            string[] lines = content.Split(new[] { "\r\n", "\n" },
                StringSplitOptions.None);
            var remainingLines = new System.Collections.Generic.List<string>();

            foreach (string line in lines)
            {
                if (line.TrimStart().StartsWith("#"))
                {
                    remainingLines.Add(line);
                    continue;
                }

                string[] parts = Regex.Split(line.Trim(), @"\s+");
                if (parts.Length < 2 || !parts.Skip(1).Any(host =>
                        host.Equals(domain, StringComparison.OrdinalIgnoreCase)))
                {
                    remainingLines.Add(line);
                    continue;
                }

                string[] hosts = parts.Skip(1)
                    .Where(host => !host.Equals(domain,
                        StringComparison.OrdinalIgnoreCase)).ToArray();

                if (hosts.Length > 0)
                    remainingLines.Add(parts[0] + "\t" + string.Join("\t", hosts));
            }

            return string.Join(newline, remainingLines);
        }

        private string UpdateVHost(
            string content, string oldDomain, string newDomain,
            string path, string port)
        {
            string safePath = path.Replace("/", "\\");

            return Regex.Replace(content,
                @"(?is)<VirtualHost\b[^>]*>.*?</VirtualHost>", block =>
                {
                    Match serverName = Regex.Match(block.Value,
                        @"(?im)^\s*ServerName\s+(\S+)");

                    if (!serverName.Success || !serverName.Groups[1].Value.Equals(
                            oldDomain, StringComparison.OrdinalIgnoreCase))
                    {
                        return block.Value;
                    }

                    string updated = Regex.Replace(block.Value,
                        @"(<VirtualHost\s+\*):\d+(>)", "$1:" + port + "$2",
                        RegexOptions.IgnoreCase);
                    updated = Regex.Replace(updated,
                        @"(?im)(^\s*ServerName\s+)\S+",
                        "$1" + newDomain);
                    updated = Regex.Replace(updated,
                        @"(?im)(^\s*DocumentRoot\s+)(?:""[^""\r\n]*""|\S+)",
                        match => match.Groups[1].Value + "\"" + safePath + "\"");
                    updated = Regex.Replace(updated,
                        @"(?im)(^\s*<Directory\s+)(?:""[^""\r\n]*""|\S+)",
                        match => match.Groups[1].Value + "\"" + safePath + "\"");
                    return updated;
                });
        }

        private string RemoveVHost(string content, string domain)
        {
            return Regex.Replace(content,
                @"(?is)<VirtualHost\b[^>]*>.*?</VirtualHost>", block =>
                {
                    Match serverName = Regex.Match(block.Value,
                        @"(?im)^\s*ServerName\s+(\S+)");

                    return serverName.Success && serverName.Groups[1].Value.Equals(
                        domain, StringComparison.OrdinalIgnoreCase)
                        ? ""
                        : block.Value;
                });
        }

        private void SaveDomainConfiguration(
            string hosts, string vhosts, string httpd)
        {
            string hostsBackup = CreateBackup(HostsFilePath);
            string vhostsBackup = CreateBackup(VHostsFilePath);
            string httpdBackup = CreateBackup(HttpdFilePath);

            try
            {
                File.WriteAllText(HostsFilePath, hosts);
                File.WriteAllText(HttpdFilePath, httpd);
                File.WriteAllText(VHostsFilePath, vhosts);

                string validationError = TestApacheConfig();
                if (!string.IsNullOrEmpty(validationError))
                {
                    throw new InvalidOperationException(
                        "Apache configuration validation failed:\n\n" +
                        validationError);
                }
            }
            catch
            {
                RestoreBackup(hostsBackup, HostsFilePath);
                RestoreBackup(httpdBackup, HttpdFilePath);
                RestoreBackup(vhostsBackup, VHostsFilePath);
                throw;
            }
        }

        private string AddHostsMapping(string content, string domain)
        {
            return content.TrimEnd() + Environment.NewLine +
                   "127.0.0.1\t" + domain + Environment.NewLine;
        }

        private bool HasHostsMapping(string content, string domain)
        {
            foreach (string line in content.Split(
                         new[] { "\r\n", "\n" },
                         StringSplitOptions.None))
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("#"))
                    continue;

                string[] parts = Regex.Split(trimmed, @"\s+");

                if (parts.Length < 2)
                    continue;

                if (parts.Skip(1).Any(host =>
                    host.Equals(domain, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasServerName(string content, string domain)
        {
            return Regex.IsMatch(
                content,
                @"(?im)^\s*ServerName\s+" +
                Regex.Escape(domain) +
                @"\s*(?:#.*)?$");
        }

        private string AddListenPort(string content, string port)
        {
            bool found = Regex.IsMatch(
                content,
                @"(?im)^\s*Listen\s+" +
                Regex.Escape(port) +
                @"(?:\s|$)");

            if (found)
                return content;

            return content.TrimEnd() + Environment.NewLine +
                   "Listen " + port + Environment.NewLine;
        }

        private string AddVHostToContent(
            string content, string domain, string path, string port)
        {
            string safePath = path.Replace("/", "\\");

            string block =
                Environment.NewLine +
                "<VirtualHost *:" + port + ">" + Environment.NewLine +
                "    ServerName " + domain + Environment.NewLine +
                "    DocumentRoot \"" + safePath + "\"" + Environment.NewLine +
                "    <Directory \"" + safePath + "\">" + Environment.NewLine +
                "        AllowOverride All" + Environment.NewLine +
                "        Require all granted" + Environment.NewLine +
                "    </Directory>" + Environment.NewLine +
                "</VirtualHost>" + Environment.NewLine;

            return content.TrimEnd() + Environment.NewLine + block;
        }

        private string CreateBackup(string filePath)
        {
            string backupPath = filePath +
                ".nerdlocal-" +
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") +
                ".bak";

            File.Copy(filePath, backupPath, false);
            return backupPath;
        }

        private void RestoreBackup(string backupPath, string originalPath)
        {
            if (File.Exists(backupPath))
                File.Copy(backupPath, originalPath, true);
        }

        private string TestApacheConfig()
        {
            string apacheExe = Path.Combine(
                GetXamppPath(), "apache", "bin", "httpd.exe");

            if (!File.Exists(apacheExe))
            {
                return "Apache httpd.exe was not found:\n" + apacheExe +
                       "\n\nConfiguration validation could not be performed.";
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = apacheExe,
                Arguments = "-t -f \"" + HttpdFilePath + "\"",
                WorkingDirectory = Path.GetDirectoryName(apacheExe),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                    return "Could not start Apache configuration validation.";

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (process.ExitCode != 0)
                    return (output + Environment.NewLine + error).Trim();

                return "";
            }
        }

        private void LoadDomains()
        {
            Domains.Clear();

            if (!File.Exists(VHostsFilePath))
                return;

            string content = File.ReadAllText(VHostsFilePath);

            MatchCollection blocks = Regex.Matches(
                content,
                @"(?is)<VirtualHost\b([^>]*)>(.*?)</VirtualHost>");

            foreach (Match block in blocks)
            {
                Match portMatch = Regex.Match(
                    block.Groups[1].Value, @":(\d+)");

                Match domainMatch = Regex.Match(
                    block.Groups[2].Value,
                    @"(?im)^\s*ServerName\s+(\S+)");

                Match pathMatch = Regex.Match(
                    block.Groups[2].Value,
                    @"(?im)^\s*DocumentRoot\s+""?([^""\r\n]+)""?");

                if (!domainMatch.Success)
                    continue;

                Domains.Add(new DomainItem
                {
                    Domain = domainMatch.Groups[1].Value.Trim(),
                    Port = portMatch.Success
                        ? portMatch.Groups[1].Value
                        : "",
                    Path = pathMatch.Success
                        ? pathMatch.Groups[1].Value.Trim()
                        : ""
                });
            }

            _domainsView?.Refresh();
        }

        private void UpdateDomainCount()
        {
            txtTotalDomains.Text = Domains.Count.ToString();
        }

        private void TxtSearch_TextChanged(
            object sender,
            System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_domainsView == null)
                return;

            string keyword = txtSearch.Text.Trim();

            _domainsView.Filter = item =>
            {
                DomainItem domain = item as DomainItem;

                if (domain == null)
                    return false;

                return domain.Domain.IndexOf(
                           keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       domain.Path.IndexOf(
                           keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       domain.Port.IndexOf(
                           keyword, StringComparison.OrdinalIgnoreCase) >= 0;
            };

            _domainsView.Refresh();
        }

        private void LoadHosts_Click(object sender, RoutedEventArgs e)
        {
            LoadConfigFile(HostsFilePath);
        }

        private void LoadVHosts_Click(object sender, RoutedEventArgs e)
        {
            LoadConfigFile(VHostsFilePath);
        }

        private void LoadHttpd_Click(object sender, RoutedEventArgs e)
        {
            LoadConfigFile(HttpdFilePath);
        }

        private void LoadConfigFile(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) ||
                    !File.Exists(filePath))
                {
                    ShowError("Configuration file not found:\n" + filePath);
                    return;
                }

                if (_editorHasUnsavedChanges &&
                    System.Windows.MessageBox.Show(
                        "Discard your unsaved editor changes?",
                        "Unsaved Changes",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return;
                }

                _isLoadingEditor = true;
                txtConfigEditor.Text = File.ReadAllText(filePath);
                currentEditingFile = filePath;
                _editorHasUnsavedChanges = false;
                SetStatusBar("Loaded: " + filePath);
            }
            catch (Exception ex)
            {
                ShowError("Could not load file:\n" + ex.Message);
            }
            finally
            {
                _isLoadingEditor = false;
            }
        }

        private void ConfigEditor_TextChanged(
            object sender,
            System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!_isLoadingEditor &&
                !string.IsNullOrWhiteSpace(currentEditingFile))
            {
                _editorHasUnsavedChanges = true;
                SetStatusBar("Modified (unsaved): " + currentEditingFile);
            }
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(currentEditingFile))
            {
                ShowError("Load a configuration file first.");
                return;
            }

            try
            {
                if (!File.Exists(currentEditingFile))
                {
                    ShowError("The selected file no longer exists.");
                    return;
                }

                string backup = CreateBackup(currentEditingFile);

                File.WriteAllText(
                    currentEditingFile, txtConfigEditor.Text);

                _editorHasUnsavedChanges = false;

                System.Windows.MessageBox.Show(
                    "File saved successfully.\n\nBackup:\n" + backup,
                    "NerdLocal",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("Could not save configuration:\n" + ex.Message);
            }
        }

        private void PreviewConfig_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show(
                "Editing:\n\n" +
                (string.IsNullOrWhiteSpace(currentEditingFile)
                    ? "(No file loaded)"
                    : currentEditingFile) +
                "\n\nUnsaved changes: " +
                (_editorHasUnsavedChanges ? "Yes" : "No"),
                "Configuration Preview",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void OpenHostsFile_Click(object sender, RoutedEventArgs e)
        {
            OpenInNotepad(HostsFilePath);
        }

        private void OpenVHostsFile_Click(object sender, RoutedEventArgs e)
        {
            OpenInNotepad(VHostsFilePath);
        }

        private void OpenHttpdFile_Click(object sender, RoutedEventArgs e)
        {
            OpenInNotepad(HttpdFilePath);
        }

        private void OpenInNotepad(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    ShowError("File not found:\n" + filePath);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = "\"" + filePath + "\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowError("Could not open file:\n" + ex.Message);
            }
        }

        private void OpenXampp_Click(object sender, RoutedEventArgs e)
        {
            string xampp = Path.Combine(
                GetXamppPath(), "xampp-control.exe");

            try
            {
                if (!File.Exists(xampp))
                {
                    ShowError("XAMPP Control Panel not found:\n" + xampp);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = xampp,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowError("Could not open XAMPP:\n" + ex.Message);
            }
        }

        private void RestartApache_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show(
                "Restart Apache from the XAMPP Control Panel.\n\n" +
                "If Apache does not start, validate the configuration " +
                "and check its error log.",
                "Apache",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void CheckCurl_Click(object sender, RoutedEventArgs e)
        {
            string input = txtCurlDomain.Text.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                txtCurlOutput.Text = "Enter a website domain or URL.";
                return;
            }

            if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                input = "https://" + input;
            }

            Uri uri;

            if (!Uri.TryCreate(input, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps))
            {
                txtCurlOutput.Text = "Enter a valid HTTP or HTTPS website URL.";
                return;
            }

            Button button = sender as Button;
            if (button != null)
                button.IsEnabled = false;

            txtCurlOutput.Text = "curl.exe -IL " + uri.AbsoluteUri + "\r\n\r\n";

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "curl.exe",
                    Arguments = "-IL \"" + uri.AbsoluteUri + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> errorTask = process.StandardError.ReadToEndAsync();

                    await Task.Run(() => process.WaitForExit());

                    string output = await outputTask;
                    string error = await errorTask;

                    txtCurlOutput.Text += output;

                    if (!string.IsNullOrWhiteSpace(error))
                        txtCurlOutput.Text += error;

                    if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(error))
                        txtCurlOutput.Text += "\r\ncurl.exe exited with code " +
                                              process.ExitCode + ".";
                }
            }
            catch (Exception ex)
            {
                txtCurlOutput.Text += "Could not run curl.exe:\r\n" + ex.Message;
            }
            finally
            {
                if (button != null)
                    button.IsEnabled = true;
            }
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (RootWindowBorder == null || pathMaxRestore == null || btnMaxRestore == null)
                return;

            if (WindowState == WindowState.Maximized)
            {
                RootWindowBorder.Padding = new Thickness(7);
                RootWindowBorder.BorderThickness = new Thickness(0);
                btnMaxRestore.ToolTip = "Restore Down";
                pathMaxRestore.Data = Geometry.Parse("M 0,2 L 7,2 L 7,9 L 0,9 Z M 2,2 L 2,0 L 9,0 L 9,7 L 7,7");
            }
            else
            {
                RootWindowBorder.Padding = new Thickness(0);
                RootWindowBorder.BorderThickness = new Thickness(1);
                btnMaxRestore.ToolTip = "Maximize";
                pathMaxRestore.Data = Geometry.Parse("M 0,0 L 9,0 L 9,9 L 0,9 Z");
            }
        }

        private void ActivityNav_Click(object sender, RoutedEventArgs e)
        {
            if (_syncingNavigation || MainTabs == null)
                return;

            RadioButton navButton = sender as RadioButton;
            int tabIndex;
            if (navButton?.Tag != null &&
                int.TryParse(navButton.Tag.ToString(), out tabIndex) &&
                tabIndex >= 0 &&
                tabIndex < MainTabs.Items.Count)
            {
                _syncingNavigation = true;
                MainTabs.SelectedIndex = tabIndex;
                UpdateNavigationMetadata(tabIndex);
                _syncingNavigation = false;
            }
        }

        private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingNavigation || !ReferenceEquals(e.Source, MainTabs))
                return;

            _syncingNavigation = true;
            int index = MainTabs.SelectedIndex;
            if (navDomains != null) navDomains.IsChecked = index == 0;
            if (navConfig != null) navConfig.IsChecked = index == 1;
            if (navTools != null) navTools.IsChecked = index == 2;
            if (navCurl != null) navCurl.IsChecked = index == 3;
            UpdateNavigationMetadata(index);
            _syncingNavigation = false;
        }

        private void UpdateNavigationMetadata(int tabIndex)
        {
            string section = "Domain Manager";
            if (tabIndex == 1) section = "Config Files";
            else if (tabIndex == 2) section = "Tools";
            else if (tabIndex == 3) section = "Curl Checker";

            if (txtCommandBarHint != null)
                txtCommandBarHint.Text = "NerdLocal  •  " + section;
        }

        private void SetStatusBar(string text)
        {
            if (txtStatusBarInfo != null)
                txtStatusBarInfo.Text = text;
        }

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(!_isDarkTheme, savePreference: true);
        }

        private bool LoadSavedThemePreference()
        {
            try
            {
                if (File.Exists(ThemeSettingsFilePath))
                {
                    string saved = File.ReadAllText(ThemeSettingsFilePath).Trim();
                    if (saved.Equals("light", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch
            {
                // Fall back to default Dark Modern theme if preference file cannot be read.
            }

            return true;
        }

        private void SaveThemePreference(bool isDark)
        {
            try
            {
                string directory = Path.GetDirectoryName(ThemeSettingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(ThemeSettingsFilePath, isDark ? "dark" : "light");
            }
            catch
            {
                // Ignore non-critical settings persistence errors.
            }
        }

        private static void SetBrushResource(ResourceDictionary resources, string key, string hexColor)
        {
            Color color = (Color)ColorConverter.ConvertFromString(hexColor);
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[key] = brush;
        }

        private void ApplyTheme(bool isDark, bool savePreference)
        {
            _isDarkTheme = isDark;
            ResourceDictionary resources = Application.Current.Resources;

            if (isDark)
            {
                // VS Code Dark Modern
                SetBrushResource(resources, "TitleBarBg", "#181818");
                SetBrushResource(resources, "ActivityBarBg", "#181818");
                SetBrushResource(resources, "SidebarBg", "#181818");
                SetBrushResource(resources, "EditorBg", "#1F1F1F");
                SetBrushResource(resources, "SurfaceBg", "#252526");
                SetBrushResource(resources, "CommandCenterBg", "#242424");
                SetBrushResource(resources, "CommandCenterBorder", "#3C3C3C");
                SetBrushResource(resources, "InputBg", "#313131");
                SetBrushResource(resources, "InputBorder", "#3C3C3C");
                SetBrushResource(resources, "BorderSubtle", "#2B2B2B");
                SetBrushResource(resources, "TabInactiveBg", "#181818");
                SetBrushResource(resources, "TabActiveBg", "#1F1F1F");
                SetBrushResource(resources, "HoverBg", "#2A2D2E");
                SetBrushResource(resources, "SelectionBg", "#04395E");
                SetBrushResource(resources, "ButtonSecondaryBg", "#313131");
                SetBrushResource(resources, "ButtonSecondaryHoverBg", "#3C3C3C");
                SetBrushResource(resources, "ButtonSecondaryBorder", "#3C3C3C");
                SetBrushResource(resources, "AccentBrush", "#0078D4");
                SetBrushResource(resources, "AccentHoverBrush", "#026EC1");
                SetBrushResource(resources, "AccentSubtleBg", "#12293D");
                SetBrushResource(resources, "AccentSubtleBorder", "#1E4B73");
                SetBrushResource(resources, "DangerBrush", "#F14C4C");
                SetBrushResource(resources, "DangerSubtleBg", "#3A1D1E");
                SetBrushResource(resources, "SuccessBrush", "#3FB950");
                SetBrushResource(resources, "SuccessSubtleBg", "#162C1E");
                SetBrushResource(resources, "SuccessSubtleBorder", "#244B31");
                SetBrushResource(resources, "WarningBg", "#2B2315");
                SetBrushResource(resources, "WarningBorder", "#594522");
                SetBrushResource(resources, "WarningTitleFg", "#E3B341");
                SetBrushResource(resources, "WarningTextFg", "#D2A852");
                SetBrushResource(resources, "TextPrimary", "#CCCCCC");
                SetBrushResource(resources, "TextStrong", "#FFFFFF");
                SetBrushResource(resources, "TextSecondary", "#9D9D9D");
                SetBrushResource(resources, "TextMuted", "#6E7681");
                SetBrushResource(resources, "ActivityIconInactive", "#868686");
                SetBrushResource(resources, "ActivityIconActive", "#FFFFFF");
                SetBrushResource(resources, "CodeEditorBg", "#1A1A1A");
                SetBrushResource(resources, "CodeEditorFg", "#D4D4D4");
                SetBrushResource(resources, "CodeEditorBorder", "#2B2B2B");
                SetBrushResource(resources, "StatusBarBg", "#181818");
                SetBrushResource(resources, "StatusBarBorder", "#2B2B2B");
                SetBrushResource(resources, "StatusBarFg", "#CCCCCC");
                SetBrushResource(resources, "StatusRemoteBg", "#0078D4");
                SetBrushResource(resources, "StatusRemoteFg", "#FFFFFF");
                SetBrushResource(resources, "GridHeaderBg", "#181818");
                SetBrushResource(resources, "GridRowAltBg", "#232324");
                SetBrushResource(resources, "ScrollThumbBg", "#424242");
                SetBrushResource(resources, "ScrollThumbHoverBg", "#4F4F4F");

                if (txtThemeIcon != null) txtThemeIcon.Text = "☀";
                if (txtThemeLabel != null) txtThemeLabel.Text = "Light Mode";
                if (txtStatusTheme != null) txtStatusTheme.Text = "Theme: Dark Modern";
            }
            else
            {
                // VS Code Light Modern
                SetBrushResource(resources, "TitleBarBg", "#F8F8F8");
                SetBrushResource(resources, "ActivityBarBg", "#F8F8F8");
                SetBrushResource(resources, "SidebarBg", "#F8F8F8");
                SetBrushResource(resources, "EditorBg", "#FFFFFF");
                SetBrushResource(resources, "SurfaceBg", "#F8F8F8");
                SetBrushResource(resources, "CommandCenterBg", "#EFEFEF");
                SetBrushResource(resources, "CommandCenterBorder", "#D8D8D8");
                SetBrushResource(resources, "InputBg", "#FFFFFF");
                SetBrushResource(resources, "InputBorder", "#CECECE");
                SetBrushResource(resources, "BorderSubtle", "#E5E5E5");
                SetBrushResource(resources, "TabInactiveBg", "#F8F8F8");
                SetBrushResource(resources, "TabActiveBg", "#FFFFFF");
                SetBrushResource(resources, "HoverBg", "#ECECEC");
                SetBrushResource(resources, "SelectionBg", "#E4F1FA");
                SetBrushResource(resources, "ButtonSecondaryBg", "#F3F3F3");
                SetBrushResource(resources, "ButtonSecondaryHoverBg", "#E6E6E6");
                SetBrushResource(resources, "ButtonSecondaryBorder", "#CECECE");
                SetBrushResource(resources, "AccentBrush", "#005FB8");
                SetBrushResource(resources, "AccentHoverBrush", "#02539E");
                SetBrushResource(resources, "AccentSubtleBg", "#EBF3FC");
                SetBrushResource(resources, "AccentSubtleBorder", "#BDD8F5");
                SetBrushResource(resources, "DangerBrush", "#CD3131");
                SetBrushResource(resources, "DangerSubtleBg", "#FDECEC");
                SetBrushResource(resources, "SuccessBrush", "#1A7F37");
                SetBrushResource(resources, "SuccessSubtleBg", "#EAF7EE");
                SetBrushResource(resources, "SuccessSubtleBorder", "#C2E5CD");
                SetBrushResource(resources, "WarningBg", "#FFF8E6");
                SetBrushResource(resources, "WarningBorder", "#E8D29B");
                SetBrushResource(resources, "WarningTitleFg", "#8A5A00");
                SetBrushResource(resources, "WarningTextFg", "#6B4C11");
                SetBrushResource(resources, "TextPrimary", "#3B3B3B");
                SetBrushResource(resources, "TextStrong", "#1F1F1F");
                SetBrushResource(resources, "TextSecondary", "#616161");
                SetBrushResource(resources, "TextMuted", "#8A8A8A");
                SetBrushResource(resources, "ActivityIconInactive", "#6E6E6E");
                SetBrushResource(resources, "ActivityIconActive", "#1F1F1F");
                SetBrushResource(resources, "CodeEditorBg", "#FAFAFA");
                SetBrushResource(resources, "CodeEditorFg", "#1F1F1F");
                SetBrushResource(resources, "CodeEditorBorder", "#DFDFDF");
                SetBrushResource(resources, "StatusBarBg", "#F8F8F8");
                SetBrushResource(resources, "StatusBarBorder", "#E5E5E5");
                SetBrushResource(resources, "StatusBarFg", "#3B3B3B");
                SetBrushResource(resources, "StatusRemoteBg", "#005FB8");
                SetBrushResource(resources, "StatusRemoteFg", "#FFFFFF");
                SetBrushResource(resources, "GridHeaderBg", "#F5F5F5");
                SetBrushResource(resources, "GridRowAltBg", "#FAFAFA");
                SetBrushResource(resources, "ScrollThumbBg", "#C4C4C4");
                SetBrushResource(resources, "ScrollThumbHoverBg", "#A8A8A8");

                if (txtThemeIcon != null) txtThemeIcon.Text = "☾";
                if (txtThemeLabel != null) txtThemeLabel.Text = "Dark Mode";
                if (txtStatusTheme != null) txtStatusTheme.Text = "Theme: Light Modern";
            }

            if (savePreference)
                SaveThemePreference(isDark);
        }

        private void ShowError(string message)
        {
            System.Windows.MessageBox.Show(
                message,
                "NerdLocal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public class DomainItem
    {
        public string Domain { get; set; }
        public string Port { get; set; }
        public string Path { get; set; }
    }
}
