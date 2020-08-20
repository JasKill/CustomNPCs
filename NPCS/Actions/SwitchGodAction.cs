﻿using Exiled.API.Features;
using NPCS.Talking;
using System.Collections.Generic;

namespace NPCS.Actions
{
    internal class SwitchGodAction : NodeAction
    {
        public override string Name => "SwitchGodAction";

        public override void Process(Npc npc, Player player, Dictionary<string, string> args)
        {
            Player.Get(npc.GameObject).IsGodModeEnabled = bool.Parse(args["value"]);
        }
    }
}