namespace IHLDota2Bot;

public class AccountPoolStats
{
    public int TotalAccounts { get; set; }
    public int ReadyAccounts { get; set; }
    public int InUseAccounts { get; set; }
    public int OnCooldownAccounts { get; set; }
    public List<SteamAccount> Accounts { get; set; } = [];

    public override string ToString() =>
        $"Pool Stats: Total={TotalAccounts}, Ready={ReadyAccounts}, InUse={InUseAccounts}, Cooldown={OnCooldownAccounts}";
}
