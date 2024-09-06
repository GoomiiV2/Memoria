using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Memoria
{
    internal class OBSLink
    {
        public Configuration configuration;
        private OBSWebsocket OBSWebsocket; // this lib is kinda annoying :<
        private bool ShouldStop = false;
        private int FailedConnectionAttempts = 0;
        private bool StartRecordingOnConnect = false;
        private bool StopRecordingOnConnect = false;

        public void Init(Configuration config)
        {
            configuration              = config;
            OBSWebsocket               = new OBSWebsocket();
            OBSWebsocket.Connected    += OnConnected;
            OBSWebsocket.Disconnected += OnDisconnected;
            ShouldStop = false;

            //Connect();
        }

        public void UnInit()
        {
            ShouldStop = true;

            if (OBSWebsocket.IsConnected)
                OBSWebsocket.Disconnect();
        }

        private void OnConnected(object? sender, EventArgs e)
        {
            Plugin.Log.Information("OnConnected");
            FailedConnectionAttempts = 0;

            if (StartRecordingOnConnect)
            {
                StartRecording();
                StartRecordingOnConnect = false;
            }

            if (StopRecordingOnConnect)
            {
                StopRecording();
                StopRecordingOnConnect = false ;
            }
        }

        private void OnDisconnected(object? sender, OBSWebsocketDotNet.Communication.ObsDisconnectionInfo e)
        {
            Plugin.Log.Information("OnDisconnected");

            if (ShouldStop)
                return;

            FailedConnectionAttempts++;

            if (FailedConnectionAttempts > 8)
            {
                FailedConnectionAttempts = 0;
                return;
            }

            Task.Factory.StartNew(async () =>
            {
                var reconnectDelay = 2 * (FailedConnectionAttempts * FailedConnectionAttempts);
                Plugin.Log.Information($"reconnectDelay: {reconnectDelay}");

                await Task.Delay(TimeSpan.FromSeconds(reconnectDelay));
                Connect();
            });
        }

        public void StartRecording()
        {
            try
            {
                if (!OBSWebsocket.IsConnected)
                {
                    StartRecordingOnConnect = true;
                    Connect(true);
                }
                else
                {
                    OBSWebsocket.StartRecord();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Connect Error: {ex.Message}");
            }
        }

        public string StopRecording()
        {
            try
            {
                StartRecordingOnConnect = false;

                if (!OBSWebsocket.IsConnected)
                {
                    Connect(true);
                    StopRecordingOnConnect = true;
                    return null; // Can't get the file path now yay :<
                }
                else
                {
                    return OBSWebsocket.StopRecord();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Connect Error: {ex.Message}");
            }

            return null;
        }

        private void Connect(bool freshAttempt = false)
        {
            try
            {
                Plugin.Log.Information("Connect");

                if (OBSWebsocket.IsConnected)
                    return;

                //if (freshAttempt)
                    //FailedConnectionAttempts = 0;

                OBSWebsocket.ConnectAsync(configuration.OBSUrl, null);
            }
            catch (Exception ex)
            {
                Plugin.Log.Information($"Connect Error: {ex.Message}");
            }
        }

        public enum PendingAction
        {
            None,
            StartRecording,
            StopRecording
        }
    }
}
