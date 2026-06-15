using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

class Program {
    static async Task Main() {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var settingsPath = Path.Combine(appData, "BarberiaSystem", "config", "sync-settings.json");
        var settingsStr = File.ReadAllText(settingsPath);
        var doc = JsonDocument.Parse(settingsStr);
        var url = doc.RootElement.GetProperty("SupabaseUrl").GetString();
        var did = doc.RootElement.GetProperty("DeviceId").GetString();
        var dsec = doc.RootElement.GetProperty("DeviceSecret").GetString();
        
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("x-device-id", did);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", dsec);
        
        var payload = new StringContent("{"cursor": "2026-06-15T00:00:00.000Z"}", System.Text.Encoding.UTF8, "application/json");
        var resp = await client.PostAsync(url + "/functions/v1/sync-changes", payload);
        
        Console.WriteLine("Status: " + (int)resp.StatusCode);
        Console.WriteLine(await resp.Content.ReadAsStringAsync());
    }
}
