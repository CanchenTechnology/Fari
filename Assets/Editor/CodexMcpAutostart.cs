using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CodexMcpAutostart
{
    private const string SessionKey = "CodexMcpAutostart.Started";
    private const string HttpUrl = "http://127.0.0.1:8080";

    static CodexMcpAutostart()
    {
        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += () => _ = StartBridgeAsync();
    }

    private static async Task StartBridgeAsync()
    {
        try
        {
            var config = EditorConfigurationCache.Instance;
            config.SetUseHttpTransport(true);
            config.SetHttpTransportScope("local");
            config.SetHttpBaseUrl(HttpUrl);
            config.Refresh();

            EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);
            EditorPrefs.SetBool("MCPForUnity.ProjectScopedTools.LocalHttp", true);

            for (var attempt = 0; attempt < 20; attempt++)
            {
                if (MCPServiceLocator.Bridge.IsRunning)
                {
                    Debug.Log("[CodexMcpAutostart] MCP for Unity bridge is already running.");
                    return;
                }

                if (await MCPServiceLocator.Bridge.StartAsync())
                {
                    Debug.Log("[CodexMcpAutostart] MCP for Unity bridge connected to " + HttpUrl + ".");
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            Debug.LogWarning("[CodexMcpAutostart] MCP for Unity bridge did not connect to " + HttpUrl + ".");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CodexMcpAutostart] Failed to start MCP for Unity bridge: " + ex.Message);
        }
    }
}
