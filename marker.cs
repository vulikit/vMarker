using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CS2TraceRay.Class;
using CS2TraceRay.Enum;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json.Serialization;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace vMarker
{
    public class vMarker : BasePlugin
    {
        public override string ModuleName => "vMarker";
        public override string ModuleVersion => "0.0.3";
        public override string ModuleAuthor => "varkit";
        private Dictionary<CCSPlayerController, PlayerMarkerData> playerData = new Dictionary<CCSPlayerController, PlayerMarkerData>();
        public Cfg Config { get; set; }
        public helper helper { get; set; }

        private float TimeToCleanUp = 30f;
        private bool isRoundActive = false;

        public override void Load(bool hotReload)
        {
            vMarkerConfig.LoadConfig();
            Config = vMarkerConfig.Config ?? new Cfg { Settings = new xSettings() };
            helper = new helper();

            if (Config.Settings == null)
            {
                Config.Settings = new xSettings();
            }

            helper.RegisterCommandList(
                Config.Settings.MarkerCommands,
                "vMarker Commands",
                OnMarkerCommand
            );
        }

        [GameEventHandler]
        public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            isRoundActive = true;
            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            isRoundActive = false;
            foreach (var data in playerData.Values)
            {
                data.IsDrawingActive = false;
                if (data.CleanupTimer != null)
                {
                    data.CleanupTimer.Kill();
                    data.CleanupTimer = null;
                }
                foreach (var laser in data.MarkerLasers)
                {
                    laser?.Remove();
                }
                data.MarkerLasers.Clear();
            }
            playerData.Clear();
            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            var player = @event?.Userid;
            if (player != null && playerData.ContainsKey(player))
            {
                var data = playerData[player];
                if (data.CleanupTimer != null)
                {
                    data.CleanupTimer.Kill();
                }
                foreach (var laser in data.MarkerLasers)
                {
                    laser?.Remove();
                }
                playerData.Remove(player);
            }
            return HookResult.Continue;
        }

        public void OnMarkerCommand(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (player == null || !player.IsValid || !isRoundActive || Config?.Settings?.UseMarkerType != "command")
            {
                return;
            }

            if (!helper.HasPerm(player, Config?.Settings?.MarkerPermissions ?? new string[0]))
            {
                return;
            }

            if (!playerData.ContainsKey(player))
            {
                playerData[player] = new PlayerMarkerData();
            }

            var data = playerData[player];
            if (!data.IsDrawingActive)
            {
                UseMarker(player);
            }
            else
            {
                StopMarker(player);
            }
        }

        [GameEventHandler]
        public HookResult OnPlayerPing(EventPlayerPing @event, GameEventInfo info)
        {
            var player = @event?.Userid;
            if (player == null || !player.IsValid || !isRoundActive || Config?.Settings?.UseMarkerType != "ping")
            {
                return HookResult.Continue;
            }

            if (!helper.HasPerm(player, Config?.Settings?.MarkerPermissions ?? new string[0]))
            {
                info.DontBroadcast = true;
                return HookResult.Stop;
            }

            if (!playerData.ContainsKey(player))
            {
                playerData[player] = new PlayerMarkerData();
            }

            var data = playerData[player];
            if (!data.IsDrawingActive)
            {
                UseMarker(player);
            }
            else
            {
                StopMarker(player);
            }

            return HookResult.Continue;
        }

        public void UseMarker(CCSPlayerController player)
        {
            if (!playerData.ContainsKey(player))
            {
                playerData[player] = new PlayerMarkerData();
            }

            var data = playerData[player];

            if (data.CleanupTimer != null)
            {
                data.CleanupTimer.Kill();
                data.CleanupTimer = null;
            }

            foreach (var laser in data.MarkerLasers)
            {
                laser?.Remove();
            }
            data.MarkerLasers.Clear();

            var trace = player.GetGameTraceByEyePosition(TraceMask.MaskAll, Contents.NoDraw, player);
            if (trace == null)
            {
                return;
            }

            var startVector = new Vector(trace.Value.Position.X, trace.Value.Position.Y, trace.Value.Position.Z);
            data.DrawingZ = startVector.Z;

            DrawCircle(player, startVector, 50, Color.Red);

            AddTimer(0.02f, () =>
            {
                if (!data.IsDrawingActive || !player.IsValid || !isRoundActive)
                {
                    return;
                }
                var currentTrace = player.GetGameTraceByEyePosition(TraceMask.MaskAll, Contents.NoDraw, player);
                if (currentTrace == null)
                {
                    return;
                }

                if (data.MarkerLasers.Count == 0)
                {
                    return;
                }

                var lastPoint = data.MarkerLasers[data.MarkerLasers.Count - 1].EndPos;
                var newPoint = new Vector(currentTrace.Value.Position.X, currentTrace.Value.Position.Y, data.DrawingZ);

                DrawLaserBetween(player, lastPoint, newPoint, Color.Blue);
            }, TimerFlags.REPEAT);

            data.IsDrawingActive = true;
        }

        public void StopMarker(CCSPlayerController player)
        {
            if (!playerData.ContainsKey(player))
            {
                return;
            }

            var data = playerData[player];
            data.IsDrawingActive = false;

            var trace = player.GetGameTraceByEyePosition(TraceMask.MaskAll, Contents.NoDraw, player);
            if (trace == null)
            {
                return;
            }

            var endVector = new Vector(trace.Value.Position.X, trace.Value.Position.Y, data.DrawingZ);
            DrawCircle(player, endVector, 50, Color.Green);

            data.CleanupTimer = AddTimer(TimeToCleanUp, () =>
            {
                foreach (var laser in data.MarkerLasers)
                {
                    laser?.Remove();
                }
                data.MarkerLasers.Clear();
                data.CleanupTimer = null;
            });
        }

        public void DrawCircle(CCSPlayerController player, Vector center, float radius, Color color)
        {
            if (!playerData.ContainsKey(player))
            {
                return;
            }

            var data = playerData[player];
            float angleIncrement = (float)(2 * Math.PI / 70);
            Vector startPoint = new Vector(center.X + radius, center.Y, data.DrawingZ);
            Vector previousPoint = startPoint;

            for (int i = 1; i <= 70; i++)
            {
                float angle = i * angleIncrement;
                Vector currentPoint = new Vector(
                    center.X + radius * (float)Math.Cos(angle),
                    center.Y + radius * (float)Math.Sin(angle),
                    data.DrawingZ
                );
                DrawLaserBetween(player, previousPoint, currentPoint, color);
                previousPoint = currentPoint;
            }
            DrawLaserBetween(player, previousPoint, startPoint, color);
        }

        public void DrawLaserBetween(CCSPlayerController player, Vector startPos, Vector endPos, Color color)
        {
            if (!playerData.ContainsKey(player))
            {
                return;
            }

            var data = playerData[player];
            CBeam beam = Utilities.CreateEntityByName<CBeam>("beam");
            if (beam == null)
            {
                return;
            }

            beam.Render = color;
            beam.Width = 3f;
            beam.Teleport(startPos, new QAngle(0, 0, 0), new Vector(0, 0, 0));
            beam.EndPos.X = endPos.X;
            beam.EndPos.Y = endPos.Y;
            beam.EndPos.Z = endPos.Z;
            beam.DispatchSpawn();
            data.MarkerLasers.Add(beam);
        }
    }
    public class PlayerMarkerData
    {
        public bool IsDrawingActive { get; set; } = false;
        public List<CBeam> MarkerLasers { get; set; } = new List<CBeam>();
        public float DrawingZ { get; set; } = 0f;
        public Timer? CleanupTimer { get; set; } = null;
    }
}