using OBSStudioClient;
using OBSStudioClient.Events;
using System;
using System.Threading.Tasks;

namespace Memoria
{
    internal class OBSLink
    {
        public Configuration configuration;
        private ObsClient ObsClient; // this lib is kinda annoying :<

        public bool IsConnected => ObsClient != null && ObsClient.ConnectionState == OBSStudioClient.Enums.ConnectionState.Connected;
        public bool IsRecording { get; private set; } = false;

        public void Init(Configuration config)
        {
            configuration = config;
            ObsClient = new ObsClient();
            ObsClient.AutoReconnect = true;
            ObsClient.ConnectionClosed += OnConnectionClosed;
            ObsClient.RecordStateChanged += OnRecordStateChanged;

            Connect();
        }

        public void UnInit()
        {
            ObsClient.Disconnect();
        }

        private void OnConnectionClosed(object? sender, ConnectionClosedEventArgs e)
        {
            Plugin.Log.Debug($"OnConnectionClosed: {e}");
        }

        private void OnRecordStateChanged(object? sender, RecordStateChangedEventArgs e)
        {
            Plugin.Log.Debug($"OnRecordStateChanged: {e}");
            IsRecording = e.OutputActive;
        }

        public async Task StartRecording()
        {
            try
            {
                if (!IsConnected)
                    await Connect();

                await ObsClient.StartRecord();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Connect Error: {ex.Message}");
            }
        }

        public async Task<string> StopRecording()
        {
            try
            {
                if (!IsConnected)
                    await Connect();

                return await ObsClient.StopRecord();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Connect Error: {ex.Message}");
            }

            return null;
        }

        private async Task<bool> Connect(bool freshAttempt = false)
        {
            try
            {
                Plugin.Log.Information("Connect");

                var url = new Uri(configuration.OBSUrl);
                Plugin.Log.Information($"url.Host: {url.Host}");

                return await ObsClient.ConnectAsync(true, "", url.Host, url.Port);
            }
            catch (Exception ex)
            {
                Plugin.Log.Information($"Connect Error: {ex.Message}");
            }

            return false;
        }
    }
}
