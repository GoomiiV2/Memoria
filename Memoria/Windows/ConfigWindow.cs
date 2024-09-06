using System;
using System.Numerics;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace Memoria.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("A Wonderful Configuration Window###With a constant ID")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(500, 90);
        SizeCondition = ImGuiCond.Once;

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
        // can't ref a property, so use a local copy
        var configValue = Configuration.SomePropertyToBeSavedAndWithADefault;
        if (ImGui.Checkbox("Random Config Bool", ref configValue))
        {
            Configuration.SomePropertyToBeSavedAndWithADefault = configValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            Configuration.Save();
        }

        var movable = Configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable Config Window", ref movable))
        {
            Configuration.IsConfigWindowMovable = movable;
            Configuration.Save();
        }

        //ImGui.Text($"Pull Save Location: ");
        //ImGui.SameLine();
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

        var obsUrl = Configuration.OBSUrl;
        if (ImGui.InputText("OBS Url", ref obsUrl, 1000))
        {
            Configuration.OBSUrl = obsUrl;
            Configuration.Save();
        }
    }
}
