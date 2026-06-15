using System;
using Microsoft.Data.Sqlite;
class Program {
    static void Main() {
        var conn = new SqliteConnection(@"Data Source=C:\Users\Albin Rodríguez\AppData\Local\BarberiaSystem\barberia-local.db");
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM sync_state WHERE key LIKE 'cloud_ticket_command:%'";
        using var reader = cmd.ExecuteReader();
        while(reader.Read()) {
            Console.WriteLine(reader.GetString(0) + " = " + reader.GetString(1));
        }
        var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "SELECT event_type, payload FROM sync_outbox ORDER BY occurred_at DESC LIMIT 5";
        using var reader2 = cmd2.ExecuteReader();
        while(reader2.Read()) {
            Console.WriteLine("OUTBOX: " + reader2.GetString(0) + " " + reader2.GetString(1));
        }
    }
}
