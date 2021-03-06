using System;
using System.Collections.Generic;
using System.Linq;

using PoeHUD.Controllers;
using PoeHUD.Framework;
using PoeHUD.Hud.UI;

using SharpDX;
using SharpDX.Direct3D9;

namespace PoeHUD.Hud.Preload
{
    public class PreloadAlertPlugin : SizedPlugin<PreloadAlertSettings>
    {
        private readonly HashSet<string> disp;

        private Dictionary<string, string> alertStrings;

        private int lastCount;

        public PreloadAlertPlugin(GameController gameController, Graphics graphics, PreloadAlertSettings settings)
            : base(gameController, graphics, settings)
        {
            disp = new HashSet<string>();
            InitAlertStrings();
            GameController.Area.OnAreaChange += CurrentArea_OnAreaChange;
            CurrentArea_OnAreaChange(GameController.Area);
        }

        public override void Render()
        {
            base.Render();
            if (!Settings.Enable)
            {
                return;
            }

            Memory memory = GameController.Memory;
            int count = memory.ReadInt(memory.AddressOfProcess + memory.offsets.FileRoot, 12);
            if (count != lastCount)
            {
                lastCount = count;
                Parse();
            }

            if (disp.Count > 0)
            {
                Vector2 startPosition = StartDrawPointFunc();
                Vector2 position = startPosition;
                int maxWidth = 0;
                foreach (string current in disp)
                {
                    Size2 size = Graphics.DrawText(current, Settings.TextSize, position, FontDrawFlags.Right);
                    if (size.Width + 10 > maxWidth)
                    {
                        maxWidth = size.Width + 10;
                    }
                    position.Y += size.Height;
                }
                if (maxWidth > 0)
                {
                    var bounds = new RectangleF(startPosition.X - maxWidth + 5, startPosition.Y - 5,
                        maxWidth, position.Y - startPosition.Y + 10);
                    Graphics.DrawBox(bounds, Settings.BackgroundColor);
                    Size = bounds.Size;
                    Margin = new Vector2(0, 5);
                }
            }
        }

        private void CurrentArea_OnAreaChange(AreaController area)
        {
            if (Settings.Enable)
            {
                Parse();
            }
        }

        private void InitAlertStrings()
        {
            alertStrings = LoadConfig("config/preload_alerts.txt");
        }

        private void Parse()
        {
            disp.Clear();
            Memory memory = GameController.Memory;
            int pFileRoot = memory.ReadInt(memory.AddressOfProcess + memory.offsets.FileRoot);
            int count = memory.ReadInt(pFileRoot + 12);
            int listIterator = memory.ReadInt(pFileRoot + 20);
            int areaChangeCount = GameController.Game.AreaChangeCount;
            for (int i = 0; i < count; i++)
            {
                listIterator = memory.ReadInt(listIterator);
                if (memory.ReadInt(listIterator + 8) != 0 && memory.ReadInt(listIterator + 12, 36) == areaChangeCount)
                {
                    string text = memory.ReadStringU(memory.ReadInt(listIterator + 8));
                    if (text.Contains("vaal_sidearea"))
                    {
                        disp.Add("Area contains Corrupted Area");
                    }
                    if (text.Contains('@'))
                    {
                        text = text.Split(new[] { '@' })[0];
                    }
                    if (text.StartsWith("Metadata/Monsters/Missions/MasterStrDex"))
                    {
                        Console.WriteLine("bad alert " + text);
                        disp.Add("Area contains Vagan, Weaponmaster");
                    }
                    if (alertStrings.ContainsKey(text))
                    {
                        Console.WriteLine("Alert because of " + text);
                        disp.Add(alertStrings[text]);
                    }
                    else if (text.EndsWith("BossInvasion"))
                    {
                        disp.Add("Area contains Invasion Boss");
                    }
                }
            }
        }
    }
}