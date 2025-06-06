using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
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
    public class vMarkerConfig : BasePluginConfig
    {
        [JsonPropertyName("TimeToCleanUp")]
        public float TimeToCleanUp { get; set; } = 30f;
    }

    public class marker : BasePlugin, IPluginConfig<vMarkerConfig>
    {
        public List<CBeam> markerlasers = new List<CBeam>();
        public override string ModuleName => "vMarker";
        public override string ModuleVersion => "0.0.3";
        public override string ModuleAuthor => "varkit";

        public bool isDrawingActive = false;
        private float drawingZ = 0f;
        private Timer? cleanupTimer = null;
        private float TimeToCleanUp = 30f;
        private bool isRoundActive = false;

        public vMarkerConfig Config { get; set; }

        public void OnConfigParsed(vMarkerConfig config)
        {
            Config = config;
            TimeToCleanUp = config.TimeToCleanUp;
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
            isDrawingActive = false;
            if (cleanupTimer != null)
            {
                cleanupTimer.Kill();
                cleanupTimer = null;
            }

            foreach (var laser in markerlasers)
            {
                laser.Remove();
            }
            markerlasers.Clear();

            return HookResult.Continue;
        }


        [ConsoleCommand("css_vmarker")]
        public void OnMarkerCommand(CCSPlayerController? player, CommandInfo commandInfo)
        {
            if (player == null || !isRoundActive)
                return;

            if (!isDrawingActive)
            {
                if (cleanupTimer != null)
                {
                    cleanupTimer.Kill();
                    cleanupTimer = null;
                }

                foreach (var laser in markerlasers)
                {
                    laser.Remove();
                }
                markerlasers.Clear();

                var trace = player.GetGameTraceByEyePosition(TraceMask.MaskAll, Contents.NoDraw, player);
                if (trace == null) return;

                var startVector = new Vector(trace.Value.Position.X, trace.Value.Position.Y, trace.Value.Position.Z);
                drawingZ = startVector.Z;

                DrawCircle(startVector, 50, Color.Red);

                AddTimer(0.02f, () =>
                {
                    if (!isDrawingActive || !player.IsValid || !isRoundActive) return;

                    var currentTrace = player.GetGameTraceByEyePosition(TraceMask.MaskAll, Contents.NoDraw, player);
                    if (currentTrace == null) return;

                    var lastPoint = markerlasers[markerlasers.Count - 1].EndPos;
                    var newPoint = new Vector(currentTrace.Value.Position.X, currentTrace.Value.Position.Y, drawingZ);

                    DrawLaserBetween(lastPoint, newPoint, Color.Blue);
                }, TimerFlags.REPEAT);

                isDrawingActive = true;
            }
            else
            {
                isDrawingActive = false;

                var trace = player.GetGameTraceByEyePosition(TraceMask.MaskAll, Contents.NoDraw, player);
                if (trace == null) return;

                var endVector = new Vector(trace.Value.Position.X, trace.Value.Position.Y, drawingZ);
                DrawCircle(endVector, 50, Color.Green);

                cleanupTimer = AddTimer(TimeToCleanUp, () =>
                {
                    foreach (var laser in markerlasers)
                    {
                        laser.Remove();
                    }
                    markerlasers.Clear();
                    cleanupTimer = null;
                });
            }
        }

        public void DrawCircle(Vector center, float radius, Color color)
        {
            float angleIncrement = (float)(2 * Math.PI / 70);
            Vector startPoint = new Vector(center.X + radius, center.Y, drawingZ);
            Vector previousPoint = startPoint;

            for (int i = 1; i <= 70; i++)
            {
                float angle = i * angleIncrement;
                Vector currentPoint = new Vector(
                    center.X + radius * (float)Math.Cos(angle),
                    center.Y + radius * (float)Math.Sin(angle),
                    drawingZ
                );
                DrawLaserBetween(previousPoint, currentPoint, color);
                previousPoint = currentPoint;
            }
            DrawLaserBetween(previousPoint, startPoint, color);
        }

        public void DrawLaserBetween(Vector startPos, Vector endPos, Color color)
        {
            CBeam beam = Utilities.CreateEntityByName<CBeam>("beam");
            if (beam == null) return;

            beam.Render = color;
            beam.Width = 3f;
            beam.Teleport(startPos, new QAngle(0, 0, 0), new Vector(0, 0, 0));
            beam.EndPos.X = endPos.X;
            beam.EndPos.Y = endPos.Y;
            beam.EndPos.Z = endPos.Z;
            beam.DispatchSpawn();
            markerlasers.Add(beam);
        }
    }
}
