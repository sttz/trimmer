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

    public TrimmerServer Server { get; set; }

    public void CreateServer(int serverPort, bool isDiscoverable)
    {
        Server = new TrimmerServer();
        Server.ServerPort = serverPort;
        Server.IsDiscoverable = isDiscoverable;
        if (isActiveAndEnabled) {
            Server.Start();
        }
    }

    void OnEnable()
    {
        if (Server == null && serverPort > 0) {
            CreateServer(serverPort, isDiscoverable);
        }

        if (Server != null) {
            Server.Start();
        }
    }

    void OnDisable()
    {
        Server.Stop();
    }

    void Update()
    {
        Server.Update();
    }
}

#endif

}
