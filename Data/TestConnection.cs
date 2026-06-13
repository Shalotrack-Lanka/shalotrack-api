using Npgsql;

string connString =
    "Host=db.riyjkfwxkamqbuuuwdli.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true";

try
{
    using var conn = new NpgsqlConnection(connString);
    conn.Open();
    //to check the conenctions with the DB
    Console.WriteLine("Connected successfully!");
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}