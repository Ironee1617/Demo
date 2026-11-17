using System;
using System.IO;
using PlayEveryWare.EpicOnlineServices;
using UnityEditor;
using UnityEngine;

// R85: writes the Epic Dev Portal values (ArtSources/EOS/eos_credentials.json, kept out of Assets) into the EOS
// plugin's own config files (Assets/StreamingAssets/EOS: eos_product_config.json and the Windows platform config).
public static class AquariumEosSetup
{
    private static string CredentialsPath { get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ArtSources/EOS/eos_credentials.json"); } }

    [MenuItem("Aquarium/EOS/Apply credentials")]
    public static string Apply()
    {
        if (!File.Exists(CredentialsPath)) return "no credentials file: " + CredentialsPath;
        string json = File.ReadAllText(CredentialsPath);
        var creds = new EOSClientCredentials { ClientId = Value(json, "clientID"), ClientSecret = Value(json, "clientSecret"), EncryptionKey = Value(json, "encryptionKey") };
        var sandbox = SandboxId.FromString(Value(json, "sandboxID"));
        var deployment = new Deployment { SandboxId = sandbox, DeploymentId = Guid.Parse(Value(json, "deploymentID")) };

        var product = Config.Get<ProductConfig>();
        product.ProductName = Value(json, "productName");
        product.ProductId = Guid.Parse(Value(json, "productID"));
        product.ProductVersion = "1.0";
        product.Clients.Clear(); product.Clients.Add(creds);
        product.Environments.Sandboxes.Clear(); product.Environments.Sandboxes.Add(sandbox);
        product.Environments.Deployments.Clear(); product.Environments.Deployments.Add(deployment);
        product.Write();

        var windows = Config.Get<WindowsConfig>();
        windows.deployment = deployment;
        windows.clientCredentials = creds;
        windows.Write();

        AssetDatabase.Refresh();
        return "product " + product.FilePath + " | windows " + windows.FilePath;
    }

    private static string Value(string json, string key)
    {
        int i = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
        if (i < 0) throw new InvalidOperationException("missing " + key);
        int a = json.IndexOf('"', json.IndexOf(':', i) + 1) + 1;
        return json.Substring(a, json.IndexOf('"', a) - a);
    }
}
