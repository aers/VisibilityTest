using ImGuiNET;

namespace VisibilityTest
{
    public class PluginUI
    {

        private bool visible = false;

        private Plugin _plugin;

        public PluginUI(Plugin _p)
        {
            _plugin = _p;
        }
        public bool IsVisible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        public void Draw()
        {
            if (!IsVisible)
                return;

            if (ImGui.Begin("VisibilityTest", ref visible))
            {
                bool hidePlayers = _plugin.config.HidePlayers;
                if (ImGui.Checkbox("Hide Players", ref hidePlayers))
                {
                    _plugin.config.HidePlayers = hidePlayers;
                    if (!hidePlayers)
                        _plugin.vm.UnhidePlayers();
                }
            }
        }
    }
}
