//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEditor;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Unity editor GUI for <see cref="sttz.Trimmer.Options.TrimmerClient"/>.
/// </summary>
public class TrimmerClientWindow : EditorWindow
{
    // ------ Configuration ------

    /// <summary>
    /// Port used by servers to discover on local network.
    /// </summary>
    public int discoverServerPort = 21076;

    // ------ Menu ------

    [MenuItem("Window/Trimmer/Client")]
    static void Open()
    {
        EditorWindow.GetWindow<TrimmerClientWindow>("Trimmer Client");
    }

    // ------ Fields ------

    sttz.Trimmer.Options.TrimmerClient client;

    List<Server> servers = new List<Server>();
    List<string> manualServers = new List<string>();
    string[] serverNames;
    string connectedTo;

    List<string> output = new List<string>();
	float outputScroll;
	int visibleOutput;
	float lineHeight;
	Rect outputRect;

    string input;
    bool refocusInput;
    bool repaintOnNextUpdate;
    Rect serversPopupRect;

    GUIStyle paddingStyle;

    struct Server
    {
        public IPAddress address;
        public string name;
    }

    // ------ Unity Events ------

    void OnEnable()
    {
        paddingStyle = new GUIStyle();
        paddingStyle.padding = new RectOffset(5, 5, 5, 5);

        client = new sttz.Trimmer.Options.TrimmerClient();
        client.OnServerFound += OnServerFound;
    }

    void OnDisable()
    {
        client.OnServerFound -= OnServerFound;
        client.Disconnect();
    }

    void OnFocus()
    {
        FindServers();
    }

    void Update()
    {
        client.Update();
    }

    void OnGUI()
    {
        if (lineHeight == 0f) {
            lineHeight = EditorStyles.label.CalcSize(new GUIContent("Test")).y;
        }

        // Delayed refocusing of the input field
        if (refocusInput && Event.current.type == EventType.Repaint) {
            EditorGUI.FocusTextInControl("Input");
            refocusInput = false;
        }

        if (repaintOnNextUpdate) {
            Repaint();
            repaintOnNextUpdate = false;
        }

        // -- Toolbar
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        {
            var selectedServer = 0;
            if (connectedTo != null) {
                if (!client.IsConnected) {
                    connectedTo = null;
                    serverNames = null;
                } else if (client.IsConnected) {
                    selectedServer = 1;
                }
            }

            if (serverNames == null) {
                BuildServerMenu();
            }

            GUI.changed = false;
            selectedServer = EditorGUILayout.Popup(selectedServer, serverNames, EditorStyles.toolbarPopup);
            if (Event.current.type == EventType.Repaint) serversPopupRect = GUILayoutUtility.GetLastRect();

            if (GUI.changed) {
                client.Disconnect();

                var server = GetServerFromMenu(selectedServer);
                if (server is int && (int)server == 1) {
                    PopupWindow.Show(serversPopupRect, new AddServerPopup() {
                        addHandler = (newServer, port) => {
                            if (string.IsNullOrEmpty(newServer)) return;
                            manualServers.Add(newServer + ":" + port);
                            selectedServer = 1 + servers.Count + 1 + manualServers.Count - 1;
                            serverNames = null;
                        }
                    });
                } else if (server is IPAddress) {
                    Connect((IPAddress)server);
                } else if (server is string) {
                    Connect((string)server);
                }
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton)) {
                FindServers();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton)) {
                output.Clear();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginVertical(paddingStyle);
        {
            // -- Output
			EditorGUILayout.BeginHorizontal();
			{
				// Use GetRect to fill the available area regardless of the output content
				var newRect = GUILayoutUtility.GetRect(50, float.MaxValue, 50, float.MaxValue);
				if (newRect.width > 10 && newRect.height > 10) {
					if (outputRect != newRect) {
						Repaint();
					}
					outputRect = newRect;
				}

				// Only the visible output lines are processed to prevent lots of line
				// slowing the GUI down or producing errors (.e.g when giving SelectableLabel
				// too much text to display).
				GUILayout.BeginArea(outputRect);
				{
					if (Event.current.type == EventType.Layout && outputRect.height > 0 && lineHeight > 0) {
						visibleOutput = Mathf.FloorToInt(outputRect.height / (lineHeight + 1));
					}

					var index = (int)outputScroll;
					for (int i = 0; i < visibleOutput; i++) {
						Rect position = EditorGUILayout.GetControlRect(false, lineHeight, EditorStyles.label);
						if (index < output.Count) {
							EditorGUI.SelectableLabel(position, output[index]);
						}
						index++;
					}
				}
				GUILayout.EndArea();

				// -- Scroll bar
				if (output.Count >= visibleOutput) {
					outputScroll = GUILayout.VerticalScrollbar(
						outputScroll,
						visibleOutput - 1, 0, output.Count,
						GUILayout.ExpandHeight(true)
					);
					outputScroll = Mathf.Max(0, outputScroll);

					// Handle scroll wheel
					if (outputRect.Contains(Event.current.mousePosition)
							&& Event.current.type == EventType.ScrollWheel) {
						outputScroll += Event.current.delta.y;
						outputScroll = Mathf.Min(Mathf.Max(outputScroll, 0), output.Count - visibleOutput + 1);
						Repaint();
					}
				} else {
					outputScroll = 0;
				}
			}
			EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            {
                GUI.enabled = client.IsConnected;

                GUILayout.Label(">", GUILayout.Width(11));

                if (Event.current.type == EventType.KeyDown
                        && (Event.current.keyCode == KeyCode.Return
                        || Event.current.keyCode == KeyCode.KeypadEnter)) {
                    Print("> " + input);
                    Execute(input);
                    input = "";
                    GUI.FocusControl(null);
                    Event.current.Use();
                }

                GUI.SetNextControlName("Input");
                input = EditorGUILayout.TextField(input);
                
                GUI.enabled = true;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
    }

    void BuildServerMenu()
    {
        var entries = new List<string>();
        entries.Add("Disconnected");

        if (connectedTo != null) {
            entries.Add(connectedTo);
        }

        if (servers.Count > 0) {
            entries.Add("");
            entries.AddRange(servers.Select(s => s.name));
        }

        if (manualServers.Count > 0) {
            entries.Add("");
            entries.AddRange(manualServers);
        }

        entries.Add("");
        entries.Add("Add Server...");

        serverNames = entries.ToArray();
    }

    object GetServerFromMenu(int index)
    {
        if (index < 0 || index >= serverNames.Length)
            return null;
        
        if (index == 0)
            return 0; // Disconnect
        else if (index == serverNames.Length - 1)
            return 1; // Add Server

        index -= 1; // Disconnect
        if (connectedTo != null) index -= 1; // Connected to

        if (servers.Count > 0) {
            index -= 1; // Separator

            if (index >= 0 && index < servers.Count) {
                return servers[index].address;
            }
        }

        index -= servers.Count;

        if (manualServers.Count > 0) {
            index -= 1; // Separator

            if (index >= 0 && index < manualServers.Count) {
                return manualServers[index];
            }
        }

        return null;
    }

    void Print(string line)
    {
        output.Add(line);

        outputScroll = Mathf.Max(output.Count - visibleOutput + 1, 0);
    }

    void Execute(string command)
    {
        if (command.Contains("=")) {
            client.SetOptionValue(command, (success, value) => {
                if (success) {
                    Print(value.Trim());
                } else {
                    Print("ERROR: " + value.Trim());
                }
                refocusInput = true;
                Repaint();
            });
        } else {
            var path = command;
            client.GetOptionValue(path, (success, value) => {
                if (success) {
                    Print(path + " = " + value.Trim());
                } else {
                    Print("ERROR: " + value.Trim());
                }
                refocusInput = true;
                Repaint();
            });
        }
    }

    // ------ Discovery ------

    void FindServers()
    {
        servers.Clear();
        serverNames = null;

        client.FindServers();
    }

    void OnServerFound(IPAddress address, string hello)
    {
        if (!servers.Any(s => s.address == address)) {
            servers.Add(new Server() {
                address = address,
                name = hello
            });
            serverNames = null;
        }
    }

    // ------ Connection ------

    void Connect(string address)
    {
        string hostString, portString;
        var lastColon = address.LastIndexOf(':');
        if (lastColon > 0) {
            portString = address.Substring(lastColon + 1);
            hostString = address.Substring(0, lastColon);
        } else {
            portString = discoverServerPort.ToString();
            hostString = address;
        }

        int port;
        if (!int.TryParse(portString, out port) 
                || port < IPEndPoint.MinPort
                || port > IPEndPoint.MaxPort) {
            Print("ERROR: Invalid port '" + portString + "'");
            return;
        }

        IPAddress ip;
        if (IPAddress.TryParse(hostString, out ip)) {
            Connect(ip, port);
        } else {
            try {
                Connect(Dns.GetHostAddresses(hostString)[0], port);
                connectedTo = address;
            } catch {
                Print("ERROR: Could not resolve '" + address + "'");
            }
        }
    }

    void Connect(IPAddress address, int port = -1)
    {
        connectedTo = address.ToString();
        serverNames = null;

        if (port > 0) {
            client.ServerPort = port;
        } else {
            client.ServerPort = discoverServerPort;
        }

        Print("> Connecting to server: " + address + ":" + port);
        client.Connect(address, (success, message) => {
            if (success) {
                Print(message.Trim());
            } else {
                Print("ERROR: " + message.Trim());
            }
            repaintOnNextUpdate = true;
        });
    }

    // ------ Add Server ------

    public class AddServerPopup : PopupWindowContent
    {
        public string address;
        public int port = 21076;
        public Action<string, int> addHandler;

        public override Vector2 GetWindowSize()
        {
            return new Vector2(200, 90);
        }

        public override void OnGUI(Rect rect)
        {
            EditorGUIUtility.labelWidth = 60;

            GUILayout.Label("Add new server", EditorStyles.boldLabel);

            address = EditorGUILayout.TextField("Address", address);
            port = EditorGUILayout.IntField("Port", port);
            
            if (GUILayout.Button("Add")) {
                addHandler(address, port);
                editorWindow.Close();
            }
        }
    }
}

}
