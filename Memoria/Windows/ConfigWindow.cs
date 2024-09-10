using System;
using System.Numerics;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using ImGuiScene;

namespace Memoria.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;

    public ConfigWindow(Plugin plugin) : base("Memoria Settings")
    {
        Size = new Vector2(550, 800);
        Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Flags must be added or removed before Draw() is being called, or they won't apply
        if (Configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {
        var pullLoc = Configuration.PullSaveLocation;
        ImGui.SetNextItemWidth(250);
        ImGui.InputText("Pull Save Location##PullSaveLoc", ref pullLoc, 1000, ImGuiInputTextFlags.ReadOnly);
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Folder))
        {
            Plugin.FileDialogManager.OpenFolderDialog("Choose where to save pull logs", (success, path) =>
            {
                if (success)
                {
                    Configuration.PullSaveLocation = path;
                    Configuration.Save();
                }
            }, Configuration.PullSaveLocation);
        }

        /*var obsUrl = Configuration.OBSUrl;
        if (ImGui.InputText("OBS Url", ref obsUrl, 1000))
        {
            Configuration.OBSUrl = obsUrl;
            Configuration.Save();
        }*/

        RenderTitle("OBS Settings");

        var obsHost = Configuration.OBSHost;
        var obsPort = (int)Configuration.OBSPort;
        ImGui.SetNextItemWidth(200);
        var obsSettingsChanged = ImGui.InputText("##OBS Host", ref obsHost, 100);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(50);
        obsSettingsChanged |= ImGui.InputInt("OBS Host and Port", ref obsPort, 0, 0);

        ImGui.Text("OBS Status:");
        ImGui.SameLine();
        if (Plugin.OBSLink.IsRecording)
            ImGui.TextColored(new Vector4(1, 1, 0, 1), "Recording");
        else if (Plugin.OBSLink.IsConnected)
            ImGui.TextColored(new Vector4(0, 1, 0, 1), "Connected");
        else
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1), "Disconnected");

        if (obsSettingsChanged &&
            (obsHost != Configuration.OBSHost || obsPort != Configuration.OBSPort)
            && !ImGui.IsAnyItemActive())
        {
            Configuration.OBSHost = obsHost;
            Configuration.OBSPort = (ushort)obsPort;
            Configuration.Save();
            Plugin.OBSLink.Reconnect();
        }

        ImGui.SetNextItemWidth(260);
        var recEndDelay = Configuration.DelayAfterPullEndToStopRec;
        if (ImGui.SliderInt("Record X seconds after pull", ref recEndDelay, 0, 30) && recEndDelay != Configuration.DelayAfterPullEndToStopRec)
        {
            Configuration.DelayAfterPullEndToStopRec = recEndDelay;
            Configuration.Save();
        }

        ImGui.BeginGroup();
        if (ImGui.CollapsingHeader("Enabled Contnet"))
        {
            var recInNormRaids = Configuration.RecInNormRaids;
            var hasChanged = ImGui.Checkbox("Normal Raids", ref recInNormRaids);
            Configuration.RecInNormRaids = recInNormRaids;

            ImGui.SameLine();
            var recInNormTrials = Configuration.RecInNormTrials;
            hasChanged |= ImGui.Checkbox("Normal Trials", ref recInNormTrials);
            Configuration.RecInNormTrials = recInNormTrials;

            ImGui.SameLine();
            var recInSavageRaids = Configuration.RecInSavageRaids;
            hasChanged |= ImGui.Checkbox("Savages", ref recInSavageRaids);
            Configuration.RecInSavageRaids = recInSavageRaids;

            ImGui.SameLine();
            var recInExTrials = Configuration.RecInExTrials;
            hasChanged |= ImGui.Checkbox("Exterme Trials", ref recInExTrials);
            Configuration.RecInExTrials = recInExTrials;

            ImGui.SameLine();
            var recInUltimates = Configuration.RecInUltimates;
            hasChanged |= ImGui.Checkbox("Ultimates", ref recInUltimates);
            Configuration.RecInUltimates = recInUltimates;

            if (hasChanged)
            {
                Plugin.Log.Information("Enabled COntent was changed, saving");
                Configuration.Save();
            }
        }
        ImGui.EndGroup();
    }

    public static void RenderTitle(string title)
    {
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        Vector2 cursorScreenPos = ImGui.GetCursorScreenPos();
        ImGuiStylePtr styles = ImGui.GetStyle();
        dl.AddRectFilled(cursorScreenPos, cursorScreenPos + new Vector2(ImGui.GetColumnWidth(), 24), ImGui.GetColorU32(ImGuiCol.Header), styles.WindowRounding);
        dl.AddText(cursorScreenPos + new Vector2(5f, (24f - ImGui.GetTextLineHeight()) / 2), ImGui.GetColorU32(ImGuiCol.Text), title);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 28);
    }
}
