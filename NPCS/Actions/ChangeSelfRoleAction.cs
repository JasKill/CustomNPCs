﻿using Exiled.API.Features;
using System;
using System.Collections.Generic;

namespace NPCS.Actions
{
    internal class ChangeSelfRoleAction : Talking.NodeAction
    {
        public override string Name => "ChangeSelfRoleAction";

        public override void Process(Npc npc, Player player, Dictionary<string, string> args)
        {
            npc.Role = (RoleType)int.Parse(args["role"]);
            if (!bool.Parse(args["preserve_position"]))
            {
                npc.OverridePosition(Map.GetRandomSpawnPoint(npc.Role));
            }
        }
    }
}