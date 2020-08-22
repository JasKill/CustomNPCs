﻿using Exiled.API.Features;
using System.Collections.Generic;

namespace NPCS.Talking
{
    internal abstract class NodeAction
    {
        public abstract string Name { get; }

        public abstract void Process(NPCS.Npc npc, Player player, Dictionary<string, string> args);

        private static readonly Dictionary<string, NodeAction> registry = new Dictionary<string, NodeAction>();

        public static NodeAction GetFromToken(string token)
        {
            try
            {
                return registry[token];
            }
            catch (KeyNotFoundException e)
            {
                return null;
            }
        }

        public static void Register(NodeAction cond)
        {
            registry.Add(cond.Name, cond);
            Log.Debug($"Registered action token: {cond.Name}", Plugin.Instance.Config.VerboseOutput);
        }
    }
}