using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace REPOMenu.Core
{
    // Small, null-safe questions about the game's state. Everything here may be asked from OnGUI
    // while the game is loading, so nothing assumes a singleton exists.
    internal static class Game
    {
        private static readonly List<PlayerAvatar> empty = new();

        public static bool Ready => GameManager.instance && RunManager.instance && GameDirector.instance;

        public static bool Multiplayer => GameManager.instance && GameManager.instance.gameMode == 1;

        // host of a multiplayer lobby, or anyone playing alone
        public static bool IsHost => !Multiplayer || PhotonNetwork.IsMasterClient;

        public static bool IsGuest => Multiplayer && !PhotonNetwork.IsMasterClient;

        public static PlayerAvatar Local => PlayerAvatar.instance;

        public static PlayerController Controller => PlayerController.instance;

        public static bool InMenuLevel => Ready && Safe.Get("game.menulevel", SemiFunc.MenuLevel, true);

        // a level is loaded and the local player exists in it (levels, shop, truck lobby, arena)
        public static bool InWorld => Ready && !InMenuLevel && LevelGenerator.Instance && LevelGenerator.Instance.Generated && Local && Controller;

        // a real run level (not the shop or the truck lobby)
        public static bool InRunLevel => InWorld && Safe.Get("game.runislevel", SemiFunc.RunIsLevel);

        public static Camera Camera
        {
            get
            {
                if (GameDirector.instance && GameDirector.instance.MainCamera)
                {
                    return GameDirector.instance.MainCamera;
                }

                return Camera.main;
            }
        }

        public static List<PlayerAvatar> Players => GameDirector.instance && GameDirector.instance.PlayerList != null ? GameDirector.instance.PlayerList : empty;

        public static string PlayerName(PlayerAvatar p)
        {
            if (!p)
            {
                return "?";
            }

            return string.IsNullOrEmpty(p.playerName) ? "Player" : p.playerName;
        }

        public static string Version => BuildManager.instance && BuildManager.instance.version ? BuildManager.instance.version.title : "?";

        public static string Status
        {
            get
            {
                if (!Ready)
                {
                    return "Loading";
                }

                if (!Multiplayer)
                {
                    return "Singleplayer: everything works";
                }

                return PhotonNetwork.IsMasterClient ? "Host: everything works" : "Guest: host-only cards are locked";
            }
        }

        public static Vector3 LocalPosition => Controller ? Controller.transform.position : (Local ? Local.transform.position : Vector3.zero);

        // where the player is looking, up to a distance, stopping at the first wall or object
        public static Vector3 AimPoint(float maxDistance = 60f, float backOff = 0.6f)
        {
            var cam = Camera;

            if (!cam)
            {
                return LocalPosition;
            }

            var ray = new Ray(cam.transform.position, cam.transform.forward);

            if (Physics.Raycast(ray, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point + hit.normal * backOff;
            }

            return ray.GetPoint(Mathf.Min(maxDistance, 8f));
        }

        public static string Money(int value) => "$" + value.ToString("N0") + "K";
    }
}
