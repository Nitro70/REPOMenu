using REPOMenu.Core;

namespace REPOMenu.Features
{
    // Wires every feature into the menu (in sidebar order) and runs their per-frame work.
    internal static class Hub
    {
        public static void RegisterAll()
        {
            SelfMods.Register();
            Esp.Register();
            Visuals.Register();
            Teleport.Register();
            Character.Register();
            HostPlayers.Register();
            HostEnemies.Register();
            Spawner.Register();
            HostWorld.Register();
            MenuPages.Register();
        }

        public static void Tick()
        {
            Safe.Run("tick.self", SelfMods.Tick);
            Safe.Run("tick.character", Character.Tick);
            Safe.Run("tick.players", HostPlayers.Tick);
            Safe.Run("tick.enemies", HostEnemies.Tick);
            Safe.Run("tick.world", HostWorld.Tick);
        }

        public static void LateTick()
        {
            Safe.Run("tick.visuals", Visuals.LateTick);
        }
    }
}
