using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[DisallowMultipleComponent]
public class GestureUdpReceiver : MonoBehaviour
{
    [SerializeField] private int port = 5052;
    [SerializeField] private string fallbackPlayerId = "player-1";
    [SerializeField] private bool printPackets;

    private readonly object packetsLock = new object();
    private readonly Dictionary<string, GesturePacket> latestPacketsByPlayer = new Dictionary<string, GesturePacket>();

    private Thread receiveThread;
    private UdpClient client;
    private volatile bool running;

    public int ActivePlayerCount
    {
        get
        {
            lock (packetsLock)
            {
                return latestPacketsByPlayer.Count;
            }
        }
    }

    private void OnEnable()
    {
        StartReceiver();
    }

    private void OnDisable()
    {
        StopReceiver();
    }

    private void OnDestroy()
    {
        StopReceiver();
    }

    public bool TryGetLatestPacket(string playerId, out GesturePacket packet)
    {
        lock (packetsLock)
        {
            return latestPacketsByPlayer.TryGetValue(playerId, out packet);
        }
    }

    public string[] GetConnectedPlayerIds()
    {
        lock (packetsLock)
        {
            string[] ids = new string[latestPacketsByPlayer.Count];
            latestPacketsByPlayer.Keys.CopyTo(ids, 0);
            return ids;
        }
    }

    private void StartReceiver()
    {
        if (running)
        {
            return;
        }

        running = true;
        receiveThread = new Thread(ReceiveLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    private void StopReceiver()
    {
        running = false;

        if (client != null)
        {
            client.Close();
            client = null;
        }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(200);
        }

        receiveThread = null;
    }

    private void ReceiveLoop()
    {
        client = new UdpClient(port);

        while (running)
        {
            try
            {
                IPEndPoint anyIp = new IPEndPoint(IPAddress.Any, 0);
                byte[] rawBytes = client.Receive(ref anyIp);
                string json = Encoding.UTF8.GetString(rawBytes);
                GesturePacket packet = JsonUtility.FromJson<GesturePacket>(json);

                if (packet == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(packet.playerId))
                {
                    packet.playerId = fallbackPlayerId;
                }

                if (packet.command == null)
                {
                    packet.command = new GestureCommand();
                }

                if (packet.landmarks == null)
                {
                    packet.landmarks = new GestureLandmark[0];
                }

                lock (packetsLock)
                {
                    latestPacketsByPlayer[packet.playerId] = packet;
                }

                if (printPackets)
                {
                    Debug.Log(json);
                }
            }
            catch (SocketException)
            {
                if (!running)
                {
                    break;
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Gesture UDP receiver error: {exception.Message}");
            }
        }
    }
}