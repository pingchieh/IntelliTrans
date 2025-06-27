namespace IntelliTrans.Cli;

public record DbType(string Name, string Assembly)
{
    public static readonly DbType Sqlite = new(
        nameof(Sqlite),
        typeof(Migrations.Sqlite.Marker).Assembly.GetName().Name!
    );
    public static readonly DbType Postgres = new(
        nameof(Postgres),
        typeof(Migrations.Postgres.Marker).Assembly.GetName().Name!
    );
}
