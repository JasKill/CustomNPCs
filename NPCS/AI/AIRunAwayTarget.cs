﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NPCS.AI
{
    class AIRunAwayTarget : AITarget
    {
        public override string Name => "AIRunAwayTarget";

        public override string[] RequiredArguments => new string[] { };

        public override bool Check(Npc npc)
        {
            return npc.CurrentAIPlayerTarget != null;
        }

        public override void Construct()
        {
            
        }

        public override float Process(Npc npc)
        {
            return 0f;
        }

        protected override AITarget CreateInstance()
        {
            return new AIRunAwayTarget();
        }
    }
}
