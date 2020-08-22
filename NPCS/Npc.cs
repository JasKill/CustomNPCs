﻿using Exiled.API.Features;
using MEC;
using Mirror;
using NPCS.Events;
using NPCS.Navigation;
using NPCS.Talking;
using RemoteAdmin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace NPCS
{
    //This component provides interface to control NPC. It can be created multiple times for NPC
    internal class Npc
    {
        //For simpler saving
        private class NPCSerializeInfo
        {
            private readonly Npc parent;

            public NPCSerializeInfo(Npc which)
            {
                parent = which;
            }

            public string name
            {
                get
                {
                    return parent.Name;
                }
            }

            public int role
            {
                get
                {
                    return (int)parent.Role;
                }
            }

            public int item_held
            {
                get
                {
                    return (int)parent.ItemHeld;
                }
            }

            public string root_node
            {
                get
                {
                    return Path.GetFileName(parent.RootNode.NodeFile);
                }
            }

            public bool god_mode
            {
                get
                {
                    return Player.Get(parent.GameObject).IsGodModeEnabled;
                }
            }

            public bool is_exclusive
            {
                get
                {
                    return parent.IsExclusive;
                }
            }
        }

        public enum MovementDirection
        {
            NONE,
            FORWARD,
            BACKWARD,
            LEFT,
            RIGHT
        };

        public GameObject GameObject { get; set; }

        public ReferenceHub ReferenceHub
        {
            get
            {
                return GameObject.GetComponent<ReferenceHub>();
            }
        }

        public NPCComponent NPCComponent
        {
            get
            {
                return GameObject.GetComponent<NPCComponent>();
            }
        }

        public TalkNode RootNode
        {
            get
            {
                return NPCComponent.root_node;
            }
            set
            {
                NPCComponent.root_node = value;
            }
        }

        public string Name
        {
            get
            {
                return GameObject.GetComponent<NicknameSync>().Network_myNickSync;
            }
            set
            {
                GameObject.GetComponent<NicknameSync>().Network_myNickSync = value;
            }
        }

        public Dictionary<Player, TalkNode> TalkingStates
        {
            get
            {
                return NPCComponent.talking_states;
            }
        }

        public RoleType Role
        {
            get
            {
                return GameObject.GetComponent<CharacterClassManager>().CurClass;
            }
            set
            {
                GameObject.GetComponent<CharacterClassManager>().CurClass = value;
            }
        }

        public ItemType ItemHeld
        {
            get
            {
                return GameObject.GetComponent<Inventory>().curItem;
            }
            set
            {
                GameObject.GetComponent<Inventory>().SetCurItem(value);
            }
        }

        public MovementDirection CurMovementDirection
        {
            get
            {
                return NPCComponent.curDir;
            }
            set
            {
                NPCComponent.curDir = value;
            }
        }

        public Vector3 Position
        {
            get
            {
                return GameObject.GetComponent<PlayerMovementSync>().RealModelPosition;
            }
            set
            {
                GameObject.GetComponent<PlayerMovementSync>().OverridePosition(value, 0f, false);
            }
        }

        public Vector2 Rotation
        {
            get
            {
                return GameObject.GetComponent<PlayerMovementSync>().RotationSync;
            }
            set
            {
                GameObject.GetComponent<PlayerMovementSync>().RotationSync = value;
            }
        }

        public bool IsLocked
        {
            get
            {
                return NPCComponent.locked;
            }
            set
            {
                NPCComponent.locked = value;
            }
        }

        public Player LockHandler
        {
            get
            {
                return NPCComponent.lock_handler;
            }
            set
            {
                NPCComponent.lock_handler = value;
            }
        }

        public bool IsActionLocked
        {
            get
            {
                return NPCComponent.action_locked;
            }
            set
            {
                NPCComponent.action_locked = value;
            }
        }

        public bool IsExclusive
        {
            get
            {
                return NPCComponent.is_exclusive;
            }
            set
            {
                NPCComponent.is_exclusive = value;
            }
        }

        public Dictionary<string, Dictionary<NodeAction, Dictionary<string, string>>> Events
        {
            get
            {
                return NPCComponent.attached_events;
            }
        }

        public Queue<NavigationNode> NavigationQueue
        {
            get
            {
                return NPCComponent.nav_queue;
            }
        }

        public NavigationNode CurrentNavTarget
        {
            get
            {
                return NPCComponent.nav_current_target;
            }
            set
            {
                NPCComponent.nav_current_target = value;
            }
        }

        public Player FollowTarget
        {
            get
            {
                return NPCComponent.follow_target;
            }
            set
            {
                NPCComponent.follow_target = value;
            }
        }

        public Npc(GameObject obj)
        {
            GameObject = obj;
        }

        //------------------------------------------ Coroutines

        private static IEnumerator<float> NavCoroutine(NPCComponent cmp)
        {
            Npc npc = Npc.FromComponent(cmp);
            for (; ; )
            {
                if (npc.FollowTarget != null)
                {
                    if (npc.FollowTarget.IsAlive)
                    {
                        npc.GoTo(npc.FollowTarget.Position);
                    }
                    else
                    {
                        npc.FollowTarget = null;
                    }
                }
                else
                {
                    if (!npc.NavigationQueue.IsEmpty()){
                        npc.CurrentNavTarget = npc.NavigationQueue.Dequeue();
                        yield return Timing.WaitForSeconds(npc.GoTo(npc.CurrentNavTarget.Position) + 0.1f);
                        npc.CurrentNavTarget = null;
                    }
                }
                yield return Timing.WaitForSeconds(0.1f);
            }
        }

        private static IEnumerator<float> UpdateTalking(NPCComponent cmp)
        {
            for (; ; )
            {
                List<Player> invalid_players = new List<Player>();
                foreach (Player p in cmp.talking_states.Keys)
                {
                    if (!p.IsAlive || !Player.List.Contains(p))
                    {
                        invalid_players.Add(p);
                    }
                }
                foreach (Player p in invalid_players)
                {
                    cmp.talking_states.Remove(p);
                    if (p == cmp.lock_handler)
                    {
                        cmp.lock_handler = null;
                        cmp.locked = false;
                    }
                }
                yield return Timing.WaitForSeconds(0.5f);
            }
        }

        private static IEnumerator<float> MoveCoroutine(NPCComponent cmp)
        {
            for (; ; )
            {
                switch (cmp.curDir)
                {
                    case MovementDirection.FORWARD:
                        try
                        {
                            if (!Physics.Linecast(cmp.transform.position, cmp.transform.position + cmp.GetComponent<ReferenceHub>().PlayerCameraReference.forward / 10, cmp.GetComponent<PlayerMovementSync>().CollidableSurfaces))
                            {
                                cmp.GetComponent<PlayerMovementSync>().OverridePosition(cmp.transform.position + cmp.GetComponent<ReferenceHub>().PlayerCameraReference.forward / 10, cmp.transform.rotation.y, true);
                            }
                        }
                        catch (Exception e) { }
                        break;

                    case MovementDirection.BACKWARD:
                        try
                        {
                            if (!Physics.Linecast(cmp.transform.position, cmp.transform.position - cmp.GetComponent<ReferenceHub>().PlayerCameraReference.forward / 10, cmp.GetComponent<PlayerMovementSync>().CollidableSurfaces))
                            {
                                cmp.GetComponent<PlayerMovementSync>().OverridePosition(cmp.transform.position - cmp.GetComponent<ReferenceHub>().PlayerCameraReference.forward / 10, cmp.transform.rotation.y, true);
                            }
                        }
                        catch (Exception e) { }
                        break;

                    case MovementDirection.LEFT:
                        try
                        {
                            if (!Physics.Linecast(cmp.transform.position, cmp.transform.position + Quaternion.AngleAxis(90, Vector3.up) * cmp.GetComponent<ReferenceHub>().PlayerCameraReference.forward / 10, cmp.GetComponent<PlayerMovementSync>().CollidableSurfaces))
                            {
                                cmp.GetComponent<PlayerMovementSync>().OverridePosition(cmp.transform.position + Quaternion.AngleAxis(90, Vector3.up) * cmp.GetComponent<ReferenceHub>().PlayerCameraReference.forward / 10, cmp.transform.rotation.y, true);
                            }
                        }
                        catch (Exception e) { }
                        break;

                    case MovementDirection.RIGHT:
                        try
                        {
                            if (!Physics.Linecast(cmp.transform.position, cmp.transform.position - Quaternion.AngleAxis(90, Vector3.up) * cmp.GetComponent<ReferenceHub>().PlayerCameraReference.forward / 10, cmp.GetComponent<PlayerMovementSync>().CollidableSurfaces))
                            {
                                cmp.GetComponent<PlayerMovementSync>().OverridePosition(cmp.transform.position - Quaternion.AngleAxis(90, Vector3.up) * cmp.GetComponent<ReferenceHub>().PlayerCameraReference.forward / 10, cmp.transform.rotation.y, true);
                            }
                        }
                        catch (Exception e) { }

                        break;

                    default:
                        break;
                }
                yield return Timing.WaitForSeconds(0.1f);
            }
        }

        private IEnumerator<float> StartTalkCoroutine(Player p)
        {
            IsLocked = true;
            LockHandler = p;
            TalkingStates.Add(p, RootNode);
            bool end = RootNode.Send(Name, p);
            IsActionLocked = true;
            foreach (NodeAction action in RootNode.Actions.Keys)
            {
                action.Process(this, p, RootNode.Actions[action]);
                yield return Timing.WaitForSeconds(float.Parse(RootNode.Actions[action]["next_action_delay"].Replace('.', ',')));
            }
            IsActionLocked = false;
            if (end)
            {
                TalkingStates.Remove(p);
                p.SendConsoleMessage(Name + " ended talk", "yellow");
                IsLocked = false;
            }
        }

        private IEnumerator<float> HandleAnswerCoroutine(Player p, string answer)
        {
            if (TalkingStates.ContainsKey(p))
            {
                TalkNode cur_node = TalkingStates[p];
                if (int.TryParse(answer, out int node))
                {
                    if (cur_node.NextNodes.TryGet(node, out TalkNode new_node))
                    {
                        TalkingStates[p] = new_node;
                        IsActionLocked = true;
                        bool end = new_node.Send(Name, p);
                        foreach (NodeAction action in new_node.Actions.Keys)
                        {
                            try
                            {
                                action.Process(this, p, new_node.Actions[action]);
                            }
                            catch (Exception e)
                            {
                                Log.Error($"Exception during processing action {action.Name}: {e}");
                            }
                            yield return Timing.WaitForSeconds(float.Parse(new_node.Actions[action]["next_action_delay"].Replace('.', ',')));
                        }
                        IsActionLocked = false;
                        if (end)
                        {
                            TalkingStates.Remove(p);
                            p.SendConsoleMessage(Name + " ended talk", "yellow");
                            IsLocked = false;
                        }
                    }
                    else
                    {
                        p.SendConsoleMessage("Invalid answer!", "red");
                    }
                }
                else
                {
                    p.SendConsoleMessage("Incorrect answer format!", "red");
                }
            }
            else
            {
                p.SendConsoleMessage("You aren't talking to this NPC!", "red");
            }
        }

        //------------------------------------------

        public void Move(MovementDirection dir)
        {
            CurMovementDirection = dir;
            switch (dir)
            {
                case MovementDirection.FORWARD:
                    ReferenceHub.animationController.Networkspeed = new Vector2(1, 0);
                    break;

                case MovementDirection.BACKWARD:
                    ReferenceHub.animationController.Networkspeed = new Vector2(-1, 0);
                    break;

                case MovementDirection.RIGHT:
                    ReferenceHub.animationController.Networkspeed = new Vector2(0, 1);
                    break;

                case MovementDirection.LEFT:
                    ReferenceHub.animationController.Networkspeed = new Vector2(0, -1);
                    break;

                default:
                    ReferenceHub.animationController.Networkspeed = new Vector2(0, 0);
                    break;
            }
        }

        public void AddNavTarget(NavigationNode node)
        {
            NavigationQueue.Enqueue(node);
        }

        public void ClearNavTargets()
        {
            NavigationQueue.Clear();
        }

        public void Follow(Player p)
        {
            FollowTarget = p;
        }

        public float GoTo(Vector3 position)
        {
            IsActionLocked = true;
            Timing.KillCoroutines(NPCComponent.movement_coroutines);
            Vector3 heading = (position - Position);
            Quaternion lookRot = Quaternion.LookRotation(heading.normalized);
            Player pl = Player.Get(GameObject);
            float dist = heading.magnitude;
            Rotation = new Vector2(lookRot.eulerAngles.x, lookRot.eulerAngles.y);
            Move(MovementDirection.FORWARD);
            float eta = 0.1f * (dist / (pl.CameraTransform.forward / 10).magnitude);
            NPCComponent.movement_coroutines.Add(Timing.CallDelayed(eta, () =>
            {
                Move(MovementDirection.NONE);
                IsActionLocked = false;
            }));
            return eta;
        }

        public void TalkWith(Player p)
        {
            NPCComponent.attached_coroutines.Add(Timing.RunCoroutine(StartTalkCoroutine(p)));
        }

        public void HandleAnswer(Player p, string answer)
        {
            if (!IsActionLocked)
            {
                NPCComponent.attached_coroutines.Add(Timing.RunCoroutine(HandleAnswerCoroutine(p, answer)));
            }
            else
            {
                p.SendConsoleMessage($"[{Name}] I'm busy now, wait a second", "yellow");
            }
        }

        public static Npc CreateNPC(Vector3 pos, Vector2 rot, RoleType type = RoleType.ClassD, ItemType itemHeld = ItemType.None, string name = "(EMPTY)", string root_node = "default_node.yml")
        {
            GameObject obj =
                UnityEngine.Object.Instantiate(
                    NetworkManager.singleton.spawnPrefabs.FirstOrDefault(p => p.gameObject.name == "Player"));
            CharacterClassManager ccm = obj.GetComponent<CharacterClassManager>();

            NPCComponent npcc = obj.AddComponent<NPCComponent>();

            obj.transform.localScale = Vector3.one;
            obj.transform.position = pos;

            obj.GetComponent<QueryProcessor>().NetworkPlayerId = QueryProcessor._idIterator++;
            obj.GetComponent<QueryProcessor>()._ipAddress = "127.0.0.WAN";

            if (Plugin.Instance.Config.DisplayNPCInPlayerList)
            {
                ccm._privUserId = $"{name}-{obj.GetComponent<QueryProcessor>().PlayerId }@NPC";
            }

            ccm.CurClass = type;
            obj.GetComponent<PlayerStats>().SetHPAmount(ccm.Classes.SafeGet(type).maxHP);

            obj.GetComponent<NicknameSync>().Network_myNickSync = name;

            obj.GetComponent<ServerRoles>().MyText = "NPC";
            obj.GetComponent<ServerRoles>().MyColor = "red";

            NetworkServer.Spawn(obj);
            PlayerManager.AddPlayer(obj); //I'm not sure if I need this

            Player ply_obj = new Player(obj);
            Player.Dictionary.Add(obj, ply_obj);

            Player.IdsCache.Add(ply_obj.Id, ply_obj);

            if (Plugin.Instance.Config.DisplayNPCInPlayerList)
            {
                Player.UserIdsCache.Add(ccm._privUserId, ply_obj);
            }

            Npc b = new Npc(obj)
            {
                RootNode = (TalkNode.FromFile(Path.Combine(Config.NPCs_nodes_path, root_node))),
                ItemHeld = (itemHeld)
            };

            npcc.attached_coroutines.Add(Timing.RunCoroutine(UpdateTalking(npcc)));
            npcc.attached_coroutines.Add(Timing.RunCoroutine(MoveCoroutine(npcc)));
            npcc.attached_coroutines.Add(Timing.RunCoroutine(NavCoroutine(npcc)));
            npcc.attached_coroutines.Add(Timing.CallDelayed(0.3f, () =>
            {
                b.ReferenceHub.playerMovementSync.OverridePosition(pos, 0f, true);
                b.Rotation = rot;
            }));

            return b;
        }

        //NPC format
        //name: SomeName
        //role: 6
        //item_held: 24
        //root_node: default_node.yml
        //god_mode: false
        //is_exclusive: true
        //events: []
        public static Npc CreateNPC(Vector3 pos, Vector2 rot, string path)
        {
            try
            {
                var input = new StringReader(File.ReadAllText(Path.Combine(Config.NPCs_root_path, path)));

                var yaml = new YamlStream();
                yaml.Load(input);

                var mapping =
                    (YamlMappingNode)yaml.Documents[0].RootNode;

                Npc n = CreateNPC(pos, rot, (RoleType)int.Parse((string)mapping.Children[new YamlScalarNode("role")]), (ItemType)int.Parse((string)mapping.Children[new YamlScalarNode("item_held")]), (string)mapping.Children[new YamlScalarNode("name")], (string)mapping.Children[new YamlScalarNode("root_node")]);
                if (bool.Parse((string)mapping.Children[new YamlScalarNode("god_mode")]))
                {
                    Player pl = Player.Get(n.GameObject);
                    pl.IsGodModeEnabled = true;
                }
                n.IsExclusive = bool.Parse((string)mapping.Children[new YamlScalarNode("is_exclusive")]);

                Log.Info("Parsing events...");

                YamlSequenceNode events = (YamlSequenceNode)mapping.Children[new YamlScalarNode("events")];

                foreach (YamlMappingNode event_node in events.Children)
                {
                    var actions = (YamlSequenceNode)event_node.Children[new YamlScalarNode("actions")];
                    Dictionary<NodeAction, Dictionary<string, string>> actions_mapping = new Dictionary<NodeAction, Dictionary<string, string>>();
                    foreach (YamlMappingNode action_node in actions)
                    {
                        NodeAction act = NodeAction.GetFromToken((string)action_node.Children[new YamlScalarNode("token")]);
                        if (act != null)
                        {
                            Log.Debug($"Recognized action: {act.Name}", Plugin.Instance.Config.VerboseOutput);
                            var yml_args = (YamlMappingNode)action_node.Children[new YamlScalarNode("args")];
                            Dictionary<string, string> arg_bindings = new Dictionary<string, string>();
                            foreach (YamlScalarNode arg in yml_args.Children.Keys)
                            {
                                arg_bindings.Add((string)arg.Value, (string)yml_args.Children[arg]);
                            }
                            actions_mapping.Add(act, arg_bindings);
                        }
                        else
                        {
                            Log.Error($"Failed to parse action: {(string)action_node.Children[new YamlScalarNode("token")]} (invalid token)");
                        }
                    }
                    n.Events.Add((string)event_node.Children[new YamlScalarNode("token")], actions_mapping);
                }
                return n;
            }
            catch (Exception e)
            {
                Log.Error($"Failed to load NPC from {path}: {e}");
                return null;
            }
        }

        public static Npc FromComponent(NPCComponent npc)
        {
            return new Npc(npc.gameObject);
        }

        public void Kill(bool spawn_ragdoll)
        {
            if (spawn_ragdoll)
            {
                GameObject.GetComponent<RagdollManager>().SpawnRagdoll(GameObject.transform.position, GameObject.transform.rotation, Vector3.zero, (int)ReferenceHub.characterClassManager.CurClass, new PlayerStats.HitInfo(), false, "", Name, 9999);
            }
            UnityEngine.Object.Destroy(GameObject);
        }

        public void Serialize(string path)
        {
            path = Path.Combine(Config.NPCs_root_path, path);
            StreamWriter sw;
            if (!File.Exists(path))
            {
                sw = File.CreateText(path);
                var serializer = new SerializerBuilder().Build();
                NPCSerializeInfo info = new NPCSerializeInfo(this);
                var yaml = serializer.Serialize(info);
                sw.Write(yaml);
                sw.Close();
            }
            else
            {
                Log.Error("Failed to save npc: File exists!");
            }
        }

        public void FireEvent(NPCEvent ev)
        {
            try
            {
                ev.FireActions(Events[ev.Name]);
            }
            catch (KeyNotFoundException e)
            {
                Log.Debug($"Skipping unused event {ev.Name}", Plugin.Instance.Config.VerboseOutput);
            }
        }
    }
}