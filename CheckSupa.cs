using System;
using System.Net.Http;
using System.Threading.Tasks;
class Program {
    static async Task Main() {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("apikey", "sb_publishable_26sUwu9-pd-KU7FADPAFRw_NUd-VUc6");
        var resp = await client.GetAsync("https://ivfodjulouwblbpraqeu.supabase.co/rest/v1/ticket_admin_commands?select=*");
        Console.WriteLine(await resp.Content.ReadAsStringAsync());
    }
}
