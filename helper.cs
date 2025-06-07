using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vMarker
{
    public class helper
    {
        public vMarker plugin;
        public void RegisterCommandList(IEnumerable<string> commandList, string description, CommandInfo.CommandCallback handler)
        {
            if (commandList == null) return;

            foreach (string command in commandList)
            {
                if (!string.IsNullOrWhiteSpace(command))
                {
                    plugin.AddCommand(command.Trim(), description, handler);
                }
            }
        }

        public static bool HasPerm(CCSPlayerController player, string[] perm)
        {
            if (!perm.Any(p => AdminManager.PlayerHasPermissions(player, p))) return false;
            return true;
        }
    }
}
