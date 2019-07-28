//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using UnityEngine;

namespace sttz.Trimmer.Options
{

#if TRIMMER_SERVER || UNITY_EDITOR

/// <summary>
/// Script used to host <see cref="TrimmerServer"/>.
/// </summary>
public class TrimmerServerHost : MonoBehaviour
{
    public int serverPort;
    public bool isDiscoverable;

    TrimmerServer server;

    void OnEnable()
    {
        if (server != null) {
            server.Start();
        }
    }

    void Start()
    {
        server = new TrimmerServer();
        server.ServerPort = serverPort;
        server.IsDiscoverable = isDiscoverable;
        server.Start();
    }

    void OnDisable()
    {
        server.Stop();
    }

    void Update()
    {
        server.Update();
    }
}

#endif

}
