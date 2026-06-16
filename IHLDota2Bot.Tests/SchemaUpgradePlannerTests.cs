using IHLDota2Bot.Data;

namespace IHLDota2Bot.Tests;

/// <summary>
/// The decision table for the migration upgrade path — especially adopting a
/// legacy <c>EnsureCreated</c> database into migration control.
/// </summary>
public class SchemaUpgradePlannerTests
{
    [Fact]
    public void NoDatabase_Migrates()
    {
        var action = SchemaUpgradePlanner.Decide(
            databaseExists: false, migrationsHistoryExists: false, hasApplicationTables: false);

        Assert.Equal(SchemaUpgradeAction.Migrate, action);
    }

    [Fact]
    public void EmptyDatabase_Migrates()
    {
        var action = SchemaUpgradePlanner.Decide(
            databaseExists: true, migrationsHistoryExists: false, hasApplicationTables: false);

        Assert.Equal(SchemaUpgradeAction.Migrate, action);
    }

    [Fact]
    public void LegacyEnsureCreatedDatabase_BaselinesThenMigrates()
    {
        // Tables present, but no __EFMigrationsHistory — the EnsureCreated fingerprint.
        var action = SchemaUpgradePlanner.Decide(
            databaseExists: true, migrationsHistoryExists: false, hasApplicationTables: true);

        Assert.Equal(SchemaUpgradeAction.BaselineThenMigrate, action);
    }

    [Fact]
    public void MigrationManagedDatabase_Migrates()
    {
        var action = SchemaUpgradePlanner.Decide(
            databaseExists: true, migrationsHistoryExists: true, hasApplicationTables: true);

        Assert.Equal(SchemaUpgradeAction.Migrate, action);
    }
}
