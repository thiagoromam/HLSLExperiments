using MapEditor.MapClasses;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TextLib;

namespace MapEditor
{
    public class Game1 : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        private Map _map;
        private Text _text;
        private SpriteFont _font;

        private Texture2D[] _mapsTex;
        private Texture2D _nullTex;
        private Texture2D _iconsTex;

        private int _mosX, _mosY;
        private bool _rightMouseDown;
        private bool _midMouseDown;
        private bool _mouseClick;

        private Vector2 _scroll;

        private int _mouseDragSeg = -1;
        private int _curLayer = 1;
        private int _pMosX, _pMosY;

        private int _curLedge;

        private int _scriptScroll;
        private int _selScript = -1;
        private const int ColorNone = 0;
        private const int ColorYellow = 1;
        private const int ColorGreen = 2;

        private KeyboardState _oldKeyState;

        private DrawingMode _drawType = DrawingMode.SegmentSelection;
        private EditingMode _editMode = EditingMode.None;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 800,
                PreferredBackBufferHeight = 600
            };
            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            base.Initialize();
            _map = new Map();
        }

        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _font = Content.Load<SpriteFont>(@"Fonts/Arial");

            _text = new Text(_spriteBatch, _font);

            _nullTex = Content.Load<Texture2D>(@"gfx/1x1");
            _mapsTex = new Texture2D[1];

            for (var i = 0; i < _mapsTex.Length; i++)
                _mapsTex[i] = Content.Load<Texture2D>(@"gfx/maps" + (i + 1));

            _iconsTex = Content.Load<Texture2D>(@"gfx/icons");
        }

        private bool GetCanEdit()
        {
            if (_mosX > 100 && _mosX < 500 && _mosY > 100 && _mosY < 550)
                return true;

            return false;
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                Exit();

            var mState = Mouse.GetState();

            _mosX = mState.X;
            _mosY = mState.Y;

            var pMouseDown = _rightMouseDown;

            if (mState.LeftButton == ButtonState.Pressed)
            {
                if (!_rightMouseDown && GetCanEdit())
                {
                    if (_drawType == DrawingMode.SegmentSelection)
                    {
                        var f = _map.GetHoveredSegment(_mosX, _mosY, _curLayer, _scroll);

                        if (f != -1)
                            _mouseDragSeg = f;
                    }
                    else if (_drawType == DrawingMode.CollisionMap)
                    {
                        var x = (_mosX + (int)(_scroll.X / 2)) / 32;
                        var y = (_mosY + (int)(_scroll.Y / 2)) / 32;
                        if (x >= 0 && y >= 0 && x < 20 && y < 20)
                        {
                            if (mState.LeftButton == ButtonState.Pressed)
                                _map.Grid[x, y] = 1;
                            else if (mState.RightButton == ButtonState.Pressed)
                                _map.Grid[x, y] = 0;
                        }
                    }
                    else if (_drawType == DrawingMode.Ledges)
                    {
                        if (_map.Ledges[_curLedge] == null)
                            _map.Ledges[_curLedge] = new Ledge();

                        if (_map.Ledges[_curLedge].TotalNodes < 15)
                        {
                            _map.Ledges[_curLedge].Nodes[_map.Ledges[_curLedge].TotalNodes] =
                                new Vector2(_mosX, _mosY) + _scroll / 2f;

                            _map.Ledges[_curLedge].TotalNodes++;
                        }
                    }
                    else if (_drawType == DrawingMode.Script)
                    {
                        if (_selScript > -1)
                        {
                            if (_mosX < 400)
                                _map.Scripts[_selScript] += " " + ((int)(_mosX + _scroll.X / 2f)) + " " +
                                                            ((int)(_mosY + _scroll.Y / 2f));
                        }
                    }
                }
                _rightMouseDown = true;
            }
            else
                _rightMouseDown = false;

            _midMouseDown = (mState.MiddleButton == ButtonState.Pressed);

            if (pMouseDown && !_rightMouseDown) _mouseClick = true;

            if (_mouseDragSeg > -1)
            {
                if (!_rightMouseDown)
                    _mouseDragSeg = -1;
                else
                {
                    var loc = _map.Segments[_curLayer, _mouseDragSeg].Location;

                    loc.X += (_mosX - _pMosX);
                    loc.Y += (_mosY - _pMosY);

                    _map.Segments[_curLayer, _mouseDragSeg].Location = loc;
                }
            }

            if (_midMouseDown)
            {
                _scroll.X -= (_mosX - _pMosX) * 2f;
                _scroll.Y -= (_mosY - _pMosY) * 2f;
            }

            _pMosX = _mosX;
            _pMosY = _mosY;

            UpdateKeys();

            base.Update(gameTime);
        }

        private void UpdateKeys()
        {
            var keyState = Keyboard.GetState();

            var currentKeys = keyState.GetPressedKeys();
            var lastKeys = _oldKeyState.GetPressedKeys();

            // ReSharper disable ForCanBeConvertedToForeach
            for (var i = 0; i < currentKeys.Length; i++)
            {
                var found = false;

                // ReSharper disable once LoopCanBeConvertedToQuery
                for (var y = 0; y < lastKeys.Length; y++)
                {
                    if (currentKeys[i] == lastKeys[y])
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    PressKey(currentKeys[i]);
            }
            // ReSharper restore ForCanBeConvertedToForeach

            _oldKeyState = keyState;
        }

        private void PressKey(Keys key)
        {
            string t;
            switch (_editMode)
            {
                case EditingMode.Path:
                    t = _map.Path;
                    break;
                case EditingMode.Script:
                    if (_selScript < 0)
                        return;
                    t = _map.Scripts[_selScript];
                    break;
                default:
                    return;
            }

            var delLine = false;

            if (key == Keys.Back)
            {
                if (t.Length > 0)
                    t = t.Substring(0, t.Length - 1);
                else if (_editMode == EditingMode.Script)
                    delLine = ScriptDelLine();
            }
            else if (key == Keys.Enter)
            {
                if (_editMode == EditingMode.Script)
                {
                    if (ScriptEnter())
                        t = "";
                }
                else
                    _editMode = EditingMode.None;
            }
            else
            {
                t = (t + (char)key).ToLower();
            }

            if (!delLine)
            {
                switch (_editMode)
                {
                    case EditingMode.Path:
                        _map.Path = t;
                        break;
                    case EditingMode.Script:
                        _map.Scripts[_selScript] = t;
                        break;
                }
            }
            else _selScript--;
        }

        protected override void Draw(GameTime gameTime)
        {
            _graphics.GraphicsDevice.Clear(Color.CornflowerBlue);

            #region Part One

            /*
            text.Size = 3.0f;
            text.Color = new Color(0, 0, 0, 125);
            for (int i = 0; i < 3; i++)
            {
                if (i == 2)
                    text.Color = Color.White;

                text.DrawText(25 - i * 2, 250 - i * 2, "Zombie Smashers XNA FTW!");
            }
            */

            #endregion

            _map.Draw(_spriteBatch, _mapsTex, _scroll);

            switch (_drawType)
            {
                case DrawingMode.SegmentSelection:
                    DrawMapSegments();
                    break;
                case DrawingMode.Ledges:
                    DrawLedgePalette();
                    break;
            }

            if (DrawButton(5, 65, 3, _mosX, _mosY, _mouseClick))
                _map.Write();

            if (DrawButton(40, 65, 4, _mosX, _mosY, _mouseClick))
                _map.Read();

            DrawGrid();
            DrawLedges();
            DrawText();
            DrawCursor();

            base.Draw(gameTime);
        }

        private bool DrawButton(int x, int y, int index, int mosX, int mosY, bool mouseClick)
        {
            var r = false;

            var sRect = new Rectangle(32 * (index % 8), 32 * (index / 8), 32, 32);
            var dRect = new Rectangle(x, y, 32, 32);

            if (dRect.Contains(mosX, mosY))
            {
                dRect.X -= 1;
                dRect.Y -= 1;
                dRect.Width += 2;
                dRect.Height += 2;

                if (mouseClick)
                    r = true;
            }

            _spriteBatch.Begin();
            _spriteBatch.Draw(_iconsTex, dRect, sRect, Color.White);
            _spriteBatch.End();

            return r;
        }

        private void DrawLedgePalette()
        {
            for (var i = 0; i < 16; i++)
            {
                if (_map.Ledges[i] == null)
                    continue;

                var y = 50 + i * 20;
                if (_curLedge == i)
                {
                    _text.Color = Color.Lime;
                    _text.DrawText(520, y, "ledge " + i);
                }
                else
                {
                    if (_text.DrawClickText(520, y, "ledge " + i,
                        _mosX, _mosY, _mouseClick))
                        _curLedge = i;
                }

                _text.Color = Color.White;
                _text.DrawText(620, y, "n" + _map.Ledges[i].TotalNodes);

                if (_text.DrawClickText(680, y, "f" + _map.Ledges[i].Flags, _mosX, _mosY, _mouseClick))
                    _map.Ledges[i].Flags = (_map.Ledges[i].Flags + 1) % 2;
            }
        }

        private void DrawGrid()
        {
            _spriteBatch.Begin();

            for (var y = 0; y < 20; y++)
            {
                for (var x = 0; x < 20; x++)
                {
                    var dRect = new Rectangle(
                        x * 32 - (int)(_scroll.X / 2),
                        y * 32 - (int)(_scroll.Y / 2),
                        32,
                        32
                        );

                    if (x < 19)
                        _spriteBatch.Draw(_nullTex,
                            new Rectangle(dRect.X, dRect.Y, 32, 1),
                            new Color(255, 0, 0, 100));

                    if (y < 19)
                        _spriteBatch.Draw(_nullTex,
                            new Rectangle(dRect.X, dRect.Y, 1, 32),
                            new Color(255, 0, 0, 100));

                    if (x < 19 && y < 19)
                    {
                        if (_map.Grid[x, y] == 1)
                            _spriteBatch.Draw(_nullTex, dRect,
                                new Color(255, 0, 0, 100));
                    }
                }
            }

            var oColor = new Color(255, 255, 255, 100);
            _spriteBatch.Draw(_nullTex, new Rectangle(100, 50, 400, 1), oColor);
            _spriteBatch.Draw(_nullTex, new Rectangle(100, 50, 1, 500), oColor);
            _spriteBatch.Draw(_nullTex, new Rectangle(500, 50, 1, 500), oColor);
            _spriteBatch.Draw(_nullTex, new Rectangle(100, 550, 400, 1), oColor);

            _spriteBatch.End();
        }

        private void DrawLedges()
        {
            var rect = new Rectangle();

            _spriteBatch.Begin();

            rect.X = 32;
            rect.Y = 0;
            rect.Width = 32;
            rect.Height = 32;

            for (var i = 0; i < 16; i++)
            {
                if (_map.Ledges[i] != null && _map.Ledges[i].TotalNodes > 0)
                {
                    for (var n = 0; n < _map.Ledges[i].TotalNodes; n++)
                    {
                        var tVec = _map.Ledges[i].Nodes[n];
                        tVec -= _scroll / 2.0f;
                        tVec.X -= 5.0f;

                        var tColor = _curLedge == i ? Color.Yellow : Color.White;

                        _spriteBatch.Draw(_iconsTex, tVec, rect, tColor,
                            0.0f, Vector2.Zero, 0.35f, SpriteEffects.None, 0.0f);

                        if (n < _map.Ledges[i].TotalNodes - 1)
                        {
                            var nVec = _map.Ledges[i].Nodes[n + 1];
                            nVec -= _scroll / 2.0f;
                            nVec.X -= 4.0f;

                            for (var x = 1; x < 20; x++)
                            {
                                var iVec = (nVec - tVec) * (x / 20f) + tVec;

                                var nColor = new Color(255, 255, 255, 75);

                                if (_map.Ledges[i].Flags == 1)
                                    nColor = new Color(255, 0, 0, 75);

                                _spriteBatch.Draw(_iconsTex, iVec, rect, nColor, 0.0f, Vector2.Zero, 0.25f,
                                    SpriteEffects.None, 0.0f);
                            }
                        }
                    }
                }
            }

            _spriteBatch.End();
        }

        private void DrawText()
        {
            var layerName = "map";
            switch (_curLayer)
            {
                case 0:
                    layerName = "back";
                    break;
                case 1:
                    layerName = "mid";
                    break;
                case 2:
                    layerName = "fore";
                    break;
            }

            if (_text.DrawClickText(5, 5, "layer: " + layerName, _mosX, _mosY, _mouseClick))
                _curLayer = (_curLayer + 1) % 3;

            switch (_drawType)
            {
                case DrawingMode.SegmentSelection:
                    layerName = "select";
                    break;
                case DrawingMode.CollisionMap:
                    layerName = "col";
                    break;
                case DrawingMode.Ledges:
                    layerName = "ledge";
                    break;
                case DrawingMode.Script:
                    layerName = "script";
                    break;
            }

            if (_text.DrawClickText(5, 25, "draw: " + layerName, _mosX, _mosY, _mouseClick))
                _drawType = (DrawingMode)(((int)_drawType + 1) % 4);

            if (_drawType == DrawingMode.Script)
            {
                _spriteBatch.Begin();
                _spriteBatch.Draw(_nullTex, new Rectangle(400, 20, 400, 565), new Color(new Vector4(0f, 0f, 0f, .62f)));
                _spriteBatch.End();

                for (var i = _scriptScroll; i < _scriptScroll + 28; i++)
                {
                    if (_selScript == i)
                    {
                        _text.Color = Color.White;
                        _text.DrawText(405, 25 + (i - _scriptScroll) * 20, i + ": " + _map.Scripts[i] + "*");
                    }
                    else
                    {
                        if (_text.DrawClickText(405, 25 + (i - _scriptScroll) * 20, i + ": " + _map.Scripts[i], _mosX,
                            _mosY, _mouseClick))
                        {
                            _selScript = i;
                            _editMode = EditingMode.Script;
                        }
                    }

                    if (_map.Scripts[i].Length > 0)
                    {
                        var split = _map.Scripts[i].Split(' ');
                        var c = GetCommandColor(split[0]);
                        if (c > ColorNone)
                        {
                            switch (c)
                            {
                                case ColorGreen:
                                    _text.Color = Color.Lime;
                                    break;
                                case ColorYellow:
                                    _text.Color = Color.Yellow;
                                    break;
                            }
                            _text.DrawText(405, 25 + (i - _scriptScroll) * 20, i + ": " + split[0]);
                        }
                    }

                    _text.Color = Color.White;
                    _text.DrawText(405, 25 + (i - _scriptScroll) * 20, i + ": ");
                }

                var mouseDown = (Mouse.GetState().LeftButton == ButtonState.Pressed);
                if (DrawButton(770, 20, 1, _mosX, _mosY, mouseDown) && _scriptScroll > 0) _scriptScroll--;
                if (DrawButton(770, 550, 2, _mosX, _mosY, mouseDown) && _scriptScroll < _map.Scripts.Length - 28)
                    _scriptScroll++;
            }

            _text.Color = Color.White;

            if (_editMode == EditingMode.Path)
                _text.DrawText(5, 45, _map.Path + "*");
            else
            {
                if (_text.DrawClickText(5, 45, _map.Path, _mosX, _mosY, _mouseClick))
                    _editMode = EditingMode.Path;
            }

            _mouseClick = false;
        }

        private void DrawCursor()
        {
            _spriteBatch.Begin();

            _spriteBatch.Draw(_iconsTex,
                new Vector2(_mosX, _mosY),
                new Rectangle(0, 0, 32, 32),
                Color.White, 0.0f,
                new Vector2(0, 0),
                1.0f,
                SpriteEffects.None,
                0.0f);

            _spriteBatch.End();
        }

        private void DrawMapSegments()
        {
            var dRect = new Rectangle();

            _text.Size = 0.8f;

            _spriteBatch.Begin();
            _spriteBatch.Draw(_nullTex, new Rectangle(500, 20, 280, 550), new Color(0, 0, 0, 100));
            _spriteBatch.End();

            for (var i = 0; i < 9; i++)
            {
                var segDef = _map.SegmentDefinitions[i];

                if (segDef == null)
                    continue;

                _spriteBatch.Begin();

                dRect.X = 500;
                dRect.Y = 50 + i * 60;

                var sRect = segDef.SourceRect;

                if (sRect.Width > sRect.Height)
                {
                    dRect.Width = 45;
                    dRect.Height = (int)((sRect.Height / (float)sRect.Width) * 45.0f);
                }
                else
                {
                    dRect.Height = 45;
                    dRect.Width = (int)((sRect.Width / (float)sRect.Height) * 45.0f);
                }

                _spriteBatch.Draw(_mapsTex[segDef.SourceIndex],
                    dRect,
                    sRect,
                    Color.White
                    );

                _spriteBatch.End();

                _text.Color = Color.White;

                _text.DrawText(dRect.X + 50, dRect.Y, segDef.Name);

                if (_rightMouseDown)
                {
                    if (_mosX > dRect.X && _mosX < 780 && _mosY > dRect.Y && _mosY < dRect.Y + 45)
                    {
                        if (_mouseDragSeg == -1)
                        {
                            var f = _map.AddSeg(_curLayer, i);

                            if (f <= -1)
                                continue;

                            var layerScalar = 0.5f;
                            if (_curLayer == 0)
                                layerScalar = 0.375f;
                            else if (_curLayer == 2)
                                layerScalar = 0.625f;

                            _map.Segments[_curLayer, f].Location.X = (_mosX - sRect.Width / 4 + _scroll.X * layerScalar);
                            _map.Segments[_curLayer, f].Location.Y = (_mosY - sRect.Height / 4 + _scroll.Y * layerScalar);

                            _mouseDragSeg = f;
                        }
                    }
                }
            }
        }

        private int GetCommandColor(string s)
        {
            switch (s)
            {
                case "fog":
                case "monster":
                case "makebucket":
                case "addbucket":
                case "ifnotbucketgoto":
                case "wait":
                case "setflag":
                case "iftruegoto":
                case "iffalsegoto":
                case "setglobalflag":
                case "ifglobaltruegoto":
                case "ifglobalfalsegoto":
                case "stop":
                    return ColorGreen;
                case "tag":
                    return ColorYellow;
            }
            return ColorNone;
        }

        private bool ScriptEnter()
        {
            if (_selScript >= _map.Scripts.Length - 1)
                return false;

            for (var i = _map.Scripts.Length - 1; i > _selScript; i--)
                _map.Scripts[i] = _map.Scripts[i - 1];

            _selScript++;
            return true;
        }

        private bool ScriptDelLine()
        {
            if (_selScript <= 0) 
                return false;

            for (var i = _selScript; i < _map.Scripts.Length - 1; i++)
                _map.Scripts[i] = _map.Scripts[i + 1];

            return true;
        }
    }
}