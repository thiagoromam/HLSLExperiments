using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

// ReSharper disable ForCanBeConvertedToForeach

namespace MapEditor.MapClasses
{
    internal class Map
    {
        public Map()
        {
            SegmentDefinitions = new SegmentDefinition[512];
            Segments = new MapSegment[3, 64];
            Grid = new int[20, 20];
            Scripts = new String[128];
            Path = "map";

            Ledges = new Ledge[16];

            for (var i = 0; i < 16; i++)
                Ledges[i] = new Ledge();

            ReadSegmentDefinitions();

            for (var i = 0; i < Scripts.Length; i++)
                Scripts[i] = "";
        }

        public Ledge[] Ledges { get; private set; }
        public int[,] Grid { get; private set; }
        public MapSegment[,] Segments { get; private set; }
        public SegmentDefinition[] SegmentDefinitions { get; private set; }
        public string Path { get; set; }
        public string[] Scripts { get; set; }

        public void Draw(SpriteBatch sprite, Texture2D[] mapsTex, Vector2 scroll)
        {
            var dRect = new Rectangle();

            sprite.Begin();

            for (var l = 0; l < 3; l++)
            {
                var scale = 1.0f;

                var color = Color.White;

                if (l == 0)
                {
                    color = Color.Gray;
                    scale = 0.75f;
                }
                else if (l == 2)
                {
                    color = Color.DarkGray;
                    scale = 1.25f;
                }

                scale *= 0.5f;

                for (var i = 0; i < 64; i++)
                {
                    if (Segments[l, i] != null)
                    {
                        var sRect = SegmentDefinitions[Segments[l, i].Index].SourceRect;

                        dRect.X = (int)(Segments[l, i].Location.X - scroll.X * scale);
                        dRect.Y = (int)(Segments[l, i].Location.Y - scroll.Y * scale);
                        dRect.Width = (int)(sRect.Width * scale);
                        dRect.Height = (int)(sRect.Height * scale);

                        sprite.Draw(mapsTex[SegmentDefinitions[Segments[l, i].Index].SourceIndex],
                            dRect,
                            sRect,
                            color);
                    }
                }
            }

            sprite.End();
        }

        public void Write()
        {
            var file = new BinaryWriter(File.Open(@"data/" + Path + ".zmx", FileMode.Create));

            for (var i = 0; i < Ledges.Length; i++)
            {
                file.Write(Ledges[i].TotalNodes);
                for (var n = 0; n < Ledges[i].TotalNodes; n++)
                {
                    file.Write(Ledges[i].Nodes[n].X);
                    file.Write(Ledges[i].Nodes[n].Y);
                }
                file.Write(Ledges[i].Flags);
            }

            for (var l = 0; l < 3; l++)
            {
                for (var i = 0; i < 64; i++)
                {
                    if (Segments[l, i] == null)
                        file.Write(-1);
                    else
                    {
                        file.Write(Segments[l, i].Index);
                        file.Write(Segments[l, i].Location.X);
                        file.Write(Segments[l, i].Location.Y);
                    }
                }
            }

            for (var x = 0; x < 20; x++)
            {
                for (var y = 0; y < 20; y++)
                {
                    file.Write(Grid[x, y]);
                }
            }

            for (var i = 0; i < Scripts.Length; i++)
                file.Write(Scripts[i]);

            file.Close();
        }

        public void Read()
        {
            var file = new BinaryReader(File.Open(@"data/" + Path + ".zmx", FileMode.Open));

            for (var i = 0; i < Ledges.Length; i++)
            {
                Ledges[i] = new Ledge { TotalNodes = file.ReadInt32() };
                for (var n = 0; n < Ledges[i].TotalNodes; n++)
                {
                    Ledges[i].Nodes[n] = new Vector2(file.ReadSingle(), file.ReadSingle());
                }
                Ledges[i].Flags = file.ReadInt32();
            }

            for (var l = 0; l < 3; l++)
            {
                for (var i = 0; i < 64; i++)
                {
                    var t = file.ReadInt32();

                    if (t == -1)
                        Segments[l, i] = null;
                    else
                    {
                        Segments[l, i] = new MapSegment
                        {
                            Index = t,
                            Location = new Vector2(file.ReadSingle(), file.ReadSingle())
                        };
                    }
                }
            }

            for (var x = 0; x < 20; x++)
            {
                for (var y = 0; y < 20; y++)
                {
                    Grid[x, y] = file.ReadInt32();
                }
            }

            for (var i = 0; i < Scripts.Length; i++)
                Scripts[i] = file.ReadString();
            
            file.Close();
        }

        private void ReadSegmentDefinitions()
        {
            var s = new StreamReader(@"Content/maps.zdx");

            var currentTex = 0;
            var curDef = -1;

            var tRect = new Rectangle();

            s.ReadLine();

            while (!s.EndOfStream)
            {
                // ReSharper disable PossibleNullReferenceException
                var t = s.ReadLine();

                string[] split;
                if (t.StartsWith("#"))
                {
                    if (t.StartsWith("#src"))
                    {
                        split = t.Split(' ');
                        if (split.Length > 1)
                        {
                            var n = Convert.ToInt32(split[1]);
                            currentTex = n - 1;
                        }
                    }
                }
                else
                {
                    curDef++;

                    var name = t;

                    t = s.ReadLine();
                    split = t.Split(' ');

                    if (split.Length > 3)
                    {
                        tRect.X = Convert.ToInt32(split[0]);
                        tRect.Y = Convert.ToInt32(split[1]);
                        tRect.Width = Convert.ToInt32(split[2]) - tRect.X;
                        tRect.Height = Convert.ToInt32(split[3]) - tRect.Y;
                    }
                    else
                        Console.WriteLine("read fail: " + name);

                    var tex = currentTex;

                    t = s.ReadLine();
                    var flags = Convert.ToInt32(t);

                    SegmentDefinitions[curDef] = new SegmentDefinition(name, tex, tRect, flags);
                }
                // ReSharper restore PossibleNullReferenceException
            }
        }

        public int GetHoveredSegment(int x, int y, int l, Vector2 scroll)
        {
            var scale = 1.0f;
            if (l == 0)
                scale = 0.75f;
            else if (l == 2)
                scale = 1.25f;

            scale *= 0.5f;

            for (var i = 63; i >= 0; i--)
            {
                if (Segments[l, i] != null)
                {
                    var sRect = SegmentDefinitions[Segments[l, i].Index].SourceRect;
                    var dRect = new Rectangle(
                        (int)(Segments[l, i].Location.X - scroll.X * scale),
                        (int)(Segments[l, i].Location.Y - scroll.Y * scale),
                        (int)(sRect.Width * scale),
                        (int)(sRect.Height * scale)
                        );

                    if (dRect.Contains(x, y))
                        return i;
                }
            }

            return -1;
        }

        public int AddSeg(int layer, int index)
        {
            for (var i = 0; i < 64; i++)
            {
                if (Segments[layer, i] == null)
                {
                    Segments[layer, i] = new MapSegment { Index = index };

                    return i;
                }
            }

            return -1;
        }
    }
}