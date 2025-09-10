namespace LksBrothers.Node.Models;

public class NodeConfiguration
{
    public required string DataDirectory { get; set; }
    public required bool IsValidator { get; set; }
    public string? ValidatorKeyFile { get; set; }
    public required string NetworkId { get; set; }
    public required int P2PPort { get; set; }
    public required int RpcPort { get; set; }
    public required List<string> BootstrapNodes { get; set; }
}

public class ValidatorKeyData
{
    public required string Address { get; set; }
    public required string PrivateKey { get; set; }
    public required string PublicKey { get; set; }
    public string? Stake { get; set; }
}
