using System;
using System.Net;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Runtime.InteropServices;

class Program
{
    static List<string> blockedDomains = new List<string>();
    static readonly object blockedLock = new object();
    static async Task Main(string[] args)
    {
        int n = Convert.ToInt32(Console.ReadLine());

        for (int i = 0; i < n; i++)
            blockedDomains.Add(Console.ReadLine());

        try
        {
            ProxySetter.EnableProxy("127.0.0.1", 8000);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return;
        }

        var proxyServer = new ProxyServer();

        try
        {
            proxyServer.CertificateManager.CreateRootCertificate();
            proxyServer.CertificateManager.TrustRootCertificate(true);

            var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, 8000, true);
            proxyServer.AddEndPoint(explicitEndPoint);

            proxyServer.BeforeRequest += OnRequest;

            proxyServer.Start();

            lock (blockedLock)
            {
                Console.WriteLine("Proxy działa na porcie 8000. Naciśnij Enter, aby zakończyć...");
            }

            var commandTask = Task.Run(() => CommandLoop());
            await commandTask;

            lock (blockedLock)
            {
                Console.WriteLine("Naciśnij enter, aby zakończyć");
                Console.ReadLine();
            }

            Console.WriteLine("Program zakończony");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd działania proxy: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("Zatrzymywanie proxy");
            ProxySetter.DisableProxy();

            proxyServer.Stop();
            lock (blockedLock)
            {
                blockedDomains.Clear();
            }
        }
    }

    private static async Task OnRequest(object sender, SessionEventArgs e)
    {
        lock (blockedLock)
        {
            foreach (var domain in blockedDomains)
            {
                if (e.HttpClient.Request.RequestUri.Host.Contains(domain))
                {
                    Console.WriteLine($"Blokowanie dostępu do: {domain}");
                    e.Ok("<html><body><h1>Ta strona jest zablokowana</h1></body></html>");
                    return;
                }
            }
        }
    }

    static async Task CommandLoop()
    {
        Console.WriteLine("Odblokować stronę? y/n");
        string cmd = Console.ReadLine();

        if (cmd == "n")
            Console.WriteLine("Wybrano 'n'");

        if (cmd == "y")
        {
            string domain = Console.ReadLine();

            lock (blockedLock)
            {
                blockedDomains.Remove(domain);
            }

            await Task.Delay(60000);

            lock (blockedLock)
            {
                blockedDomains.Add(domain);
            }

            Console.WriteLine($"Strona {domain} znowu jest zablokowana");
        }
    }
}

class ProxySetter
{
    [DllImport("wininet.dll", SetLastError = true)]
    public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

    const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    const int INTERNET_OPTION_REFRESH = 37;

    public static void EnableProxy(string address, int port)
    {
        string proxy = $"{address}:{port}";
        using (RegistryKey registry = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", true))
        {
            registry.SetValue("ProxyEnable", 1);
            registry.SetValue("ProxyServer", proxy);
        }

        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);

        Console.WriteLine($"Proxy włączone: {proxy}");
    }

    public static void DisableProxy()
    {
        using (RegistryKey registry = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", true))
        {
            registry.SetValue("ProxyEnable", 0);
        }

        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);

        Console.WriteLine("\nProxy wyłączone");
    }
}