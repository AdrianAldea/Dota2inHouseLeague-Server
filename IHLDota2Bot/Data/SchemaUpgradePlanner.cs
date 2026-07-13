namespace IHLDota2Bot.Data;

/// <summary>
/// What the <see cref="DatabaseInitializer"/> should do to bring the schema up to
/// date, given the observed state of the target database.
/// </summary>
public enum SchemaUpgradeAction
{
    /// <summary>
    /// Run migrations normally. Covers a missing/empty database (migrations build
    /// the whole schema + history) and a database already under migration control
    /// (migrations apply whatever is pending).
    /// </summary>
    Migrate,

    /// <summary>
    /// The database has the application's tables but no <c>__EFMigrationsHistory</c>
    /// table — the hallmark of a schema created by the old <c>EnsureCreated</c>
    /// path. Record the initial migration as already applied (so its <c>Up()</c>
    /// isn't replayed over existing tables), then migrate any later migrations.
    /// </summary>
    BaselineThenMigrate,
}

/// <summary>
/// Pure decision for the migration upgrade path, kept separate from the EF Core
/// calls so the branching — especially the legacy <c>EnsureCreated</c> →
/// migrations transition — can be unit-tested without a live database.
/// </summary>
public static class SchemaUpgradePlanner
{
    /// <param name="databaseExists">Whether the target database exists at all.</param>
    /// <param name="migrationsHistoryExists">Whether the <c>__EFMigrationsHistory</c> table is present.</param>
    /// <param name="hasApplicationTables">Whether the database already contains user tables.</param>
    public static SchemaUpgradeAction Decide(
        bool databaseExists,
        bool migrationsHistoryExists,
        bool hasApplicationTables
    )
    {
        // Nothing there yet (no database, or an empty one): migrations create it all.
        if (!databaseExists || !hasApplicationTables)
            return SchemaUpgradeAction.Migrate;

        // Tables exist but EF has no record of any migration → created by
        // EnsureCreated. Baseline before migrating so we don't recreate them.
        if (!migrationsHistoryExists)
            return SchemaUpgradeAction.BaselineThenMigrate;

        // Tables + history present: ordinary "apply pending migrations" path.
        return SchemaUpgradeAction.Migrate;
    }
}
