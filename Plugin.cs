using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Dalamud.Plugin;
using VisibilityTest.Attributes;

namespace VisibilityTest
{
    public class Plugin : IDalamudPlugin
    {
        public DalamudPluginInterface pluginInterface;
        private PluginCommandManager<Plugin> commandManager;
        public Configuration config;
        private PluginUI ui;

        public VisibilityManager vm;

        public string Name => "VisibilityTest";

        private void SetLocation(string dllPath)
        {
            Location = dllPath;

            AppDomain.CurrentDomain.AssemblyResolve += delegate (object sender, ResolveEventArgs args)
            {
                string assemblyFile = (args.Name.Contains(','))
                    ? args.Name.Substring(0, args.Name.IndexOf(','))
                    : args.Name;

                assemblyFile += ".dll";

                string absoluteFolder = new FileInfo((new System.Uri(Location)).LocalPath).Directory.FullName;
                string targetPath = Path.Combine(absoluteFolder, assemblyFile);

                try
                {
                    return Assembly.LoadFile(targetPath);
                }
                catch (Exception)
                {
                    return null;
                }
            };

        }

        public string Location { get; private set; } = Assembly.GetExecutingAssembly().Location;


        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;

            this.config = (Configuration)this.pluginInterface.GetPluginConfig() ?? new Configuration();
            this.config.Initialize(this.pluginInterface);

            this.ui = new PluginUI(this);
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.Draw;

            this.commandManager = new PluginCommandManager<Plugin>(this, this.pluginInterface);

            this.vm = new VisibilityManager(this);
            this.vm.Init();
        }

        [Command("/vtest")]
        [HelpMessage("Show visibility test config window.")]
        public void VTestConfig(string command, string args)
        {
            this.ui.IsVisible = true;
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.vm.Dispose();

            this.commandManager.Dispose();

            this.pluginInterface.SavePluginConfig(this.config);

            this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.Draw;

            this.pluginInterface.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
